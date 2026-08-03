# 매치메이킹 동시성 DB 통합 테스트 — 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 진짜 Postgres를 띄워 확정과 취소가 부딪히는 상황을 자동 재현하는 테스트를 만든다. 지금 주석에만 적혀 있는 안전성 주장이 실행되는 증거가 된다.

**Architecture:** `@testcontainers/postgresql`로 일회용 Postgres를 띄우고 `prisma migrate deploy`로 스키마를 올린다. 모듈이 로드되기 전에 env를 그 컨테이너로 맞춰, **실제 DAO 코드**가 그대로 컨테이너를 보게 한다. 유닛 테스트와 섞지 않도록 jest 설정·스크립트를 따로 둔다.

**Tech Stack:** jest 29 + ts-jest, `@testcontainers/postgresql`, Prisma 6, Postgres 16

**작업 저장소:** `C:\Users\re5na\workspace\LOP\lop-backend` (설계 문서는 클라 repo, **코드는 전부 lop-backend**)

**설계 문서:** `docs/superpowers/specs/2026-08-03-matchmaking-db-integration-tests-design.md` (클라 repo)

## Global Constraints

- **로직을 다시 짜지 않는다.** 테스트는 실제 DAO 메서드(`MatchDaoPostgres.saveWithRounds`, `MatchmakingTicketDaoPostgres.deleteByIdIfUnconsumed`)를 호출한다. SQL을 손으로 재현하면 배포되는 코드를 지키지 못한다.
- **테스트를 트랜잭션으로 감싸 롤백하는 격리 방식을 쓰지 않는다.** 커넥션 둘이 서로의 커밋을 봐야 하는 것이 검증 대상이다. 정리는 `deleteMany`로 한다.
- **워커 1개로 직렬 실행**(`maxWorkers: 1`) — DB 하나를 공유한다.
- **DAO 레이어에서 테스트한다.** 리포지토리 레이어는 Redis 캐시 무효화가 얽혀 Redis 컨테이너가 필요해진다 — 그건 다음 단계(B) 몫이다.
- **"막혔다"는 순서로 증명한다.** 결과가 0건인 것만으로는 부족하다(애초에 못 찾아도 0이다). 취소 완료가 Director 커밋 **이후**여야 한다.
- 한글 주석, 한국어 커밋 메시지. 주석은 "왜"만 짧게.

## File Structure

**새로 만드는 것** (전부 `apps/matchmaking-server/`):

| 파일 | 책임 |
|---|---|
| `jest.integration.config.js` | 통합 테스트 전용 jest 설정 |
| `test/integration/globalSetup.ts` | 컨테이너 기동 + 마이그레이션 + env 세팅 |
| `test/integration/globalTeardown.ts` | 컨테이너 정리 |
| `test/integration/db.ts` | 테스트가 쓰는 원시 Prisma 클라이언트 + 정리 헬퍼 |
| `test/integration/ticketClaim.integration.test.ts` | 시나리오 ①~④ |

**고치는 것:**

| 파일 | 무엇 |
|---|---|
| `apps/matchmaking-server/package.json` | `test:integration` 스크립트 + devDependency |
| `.github/workflows/backend-deploy.yml` | 통합 테스트 단계 추가 |

---

### Task 1: 하니스 (컨테이너 + 마이그레이션 + 스모크)

이 태스크가 끝나면 "컨테이너가 뜨고, 스키마가 올라가고, **실제 DAO가 그 컨테이너를 본다**"가 증명된다.

**Files:**
- Create: `apps/matchmaking-server/jest.integration.config.js`
- Create: `apps/matchmaking-server/test/integration/globalSetup.ts`
- Create: `apps/matchmaking-server/test/integration/globalTeardown.ts`
- Create: `apps/matchmaking-server/test/integration/db.ts`
- Create: `apps/matchmaking-server/test/integration/smoke.integration.test.ts` (Task 2에서 삭제)
- Modify: `apps/matchmaking-server/package.json`

**Interfaces:**
- Produces: `db.ts`가 `rawPrisma`(테스트용 원시 클라이언트)와 `resetTables()`를 export. Task 2가 쓴다.

- [ ] **Step 1: 의존성 추가**

Run: `cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm --filter matchmaking-server add -D @testcontainers/postgresql`
Expected: 설치 성공. `apps/matchmaking-server/package.json`의 `devDependencies`에 항목이 생긴다.

- [ ] **Step 2: `package.json`에 스크립트 추가**

`"test": "jest"` 바로 다음 줄에 추가:
```json
        "test:integration": "jest --config jest.integration.config.js",
```

- [ ] **Step 3: jest 통합 설정 작성**

`apps/matchmaking-server/jest.integration.config.js`:
```javascript
const base = require('./jest.config');

/** @type {import('ts-jest').JestConfigWithTsJest} */
module.exports = {
    ...base,
    //  유닛과 파일 위치가 겹치지 않게 test/ 아래만 본다.
    testMatch: ['<rootDir>/test/integration/**/*.integration.test.ts'],
    globalSetup: '<rootDir>/test/integration/globalSetup.ts',
    globalTeardown: '<rootDir>/test/integration/globalTeardown.ts',
    //  DB 하나를 공유하므로 병렬로 돌리면 서로의 데이터를 지운다.
    maxWorkers: 1,
    //  컨테이너 기동(약 10초)이 있어 기본 5초로는 모자란다.
    testTimeout: 60000,
};
```

- [ ] **Step 4: globalSetup 작성**

`apps/matchmaking-server/test/integration/globalSetup.ts`:
```typescript
import { PostgreSqlContainer, StartedPostgreSqlContainer } from '@testcontainers/postgresql';
import { execFileSync } from 'child_process';
import { join } from 'path';

//  globalTeardown에 컨테이너를 넘기는 통로. jest가 두 훅 사이에 공유해 주는 값이다.
declare global {
    var __PG_CONTAINER__: StartedPostgreSqlContainer | undefined;
}

export default async function globalSetup(): Promise<void> {
    const container = await new PostgreSqlContainer('postgres:16-alpine').start();
    globalThis.__PG_CONTAINER__ = container;

    const databaseUrl = container.getConnectionUri();

    //  스키마는 앱이 아니라 마이그레이션이 만든다 — 운영과 같은 경로여야 의미가 있다.
    const databaseDir = join(__dirname, '..', '..', '..', '..', 'packages', 'database');
    execFileSync('npx', ['prisma', 'migrate', 'deploy'], {
        cwd: databaseDir,
        env: { ...process.env, DATABASE_URL: databaseUrl },
        stdio: 'inherit',
        shell: true,
    });

    //  실제 DAO는 @lop/server-core/postgres의 prismaClient를 쓰고, 그 URL은 이 env들로 조립된다.
    //  테스트 모듈이 로드되기 전에 여기서 맞춰야 DAO가 컨테이너를 본다.
    process.env.POSTGRES_HOST = container.getHost();
    process.env.POSTGRES_PORT = String(container.getPort());
    process.env.POSTGRES_USER = container.getUsername();
    process.env.POSTGRES_PASSWORD = container.getPassword();
    process.env.POSTGRES_DATABASE = container.getDatabase();
    //  테스트가 직접 쓰는 원시 클라이언트용.
    process.env.TEST_DATABASE_URL = databaseUrl;
}
```

- [ ] **Step 5: globalTeardown 작성**

`apps/matchmaking-server/test/integration/globalTeardown.ts`:
```typescript
export default async function globalTeardown(): Promise<void> {
    await globalThis.__PG_CONTAINER__?.stop();
}
```

- [ ] **Step 6: 테스트용 DB 헬퍼 작성**

`apps/matchmaking-server/test/integration/db.ts`:
```typescript
import { PrismaClient } from '@lop/database';

//  테스트가 데이터를 심고 결과를 확인할 때 쓰는 클라이언트. DAO가 쓰는 것과 다른 커넥션이라
//  "커넥션 둘이 서로의 커밋을 본다"를 확인할 수 있다.
export const rawPrisma = new PrismaClient({
    datasources: { db: { url: process.env.TEST_DATABASE_URL } },
});

/** 테스트 사이 정리. 트랜잭션 롤백식 격리는 검증 대상을 가리므로 지우는 방식으로 한다. */
export async function resetTables(): Promise<void> {
    await rawPrisma.matchRound.deleteMany({});
    await rawPrisma.match.deleteMany({});
    await rawPrisma.matchmakingTicket.deleteMany({});
}

export async function createTicket(id: string, userId: string): Promise<void> {
    await rawPrisma.matchmakingTicket.create({
        data: { id, userIds: [userId], queueId: 1, gameModeIds: [1], mapIds: [1], rating: 1000 },
    });
}
```

- [ ] **Step 7: 스모크 테스트 작성 — 실제 DAO가 컨테이너를 보는지**

`apps/matchmaking-server/test/integration/smoke.integration.test.ts`:
```typescript
import { rawPrisma, resetTables, createTicket } from './db';
import { MatchmakingTicketDaoPostgres } from '@daos/matchmakingTicket.dao.postgres';

//  하니스가 성립하는지만 본다 — 실제 DAO가 테스트 컨테이너를 보고 있어야 한다.
//  (여기서 실패하면 env가 모듈 로드보다 늦게 세팅된 것이다.)
describe('통합 테스트 하니스', () => {
    beforeEach(resetTables);
    afterAll(async () => { await rawPrisma.$disconnect(); });

    it('실제 DAO가 테스트 컨테이너의 데이터를 읽는다', async () => {
        await createTicket('T1', 'U1');

        const dao = new MatchmakingTicketDaoPostgres();
        const tickets = await dao.findAllUnconsumed();

        expect(tickets.map(t => t.id)).toEqual(['T1']);
    });
});
```

- [ ] **Step 8: 돌려서 통과 확인**

Run: `cd /c/Users/re5na/workspace/LOP/lop-backend/apps/matchmaking-server && npm run test:integration`
Expected: 컨테이너 기동 로그 + 마이그레이션 적용 + `1 passed`

> **걸릴 수 있는 두 곳 — 막히면 이렇게 판단한다:**
>
> 1. **`globalSetup`을 TS로 못 읽는다** (`SyntaxError: Unexpected token 'import'` 등). jest가
>    globalSetup에 transform을 적용하지 못하는 경우다. → 그 두 파일만 **`.js`(CommonJS)로 바꾼다**
>    (`globalSetup.js` / `globalTeardown.js`, `require`/`module.exports` 사용). 컨테이너를 띄우고
>    env를 세팅하는 일이라 타입이 주는 이득이 작다. 설정의 경로도 함께 고칠 것.
> 2. **스모크 테스트가 "실제 DAO가 컨테이너를 본다"에서 실패한다** — DAO가 엉뚱한 DB(로컬/운영)를
>    보고 있다는 뜻이다. `globalSetup`에서 세팅한 `process.env`가 테스트 워커까지 전달되지 않은
>    것이므로, **env 세팅을 `setupFiles`로 옮긴다**(워커 안에서, 테스트 모듈 로드 전에 실행된다).
>    그 경우 컨테이너 접속 정보는 `globalSetup`이 임시 파일이나 `process.env`에 남기고 `setupFiles`가
>    읽는다. ⚠️ **이 실패를 "DB가 없다"로 오해하지 말 것** — 배선이 잘못된 것이다.

- [ ] **Step 9: 유닛 테스트가 영향받지 않았는지 확인**

Run: `cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm exec turbo run test`
Expected: matchmaking 20 suites / 155 tests, room 1 suite / 11 tests — **통합 테스트는 여기 안 잡혀야 한다**(별도 설정이므로)

- [ ] **Step 10: 커밋**

```bash
git add apps/matchmaking-server/jest.integration.config.js apps/matchmaking-server/test apps/matchmaking-server/package.json pnpm-lock.yaml
git commit -m "test(matchmaking): DB 통합 테스트 하니스 추가

testcontainers로 일회용 Postgres를 띄우고 prisma migrate deploy로 스키마를
올린다. 모듈 로드 전에 env를 맞춰 실제 DAO가 그 컨테이너를 보게 한다.
유닛과 섞이지 않도록 jest 설정과 스크립트를 분리했다."
```

---

### Task 2: 동시성 시나리오 4종

**Files:**
- Create: `apps/matchmaking-server/test/integration/ticketClaim.integration.test.ts`
- Delete: `apps/matchmaking-server/test/integration/smoke.integration.test.ts` (역할을 이 파일이 흡수)

**Interfaces:**
- Consumes: Task 1의 `db.ts` (`rawPrisma`, `resetTables`, `createTicket`)

- [ ] **Step 1: 테스트 파일 작성**

`apps/matchmaking-server/test/integration/ticketClaim.integration.test.ts`:
```typescript
import { rawPrisma, resetTables, createTicket } from './db';
import { MatchDaoPostgres } from '@daos/match.dao.postgres';
import { MatchmakingTicketDaoPostgres } from '@daos/matchmakingTicket.dao.postgres';

const 매치 = (id: string, players: string[]) => ({
    id, queueId: 1, targetRating: 1000, createdAt: new Date(), playerList: players,
});
const 라운드 = (matchId: string) => [{ id: `${matchId}-r0`, matchId, index: 0, gameModeId: 1, mapId: 1 }];

describe('티켓 선점 — 확정과 취소가 부딪힐 때', () => {
    let matchDao: MatchDaoPostgres;
    let ticketDao: MatchmakingTicketDaoPostgres;

    beforeEach(async () => {
        await resetTables();
        matchDao = new MatchDaoPostgres();
        ticketDao = new MatchmakingTicketDaoPostgres();
    });
    afterAll(async () => { await rawPrisma.$disconnect(); });

    it('하니스 점검 — 실제 DAO가 테스트 컨테이너를 본다', async () => {
        await createTicket('T1', 'U1');
        const tickets = await ticketDao.findAllUnconsumed();
        expect(tickets.map(t => t.id)).toEqual(['T1']);
    });

    //  ① 확정이 티켓을 선점한 채 아직 커밋 전인데 취소가 들어오는 경우.
    //  검증 대상은 *취소 쪽 실제 코드*다. 확정 쪽은 실제 코드가 그 시점에 하는 것과 똑같은
    //  UPDATE를 연 채로 붙들어 대신한다 — 실제 saveWithRounds는 중간에서 멈출 수단이 없다.
    it('확정이 선점 중이면 취소는 기다렸다가 아무것도 못 지운다', async () => {
        await createTicket('T1', 'U1');

        let 커밋시각 = 0;
        let 취소완료시각 = 0;
        const 붙드는시간 = 800;

        const 확정중 = rawPrisma.$transaction(async (tx) => {
            await tx.matchmakingTicket.updateMany({
                where: { id: { in: ['T1'] }, matchId: null },
                data: { matchId: 'M1' },
            });
            await new Promise(resolve => setTimeout(resolve, 붙드는시간));
            커밋시각 = Date.now();
        }, { timeout: 20000, maxWait: 20000 });

        //  확정이 선점을 끝낸 뒤에 취소가 들어가도록 조금 늦춘다.
        await new Promise(resolve => setTimeout(resolve, 200));
        const 취소 = ticketDao.deleteByIdIfUnconsumed('T1')
            .then(count => { 취소완료시각 = Date.now(); return count; });

        const [, 지운개수] = await Promise.all([확정중, 취소]);

        expect(지운개수).toBe(0);
        //  결과가 0인 것만으로는 "기다렸다"를 증명하지 못한다 — 순서로 증명한다.
        expect(취소완료시각).toBeGreaterThanOrEqual(커밋시각);
        //  티켓은 지워지지 않고 소비 표시된 채로 남아 있어야 한다.
        const 남은티켓 = await rawPrisma.matchmakingTicket.findUnique({ where: { id: 'T1' } });
        expect(남은티켓?.matchId).toBe('M1');
    });

    //  ② 취소가 먼저 커밋된 뒤 확정이 시도되는 경우. 여기선 양쪽 다 실제 코드다.
    it('취소가 먼저면 확정은 매치·라운드까지 통째로 롤백된다', async () => {
        await createTicket('T1', 'U1');
        await createTicket('T2', 'U2');

        expect(await ticketDao.deleteByIdIfUnconsumed('T1')).toBe(1);

        await expect(
            matchDao.saveWithRounds(매치('M1', ['U1', 'U2']), 라운드('M1'), ['U1', 'U2'], ['T1', 'T2']),
        ).rejects.toThrow(/Ticket claim failed/);

        expect(await rawPrisma.match.count()).toBe(0);
        expect(await rawPrisma.matchRound.count()).toBe(0);
        //  함께 묶였던 애꿎은 티켓은 풀에 그대로 남아 다음 틱에 다시 매칭돼야 한다.
        const 남은티켓 = await rawPrisma.matchmakingTicket.findMany({ select: { id: true, matchId: true } });
        expect(남은티켓).toEqual([{ id: 'T2', matchId: null }]);
    });

    //  ③ 넓히기 단계는 개수를 비교하지 않는다. 비교하도록 잘못 고치면 정상 매칭이 통째로 롤백된다.
    it('같은 유저의 여분 티켓이 있어도 정상 매치는 성공하고 여분도 함께 소비된다', async () => {
        await createTicket('T1', 'U1');
        await createTicket('T2', 'U2');
        await createTicket('T3', 'U1');   //  U1이 중복 발급받은 여분

        const { consumedTicketIds } = await matchDao.saveWithRounds(
            매치('M1', ['U1', 'U2']), 라운드('M1'), ['U1', 'U2'], ['T1', 'T2'],
        );

        expect(consumedTicketIds.sort()).toEqual(['T1', 'T2', 'T3']);
        expect(await rawPrisma.match.count()).toBe(1);
        //  여분까지 소비돼야 다음 틱이 같은 사람을 또 매칭하지 않는다.
        expect(await ticketDao.findAllUnconsumed()).toEqual([]);
    });

    //  ④ 이미 매치가 가져간 티켓은 취소되지 않는다.
    it('이미 소비된 티켓은 취소해도 지워지지 않는다', async () => {
        await createTicket('T1', 'U1');
        await matchDao.saveWithRounds(매치('M1', ['U1']), 라운드('M1'), ['U1'], ['T1']);

        expect(await ticketDao.deleteByIdIfUnconsumed('T1')).toBe(0);
        expect(await rawPrisma.matchmakingTicket.count()).toBe(1);
    });
});
```

- [ ] **Step 2: 스모크 파일 삭제 (역할 흡수됨)**

Run: `rm apps/matchmaking-server/test/integration/smoke.integration.test.ts`

- [ ] **Step 3: 돌려서 5개 전부 통과 확인**

Run: `cd /c/Users/re5na/workspace/LOP/lop-backend/apps/matchmaking-server && npm run test:integration`
Expected: `Tests: 5 passed, 5 total`

- [ ] **Step 4: 변별력 확인 — 가드를 빼면 실패하는가**

테스트가 통과하는 것만으로는 부족하다. 지키려는 가드를 일부러 없애 보고 **실제로 잡는지** 확인한다.

`packages/server-core`가 아니라 `apps/matchmaking-server/src/daos/match.dao.postgres.ts`에서, 개수 비교 블록을 잠시 주석 처리한다:
```typescript
                // if (claimed.count !== requiredTicketIds.length) {
                //     throw new Error(...);
                // }
```

Run: `npm run test:integration`
Expected: **②가 실패한다** (`rejects.toThrow`가 안 나고 매치가 남는다)

되돌린 뒤 다시:
Run: `npm run test:integration`
Expected: 5개 전부 통과

이 확인 결과(어떤 테스트가 어떻게 실패했는지)를 보고에 남긴다.

- [ ] **Step 5: 커밋**

```bash
git add apps/matchmaking-server/test
git commit -m "test(matchmaking): 확정·취소 경합 시나리오 4종

선점 중 취소는 기다렸다 0건 / 취소가 먼저면 매치·라운드까지 롤백 /
여분 티켓이 있어도 정상 매치는 성공(넓히기는 개수를 안 본다) /
이미 소비된 티켓은 취소 불가.

변별력 확인: 개수 비교 가드를 빼면 두 번째가 실패한다."
```

---

### Task 3: CI 배선

**Files:**
- Modify: `.github/workflows/backend-deploy.yml`

- [ ] **Step 1: 통합 테스트 단계 추가**

`- name: 테스트 (turbo, 해당 앱 — 실패 시 이미지 빌드·푸시 중단)` 단계 **바로 다음**에 추가:

```yaml
      - name: 통합 테스트 (DB 필요 — matchmaking만)
        if: steps.filter.outputs.run == 'true' && matrix.app == 'matchmaking-server'
        #  확정/취소 경합 방어는 Postgres의 잠금 의미론에 기대는 주장이라, 진짜 DB로 돌려야 증거가 된다.
        #  testcontainers가 러너의 Docker로 일회용 Postgres를 띄운다.
        run: pnpm --filter matchmaking-server run test:integration
```

- [ ] **Step 2: YAML 문법 확인**

Run: `cd /c/Users/re5na/workspace/LOP/lop-backend && node -e "const fs=require('fs');const t=fs.readFileSync('.github/workflows/backend-deploy.yml','utf8');console.log('줄 수:', t.split('\n').length); console.log(t.includes('test:integration') ? '단계 추가됨' : '누락')"`
Expected: `단계 추가됨`

- [ ] **Step 3: 커밋**

```bash
git add .github/workflows/backend-deploy.yml
git commit -m "ci: matchmaking 통합 테스트 단계 추가

확정/취소 경합 방어는 Postgres 잠금 의미론에 기대는 주장이라 진짜 DB로
돌려야 증거가 된다. 안 돌리면 아무도 안 돌려서 서서히 썩는다."
```

---

## 실행 후 (컨트롤러가 직접)

1. **로컬 전체 확인** — `pnpm exec turbo run test`(유닛)과 `test:integration`(통합)이 각각 통과하는지.
2. **CI에서 실제로 도는지** — 이 단계가 GitHub Actions에서 처음 도는 것이므로, 배포를 걸어 **통합 테스트 단계가 초록인지 로그로 확인**한다. 여기서 죽으면 배포가 막히므로 반드시 눈으로 본다.
3. **E2E** — 이번 변경은 테스트만 추가하므로 런타임 동작은 변하지 않는다. 파드 기동·에러 0만 확인.
4. **ROADMAP 갱신 + 커밋**, 워크트리 머지.
5. 다음 단계(B·C 수정)는 별도 spec.
