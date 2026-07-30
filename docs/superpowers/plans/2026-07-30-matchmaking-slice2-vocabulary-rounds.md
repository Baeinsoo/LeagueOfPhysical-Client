# 매치메이킹 슬라이스 2 — 필드 어휘 리네임 + `Match` 라운드화 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 매칭 도메인의 필드 이름을 업계 표준(`queueId`/`gameModeId`)으로 바꾸고 그 값을 슬라이스 1이 만든 Luban 테이블의 정수 기본키로 전환하며, `Match`가 게임·맵을 직접 들지 않고 `rounds[]`(원소 1개)로 들게 한다.

**Architecture:** DB 스키마 → 매칭 서버 → 로비/룸 서버 → 게임 서버 → 클라 순서로 흐른다. `MatchRound`는 별도 테이블이지만 `MatchRepository`(애그리게잇 루트)가 읽기·쓰기를 감춰서, 호출부는 `Match.rounds`를 평범한 필드처럼 쓴다. 게임 서버는 하드코딩된 씬 경로 대신 `rounds[0].mapId` → `TbMap.scenePath`로 맵을 로드한다.

**Tech Stack:** pnpm + turbo 모노레포(TypeScript, Express, Prisma 6, jest), Unity 6(C#, Assembly-CSharp), Luban 마스터데이터, k8s + ArgoCD GitOps.

## Global Constraints

- **설계 원천:** `docs/superpowers/specs/2026-07-27-matchmaking-standardization-design.md` — 특히 §8의 "슬라이스 2 확정 사항".
- **저장소는 `Baeinsoo/lop-backend` 모노레포다.** `re5nardo/LeagueOfPhysical-{Lobby,Matchmaking,Room}Server`는 2025-08-31 아카이브됐다. 로컬에 클론이 남아 있어도 **손대지 않는다.**
- **정수 id 값 고정:** `TbQueue` — Casual=1, Ranked=2. `TbGameMode` — FlapWang=1, Dodgeball=2, ObserverAvoid=3, RememberGame=4, TargetShooting=5. `TbMap` — FlapWangMap=1(gameModeId=1, scenePath=`Assets/Art/Scenes/FlapWangMap.unity`).
- **`WaitingRoom`이라는 *이름*은 이 슬라이스에서 바꾸지 않는다.** 필드(`matchType`/`subGameId`/`mapId`)만 바꾼다. 엔티티 삭제는 슬라이스 4.
- **`enum GameMode { Normal, Ranked }`는 전부 삭제한다** — 백엔드 3앱의 `src/interfaces/enums.ts`, 클라 `Assets/Scripts/Domain/Enums.cs`, 게임 서버 `Assets/Scripts/Domain/Enums.cs`. 남은 `enums.ts`가 빈 파일이 되면 파일째 지우고 import를 정리한다.
- **Prisma `generated/`는 커밋하지 않는다**(루트 `.gitignore`의 `generated/`). 스키마를 바꾼 뒤에는 반드시 `pnpm --filter @lop/database generate`를 돌려야 TypeScript가 컴파일된다.
- **커밋 메시지는 한국어**로 쓰고 아래 trailer를 붙인다:
  `Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>`
- **브랜치:** 각 저장소에서 `feature/matchmaking-slice2-vocabulary-rounds` 피처 브랜치를 쓴다. main 직접 커밋 금지.
- **Unity 컴파일 검증의 한계:** 클라 Unity 에디터는 워크트리가 아니라 **main 체크아웃**을 본다. 워크트리의 클라 `Assets/` 변경은 머지 전에는 에디터로 컴파일 검증할 수 없다. 게임 서버 저장소는 워크트리를 쓰지 않으므로 그 자리에서 검증 가능하다.

---

## File Structure

**`lop-backend/packages/database`**
- `prisma/schema.prisma` — 수정: `Match`/`MatchmakingTicket`/`WaitingRoom`/`UserStats` 필드, `MatchRound` 신설, `enum GameMode` 삭제
- `prisma/migrations/<ts>_matchmaking_vocabulary_and_rounds/migration.sql` — 신설(손으로 다듬은 SQL)

**`lop-backend/apps/matchmaking-server`**
- `src/interfaces/enums.ts` — 삭제
- `src/interfaces/matchRound.interface.ts` — 신설(도메인 `MatchRound`)
- `src/interfaces/match.interface.ts` — `queueId` + `rounds: MatchRound[]`
- `src/interfaces/matchmakingTicket.interface.ts`, `src/interfaces/waitingRoom.interface.ts` — 필드 3종
- `src/daos/matchRound.dao.postgres.ts` — 신설(`findAllByMatchId`/`deleteAllByMatchId`)
- `src/mappers/entities/matchRound.mapper.ts` — 신설
- `src/mappers/entities/match.mapper.ts` — enum 변환 제거, rounds는 매퍼 밖(리포지토리 소관)
- `src/repositories/match.repository.ts` — 애그리게잇 리포지토리로 확장(`save`/`findById` override)
- `src/dtos/*.dto.ts`, `src/factories/*.factory.ts`, `src/models/*.model.ts`, `src/mappers/controllers/*.mapper.ts` — 필드명·타입
- `src/loaders/masterdata.loader.ts` — `findGameModeByCode` 삭제
- `src/services/matchmaking.service.ts`, `matchmakingTicket.service.ts`, `waitingRoom.service.ts`, `user-stats.service.ts` — 호출부
- `src/services/httpServices/lobbyServer.service.ts` — 전적 조회 쿼리스트링
- `src/loaders/__tests__/masterdata.loader.test.ts` — 헬퍼 삭제분 반영
- `src/mappers/entities/__tests__/match.mapper.test.ts` — 신설

**`lop-backend/apps/lobby-server`**
- `src/interfaces/enums.ts` — 삭제
- `src/interfaces/user-stats.interface.ts`, `matchmakingTicket.interface.ts`, `waitingRoom.interface.ts`
- `src/dtos/user-stats.dto.ts`, `src/factories/user-stats.factory.ts`
- `src/mappers/entities/user-stats.mapper.ts` — enum 변환 헬퍼 2개 삭제(패스스루)
- `src/mappers/controllers/user-stats.mapper.ts`, `src/controllers/user-stats.controller.ts`, `src/services/user.service.ts`, `src/services/user-stats.service.ts`

**`lop-backend/apps/room-server`**
- `src/interfaces/enums.ts` — 삭제
- `src/interfaces/match.interface.ts`, `src/dtos/match.dto.ts`

**`LeagueOfPhysical-Server` (게임 서버, Unity)**
- `Assets/Scripts/Domain/Enums.cs` — `GameMode` 삭제
- `Assets/Scripts/Domain/Match.cs`, `Assets/Scripts/Domain/MatchRound.cs`(신설)
- `Assets/Scripts/WebAPI/Dto/MatchDto.cs`, `Assets/Scripts/WebAPI/Dto/MatchRoundDto.cs`(신설)
- `Assets/Scripts/WebAPI/Dto/MatchSetting.cs`, `Assets/Scripts/WebAPI/Dto/Request/NotifyStartServerRequest.cs` — 삭제(죽은 코드)
- `Assets/Scripts/WebAPI/WebAPI.cs` — `NotifyStartServer` 삭제
- `Assets/Scripts/Entrance/EntranceComponent/ConfigureRoomComponent.cs` — 에디터 픽스처
- `Assets/Scripts/Game/LOPRunner.cs` — 맵 경로를 `rounds[0]` → `TbMap`에서

**`LeagueOfPhysical-Client` (클라, Unity — 이 워크트리)**
- `Assets/Scripts/Domain/Enums.cs` — `GameMode` 삭제
- `Assets/Scripts/Domain/Match.cs`, `Assets/Scripts/Domain/MatchRound.cs`(신설), `Assets/Scripts/Domain/UserStats.cs`
- `Assets/Scripts/WebAPI/Dto/MatchDto.cs`, `MatchRoundDto.cs`(신설), `UserStatsDto.cs`, `Request/MatchmakingRequest.cs`
- `Assets/Scripts/WebAPI/WebAPI.cs` — 전적 조회 쿼리스트링
- `Assets/Scripts/Stores/IMatchmakingDataStore.cs`, `MatchmakingDataStore.cs`, `IUserDataStore.cs`, `UserDataStore.cs`
- `Assets/Scripts/Matchmaking/MatchStateMachine/States/RequestMatchmaking.cs`
- `Assets/Scripts/UI/Matchmaking/MatchmakingViewModel.cs`
- `Assets/Scripts/Entrance/EntranceComponent/CheckUserComponent.cs`

---

## Task 1: DB 스키마 + 마이그레이션

**Files:**
- Modify: `lop-backend/packages/database/prisma/schema.prisma`
- Create: `lop-backend/packages/database/prisma/migrations/<timestamp>_matchmaking_vocabulary_and_rounds/migration.sql`

**Interfaces:**
- Consumes: 없음(첫 태스크)
- Produces: Prisma 클라이언트 타입 — `Match { id: string; queueId: number; targetRating: number; createdAt: Date; playerList: string[] }`, `MatchRound { id: string; matchId: string; index: number; gameModeId: number; mapId: number }`, `MatchmakingTicket { id: string; creator: string; queueId: number; gameModeId: number; mapId: number; rating: number; createdAt: Date }`, `WaitingRoom { ..., queueId: number; gameModeId: number; mapId: number, ... }`, `UserStats { ..., queueId: number, ... }`. `Entity.GameMode`는 **더 이상 존재하지 않는다.**

- [ ] **Step 1: 브랜치 생성**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
git checkout -b feature/matchmaking-slice2-vocabulary-rounds
```

- [ ] **Step 2: `schema.prisma` 수정**

`packages/database/prisma/schema.prisma`에서 `UserStats`, `Match`, `MatchmakingTicket`, `WaitingRoom` 모델과 `enum GameMode`를 아래로 교체한다. `MatchRound`는 `Match` 바로 뒤에 새로 넣는다.

```prisma
model UserStats {
  id          String   @id @default(uuid())
  queueId     Int
  gamesPlayed Int      @default(0)
  wins        Int      @default(0)
  losses      Int      @default(0)
  draws       Int      @default(0)
  eloRating   Int      @default(1000)
  mmr         Int      @default(1000)
  tier        String   @default("BRONZE")
  createdAt   DateTime @default(now())
  updatedAt   DateTime @updatedAt
  userId      String

  @@unique([userId, queueId])
}

model Match {
  id           String   @id @unique
  queueId      Int
  targetRating Int
  createdAt    DateTime @default(now())
  playerList   String[]
}

// 한 매치 안의 한 게임. 지금은 매치마다 원소 1개(index 0)뿐이지만,
// 여러 게임을 연속으로 하되 최종 결과는 하나인 형태를 위해 목록으로 둔다.
// 복합 기본키 대신 대리 id를 두는 이유: 공용 DAO가 스칼라 `id`를 요구한다.
model MatchRound {
  id         String @id @default(uuid())
  matchId    String
  index      Int
  gameModeId Int
  mapId      Int

  @@unique([matchId, index])
}

model MatchmakingTicket {
  id         String   @id @unique
  creator    String
  queueId    Int
  gameModeId Int
  mapId      Int
  rating     Int
  createdAt  DateTime @default(now())
}

model WaitingRoom {
  id                    String            @id @unique
  queueId               Int
  gameModeId            Int
  mapId                 Int
  targetRating          Int
  createdAt             DateTime          @default(now())
  matchmakingTicketList String[]
  maxWaitingTime        Int
  minPlayerCount        Int
  maxPlayerCount        Int
  status                WaitingRoomStatus
}
```

그리고 `enum GameMode { Normal Ranked }` 블록을 **통째로 삭제**한다. (`enum Location`, `enum WaitingRoomStatus`, `enum RoomStatus`는 그대로 둔다 — 슬라이스 4·5 소관.)

- [ ] **Step 3: 스키마가 유효한지 확인**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
pnpm --filter @lop/database exec prisma validate
```
기대 출력: `The schema at prisma\schema.prisma is valid 🚀`

> 만약 `index`라는 필드 이름이 거부되면(Prisma가 예약어로 볼 가능성) 필드를 `roundIndex`로 바꾸고 **Task 2 이후의 모든 `index` 언급을 `roundIndex`로 함께 바꾼다** — 도메인 `MatchRound.index`는 그대로 두고 매퍼에서만 `roundIndex ↔ index`로 옮기면 바깥 계약(JSON의 `index`)은 흔들리지 않는다.

- [ ] **Step 4: 마이그레이션 SQL 생성용 임시 postgres 띄우기**

클러스터 DB는 건드리지 않는다. 섀도 DB만 필요하므로 버리는 컨테이너를 쓴다.

```bash
docker run --rm -d --name lop-shadow-db -e POSTGRES_PASSWORD=shadow -p 55432:5432 postgres:16
sleep 5
docker exec lop-shadow-db pg_isready -U postgres
```
기대 출력: `/var/run/postgresql:5432 - accepting connections`

- [ ] **Step 5: 마이그레이션 SQL 뽑기**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/packages/database
mkdir -p "prisma/migrations/20260730000000_matchmaking_vocabulary_and_rounds"
pnpm exec prisma migrate diff \
  --from-migrations ./prisma/migrations \
  --to-schema-datamodel ./prisma/schema.prisma \
  --shadow-database-url "postgresql://postgres:shadow@localhost:55432/postgres?schema=public" \
  --script > "prisma/migrations/20260730000000_matchmaking_vocabulary_and_rounds/migration.sql"
cat "prisma/migrations/20260730000000_matchmaking_vocabulary_and_rounds/migration.sql"
```
기대: `CreateTable "MatchRound"`, `AlterTable`로 `matchType`/`subGameId`/`mapId`/`gameMode` DROP + `queueId`/`gameModeId`/`mapId` ADD, `DropEnum "GameMode"`가 들어 있는 SQL.

- [ ] **Step 6: 마이그레이션 SQL을 손으로 다듬기**

생성된 SQL은 두 가지 이유로 그대로 쓸 수 없다. ① `NOT NULL` 컬럼을 **행이 있는 테이블에** 추가하므로 배포 시 실패한다. ② `UserStats`를 그냥 지우면 **기존 게스트 유저의 전적이 사라져** 로그인이 `USER_STATS_NOT_EXIST`로 깨진다(전적 행은 유저 생성 시에만 만들어진다).

생성된 파일 **맨 위**에 아래 블록을 넣는다. 매칭 중이던 티켓·대기방·매치는 버려도 되는 일시 데이터다.

```sql
-- 일시 데이터(매칭 중 상태)는 버린다. 새 컬럼이 NOT NULL이라 행이 남아 있으면 ADD COLUMN이 실패한다.
DELETE FROM "WaitingRoom";
DELETE FROM "MatchmakingTicket";
DELETE FROM "Match";
```

그리고 `UserStats` 관련 자동 생성 구문(`ALTER TABLE "UserStats" DROP COLUMN "gameMode"`, `ADD COLUMN "queueId" INTEGER NOT NULL`, 그리고 `DROP INDEX`/`CREATE UNIQUE INDEX`)을 **아래 블록으로 통째로 교체**한다. 유저 전적을 살려서 옮긴다.

```sql
-- 유저 전적은 살린다. 옛 enum 값 Normal/Ranked를 TbQueue의 정수 id로 옮긴다.
ALTER TABLE "UserStats" ADD COLUMN "queueId" INTEGER;
UPDATE "UserStats" SET "queueId" = CASE "gameMode"
    WHEN 'Normal' THEN 1   -- TbQueue: Casual
    WHEN 'Ranked' THEN 2   -- TbQueue: Ranked
END;
ALTER TABLE "UserStats" ALTER COLUMN "queueId" SET NOT NULL;
DROP INDEX IF EXISTS "UserStats_userId_gameMode_key";
ALTER TABLE "UserStats" DROP COLUMN "gameMode";
CREATE UNIQUE INDEX "UserStats_userId_queueId_key" ON "UserStats"("userId", "queueId");
```

`DROP TYPE "GameMode"`(또는 `DropEnum`) 구문은 **`UserStats` 블록 뒤에 오도록** 순서를 확인한다 — 컬럼이 아직 그 타입을 쓰는 상태에서 타입을 지우면 실패한다.

- [ ] **Step 7: 다듬은 SQL이 빈 DB에 적용되는지 확인**

```bash
docker exec lop-shadow-db psql -U postgres -c "DROP SCHEMA public CASCADE; CREATE SCHEMA public;"
cd /c/Users/re5na/workspace/LOP/lop-backend/packages/database
DATABASE_URL="postgresql://postgres:shadow@localhost:55432/postgres?schema=public" pnpm exec prisma migrate deploy
```
기대 출력: `2 migrations found in prisma/migrations` + `All migrations have been successfully applied.`

- [ ] **Step 8: 스키마와 마이그레이션이 어긋나지 않는지 확인**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/packages/database
pnpm exec prisma migrate diff \
  --from-url "postgresql://postgres:shadow@localhost:55432/postgres?schema=public" \
  --to-schema-datamodel ./prisma/schema.prisma \
  --script
```
기대 출력: `-- This is an empty migration.` (차이가 없다는 뜻. 무언가 출력되면 Step 6의 손질이 스키마와 어긋난 것이므로 고친다.)

- [ ] **Step 9: 데이터 보존 경로 확인 — 옛 스키마 + 옛 데이터 위에서 태워 본다**

이게 이 태스크에서 가장 중요한 검증이다. 클러스터 DB에는 **이미 유저와 전적 행이 있고**, 그 위에서 이 마이그레이션이 돌아야 한다.

먼저 DB를 비우고 **옛 마이그레이션만** 적용한다:
```bash
docker exec lop-shadow-db psql -U postgres -c "DROP SCHEMA public CASCADE; CREATE SCHEMA public;"
cd /c/Users/re5na/workspace/LOP/lop-backend/packages/database
mv "prisma/migrations/20260730000000_matchmaking_vocabulary_and_rounds" /tmp/slice2-migration
DATABASE_URL="postgresql://postgres:shadow@localhost:55432/postgres?schema=public" pnpm exec prisma migrate deploy
```
기대: `1 migration found` + `All migrations have been successfully applied.`

옛 모양의 데이터를 심는다:
```bash
docker exec lop-shadow-db psql -U postgres -c "
INSERT INTO \"User\" (id, username, email, \"passwordHash\", \"updatedAt\")
VALUES ('u1', 'tester', 't@example.com', 'x', now());
INSERT INTO \"UserStats\" (id, \"userId\", \"gameMode\", \"updatedAt\") VALUES
  ('s1', 'u1', 'Normal', now()),
  ('s2', 'u1', 'Ranked', now());
INSERT INTO \"Match\" (id, \"matchType\", \"subGameId\", \"mapId\", \"targetRating\")
VALUES ('m1', 'Normal', 'FlapWang', 'FlapWangMap', 1000);
"
```
기대: `INSERT 0 1` × 4.

새 마이그레이션을 되돌려 놓고 적용한다:
```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/packages/database
mv /tmp/slice2-migration "prisma/migrations/20260730000000_matchmaking_vocabulary_and_rounds"
DATABASE_URL="postgresql://postgres:shadow@localhost:55432/postgres?schema=public" pnpm exec prisma migrate deploy
docker exec lop-shadow-db psql -U postgres -c "SELECT id, \"queueId\" FROM \"UserStats\" ORDER BY id;"
docker exec lop-shadow-db psql -U postgres -c "SELECT count(*) FROM \"Match\";"
```
기대:
- 마이그레이션이 **에러 없이** 통과
- 전적 두 행이 살아 있고 `s1 | 1`, `s2 | 2`
- `Match`는 `0`(일시 데이터라 지운 게 맞다)

여기서 실패하면 Step 6의 손질이 잘못된 것이다 — 고치고 Step 7부터 다시 한다.

- [ ] **Step 10: 임시 postgres 정리 + Prisma 클라이언트 재생성**

```bash
docker stop lop-shadow-db
cd /c/Users/re5na/workspace/LOP/lop-backend
pnpm --filter @lop/database generate
```
기대 출력: `Generated Prisma Client (v6.x.x) to .\generated\client`

- [ ] **Step 11: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
git add packages/database/prisma
git commit -m "$(cat <<'EOF'
feat(db): 매칭 어휘를 queueId/gameModeId로, Match를 라운드 목록으로

matchType(enum)·subGameId(string)·mapId(string)를 슬라이스 1이 만든
Luban 테이블의 정수 기본키로 바꾸고, Match가 게임·맵을 직접 들지 않고
MatchRound 목록을 통해 갖도록 했다. enum GameMode는 삭제 — 큐는 이제
코드가 아니라 TbQueue 행이다.

마이그레이션은 티켓·대기방·매치(일시 데이터)는 버리고 UserStats는
Normal→1, Ranked→2로 옮겨 살린다. 전적 행은 유저 생성 시에만 만들어져서
지우면 기존 게스트 로그인이 깨진다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: 매칭 서버 — `Match` 애그리게잇에 라운드 붙이기

**Files:**
- Create: `lop-backend/apps/matchmaking-server/src/interfaces/matchRound.interface.ts`
- Create: `lop-backend/apps/matchmaking-server/src/daos/matchRound.dao.postgres.ts`
- Create: `lop-backend/apps/matchmaking-server/src/mappers/entities/matchRound.mapper.ts`
- Create: `lop-backend/apps/matchmaking-server/src/mappers/entities/__tests__/match.mapper.test.ts`
- Modify: `lop-backend/apps/matchmaking-server/src/interfaces/match.interface.ts`
- Modify: `lop-backend/apps/matchmaking-server/src/mappers/entities/match.mapper.ts`
- Modify: `lop-backend/apps/matchmaking-server/src/repositories/match.repository.ts`
- Modify: `lop-backend/apps/matchmaking-server/src/factories/match.factory.ts`
- Modify: `lop-backend/apps/matchmaking-server/src/models/match.model.ts`
- Modify: `lop-backend/apps/matchmaking-server/src/dtos/match.dto.ts`
- Modify: `lop-backend/apps/matchmaking-server/src/mappers/controllers/match.mapper.ts`

**Interfaces:**
- Consumes: Task 1의 Prisma 타입 `Match`, `MatchRound`.
- Produces:
  - `interface MatchRound { index: number; gameModeId: number; mapId: number }` (도메인 — `id`/`matchId`는 저장 상세라 도메인에 두지 않는다)
  - `interface Match { id: string; queueId: number; targetRating: number; createdAt: Date; playerList: string[]; rounds: MatchRound[] }`
  - `MatchFactory.create(properties?: Partial<Match>): Match`
  - `class MatchRepository { save(match: Match): Promise<Match>; findById(id: string): Promise<Match | null> }` — rounds를 함께 읽고 쓴다
  - `CreateMatchDto { queueId: number; targetRating: number; playerList: string[]; rounds: MatchRoundDto[] }`, `MatchRoundDto { index: number; gameModeId: number; mapId: number }`
  - `MatchResponseDto { id: string; queueId: number; targetRating: number; playerList: string[]; rounds: MatchRoundDto[] }`

- [ ] **Step 1: 실패하는 테스트 작성**

`src/mappers/entities/__tests__/match.mapper.test.ts` 생성:

```typescript
import { MatchMapper } from '@mappers/entities/match.mapper';
import { MatchRoundMapper } from '@mappers/entities/matchRound.mapper';
import { Match } from '@interfaces/match.interface';

describe('MatchMapper', () => {
    const mapper = new MatchMapper();

    it('엔티티에는 라운드가 없다 — 라운드는 별도 테이블이라 리포지토리가 채운다', () => {
        const domain: Match = {
            id: 'match-1',
            queueId: 1,
            targetRating: 1000,
            createdAt: new Date('2026-07-30T00:00:00.000Z'),
            playerList: ['user-1', 'user-2'],
            rounds: [{ index: 0, gameModeId: 1, mapId: 1 }],
        };

        const entity = mapper.toEntity(domain);

        expect(entity).toEqual({
            id: 'match-1',
            queueId: 1,
            targetRating: 1000,
            createdAt: new Date('2026-07-30T00:00:00.000Z'),
            playerList: ['user-1', 'user-2'],
        });
        expect('rounds' in entity).toBe(false);
    });

    it('엔티티를 도메인으로 되돌리면 라운드는 빈 목록으로 시작한다', () => {
        const domain = mapper.toDomain({
            id: 'match-1',
            queueId: 2,
            targetRating: 1200,
            createdAt: new Date('2026-07-30T00:00:00.000Z'),
            playerList: [],
        });

        expect(domain.queueId).toBe(2);
        expect(domain.rounds).toEqual([]);
    });
});

describe('MatchRoundMapper', () => {
    const mapper = new MatchRoundMapper();

    it('도메인 라운드에 matchId를 붙여 엔티티로 만든다', () => {
        const entities = mapper.toEntities('match-1', [
            { index: 0, gameModeId: 3, mapId: 1 },
        ]);

        expect(entities).toHaveLength(1);
        expect(entities[0].matchId).toBe('match-1');
        expect(entities[0].index).toBe(0);
        expect(entities[0].gameModeId).toBe(3);
        expect(entities[0].mapId).toBe(1);
        expect(entities[0].id).toEqual(expect.any(String));
    });

    it('엔티티를 index 순으로 정렬해 도메인 라운드로 돌려준다', () => {
        const rounds = mapper.toDomains([
            { id: 'b', matchId: 'match-1', index: 1, gameModeId: 2, mapId: 1 },
            { id: 'a', matchId: 'match-1', index: 0, gameModeId: 1, mapId: 1 },
        ]);

        expect(rounds.map(r => r.index)).toEqual([0, 1]);
        expect(rounds[0].gameModeId).toBe(1);
    });
});
```

- [ ] **Step 2: 테스트가 실패하는지 확인**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/apps/matchmaking-server
pnpm test -- match.mapper
```
기대: FAIL — `Cannot find module '@mappers/entities/matchRound.mapper'`

- [ ] **Step 3: 도메인 인터페이스 작성**

`src/interfaces/matchRound.interface.ts` 생성:

```typescript
/** 매치 안의 한 게임. 지금은 매치마다 index 0 하나뿐이다. */
export interface MatchRound {
    index: number;
    gameModeId: number;
    mapId: number;
}
```

`src/interfaces/match.interface.ts` 전체 교체:

```typescript
import { MatchRound } from '@interfaces/matchRound.interface';

export interface Match {
    id: string;
    queueId: number;
    targetRating: number;
    createdAt: Date;
    playerList: string[];
    rounds: MatchRound[];
};
```

- [ ] **Step 4: 매퍼 작성**

`src/mappers/entities/matchRound.mapper.ts` 생성:

```typescript
import { MatchRound } from '@interfaces/matchRound.interface';
import { MatchRound as MatchRoundEntity } from '@lop/database';
import { v4 } from 'uuid';

export class MatchRoundMapper {
    public toEntities(matchId: string, rounds: Iterable<MatchRound>): MatchRoundEntity[] {
        return Array.from(rounds, (round) => ({
            id: v4(),
            matchId: matchId,
            index: round.index,
            gameModeId: round.gameModeId,
            mapId: round.mapId,
        }));
    }

    public toDomains(entities: Iterable<MatchRoundEntity>): MatchRound[] {
        return Array.from(entities, (entity) => ({
            index: entity.index,
            gameModeId: entity.gameModeId,
            mapId: entity.mapId,
        })).sort((x, y) => x.index - y.index);
    }
}
```

`src/mappers/entities/match.mapper.ts` 전체 교체(enum 변환이 사라지고, 라운드는 다루지 않는다):

```typescript
import { Match } from '@interfaces/match.interface';
import { Match as MatchEntity } from '@lop/database';
import { DomainEntityMapper } from '@mappers/domain.entity.mapper'

/**
 * 라운드는 별도 테이블이라 이 매퍼가 다루지 않는다 — MatchRepository가 함께 읽고 쓴다.
 * 그래서 toDomain은 rounds를 빈 목록으로 두고 돌려준다.
 */
export class MatchMapper implements DomainEntityMapper<Match, MatchEntity> {
    public toDomain(entity: MatchEntity): Match {
        return {
            id: entity.id,
            queueId: entity.queueId,
            targetRating: entity.targetRating,
            createdAt: entity.createdAt,
            playerList: entity.playerList,
            rounds: [],
        };
    }

    public toEntity(domain: Match): MatchEntity {
        return {
            id: domain.id,
            queueId: domain.queueId,
            targetRating: domain.targetRating,
            createdAt: new Date(domain.createdAt),
            playerList: domain.playerList,
        };
    }

    public toDomains(entities: Iterable<MatchEntity>): Iterable<Match> {
        return Array.from(entities, (entity) => this.toDomain(entity));
    }

    public toEntities(domains: Iterable<Match>): Iterable<MatchEntity> {
        return Array.from(domains, (domain) => this.toEntity(domain));
    }

    public getEntityFieldName<K extends keyof Match>(field: K): string {
        switch (field) {
            default: return field;
        }
    }

    public toEntityValue<K extends keyof Match>(field: K, value: Match[K]): any {
        switch (field) {
            default: return value;
        }
    }
}
```

- [ ] **Step 5: 테스트 통과 확인**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/apps/matchmaking-server
pnpm test -- match.mapper
```
기대: PASS (4 tests)

- [ ] **Step 6: DAO 작성**

`src/daos/matchRound.dao.postgres.ts` 생성:

```typescript
import { PrismaClient, MatchRound as MatchRoundEntity } from '@lop/database';
import { DaoPostgresBase } from "@daos/dao.postgres.base";
import { prismaClient } from '@loaders/postgres.loader';

export class MatchRoundDaoPostgres extends DaoPostgresBase<MatchRoundEntity, PrismaClient["matchRound"]> {
    constructor() {
        super(prismaClient, prismaClient.matchRound);
    }

    public async findAllByMatchId(matchId: string): Promise<MatchRoundEntity[]> {
        try {
            return await this.model.findMany({ where: { matchId } });
        } catch (error) {
            return Promise.reject(error);
        }
    }

    public async deleteAllByMatchId(matchId: string): Promise<void> {
        try {
            await this.model.deleteMany({ where: { matchId } });
        } catch (error) {
            return Promise.reject(error);
        }
    }
}
```

- [ ] **Step 7: 애그리게잇 리포지토리 작성**

`src/repositories/match.repository.ts` 전체 교체:

```typescript
import { Match } from '@interfaces/match.interface';
import { Match as MatchEntity } from '@lop/database';
import { CrudRepositoryBase } from '@repositories/crudRepositoryBase';
import { MatchDaoPostgres } from '@daos/match.dao.postgres';
import { MatchRoundDaoPostgres } from '@daos/matchRound.dao.postgres';
import { MatchMapper } from '@mappers/entities/match.mapper'
import { MatchRoundMapper } from '@mappers/entities/matchRound.mapper'

/**
 * 라운드는 별도 테이블이지만 매치의 일부다 — 바깥에서는 match.rounds로만 보이고
 * 테이블이 둘이라는 사실이 새어 나가지 않게 여기서 함께 읽고 쓴다.
 *
 * 두 테이블 쓰기를 트랜잭션으로 묶지 않는 것은 이 코드베이스의 현재 수준을 따른 것이다
 * (매치 생성 경로 전체가 트랜잭션 밖에 있다). 원자성은 Director 전환에서 함께 본다.
 */
export class MatchRepository extends CrudRepositoryBase<Match, MatchEntity> {
    private readonly roundDao = new MatchRoundDaoPostgres();
    private readonly roundMapper = new MatchRoundMapper();

    constructor() {
        super(new MatchDaoPostgres(), new MatchMapper());
    }

    public async save(match: Match): Promise<Match> {
        try {
            const saved = await super.save(match);

            await this.roundDao.deleteAllByMatchId(saved.id);
            await this.roundDao.saveAll(this.roundMapper.toEntities(saved.id, match.rounds));

            return { ...saved, rounds: match.rounds };
        } catch (error) {
            return Promise.reject(error);
        }
    }

    public async findById(id: string): Promise<Match | null> {
        try {
            const match = await super.findById(id);
            if (!match) {
                return null;
            }

            const roundEntities = await this.roundDao.findAllByMatchId(id);

            return { ...match, rounds: this.roundMapper.toDomains(roundEntities) };
        } catch (error) {
            return Promise.reject(error);
        }
    }
}
```

- [ ] **Step 8: 팩토리·mongoose 모델·DTO·컨트롤러 매퍼 갱신**

`src/factories/match.factory.ts` 전체 교체:

```typescript
import { Match } from '@interfaces/match.interface';
import { v4 } from 'uuid';

export class MatchFactory {
    public static create(properties?: Partial<Match>): Match {
        return { ...MatchFactory.createDefault(), ...properties };
    }

    private static createDefault(): Match {
        return {
            id: v4(),
            queueId: 1,
            targetRating: 1000,
            createdAt: new Date(),
            playerList: [],
            rounds: [],
        };
    }
}
```

`src/models/match.model.ts` 전체 교체:

```typescript
import { model, Schema, Document } from 'mongoose';
import { Match } from '@interfaces/match.interface';

const matchRoundSchema: Schema = new Schema({
    index: Number,
    gameModeId: Number,
    mapId: Number,
}, { _id: false });

const matchSchema: Schema = new Schema({
    id: {
        type: String,
        required: true,
        unique: true,
    },
    queueId: Number,
    targetRating: Number,
    createdAt: {
        type: Date,
        default: Date.now,
    },
    playerList: [String],
    rounds: [matchRoundSchema],
});

const matchModel = model<Match & Document>('Match', matchSchema);

export default matchModel;
```

`src/dtos/match.dto.ts` 전체 교체:

```typescript
import { IsNumber, IsString, IsArray, ValidateNested } from 'class-validator';
import { Type } from 'class-transformer';
import { ResponseBase } from '@interfaces/responseBase.interface';

export class MatchRoundDto {
    @IsNumber()
    public index: number;

    @IsNumber()
    public gameModeId: number;

    @IsNumber()
    public mapId: number;
}

export class CreateMatchDto {
    @IsNumber()
    public queueId: number;

    @IsNumber()
    public targetRating: number;

    @IsArray()
    @IsString({ each: true })
    public playerList: string[];

    @IsArray()
    @ValidateNested({ each: true })
    @Type(() => MatchRoundDto)
    public rounds: MatchRoundDto[];
}

export class MatchResponseDto {
    public id: string;
    public queueId: number;
    public targetRating: number;
    public playerList: string[];
    public rounds: MatchRoundDto[];
}

export class GetMatchResponseDto implements ResponseBase {
    public code: number;
    public match?: MatchResponseDto;
}
```

> `class-transformer`가 이미 의존성에 있는지 확인한다: `grep class-transformer apps/matchmaking-server/package.json`. 없으면 `@ValidateNested`/`@Type` 두 줄과 그 import를 빼고 `@IsArray()`만 남긴다 — 이 DTO는 서버 내부에서만 만들어지므로 중첩 검증이 필수는 아니다.

`src/mappers/controllers/match.mapper.ts` 전체 교체:

```typescript
import { Match } from '@interfaces/match.interface';
import { MatchFactory } from '@factories/match.factory';
import { CreateMatchDto, MatchResponseDto } from '@dtos/match.dto';

export class MatchMapper {
    static CreateMatchDto = class {
        public static toEntity(createMatchDto: CreateMatchDto): Match {
            return MatchFactory.create({
                queueId: createMatchDto.queueId,
                targetRating: createMatchDto.targetRating,
                playerList: createMatchDto.playerList,
                rounds: createMatchDto.rounds,
            });
        }
    };

    public static toMatchResponseDto(match: Match): MatchResponseDto {
        return {
            id: match.id,
            queueId: match.queueId,
            targetRating: match.targetRating,
            playerList: match.playerList,
            rounds: match.rounds,
        };
    }
}
```

- [ ] **Step 9: 테스트 재실행**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/apps/matchmaking-server
pnpm test
```
기대: 이전 스위트 + 새 매퍼 테스트가 전부 PASS. (`masterdata.loader.test.ts`는 아직 통과한다 — Task 3에서 손댄다.)

- [ ] **Step 10: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
git add apps/matchmaking-server/src
git commit -m "$(cat <<'EOF'
feat(matchmaking): Match가 라운드 목록을 갖도록

라운드는 별도 테이블이지만 MatchRepository(애그리게잇 루트)가 함께 읽고
써서 바깥에는 match.rounds로만 보인다. 공용 DAO/매퍼 구조는 그대로다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: 매칭 서버 — 티켓·대기방·서비스 어휘 전환

**Files:**
- Delete: `lop-backend/apps/matchmaking-server/src/interfaces/enums.ts`
- Modify: `.../src/interfaces/matchmakingTicket.interface.ts`, `.../src/interfaces/waitingRoom.interface.ts`
- Modify: `.../src/dtos/matchmaking.dto.ts`, `.../src/dtos/matchmakingTicket.dto.ts`, `.../src/dtos/waitingRoom.dto.ts`, `.../src/dtos/user-stats.dto.ts`
- Modify: `.../src/factories/matchmakingTicket.factory.ts`, `.../src/factories/waitingRoom.factory.ts`
- Modify: `.../src/models/matchmakingTicket.model.ts`, `.../src/models/waitingRoom.model.ts`
- Modify: `.../src/mappers/entities/matchmakingTicket.mapper.ts`, `.../src/mappers/entities/waitingRoom.mapper.ts`
- Modify: `.../src/mappers/controllers/matchmakingTicket.mapper.ts`, `.../src/mappers/controllers/waitingRoom.mapper.ts`
- Modify: `.../src/loaders/masterdata.loader.ts`
- Modify: `.../src/services/matchmaking.service.ts`, `matchmakingTicket.service.ts`, `waitingRoom.service.ts`, `user-stats.service.ts`, `httpServices/lobbyServer.service.ts`
- Modify: `.../src/loaders/__tests__/masterdata.loader.test.ts`

**Interfaces:**
- Consumes: Task 2의 `CreateMatchDto { queueId, targetRating, playerList, rounds }`, `MatchRound { index, gameModeId, mapId }`.
- Produces:
  - `interface MatchmakingTicket { id: string; creator: string; queueId: number; gameModeId: number; mapId: number; rating: number; createdAt: Date }`
  - `interface WaitingRoom { id: string; queueId: number; gameModeId: number; mapId: number; targetRating: number; createdAt: Date; matchmakingTicketList: string[]; maxWaitingTime: number; minPlayerCount: number; maxPlayerCount: number; status: WaitingRoomStatus }`
  - `RequestMatchmakingDto { userId: string; queueId: number; gameModeId: number; mapId: number }` — **클라가 보내는 요청 본문 형태**
  - `MatchmakingTicketService.issueMatchmakingTicket(userId: string, queueId: number, gameModeId: number, mapId: number, rating: number): Promise<MatchmakingTicket>`
  - `UserStatsService.findUserStatsById(userId: string, queueId: number): Promise<GetUserStatsResponseDto>`
  - `masterdata.loader`는 `getTables()`와 `load(folder?)`만 export한다(`findGameModeByCode` 없음)

- [ ] **Step 1: 로더 테스트를 새 모양으로 고쳐서 실패시키기**

`src/loaders/__tests__/masterdata.loader.test.ts`에서 `findGameModeByCode` import를 지우고, 그것을 쓰는 두 테스트를 아래로 교체한다.

import 줄:
```typescript
import { load, getTables } from '@loaders/masterdata.loader';
```

`'기존 XML과 같은 정원 값을 준다 (동작 무변화)'` 테스트 교체:
```typescript
    it('게임을 정수 id로 찾고 정원을 준다', () => {
        const flapWang = getTables().TbGameMode.get(1);
        expect(flapWang).toBeDefined();
        expect(flapWang!.code).toBe('FlapWang');
        expect(flapWang!.minPlayers).toBe(2);
        expect(flapWang!.maxPlayers).toBe(8);
    });
```

`'없는 code는 undefined를 준다 (throw 아님)'` 테스트 교체:
```typescript
    it('없는 id는 undefined를 준다 (throw 아님)', () => {
        expect(getTables().TbGameMode.get(999)).toBeUndefined();
    });
```

- [ ] **Step 2: 새 테스트가 헬퍼 없이도 통과하는지 확인**

이건 삭제라서 "먼저 빨갛게" 만들 대상이 없다. 대신 **헬퍼를 지우기 전에** 그것 없이도 같은 값이 나온다는 것을 못박는다 — 그래야 다음 단계의 삭제가 안전하다는 근거가 생긴다.

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/apps/matchmaking-server
pnpm test -- masterdata.loader
```
기대: PASS. 여기서 실패하면 `TbGameMode.get(1)`이 기대한 값을 안 준다는 뜻이므로, 헬퍼를 지우지 말고 원인을 먼저 찾는다.

- [ ] **Step 3: 로더에서 헬퍼 삭제**

`src/loaders/masterdata.loader.ts`에서 아래 블록을 삭제하고, `import { Tables, GameMode } from '@src/masterdata/schema';`를 `import { Tables } from '@src/masterdata/schema';`로 바꾼다.

```typescript
/**
 * code로 게임을 찾는다.
 * 슬라이스 2에서 subGameId(string)가 gameModeId(int)로 바뀌면 이 헬퍼는 사라진다.
 */
export function findGameModeByCode(code: string): GameMode | undefined {
    return getTables().TbGameMode.getDataList().find(x => x.code === code);
}
```

- [ ] **Step 4: 테스트 통과 확인**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/apps/matchmaking-server
pnpm test -- masterdata.loader
```
기대: PASS (6 tests)

- [ ] **Step 5: `enums.ts` 삭제**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
git rm apps/matchmaking-server/src/interfaces/enums.ts
```

- [ ] **Step 6: 인터페이스 2개 교체**

`src/interfaces/matchmakingTicket.interface.ts` 전체 교체:
```typescript
export interface MatchmakingTicket {
    id: string;
    creator: string;
    queueId: number;
    gameModeId: number;
    mapId: number;
    rating: number;
    createdAt: Date;
}
```

`src/interfaces/waitingRoom.interface.ts`에서 `import { GameMode } from '@interfaces/enums';` 줄을 지우고 세 필드를 바꾼다:
```typescript
    queueId: number;
    gameModeId: number;
    mapId: number;
```

- [ ] **Step 7: DTO 3개 교체**

`src/dtos/matchmaking.dto.ts`의 `RequestMatchmakingDto`를 교체하고 `GameMode` import를 지운다:
```typescript
export class RequestMatchmakingDto {
    @IsString()
    public userId: string;

    @IsNumber()
    public queueId: number;

    @IsNumber()
    public gameModeId: number;

    @IsNumber()
    public mapId: number;
}
```

`src/dtos/matchmakingTicket.dto.ts`에서 `GameMode` import를 지우고 두 클래스의 세 필드를 바꾼다:
```typescript
    @IsNumber()
    public queueId: number;

    @IsNumber()
    public gameModeId: number;

    @IsNumber()
    public mapId: number;
```
(`MatchmakingTicketResponseDto` 쪽은 데코레이터 없이 `public queueId: number; public gameModeId: number; public mapId: number;`)

`src/dtos/waitingRoom.dto.ts`도 같은 방식으로 세 필드를 바꾸고, 생성자 시그니처를 바꾼다:
```typescript
    public constructor(queueId: number, gameModeId: number, mapId: number, targetRating: number, maxWaitingTime: number, minPlayerCount: number, maxPlayerCount: number) {
        this.queueId = queueId;
        this.gameModeId = gameModeId;
        this.mapId = mapId;
```
(생성자 나머지 줄은 그대로 둔다.)

`src/dtos/user-stats.dto.ts`에 `gameMode` 필드가 있으면 `queueId: number`로 바꾸고 `GameMode` import를 지운다.

- [ ] **Step 8: 팩토리·mongoose 모델·엔티티 매퍼·컨트롤러 매퍼 교체**

두 팩토리(`matchmakingTicket.factory.ts`, `waitingRoom.factory.ts`)에서 `GameMode` import를 지우고 기본값을 바꾼다:
```typescript
            queueId: 1,
            gameModeId: 1,
            mapId: 1,
```
(`subGameId: ''`, `mapId: ''` 줄은 사라진다.)

두 mongoose 모델에서 `GameMode` import를 지우고 필드를 바꾼다:
```typescript
    queueId: Number,
    gameModeId: Number,
    mapId: Number,
```
(`matchType: { type: Number, enum: GameMode }` 블록을 위 `queueId: Number` 한 줄로 교체.)

두 엔티티 매퍼(`entities/matchmakingTicket.mapper.ts`, `entities/waitingRoom.mapper.ts`)에서 `GameMode` import와 enum 변환을 지우고 패스스루로 바꾼다:
```typescript
            queueId: entity.queueId,
            gameModeId: entity.gameModeId,
            mapId: entity.mapId,
```
(`toEntity` 쪽도 `queueId: domain.queueId,` 식으로 대칭. `GameMode[...] as Entity.GameMode` 캐스팅은 전부 사라진다. `import * as Entity from '@lop/database';`가 다른 데 안 쓰이면 함께 지운다.)

두 컨트롤러 매퍼도 같은 세 필드로 바꾼다.

- [ ] **Step 9: 서비스 교체 — 여기가 실제 로직이 바뀌는 곳**

`src/services/matchmakingTicket.service.ts`의 `issueMatchmakingTicket` 시그니처와 본문:
```typescript
    public async issueMatchmakingTicket(userId: string, queueId: number, gameModeId: number, mapId: number, rating: number): Promise<MatchmakingTicket> {
        try {
            const matchmakingTicket = MatchmakingTicketFactory.create({
                creator: userId,
                queueId: queueId,
                gameModeId: gameModeId,
                mapId: mapId,
                rating: rating,
            });
            return await this.matchmakingTicketRepository.save(matchmakingTicket);
        } catch (error) {
            return Promise.reject(error);
        }
    }
```
파일 상단의 `import { GameMode } from '@interfaces/enums';`를 지운다.

`src/services/user-stats.service.ts`:
```typescript
import LobbyServerService from '@services/httpServices/lobbyServer.service';
import { GetUserStatsResponseDto } from '@dtos/user-stats.dto';

class UserStatsService {

    private lobbyServerService = new LobbyServerService();

    public async findUserStatsById(userId: string, queueId: number): Promise<GetUserStatsResponseDto> {
        try {
            return await this.lobbyServerService.findUserStatsById(userId, queueId);
        } catch (error) {
            return Promise.reject(error);
        }
    }
}

export default UserStatsService;
```

`src/services/httpServices/lobbyServer.service.ts`에서 전적 조회 URL의 쿼리 파라미터를 `gameMode` → `queueId`로 바꾸고 인자 이름·타입도 `queueId: number`로 바꾼다. (`grep -n "gameMode" src/services/httpServices/lobbyServer.service.ts`로 위치를 찾는다.)

`src/services/matchmaking.service.ts` — `GameMode` import를 지우고, 큐 결정 한 줄과 티켓 발급 호출을 바꾼다:
```typescript
            const queueId = requestMatchmakingDto.queueId;
            const getUserStatsResponse = await this.userStatsService.findUserStatsById(user.id, queueId);
```
```typescript
            const matchmakingTicket = await this.matchmakingTicketService.issueMatchmakingTicket(
                requestMatchmakingDto.userId,
                requestMatchmakingDto.queueId,
                requestMatchmakingDto.gameModeId,
                requestMatchmakingDto.mapId,
                targetRating
            );
```

> 옛 코드는 `requestMatchmakingDto.matchType === GameMode.Ranked ? Ranked : Normal`로 **모르는 값을 Normal로 뭉갰다.** 이제 큐가 데이터라 그 방어는 "존재하는 큐인가"로 바뀌어야 하는데, 검증 자리는 슬라이스 4(Director)다. 여기서는 받은 `queueId`를 그대로 쓰고, 없는 큐면 전적 조회가 `USER_STATS_NOT_EXIST`로 실패한다.

`src/services/waitingRoom.service.ts` — `findGameModeByCode` import를 지우고 `getTables`를 쓴다. 방 생성 블록:
```typescript
            if (waitingRoom === undefined) {
                const gameMode = getTables().TbGameMode.get(matchmakingTicket.gameModeId);
                if (gameMode === undefined) {
                    throw new Error(`Unknown gameModeId: ${matchmakingTicket.gameModeId}`);
                }
                waitingRoom = await this.createWaitingRoom(new CreateWaitingRoomDto(
                    matchmakingTicket.queueId,
                    matchmakingTicket.gameModeId,
                    matchmakingTicket.mapId,
                    matchmakingTicket.rating,
                    5,  //  ?
                    gameMode.minPlayers,
                    gameMode.maxPlayers
                ));
            }
```
그리고 매치 생성 블록(`updateWaitingRoom` 안)을 라운드 형태로:
```typescript
                const createMatchDto: CreateMatchDto = {
                    queueId: waitingRoom.queueId,
                    targetRating: waitingRoom.targetRating,
                    playerList: waitingPlayerIds,
                    rounds: [{
                        index: 0,
                        gameModeId: waitingRoom.gameModeId,
                        mapId: waitingRoom.mapId,
                    }],
                };
```
import 줄도 바꾼다: `import { findGameModeByCode } from '@loaders/masterdata.loader';` → `import { getTables } from '@loaders/masterdata.loader';`

- [ ] **Step 10: 남은 `GameMode` 참조가 없는지 확인**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/apps/matchmaking-server
grep -rn "GameMode\|matchType\|subGameId" src --include=*.ts | grep -v "gameModeId\|GameModeId\|TbGameMode\|masterdata/schema"
```
기대 출력: 없음(빈 결과). 무언가 나오면 그 파일을 마저 고친다.

- [ ] **Step 11: 빌드 + 테스트**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
pnpm --filter matchmaking-server build
pnpm --filter matchmaking-server test
```
기대: 빌드 에러 0, 테스트 전부 PASS.

> `package.json`의 `name`이 `matchmaking-server`가 아닐 수 있다. 그 경우 `cat apps/matchmaking-server/package.json | head -3`으로 실제 이름을 확인해 `--filter`에 넣는다.

- [ ] **Step 12: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
git add apps/matchmaking-server
git commit -m "$(cat <<'EOF'
refactor(matchmaking): 티켓·대기방 어휘를 queueId/gameModeId 정수로

enum GameMode 삭제. 게임 조회가 code 문자열에서 TbGameMode의 정수 id로
바뀌면서 슬라이스 1의 임시 헬퍼 findGameModeByCode도 사라졌다.
매치 생성은 rounds 원소 1개를 만든다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: 로비 서버 어휘 전환

**Files:**
- Delete: `lop-backend/apps/lobby-server/src/interfaces/enums.ts`
- Modify: `.../src/interfaces/user-stats.interface.ts`, `matchmakingTicket.interface.ts`, `waitingRoom.interface.ts`
- Modify: `.../src/dtos/user-stats.dto.ts`, `.../src/factories/user-stats.factory.ts`
- Modify: `.../src/mappers/entities/user-stats.mapper.ts`, `.../src/mappers/controllers/user-stats.mapper.ts`
- Modify: `.../src/controllers/user-stats.controller.ts`, `.../src/services/user-stats.service.ts`, `.../src/services/user.service.ts`

**Interfaces:**
- Consumes: Task 1의 Prisma `UserStats { queueId: number }`.
- Produces: 전적 조회 HTTP 계약이 `GET /user/:userId/stats?queueId=<int>`로 바뀐다. 이 쿼리 파라미터 이름은 Task 3(매칭 서버 `lobbyServer.service.ts`)과 Task 7(클라 `WebAPI.GetUserStats`)이 맞춰야 한다.

- [ ] **Step 1: `enums.ts` 삭제**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
git rm apps/lobby-server/src/interfaces/enums.ts
```

- [ ] **Step 2: 인터페이스 3개 교체**

`src/interfaces/user-stats.interface.ts`에서 `GameMode` import를 지우고 `gameMode: GameMode;` → `queueId: number;`.

`src/interfaces/matchmakingTicket.interface.ts`, `src/interfaces/waitingRoom.interface.ts`에서 `GameMode` import를 지우고:
```typescript
    queueId: number;
    gameModeId: number;
    mapId: number;
```
(옛 `matchType`/`subGameId: string`/`mapId: string` 세 줄을 위 셋으로 교체.)

- [ ] **Step 3: DTO·팩토리 교체**

`src/dtos/user-stats.dto.ts`: `GameMode` import 삭제, `public gameMode: GameMode;` → `public queueId: number;`.

`src/factories/user-stats.factory.ts`: `GameMode` import 삭제, `gameMode: GameMode.Normal,` → `queueId: 1,`.

- [ ] **Step 4: 엔티티 매퍼에서 enum 변환 걷어내기**

`src/mappers/entities/user-stats.mapper.ts` 전체 교체(변환 헬퍼 두 개가 사라져 패스스루가 된다):

```typescript
import { UserStats } from '@interfaces/user-stats.interface';
import { UserStats as UserStatsEntity } from '@lop/database';
import { DomainEntityMapper } from '@mappers/domain.entity.mapper'

export class UserStatsMapper implements DomainEntityMapper<UserStats, UserStatsEntity> {
    public toDomain(entity: UserStatsEntity): UserStats {
        return {
            id: entity.id,
            userId: entity.userId,
            queueId: entity.queueId,
            gamesPlayed: entity.gamesPlayed,
            wins: entity.wins,
            losses: entity.losses,
            draws: entity.draws,
            eloRating: entity.eloRating,
            mmr: entity.mmr,
            tier: entity.tier,
        };
    }

    public toEntity(domain: UserStats): UserStatsEntity {
        return {
            id: domain.id,
            userId: domain.userId,
            queueId: domain.queueId,
            gamesPlayed: domain.gamesPlayed,
            wins: domain.wins,
            losses: domain.losses,
            draws: domain.draws,
            eloRating: domain.eloRating,
            mmr: domain.mmr,
            tier: domain.tier,
        } as UserStatsEntity;
    }

    public toDomains(entities: Iterable<UserStatsEntity>): Iterable<UserStats> {
        return Array.from(entities, (entity) => this.toDomain(entity));
    }

    public toEntities(domains: Iterable<UserStats>): Iterable<UserStatsEntity> {
        return Array.from(domains, (domain) => this.toEntity(domain));
    }

    public getEntityFieldName<K extends keyof UserStats>(field: K): string {
        switch (field) {
            default: return field;
        }
    }

    public toEntityValue<K extends keyof UserStats>(field: K, value: UserStats[K]): any {
        switch (field) {
            default: return value;
        }
    }
}
```

`src/mappers/controllers/user-stats.mapper.ts`에서 `gameMode: userStats.gameMode,` → `queueId: userStats.queueId,`.

- [ ] **Step 5: 컨트롤러·서비스 교체**

`src/controllers/user-stats.controller.ts` 전체 교체:
```typescript
import { NextFunction, Request, Response } from 'express';
import UserStatsService from '@services/user-stats.service';

class UserStatsController {
    private userStatsService = new UserStatsService();

    public getUserStatsById = async (req: Request, res: Response, next: NextFunction) => {
        try {
            const userId = req.params.userId;
            const queueId = Number(req.query.queueId as unknown);
            const response = await this.userStatsService.findUserStatsById(userId, queueId);
            res.status(200).json(response);
        } catch (error) {
            next(error);
        }
    };
}

export default UserStatsController;
```

`src/services/user-stats.service.ts`: `GameMode` import 삭제, 시그니처를 `findUserStatsById(userId: string, queueId: number)`로, 조회 조건을 `['queueId', queueId],`로.

`src/services/user.service.ts`의 전적 시딩 블록 교체:
```typescript
            //  큐마다 전적 행을 하나씩. 큐 목록을 TbQueue에서 읽는 것은
            //  로비 서버가 마스터데이터를 싣게 되는 로비 선택 UI 작업에서 한다.
            const casualUserStats = UserStatsFactory.create({
                userId: user.id,
                queueId: 1,
            });

            await this.userStatsRepository.save(casualUserStats);

            const rankedUserStats = UserStatsFactory.create({
                userId: user.id,
                queueId: 2,
            });

            await this.userStatsRepository.save(rankedUserStats);
```
파일 상단의 `import { GameMode } from '@interfaces/enums';`도 지운다.

- [ ] **Step 6: 남은 참조 확인**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/apps/lobby-server
grep -rn "GameMode\|matchType\|subGameId\|gameMode" src --include=*.ts
```
기대 출력: 없음.

- [ ] **Step 7: 빌드**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
pnpm --filter lobby-server build
```
기대: 에러 0.

- [ ] **Step 8: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
git add apps/lobby-server
git commit -m "$(cat <<'EOF'
refactor(lobby): 전적 축을 gameMode에서 queueId로

전적 조회 쿼리도 ?gameMode= 에서 ?queueId= 로 바뀐다.
enum 변환 헬퍼가 사라져 매퍼가 패스스루가 됐다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: 룸 서버 어휘 전환

**Files:**
- Delete: `lop-backend/apps/room-server/src/interfaces/enums.ts` (다른 곳에서 안 쓰일 때만 — Step 1에서 확인)
- Modify: `lop-backend/apps/room-server/src/interfaces/match.interface.ts`
- Modify: `lop-backend/apps/room-server/src/dtos/match.dto.ts`

**Interfaces:**
- Consumes: Task 2의 `MatchResponseDto { id, queueId, targetRating, playerList, rounds }`.
- Produces: 룸 서버가 매칭 서버에서 받아 게임 서버로 중계하는 매치 형태. 게임 서버(Task 6)와 클라(Task 7)의 DTO가 이 모양을 따라야 한다.

- [ ] **Step 1: `enums.ts`가 `GameMode`만 담고 있는지 확인**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/apps/room-server
cat src/interfaces/enums.ts
grep -rn "from '@interfaces/enums'" src --include=*.ts
```
`GameMode`만 있고 참조가 아래 두 파일뿐이면 파일째 삭제한다. 다른 enum이 함께 있으면 `GameMode` 블록만 지운다.

- [ ] **Step 2: 인터페이스·DTO 교체**

`src/interfaces/match.interface.ts` 전체 교체:
```typescript
export interface MatchRound {
    index: number;
    gameModeId: number;
    mapId: number;
}

export interface Match {
    id: string;
    queueId: number;
    targetRating: number;
    createdAt: Date;
    playerList: string[];
    rounds: MatchRound[];
};
```

`src/dtos/match.dto.ts` 전체 교체:
```typescript
import { ResponseBase } from '@interfaces/responseBase.interface';

export class MatchRoundDto {
    public index: number;
    public gameModeId: number;
    public mapId: number;
}

export class MatchResponseDto {
    public id: string;
    public queueId: number;
    public targetRating: number;
    public playerList: string[];
    public rounds: MatchRoundDto[];
}

export class GetMatchResponseDto implements ResponseBase {
    public code: number;
    public match?: MatchResponseDto;
}
```

- [ ] **Step 3: 남은 참조 확인 + 빌드**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/apps/room-server
grep -rn "GameMode\|matchType\|subGameId" src --include=*.ts | grep -v gameModeId
cd /c/Users/re5na/workspace/LOP/lop-backend
pnpm --filter room-server build
```
기대: grep 결과 없음, 빌드 에러 0.

- [ ] **Step 4: 전체 모노레포 빌드·테스트로 마감**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
pnpm build
pnpm test
```
기대: 4개 패키지 빌드 성공, 테스트 전부 PASS.

- [ ] **Step 5: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
git add apps/room-server
git commit -m "$(cat <<'EOF'
refactor(room): 매치 어휘를 queueId + rounds로

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: 게임 서버 — 어휘 전환 + 맵을 `rounds[0]`에서 읽기

**Files:**
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Domain/Enums.cs`
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Domain/MatchRound.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Domain/Match.cs`
- Create: `LeagueOfPhysical-Server/Assets/Scripts/WebAPI/Dto/MatchRoundDto.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/WebAPI/Dto/MatchDto.cs`
- Delete: `LeagueOfPhysical-Server/Assets/Scripts/WebAPI/Dto/MatchSetting.cs`, `.../Dto/Request/NotifyStartServerRequest.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/WebAPI/WebAPI.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Entrance/EntranceComponent/ConfigureRoomComponent.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/LOPRunner.cs`

**Interfaces:**
- Consumes: Task 5의 매치 JSON 형태(`queueId`, `rounds[]`), 서버 MasterData 패키지의 `LOP.MasterData.LOPMasterData.Tables.TbMap.get(int): GameMap`(필드 `Id`, `GameModeId`, `Code`, `ScenePath`).
- Produces: 게임 서버 쪽 소비자 없음(마지막 소비처).

- [ ] **Step 1: 브랜치 생성**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git checkout -b feature/matchmaking-slice2-vocabulary-rounds
```

- [ ] **Step 2: `TbMap` 생성 클래스의 실제 프로퍼티 이름 확인**

```bash
grep -n "public.*Id\|public.*ScenePath\|public.*Code" /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server/Runtime.Generated/Scripts/MasterData/GameMap.cs
grep -n "TbMap" /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server/Runtime.Generated/Scripts/MasterData/Tables.cs
```
Luban의 C# 생성 규칙상 `ScenePath`/`GameModeId`지만, 실제 이름을 눈으로 확인하고 아래 코드에 반영한다.

- [ ] **Step 3: 도메인·DTO 교체**

`Assets/Scripts/Domain/Enums.cs`에서 `GameMode` enum 블록을 삭제한다. 파일에 다른 enum이 없으면 파일과 `.meta`를 함께 지운다.

`Assets/Scripts/Domain/MatchRound.cs` 생성:
```csharp
using System;

namespace LOP
{
    [Serializable]
    public class MatchRound
    {
        public int index;
        public int gameModeId;
        public int mapId;
    }
}
```

`Assets/Scripts/Domain/Match.cs` 전체 교체:
```csharp
using System;

namespace LOP
{
    public class Match
    {
        public string id;
        public int queueId;
        public int targetRating;
        public string[] playerList;
        public MatchRound[] rounds;
    }
}
```

`Assets/Scripts/WebAPI/Dto/MatchRoundDto.cs` 생성:
```csharp
using System;

namespace LOP
{
    [Serializable]
    public class MatchRoundDto
    {
        public int index;
        public int gameModeId;
        public int mapId;
    }
}
```

`Assets/Scripts/WebAPI/Dto/MatchDto.cs` 전체 교체:
```csharp
using System;

namespace LOP
{
    [Serializable]
    public class MatchDto
    {
        public string id;
        public int queueId;
        public int targetRating;
        public string[] playerList;
        public MatchRoundDto[] rounds;
    }
}
```

- [ ] **Step 4: 죽은 코드 삭제**

`MatchSetting`은 삭제되는 `GameMode`를 참조하는데 `WebAPI.NotifyStartServer`를 부르는 곳이 0곳이고 룸 서버에 대응 핸들러도 없다.

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git rm Assets/Scripts/WebAPI/Dto/MatchSetting.cs Assets/Scripts/WebAPI/Dto/MatchSetting.cs.meta
git rm Assets/Scripts/WebAPI/Dto/Request/NotifyStartServerRequest.cs Assets/Scripts/WebAPI/Dto/Request/NotifyStartServerRequest.cs.meta
```

`Assets/Scripts/WebAPI/WebAPI.cs`에서 `NotifyStartServer` 메서드 블록을 삭제한다:
```csharp
        public static WebRequest<string> NotifyStartServer(NotifyStartServerRequest request)
        {
            return new WebRequestBuilder<string>()
                .SetUri($"{EnvironmentSettings.active.roomBaseURL}/room")
                .SetMethod(HttpMethod.PUT)
                .SetRequestBody(request)
                .SetWebRequestInterceptor(LOPWebRequestInterceptor.Default)
                .Build();
        }
```

- [ ] **Step 5: 에디터 픽스처 갱신**

`Assets/Scripts/Entrance/EntranceComponent/ConfigureRoomComponent.cs`의 `Match` 생성 블록에서 세 줄을 교체한다:
```csharp
                    matchType = GameMode.Normal,
                    subGameId = "FlapWang",
                    mapId = "FlapWangMap",
```
→
```csharp
                    queueId = 1,
                    rounds = new MatchRound[]
                    {
                        new MatchRound { index = 0, gameModeId = 1, mapId = 1 },
                    },
```
(`targetRating`과 `playerList`는 그대로 둔다.)

- [ ] **Step 6: 맵 로딩을 `rounds[0]`에서**

`Assets/Scripts/Game/LOPRunner.cs`에서 상수를 지운다:
```csharp
        private const string MapId = "Assets/Art/Scenes/FlapWangMap.unity";
```

`[Inject]` 블록에 두 의존을 추가한다(이미 있으면 중복 추가하지 않는다):
```csharp
        [Inject] private IRoomDataStore roomDataStore;
        [Inject] private LOP.MasterData.LOPMasterData masterData;
```

`InitializeAsync`의 맵 로딩 줄을 교체한다:
```csharp
            var mapLoadTask = mapLoader.LoadAsync(MapId);
```
→
```csharp
            var mapLoadTask = mapLoader.LoadAsync(ResolveScenePath());
```

그리고 클래스에 메서드를 추가한다:
```csharp
        /// <summary>이 판에서 로드할 씬. 매치의 첫 라운드가 가리키는 맵에서 온다.</summary>
        private string ResolveScenePath()
        {
            var rounds = roomDataStore.match?.rounds;
            if (rounds == null || rounds.Length == 0)
            {
                throw new Exception("매치에 라운드가 없어 맵을 정할 수 없습니다.");
            }

            var mapId = rounds[0].mapId;
            var map = masterData.Tables.TbMap.get(mapId);
            if (map == null)
            {
                throw new Exception($"TbMap에 없는 mapId입니다. mapId: {mapId}");
            }

            return map.ScenePath;
        }
```

> `masterData.Tables` 접근자와 `TbMap.get`의 정확한 표기는 Step 2에서 확인한 것을 쓴다. 다른 곳에서 마스터데이터를 어떻게 읽는지는 `grep -rn "masterData.Tables" Assets/Scripts --include=*.cs`로 짝을 맞춘다.

- [ ] **Step 7: 남은 참조 확인**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
grep -rn "GameMode\|matchType\|subGameId\|MatchSetting\|NotifyStartServer" Assets/Scripts --include=*.cs | grep -v gameModeId
```
기대 출력: 없음.

- [ ] **Step 8: Unity 컴파일 확인**

서버 Unity 에디터에서 컴파일 결과를 읽는다. 인스턴스 id는 `mcpforunity://instances`에서 이름이 `LeagueOfPhysical-Server`인 것을 쓴다.

```
refresh_unity(unity_instance="LeagueOfPhysical-Server@<hash>")
read_console(unity_instance="LeagueOfPhysical-Server@<hash>", types=["error"])
```
기대: 컴파일 에러 0건.

에디터가 붙어 있지 않으면 그 자리에서 **멈추고 사용자에게 알린다** — 게임 서버는 워크트리를 쓰지 않으므로 여기서 검증할 수 있어야 한다.

- [ ] **Step 9: 에디터 플레이로 맵이 뜨는지 확인**

Unity 에디터에서 플레이 모드로 들어가 `FlapWangMap` 씬이 로드되는지 본다(에디터 경로는 Step 5의 픽스처를 쓰므로 `mapId = 1` → `TbMap` → `Assets/Art/Scenes/FlapWangMap.unity`). 콘솔에 "매치에 라운드가 없어" 또는 "TbMap에 없는 mapId" 예외가 없어야 한다.

- [ ] **Step 10: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git add -A Assets/Scripts
git commit -m "$(cat <<'EOF'
feat(server): 맵을 하드코딩이 아니라 매치의 첫 라운드에서 읽는다

rounds[0].mapId로 TbMap을 찾아 scenePath를 로드한다. 어휘도 queueId/
gameModeId 정수로. 부르는 데가 없던 MatchSetting/NotifyStartServer는
삭제된 GameMode enum만 참조하던 죽은 코드라 함께 지웠다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 7: 클라 — 어휘 전환

**Files:**
- Modify: `Assets/Scripts/Domain/Enums.cs`
- Create: `Assets/Scripts/Domain/MatchRound.cs`
- Modify: `Assets/Scripts/Domain/Match.cs`, `Assets/Scripts/Domain/UserStats.cs`
- Create: `Assets/Scripts/WebAPI/Dto/MatchRoundDto.cs`
- Modify: `Assets/Scripts/WebAPI/Dto/MatchDto.cs`, `.../Dto/UserStatsDto.cs`, `.../Dto/Request/MatchmakingRequest.cs`
- Modify: `Assets/Scripts/WebAPI/WebAPI.cs`
- Modify: `Assets/Scripts/Stores/IMatchmakingDataStore.cs`, `MatchmakingDataStore.cs`, `IUserDataStore.cs`, `UserDataStore.cs`
- Modify: `Assets/Scripts/Matchmaking/MatchStateMachine/States/RequestMatchmaking.cs`
- Modify: `Assets/Scripts/UI/Matchmaking/MatchmakingViewModel.cs`
- Modify: `Assets/Scripts/Entrance/EntranceComponent/CheckUserComponent.cs`

**Interfaces:**
- Consumes: Task 3의 `RequestMatchmakingDto { userId, queueId, gameModeId, mapId }`, Task 4의 `GET /user/:userId/stats?queueId=<int>`, Task 5의 매치 JSON.
- Produces: 소비자 없음(마지막 태스크).

> **작업 위치:** 이 워크트리(`.claude/worktrees/docs+matchmaking-standardization`)에서 편집한다. Unity 에디터는 main 체크아웃을 보므로 **여기서는 컴파일 검증이 안 된다** — Task 8에서 머지 후 확인한다.

- [ ] **Step 1: 도메인·DTO 교체**

`Assets/Scripts/Domain/Enums.cs`에서 `GameMode` enum을 삭제한다. 파일에 다른 enum이 없으면 파일과 `.meta`를 함께 지운다.

`Assets/Scripts/Domain/MatchRound.cs` 생성:
```csharp
using System;

namespace LOP
{
    [Serializable]
    public class MatchRound
    {
        public int index;
        public int gameModeId;
        public int mapId;
    }
}
```

`Assets/Scripts/Domain/Match.cs` 전체 교체:
```csharp
using System;

namespace LOP
{
    public class Match
    {
        public string id;
        public int queueId;
        public int targetRating;
        public string[] playerList;
        public MatchRound[] rounds;
    }
}
```

`Assets/Scripts/WebAPI/Dto/MatchRoundDto.cs` 생성:
```csharp
using System;

namespace LOP
{
    [Serializable]
    public class MatchRoundDto
    {
        public int index;
        public int gameModeId;
        public int mapId;
    }
}
```

`Assets/Scripts/WebAPI/Dto/MatchDto.cs` 전체 교체:
```csharp
using System;

namespace LOP
{
    [Serializable]
    public class MatchDto
    {
        public string id;
        public int queueId;
        public int targetRating;
        public string[] playerList;
        public MatchRoundDto[] rounds;
    }
}
```

`Assets/Scripts/Domain/UserStats.cs`와 `Assets/Scripts/WebAPI/Dto/UserStatsDto.cs`에서 `public GameMode gameMode;` → `public int queueId;`.

`Assets/Scripts/WebAPI/Dto/Request/MatchmakingRequest.cs` 전체 교체:
```csharp
namespace LOP
{
    public class MatchmakingRequest
    {
        public string userId;
        public int queueId;
        public int gameModeId;
        public int mapId;
    }
}
```

- [ ] **Step 2: WebAPI 쿼리 파라미터 교체**

`Assets/Scripts/WebAPI/WebAPI.cs`의 `GetUserStats`:
```csharp
        public static WebRequest<GetUserStatsResponse> GetUserStats(string userId, int queueId)
        {
            return new WebRequestBuilder<GetUserStatsResponse>()
                .SetUri($"{EnvironmentSettings.active.lobbyBaseURL}/user/{userId}/stats?queueId={queueId}")
```
(메서드의 나머지 체인은 그대로 둔다.)

- [ ] **Step 3: 데이터 스토어 교체**

`Assets/Scripts/Stores/IMatchmakingDataStore.cs` 전체 교체:
```csharp
using GameFramework;

namespace LOP
{
    public interface IMatchmakingDataStore : IDataStore
    {
        int queueId { get; set; }
        int gameModeId { get; set; }
        int mapId { get; set; }
    }
}
```

`Assets/Scripts/Stores/MatchmakingDataStore.cs`에서 세 프로퍼티와 `Clear()` 안의 세 줄을 같은 이름으로 바꾼다:
```csharp
        public int queueId { get; set; }
        public int gameModeId { get; set; }
        public int mapId { get; set; }
```
```csharp
            queueId = default;
            gameModeId = default;
            mapId = default;
```

`Assets/Scripts/Stores/IUserDataStore.cs`에서 두 프로퍼티를 하나로 교체:
```csharp
        UserStats normalUserStats { get; set; }
        UserStats rankedUserStats { get; set; }
```
→
```csharp
        System.Collections.Generic.IReadOnlyDictionary<int, UserStats> userStatsByQueueId { get; }
```

`Assets/Scripts/Stores/UserDataStore.cs`에서 필드·핸들러·`Clear()`를 교체한다.

필드:
```csharp
        public UserStats normalUserStats { get; set; }
        public UserStats rankedUserStats { get; set; }
```
→
```csharp
        // 큐가 enum이 아니라 데이터(TbQueue 행)라서 전적도 큐 id로 담는다.
        private readonly Dictionary<int, UserStats> statsByQueueId = new();
        public IReadOnlyDictionary<int, UserStats> userStatsByQueueId => statsByQueueId;
```

핸들러:
```csharp
        private void HandleGetUserStats(GetUserStatsResponse response)
        {
            UserStats userStats = MapperConfig.mapper.Map<UserStats>(response.userStats);

            if (userStats.gameMode == GameMode.Normal)
            {
                normalUserStats = userStats;
            }
            else if (userStats.gameMode == GameMode.Ranked)
            {
                rankedUserStats = userStats;
            }
        }
```
→
```csharp
        private void HandleGetUserStats(GetUserStatsResponse response)
        {
            UserStats userStats = MapperConfig.mapper.Map<UserStats>(response.userStats);

            statsByQueueId[userStats.queueId] = userStats;
        }
```

`Clear()`:
```csharp
            normalUserStats = null;
            rankedUserStats = null;
```
→
```csharp
            statsByQueueId.Clear();
```

파일 상단에 `using System.Collections.Generic;`을 추가한다.

- [ ] **Step 4: 호출부 교체**

`Assets/Scripts/Matchmaking/MatchStateMachine/States/RequestMatchmaking.cs`:
```csharp
                matchType = matchmakingDataStore.matchType,
                subGameId = matchmakingDataStore.subGameId,
                mapId = matchmakingDataStore.mapId,
```
→
```csharp
                queueId = matchmakingDataStore.queueId,
                gameModeId = matchmakingDataStore.gameModeId,
                mapId = matchmakingDataStore.mapId,
```

`Assets/Scripts/UI/Matchmaking/MatchmakingViewModel.cs`의 `Play()`:
```csharp
            _matchmakingDataStore.matchType = GameMode.Normal;
            _matchmakingDataStore.subGameId = "FlapWang";
            _matchmakingDataStore.mapId = "FlapWangMap";
```
→
```csharp
            // 하드코딩 제거는 로비 선택 UI 슬라이스 몫이다. 지금은 값만 정수 id로.
            _matchmakingDataStore.queueId = 1;      // TbQueue: Casual
            _matchmakingDataStore.gameModeId = 1;   // TbGameMode: FlapWang
            _matchmakingDataStore.mapId = 1;        // TbMap: FlapWangMap
```

`Assets/Scripts/Entrance/EntranceComponent/CheckUserComponent.cs`:
```csharp
                var getNormalUserStats = await WebAPI.GetUserStats(userDataStore.user.id, GameMode.Normal);
                var getRankedUserStats = await WebAPI.GetUserStats(userDataStore.user.id, GameMode.Ranked);
```
→
```csharp
                // 큐 목록을 TbQueue에서 읽는 것은 로비 선택 UI 슬라이스 몫이다 —
                // 마스터데이터가 이 컴포넌트보다 뒤에 로드돼서 지금은 값을 안다고 칠 수 없다.
                await WebAPI.GetUserStats(userDataStore.user.id, 1);   // TbQueue: Casual
                await WebAPI.GetUserStats(userDataStore.user.id, 2);   // TbQueue: Ranked
```

> `getNormalUserStats`/`getRankedUserStats` 지역 변수는 아래에서 쓰이지 않는다(응답은 MessagePipe로 `UserDataStore`에 들어간다). 쓰는 곳이 있으면 변수를 남기고 이름만 바꾼다 — `grep -n "getNormalUserStats\|getRankedUserStats" Assets/Scripts/Entrance/EntranceComponent/CheckUserComponent.cs`로 확인한다.

- [ ] **Step 5: 남은 참조 확인**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/.claude/worktrees/docs+matchmaking-standardization"
grep -rn "GameMode\|matchType\|subGameId\|normalUserStats\|rankedUserStats" Assets/Scripts --include=*.cs | grep -v gameModeId
```
기대 출력: 없음.

- [ ] **Step 6: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/.claude/worktrees/docs+matchmaking-standardization"
git add -A Assets/Scripts
git commit -m "$(cat <<'EOF'
refactor(client): 매칭 어휘를 queueId/gameModeId 정수로

GameMode enum 삭제. 전적은 normalUserStats/rankedUserStats 두 필드
대신 queueId를 키로 하는 사전에 담는다 — 큐가 코드가 아니라 데이터라서.
바깥에서 그 두 필드를 읽던 곳은 없었다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 8: 머지 · 배포 · E2E 검증

**Files:**
- Modify: `infrastructure/k8s/apps/backend/*/kustomization.yaml` (CI가 자동 bump — 손으로 고치지 않는다)
- Modify: `docs/ROADMAP.md` (클라 저장소)

**Interfaces:**
- Consumes: Task 1~7의 모든 변경.
- Produces: 없음(마감).

- [ ] **Step 1: 백엔드 머지 + 푸시**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
git checkout main
git merge --no-ff feature/matchmaking-slice2-vocabulary-rounds -m "Merge feature/matchmaking-slice2-vocabulary-rounds: 매칭 어휘 표준화 + Match 라운드화"
git push origin main
```

- [ ] **Step 2: 게임 서버 머지 + 푸시**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git checkout main
git merge --no-ff feature/matchmaking-slice2-vocabulary-rounds -m "Merge feature/matchmaking-slice2-vocabulary-rounds: 맵을 매치 라운드에서 읽기"
git push origin main
```

- [ ] **Step 3: 클라 머지 + 에디터 컴파일 확인 (진짜 게이트)**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git checkout main
git merge --no-ff worktree-docs+matchmaking-standardization -m "Merge docs+matchmaking-standardization: 매칭 어휘 표준화 + Match 라운드화"
```

그 다음 클라 Unity 에디터에서 컴파일을 확인한다. 인스턴스 id는 `mcpforunity://instances`에서 이름이 `LeagueOfPhysical-Client`인 것을 쓴다.

```
refresh_unity(unity_instance="LeagueOfPhysical-Client@<hash>")
read_console(unity_instance="LeagueOfPhysical-Client@<hash>", types=["error"])
```
기대: 컴파일 에러 0건. 에러가 나면 main에서 고치고 커밋한다(워크트리로 되돌아가지 않는다 — 이미 머지됐다).

- [ ] **Step 4: 백엔드 배포**

GitHub Actions `backend-deploy` 워크플로를 **대상 앱 3개(matchmaking-server, lobby-server, room-server)와 db-migrate**에 대해 실행한다. 워크플로가 이미지를 빌드·푸시하고 `infrastructure`의 태그를 자동 bump하며 ArgoCD가 롤아웃한다.

```bash
gh workflow list --repo Baeinsoo/lop-backend
```
로 워크플로 이름을 확인한 뒤 `gh workflow run <name> --repo Baeinsoo/lop-backend -f <입력>`으로 실행한다. 입력 필드 이름은 `gh workflow view <name> --repo Baeinsoo/lop-backend`로 확인한다.

- [ ] **Step 5: 마이그레이션이 실제로 돌았는지 확인**

```bash
kubectl get jobs -n default | grep db-migrate
kubectl logs job/db-migrate -n default --tail=30
```
기대: `All migrations have been successfully applied.`

그리고 전적이 살아남았는지 본다:
```bash
kubectl exec -n default deploy/postgres -- psql -U postgres -c "SELECT \"queueId\", count(*) FROM \"UserStats\" GROUP BY 1 ORDER BY 1;"
```
기대: `queueId` 1과 2에 각각 유저 수만큼의 행. (파드 이름이 다르면 `kubectl get pods -n default | grep postgres`로 찾는다.)

- [ ] **Step 6: 파드가 새 이미지로 떴는지 확인**

```bash
kubectl get pods -n default -o custom-columns=NAME:.metadata.name,IMAGE:.spec.containers[0].image | grep -E "matchmaking|lobby|room"
```
기대: 세 파드의 이미지 태그가 Step 4에서 푸시한 git sha.

- [ ] **Step 7: 게임 서버 이미지 빌드·배포**

게임 서버 이미지 CI를 실행한다(서버 저장소의 워크플로). 실행 방법은 `gh workflow list --repo Baeinsoo/LeagueOfPhysical-Server`로 확인한다. 콜드 상태면 kind 노드에 이미지 사전 로드가 필요할 수 있다(하트비트 60초 초과 시 파드 삭제 — 알려진 함정).

- [ ] **Step 8: E2E 검증**

클라 2개(메인 에디터 + MPPM 가상 플레이어)로 매칭을 돌린다. 확인할 것:

| | 확인 |
|---|---|
| 1 | 로그인 후 전적 조회가 성공한다(`?queueId=1`, `?queueId=2`) |
| 2 | Play 버튼 → 티켓 발급 → 두 클라가 같은 매치로 묶인다 |
| 3 | 룸이 뜨고 두 클라가 입장한다 |
| 4 | **맵이 뜬다** — 하드코딩이 아니라 `rounds[0].mapId` → `TbMap.scenePath`로 로드된 것 |
| 5 | 게임이 정상 진행된다(이동·전투) |

게임 서버 로그로 4번을 확인한다:
```bash
kubectl logs -n default -l app=room-server --tail=50
kubectl get pods -n default | grep game
```

- [ ] **Step 9: ROADMAP 갱신**

`docs/ROADMAP.md`의 "매치메이킹 표준화 트랙" 절에서 `▶ **다음 = 슬라이스 2**...` 줄을 아래로 교체한다:

```markdown
- ✅ **슬라이스 2 — 필드 어휘 리네임 + `Match` 라운드화 (07-30)** — `matchType`/`subGameId`/`mapId`가
  `queueId`/`gameModeId`/`mapId`(전부 Luban 테이블의 정수 기본키)로 바뀌고 `enum GameMode`가 5곳에서
  사라졌다. `Match`는 게임·맵을 직접 들지 않고 `MatchRound`(원소 1개)로 든다 — 읽기·쓰기는
  `MatchRepository`(애그리게잇 루트)가 감춘다. **게임 서버 맵이 하드코딩에서 `rounds[0].mapId` →
  `TbMap.scenePath`로** 바뀐 것이 이 슬라이스의 유일한 동작 변화다. 유저 전적은 `Normal→1/Ranked→2`
  손질 마이그레이션으로 살렸다(전적 행은 유저 생성 시에만 만들어져 지우면 기존 게스트가 깨진다).
  plan `2026-07-30-matchmaking-slice2-vocabulary-rounds`.
- ▶ **다음 = 슬라이스 3**(티켓 모델 확장 — `creator`→`userIds[]`, 후보 목록 `gameModeIds[]`/`mapIds[]`).
```

- [ ] **Step 10: 커밋 + 푸시**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add docs/ROADMAP.md
git commit -m "$(cat <<'EOF'
docs(roadmap): 슬라이스 2 완료 기록

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
git push origin main
```

---

## 이 슬라이스에서 하지 않는 것 (경계)

혼동을 막기 위해 명시한다. 아래는 전부 다른 슬라이스 몫이다.

| 안 하는 것 | 어디서 |
|---|---|
| `WaitingRoom` 엔티티·이름 삭제, `Updater` 제거, Director 신설 | 슬라이스 4 |
| 티켓의 `creator`→`userIds[]`, 후보 목록 `gameModeIds[]`/`mapIds[]` | 슬라이스 3 |
| `Location.WaitingRoom`→`Matchmaking`, FSM 상태 개명 | 슬라이스 5 |
| `MatchmakingViewModel`의 하드코딩 **제거**(값만 정수로 바꾼다) | 슬라이스 5 |
| 큐 목록을 `TbQueue`에서 순회(클라 진입 순서·로비 서버 마스터데이터) | E(로비 선택 UI) |
| `UserStats` 스키마 구조 개선·레이팅 알고리즘 | D(레이팅) |
| 매치 결과·승패 판정 | C(매치 결과) |
| 두 테이블 쓰기의 트랜잭션 원자성 | 슬라이스 4 |
