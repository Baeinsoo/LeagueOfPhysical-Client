# 로비 자가치유 lost-update(B) — 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 로비가 유저 위치를 확인하는 사이 남이 쓴 값을 옛 값으로 덮어쓰는 것을 없앤다. 게임에 들어가야 할 사람이 로비로 튕기지 않는다.

**Architecture:** 자가치유를 **읽기 경로로만** 옮긴다. 쓰기 경로는 요청받은 값을 그대로 쓴다(이웃 서비스에 묻지 않는다). 읽기 경로는 **바뀐 게 없으면 아무것도 쓰지 않고**, 치유가 필요할 때만 **내가 본 상태 그대로일 때만** 쓴다(조건부 쓰기).

**Tech Stack:** Prisma 6 + PostgreSQL 16 + Redis, TypeScript/CJS, jest 29 + ts-jest, testcontainers

**작업 저장소:** `C:\Users\re5na\workspace\LOP\lop-backend` (설계 문서는 클라 repo, **코드는 전부 lop-backend**)

**설계 문서:** `docs/superpowers/specs/2026-08-04-matchmaking-race-fixes-design.md` **§5(B)**. §4(C)는 이미 완료·배포됨.

## Global Constraints

- **lobby-server에는 테스트가 하나도 없다.** jest 설정부터 만든다.
- **위치 저장소는 Postgres + Redis 캐시**다(`CacheCrudRepository`). 새로 만드는 쓰기가 `save()`를 우회하므로 **캐시 무효화를 직접 해야 한다** — 선례: `MatchmakingTicketRepository.deleteByIdIfUnconsumed`(DB 삭제 **뒤에** 캐시 삭제).
- **`locationDetail`은 이중 인코딩돼 있다** — jsonb 컬럼에 JSON *문자열*이 들어간다(운영 DB 확인: `"{\"location\":0}"`). 그래서 JSON path 필터는 안 먹고, 저장된 것과 **같은 문자열로 비교**해야 한다. 이 이중 인코딩 자체는 "유저 위치 전반 재정비" 트랙 몫이라 여기서 바꾸지 않는다.
- 한글 주석("왜"만 짧게), 한국어 커밋 메시지.
- 기존 베이스라인: matchmaking 유닛 21 suites/159, room 1/11, matchmaking 통합 2/14. **lobby는 0.**

## File Structure

| 파일 | 무엇 |
|---|---|
| `apps/lobby-server/jest.config.js` · `jest.integration.config.js` | 신설 |
| `apps/lobby-server/test/integration/{globalSetup,globalTeardown,db}.ts` | Postgres + Redis 컨테이너 |
| `apps/lobby-server/package.json` | `test` / `test:integration` 스크립트 + devDeps |
| `apps/lobby-server/src/daos/user-location.dao.postgres.ts` | `clearLocationIfUnchanged` |
| `apps/lobby-server/src/repositories/user-location.repository.ts` | 도메인 래퍼 + 캐시 무효화 + 캐시 우회 조회 |
| `apps/lobby-server/src/services/user-location.service.ts` | 쓰기 경로에서 자가치유 제거 / 읽기 경로 조건부 치유 |
| `.github/workflows/backend-ci.yml` · `backend-deploy.yml` | lobby 통합 테스트 단계 추가 |

---

### Task 1: lobby-server 테스트 기반

**Files:**
- Create: `apps/lobby-server/jest.config.js`, `jest.integration.config.js`
- Create: `apps/lobby-server/test/integration/globalSetup.ts`, `globalTeardown.ts`, `db.ts`
- Create: `apps/lobby-server/test/integration/smoke.integration.test.ts` (Task 3에서 실제 테스트로 교체)
- Modify: `apps/lobby-server/package.json`

> **하니스를 공용화하지 않는다.** matchmaking에 비슷한 게 있지만 lobby는 Redis가 더 붙어 모양이 다르고,
> 지금 둘뿐이다. **세 번째가 생기면** 그때 공용 패키지로 뽑는다.

- [ ] **Step 1: 의존성 + 스크립트**

Run: `cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm --filter lobby-server add -D jest ts-jest @types/jest @testcontainers/postgresql @testcontainers/redis`

`apps/lobby-server/package.json`의 `scripts`에 추가:
```json
        "test": "jest",
        "test:integration": "jest --config jest.integration.config.js",
```

- [ ] **Step 2: jest 설정 2개**

`apps/lobby-server/jest.config.js` — matchmaking 것을 참고하되 **lobby의 tsconfig `paths`와 맞춘다.**
`apps/lobby-server/tsconfig.json`의 `paths`를 그대로 `moduleNameMapper`로 옮기고, 아래 두 줄을 반드시 포함한다:
```javascript
        '^@lop/server-core$': '<rootDir>/../../packages/server-core/src',
        //  jest는 package.json의 exports 맵을 보지 않는다. 서브패스를 여기 적지 않으면 그것만
        //  node 해석을 타고 dist로 새어, 한 테스트 안에 src와 dist 두 벌이 뜬다.
        '^@lop/server-core/(.*)$': '<rootDir>/../../packages/server-core/src/entries/$1',
```
`testMatch`는 `['<rootDir>/src/**/__tests__/**/*.test.ts']`.

`apps/lobby-server/jest.integration.config.js`:
```javascript
const base = require('./jest.config');

/** @type {import('ts-jest').JestConfigWithTsJest} */
module.exports = {
    ...base,
    testMatch: ['<rootDir>/test/integration/**/*.integration.test.ts'],
    globalSetup: '<rootDir>/test/integration/globalSetup.ts',
    globalTeardown: '<rootDir>/test/integration/globalTeardown.ts',
    //  DB 하나를 공유하므로 병렬로 돌리면 서로의 데이터를 지운다.
    maxWorkers: 1,
    //  컨테이너 두 개 기동이 있어 기본 5초로는 모자란다.
    testTimeout: 60000,
};
```

- [ ] **Step 3: globalSetup — Postgres + Redis**

`apps/lobby-server/test/integration/globalSetup.ts`:
```typescript
import { PostgreSqlContainer, StartedPostgreSqlContainer } from '@testcontainers/postgresql';
import { RedisContainer, StartedRedisContainer } from '@testcontainers/redis';
import { execFileSync } from 'child_process';
import { join } from 'path';

declare global {
    var __PG_CONTAINER__: StartedPostgreSqlContainer | undefined;
    var __REDIS_CONTAINER__: StartedRedisContainer | undefined;
}

export default async function globalSetup(): Promise<void> {
    const [pg, redis] = await Promise.all([
        new PostgreSqlContainer('postgres:16-alpine').start(),
        new RedisContainer('redis:7-alpine').start(),
    ]);
    globalThis.__PG_CONTAINER__ = pg;
    globalThis.__REDIS_CONTAINER__ = redis;

    //  스키마는 앱이 아니라 마이그레이션이 만든다 — 운영과 같은 경로여야 의미가 있다.
    const databaseDir = join(__dirname, '..', '..', '..', '..', 'packages', 'database');
    execFileSync('npx', ['prisma', 'migrate', 'deploy'], {
        cwd: databaseDir,
        env: { ...process.env, DATABASE_URL: pg.getConnectionUri() },
        stdio: 'inherit',
        shell: true,
    });

    //  실제 DAO가 @lop/server-core의 클라이언트를 쓰고, 그 접속 정보는 이 env들로 조립된다.
    process.env.POSTGRES_HOST = pg.getHost();
    process.env.POSTGRES_PORT = String(pg.getPort());
    process.env.POSTGRES_USER = pg.getUsername();
    process.env.POSTGRES_PASSWORD = pg.getPassword();
    process.env.POSTGRES_DATABASE = pg.getDatabase();
    process.env.REDIS_HOST = redis.getHost();
    process.env.REDIS_PORT = String(redis.getFirstMappedPort());
    process.env.TEST_DATABASE_URL = pg.getConnectionUri();
}
```

`globalTeardown.ts`:
```typescript
export default async function globalTeardown(): Promise<void> {
    await Promise.all([
        globalThis.__PG_CONTAINER__?.stop(),
        globalThis.__REDIS_CONTAINER__?.stop(),
    ]);
}
```

- [ ] **Step 4: 테스트용 헬퍼**

`apps/lobby-server/test/integration/db.ts`:
```typescript
import { PrismaClient } from '@lop/database';
import { redisLoader, redisClient } from '@lop/server-core/redis';

//  테스트가 데이터를 심고 결과를 확인할 때 쓰는 클라이언트. DAO가 쓰는 것과 다른 커넥션이다.
export const rawPrisma = new PrismaClient({
    datasources: { db: { url: process.env.TEST_DATABASE_URL } },
});

//  redisClient는 모듈 로드 시 만들어지지만 connect()는 따로 해야 한다.
export async function connectRedis(): Promise<void> {
    if (!redisClient.isOpen) {
        await redisLoader.load();
    }
}

export async function resetAll(): Promise<void> {
    await rawPrisma.userLocation.deleteMany({});
    await redisClient.flushAll();
}

export async function disconnectAll(): Promise<void> {
    await rawPrisma.$disconnect();
    if (redisClient.isOpen) {
        await redisClient.quit();
    }
}
```

- [ ] **Step 5: 스모크 — 실제 리포지토리가 두 컨테이너를 보는지**

`apps/lobby-server/test/integration/smoke.integration.test.ts`:
```typescript
import { rawPrisma, connectRedis, resetAll, disconnectAll } from './db';
import { UserLocationRepository } from '@repositories/user-location.repository';
import { Location } from '@lop/server-core';

//  하니스가 성립하는지만 본다 — 실제 리포지토리가 테스트 컨테이너(Postgres+Redis)를 봐야 한다.
describe('lobby 통합 테스트 하니스', () => {
    beforeAll(connectRedis);
    beforeEach(resetAll);
    afterAll(disconnectAll);

    it('실제 리포지토리가 테스트 컨테이너를 읽고 쓴다', async () => {
        const repository = new UserLocationRepository();

        await repository.save({
            id: 'U1', location: Location.None, locationDetail: { location: Location.None } as any, timestamp: new Date(),
        });

        //  DB에 실제로 들어갔는지 다른 커넥션으로 확인한다.
        expect(await rawPrisma.userLocation.count()).toBe(1);
        //  캐시를 태운 조회도 같은 값을 준다.
        expect((await repository.findById('U1'))?.id).toBe('U1');
    });
});
```

- [ ] **Step 6: 돌려서 통과 확인**

Run: `cd /c/Users/re5na/workspace/LOP/lop-backend/apps/lobby-server && npm run test:integration`
Expected: 컨테이너 2개 기동 + 마이그레이션 + `1 passed`

> 막히면 판단 기준: **스모크가 실패하면 "DB가 없다"가 아니라 배선이 잘못된 것이다.** ①`globalSetup`을
> TS로 못 읽으면 그 두 파일만 `.js`(CommonJS)로 바꾼다. ②리포지토리가 엉뚱한 DB를 보면 env가 모듈
> 로드보다 늦은 것이므로 env 세팅을 `setupFiles`로 옮긴다. ③Redis 연결 실패면 `REDIS_HOST/PORT`가
> `@lop/server-core`의 `redisConnection` 조립과 맞는지 확인한다.

- [ ] **Step 7: 다른 앱에 영향 없는지 + 커밋**

Run: `cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm exec turbo run build --force && pnpm exec turbo run test`
Expected: 빌드 5/5. 테스트는 lobby가 새로 잡히는데 **유닛 테스트가 0개**라 jest가 실패할 수 있다 — 그러면 `jest.config.js`에 `passWithNoTests: true`를 넣는다(통합은 별도 설정이라 무관).

```bash
git add apps/lobby-server pnpm-lock.yaml
git commit -m "test(lobby): 통합 테스트 기반 신설 (Postgres + Redis)

lobby-server에는 테스트가 하나도 없었다. 위치 저장소가 캐시를 쓰므로
컨테이너 두 개를 띄운다. 하니스는 matchmaking 것과 모양이 달라(Redis 추가)
공용화하지 않는다 — 세 번째가 생기면 그때 뽑는다."
```

---

### Task 2: 쓰기 경로에서 자가치유 제거

**Files:**
- Modify: `apps/lobby-server/src/services/user-location.service.ts`
- Create: `apps/lobby-server/src/services/__tests__/user-location.service.update.test.ts`

**왜:** 이 API를 부르는 곳은 셋뿐이고 **전부 자기가 방금 만든 것을 기록한다**(매칭 요청/취소, Director 확정). 그런데 지금은 쓰기 경로에서도 자가치유가 돌아 **이웃 서비스에 HTTP로 되묻고**, 그 대답이 시원찮으면 **방금 받은 값을 `None`으로 지운다** — 쓰기가 자기를 되돌린다.

- [ ] **Step 1: 실패하는 테스트 먼저**

`apps/lobby-server/src/services/__tests__/user-location.service.update.test.ts`:
```typescript
import { Location, GameRoomLocationDetail } from '@lop/server-core';

const save = jest.fn();
const findAllById = jest.fn();
const findRoomById = jest.fn();
const findMatchmakingTicketById = jest.fn();

jest.mock('@repositories/user-location.repository', () => ({
    UserLocationRepository: jest.fn(() => ({ save, findAllById, findById: jest.fn() })),
}));
jest.mock('@services/room.service', () => ({ __esModule: true, default: jest.fn(() => ({ findRoomById })) }));
jest.mock('@services/matchmakingTicket.service', () => ({ __esModule: true, default: jest.fn(() => ({ findMatchmakingTicketById })) }));

import UserLocationService from '@services/user-location.service';

describe('UserLocationService.updateUserLocation', () => {
    beforeEach(() => {
        jest.clearAllMocks();
        findAllById.mockResolvedValue([{ id: 'U1', location: Location.None, locationDetail: {}, timestamp: new Date() }]);
        save.mockImplementation(async (v: any) => v);
    });

    //  호출자는 "이 상태로 해라"라고 단언한다 — 방금 만든 방을 이웃 서비스에 되물을 이유가 없고,
    //  그 대답이 시원찮다고 받은 값을 지워서도 안 된다.
    it('요청받은 값을 그대로 쓰고 이웃 서비스에 묻지 않는다', async () => {
        const service = new UserLocationService();

        await service.updateUserLocation({
            userLocations: [{
                userId: 'U1',
                location: Location.GameRoom,
                locationDetail: new GameRoomLocationDetail(Location.GameRoom, 'room-1'),
            }],
        } as any);

        expect(findRoomById).not.toHaveBeenCalled();
        expect(findMatchmakingTicketById).not.toHaveBeenCalled();
        expect(save).toHaveBeenCalledTimes(1);
        expect(save.mock.calls[0][0]).toMatchObject({ id: 'U1', location: Location.GameRoom });
    });

    //  이웃 서비스가 죽어 있어도 확정이 무효화되면 안 된다.
    it('이웃 서비스가 실패해도 요청받은 값이 저장된다', async () => {
        findRoomById.mockRejectedValue(new Error('room-server down'));
        const service = new UserLocationService();

        await service.updateUserLocation({
            userLocations: [{
                userId: 'U1',
                location: Location.GameRoom,
                locationDetail: new GameRoomLocationDetail(Location.GameRoom, 'room-1'),
            }],
        } as any);

        expect(save.mock.calls[0][0]).toMatchObject({ location: Location.GameRoom });
    });
});
```

- [ ] **Step 2: 실패 확인**

Run: `cd /c/Users/re5na/workspace/LOP/lop-backend/apps/lobby-server && npx jest src/services/__tests__/user-location.service.update`
Expected: 첫 테스트가 **실패**(`findRoomById`가 호출됨)

- [ ] **Step 3: 쓰기 경로에서 자가치유 빼기**

`user-location.service.ts`의 `updateUserLocation` 안에서
```typescript
                    userLocation = await this.verifyUserLocation(userLocation);
```
을 아래로 바꾼다:
```typescript
                    //  호출자가 "이 상태로 해라"라고 단언하는 자리다 — 방금 만든 것을 이웃 서비스에
                    //  되묻는 낭비이고, 그 대답이 시원찮으면 받은 값을 None으로 지워 **확정을 무효화**한다.
                    //  잘못된 상태가 저장돼도 안전망은 읽기 경로(getOrCreateUserLocationById)에 있다.
                    userLocation.timestamp = new Date();
                    userLocation = await this.userLocationRepository.save(userLocation);
```

- [ ] **Step 4: 통과 확인 + 빌드**

Run: `npx jest src/services/__tests__/user-location.service.update` → `2 passed`
Run: `cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm exec turbo run build --force` → 5/5

- [ ] **Step 5: 커밋**

```bash
git add apps/lobby-server/src apps/lobby-server/src/services/__tests__
git commit -m "fix(lobby): 쓰기 경로에서 자가치유 제거

이 API를 부르는 세 곳이 전부 자기가 방금 만든 것을 기록한다. 그런데 쓰기
경로에서도 자가치유가 돌아 이웃 서비스에 되묻고, 대답이 시원찮으면 방금
받은 값을 None으로 지웠다 — 쓰기가 자기를 되돌린다.

안전망은 읽기 경로에 그대로 있다. 덤으로 확정 경로에서 HTTP 왕복이 하나
사라져 다른 경합의 창도 줄어든다."
```

---

### Task 3: 읽기 경로 — 안 바뀌면 안 쓰기 + 조건부 치유

**Files:**
- Modify: `apps/lobby-server/src/daos/user-location.dao.postgres.ts`
- Modify: `apps/lobby-server/src/repositories/user-location.repository.ts`
- Modify: `apps/lobby-server/src/services/user-location.service.ts`
- Create: `apps/lobby-server/test/integration/selfHeal.integration.test.ts`
- Delete: `apps/lobby-server/test/integration/smoke.integration.test.ts`

**Interfaces:**
- Produces:
  - `UserLocationDaoPostgres.clearLocationIfUnchanged(id, seenLocation, seenDetailJson): Promise<number>` — 조건이 맞아 지운 행 수(0 또는 1)
  - `UserLocationRepository.clearLocationIfUnchanged(seen): Promise<boolean>` — 캐시 무효화 포함
  - `UserLocationRepository.findByIdBypassingCache(id)`

- [ ] **Step 1: 조건부 쓰기가 실제로 되는지부터 확인 (통합 테스트 먼저)**

`apps/lobby-server/test/integration/selfHeal.integration.test.ts`:
```typescript
import { rawPrisma, connectRedis, resetAll, disconnectAll } from './db';
import { UserLocationRepository } from '@repositories/user-location.repository';
import { Location, MatchmakingLocationDetail } from '@lop/server-core';
import * as Entity from '@lop/database';

const 매칭중 = (ticketId: string) => ({
    id: 'U1',
    location: Location.Matchmaking,
    locationDetail: new MatchmakingLocationDetail(Location.Matchmaking, ticketId),
    timestamp: new Date(),
});

describe('위치 조건부 쓰기', () => {
    let repository: UserLocationRepository;

    beforeAll(connectRedis);
    beforeEach(async () => {
        await resetAll();
        repository = new UserLocationRepository();
    });
    afterAll(disconnectAll);

    it('내가 본 상태 그대로면 치유한다', async () => {
        const seen = 매칭중('t1');
        await repository.save(seen);

        expect(await repository.clearLocationIfUnchanged(seen)).toBe(true);

        const row = await rawPrisma.userLocation.findUnique({ where: { id: 'U1' } });
        expect(row?.location).toBe(Entity.Location.None);
    });

    //  이게 이 작업의 핵심 — 판단 근거가 바뀌었으면 치유하면 안 된다.
    it('그 사이 다른 값이 쓰였으면 치유하지 않는다', async () => {
        const seen = 매칭중('t1');
        await repository.save(seen);

        //  Director가 게임방 입장을 기록한 상황
        await rawPrisma.userLocation.update({
            where: { id: 'U1' },
            data: { location: Entity.Location.GameRoom, locationDetail: JSON.stringify({ location: 2, gameRoomId: 'room-1' }) },
        });

        expect(await repository.clearLocationIfUnchanged(seen)).toBe(false);

        const row = await rawPrisma.userLocation.findUnique({ where: { id: 'U1' } });
        expect(row?.location).toBe(Entity.Location.GameRoom);
    });

    //  같은 Matchmaking이어도 대기표가 다르면 다른 상태다.
    it('대기표가 바뀌었으면 치유하지 않는다', async () => {
        const seen = 매칭중('t1');
        await repository.save(seen);
        await repository.save(매칭중('t2'));

        expect(await repository.clearLocationIfUnchanged(seen)).toBe(false);
    });

    //  조건부 쓰기는 save()를 우회하므로 캐시를 직접 비워야 한다. 안 그러면 낡은 값이 계속 나간다.
    it('치유한 뒤 캐시에 옛 값이 남지 않는다', async () => {
        const seen = 매칭중('t1');
        await repository.save(seen);
        await repository.findById('U1');            //  캐시에 올린다

        await repository.clearLocationIfUnchanged(seen);

        expect((await repository.findById('U1'))?.location).toBe(Location.None);
    });
});
```

- [ ] **Step 2: 실패 확인**

Run: `cd /c/Users/re5na/workspace/LOP/lop-backend/apps/lobby-server && npm run test:integration`
Expected: 컴파일 에러(`clearLocationIfUnchanged` 없음)

- [ ] **Step 3: DAO 구현**

`user-location.dao.postgres.ts`에 추가(파일 상단 import에 `Prisma`를 더한다):
```typescript
    /**
     * 내가 본 상태 그대로일 때만 None으로 되돌린다. 지운 행 수(0 또는 1)를 돌려준다.
     *
     * 치유 판단은 이웃 서비스에 HTTP로 물어보는 사이에 낡는다 — 그 사이 Director가 GameRoom을
     * 썼다면 옛 판단으로 덮으면 안 된다. 조건이 그 판단의 전제를 그대로 적은 것이다.
     *
     * locationDetail은 jsonb 안에 JSON **문자열**로 들어 있어(이중 인코딩) path 필터가 안 먹는다.
     * 저장할 때와 같은 문자열로 비교한다.
     */
    public async clearLocationIfUnchanged(
        id: string,
        seenLocation: Entity.Location,
        seenDetailJson: string,
        noneDetailJson: string,
    ): Promise<number> {
        try {
            const { count } = await this.model.updateMany({
                where: { id: id, location: seenLocation, locationDetail: { equals: seenDetailJson } },
                data: { location: Entity.Location.None, locationDetail: noneDetailJson, timestamp: new Date() },
            });
            return count;
        } catch (error) {
            return Promise.reject(error);
        }
    }
```

- [ ] **Step 4: 리포지토리 래퍼**

`user-location.repository.ts`에 추가:
```typescript
    /**
     * 내가 본 상태 그대로일 때만 None으로 되돌린다. 치유했으면 true.
     * 조건부 쓰기가 save()를 우회하므로 캐시는 여기서 직접 비운다 — **DB를 고친 뒤에** 비워야
     * 그 사이 들어온 조회가 옛 값을 캐시에 되살리지 않는다(티켓 취소와 같은 순서).
     */
    public async clearLocationIfUnchanged(seen: UserLocation): Promise<boolean> {
        try {
            const entity = this.mapper.toEntity(seen);
            const count = await this.postgresDao.clearLocationIfUnchanged(
                seen.id,
                entity.location,
                entity.locationDetail as string,
                JSON.stringify({ location: Location.None }),
            );
            await this.cacheDao.deleteById(seen.id);
            return count > 0;
        } catch (error) {
            return Promise.reject(error);
        }
    }

    /** 캐시를 건너뛴 조회 — 치유가 막힌 뒤 "지금 진짜 값"을 알아야 할 때 쓴다. */
    public async findByIdBypassingCache(id: string): Promise<UserLocation | null> {
        try {
            const entity = await this.postgresDao.findById(id);
            return entity ? this.mapper.toDomain(entity) : null;
        } catch (error) {
            return Promise.reject(error);
        }
    }
```
> 리포지토리가 `postgresDao`를 직접 들고 있어야 한다. `MatchmakingTicketRepository`가 하는 방식
> (생성자에서 `const postgresDao = new ...` 후 `super(...)` + `this.postgresDao = postgresDao`)을 따른다.

- [ ] **Step 5: 통합 테스트 통과 확인**

Run: `npm run test:integration` → `4 passed`(스모크는 아직 있으므로 5)

> ⚠️ **`locationDetail: { equals: ... }` 가 실제로 매칭되는지가 이 태스크의 관문이다.** 이중 인코딩
> 때문에 안 맞을 수 있다. 첫 테스트가 실패하면 저장된 값을 직접 찍어(`rawPrisma.$queryRaw`) 무엇과
> 비교해야 하는지 확인하고, **비교 방식만** 고친다(저장 형식은 바꾸지 않는다 — 별도 트랙 몫).

- [ ] **Step 6: 읽기 경로를 조건부 치유로**

`user-location.service.ts`의 `verifyUserLocation`을 아래 두 메서드로 대체한다(쓰기 경로는 Task 2에서 이미 이 함수를 안 쓴다):
```typescript
    /**
     * 저장된 상태가 아직 유효한지 보고, 아니면 치유한다.
     * **바뀐 게 없으면 아무것도 쓰지 않는다** — 예전엔 timestamp를 갱신하려고 매 조회마다 행 전체를
     * 덮어썼고, 그 사이 남이 쓴 값을 옛 값으로 지웠다.
     */
    private async healIfStale(userLocation: UserLocation): Promise<UserLocation> {
        try {
            if (!(await this.isStale(userLocation))) {
                return userLocation;
            }

            const healed = await this.userLocationRepository.clearLocationIfUnchanged(userLocation);
            if (healed) {
                return { ...userLocation, location: Location.None, locationDetail: { location: Location.None } as LocationDetail };
            }

            //  치유가 막혔다 = 판단하는 사이 누가 상태를 바꿨다. 옛 판단으로 덮지 않고 지금 값을 준다.
            //  캐시를 건너뛴다 — 방금 바뀐 값을 봐야 한다.
            return (await this.userLocationRepository.findByIdBypassingCache(userLocation.id)) ?? userLocation;
        } catch (error) {
            return Promise.reject(error);
        }
    }

    /** 저장된 위치의 근거(대기표·방)가 아직 살아 있나. */
    private async isStale(userLocation: UserLocation): Promise<boolean> {
        switch (+userLocation.location) {
            case Location.Matchmaking: {
                const detail = userLocation.locationDetail as MatchmakingLocationDetail;
                return !(await this.matchmakingTicketService.findMatchmakingTicketById(detail.matchmakingTicketId));
            }
            case Location.GameRoom: {
                const detail = userLocation.locationDetail as GameRoomLocationDetail;
                const response = await this.roomService.findRoomById(detail.gameRoomId);
                const room = response?.room;
                return !room || room.status === RoomStatus.Closed || room.status === RoomStatus.Error;
            }
            default:
                return false;
        }
    }
```
`getOrCreateUserLocationById`의 `verifyUserLocation` 호출을 `healIfStale`로 바꾼다.

- [ ] **Step 7: 스모크 삭제 + 전체 확인**

Run: `rm apps/lobby-server/test/integration/smoke.integration.test.ts`
Run: `cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm exec turbo run build --force && pnpm exec turbo run test && (cd apps/lobby-server && npm run test:integration) && (cd apps/matchmaking-server && npm run test:integration)`
Expected: 빌드 5/5, lobby 통합 4, matchmaking 통합 14, 유닛 159+11+lobby 2

- [ ] **Step 8: 변별력 확인**

`clearLocationIfUnchanged`의 `where`에서 `location`과 `locationDetail` 조건을 빼고(= 무조건 덮어쓰기) 돌리면
**"그 사이 다른 값이 쓰였으면 치유하지 않는다"와 "대기표가 바뀌었으면"** 두 테스트가 실패해야 한다.
실제 실패 출력을 보고에 남기고 반드시 원복할 것.

- [ ] **Step 9: 커밋**

```bash
git add apps/lobby-server
git commit -m "fix(lobby): 자가치유를 조건부 쓰기로 + 안 바뀌면 안 쓰기

읽기 경로가 치유할 게 없어도 매번 행 전체를 덮어썼다(timestamp 갱신). 그
사이 남이 쓴 값이 옛 값으로 사라진다. 이제 바뀐 게 없으면 아무것도 안 쓰고,
치유는 내가 본 상태 그대로일 때만 한다.

치유가 막히면 캐시를 건너뛴 조회로 지금 값을 돌려준다. 조건부 쓰기가
save()를 우회하므로 캐시는 DB를 고친 뒤에 직접 비운다."
```

---

## 실행 후 (컨트롤러가 직접)

1. **CI에 lobby 통합 테스트 단계 추가** — `backend-ci.yml`과 `backend-deploy.yml` 양쪽에.
2. **로컬 docker build 3종.**
3. **배포** — 마이그레이션 없으므로 `app: all`이 아니어도 되지만, 일관되게 `all`로 간다.
4. **E2E** — 매칭→입장이 정상인지, 그리고 **매치가 잡히는 순간 로비로 튕기지 않는지**(이 작업이 고치는 증상).
5. **ROADMAP 갱신 + 커밋**, 워크트리 머지. 이걸로 경합 트랙 B·C가 모두 닫힌다.
