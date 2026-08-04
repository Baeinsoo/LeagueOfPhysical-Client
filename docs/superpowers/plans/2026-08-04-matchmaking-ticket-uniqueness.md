# 대기표 유일성(C) — 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 한 유저가 열린 대기표를 두 장 갖는 것을 데이터베이스가 원천 차단한다. 파티(여러 명 티켓)까지 덮는다.

**Architecture:** `MatchmakingTicketUser` 표를 만들고 **`userId`를 기본키**로 둔다 — 기본키가 곧 "한 유저는 열린 티켓 하나" 규칙이다. 티켓 생성과 인원 등록을 한 트랜잭션으로 하고, 제약에 걸리면 기존 티켓을 돌려준다(멱등). 티켓이 지워지면 인원 행은 cascade로 따라 지워진다.

**Tech Stack:** Prisma 6 + PostgreSQL 16, TypeScript/CJS, jest 29 + ts-jest, testcontainers

**작업 저장소:** `C:\Users\re5na\workspace\LOP\lop-backend` (설계 문서는 클라 repo, **코드는 전부 lop-backend**)

**설계 문서:** `docs/superpowers/specs/2026-08-04-matchmaking-race-fixes-design.md` (클라 repo) — 이 계획은 그중 **C만** 다룬다. B는 별도 계획.

## Global Constraints

- **마이그레이션은 손으로 쓴다.** 이 저장소에는 `.env`도 shadow DB도 없어 `prisma migrate dev`를 쓸 수 없다. `packages/database/prisma/migrations/<timestamp>_<name>/migration.sql`을 직접 만든다. 타임스탬프는 기존 것보다 뒤여야 한다 — 이번 것은 `20260804000000`.
- **조회로 막지 않는다.** "먼저 있는지 확인하고 없으면 만든다"는 확인과 생성 사이의 틈 때문에 원리적으로 못 막는다. 반드시 **제약에 부딪히게 두고 그 결과로 분기**한다.
- **티켓을 만드는 경로는 하나로 유지한다.** 인원 등록 없이 티켓만 만드는 경로가 남으면 그 티켓은 보호되지 않는다.
- 한글 주석("왜"만 짧게), 한국어 커밋 메시지.
- 기존 유닛 베이스라인: matchmaking 20 suites / 155 tests, room 1 suite / 11 tests. 통합 1 suite / 6 tests.

## File Structure

| 파일 | 무엇 |
|---|---|
| `packages/database/prisma/schema.prisma` | `MatchmakingTicketUser` 모델 + `MatchmakingTicket`에 관계 추가 |
| `packages/database/prisma/migrations/20260804000000_matchmaking_ticket_user/migration.sql` | 기존 데이터 정리 → 표 생성 → 백필 |
| `apps/matchmaking-server/src/daos/matchmakingTicket.dao.postgres.ts` | `createWithMembers` / `findOpenByUserId` |
| `apps/matchmaking-server/src/repositories/matchmakingTicket.repository.ts` | 위 둘의 도메인 래퍼 |
| `apps/matchmaking-server/src/services/matchmakingTicket.service.ts` | 발급을 새 경로로 + **죽은 생성 메서드 2개 삭제** |
| `apps/matchmaking-server/src/services/matchmaking.service.ts` | 중복 요청 시 기존 티켓으로 응답(멱등) |
| `apps/matchmaking-server/test/integration/ticketUniqueness.integration.test.ts` | 시나리오 |

---

### Task 1: 스키마 + 마이그레이션

**Files:**
- Modify: `packages/database/prisma/schema.prisma`
- Create: `packages/database/prisma/migrations/20260804000000_matchmaking_ticket_user/migration.sql`

**Interfaces:**
- Produces: Prisma 모델 `MatchmakingTicketUser { userId(@id), ticketId, ticket }`, `MatchmakingTicket.members`

- [ ] **Step 1: 스키마 수정**

`packages/database/prisma/schema.prisma`의 `MatchmakingTicket` 모델 마지막 필드(`matchId`) 다음 줄에 추가:
```prisma
  //  이 티켓에 묶인 유저들. 인원 정보는 userIds가 그대로 갖고, 이 관계는 "한 유저는 열린 티켓
  //  하나"를 DB가 강제하게 만드는 용도다.
  members     MatchmakingTicketUser[]
```

그리고 파일 끝에 새 모델 추가:
```prisma
//  userId가 기본키인 것 자체가 규칙이다: "한 유저는 열린 티켓을 하나만 가진다".
//  발급 전에 조회해서 확인하는 방식은 확인과 생성 사이의 틈 때문에 원리적으로 못 막는다 —
//  두 요청이 둘 다 정직하게 조회하고 둘 다 "없음"을 받을 수 있다.
//  티켓이 지워지면(취소·매치 확정 후 정리) 이 행도 cascade로 함께 사라진다.
model MatchmakingTicketUser {
  userId   String @id
  ticketId String

  ticket MatchmakingTicket @relation(fields: [ticketId], references: [id], onDelete: Cascade)

  @@index([ticketId])
}
```

- [ ] **Step 2: 마이그레이션 작성**

`packages/database/prisma/migrations/20260804000000_matchmaking_ticket_user/migration.sql`:
```sql
--  기존 데이터에 같은 유저의 열린 티켓이 둘 이상 있으면 새 기본키를 만들 수 없다.
--  대기표는 잠깐 살다 가는 데이터이므로, 유저별로 가장 오래된 티켓만 남기고 나머지는 지운다.
--  DISTINCT가 꼭 필요하다: 한 티켓의 userIds 안에 같은 유저가 두 번 들어 있으면 아래 순번 매기기가
--  그걸 "티켓 두 장"으로 착각해, 그 유저의 유일한 티켓인데도 지워 버린다.
WITH expanded AS (
    SELECT DISTINCT id, "createdAt", u AS "userId"
    FROM "MatchmakingTicket", unnest("userIds") AS u
),
ranked AS (
    SELECT id, "userId",
           row_number() OVER (PARTITION BY "userId" ORDER BY "createdAt" ASC, id ASC) AS rn
    FROM expanded
)
DELETE FROM "MatchmakingTicket"
WHERE id IN (SELECT DISTINCT id FROM ranked WHERE rn > 1);

CREATE TABLE "MatchmakingTicketUser" (
    "userId" TEXT NOT NULL,
    "ticketId" TEXT NOT NULL,

    CONSTRAINT "MatchmakingTicketUser_pkey" PRIMARY KEY ("userId")
);

CREATE INDEX "MatchmakingTicketUser_ticketId_idx" ON "MatchmakingTicketUser"("ticketId");

ALTER TABLE "MatchmakingTicketUser"
    ADD CONSTRAINT "MatchmakingTicketUser_ticketId_fkey"
    FOREIGN KEY ("ticketId") REFERENCES "MatchmakingTicket"("id")
    ON DELETE CASCADE ON UPDATE CASCADE;

--  남은 티켓의 인원을 백필한다. 한 티켓 안에 같은 유저가 중복으로 들어 있어도 한 행만 만든다.
INSERT INTO "MatchmakingTicketUser" ("userId", "ticketId")
SELECT DISTINCT u, id
FROM (SELECT id, unnest("userIds") AS u FROM "MatchmakingTicket") s;
```

- [ ] **Step 3: 생성 + 빌드**

Run: `cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm --filter @lop/database run generate && pnpm exec turbo run build --force`
Expected: 5/5 성공

- [ ] **Step 4: 진짜 DB에 적용해 확인 — 빈 DB**

Run:
```bash
docker rm -f pg-mig >/dev/null 2>&1
docker run -d --name pg-mig -e POSTGRES_PASSWORD=t -e POSTGRES_USER=t -e POSTGRES_DB=t -p 55440:5432 postgres:16-alpine
until docker exec pg-mig pg_isready -U t; do sleep 1; done
cd packages/database && DATABASE_URL="postgresql://t:t@localhost:55440/t" npx prisma migrate deploy
```
Expected: `All migrations have been successfully applied.` (7개)

- [ ] **Step 5: 기존 데이터가 있는 경우도 확인 — 중복 티켓 정리가 도는가**

이 마이그레이션의 위험은 **운영 DB에 이미 중복이 있을 때**다. 그 상황을 만들어 확인한다.

Run:
```bash
docker rm -f pg-mig2 >/dev/null 2>&1
docker run -d --name pg-mig2 -e POSTGRES_PASSWORD=t -e POSTGRES_USER=t -e POSTGRES_DB=t -p 55441:5432 postgres:16-alpine
until docker exec pg-mig2 pg_isready -U t; do sleep 1; done
cd packages/database
#  이번 것 직전까지만 적용
DATABASE_URL="postgresql://t:t@localhost:55441/t" npx prisma migrate resolve --applied 20260804000000_matchmaking_ticket_user 2>/dev/null || true
```
위 방식이 번거로우면 대신 **전체 적용 후 표를 비우고 중복을 심은 뒤 마지막 마이그레이션 SQL만 손으로 실행**해도 된다. 어느 쪽이든 확인할 것은 아래다:

- U1이 낀 티켓 2장(생성 시각 다름)을 심고 정리 SQL을 실행 → **오래된 것만 남는다**
- 백필 후 `MatchmakingTicketUser`에 U1 행이 정확히 1개
- 같은 티켓 안에 `['U1','U1']`이 들어 있어도 백필이 실패하지 않는다

확인한 SQL과 실제 출력을 보고에 남길 것.

- [ ] **Step 6: 정리 + 커밋**

```bash
docker rm -f pg-mig pg-mig2
git add packages/database/prisma
git commit -m "feat(db): 대기표 인원 표 추가 — 한 유저는 열린 티켓 하나

userId를 기본키로 두어 '한 유저는 열린 티켓 하나'를 DB가 강제한다.
발급 전 조회로 확인하는 방식은 확인과 생성 사이의 틈 때문에 못 막는다.
티켓 삭제 시 cascade로 함께 정리된다.

기존 데이터에 중복이 있으면 기본키를 만들 수 없어, 유저별로 가장 오래된
티켓만 남기고 정리한 뒤 백필한다."
```

---

### Task 2: 발급을 원자적으로 (테스트 먼저)

**Files:**
- Create: `apps/matchmaking-server/test/integration/ticketUniqueness.integration.test.ts`
- Modify: `apps/matchmaking-server/src/daos/matchmakingTicket.dao.postgres.ts`
- Modify: `apps/matchmaking-server/src/repositories/matchmakingTicket.repository.ts`

**Interfaces:**
- Consumes: Task 1의 `MatchmakingTicketUser`
- Produces:
  - `MatchmakingTicketDaoPostgres.createWithMembers(entity): Promise<Entity | null>` — 성공 시 생성된 티켓, **이미 열린 티켓을 가진 유저가 있으면 `null`**
  - `MatchmakingTicketDaoPostgres.findOpenByUserId(userId): Promise<Entity | null>`
  - 리포지토리에 같은 이름의 도메인 래퍼 2개

- [ ] **Step 1: 실패하는 테스트 작성**

`apps/matchmaking-server/test/integration/ticketUniqueness.integration.test.ts`:
```typescript
import { rawPrisma, resetTables } from './db';
import { MatchmakingTicketDaoPostgres } from '@daos/matchmakingTicket.dao.postgres';

const 티켓 = (id: string, userIds: string[]) => ({
    id, userIds, queueId: 1, gameModeIds: [1], mapIds: [1], rating: 1000,
    createdAt: new Date(), matchId: null,
});

describe('대기표 유일성 — 한 유저는 열린 티켓 하나', () => {
    let dao: MatchmakingTicketDaoPostgres;

    beforeEach(async () => {
        await rawPrisma.matchmakingTicketUser.deleteMany({});
        await resetTables();
        dao = new MatchmakingTicketDaoPostgres();
    });
    afterAll(async () => { await rawPrisma.$disconnect(); });

    it('두 번째 발급은 거부된다', async () => {
        expect(await dao.createWithMembers(티켓('T1', ['U1']))).not.toBeNull();

        expect(await dao.createWithMembers(티켓('T2', ['U1']))).toBeNull();

        expect(await rawPrisma.matchmakingTicket.count()).toBe(1);
    });

    //  조회로 확인하는 방식이 못 막는 바로 그 상황 — 두 요청이 같은 순간에 들어온다.
    it('동시에 두 번 발급해도 하나만 만들어진다', async () => {
        const [a, b] = await Promise.all([
            dao.createWithMembers(티켓('T1', ['U1'])),
            dao.createWithMembers(티켓('T2', ['U1'])),
        ]);

        expect([a, b].filter(Boolean)).toHaveLength(1);
        expect(await rawPrisma.matchmakingTicket.count()).toBe(1);
    });

    it('파티 인원이 겹치면 거부된다', async () => {
        expect(await dao.createWithMembers(티켓('T1', ['U1', 'U2']))).not.toBeNull();

        //  U2가 이미 T1에 들어 있다
        expect(await dao.createWithMembers(티켓('T2', ['U2', 'U3']))).toBeNull();

        //  겹치지 않는 파티는 만들어진다
        expect(await dao.createWithMembers(티켓('T3', ['U3', 'U4']))).not.toBeNull();
    });

    it('티켓을 지우면 인원 등록도 함께 사라져 다시 발급할 수 있다', async () => {
        await dao.createWithMembers(티켓('T1', ['U1']));

        await rawPrisma.matchmakingTicket.delete({ where: { id: 'T1' } });

        expect(await rawPrisma.matchmakingTicketUser.count()).toBe(0);
        expect(await dao.createWithMembers(티켓('T2', ['U1']))).not.toBeNull();
    });

    it('열린 티켓을 유저로 찾을 수 있다', async () => {
        await dao.createWithMembers(티켓('T1', ['U1', 'U2']));

        expect((await dao.findOpenByUserId('U2'))?.id).toBe('T1');
        expect(await dao.findOpenByUserId('U9')).toBeNull();
    });

    //  한 티켓 안의 중복은 조용히 걷어내지 않고 터뜨린다 — 잘못된 데이터를 정상인 척 만들면
    //  매칭 함수가 userIds.length로 인원을 세어 혼자서 2인 매치를 만든다.
    it('한 티켓에 같은 유저가 두 번 들어오면 에러로 거부한다', async () => {
        await expect(dao.createWithMembers(티켓('T1', ['U1', 'U1']))).rejects.toThrow(/Duplicate userIds/);

        //  터졌으면 아무것도 남지 않아야 한다.
        expect(await rawPrisma.matchmakingTicket.count()).toBe(0);
    });
});
```

- [ ] **Step 2: 실패 확인**

Run: `cd /c/Users/re5na/workspace/LOP/lop-backend/apps/matchmaking-server && npm run test:integration`
Expected: 새 스위트가 **컴파일 에러로 실패**(`createWithMembers` 없음). 기존 6개는 통과.

- [ ] **Step 3: DAO 구현**

`apps/matchmaking-server/src/daos/matchmakingTicket.dao.postgres.ts`에 import를 추가하고
```typescript
import { PrismaClient, Prisma, MatchmakingTicket as MatchmakingTicketEntity } from '@lop/database';
```
클래스 안에 아래 두 메서드를 추가한다:
```typescript
    /**
     * 티켓과 인원 등록을 한 트랜잭션으로 만든다. 이미 열린 티켓을 가진 유저가 하나라도 끼어 있으면
     * `MatchmakingTicketUser`의 기본키에 걸려 통째로 롤백되고 **null**을 돌려준다.
     *
     * 만들기 전에 조회해서 확인하지 않는 이유: 확인과 생성 사이의 틈에 다른 요청이 끼면 둘 다
     * "없음"을 받고 둘 다 만든다. 제약에 부딪히게 두는 것만이 그 틈을 없앤다.
     */
    public async createWithMembers(entity: MatchmakingTicketEntity): Promise<MatchmakingTicketEntity | null> {
        try {
            //  중복을 조용히 걷어내지 않는다 — 한 티켓에 같은 유저가 두 번 들어 있는 것 자체가
            //  잘못된 데이터이고, 매칭 함수가 userIds.length로 인원을 세므로 그대로 두면 혼자서
            //  2인 매치가 만들어진다. null로 돌려주면 안 된다: null은 "이미 대기 중"이라는 뜻이라
            //  호출자가 정상 상황으로 오해한다.
            if (new Set(entity.userIds).size !== entity.userIds.length) {
                throw new Error(`Duplicate userIds in one ticket. ticketId: ${entity.id}, userIds: ${entity.userIds.join(',')}`);
            }

            return await this.prismaClient.$transaction(async (tx: Prisma.TransactionClient) => {
                const created = await tx.matchmakingTicket.create({ data: entity });
                await tx.matchmakingTicketUser.createMany({
                    data: entity.userIds.map(userId => ({ userId, ticketId: created.id })),
                });
                return created;
            });
        } catch (error) {
            //  P2002 = 유니크 제약 위반. 이미 열린 티켓을 가진 유저가 있다는 뜻이다.
            if (error instanceof Prisma.PrismaClientKnownRequestError && error.code === 'P2002') {
                return null;
            }
            return Promise.reject(error);
        }
    }

    /** 이 유저가 지금 들고 있는 티켓. 없으면 null. */
    public async findOpenByUserId(userId: string): Promise<MatchmakingTicketEntity | null> {
        try {
            const row = await this.prismaClient.matchmakingTicketUser.findUnique({
                where: { userId },
                include: { ticket: true },
            });
            return row?.ticket ?? null;
        } catch (error) {
            return Promise.reject(error);
        }
    }
```

- [ ] **Step 4: 리포지토리 래퍼 추가**

`apps/matchmaking-server/src/repositories/matchmakingTicket.repository.ts` 클래스 안에 추가:
```typescript
    /**
     * 티켓 발급. 이미 열린 티켓을 가진 유저가 끼어 있으면 null — 호출자가 기존 티켓으로 응답한다.
     * 캐시는 채우지 않는다(새 id라 낡은 사본이 있을 수 없고, 다음 조회가 알아서 채운다).
     */
    public async createWithMembers(ticket: MatchmakingTicket): Promise<MatchmakingTicket | null> {
        try {
            const entity = await this.postgresDao.createWithMembers(this.mapper.toEntity(ticket));
            return entity ? this.mapper.toDomain(entity) : null;
        } catch (error) {
            return Promise.reject(error);
        }
    }

    /** 이 유저가 지금 들고 있는 티켓. 캐시를 거치지 않는다 — 소비 표시를 최신으로 봐야 한다. */
    public async findOpenByUserId(userId: string): Promise<MatchmakingTicket | null> {
        try {
            const entity = await this.postgresDao.findOpenByUserId(userId);
            return entity ? this.mapper.toDomain(entity) : null;
        } catch (error) {
            return Promise.reject(error);
        }
    }
```

- [ ] **Step 5: 테스트 통과 확인**

Run: `cd /c/Users/re5na/workspace/LOP/lop-backend/apps/matchmaking-server && npm run test:integration`
Expected: `Tests: 11 passed` (기존 6 + 새 5)

- [ ] **Step 6: 변별력 확인 — 제약을 빼면 실패하는가**

`packages/database/prisma/schema.prisma`에서 `userId String @id`를 잠시
`userId String` + `@@id([userId, ticketId])`로 바꾸고 generate·migrate한 DB로 돌리면 "두 번째 발급 거부"와
"동시 발급" 테스트가 실패해야 한다. **또는 더 간단하게**: `createWithMembers`의 `createMany` 호출을
잠시 주석 처리하고 돌린다 — 인원 등록이 없으면 제약이 걸리지 않으므로 같은 두 테스트가 실패한다.

어느 쪽으로 확인했는지와 **실제 실패 출력**을 보고에 남기고 반드시 원복할 것. 원복 후 11개 재통과 확인.

- [ ] **Step 7: 커밋**

```bash
git add apps/matchmaking-server/src/daos apps/matchmaking-server/src/repositories apps/matchmaking-server/test
git commit -m "feat(matchmaking): 티켓 발급을 인원 등록과 한 트랜잭션으로

이미 열린 티켓을 가진 유저가 끼어 있으면 기본키에 걸려 롤백되고 null을
돌려준다. 조회로 확인하지 않는 이유를 주석에 남겼다 — 확인과 생성 사이의
틈에 다른 요청이 끼면 둘 다 '없음'을 받는다.

동시 발급·파티 겹침·cascade 정리를 진짜 DB로 검증(5종)."
```

---

### Task 3: 서비스 배선 + 우회 경로 제거

**Files:**
- Modify: `apps/matchmaking-server/src/services/matchmakingTicket.service.ts`
- Modify: `apps/matchmaking-server/src/services/matchmaking.service.ts`

- [ ] **Step 1: 죽은 생성 경로 2개 삭제**

`matchmakingTicket.service.ts`에서 **`createMatchmakingTicket`과 `createMatchmakingTickets` 메서드를 통째로 삭제**한다. 둘 다 소비처가 0이고(확인됨), **인원 등록 없이 티켓만 만드는 경로**라 남겨 두면 나중에 누가 쓰는 순간 보장이 무력화된다.

같이 쓰이지 않게 된 import(`CreateMatchmakingTicketDto`, `MatchmakingTicketMapper`)가 있으면 함께 정리한다. 다른 곳에서 여전히 쓰면 남긴다.

- [ ] **Step 2: 발급 메서드를 새 경로로**

같은 파일의 `issueMatchmakingTicket`을 아래로 교체한다:
```typescript
    /**
     * 티켓 발급. 이미 열린 티켓을 가진 유저가 끼어 있으면 **null**을 돌려준다 —
     * 호출자가 기존 티켓으로 응답할지 거절할지 정한다.
     */
    public async issueMatchmakingTicket(userIds: string[], queueId: number, gameModeIds: number[], mapIds: number[], rating: number): Promise<MatchmakingTicket | null> {
        try {
            const matchmakingTicket = MatchmakingTicketFactory.create({
                userIds: userIds,
                queueId: queueId,
                gameModeIds: gameModeIds,
                mapIds: mapIds,
                rating: rating,
            });
            return await this.matchmakingTicketRepository.createWithMembers(matchmakingTicket);
        } catch (error) {
            return Promise.reject(error);
        }
    }

    /** 이 유저가 지금 들고 있는 티켓. 없으면 undefined. */
    public async findOpenMatchmakingTicketByUserId(userId: string): Promise<MatchmakingTicket | undefined> {
        try {
            return (await this.matchmakingTicketRepository.findOpenByUserId(userId)) ?? undefined;
        } catch (error) {
            return Promise.reject(error);
        }
    }
```

- [ ] **Step 3: 요청 처리를 멱등하게**

`matchmaking.service.ts`의 `requestMatchmaking`에서, 기존
```typescript
            //  요청 하나에는 게임모드·맵이 각각 하나뿐이라, 후보 목록도 원소 하나짜리로 저장한다.
            const matchmakingTicket = await this.matchmakingTicketService.issueMatchmakingTicket(
```
로 시작하는 발급 블록 **바로 다음에** 아래를 삽입한다(변수명 `matchmakingTicket`은 그대로 쓰되 `const`를 `let`으로 바꾼다):
```typescript
            //  발급이 막혔다는 건 이 유저에게 이미 열린 티켓이 있다는 뜻이다 — 요청이 중복으로
            //  들어온 것이므로 새로 만들지 않고 그 티켓으로 답한다(버튼을 두 번 눌러도 정상으로 보인다).
            if (!matchmakingTicket) {
                const existing = await this.matchmakingTicketService.findOpenMatchmakingTicketByUserId(requestMatchmakingDto.userId);
                if (!existing || existing.matchId) {
                    //  이미 매치가 가져갔거나 그 사이 사라졌다면 새 매칭을 시작할 수 없다.
                    return {
                        code: ResponseCode.INVALID_TO_MATCH_MAKING,
                    };
                }
                matchmakingTicket = existing;
            }
```

이어지는 `updateUserLocation` 호출과 성공 응답은 그대로 둔다 — 기존 티켓 id로 위치를 다시 기록하는 것은 무해하고, 중복 요청도 같은 결과를 받는다.

- [ ] **Step 4: 빌드 + 전체 테스트**

Run: `cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm exec turbo run build --force && pnpm exec turbo run test && (cd apps/matchmaking-server && npm run test:integration)`
Expected: 빌드 5/5, 유닛 155 + 11, 통합 11

> 유닛 테스트가 `issueMatchmakingTicket`의 반환 타입 변경(`| null`)이나 삭제한 메서드 때문에 깨지면, **테스트를 새 계약에 맞춰 고친다**(계약이 바뀐 것이 맞다). 무엇을 왜 고쳤는지 보고에 남길 것.

- [ ] **Step 5: 커밋**

```bash
git add apps/matchmaking-server/src/services
git commit -m "feat(matchmaking): 중복 매칭 요청을 기존 티켓으로 응답(멱등)

발급이 제약에 막히면 새로 만들지 않고 이미 있던 티켓으로 답한다. 이미
매치가 가져간 상태면 거절한다.

인원 등록 없이 티켓만 만들던 죽은 메서드 2개(createMatchmakingTicket,
createMatchmakingTickets)를 삭제 — 소비처가 0이고, 남겨 두면 나중에 쓰는
순간 보장이 무력화되는 우회로다."
```

---

## 실행 후 (컨트롤러가 직접)

1. **로컬 docker build 3종** — 스키마·생성물이 이미지 안에서도 맞는지.
2. **배포** — ⚠️ **마이그레이션이 있으므로 `app: all`**(db-migrate가 빠지면 PreSync가 아무것도 적용하지 않는다).
3. **E2E** — 2클라로 매칭·취소를 여러 번 섞어서. 특히 **매칭 버튼 연타**로 대기표가 하나만 생기는지.
4. **ROADMAP 갱신 + 커밋**, 워크트리 머지.
5. 다음은 **B**(로비 자가치유) — lobby-server jest 신설 + Redis 컨테이너가 선행이다.
