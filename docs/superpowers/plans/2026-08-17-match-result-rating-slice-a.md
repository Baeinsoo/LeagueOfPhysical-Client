# 슬라이스 A — 스키마·어휘 재정비 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 매치 결과·레이팅 도메인의 표와 어휘를 표준으로 갈아끼운다 — **동작은 지금과 똑같아야 한다.**

**Architecture:** 스트랭글러로 간다. 먼저 새 표·컬럼을 **더하고**(T1), 소비처를 하나씩 새 것으로 옮기고(T2~T5), 마지막에 옛 것을 **지운다**(T6). 이렇게 하면 매 태스크가 컴파일·테스트를 통과한 채로 커밋된다. `UserStats` → `UserRating`(μ·σ·mmr 3층 분리), `Match`에 생애(state/시작·종료 시각)와 `targetMmr` 추가, 참가자별 결과를 담을 `MatchParticipant` 신설. **`playerList`는 응답 DTO에 파생값으로 남긴다** — 방 접속 인증이 이걸 읽는다.

**Tech Stack:** pnpm + turbo 모노레포, TypeScript, Prisma 6 + PostgreSQL, Redis(cache-aside), jest + testcontainers, Unity(C#) 클라·서버

**Spec:** `docs/superpowers/specs/2026-08-17-match-result-rating-design.md`

## Global Constraints

- **동작 무변화가 이 슬라이스의 합격 기준이다.** 값이 바뀌면 안 된다 — 신규·기존 유저 모두 `mmr = 1000`이고, 매칭은 지금과 똑같이 붙는다.
- **레포 4개**: `lop-backend`(주), `LeagueOfPhysical-Server`, `LeagueOfPhysical-Client`(Unity DTO), 마이그레이션은 `lop-backend/packages/database`.
- **`pnpm build`를 테스트보다 먼저 돌린다.** 타입만 깨져도 테스트는 통과한다 — 아무도 import 안 하는 파일은 ts-jest가 타입 검사를 건너뛴다.
- **공유 타입(`@lop/database`·`@lop/server-core`)을 건드린 태스크는 전체 테스트를 돌린다.** 개별 앱 테스트만으로는 안 잡힌다.
- **`ResponseCode`의 숫자 값은 절대 바꾸지 않는다.** Unity `ResponseCode.cs`가 같은 숫자를 복제하고 있다(이름만 바꾼다).
- **Prisma 마이그레이션은 손으로 쓴다.** `prisma migrate dev`의 자동 생성은 rename을 drop+create로 만들어 **데이터를 날린다.**
- **Unity `.meta` 파일은 함께 커밋한다.** 이번 슬라이스는 새 파일이 없어 해당 없음이지만, 새로 만들면 반드시 짝을 맞춘다.
- 배포는 이 슬라이스 범위 밖이다. **하게 되면 마이그레이션이 있으므로 `app=all`.**

**작업 위치:** `lop-backend`·Unity 두 레포는 각자 피처 브랜치를 판다 (`feature/match-rating-slice-a`). main 직접 커밋 금지.

---

## File Structure

### `lop-backend/packages/database` (T1, T6)
- `prisma/schema.prisma` — 모델 정의
- `prisma/migrations/20260817000000_match_result_rating_additive/migration.sql` — 새 표·컬럼 추가 + 데이터 이관
- `prisma/migrations/20260817100000_drop_user_stats_and_target_rating/migration.sql` — 옛 것 제거 (T6)

### `lop-backend/apps/lobby-server` (T2) — 레이팅의 주인
| 파일 | 책임 |
|---|---|
| `src/interfaces/user-rating.interface.ts` | 도메인 타입 |
| `src/factories/user-rating.factory.ts` | 신규 유저 기본값 |
| `src/daos/user-rating.dao.postgres.ts` / `.dao.redis.ts` | 저장소 어댑터 |
| `src/mappers/entities/user-rating.mapper.ts` | 도메인 ↔ 엔티티 |
| `src/repositories/user-rating.repository.ts` | cache-aside |
| `src/services/user-rating.service.ts` | 조회 |
| `src/controllers/user-rating.controller.ts` / `src/routes/user-rating.route.ts` | `GET /user/:userId/rating` |
| `src/dtos/user-rating.dto.ts` / `src/mappers/controllers/user-rating.mapper.ts` | 응답 모양 |
| `src/services/user.service.ts` | 가입 시 큐별 행 생성 |
| `src/main.ts` | 라우트 등록 |

### `lop-backend/apps/matchmaking-server` (T3, T4, T5)
| 파일 | 책임 |
|---|---|
| `src/services/user-rating.service.ts` + `httpServices/lobbyServer.service.ts` | lobby에서 레이팅 조회 |
| `src/dtos/user-rating.dto.ts` | 조회 응답 타입 |
| `src/services/matchmaking.service.ts` | 티켓 발급 시 `mmr` 사용 |
| `src/interfaces/match.interface.ts` · `dtos/match.dto.ts` · `factories/match.factory.ts` · `mappers/{entities,controllers}/match.mapper.ts` | Match 도메인 확장 |
| `src/daos/match.dao.postgres.ts` | 참가자 행 생성(트랜잭션 안) |

### `lop-backend/packages/server-core` (T2)
- `src/interfaces/responseCode.interface.ts` — `USER_STATS_NOT_EXIST` → `USER_RATING_NOT_EXIST` (값 70000 유지)

### Unity (T5)
- `LeagueOfPhysical-{Client,Server}/Assets/Scripts/Domain/Match.cs`
- `LeagueOfPhysical-{Client,Server}/Assets/Scripts/WebAPI/Dto/MatchDto.cs`
- `LeagueOfPhysical-Client/Assets/Scripts/WebAPI/ResponseCode.cs`
- `lop-backend/apps/room-server/src/dtos/match.dto.ts`

---

## Task 1: 스키마 — 새 표·컬럼을 더한다 (옛 것 유지)

**Files:**
- Modify: `lop-backend/packages/database/prisma/schema.prisma`
- Create: `lop-backend/packages/database/prisma/migrations/20260817000000_match_result_rating_additive/migration.sql`

**Interfaces:**
- Consumes: (없음 — 첫 태스크)
- Produces: Prisma 모델 `UserRating { id, userId, queueId, mu: Float, sigma: Float, mmr: Int, gamesPlayed: Int, firstPlaces: Int, placementSum: Int, createdAt, updatedAt }`, `MatchParticipant { id, matchId, userId, placement: Int?, mmrBefore: Int?, mmrAfter: Int?, muBefore: Float?, muAfter: Float?, sigmaBefore: Float?, sigmaAfter: Float? }`, enum `MatchState { Created, InProgress, Finished }`, `Match`에 `targetMmr: Int`, `state: MatchState`, `startedAt: DateTime?`, `endedAt: DateTime?` 추가. 기존 `UserStats`·`Match.targetRating`은 그대로 남아 있다.

- [ ] **Step 1: `schema.prisma`에 새 모델을 더한다**

`model UserStats { ... }` **바로 아래**에 추가한다 (UserStats는 T6까지 남긴다):

```prisma
//  숨은 실력 추정치. OpenSkill이 들고 도는 상태는 (mu, sigma) 두 값이고,
//  매칭이 읽는 것은 거기서 뽑은 정수 mmr 하나다. 유저에게 보이는 티어는 아직 없다.
//  FFA(2~8명 등수 게임)라 승/무/패가 아니라 1등 횟수와 등수 합(→평균 등수)을 센다.
model UserRating {
  id           String   @id @default(uuid())
  userId       String
  queueId      Int
  mu           Float    @default(25)
  sigma        Float    @default(8.333333333333334)
  mmr          Int      @default(1000)
  gamesPlayed  Int      @default(0)
  firstPlaces  Int      @default(0)
  placementSum Int      @default(0)
  createdAt    DateTime @default(now())
  updatedAt    DateTime @updatedAt

  @@unique([userId, queueId])
}
```

`model MatchRound { ... }` 아래에 추가한다:

```prisma
//  한 판의 한 참가자. 매치가 만들어질 때 placement=null로 미리 깔아 두고,
//  게임서버의 결과 보고가 그 빈 칸을 채운다 — 명단을 만드는 게 아니라 채우는 것이라
//  게임서버가 남의 userId를 끼워 넣을 수 없다.
model MatchParticipant {
  id          String @id @default(uuid())
  matchId     String
  userId      String
  placement   Int?
  mmrBefore   Int?
  mmrAfter    Int?
  muBefore    Float?
  muAfter     Float?
  sigmaBefore Float?
  sigmaAfter  Float?

  @@unique([matchId, userId])
  @@index([userId])
}

//  결과 보고를 정확히 한 번만 확정하기 위한 자물쇠. 조건부 갱신(CAS)의 대상이다.
enum MatchState {
  Created
  InProgress
  Finished
}
```

`model Match { ... }` 안에 네 줄을 더한다 (`targetRating`은 그대로 둔다):

```prisma
model Match {
  id           String     @id @unique
  queueId      Int
  targetRating Int
  targetMmr    Int        @default(1000)
  state        MatchState @default(Created)
  createdAt    DateTime   @default(now())
  startedAt    DateTime?
  endedAt      DateTime?
  playerList   String[]
}
```

- [ ] **Step 2: 마이그레이션 SQL을 손으로 쓴다**

`prisma/migrations/20260817000000_match_result_rating_additive/migration.sql`:

```sql
--  UserStats를 UserRating으로 옮긴다. 이름만 바꾸는 게 아니라 어휘가 바뀐다:
--  eloRating -> mmr, wins -> firstPlaces(FFA에선 1등이 승리), 승/무/패의 나머지와 tier는 버린다.
--  mu/sigma는 지금까지 존재한 적이 없으므로 기본값에서 시작한다 — 기존 mmr 1000과 일치한다
--  (mu - 3*sigma = 25 - 25 = 0 -> mmr 1000).
CREATE TABLE "UserRating" (
    "id"           TEXT NOT NULL,
    "userId"       TEXT NOT NULL,
    "queueId"      INTEGER NOT NULL,
    "mu"           DOUBLE PRECISION NOT NULL DEFAULT 25,
    "sigma"        DOUBLE PRECISION NOT NULL DEFAULT 8.333333333333334,
    "mmr"          INTEGER NOT NULL DEFAULT 1000,
    "gamesPlayed"  INTEGER NOT NULL DEFAULT 0,
    "firstPlaces"  INTEGER NOT NULL DEFAULT 0,
    "placementSum" INTEGER NOT NULL DEFAULT 0,
    "createdAt"    TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updatedAt"    TIMESTAMP(3) NOT NULL,
    CONSTRAINT "UserRating_pkey" PRIMARY KEY ("id")
);

INSERT INTO "UserRating" ("id", "userId", "queueId", "mu", "sigma", "mmr", "gamesPlayed", "firstPlaces", "placementSum", "createdAt", "updatedAt")
SELECT "id", "userId", "queueId", 25, 8.333333333333334, "eloRating", "gamesPlayed", "wins", 0, "createdAt", "updatedAt"
FROM "UserStats";

CREATE UNIQUE INDEX "UserRating_userId_queueId_key" ON "UserRating"("userId", "queueId");

--  Match에 판의 생애를 더한다. targetRating은 T6에서 지운다.
CREATE TYPE "MatchState" AS ENUM ('Created', 'InProgress', 'Finished');

ALTER TABLE "Match" ADD COLUMN "targetMmr" INTEGER NOT NULL DEFAULT 1000;
ALTER TABLE "Match" ADD COLUMN "state" "MatchState" NOT NULL DEFAULT 'Created';
ALTER TABLE "Match" ADD COLUMN "startedAt" TIMESTAMP(3);
ALTER TABLE "Match" ADD COLUMN "endedAt" TIMESTAMP(3);

UPDATE "Match" SET "targetMmr" = "targetRating";

--  참가자 행. 기존 매치는 playerList를 펼쳐 채운다(결과는 없으므로 placement는 null).
CREATE TABLE "MatchParticipant" (
    "id"          TEXT NOT NULL,
    "matchId"     TEXT NOT NULL,
    "userId"      TEXT NOT NULL,
    "placement"   INTEGER,
    "mmrBefore"   INTEGER,
    "mmrAfter"    INTEGER,
    "muBefore"    DOUBLE PRECISION,
    "muAfter"     DOUBLE PRECISION,
    "sigmaBefore" DOUBLE PRECISION,
    "sigmaAfter"  DOUBLE PRECISION,
    CONSTRAINT "MatchParticipant_pkey" PRIMARY KEY ("id")
);

INSERT INTO "MatchParticipant" ("id", "matchId", "userId")
SELECT gen_random_uuid()::text, m."id", u
FROM "Match" m, unnest(m."playerList") AS u;

CREATE UNIQUE INDEX "MatchParticipant_matchId_userId_key" ON "MatchParticipant"("matchId", "userId");
CREATE INDEX "MatchParticipant_userId_idx" ON "MatchParticipant"("userId");
```

> `gen_random_uuid()`는 PostgreSQL 13+ 내장이다. 컨테이너가 더 낮은 버전이면
> `md5(random()::text || clock_timestamp()::text)::uuid::text`로 바꾼다.

- [ ] **Step 3: 스키마가 유효하고 SQL이 실제로 도는지 확인한다**

```bash
cd lop-backend
pnpm --filter @lop/database exec prisma validate
pnpm --filter @lop/database build
```
Expected: `The schema at prisma/schema.prisma is valid` + prisma generate 성공.

이어서 마이그레이션을 **빈 DB가 아니라 기존 데이터가 있는 DB**에 걸어 본다(로컬 개발 DB):

```bash
pnpm --filter @lop/database exec prisma migrate deploy
```
Expected: `Applying migration 20260817000000_match_result_rating_additive` 후 에러 없음.

- [ ] **Step 4: 데이터가 실제로 옮겨졌는지 눈으로 확인한다**

```bash
pnpm --filter @lop/database exec prisma studio
```
또는 psql로:
```sql
SELECT count(*) FROM "UserStats";      -- 예: 12
SELECT count(*) FROM "UserRating";     -- 같아야 한다
SELECT "mmr", "mu", "sigma" FROM "UserRating" LIMIT 3;   -- 1000 / 25 / 8.333...
SELECT count(*) FROM "MatchParticipant";                  -- Match들의 playerList 길이 합과 같아야 한다
```
Expected: `UserStats`와 `UserRating`의 행 수가 같고, 모든 `mmr`이 1000, `MatchParticipant` 수가 참가자 총합과 일치.

- [ ] **Step 5: 루트 빌드가 여전히 통과하는지 확인한다**

```bash
pnpm build
```
Expected: 전부 성공 — **순수 추가라 아무 소비처도 안 깨진다.** 깨지면 무언가를 지운 것이니 되돌린다.

- [ ] **Step 6: 커밋**

```bash
git add packages/database/prisma/schema.prisma packages/database/prisma/migrations
git commit -m "feat(db): 레이팅·경기결과 표를 더한다 (옛 표 유지)

UserRating(mu/sigma/mmr) · MatchParticipant · Match의 생애 컬럼을 추가하고
기존 UserStats·playerList의 값을 그대로 옮긴다. 지우는 것은 없어 동작 무변화."
```

---

## Task 2: lobby-server를 `UserRating`으로 옮긴다

**Files:**
- Create: `apps/lobby-server/src/interfaces/user-rating.interface.ts`, `factories/user-rating.factory.ts`, `daos/user-rating.dao.postgres.ts`, `daos/user-rating.dao.redis.ts`, `mappers/entities/user-rating.mapper.ts`, `repositories/user-rating.repository.ts`, `dtos/user-rating.dto.ts`, `mappers/controllers/user-rating.mapper.ts`, `services/user-rating.service.ts`, `controllers/user-rating.controller.ts`, `routes/user-rating.route.ts`
- Modify: `apps/lobby-server/src/services/user.service.ts`, `apps/lobby-server/src/main.ts`, `packages/server-core/src/interfaces/responseCode.interface.ts`
- Test: `apps/lobby-server/test/integration/userRating.integration.test.ts`

**Interfaces:**
- Consumes: T1의 Prisma 모델 `UserRating`
- Produces:
  - `interface UserRating { id: string; userId: string; queueId: number; mu: number; sigma: number; mmr: number; gamesPlayed: number; firstPlaces: number; placementSum: number }`
  - `class UserRatingRepository extends CacheCrudRepository<UserRating, UserRatingEntity>`
  - `UserRatingService.findUserRatingById(userId: string, queueId: number): Promise<GetUserRatingResponseDto>`
  - `GetUserRatingResponseDto { code: number; userRating?: UserRatingResponseDto }`, `UserRatingResponseDto { userId, queueId, mu, sigma, mmr, gamesPlayed, firstPlaces, placementSum }`
  - HTTP `GET /user/:userId/rating?queueId=<n>` (본인 또는 서비스만)
  - `ResponseCode.USER_RATING_NOT_EXIST = 70000`

- [ ] **Step 1: 실패하는 통합 테스트를 쓴다**

`apps/lobby-server/test/integration/userRating.integration.test.ts`:

```ts
import request from 'supertest';
import { App } from '@lop/server-core/express';
import AuthRoute from '@routes/auth.route';
import UserRoute from '@routes/user.route';
import UserRatingRoute from '@routes/user-rating.route';
import { resetTables, connectRedis, disconnectAll } from './db';

const app = new App([
    new AuthRoute(), new UserRoute(), new UserRatingRoute(),
]).getServer();

let testIp = 0;

//  userAccess.integration.test.ts와 같은 방식이다 — 익명 가입으로 계정과 토큰을 함께 얻는다.
//  레이트리밋이 IP 단위라 계정마다 다른 X-Forwarded-For를 쓴다.
async function 계정을_만든다(): Promise<{ userId: string; accessToken: string }> {
    testIp += 1;
    const response = await request(app)
        .post('/auth/anonymous')
        .set('X-Forwarded-For', `198.51.100.${testIp}`)
        .send();
    return { userId: response.body.userId, accessToken: response.body.accessToken };
}

describe('GET /user/:userId/rating', () => {
    beforeAll(connectRedis);
    beforeEach(async () => { await resetTables(); });
    afterAll(disconnectAll);

    it('가입한 유저는 큐마다 레이팅 행을 갖고, 시작값은 1000이다', async () => {
        const { userId, accessToken } = await 계정을_만든다();

        const response = await request(app)
            .get(`/user/${userId}/rating?queueId=1`)
            .set('Authorization', `Bearer ${accessToken}`)
            .send();

        expect(response.status).toBe(200);
        expect(response.body.userRating.mmr).toBe(1000);
        expect(response.body.userRating.gamesPlayed).toBe(0);
        expect(response.body.userRating.firstPlaces).toBe(0);
    });

    it('남의 레이팅은 볼 수 없다', async () => {
        const 나 = await 계정을_만든다();
        const 남 = await 계정을_만든다();

        const response = await request(app)
            .get(`/user/${남.userId}/rating?queueId=1`)
            .set('Authorization', `Bearer ${나.accessToken}`)
            .send();

        expect(response.status).toBe(403);
    });
});
```

> `resetTables`가 지우는 표 목록에 `UserRating`이 빠져 있으면 두 번째 테스트부터 오염된다.
> `test/integration/db.ts`를 열어 목록에 `UserRating`(과 `MatchParticipant`)을 더한다.

- [ ] **Step 2: 실패를 확인한다**

```bash
cd lop-backend
pnpm --filter lobby-server test:integration -- userRating
```
Expected: FAIL — `Cannot find module '@routes/user-rating.route'`

- [ ] **Step 3: 도메인·저장소 계층을 만든다**

`src/interfaces/user-rating.interface.ts`:
```ts
export interface UserRating {
    id: string;
    userId: string;
    queueId: number;
    mu: number;
    sigma: number;
    mmr: number;
    gamesPlayed: number;
    firstPlaces: number;
    placementSum: number;
}
```

`src/factories/user-rating.factory.ts`:
```ts
import { UserRating } from '@interfaces/user-rating.interface';

//  기본값은 OpenSkill의 시작 상태(mu=25, sigma=25/3)와 거기서 나오는 mmr 1000이다.
//  세 값은 서로 묶여 있다 — 하나만 바꾸면 신규 유저가 시작부터 어긋난다.
export class UserRatingFactory {
    public static create(properties?: Partial<UserRating>): UserRating {
        return { ...UserRatingFactory.createDefault(), ...properties };
    }

    private static createDefault(): UserRating {
        return {
            id: '',
            userId: '',
            queueId: 1,
            mu: 25,
            sigma: 25 / 3,
            mmr: 1000,
            gamesPlayed: 0,
            firstPlaces: 0,
            placementSum: 0,
        };
    }
}
```

`src/daos/user-rating.dao.postgres.ts`:
```ts
import { PrismaClient, UserRating as UserRatingEntity } from '@lop/database';
import { DaoPostgresBase } from '@lop/server-core';
import { prismaClient } from '@lop/server-core/postgres';

export class UserRatingDaoPostgres extends DaoPostgresBase<UserRatingEntity, PrismaClient["userRating"]> {
    constructor() {
        super(prismaClient, prismaClient.userRating);
    }
}
```

`src/daos/user-rating.dao.redis.ts`:
```ts
import { UserRating as UserRatingEntity } from '@lop/database';
import { DaoRedisBase } from '@lop/server-core/redis';

const TTL: number = 5 * 60;  //  sec
const USER_RATING_PREFIX: string = 'USER_RATING_PREFIX';

export class UserRatingDaoRedis extends DaoRedisBase<UserRatingEntity> {
    get Prefix(): string {
        return USER_RATING_PREFIX;
    }

    get TTL(): number {
        return TTL;
    }
}
```

`src/mappers/entities/user-rating.mapper.ts`:
```ts
import { UserRating } from '@interfaces/user-rating.interface';
import { UserRating as UserRatingEntity } from '@lop/database';
import { DomainEntityMapper } from '@lop/server-core';

export class UserRatingMapper implements DomainEntityMapper<UserRating, UserRatingEntity> {
    public toDomain(entity: UserRatingEntity): UserRating {
        return {
            id: entity.id,
            userId: entity.userId,
            queueId: entity.queueId,
            mu: entity.mu,
            sigma: entity.sigma,
            mmr: entity.mmr,
            gamesPlayed: entity.gamesPlayed,
            firstPlaces: entity.firstPlaces,
            placementSum: entity.placementSum,
        };
    }

    public toEntity(domain: UserRating): UserRatingEntity {
        return {
            id: domain.id,
            userId: domain.userId,
            queueId: domain.queueId,
            mu: domain.mu,
            sigma: domain.sigma,
            mmr: domain.mmr,
            gamesPlayed: domain.gamesPlayed,
            firstPlaces: domain.firstPlaces,
            placementSum: domain.placementSum,
        } as UserRatingEntity;
    }

    public toDomains(entities: Iterable<UserRatingEntity>): Iterable<UserRating> {
        return Array.from(entities, (entity) => this.toDomain(entity));
    }

    public toEntities(domains: Iterable<UserRating>): Iterable<UserRatingEntity> {
        return Array.from(domains, (domain) => this.toEntity(domain));
    }

    public getEntityFieldName<K extends keyof UserRating>(field: K): string {
        switch (field) {
            default: return field;
        }
    }

    public toEntityValue<K extends keyof UserRating>(field: K, value: UserRating[K]): any {
        switch (field) {
            default: return value;
        }
    }
}
```

`src/repositories/user-rating.repository.ts`:
```ts
import { UserRating } from '@interfaces/user-rating.interface';
import { UserRating as UserRatingEntity } from '@lop/database';
import { CacheCrudRepository } from '@lop/server-core';
import { UserRatingDaoPostgres } from '@daos/user-rating.dao.postgres';
import { UserRatingDaoRedis } from '@daos/user-rating.dao.redis';
import { UserRatingMapper } from '@mappers/entities/user-rating.mapper';

export class UserRatingRepository extends CacheCrudRepository<UserRating, UserRatingEntity> {
    constructor() {
        super(new UserRatingDaoPostgres(), new UserRatingDaoRedis(), new UserRatingMapper());
    }
}
```

- [ ] **Step 4: 응답·서비스·라우트를 만든다**

`src/dtos/user-rating.dto.ts`:
```ts
import { ResponseBase } from '@lop/server-core';

export class UserRatingResponseDto {
    public userId: string;
    public queueId: number;
    public mu: number;
    public sigma: number;
    public mmr: number;
    public gamesPlayed: number;
    public firstPlaces: number;
    public placementSum: number;
}

export class GetUserRatingResponseDto implements ResponseBase {
    public code: number;
    public userRating?: UserRatingResponseDto;
}
```

`src/mappers/controllers/user-rating.mapper.ts`:
```ts
import { UserRating } from '@interfaces/user-rating.interface';
import { UserRatingResponseDto } from '@dtos/user-rating.dto';

export class UserRatingMapper {
    public static toUserRatingResponseDto(userRating: UserRating): UserRatingResponseDto {
        return {
            userId: userRating.userId,
            queueId: userRating.queueId,
            mu: userRating.mu,
            sigma: userRating.sigma,
            mmr: userRating.mmr,
            gamesPlayed: userRating.gamesPlayed,
            firstPlaces: userRating.firstPlaces,
            placementSum: userRating.placementSum,
        };
    }
}
```

`src/services/user-rating.service.ts`:
```ts
import { GetUserRatingResponseDto } from '@dtos/user-rating.dto';
import { UserRatingRepository } from '@repositories/user-rating.repository';
import { ResponseCode } from '@lop/server-core';
import { UserRatingMapper } from '@mappers/controllers/user-rating.mapper';

class UserRatingService {

    private userRatingRepository = new UserRatingRepository();

    public async findUserRatingById(userId: string, queueId: number): Promise<GetUserRatingResponseDto> {
        try {
            const userRating = await this.userRatingRepository.findWhere([
                ['userId', userId],
                ['queueId', queueId],
            ]);

            if (!userRating) {
                return {
                    code: ResponseCode.USER_RATING_NOT_EXIST
                };
            }

            return {
                code: ResponseCode.SUCCESS,
                userRating: UserRatingMapper.toUserRatingResponseDto(userRating),
            };
        } catch (error) {
            return Promise.reject(error);
        }
    }
}

export default UserRatingService;
```

> ⚠️ 옛 `UserStatsService`는 조회 직후 `save()`를 한 번 더 불렀다(읽기만 하는데 쓰기). 옮기면서
> **의도적으로 뺀다** — 조회할 때마다 쓰던 낭비를 없앤 08-04 슬라이스와 같은 방향이다.

`src/controllers/user-rating.controller.ts`:
```ts
import { NextFunction, Request, Response } from 'express';
import UserRatingService from '@services/user-rating.service';

class UserRatingController {
    private userRatingService = new UserRatingService();

    public getUserRatingById = async (req: Request, res: Response, next: NextFunction) => {
        try {
            const userId = req.params.userId;
            const queueId = Number(req.query.queueId);

            const response = await this.userRatingService.findUserRatingById(userId, queueId);

            res.status(200).json(response);
        } catch (error) {
            next(error);
        }
    };
}

export default UserRatingController;
```

> 기존 `user-stats.controller.ts`를 열어 상태 코드·에러 처리 관례를 그대로 맞춘다(위는 그 형태를 옮긴 것이다).

`src/routes/user-rating.route.ts`:
```ts
import { Router } from 'express';
import UserRatingController from '@controllers/user-rating.controller';
import { Routes } from '@lop/server-core';
import { authenticatePrincipal, requireSelfOrService } from '@lop/server-core/auth';

class UserRatingRoute implements Routes {
    public path = '/user';
    public router = Router();
    public userRatingController = new UserRatingController();

    constructor() {
        this.initializeRoutes();
    }

    private initializeRoutes() {
        this.router.get(
            `${this.path}/:userId/rating`,
            authenticatePrincipal,
            requireSelfOrService('userId'),
            this.userRatingController.getUserRatingById,
        );
    }
}

export default UserRatingRoute;
```

- [ ] **Step 5: 가입 시 레이팅 행을 만든다 + 라우트를 등록한다 + ResponseCode 이름을 바꾼다**

`src/services/user.service.ts` — `UserStatsFactory`/`userStatsRepository`를 쓰던 두 블록을 바꾼다.
**옛 `UserStats` 생성은 남겨 둔다**(T6에서 옛 표와 함께 지운다). 즉 가입 시 두 표에 다 쓴다:

```ts
//  기존 casualUserStats / rankedUserStats 저장 아래에 이어서 추가한다.
await this.userRatingRepository.save(UserRatingFactory.create({ userId: user.id, queueId: 1 }));
await this.userRatingRepository.save(UserRatingFactory.create({ userId: user.id, queueId: 2 }));
```
파일 상단에 `import { UserRatingFactory } from '@factories/user-rating.factory';`,
`import { UserRatingRepository } from '@repositories/user-rating.repository';`를 더하고
클래스 필드에 `private userRatingRepository = new UserRatingRepository();`를 더한다.

`src/main.ts` — 라우트 배열에 `new UserRatingRoute()`를 더한다(`new UserStatsRoute()`는 유지):
```ts
const app = new App([new IndexRoute(), new AuthRoute(), new UserRoute(), new UserLocationRoute(), new UserStatsRoute(), new UserRatingRoute(), new LobbyRoute(), new InternalRoute()]);
```

`packages/server-core/src/interfaces/responseCode.interface.ts` — **값은 그대로, 이름만**:
```ts
    public static readonly USER_RATING_NOT_EXIST = 70000;
```
옛 이름을 쓰던 두 곳(`lobby-server/src/services/user-stats.service.ts`,
`matchmaking-server/src/services/matchmaking.service.ts`)도 새 이름으로 바꾼다 — 안 바꾸면 컴파일이 깨진다.

- [ ] **Step 6: 테스트가 통과하는지 확인한다**

```bash
cd lop-backend
pnpm build
pnpm --filter lobby-server test:integration -- userRating
```
Expected: 빌드 성공 후 2 passed.

- [ ] **Step 7: 전체 테스트를 돌린다 (`server-core`를 건드렸다)**

```bash
pnpm build && pnpm test
```
Expected: 전부 green. `ResponseCode` 이름을 바꿨으므로 옛 이름이 남아 있으면 여기서 잡힌다.

- [ ] **Step 8: 커밋**

```bash
git add apps/lobby-server packages/server-core
git commit -m "feat(lobby): UserRating 계층을 만든다

레이팅의 주인을 mu/sigma/mmr 세 층으로 나눠 다시 세운다. 옛 UserStats는
아직 살아 있고(가입 시 양쪽에 다 쓴다) 소비처 이전은 다음 태스크다.
ResponseCode는 이름만 바꾸고 값 70000은 유지한다 — Unity가 같은 숫자를 복제한다."
```

---

## Task 3: matchmaking-server가 `mmr`을 읽게 한다

**Files:**
- Create: `apps/matchmaking-server/src/dtos/user-rating.dto.ts`, `apps/matchmaking-server/src/services/user-rating.service.ts`
- Modify: `apps/matchmaking-server/src/services/httpServices/lobbyServer.service.ts`, `apps/matchmaking-server/src/services/matchmaking.service.ts`
- Test: `apps/matchmaking-server/src/services/__tests__/matchmaking.service.request.test.ts` (기존 파일 갱신)

**Interfaces:**
- Consumes: T2의 `GET /user/:userId/rating?queueId=` → `{ code, userRating: { userId, queueId, mu, sigma, mmr, gamesPlayed, firstPlaces, placementSum } }`
- Produces: `UserRatingService.findUserRatingById(userId: string, queueId: number): Promise<GetUserRatingResponseDto>` (matchmaking 쪽), 티켓 발급이 쓰는 값 = `userRating.mmr`

- [ ] **Step 1: 기존 테스트를 새 경로로 고쳐 실패시킨다**

`apps/matchmaking-server/src/services/__tests__/matchmaking.service.request.test.ts`를 연다.
`findUserStatsById`를 목으로 세우고 `eloRating`을 돌려주던 부분을 찾아 바꾼다:

```ts
//  변경 전: jest.mock(...) 이 findUserStatsById -> { code: SUCCESS, userStats: { eloRating: 1200 } }
//  변경 후:
jest.mock('@services/user-rating.service', () => {
    return jest.fn().mockImplementation(() => ({
        findUserRatingById: jest.fn().mockResolvedValue({
            code: 0,   //  ResponseCode.SUCCESS
            userRating: { userId: 'U1', queueId: 1, mu: 25, sigma: 25 / 3, mmr: 1200, gamesPlayed: 0, firstPlaces: 0, placementSum: 0 },
        }),
    }));
});
```

그리고 "티켓의 rating은 유저의 mmr이다"를 단언하는 케이스를 확인/추가한다:

```ts
it('티켓은 유저의 mmr을 레이팅으로 싣는다', async () => {
    await service.requestMatchmaking(/* 기존 인자 그대로 */);

    expect(issueMatchmakingTicket).toHaveBeenCalledWith(
        expect.anything(), expect.anything(), expect.anything(), expect.anything(), 1200,
    );
});
```

> 기존 파일의 목 스타일(`jest.mock` vs 주입)을 먼저 읽고 **그 스타일에 맞춘다.** 위는 형태 예시다.

- [ ] **Step 2: 실패를 확인한다**

```bash
cd lop-backend
pnpm --filter matchmaking-server test -- matchmaking.service.request
```
Expected: FAIL — `Cannot find module '@services/user-rating.service'`

- [ ] **Step 3: 조회 경로를 만든다**

`apps/matchmaking-server/src/dtos/user-rating.dto.ts`:
```ts
import { ResponseBase } from '@lop/server-core';

export class UserRatingResponseDto {
    public userId: string;
    public queueId: number;
    public mu: number;
    public sigma: number;
    public mmr: number;
    public gamesPlayed: number;
    public firstPlaces: number;
    public placementSum: number;
}

export class GetUserRatingResponseDto implements ResponseBase {
    public code: number;
    public userRating?: UserRatingResponseDto;
}
```

`apps/matchmaking-server/src/services/user-rating.service.ts`:
```ts
import LobbyServerService from '@services/httpServices/lobbyServer.service';
import { GetUserRatingResponseDto } from '@dtos/user-rating.dto';

class UserRatingService {

    private lobbyServerService = new LobbyServerService();

    public async findUserRatingById(userId: string, queueId: number): Promise<GetUserRatingResponseDto> {
        try {
            return await this.lobbyServerService.findUserRatingById(userId, queueId);
        } catch (error) {
            return Promise.reject(error);
        }
    }
}

export default UserRatingService;
```

`httpServices/lobbyServer.service.ts` — `findUserStatsById` **옆에** 더한다(옛 것은 T6에서 지운다):
```ts
    public async findUserRatingById(userId: string, queueId: number): Promise<GetUserRatingResponseDto> {
        try {
            const url = `http://${this.host}:${this.port}/user/${userId}/rating?queueId=${queueId}`;
            const response = await internalHttpClient.get(url, { timeout: HTTP_TIMEOUT_MS });
            return response.data;
        } catch (error) {
            return Promise.reject(error);
        }
    }
```
파일 상단 import에 `import { GetUserRatingResponseDto } from '@dtos/user-rating.dto';`를 더한다.

- [ ] **Step 4: 티켓 발급이 `mmr`을 쓰게 바꾼다**

`apps/matchmaking-server/src/services/matchmaking.service.ts`:
```ts
//  필드 선언
private userRatingService: UserRatingService = new UserRatingService();

//  requestMatchmaking 안 (기존 getUserStatsResponse 블록을 대체)
const getUserRatingResponse = await this.userRatingService.findUserRatingById(user.id, queueId);
if (getUserRatingResponse.code !== ResponseCode.SUCCESS) {
    return { code: getUserRatingResponse.code };
} else if (!getUserRatingResponse.userRating) {
    return { code: ResponseCode.USER_RATING_NOT_EXIST };
}

const targetRating = getUserRatingResponse.userRating.mmr;
```
`import UserStatsService from '@services/user-stats.service';`를 `UserRatingService`로 바꾸고
`private userStatsService` 필드는 지운다(이 파일에서 더는 안 쓴다).

- [ ] **Step 5: 테스트가 통과하는지 확인한다**

```bash
pnpm build
pnpm --filter matchmaking-server test
```
Expected: 전부 green.

- [ ] **Step 6: 매칭이 실제로 돌아가는지 통합 테스트로 확인한다**

```bash
pnpm --filter matchmaking-server test:integration
```
Expected: 티켓 발급·소비 관련 통합 테스트 전부 green — **동작 무변화**의 핵심 증거다.

- [ ] **Step 7: 커밋**

```bash
git add apps/matchmaking-server
git commit -m "refactor(matchmaking): 티켓 레이팅 출처를 UserRating.mmr로 옮긴다

값은 그대로 1000이라 매칭 결과가 달라지지 않는다. 옛 user-stats 조회 경로는
T6에서 지운다."
```

---

## Task 4: `Match`에 생애와 참가자를 붙인다

**Files:**
- Modify: `apps/matchmaking-server/src/interfaces/match.interface.ts`, `factories/match.factory.ts`, `mappers/entities/match.mapper.ts`, `mappers/controllers/match.mapper.ts`, `daos/match.dao.postgres.ts`, `repositories/match.repository.ts`
- Test: `apps/matchmaking-server/src/mappers/entities/__tests__/match.mapper.test.ts`, `apps/matchmaking-server/test/integration/matchParticipant.integration.test.ts` (신규)

**Interfaces:**
- Consumes: T1의 `MatchParticipant` 모델, `Match.targetMmr`/`state`/`startedAt`/`endedAt`
- Produces:
  - `interface Match { id, queueId, targetRating, targetMmr, state, createdAt, startedAt, endedAt, playerList, rounds }` — **도메인의 `playerList`는 유지**(응답 DTO가 그대로 내려준다).
  - `MatchDaoPostgres.saveWithRounds(match: MatchEntity, rounds: MatchRoundEntity[], participantUserIds: string[], matchedUserIds?: string[], requiredTicketIds?: string[])` — **참가자 목록을 별도 인자로 받는다.** T6에서 `Match.playerList` 컬럼이 사라져도 이 시그니처는 그대로다.
  - `MatchRepository.saveConsumingTickets`가 `match.playerList`를 그 인자로 넘긴다.

- [ ] **Step 1: 실패하는 통합 테스트를 쓴다**

`apps/matchmaking-server/test/integration/matchParticipant.integration.test.ts`:

```ts
import { prismaClient } from '@lop/server-core/postgres';
import { connectAll, resetAll, disconnectAll } from './db';
import { MatchDaoPostgres } from '@daos/match.dao.postgres';

//  같은 폴더의 ticketClaim.integration.test.ts가 매치·티켓을 어떻게 세팅하는지 먼저 읽고
//  그 헬퍼(매치, 티켓)를 그대로 재사용한다.

describe('매치를 만들면 참가자 행이 함께 생긴다', () => {
    beforeAll(async () => { await connectAll(); });
    beforeEach(async () => { await resetAll(); });
    afterAll(async () => { await disconnectAll(); });

    it('playerList의 사람마다 placement가 빈 참가자 행이 하나씩 생긴다', async () => {
        const dao = new MatchDaoPostgres();

        await dao.saveWithRounds(
            매치('M1', ['U1', 'U2', 'U3']),
            [{ id: 'R1', matchId: 'M1', index: 0, gameModeId: 1, mapId: 1 }],
            ['U1', 'U2', 'U3'],   //  참가자 목록은 별도 인자다
            [],
            [],
        );

        const participants = await prismaClient.matchParticipant.findMany({
            where: { matchId: 'M1' },
            orderBy: { userId: 'asc' },
        });

        expect(participants.map(p => p.userId)).toEqual(['U1', 'U2', 'U3']);
        expect(participants.every(p => p.placement === null)).toBe(true);
    });

    it('같은 매치를 두 번 저장해도 참가자가 중복되지 않는다', async () => {
        const dao = new MatchDaoPostgres();
        const 라운드 = [{ id: 'R1', matchId: 'M1', index: 0, gameModeId: 1, mapId: 1 }];

        await dao.saveWithRounds(매치('M1', ['U1', 'U2']), 라운드, ['U1', 'U2'], [], []);
        await dao.saveWithRounds(매치('M1', ['U1', 'U2']), 라운드, ['U1', 'U2'], [], []);

        const count = await prismaClient.matchParticipant.count({ where: { matchId: 'M1' } });
        expect(count).toBe(2);
    });
});
```

- [ ] **Step 2: 실패를 확인한다**

```bash
cd lop-backend
pnpm --filter matchmaking-server test:integration -- matchParticipant
```
Expected: FAIL — 참가자 행이 0개.

- [ ] **Step 3: 도메인·매퍼를 확장한다**

`src/interfaces/match.interface.ts`:
```ts
import { MatchRound } from '@interfaces/matchRound.interface';
import { MatchState } from '@lop/database';

export interface Match {
    id: string;
    queueId: number;
    targetRating: number;   //  T6에서 제거
    targetMmr: number;
    state: MatchState;
    createdAt: Date;
    startedAt: Date | null;
    endedAt: Date | null;
    playerList: string[];
    rounds: MatchRound[];
};
```

`src/factories/match.factory.ts` — 기본값에 더한다:
```ts
            targetRating: 1000,
            targetMmr: 1000,
            state: 'Created',
            startedAt: null,
            endedAt: null,
```

`src/mappers/entities/match.mapper.ts` — `toDomain`/`toEntity` 양쪽에 새 필드를 더한다:
```ts
    public toDomain(entity: MatchEntity): Match {
        return {
            id: entity.id,
            queueId: entity.queueId,
            targetRating: entity.targetRating,
            targetMmr: entity.targetMmr,
            state: entity.state,
            createdAt: entity.createdAt,
            startedAt: entity.startedAt,
            endedAt: entity.endedAt,
            playerList: entity.playerList,
            rounds: [],
        };
    }

    public toEntity(domain: Match): MatchEntity {
        return {
            id: domain.id,
            queueId: domain.queueId,
            targetRating: domain.targetRating,
            targetMmr: domain.targetMmr,
            state: domain.state,
            createdAt: new Date(domain.createdAt),
            startedAt: domain.startedAt,
            endedAt: domain.endedAt,
            playerList: domain.playerList,
        };
    }
```

`src/mappers/controllers/match.mapper.ts` — `CreateMatchDto.toEntity`에서 `targetMmr`을 함께 채운다.
DTO에는 아직 `targetRating`만 있으므로 **같은 값을 둘 다에 넣는다**(T5에서 DTO를 바꾼다):
```ts
                targetRating: createMatchDto.targetRating,
                targetMmr: createMatchDto.targetRating,
```
`toMatchResponseDto`는 지금 그대로 둔다(응답 DTO 변경은 T5).

- [ ] **Step 4: 트랜잭션 안에서 참가자 행을 만든다**

`src/daos/match.dao.postgres.ts` — 시그니처에 `participantUserIds`를 **세 번째 인자**로 더하고,
라운드 교체 **바로 아래**에 참가자 생성을 넣는다:

```ts
    public async saveWithRounds(
        match: MatchEntity,
        rounds: MatchRoundEntity[],
        participantUserIds: string[] = [],
        matchedUserIds: string[] = [],
        requiredTicketIds: string[] = [],
    ): Promise<{ match: MatchEntity; consumedTicketIds: string[] }> {
```
```ts
                await tx.matchRound.deleteMany({ where: { matchId: match.id } });
                await tx.matchRound.createMany({ data: rounds });

                //  참가자 자리를 미리 깔아 둔다(결과는 아직 없으니 placement는 null).
                //  결과 보고는 이 빈 칸을 채우는 일이지 명단을 만드는 일이 아니다 —
                //  그래서 게임서버가 명단에 없는 userId를 끼워 넣을 수 없다.
                //  skipDuplicates: 같은 매치를 다시 저장해도 참가자가 불어나지 않는다.
                //  명단을 인자로 받는 이유: 진실원본이 MatchParticipant로 넘어가면
                //  Match 행에는 playerList가 없다(T6).
                await tx.matchParticipant.createMany({
                    data: participantUserIds.map(userId => ({ matchId: match.id, userId })),
                    skipDuplicates: true,
                });
```

`src/repositories/match.repository.ts` — `saveConsumingTickets`에서 명단을 넘긴다:
```ts
            const { match: savedEntity, consumedTicketIds } = await this.matchDao.saveWithRounds(
                matchEntity, roundEntities, match.playerList, matchedUserIds, requiredTicketIds,
            );
```

- [ ] **Step 5: 테스트가 통과하는지 확인한다**

```bash
pnpm build
pnpm --filter matchmaking-server test:integration -- matchParticipant
pnpm --filter matchmaking-server test
```
Expected: 신규 2 passed + 기존 단위 테스트 green. `match.mapper.test.ts`가 새 필드 때문에 깨지면
기대값에 `targetMmr`·`state`·`startedAt`·`endedAt`을 더해 고친다.

- [ ] **Step 6: 커밋**

```bash
git add apps/matchmaking-server
git commit -m "feat(match): 매치에 생애와 참가자 행을 붙인다

매치가 만들어질 때 참가자 자리를 placement=null로 미리 깔아 둔다. 결과 보고가
명단을 만드는 게 아니라 빈 칸을 채우게 하려는 것이다(슬라이스 C의 전제).
targetMmr은 아직 targetRating과 같은 값이라 동작 무변화."
```

---

## Task 5: 응답 계약의 어휘를 맞춘다 (백엔드 + Unity 3레포)

**Files:**
- Modify: `lop-backend/apps/matchmaking-server/src/dtos/match.dto.ts`, `src/mappers/controllers/match.mapper.ts`, `src/director/assignment.ts`
- Modify: `lop-backend/apps/room-server/src/dtos/match.dto.ts`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Domain/Match.cs`, `Assets/Scripts/WebAPI/Dto/MatchDto.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Domain/Match.cs`, `Assets/Scripts/WebAPI/Dto/MatchDto.cs`, `Assets/Scripts/WebAPI/ResponseCode.cs`
- Test: `lop-backend/apps/matchmaking-server/src/director/__tests__/assignment.test.ts`, `src/routes/__tests__/match.route.test.ts`

**Interfaces:**
- Consumes: T4의 `Match.targetMmr`
- Produces: `CreateMatchDto { queueId, targetMmr, playerList, rounds }`, `MatchResponseDto { id, queueId, targetMmr, playerList, rounds }`. **`playerList`는 그대로 남는다** — 게임서버 `LOPNetworkAuthenticator`(방 접속 인증)와 `canReadMatch`(조회 권한), `GameRuleSystem`(스폰 루프)이 읽는다.

> ⚠️ **JSON 필드 이름이 바뀌므로 백엔드와 Unity를 같은 커밋으로 맞춰야 한다.** 한쪽만 바꾸면
> 역직렬화가 조용히 0을 넣는다(예외가 안 난다). `targetRating`을 실제로 읽는 Unity 코드는
> 현재 **한 곳도 없어서**(로컬 에디터 픽스처가 값을 넣기만 한다) 사고가 나도 안 보인다 — 그래서 더 위험하다.

- [ ] **Step 1: 테스트를 새 이름으로 고쳐 실패시킨다**

`src/director/__tests__/assignment.test.ts`:
```ts
    it('매치의 targetMmr은 묶인 티켓 레이팅의 평균이다', async () => {
        ...
        expect.objectContaining({ targetMmr: 1150 }),
```
`src/routes/__tests__/match.route.test.ts`의 픽스처도 `targetRating: 1000` → `targetMmr: 1000`.

- [ ] **Step 2: 실패를 확인한다**

```bash
cd lop-backend
pnpm --filter matchmaking-server test -- assignment
```
Expected: FAIL — `targetMmr`이 undefined.

- [ ] **Step 3: 백엔드 DTO·매퍼·디렉터를 바꾼다**

`apps/matchmaking-server/src/dtos/match.dto.ts` — 두 곳의 `targetRating`을 `targetMmr`로:
```ts
export class CreateMatchDto {
    @IsNumber()
    public queueId: number;

    @IsNumber()
    public targetMmr: number;
    ...
}

export class MatchResponseDto {
    public id: string;
    public queueId: number;
    public targetMmr: number;
    public playerList: string[];
    public rounds: MatchRoundDto[];
}
```

`src/mappers/controllers/match.mapper.ts`:
```ts
        public static toEntity(createMatchDto: CreateMatchDto) {
            return MatchFactory.create({
                queueId: createMatchDto.queueId,
                targetRating: createMatchDto.targetMmr,   //  T6에서 이 줄이 사라진다
                targetMmr: createMatchDto.targetMmr,
                ...
            });
        }

    public static toMatchResponseDto(match: Match): MatchResponseDto {
        return {
            id: match.id,
            queueId: match.queueId,
            targetMmr: match.targetMmr,
            playerList: match.playerList,
            rounds: ...,
        };
    }
```

`src/director/assignment.ts`:
```ts
    const targetMmr = Math.round(picked.reduce((sum, ticket) => sum + ticket.rating, 0) / picked.length);

    const createMatchDto: CreateMatchDto = {
        ...
        targetMmr: targetMmr,
        ...
    };
```

`apps/room-server/src/dtos/match.dto.ts` — `public targetRating: number;` → `public targetMmr: number;`

- [ ] **Step 4: Unity 4파일을 바꾼다**

`LeagueOfPhysical-Server/Assets/Scripts/Domain/Match.cs`,
`LeagueOfPhysical-Server/Assets/Scripts/WebAPI/Dto/MatchDto.cs`,
`LeagueOfPhysical-Client/Assets/Scripts/Domain/Match.cs`,
`LeagueOfPhysical-Client/Assets/Scripts/WebAPI/Dto/MatchDto.cs`:
```csharp
        public int targetMmr;      //  was: targetRating
```

`LeagueOfPhysical-Client/Assets/Scripts/WebAPI/ResponseCode.cs` — **값 유지, 이름만**:
```csharp
        public const int USER_RATING_NOT_EXIST = 70000;
```

> 서버 레포의 로컬 픽스처 `ConfigureRoomComponent.cs`가 `targetRating = 1500`을 쓴다.
> 이 파일은 **커밋하지 않는 로컬 픽스처**이므로, 컴파일이 깨지면 `targetMmr = 1500`으로 고치되
> **커밋에는 포함하지 않는다**(기존 관례).

- [ ] **Step 5: 세 레포가 다 컴파일되는지 확인한다**

```bash
cd lop-backend && pnpm build && pnpm test
```
Expected: green.

Unity 두 프로젝트는 **에디터 컴파일**로 확인한다(UnityMCP `refresh_unity` 후 콘솔 에러 0).
클라 인스턴스는 반드시 `unity_instance`를 `LeagueOfPhysical-Client@<hash>`로 지정한다.

- [ ] **Step 6: 커밋 (레포별로 각각)**

```bash
# lop-backend
git add apps/matchmaking-server apps/room-server
git commit -m "refactor(match): 응답 계약의 targetRating을 targetMmr로 바꾼다"

# LeagueOfPhysical-Server / -Client (각 레포에서)
git add Assets/Scripts/Domain/Match.cs Assets/Scripts/WebAPI/Dto/MatchDto.cs
git commit -m "refactor(match): targetRating을 targetMmr로 맞춘다

백엔드 응답 필드 이름이 바뀌었다. 읽는 코드는 없지만 이름이 어긋나면
역직렬화가 조용히 0을 넣으므로 같이 맞춘다."
```

---

## Task 6: 옛 것을 지운다

**Files:**
- Delete: `apps/lobby-server/src/{interfaces/user-stats.interface.ts, factories/user-stats.factory.ts, daos/user-stats.dao.postgres.ts, daos/user-stats.dao.redis.ts, mappers/entities/user-stats.mapper.ts, mappers/controllers/user-stats.mapper.ts, repositories/user-stats.repository.ts, services/user-stats.service.ts, controllers/user-stats.controller.ts, routes/user-stats.route.ts, dtos/user-stats.dto.ts}`
- Delete: `apps/matchmaking-server/src/{services/user-stats.service.ts, dtos/user-stats.dto.ts}`
- Modify: `apps/lobby-server/src/main.ts`, `apps/lobby-server/src/services/user.service.ts`, `apps/matchmaking-server/src/services/httpServices/lobbyServer.service.ts`, `apps/matchmaking-server/src/interfaces/match.interface.ts`, `factories/match.factory.ts`, `mappers/entities/match.mapper.ts`, `mappers/controllers/match.mapper.ts`, `daos/match.dao.postgres.ts`, `repositories/match.repository.ts`, `packages/database/prisma/schema.prisma`
- Modify: `apps/lobby-server/test/integration/cacheAside.integration.test.ts` (대역을 `UserRating`으로)
- Create: `packages/database/prisma/migrations/20260817100000_drop_user_stats_and_target_rating/migration.sql`

**Interfaces:**
- Consumes: T2~T5가 만든 새 경로 전부
- Produces: `UserStats` 모델·파일·라우트 소멸, `Match.targetRating`·`Match.playerList` 컬럼 소멸.
  **명단의 진실원본은 `MatchParticipant`가 되고**, `MatchRepository.findById`가 거기서 읽어
  도메인의 `playerList`를 채운다(라운드를 다루는 방식과 똑같다). 응답 DTO의 `playerList`는 **그대로 남는다** —
  게임서버 방 접속 인증이 읽는 계약이다.

- [ ] **Step 1: 참조가 정말 0인지 먼저 센다**

```bash
cd lop-backend
grep -rn "UserStats\|user-stats\|userStats" --include=*.ts apps packages | grep -v node_modules
grep -rn "targetRating" --include=*.ts apps packages | grep -v node_modules | grep -v generated
grep -rn "playerList" --include=*.ts apps packages | grep -v node_modules | grep -v generated
```
Expected: `user.service.ts`(가입 시 이중 쓰기), `cacheAside.integration.test.ts`(대역),
`lobbyServer.service.ts`(옛 HTTP 메서드), `match.*`(T4에서 남긴 `targetRating` 필드)만 남아 있어야 한다.
`playerList`는 **도메인·DTO·정책(`matchAccess.ts`)에만** 남아야 한다 — 엔티티(`MatchEntity`)를
직접 읽는 곳이 또 있으면 그것도 참가자에서 채우도록 고쳐야 한다.
**그 밖의 것이 나오면 지우기 전에 왜 남았는지 확인한다.**

- [ ] **Step 2: 남은 소비처를 끊는다**

`apps/lobby-server/src/services/user.service.ts` — `UserStatsFactory`/`userStatsRepository` 관련
import·필드·저장 2줄을 지운다(`UserRating` 저장만 남는다).

`apps/lobby-server/src/main.ts` — 라우트 배열에서 `new UserStatsRoute()`와 그 import를 지운다.

`apps/matchmaking-server/src/services/httpServices/lobbyServer.service.ts` — `findUserStatsById`
메서드와 `GetUserStatsResponseDto` import를 지운다.

`apps/lobby-server/test/integration/cacheAside.integration.test.ts` — 대역을 `UserRating`으로 바꾼다:
```ts
import { UserRating as UserRatingEntity } from '@lop/database';
import { UserRating } from '@interfaces/user-rating.interface';
import { UserRatingDaoRedis } from '@daos/user-rating.dao.redis';
import { UserRatingMapper } from '@mappers/entities/user-rating.mapper';

const 전적 = (판수: number): UserRating => ({
    id: 'S1',
    userId: 'U1',
    queueId: 1,
    mu: 25,
    sigma: 25 / 3,
    mmr: 1000,
    gamesPlayed: 판수,
    firstPlaces: 0,
    placementSum: 0,
});
```
`느린DB implements CrudDao<UserStatsEntity>` → `CrudDao<UserRatingEntity>`, 나머지 타입 인자도 함께.

`apps/matchmaking-server/src/{interfaces/match.interface.ts, factories/match.factory.ts, mappers/entities/match.mapper.ts, mappers/controllers/match.mapper.ts}` — `targetRating` 줄을 전부 지운다
(T4·T5에서 `targetMmr`이 이미 짝으로 들어가 있다).

**명단의 진실원본을 옮긴다.** `mappers/entities/match.mapper.ts`에서 `playerList`를 라운드와 같이 다룬다 —
매퍼는 별도 테이블을 모르고, 저장소가 채운다:
```ts
    //  toDomain: 라운드와 같은 이유로 여기서 못 채운다. MatchRepository가 참가자에서 읽어 넣는다.
    public toDomain(entity: MatchEntity): Match {
        return { ..., playerList: [], rounds: [] };
    }

    //  toEntity: playerList 줄을 지운다 (엔티티에 그 컬럼이 없다).
```

`apps/matchmaking-server/src/daos/match.dao.postgres.ts` — 참가자 읽기를 더한다:
```ts
    public async findParticipantUserIds(matchId: string): Promise<string[]> {
        try {
            const rows = await this.prismaClient.matchParticipant.findMany({
                where: { matchId },
                select: { userId: true },
                orderBy: { userId: 'asc' },
            });
            return rows.map(row => row.userId);
        } catch (error) {
            return Promise.reject(error);
        }
    }
```

`apps/matchmaking-server/src/repositories/match.repository.ts` — 라운드 옆에서 함께 채운다:
```ts
    public async findById(id: string): Promise<Match | null> {
        try {
            const match = await super.findById(id);
            if (!match) {
                return null;
            }

            const roundEntities = await this.roundDao.findAllByMatchId(id);
            const playerList = await this.matchDao.findParticipantUserIds(id);

            return { ...match, rounds: this.roundMapper.toDomains(roundEntities), playerList };
        } catch (error) {
            return Promise.reject(error);
        }
    }
```
그리고 `saveConsumingTickets`의 반환에서도 명단을 살려 준다(매퍼가 빈 배열을 주므로):
```ts
            return { match: { ...this.mapper.toDomain(savedEntity), rounds: match.rounds, playerList: match.playerList }, consumedTicketIds };
```

> ⚠️ `findById` 말고 `findAll`류로 매치를 읽는 곳이 생기면 `playerList`가 빈 배열로 나간다.
> 지금 매치 조회 경로는 `findMatchById` 하나뿐이라 문제가 없다 —
> **Step 1의 grep에서 다른 읽기 경로가 나오면 거기서 멈추고 함께 채운다.**

- [ ] **Step 3: 파일들을 지운다**

```bash
cd lop-backend
git rm apps/lobby-server/src/interfaces/user-stats.interface.ts \
       apps/lobby-server/src/factories/user-stats.factory.ts \
       apps/lobby-server/src/daos/user-stats.dao.postgres.ts \
       apps/lobby-server/src/daos/user-stats.dao.redis.ts \
       apps/lobby-server/src/mappers/entities/user-stats.mapper.ts \
       apps/lobby-server/src/mappers/controllers/user-stats.mapper.ts \
       apps/lobby-server/src/repositories/user-stats.repository.ts \
       apps/lobby-server/src/services/user-stats.service.ts \
       apps/lobby-server/src/controllers/user-stats.controller.ts \
       apps/lobby-server/src/routes/user-stats.route.ts \
       apps/lobby-server/src/dtos/user-stats.dto.ts \
       apps/matchmaking-server/src/services/user-stats.service.ts \
       apps/matchmaking-server/src/dtos/user-stats.dto.ts
```

- [ ] **Step 4: 빌드로 잔재를 잡는다**

```bash
pnpm build
```
Expected: 성공. **실패하면 그게 요점이다** — 지운 파일을 아직 import하는 곳이 있다는 뜻이고,
테스트만 돌렸다면 못 잡았을 종류다(아무도 import 안 하는 파일은 ts-jest가 타입 검사를 건너뛴다).

- [ ] **Step 5: 스키마에서 옛 표·컬럼을 지운다**

`packages/database/prisma/schema.prisma`에서 `model UserStats { ... }` 블록을 통째로 지우고,
`model Match`에서 `targetRating Int`와 `playerList String[]` 줄을 지운다.

`prisma/migrations/20260817100000_drop_user_stats_and_target_rating/migration.sql`:
```sql
--  UserRating으로 이관이 끝났다(T1의 additive 마이그레이션이 값을 옮겼고, 이후 모든 소비처가
--  UserRating만 읽는다). 남겨 두면 아무도 안 쓰는데 값은 계속 들어 있어 다음에 보는 사람이
--  "살아있는 값"으로 오해한다.
DROP TABLE "UserStats";

--  targetMmr이 같은 값을 들고 있다.
ALTER TABLE "Match" DROP COLUMN "targetRating";

--  명단의 진실원본은 MatchParticipant다. 두 곳에 두면 결과 보고가 "명단에 있나"를 물을 때
--  어느 쪽을 믿을지가 생기고, 둘이 어긋나면 조용히 틀린 판정을 한다.
--  응답 DTO의 playerList는 참가자에서 뽑아 그대로 내려가므로 게임서버 계약은 안 바뀐다.
ALTER TABLE "Match" DROP COLUMN "playerList";
```

- [ ] **Step 6: 마이그레이션을 걸고 전체를 확인한다**

```bash
pnpm --filter @lop/database build
pnpm --filter @lop/database exec prisma migrate deploy
pnpm build && pnpm test
pnpm --filter lobby-server test:integration
pnpm --filter matchmaking-server test:integration
```
Expected: 전부 green.

- [ ] **Step 7: 커밋**

```bash
git add -A
git commit -m "chore: UserStats와 Match.targetRating을 지운다

이관이 끝나 아무도 안 읽는다. 남겨 두면 값이 계속 들어 있어 살아있는 값으로
오해하게 된다(대기표 하트비트를 지울 때와 같은 이유)."
```

---

## Task 7: 끝-끝 회귀 확인 — "지금과 똑같이 동작한다"

**Files:** (코드 변경 없음 — 검증만)

**Interfaces:**
- Consumes: T1~T6 전부
- Produces: 슬라이스 A 합격 판정

- [ ] **Step 1: 로컬 클러스터에 올린다**

```bash
cd lop-backend
gh workflow run backend-deploy.yml -f app=all
```
**`app=all`이어야 한다** — 마이그레이션이 있으므로 일부만 올리면 에러 없이 기능만 죽는다.

```bash
kubectl get pods -n <backend-namespace>
```
Expected: 세 파드 Running, 재시작 0.

- [ ] **Step 2: 마이그레이션이 실제로 적용됐는지 확인한다**

```bash
kubectl exec -it <postgres-pod> -- psql -U <user> -d <db> -c '\d "UserRating"'
kubectl exec -it <postgres-pod> -- psql -U <user> -d <db> -c 'SELECT count(*) FROM "UserStats";'
```
Expected: `UserRating` 존재, `UserStats`는 **relation does not exist**.

- [ ] **Step 3: 유니티 두 대로 한 판 돌린다**

로그인 → 로비 → 매칭 요청 → 매치 성사 → 게임 진입 → 5분(또는 강제 종료) → 로비 복귀.

Expected — **전부 지금과 똑같아야 한다**:
- 매칭이 붙는다(레이팅 값이 1000으로 같으니 예전과 동일)
- 게임서버 방 접속 인증이 통과한다(← `playerList`가 응답에 그대로 있는지가 여기서 드러난다)
- 캐릭터가 스폰된다(`GameRuleSystem`이 `playerList`를 돈다)
- Unity 콘솔 에러 0

- [ ] **Step 4: DB에 흔적이 남았는지 확인한다**

```sql
SELECT "id", "queueId", "targetMmr", "state" FROM "Match" ORDER BY "createdAt" DESC LIMIT 1;
SELECT "userId", "placement" FROM "MatchParticipant" WHERE "matchId" = '<위 id>';
SELECT "userId", "mmr", "mu", "sigma" FROM "UserRating" WHERE "queueId" = 1 LIMIT 3;
```
Expected: 매치가 `state = 'Created'`, 참가자 행이 인원수만큼 있고 **`placement`는 전부 null**
(결과 보고는 슬라이스 C의 일이다), 모든 `mmr = 1000`.

- [ ] **Step 5: 세 레포를 main에 머지한다**

```bash
git checkout main && git merge --no-ff feature/match-rating-slice-a
```
`lop-backend`, `LeagueOfPhysical-Server`, `LeagueOfPhysical-Client` 각각.

---

## 이 슬라이스가 끝나면

- `UserRating`(μ·σ·mmr)이 레이팅의 유일한 주인이고, 매칭은 `mmr` 정수를 읽는다.
- `Match`가 판의 생애(`state`·`startedAt`·`endedAt`)를 갖고, 참가자 자리가 `placement = null`로 깔려 있다.
- 명단의 진실원본이 `MatchParticipant` 하나가 됐다. 응답의 `playerList`는 거기서 파생된다.
- **화면·동작은 하나도 안 바뀌었다.**

다음은 **슬라이스 B**(`@lop/rating` 순수 패키지 — 독립이라 언제든) 또는 **슬라이스 C**(결과 보고 → 그 빈 `placement`를 채우고 `mmr`을 실제로 움직이기).
