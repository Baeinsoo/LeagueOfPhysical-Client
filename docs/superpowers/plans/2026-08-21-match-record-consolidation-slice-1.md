# 슬라이스 1 — 매치 기록 통합 (스키마 + 쓰기 경로) 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 판 하나가 `Match` **한 행**에 자기완결적으로 담기고, **동작은 지금과 똑같다.**

**Architecture:** `MatchRound`·`MatchParticipant` 두 표를 없애고 그 내용을 `Match`의 컬럼으로 옮긴다 —
`playerList`(문자열 배열, 명단), `rounds`(Json), `result`(Json, 확정 시 1회). 도메인 모델과 응답 DTO는
**이미 이 모양**이라(`playerList: string[]` + `rounds[]`) 바뀌지 않는다. 지금 리포지토리가 세 표에서
읽어 도메인으로 도로 조립하는 코드가 사라지는 것이 변경의 대부분이다.

**Tech Stack:** Prisma 6 + PostgreSQL · pnpm/turbo 모노레포 · jest + testcontainers

**Spec:** `docs/superpowers/specs/2026-08-21-match-record-consolidation-design.md`
(클라 레포에 있다: `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client\docs\superpowers\specs\...`)

---

## Global Constraints

- **레포 1개**: `C:\Users\re5na\workspace\LOP\lop-backend`. 브랜치 `feature/match-record-consolidation`.
  게임서버·클라는 **한 줄도 바뀌지 않는다**(응답 DTO 무변경) — 바꿔야 할 것 같으면 멈추고 보고한다.
- **동작 무변화가 이 슬라이스의 성공 기준이다.** 새 기능을 넣지 않는다. 전적 조회 라우트는 슬라이스 2다.
- **빌드가 테스트보다 먼저다.** 검증 명령은 항상 `pnpm exec turbo run build --force`를 맨 앞에 둔다 —
  ts-jest는 import되지 않는 파일의 타입 오류를 건너뛰므로 **테스트 전부 초록인데 빌드가 깨질 수 있다.**
  `--force`가 없으면 turbo 캐시 히트가 "통과"를 위조한다.
- **삭제는 역방향으로 검증한다.** 없애는 것(`MatchRound`/`MatchParticipant` 표, `matchRound.dao`,
  `matchRound.mapper`, `findParticipantUserIds`)마다 **아직 부르는 곳이 있는지** 레포 전체 grep.
- **명단 게이트를 깨지 않는다.** `playerList`는 매치 생성 시 확정되고 **이후 절대 갱신되지 않는다.**
  결과 보고는 그것과 대조만 한다. 이 성질이 게임서버의 명단 위조를 막는다.
- **멱등 확정을 깨지 않는다.** `state != 'Finished'` 조건부 갱신(CAS)이 먼저, 명단 검증은 그 **뒤**
  (던지면 CAS까지 롤백). 재보고는 저장된 결과를 그대로 돌려준다.
- **정렬 기준을 섞지 않는다.** 확정 경로와 재시도 응답이 같은 순서를 내야 한다. `localeCompare` 금지,
  서수 비교(`a < b ? -1 : ...`)만 쓴다.
- 주석은 **왜**만 쓴다. 코드로 자명한 것은 달지 않는다. 한국어.

---
## Task 1: 스키마 + 마이그레이션

**Files:**
- Modify: `packages/database/prisma/schema.prisma`
- Create: `packages/database/prisma/migrations/20260821000000_consolidate_match_record/migration.sql`

**Interfaces:**
- Produces: Prisma 타입 `Match`가 `playerList: string[]`, `rounds: Prisma.JsonValue`,
  `result: Prisma.JsonValue | null`을 갖는다. `MatchRound`·`MatchParticipant` 타입은 **사라진다**.
  Task 2·3이 이걸 쓴다.

- [ ] **Step 1: 스키마 교체**

`schema.prisma`에서 `model Match` 전체를 아래로 바꾸고, `model MatchRound`와
`model MatchParticipant` **두 블록을 통째로 삭제**한다. `enum MatchState`는 **그대로 둔다**.

```prisma
//  한 판의 전부가 이 한 행에 담긴다. 확정된 매치는 다시 바뀌지 않는 기록이라,
//  나눠 두면 읽을 때 조립만 늘고 얻는 게 없다(Riot Match-V5의 매치 문서와 같은 모양).
model Match {
  id         String     @id @unique
  queueId    Int
  targetMmr  Int        @default(1000)
  state      MatchState @default(Created)
  createdAt  DateTime   @default(now())
  startedAt  DateTime?
  endedAt    DateTime?

  //  명단. 매치가 만들어질 때 확정되고 이후 절대 갱신하지 않는다 — 결과 보고를 이것과
  //  대조하므로, 게임서버가 명단에 없는 userId를 끼워 넣을 수 없다.
  playerList String[]

  //  [{ index, gameModeId, mapId }]. 지금은 원소 1개뿐이지만 여러 게임을 연속으로 하되
  //  최종 결과는 하나인 형태를 위해 목록으로 둔다.
  rounds     Json       @default("[]")

  //  결과가 확정될 때 정확히 한 번 쓴다. 그전에는 null.
  //  [{ userId, displayName, placement, mmrBefore, mmrAfter, muBefore, muAfter, sigmaBefore, sigmaAfter }]
  //  displayName은 "그때 이름"이다 — 계정 이름은 바뀔 수 있고, 바뀌어도 과거 기록은 그대로여야 한다.
  result     Json?

  @@index([playerList], type: Gin)
}
```

- [ ] **Step 2: 마이그레이션 작성**

기존 데이터는 6행뿐이지만 **이전은 한다** — 지금 판이 사라지면 프로필 전적이 빈 채로 시작한다.

```sql
--  판 하나가 세 표에 흩어져 있던 것을 Match 한 행으로 합친다.
--  확정된 매치는 불변 기록이라 한 행에 담아도 어긋날 곳이 없고, 읽을 때 조립이 사라진다.
ALTER TABLE "Match" ADD COLUMN "playerList" TEXT[] NOT NULL DEFAULT ARRAY[]::TEXT[];
ALTER TABLE "Match" ADD COLUMN "rounds"     JSONB  NOT NULL DEFAULT '[]'::JSONB;
ALTER TABLE "Match" ADD COLUMN "result"     JSONB;

--  명단: 참가자 행의 userId를 정렬해 배열로. 기존 코드도 userId 오름차순으로 읽고 있었다.
UPDATE "Match" m
SET "playerList" = COALESCE((
    SELECT array_agg(p."userId" ORDER BY p."userId")
    FROM "MatchParticipant" p WHERE p."matchId" = m.id
), ARRAY[]::TEXT[]);

--  라운드: index 순 객체 배열로.
UPDATE "Match" m
SET "rounds" = COALESCE((
    SELECT jsonb_agg(jsonb_build_object('index', r."index", 'gameModeId', r."gameModeId", 'mapId', r."mapId") ORDER BY r."index")
    FROM "MatchRound" r WHERE r."matchId" = m.id
), '[]'::JSONB);

--  결과: 확정된 판만. placement가 NULL인 참가자가 하나라도 있으면 그 판은 확정된 적이 없다.
--  displayName은 지금까지 저장한 적이 없으므로 현재 계정 이름으로 채운다 — 과거는 복원할 수 없고,
--  이 시점 이후의 판부터 "그때 이름"이 제대로 박힌다.
UPDATE "Match" m
SET "result" = (
    SELECT jsonb_agg(jsonb_build_object(
        'userId', p."userId",
        'displayName', COALESCE(u."username", p."userId"),
        'placement', p."placement",
        'mmrBefore', p."mmrBefore",
        'mmrAfter', p."mmrAfter",
        'muBefore', p."muBefore",
        'muAfter', p."muAfter",
        'sigmaBefore', p."sigmaBefore",
        'sigmaAfter', p."sigmaAfter"
    ) ORDER BY p."userId")
    FROM "MatchParticipant" p
    LEFT JOIN "User" u ON u.id = p."userId"
    WHERE p."matchId" = m.id
)
WHERE m."state" = 'Finished'
  AND NOT EXISTS (SELECT 1 FROM "MatchParticipant" p2 WHERE p2."matchId" = m.id AND p2."placement" IS NULL);

--  기본값은 이전용이었다. 앞으로는 쓰는 쪽이 항상 값을 넣는다.
ALTER TABLE "Match" ALTER COLUMN "playerList" DROP DEFAULT;
ALTER TABLE "Match" ALTER COLUMN "rounds"     DROP DEFAULT;

--  "이 유저가 낀 판" 조회용(전적 목록). 배열 포함 검색이라 b-tree가 아니라 GIN이다.
CREATE INDEX "Match_playerList_idx" ON "Match" USING GIN ("playerList");

DROP TABLE "MatchParticipant";
DROP TABLE "MatchRound";
```

- [ ] **Step 3: 스키마 문법 검증**

GIN 인덱스 선언을 Prisma가 받아주는지 먼저 본다.

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
pnpm --filter @lop/database exec prisma validate
```

기대: `The schema at ... is valid`. **거절당하면** `@@index([playerList], type: Gin)` 줄만 스키마에서
빼고(마이그레이션 SQL의 `CREATE INDEX ... USING GIN`은 **그대로 둔다**), Prisma가 낸 메시지를 보고서에
적는다 — 스키마에 없는 인덱스는 나중에 `prisma migrate dev`가 드리프트로 잡을 수 있다.

- [ ] **Step 4: 마이그레이션을 스크래치 DB에서 검증**

⚠️ **실DB에 psql로 직접 적용하지 말 것.** Prisma는 `_prisma_migrations` 표로 적용 이력을 추적하는데,
손으로 밀어 넣으면 그 기록이 없어 배포 때 `migrate deploy`가 같은 걸 또 돌리려다 실패한다.
실제 적용은 T5의 배포(`db-migrate`)가 한다. 여기서는 **복제본에서 SQL이 도는지만** 본다.

```bash
kubectl exec deploy/postgres-deployment -- psql -U postgres -c 'DROP DATABASE IF EXISTS scratch;'
kubectl exec deploy/postgres-deployment -- psql -U postgres -c 'CREATE DATABASE scratch TEMPLATE postgres;'
```

> `TEMPLATE postgres`는 원본 DB에 열린 연결이 없어야 한다. 실패하면 이 단계를 **건너뛰고 보고**한다 —
> 마이그레이션은 T5 배포에서 실제로 밟게 되므로 검증 기회가 또 있다. (백엔드 파드를 0으로 줄여
> 연결을 끊는 방법도 있지만 ArgoCD가 되살리므로 권하지 않는다.)

```bash
kubectl exec -i deploy/postgres-deployment -- psql -U postgres -d scratch -v ON_ERROR_STOP=1   < packages/database/prisma/migrations/20260821000000_consolidate_match_record/migration.sql
```

기대: 에러 없이 완료. 이전 결과를 검증한다:

```bash
kubectl exec deploy/postgres-deployment -- psql -U postgres -d scratch -c   'SELECT id, state, array_length("playerList",1) AS n_players, jsonb_array_length("rounds") AS n_rounds, ("result" IS NOT NULL) AS has_result FROM "Match" ORDER BY "createdAt" DESC;'
```

기대: **끝난 판 3개**는 `n_players=2` · `n_rounds=1` · `has_result=t`. **Created 판들**은
`n_players=2` · `n_rounds=1` · `has_result=f`. 하나라도 어긋나면 멈추고 보고한다.

결과 내용도 눈으로 본다:

```bash
kubectl exec deploy/postgres-deployment -- psql -U postgres -d scratch -c   "SELECT jsonb_pretty(\"result\") FROM \"Match\" WHERE state = 'Finished' ORDER BY \"endedAt\" DESC LIMIT 1;"
```

기대: 참가자 2명, 각각 `userId`/`displayName`(`Guest-...`)/`placement`/`mmrBefore`/`mmrAfter`가 채워져 있다.

정리:

```bash
kubectl exec deploy/postgres-deployment -- psql -U postgres -c 'DROP DATABASE scratch;'
```

- [ ] **Step 5: Prisma 클라이언트 재생성 + 빌드**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
pnpm --filter @lop/database run generate
pnpm exec turbo run build --force
```

기대: **빌드가 깨진다.** `MatchRound`/`MatchParticipant` 타입이 사라져 Task 2·3이 고칠 파일들이
타입 에러를 낸다. **이 실패는 정상이고, 어떤 파일이 깨졌는지 목록을 보고서에 적는다** — Task 2·3의
작업 목록이 된다.

- [ ] **Step 6: 커밋**

```bash
git add packages/database/prisma/schema.prisma packages/database/prisma/migrations
git commit -m "feat(match): 판 하나를 Match 한 행에 담는다"
```

`packages/database/generated/`는 빌드 산출물이다 — `git status --short`로 스테이지된 것이
스키마와 마이그레이션뿐인지 확인한다.

---

## Task 2: matchmaking-server — 세 표 조립을 걷어낸다

**Files:**
- Modify: `apps/matchmaking-server/src/daos/match.dao.postgres.ts`
- Modify: `apps/matchmaking-server/src/mappers/entities/match.mapper.ts`
- Modify: `apps/matchmaking-server/src/repositories/match.repository.ts`
- Delete: `apps/matchmaking-server/src/daos/matchRound.dao.postgres.ts`
- Delete: `apps/matchmaking-server/src/mappers/entities/matchRound.mapper.ts`

**Interfaces:**
- Consumes: Task 1의 `Match` 타입(`playerList: string[]`, `rounds: Json`, `result: Json | null`).
- Produces: `MatchDaoPostgres.save(match, matchedUserIds, requiredTicketIds)` — 이름이
  `saveWithRounds`에서 바뀐다(라운드가 더 이상 별도 개념이 아니다). `findParticipantUserIds`는 **삭제**.
  `MatchMapper.toDomain/toEntity`가 `playerList`·`rounds`를 **실제로 채운다**.

**배경:** 도메인 `Match`는 이미 `playerList: string[]` + `rounds: MatchRound[]`를 갖는다. 지금은
엔티티에 그 자리가 없어서 매퍼가 빈 값으로 두고, 리포지토리가 표 두 개를 더 읽어 채워 넣는다.
컬럼이 생기면 **그 우회가 통째로 사라진다.**

- [ ] **Step 1: 매퍼가 실제로 채우게 한다**

`match.mapper.ts`의 클래스 상단 주석(라운드·playerList를 못 채운다는 설명) **3줄을 삭제**하고,
`toDomain`/`toEntity`를 아래로 바꾼다. 나머지 메서드는 그대로 둔다.

```ts
    public toDomain(entity: MatchEntity): Match {
        return {
            id: entity.id,
            queueId: entity.queueId,
            targetMmr: entity.targetMmr,
            state: entity.state,
            createdAt: entity.createdAt,
            startedAt: entity.startedAt,
            endedAt: entity.endedAt,
            playerList: entity.playerList,
            //  Json 컬럼이라 Prisma가 모양을 검증하지 않는다. 쓰는 곳이 이 파일과
            //  결과 확정 한 곳뿐이라, 읽을 때 형태를 한 번 맞춰 주고 index 순으로 세운다.
            rounds: (entity.rounds as unknown as MatchRound[] ?? [])
                .map(round => ({ index: round.index, gameModeId: round.gameModeId, mapId: round.mapId }))
                .sort((x, y) => x.index - y.index),
        };
    }

    public toEntity(domain: Match): MatchEntity {
        return {
            id: domain.id,
            queueId: domain.queueId,
            targetMmr: domain.targetMmr,
            state: domain.state,
            createdAt: new Date(domain.createdAt),
            startedAt: domain.startedAt,
            endedAt: domain.endedAt,
            playerList: domain.playerList,
            rounds: domain.rounds as unknown as Prisma.JsonValue,
            result: null,
        };
    }
```

import에 `MatchRound`(도메인)와 `Prisma`를 추가한다:

```ts
import { MatchRound } from '@interfaces/matchRound.interface';
import { Match as MatchEntity, Prisma } from '@lop/database';
```

> ⚠️ `toEntity`가 `result: null`을 넣는 것에 주의. 이 매퍼는 **매치 생성 경로에서만** 쓰이고,
> 아래 DAO가 `update`에서 `result`를 제외하므로 확정된 결과를 덮지 않는다. 그 제외를 없애면
> 재저장이 결과를 지운다.

- [ ] **Step 2: DAO에서 라운드·참가자 표 조작을 걷어낸다**

`match.dao.postgres.ts`에서:

1. import를 `import { PrismaClient, Prisma, Match as MatchEntity } from '@lop/database';`로 바꾼다
   (`MatchRound as MatchRoundEntity` 제거)
2. `saveWithRounds` → **`save`** 로 이름을 바꾸고 시그니처를 줄인다
3. `tx.matchRound.deleteMany` / `tx.matchRound.createMany` / `tx.matchParticipant.createMany` **삭제**
4. `findParticipantUserIds` 메서드 **통째로 삭제**
5. 메서드 문서주석의 첫 문단을 아래로 교체(라운드/참가자 표가 없어졌으므로)

```ts
    /**
     * 매치 행 저장 + 티켓 소비 표시를 한 트랜잭션으로 묶는다. 매치만 생기고 티켓이 풀에 남으면
     * 다음 틱이 같은 사람을 또 매칭하므로, 둘 중 하나만 반영되고 끝나면 안 된다.
     *
     * **콜백형(인터랙티브) 트랜잭션인 이유**: 아래 CAS가 어긋나면 *중간에 던져 통째로 롤백*해야 하는데,
     * 배열형 `$transaction([...])`은 그런 조건부 중단을 표현할 수 없다.
     *
     * (티켓 2단계 설명은 아래 원문 그대로 유지한다)
     */
    public async save(
        match: MatchEntity,
        matchedUserIds: string[] = [],
        requiredTicketIds: string[] = [],
    ): Promise<{ match: MatchEntity; consumedTicketIds: string[] }> {
```

트랜잭션 안의 upsert는 아래로 바꾼다:

```ts
                //  update에 match를 통째로 넣으면 판의 생애(state/startedAt/endedAt)와 확정된
                //  결과(result)까지 덮어써서, 이미 끝난 매치를 다시 저장할 때 그 사실이 지워진다.
                //  여기(매치 성사)는 생애의 시작점일 뿐이라 그 컬럼들을 건드리지 않는다.
                //  playerList도 뺀다 — 명단은 생성 때 확정되고 이후 불변이어야 결과 보고의
                //  위조 방지 기준이 된다.
                const { state, startedAt, endedAt, result, playerList, ...updatable } = match;

                const savedMatch = await tx.match.upsert({
                    where: { id: match.id },
                    update: updatable,
                    create: match,
                });
```

그 아래 `matchRound`/`matchParticipant` 블록 세 개를 지운다. 티켓 CAS 이하는 **한 줄도 바꾸지 않는다.**

- [ ] **Step 3: 리포지토리에서 조립을 걷어낸다**

`match.repository.ts`에서:

1. `roundDao`/`roundMapper` 필드와 그 import·생성자 초기화 삭제
2. `saveConsumingTickets` 본문을 아래로

```ts
    public async saveConsumingTickets(match: Match, matchedUserIds: string[], requiredTicketIds: string[]): Promise<{ match: Match; consumedTicketIds: string[] }> {
        try {
            const { match: savedEntity, consumedTicketIds } = await this.matchDao.save(
                this.mapper.toEntity(match), matchedUserIds, requiredTicketIds,
            );

            return { match: this.mapper.toDomain(savedEntity), consumedTicketIds };
        } catch (error) {
            return Promise.reject(error);
        }
    }
```

3. **`findById` 오버라이드를 통째로 삭제한다.** 베이스가 하는 일과 같아졌다(한 행 읽고 매퍼에 통과).
   삭제 후 클래스에 남는 메서드가 `saveConsumingTickets` 하나뿐인지 확인한다.

- [ ] **Step 3b: 명단 중복을 생성 시점에 막는다**

`@@unique([matchId, userId])`가 사라지면서 DB가 막아 주던 참가자 중복이 코드 책임이 된다.
`match.dao.postgres.ts`의 트랜잭션 안, `upsert` **바로 앞**에 넣는다:

```ts
                //  같은 userId가 두 번 들어오면 결과 보고 대조가 어긋난다(보고는 사람당 하나씩 온다).
                //  표를 합치면서 DB의 유니크 제약이 사라졌으므로 여기서 막는다 — 여기까지 왔다는 건
                //  티켓 선점이 같은 사람을 두 번 실었다는 뜻이라, 조용히 고치지 않고 드러낸다.
                if (new Set(match.playerList).size !== match.playerList.length) {
                    throw new Error(`Duplicate userId in playerList. matchId: ${match.id}`);
                }
```

- [ ] **Step 4: 죽은 파일 삭제 + 역방향 검증**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
rm apps/matchmaking-server/src/daos/matchRound.dao.postgres.ts
rm apps/matchmaking-server/src/mappers/entities/matchRound.mapper.ts
```

**없앤 것을 아직 부르는 곳이 있는지** 레포 전체에서 확인한다(정방향 grep이 아니라 역방향이다):

```bash
grep -rn "matchRound\|MatchRound\b" apps/ packages/ --include=*.ts | grep -v "matchRound.interface\|MatchRoundDto\|node_modules\|generated"
grep -rn "saveWithRounds\|findParticipantUserIds\|matchParticipant" apps/ packages/ --include=*.ts | grep -v node_modules
```

기대: **첫 번째는 도메인 인터페이스(`matchRound.interface.ts`)와 DTO만** 남는다(둘 다 유지 대상).
두 번째는 **lobby-server의 결과 확정 DAO만** 남는다(Task 3이 고친다). 테스트 파일에서 걸리면 그것도
같이 고친다.

- [ ] **Step 5: 빌드 + 매칭 테스트**

```bash
pnpm exec turbo run build --force
pnpm --filter matchmaking-server test
```

기대: 빌드는 **lobby-server만** 깨진다(Task 3 몫). matchmaking 테스트는 전부 통과 —
`Cached: 0 cached` 문구를 보고서에 그대로 붙인다.

- [ ] **Step 6: 커밋**

```bash
git add apps/matchmaking-server
git commit -m "refactor(match): 매치를 한 행으로 읽고 쓴다"
```

---

## Task 3: lobby-server — 결과를 문서 한 번으로 확정한다

**Files:**
- Modify: `apps/lobby-server/src/daos/match-result.dao.postgres.ts`
- Modify: `apps/lobby-server/src/interfaces/match-result.interface.ts`

**Interfaces:**
- Consumes: Task 1의 `Match.playerList`(명단) / `Match.result`(결과 문서).
- Produces: `ConfirmOutcome`은 **모양 그대로**(`confirmed`/`alreadyConfirmed`/`matchNotFound`/
  `rosterMismatch` + `participants: ConfirmedParticipant[]`). 컨트롤러·서비스·응답 DTO는 **무변경**.

**이 태스크가 이 슬라이스에서 가장 위험하다.** 멱등 확정(CAS)과 명단 위조 거절이 여기 있다.
아래 성질을 하나도 바꾸지 않으면서 저장 위치만 옮긴다.

- [ ] **Step 1: 결과 문서 타입을 인터페이스에 추가**

`match-result.interface.ts` **끝에** 추가한다(기존 타입은 건드리지 않는다):

```ts
/**
 * Match.result에 저장되는 참가자 한 명. Json 컬럼이라 DB가 모양을 검증하지 않으므로,
 * 쓰는 곳(확정 트랜잭션)과 읽는 곳이 이 타입 하나만 보도록 한다.
 *
 * displayName은 "그때 이름"이다 — 계정 이름은 바뀔 수 있고, 바뀌어도 과거 기록은 그대로여야 한다.
 */
export interface StoredParticipant {
    userId: string;
    displayName: string;
    placement: number;
    mmrBefore: number;
    mmrAfter: number;
    muBefore: number;
    muAfter: number;
    sigmaBefore: number;
    sigmaAfter: number;
}
```

- [ ] **Step 2: 명단을 `playerList`에서 읽는다**

`confirm()` 안에서 `roster` 조회를 아래로 바꾼다. **위치는 그대로 CAS 뒤**여야 한다 — 앞으로 옮기면
두 요청이 동시에 검증을 통과해 둘 다 계산할 수 있다.

기존:

```ts
                const roster = await tx.matchParticipant.findMany({
                    where: { matchId },
                    orderBy: { userId: 'asc' },
                });

                const 보고된 = [...placements].map(p => p.userId).sort();
                const 명단 = roster.map(p => p.userId).sort();
```

교체:

```ts
                //  명단은 매치가 만들어질 때 확정돼 이후 갱신되지 않는다 — 그래서 보고를 이것과
                //  대조하면 게임서버가 명단에 없는 userId를 끼워 넣을 수 없다.
                const 보고된 = [...placements].map(p => p.userId).sort();
                const 명단 = [...match.playerList].sort();
```

`match`는 이 블록 위에서 이미 `findUnique`로 읽어 둔 값이다(추가 조회 없음).

- [ ] **Step 3: 그때 이름을 읽어 온다**

`before` 맵을 만드는 루프 **바로 앞에** 추가한다:

```ts
                //  확정 시점의 계정 이름을 결과에 박는다. 조회할 때 User에서 끌어오면 누가 개명하는
                //  순간 과거 전적이 소급해서 바뀐다 — 전적은 "그때 무슨 일이 있었나"의 기록이다.
                const users = await tx.user.findMany({
                    where: { id: { in: 명단 } },
                    select: { id: true, username: true },
                });
                const 이름 = new Map(users.map(u => [u.id, u.username]));
```

- [ ] **Step 4: 참가자별 update를 결과 문서 한 번으로 바꾼다**

기존 루프 안의 `tx.matchParticipant.update({...})` **블록을 삭제**하고, 대신 루프 위에 배열을 만들어
루프 안에서 채운 뒤 루프가 끝나고 **한 번** 쓴다.

루프 앞:

```ts
                const stored: StoredParticipant[] = [];
```

루프 안, `confirmed.push(...)` **바로 앞**에:

```ts
                    stored.push({
                        userId,
                        //  계정이 지워진 뒤 확정되는 경우가 이론상 있다. 그때는 id를 이름 자리에
                        //  둔다 — 화면에 빈칸이 뜨는 것보다 낫고, 무엇이 빠졌는지도 드러난다.
                        displayName: 이름.get(userId) ?? userId,
                        placement,
                        mmrBefore: prev.mmr,
                        mmrAfter,
                        muBefore: prev.mu,
                        muAfter: next.mu,
                        sigmaBefore: prev.sigma,
                        sigmaAfter: next.sigma,
                    });
```

루프가 끝난 **직후**, `return { kind: 'confirmed', ... }` 앞에:

```ts
                //  결과는 판당 한 번만 쓴다. 위 CAS가 이 트랜잭션 하나만 여기 도달하게 보장한다.
                await tx.match.update({
                    where: { id: matchId },
                    data: { result: stored as unknown as Prisma.InputJsonValue },
                });
```

`UserRating` upsert는 **한 줄도 바꾸지 않는다.**

- [ ] **Step 5: 재시도 응답을 문서에서 읽는다**

`readConfirmed`를 아래로 교체한다:

```ts
    /** 이미 확정된 매치의 저장된 결과. 재시도한 보고에 같은 답을 주기 위한 것이다. */
    private async readConfirmed(tx: Prisma.TransactionClient, matchId: string): Promise<ConfirmedParticipant[]> {
        const match = await tx.match.findUnique({
            where: { id: matchId },
            select: { result: true },
        });

        const stored = (match?.result ?? []) as unknown as StoredParticipant[];

        return stored.map(row => ({
            userId: row.userId,
            placement: row.placement,
            mmrBefore: row.mmrBefore,
            mmrAfter: row.mmrAfter,
        }));
    }
```

> **바뀐 것 하나를 의식하고 갈 것:** 옛 코드는 `placement ?? 0`으로 null을 0으로 메웠다. 이제
> `result`는 확정될 때 통째로 쓰이므로 부분적으로 빈 상태가 존재하지 않는다 — 메울 일이 없다.
> `result`가 통째로 null인 경우(확정 안 된 매치)는 빈 배열이 되는데, 이 함수는 CAS가 0행일 때만
> 불리므로 그 경로로는 오지 않는다.

import에 `StoredParticipant`를 추가한다.

- [ ] **Step 6: 통합 테스트 갱신 + 추가**

`apps/lobby-server/test/integration/matchResult.integration.test.ts`가 `matchParticipant`를 직접
만들고 읽는다. 그 부분을 `Match.playerList`/`Match.result`로 옮긴다. **테스트가 검증하는 성질은
바꾸지 않는다** — 멱등 확정, 명단 위조 거절, 명단 일부 누락 거절.

그리고 **이번 변경의 존재 이유를 고정하는 테스트를 새로 추가**한다:

```ts
    it('확정 뒤 계정 이름이 바뀌어도 전적의 이름은 그대로다', async () => {
        //  전적은 "그때 무슨 일이 있었나"의 기록이다. 조회 시점에 User에서 끌어오면
        //  개명이 과거를 소급해서 바꾼다 — 그걸 막는 것이 displayName을 박아 두는 이유다.
        const { matchId, userIds } = await 매치를_만든다(2);
        await dao.confirm(matchId, [
            { userId: userIds[0], placement: 1 },
            { userId: userIds[1], placement: 2 },
        ]);

        const before = await 저장된_결과(matchId);
        await prismaClient.user.update({
            where: { id: userIds[0] },
            data: { username: '개명한이름' },
        });

        const after = await 저장된_결과(matchId);
        expect(after[0].displayName).toBe(before[0].displayName);
        expect(after[0].displayName).not.toBe('개명한이름');
    });
```

> **실제 헬퍼는 `매치를_만든다(matchId: string, userIds: string[], queueId = 1)`** 이다(파일 상단).
> 지금은 `rawPrisma.matchParticipant.createMany`로 명단을 깐다 — 그 줄을 지우고 `match.create`의
> `data`에 **`playerList: userIds, rounds: []`** 를 넣도록 고친다. 위 새 테스트도 이 시그니처를 쓴다
> (`매치를_만든다('m-rename', ['u1','u2'])`).
>
> `저장된_결과`는 **없으니 만든다**:
>
> ```ts
> async function 저장된_결과(matchId: string) {
>     const row = await rawPrisma.match.findUnique({ where: { id: matchId }, select: { result: true } });
>     return (row?.result ?? []) as unknown as Array<{ userId: string; displayName: string; placement: number }>;
> }
> ```

- [ ] **Step 7: 빌드 + 전체 테스트**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
pnpm exec turbo run build --force
pnpm --filter lobby-server test
pnpm --filter matchmaking-server test
```

기대: **빌드 5/5 통과**(`Cached: 0 cached` 확인), 두 앱 테스트 전부 통과.
보고서에 빌드 출력의 캐시 줄과 테스트 요약을 그대로 붙인다.

- [ ] **Step 8: 커밋**

```bash
git add apps/lobby-server
git commit -m "refactor(match): 결과를 매치 행에 문서로 확정한다"
```

---

## Task 4: 배포 + 회귀 검증 (사람 손 필요)

**Files:** 없음(운영 작업)

**Interfaces:**
- Consumes: Task 1~3의 커밋 전부.

> **왜 사람이 필요한가:** 마이그레이션이 실DB에 처음 적용되고, 에디터 2대로 실제 한 판을 돌려야 한다.
> **이 슬라이스는 "동작 무변화"가 성공 기준**이라, 새 화면이 아니라 **기존 흐름이 그대로인지**를 본다.

- [ ] **Step 1: 배포 — `app=all` (마이그레이션 포함)**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
git push origin feature/match-record-consolidation
gh workflow run backend-deploy.yml --ref feature/match-record-consolidation -f app=all -f environment=local
```

> **`app=all`이어야 한다** — 마이그레이션이 있으면 `db-migrate`가 함께 돌아야 하고,
> `packages/database`가 바뀌었으니 세 앱 모두 새 이미지가 필요하다.
> **게임서버는 재빌드 불필요** — 이 슬라이스는 Unity 코드를 한 줄도 안 바꿨다.

- [ ] **Step 2: 마이그레이션이 실제로 적용됐는지 확인**

```bash
kubectl get jobs | grep db-migrate
kubectl logs job/<db-migrate-job> | tail -20
```

기대: 성공 종료. 그리고 스키마와 데이터를 직접 본다:

```bash
kubectl exec deploy/postgres-deployment -- psql -U postgres -d postgres -c '\d "Match"'
kubectl exec deploy/postgres-deployment -- psql -U postgres -d postgres -c \
  'SELECT id, state, array_length("playerList",1) AS n_players, ("result" IS NOT NULL) AS has_result FROM "Match" ORDER BY "createdAt" DESC LIMIT 5;'
```

기대: `playerList`/`rounds`/`result` 컬럼 존재, GIN 인덱스 존재, **`MatchParticipant`·`MatchRound` 표는
사라짐**, 끝난 판 3개에 `has_result=t`.

- [ ] **Step 3: 파드가 새 이미지로 롤아웃됐는지**

```bash
kubectl get pods -o custom-columns='NAME:.metadata.name,IMAGE:.spec.containers[0].image,STATUS:.status.phase'
```

기대: lobby/matchmaking/room 전부 새 태그로 Running. **옛 태그면 기다린다** — 태그만 올라가고
파드는 옛 것이 계속 응답하는 상태가 실재한다.

- [ ] **Step 4: 회귀 검증 (이 슬라이스의 본 게임)**

`local-k8s`로 클라 에디터 2개. **전부 이전과 똑같아야 한다:**

| 확인 | 기대 |
|---|---|
| 로그인 → 로비 | 정상 |
| 매칭 → 방 진입 | **정상** — 방 접속 인증이 `playerList`를 읽는다(명단 게이트) |
| 게임 진행 | 캐릭터 스폰 정상 — 스폰 루프도 `playerList`를 읽는다 |
| 매치 종료 → 결과 화면 | 등수표와 본인 점수 변화가 이전과 같이 뜬다 |
| 프로필 | 판수·1등·평균 등수·전적 점수가 갱신된다 |

DB로 대조한다:

```bash
kubectl exec deploy/postgres-deployment -- psql -U postgres -d postgres -c \
  "SELECT jsonb_pretty(\"result\") FROM \"Match\" WHERE state='Finished' ORDER BY \"endedAt\" DESC LIMIT 1;"
```

기대: 방금 판의 참가자 2명이 `displayName` 포함해 담겨 있고, `mmrBefore`가 **직전 판의 `mmrAfter`와
일치**한다(고리가 여전히 돈다).

- [ ] **Step 5: 멱등성 재확인**

확정 경로를 다시 썼으므로 **다시 증명한다.** 등수를 뒤집어 재보고해도 저장된 결과가 그대로여야 한다.

```bash
KEY=$(kubectl get secret internal-api-secret -o jsonpath='{.data.INTERNAL_API_KEY}' | base64 -d)
POD=$(kubectl get pods -l app=lobby-server --field-selector=status.phase=Running -o jsonpath='{.items[0].metadata.name}')
kubectl exec $POD -- curl -s -X POST -H "X-Internal-Api-Key: $KEY" -H "Content-Type: application/json" \
  -d '{"participants":[{"userId":"<1등이었던 사람>","placement":2},{"userId":"<2등이었던 사람>","placement":1}]}' \
  http://localhost:80/internal/match/<방금 matchId>/result
```

기대: **저장된 원래 결과**가 그대로 돌아온다(등수가 뒤집히지 않음). 그리고 `UserRating.gamesPlayed`와
`updatedAt`이 **안 움직였는지** 확인한다.

> ⚠️ `/internal`은 인그레스에서 차단돼 있다 — `localhost/lobby/internal/...`은 404다. 위처럼
> 클러스터 안에서 쳐야 한다.

- [ ] **Step 6: 머지**

`CLAUDE.md`의 "푸시 규약"대로. **한 줄씩 결과를 확인하고 넘어간다.** 백엔드 레포 하나뿐이라
Unity 픽스처 stash는 해당 없다.

---

## 검증 요약 (전체가 끝났다는 기준)

1. 빌드 5/5 (`--force`로 캐시 우회 확인), 두 앱 테스트 전부 통과
2. 역방향 grep 클린 — `matchParticipant`/`matchRound`/`saveWithRounds`/`findParticipantUserIds`를
   부르는 곳이 남아 있지 않다
3. 마이그레이션 적용 후 끝난 판 3개가 `result`를 갖고, 두 표가 사라졌다
4. **실플레이 회귀 0** — 매칭·방 진입·스폰·결과 화면·프로필이 전부 이전과 같다
5. **멱등성 재증명** — 등수를 뒤집어 재보고해도 저장값 불변
6. **그때 이름 고정** — 통합 테스트로 개명이 과거를 안 바꾸는 것이 박혀 있다
