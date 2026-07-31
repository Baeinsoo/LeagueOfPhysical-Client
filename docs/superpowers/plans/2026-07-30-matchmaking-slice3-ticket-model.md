# 매치메이킹 슬라이스 3 — 티켓 모델 확장 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 매칭 티켓이 유저 하나·게임 하나·맵 하나 대신 **목록**(`userIds`/`gameModeIds`/`mapIds`)을 들도록 저장 모델을 바꾼다 — 슬라이스 4의 Director가 필요로 하는 모양을 미리 갖추는 준비 작업이다.

**Architecture:** 백엔드 모노레포 안에서만 끝난다. DB 스키마 → 티켓 모델(도메인·DTO·매퍼·팩토리) → 소비처 3곳 순서로 간다. 클라가 보내는 요청은 그대로 단수이고, **서버가 받은 값을 `[값]`으로 감싸 저장**한다 — 그 감싸는 자리가 요청 경계(`matchmaking.service`)다.

**Tech Stack:** pnpm + turbo 모노레포(TypeScript, Express, Prisma 6, jest), k8s + ArgoCD GitOps.

## Global Constraints

- **설계 원천:** `docs/superpowers/specs/2026-07-27-matchmaking-standardization-design.md` — 특히 §8의 "슬라이스 3 확정 사항".
- **저장소는 `Baeinsoo/lop-backend` 모노레포 하나뿐이다.** 클라(`LeagueOfPhysical-Client`)와 게임 서버(`LeagueOfPhysical-Server`)는 **이 슬라이스에서 한 줄도 바꾸지 않는다.** 두 소비자 모두 티켓의 필드를 읽지 않는 것이 확인됐다(클라는 `ticketId` 문자열만, 로비 서버는 존재 여부만).
- **와이어 계약은 바뀌지 않는다.** `RequestMatchmakingDto`(클라 → 매칭 서버)는 `{ userId, queueId, gameModeId, mapId }` 그대로다. 그래서 이번엔 새 클라 + 옛 백엔드 같은 배포 창이 없다.
- **고정 정수 id:** `TbQueue` — Casual=1, Ranked=2. `TbGameMode` — FlapWang=1, Dodgeball=2, ObserverAvoid=3, RememberGame=4, TargetShooting=5. `TbMap` — FlapWangMap=1.
- **`WaitingRoom`은 이름도 필드도 바꾸지 않는다.** 방은 *이미 결정된* 게임·맵을 들기 때문에 단수가 맞다. 삭제는 슬라이스 4.
- **빈 목록은 이 단계에선 에러다.** 현 대기방 알고리즘은 게임이 정해져야 정원을 알 수 있다. `gameModeIds[0]`/`mapIds[0]`을 쓰고 목록이 비면 명시적으로 던진다. "빈 목록 = 제한 없음"은 슬라이스 4 Director 소관.
- **Prisma `generated/`는 커밋하지 않는다**(루트 `.gitignore`의 `generated/`). 스키마를 바꾼 뒤에는 반드시 `pnpm --filter @lop/database generate`를 돌려야 TypeScript가 컴파일된다.
- **브랜치:** `feature/matchmaking-slice3-ticket-model`. main 직접 커밋 금지.
- **커밋 메시지는 한국어**로 쓰고 아래 trailer를 붙인다:
  `Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>`
- **주석 컨벤션:** 한국어, 비자명한 *의도(왜)* 만 짧게. 코드로 자명한 것은 주석 없이 둔다.

---

## File Structure

**`packages/database`**
- `prisma/schema.prisma` — 수정: `MatchmakingTicket`의 세 필드를 목록으로
- `prisma/migrations/<ts>_matchmaking_ticket_lists/migration.sql` — 신설(손질 SQL)

**`apps/matchmaking-server`**
- `src/interfaces/matchmakingTicket.interface.ts` — 도메인 타입
- `src/dtos/matchmakingTicket.dto.ts` — 생성·응답 DTO
- `src/factories/matchmakingTicket.factory.ts` — 기본값
- `src/models/matchmakingTicket.model.ts` — mongoose 스키마(미사용 변형이지만 타입 일관 유지)
- `src/mappers/entities/matchmakingTicket.mapper.ts` — 도메인 ↔ Prisma 엔티티
- `src/mappers/controllers/matchmakingTicket.mapper.ts` — DTO ↔ 도메인
- `src/mappers/entities/__tests__/matchmakingTicket.mapper.test.ts` — 신설
- `src/services/matchmakingTicket.service.ts` — 발급 시그니처
- `src/services/matchmaking.service.ts` — **단수 요청 → 목록 감싸기**가 일어나는 자리 + 취소 흐름
- `src/services/waitingRoom.service.ts` — 소비처 3곳(슬라이스 4에서 삭제될 코드)

**`apps/lobby-server`**
- `src/interfaces/matchmakingTicket.interface.ts` — 모양만 맞춤(필드를 읽는 코드는 없음)

---

## Task 1: DB 스키마 + 마이그레이션

**Files:**
- Modify: `lop-backend/packages/database/prisma/schema.prisma`
- Create: `lop-backend/packages/database/prisma/migrations/20260731000000_matchmaking_ticket_lists/migration.sql`

**Interfaces:**
- Consumes: 없음(첫 태스크)
- Produces: Prisma 타입 `MatchmakingTicket { id: string; userIds: string[]; queueId: number; gameModeIds: number[]; mapIds: number[]; rating: number; createdAt: Date }`. `creator`/`gameModeId`/`mapId`는 **더 이상 존재하지 않는다.**

- [ ] **Step 1: 브랜치 생성**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
git checkout main
git pull --ff-only origin main
git checkout -b feature/matchmaking-slice3-ticket-model
```

- [ ] **Step 2: `schema.prisma` 수정**

`packages/database/prisma/schema.prisma`의 `MatchmakingTicket` 모델을 아래로 교체한다. 다른 모델(`Match`, `MatchRound`, `WaitingRoom`, `UserStats` 등)은 **건드리지 않는다.**

```prisma
model MatchmakingTicket {
  id          String   @id @unique
  userIds     String[]
  queueId     Int
  gameModeIds Int[]
  mapIds      Int[]
  rating      Int
  createdAt   DateTime @default(now())
}
```

- [ ] **Step 3: 스키마 유효성 확인**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
pnpm --filter @lop/database exec prisma validate
```
기대 출력: `The schema at prisma\schema.prisma is valid 🚀`

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
mkdir -p "prisma/migrations/20260731000000_matchmaking_ticket_lists"
pnpm exec prisma migrate diff \
  --from-migrations ./prisma/migrations \
  --to-schema-datamodel ./prisma/schema.prisma \
  --shadow-database-url "postgresql://postgres:shadow@localhost:55432/postgres?schema=public" \
  --script > "prisma/migrations/20260731000000_matchmaking_ticket_lists/migration.sql"
cat "prisma/migrations/20260731000000_matchmaking_ticket_lists/migration.sql"
```
기대: `MatchmakingTicket`에서 `creator`/`gameModeId`/`mapId`를 DROP하고 `userIds`/`gameModeIds`/`mapIds`를 ADD하는 SQL.

- [ ] **Step 6: 마이그레이션 SQL 손질**

생성된 파일 **맨 위**에 아래를 넣는다. 티켓은 매칭 중에만 존재하는 일시 데이터라 버려도 되고, 남아 있으면 컬럼 교체가 어색해진다(옛 단수 값을 목록으로 옮길 의미가 없다 — 매칭 중이던 사람은 어차피 다시 눌러야 한다).

```sql
-- 티켓은 매칭 중에만 존재하는 일시 데이터다. 남겨 봐야 옛 단수 값을 목록으로 옮길 의미가 없다.
DELETE FROM "MatchmakingTicket";
```

**다른 테이블을 건드리는 구문이 섞여 있으면 지운다** — 이 마이그레이션은 `MatchmakingTicket`만 손대야 한다. 슬라이스 2의 마이그레이션이 이미 적용된 상태에서 뽑으므로 정상적으로는 안 나오지만, 나오면 스키마를 잘못 건드린 것이니 Step 2로 돌아간다.

- [ ] **Step 7: 빈 DB에 적용되는지 확인**

```bash
docker exec lop-shadow-db psql -U postgres -c "DROP SCHEMA public CASCADE; CREATE SCHEMA public;"
cd /c/Users/re5na/workspace/LOP/lop-backend/packages/database
DATABASE_URL="postgresql://postgres:shadow@localhost:55432/postgres?schema=public" pnpm exec prisma migrate deploy
```
기대 출력: `3 migrations found in prisma/migrations` + `All migrations have been successfully applied.`

- [ ] **Step 8: 스키마와 마이그레이션이 어긋나지 않는지 확인**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/packages/database
pnpm exec prisma migrate diff \
  --from-url "postgresql://postgres:shadow@localhost:55432/postgres?schema=public" \
  --to-schema-datamodel ./prisma/schema.prisma \
  --script
```
기대 출력: `-- This is an empty migration.` (차이 없음. 무언가 출력되면 Step 6의 손질이 스키마와 어긋난 것이다.)

- [ ] **Step 9: 옛 데이터가 있는 DB에서도 통과하는지 확인**

운영 DB에는 다른 테이블의 데이터(유저·전적)가 들어 있고, 티켓 행도 있을 수 있다. 그 상태에서 통과해야 한다.

```bash
docker exec lop-shadow-db psql -U postgres -c "DROP SCHEMA public CASCADE; CREATE SCHEMA public;"
cd /c/Users/re5na/workspace/LOP/lop-backend/packages/database
mv "prisma/migrations/20260731000000_matchmaking_ticket_lists" /tmp/slice3-migration
DATABASE_URL="postgresql://postgres:shadow@localhost:55432/postgres?schema=public" pnpm exec prisma migrate deploy
```
기대: `2 migrations found` + 성공(슬라이스 3 마이그레이션을 잠시 빼 뒀으므로).

옛 모양의 데이터를 심는다:
```bash
docker exec lop-shadow-db psql -U postgres -c "
INSERT INTO \"User\" (id, username, email, \"passwordHash\", \"updatedAt\")
VALUES ('u1', 'tester', 't@example.com', 'x', now());
INSERT INTO \"UserStats\" (id, \"userId\", \"queueId\", \"updatedAt\") VALUES ('s1', 'u1', 1, now());
INSERT INTO \"MatchmakingTicket\" (id, creator, \"queueId\", \"gameModeId\", \"mapId\", rating)
VALUES ('t1', 'u1', 1, 1, 1, 1000);
"
```
기대: `INSERT 0 1` × 3.

되돌려 놓고 적용한다:
```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/packages/database
mv /tmp/slice3-migration "prisma/migrations/20260731000000_matchmaking_ticket_lists"
DATABASE_URL="postgresql://postgres:shadow@localhost:55432/postgres?schema=public" pnpm exec prisma migrate deploy
docker exec lop-shadow-db psql -U postgres -c 'SELECT count(*) FROM "UserStats";'
docker exec lop-shadow-db psql -U postgres -c '\d "MatchmakingTicket"'
```
기대:
- 마이그레이션이 **에러 없이** 통과
- `UserStats` 1행 **그대로**(이 마이그레이션은 티켓만 건드려야 한다)
- `MatchmakingTicket`에 `userIds text[]`, `gameModeIds integer[]`, `mapIds integer[]`가 있고 `creator`/`gameModeId`/`mapId`는 없다

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
feat(db): 매칭 티켓이 유저·게임·맵을 목록으로 들도록

creator/gameModeId/mapId를 userIds/gameModeIds/mapIds로. 파티(여러 명)와
후보 목록("이 중 아무거나")을 담을 수 있는 모양이다. 실제로 그 의미를
쓰는 것은 Director를 세우는 다음 슬라이스이고, 지금은 저장 모양만 맞춘다.

티켓은 매칭 중에만 존재하는 일시 데이터라 마이그레이션에서 비운다 —
옛 단수 값을 목록으로 옮길 의미가 없다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: 티켓 모델 — 도메인·DTO·매퍼·팩토리

**Files:**
- Modify: `lop-backend/apps/matchmaking-server/src/interfaces/matchmakingTicket.interface.ts`
- Modify: `lop-backend/apps/matchmaking-server/src/dtos/matchmakingTicket.dto.ts`
- Modify: `lop-backend/apps/matchmaking-server/src/factories/matchmakingTicket.factory.ts`
- Modify: `lop-backend/apps/matchmaking-server/src/models/matchmakingTicket.model.ts`
- Modify: `lop-backend/apps/matchmaking-server/src/mappers/entities/matchmakingTicket.mapper.ts`
- Modify: `lop-backend/apps/matchmaking-server/src/mappers/controllers/matchmakingTicket.mapper.ts`
- Create: `lop-backend/apps/matchmaking-server/src/mappers/entities/__tests__/matchmakingTicket.mapper.test.ts`

**Interfaces:**
- Consumes: Task 1의 Prisma 타입 `MatchmakingTicket { id, userIds: string[], queueId: number, gameModeIds: number[], mapIds: number[], rating: number, createdAt: Date }`.
- Produces:
  - `interface MatchmakingTicket { id: string; userIds: string[]; queueId: number; gameModeIds: number[]; mapIds: number[]; rating: number; createdAt: Date }`
  - `CreateMatchmakingTicketDto { id: string; userIds: string[]; queueId: number; gameModeIds: number[]; mapIds: number[]; rating: number }`
  - `MatchmakingTicketResponseDto { id: string; userIds: string[]; queueId: number; gameModeIds: number[]; mapIds: number[]; rating: number }`
  - `MatchmakingTicketFactory.create(properties?: Partial<MatchmakingTicket>): MatchmakingTicket`

> **이 태스크가 끝나도 앱은 컴파일되지 않는다.** 소비처(`waitingRoom.service`, `matchmaking.service`, `matchmakingTicket.service`)가 아직 옛 필드를 참조하기 때문이고, 그것은 Task 3이 고친다. 이 태스크의 판정 기준은 **jest 테스트**이지 앱 전체 빌드가 아니다.

- [ ] **Step 1: 실패하는 테스트 작성**

`src/mappers/entities/__tests__/matchmakingTicket.mapper.test.ts` 생성:

```typescript
import { MatchmakingTicketMapper } from '@mappers/entities/matchmakingTicket.mapper';
import { MatchmakingTicketFactory } from '@factories/matchmakingTicket.factory';
import { MatchmakingTicket } from '@interfaces/matchmakingTicket.interface';

describe('MatchmakingTicketMapper', () => {
    const mapper = new MatchmakingTicketMapper();

    const domain: MatchmakingTicket = {
        id: 'ticket-1',
        userIds: ['user-1'],
        queueId: 1,
        gameModeIds: [1],
        mapIds: [1],
        rating: 1050,
        createdAt: new Date('2026-07-31T00:00:00.000Z'),
    };

    it('도메인의 목록을 그대로 엔티티로 옮긴다', () => {
        expect(mapper.toEntity(domain)).toEqual({
            id: 'ticket-1',
            userIds: ['user-1'],
            queueId: 1,
            gameModeIds: [1],
            mapIds: [1],
            rating: 1050,
            createdAt: new Date('2026-07-31T00:00:00.000Z'),
        });
    });

    it('엔티티를 도메인으로 되돌려도 목록이 유지된다', () => {
        const back = mapper.toDomain(mapper.toEntity(domain));

        expect(back).toEqual(domain);
    });

    it('원소가 여럿인 목록도 잘라내지 않는다', () => {
        const party = { ...domain, userIds: ['user-1', 'user-2'], gameModeIds: [1, 2, 3] };

        const entity = mapper.toEntity(party);

        expect(entity.userIds).toEqual(['user-1', 'user-2']);
        expect(entity.gameModeIds).toEqual([1, 2, 3]);
    });

    it('빈 목록도 그대로 통과시킨다 — 뜻을 해석하는 것은 매퍼의 일이 아니다', () => {
        const unrestricted = { ...domain, gameModeIds: [], mapIds: [] };

        const entity = mapper.toEntity(unrestricted);

        expect(entity.gameModeIds).toEqual([]);
        expect(entity.mapIds).toEqual([]);
    });
});

describe('MatchmakingTicketFactory', () => {
    it('기본값은 빈 목록이다', () => {
        const ticket = MatchmakingTicketFactory.create();

        expect(ticket.userIds).toEqual([]);
        expect(ticket.gameModeIds).toEqual([]);
        expect(ticket.mapIds).toEqual([]);
        expect(ticket.queueId).toBe(1);
    });

    it('넘긴 값이 기본값을 덮는다', () => {
        const ticket = MatchmakingTicketFactory.create({ userIds: ['u1'], gameModeIds: [2] });

        expect(ticket.userIds).toEqual(['u1']);
        expect(ticket.gameModeIds).toEqual([2]);
        expect(ticket.mapIds).toEqual([]);
    });
});
```

- [ ] **Step 2: 테스트가 실패하는지 확인**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/apps/matchmaking-server
pnpm test -- matchmakingTicket.mapper
```
기대: FAIL — 타입 에러(`userIds`가 `MatchmakingTicket`에 없음).

- [ ] **Step 3: 도메인 인터페이스 교체**

`src/interfaces/matchmakingTicket.interface.ts` 전체 교체:

```typescript
export interface MatchmakingTicket {
    id: string;
    userIds: string[];
    queueId: number;
    gameModeIds: number[];
    mapIds: number[];
    rating: number;
    createdAt: Date;
}
```

- [ ] **Step 4: DTO 교체**

`src/dtos/matchmakingTicket.dto.ts` 전체 교체:

```typescript
import { IsArray, IsNumber, IsString } from 'class-validator';
import { ResponseBase } from '@interfaces/responseBase.interface';

export class CreateMatchmakingTicketDto {
    @IsString()
    public id: string;

    @IsArray()
    @IsString({ each: true })
    public userIds: string[];

    @IsNumber()
    public queueId: number;

    @IsArray()
    @IsNumber({}, { each: true })
    public gameModeIds: number[];

    @IsArray()
    @IsNumber({}, { each: true })
    public mapIds: number[];

    @IsNumber()
    public rating: number;
}

export class MatchmakingTicketResponseDto {
    public id: string;
    public userIds: string[];
    public queueId: number;
    public gameModeIds: number[];
    public mapIds: number[];
    public rating: number;
}

export class GetMatchmakingTicketResponseDto implements ResponseBase {
    public code: number;
    public matchmakingTicket?: MatchmakingTicketResponseDto;
}
```

- [ ] **Step 5: 팩토리 교체**

`src/factories/matchmakingTicket.factory.ts` 전체 교체:

```typescript
import { MatchmakingTicket } from '@interfaces/matchmakingTicket.interface';
import { v4 } from 'uuid';

export class MatchmakingTicketFactory {
    public static create(properties?: Partial<MatchmakingTicket>): MatchmakingTicket {
        return { ...MatchmakingTicketFactory.createDefault(), ...properties };
    }

    private static createDefault(): MatchmakingTicket {
        return {
            id: v4(),
            userIds: [],
            queueId: 1,
            gameModeIds: [],
            mapIds: [],
            rating: 1000,
            createdAt: new Date(),
        };
    }
}
```

- [ ] **Step 6: mongoose 모델 교체**

`src/models/matchmakingTicket.model.ts`에서 세 필드를 배열로 바꾼다:

```typescript
    userIds: [String],
    queueId: Number,
    gameModeIds: [Number],
    mapIds: [Number],
```
(옛 `creator: String`, `gameModeId: Number`, `mapId: Number` 세 줄을 위 넷으로 교체. 나머지 필드와 `model(...)` 호출은 그대로.)

- [ ] **Step 7: 매퍼 두 개 교체**

`src/mappers/entities/matchmakingTicket.mapper.ts`의 `toDomain`/`toEntity` 본문에서 세 필드를 바꾼다:

```typescript
            userIds: entity.userIds,
            queueId: entity.queueId,
            gameModeIds: entity.gameModeIds,
            mapIds: entity.mapIds,
```
`toEntity` 쪽도 대칭으로 `userIds: domain.userIds,` / `gameModeIds: domain.gameModeIds,` / `mapIds: domain.mapIds,`. 나머지 메서드(`toDomains`/`toEntities`/`getEntityFieldName`/`toEntityValue`)는 그대로 둔다.

`src/mappers/controllers/matchmakingTicket.mapper.ts`도 같은 세 필드로 바꾼다:

```typescript
    static CreateMatchmakingTicketDto = class {
        public static toEntity(createMatchmakingTicketDto: CreateMatchmakingTicketDto): MatchmakingTicket {
            return MatchmakingTicketFactory.create({
                id: createMatchmakingTicketDto.id,
                userIds: createMatchmakingTicketDto.userIds,
                queueId: createMatchmakingTicketDto.queueId,
                gameModeIds: createMatchmakingTicketDto.gameModeIds,
                mapIds: createMatchmakingTicketDto.mapIds,
                rating: createMatchmakingTicketDto.rating
            });
        }
    };

    public static toMatchmakingTicketResponseDto(matchmakingTicket: MatchmakingTicket): MatchmakingTicketResponseDto {
        return {
            id: matchmakingTicket.id,
            userIds: matchmakingTicket.userIds,
            queueId: matchmakingTicket.queueId,
            gameModeIds: matchmakingTicket.gameModeIds,
            mapIds: matchmakingTicket.mapIds,
            rating: matchmakingTicket.rating
        };
    }
```

- [ ] **Step 8: 테스트 통과 확인**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/apps/matchmaking-server
pnpm test -- matchmakingTicket.mapper
```
기대: PASS (6 tests)

- [ ] **Step 9: 전체 jest도 여전히 통과하는지 확인**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/apps/matchmaking-server
pnpm test
```
기대: 3 suites / 16 tests PASS (기존 매퍼 4 + 로더 6 + 신규 6).

- [ ] **Step 10: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
git add apps/matchmaking-server/src
git commit -m "$(cat <<'EOF'
feat(matchmaking): 티켓 도메인·DTO·매퍼를 목록으로

매퍼는 목록의 뜻을 해석하지 않고 그대로 옮긴다 — 빈 목록이 무엇을
의미하는지는 매칭을 도는 쪽이 정할 일이다.

소비처는 아직 옛 필드를 참조해 앱 전체 빌드는 깨진 상태다(다음 커밋에서 해소).

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: 소비처 3곳 + 로비 인터페이스

**Files:**
- Modify: `lop-backend/apps/matchmaking-server/src/services/matchmakingTicket.service.ts`
- Modify: `lop-backend/apps/matchmaking-server/src/services/matchmaking.service.ts`
- Modify: `lop-backend/apps/matchmaking-server/src/services/waitingRoom.service.ts`
- Modify: `lop-backend/apps/lobby-server/src/interfaces/matchmakingTicket.interface.ts`

**Interfaces:**
- Consumes: Task 2의 `MatchmakingTicket`, `MatchmakingTicketFactory`.
- Produces: `MatchmakingTicketService.issueMatchmakingTicket(userIds: string[], queueId: number, gameModeIds: number[], mapIds: number[], rating: number): Promise<MatchmakingTicket>`

> **이 태스크가 앱 컴파일을 되살린다.** 시작 시점에 `npx tsc --noEmit`은 소비처 파일들에서 에러를 뿜고, 끝나면 0이어야 한다 — 그게 이 태스크의 판정 기준이다.

- [ ] **Step 1: 시작 상태 확인 (에러가 어디에 있는지 기록)**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/apps/matchmaking-server
npx tsc --noEmit
```
기대: `matchmakingTicket.service.ts`, `matchmaking.service.ts`, `waitingRoom.service.ts`에서 에러. 이 목록을 보고서에 적는다 — Step 6에서 0이 되는 것과 대조한다.

- [ ] **Step 2: 티켓 발급 시그니처를 목록으로**

`src/services/matchmakingTicket.service.ts`의 `issueMatchmakingTicket`를 교체한다:

```typescript
    public async issueMatchmakingTicket(userIds: string[], queueId: number, gameModeIds: number[], mapIds: number[], rating: number): Promise<MatchmakingTicket> {
        try {
            const matchmakingTicket = MatchmakingTicketFactory.create({
                userIds: userIds,
                queueId: queueId,
                gameModeIds: gameModeIds,
                mapIds: mapIds,
                rating: rating,
            });
            return await this.matchmakingTicketRepository.save(matchmakingTicket);
        } catch (error) {
            return Promise.reject(error);
        }
    }
```

- [ ] **Step 3: 요청 경계에서 단수를 목록으로 감싼다**

`src/services/matchmaking.service.ts`의 티켓 발급 호출을 교체한다. **여기가 "요청은 단수, 티켓은 목록"의 이음매**다 — 클라가 여러 개를 고를 수 있게 되면 이 감싸기가 사라진다.

```typescript
            //  클라는 아직 하나만 고를 수 있어 단수로 온다. 여러 개 고르기·랜덤은 로비 선택 UI가 생길 때.
            const matchmakingTicket = await this.matchmakingTicketService.issueMatchmakingTicket(
                [requestMatchmakingDto.userId],
                requestMatchmakingDto.queueId,
                [requestMatchmakingDto.gameModeId],
                [requestMatchmakingDto.mapId],
                targetRating
            );
```

같은 파일의 **취소 흐름**에서 `matchmakingTicket.creator`를 쓰는 두 곳을 바꾼다:

```typescript
            const getUserResponseDto = await this.userService.findUserById(matchmakingTicket.userIds[0]);
```
```typescript
            const userLocationDto = await this.userLocationService.getOrCreateUserLocationById(matchmakingTicket.userIds[0]);
```

> 파티가 생기면 취소는 *전원*의 위치를 정리해야 한다. 지금은 항상 1명이라 `[0]`으로 충분하고, 여러 명을 다루는 것은 파티 기능이 실제로 생길 때다. 이 사실을 코드에 주석으로 남기지 않는다 — 아직 없는 기능을 현재 주석에 섞지 않는다는 프로젝트 컨벤션 때문이다.

- [ ] **Step 4: 대기방 서비스 3곳**

`src/services/waitingRoom.service.ts`에서 `matchmakingTicket.creator`를 쓰는 **두 곳**(후보 방 탐색 루프 안, 그리고 `updateWaitingRoom` 안)을 각각 바꾼다:

```typescript
                        waitingPlayerIds.push(...matchmakingTicket.userIds);
```

그리고 방 생성 블록에서 게임·맵을 정하는 부분을 교체한다. 현재는 `matchmakingTicket.gameModeId` / `matchmakingTicket.mapId`를 직접 쓰고 있다:

```typescript
            if (waitingRoom === undefined) {
                //  대기방은 게임이 정해져야 정원을 알 수 있다. 후보 중 아무거나 고르는 것은 Director를 세울 때.
                if (matchmakingTicket.gameModeIds.length === 0 || matchmakingTicket.mapIds.length === 0) {
                    throw new Error(`Ticket has no gameMode/map candidate: ${matchmakingTicket.id}`);
                }

                const gameModeId = matchmakingTicket.gameModeIds[0];
                const mapId = matchmakingTicket.mapIds[0];

                const gameMode = getTables().TbGameMode.get(gameModeId);
                if (gameMode === undefined) {
                    throw new Error(`Unknown gameModeId: ${gameModeId}`);
                }

                const map = getTables().TbMap.get(mapId);
                if (map === undefined || map.gameModeId !== gameModeId) {
                    throw new Error(`Invalid mapId: ${mapId} for gameModeId: ${gameModeId}`);
                }

                waitingRoom = await this.createWaitingRoom(new CreateWaitingRoomDto(
                    matchmakingTicket.queueId,
                    gameModeId,
                    mapId,
                    matchmakingTicket.rating,
                    5,  //  ?
                    gameMode.minPlayers,
                    gameMode.maxPlayers
                ));
            }
```

> `CreateWaitingRoomDto`의 인자 순서와 나머지 인자(`rating`, `5`, `minPlayers`, `maxPlayers`)는 지금 코드 그대로다 — 실제 파일을 열어 현재 호출과 대조한 뒤 게임·맵 두 자리만 바꾼다.

- [ ] **Step 5: 로비 서버 인터페이스 모양 맞춤**

`apps/lobby-server/src/interfaces/matchmakingTicket.interface.ts` 전체 교체:

```typescript
export interface MatchmakingTicket {
    id: string;
    userIds: string[];
    queueId: number;
    gameModeIds: number[];
    mapIds: number[];
    rating: number;
    createdAt: Date;
}
```

> 로비 서버는 이 티켓의 *존재*만 확인하고(`user-location.service.ts`의 `!matchmakingTicket`) 필드를 읽지 않는다. 그래도 매칭 서버가 실제로 돌려주는 모양과 어긋나 있으면 다음에 읽는 사람이 속으므로 맞춰 둔다.

- [ ] **Step 6: 남은 옛 필드 참조가 없는지 확인**

**티켓의** 옛 필드만 찾아야 한다. `waitingRoom.gameModeId`/`waitingRoom.mapId`는 **남는 것이 정상**이고(방은 이미 결정된 게임·맵을 단수로 든다), `map.gameModeId`도 `TbMap`의 필드라 정상이다. 그래서 티켓 변수를 통한 접근만 본다:

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
grep -rnE "matchmakingTicket\.(creator|gameModeId|mapId)\b|ticket\.(creator|gameModeId|mapId)\b|creator:" apps/matchmaking-server/src apps/lobby-server/src --include=*.ts
```
기대 출력: 없음(빈 결과). 무언가 나오면 그 파일을 마저 고친다.

이어서 **남아 있어야 할 것**이 실수로 사라지지 않았는지도 본다:

```bash
grep -rn "waitingRoom.gameModeId\|waitingRoom.mapId" apps/matchmaking-server/src --include=*.ts
```
기대: 매치 생성 블록(`rounds`를 만드는 곳)에서 두 줄이 **그대로 나온다.** 여기가 비면 `WaitingRoom`을 잘못 건드린 것이다.

- [ ] **Step 7: 컴파일·빌드·테스트**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/apps/matchmaking-server
npx tsc --noEmit
cd /c/Users/re5na/workspace/LOP/lop-backend
pnpm build
pnpm test
```
기대: `tsc` **에러 0**(Step 1의 목록이 전부 사라짐), 4개 패키지 빌드 성공, matchmaking-server 3 suites / 16 tests + room-server 1 suite / 11 tests 전부 PASS.

- [ ] **Step 8: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
git add apps/matchmaking-server/src apps/lobby-server/src
git commit -m "$(cat <<'EOF'
refactor(matchmaking): 티켓 소비처를 목록에 맞춤

요청은 여전히 단수로 오고 matchmaking.service가 [값]으로 감싼다 —
클라가 여러 개를 고를 수 있게 되면 그 감싸기가 사라진다.

대기방은 게임이 정해져야 정원을 알 수 있어 후보의 첫 원소를 쓰고,
후보가 비어 있으면 명시적으로 던진다. 후보 중에서 고르는 일은
Director를 세우는 다음 슬라이스 몫이다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: 머지 · 배포 · E2E 검증

**Files:**
- Modify: `docs/ROADMAP.md` (클라 저장소 — 워크트리에서 편집 후 머지)

**Interfaces:**
- Consumes: Task 1~3의 모든 변경.
- Produces: 없음(마감).

- [ ] **Step 1: 머지 + 푸시**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
git checkout main
git merge --no-ff feature/matchmaking-slice3-ticket-model -m "Merge feature/matchmaking-slice3-ticket-model: 티켓 모델을 목록으로"
git push origin main
git rev-parse --short HEAD
```

> 클라·게임 서버는 이 슬라이스에서 바뀐 것이 없으므로 머지·배포 대상이 아니다.

- [ ] **Step 2: 배포 — `app: all` 한 번**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
gh workflow run backend-deploy.yml --repo Baeinsoo/lop-backend -f app=all
gh run list --workflow backend-deploy.yml --repo Baeinsoo/lop-backend --limit 1
```

> ⚠️ **반드시 `app: all` 한 번**이어야 한다. 마이그레이션은 db-migrate 이미지 안에 있고 그 Job은 ArgoCD PreSync 훅이다. 앱별로 따로 돌리면 db-migrate 태그가 안 올라가 PreSync가 옛 마이그레이션을 돌린다 → 새 코드가 `userIds`를 쿼리하는데 DB엔 `creator`뿐 → 전 요청 500.

워크플로 완료를 기다린다:
```bash
gh run watch <RUN_ID> --repo Baeinsoo/lop-backend --exit-status
```
기대: exit 0. 실패하면 `gh run view <RUN_ID> --repo Baeinsoo/lop-backend --log-failed`로 원인을 본다.

- [ ] **Step 3: 마이그레이션이 실제로 돌았는지 확인**

```bash
kubectl get jobs -n default | grep db-migrate
kubectl logs job/db-migrate -n default --tail=30
```
기대: `All migrations have been successfully applied.`

- [ ] **Step 4: 스키마와 데이터 확인**

```bash
PG=$(kubectl get pods -n default -o name | grep postgres | head -1)
kubectl exec -n default $PG -- psql -U postgres -c '\d "MatchmakingTicket"'
kubectl exec -n default $PG -- psql -U postgres -c 'SELECT "queueId", count(*) FROM "UserStats" GROUP BY 1 ORDER BY 1;'
```
기대:
- `MatchmakingTicket`에 `userIds text[]`, `gameModeIds integer[]`, `mapIds integer[]`
- `UserStats`는 **그대로**(queueId 1·2에 각각 유저 수만큼) — 이 마이그레이션이 티켓 말고 다른 것을 건드리지 않았다는 증거

- [ ] **Step 5: 파드가 새 이미지로 떴는지 확인**

```bash
kubectl get pods -n default -o custom-columns=NAME:.metadata.name,IMAGE:.spec.containers[0].image --no-headers | grep -E "matchmaking|lobby|room-server"
```
기대: 세 파드 전부 Step 1에서 얻은 git sha 태그. 파드가 옛 태그면 ArgoCD 동기화를 더 기다린다.

- [ ] **Step 6: E2E 검증 (2클라)**

클라는 이 슬라이스에서 바뀐 것이 없으므로 **재빌드 없이** 지금 에디터로 확인한다.

| | 확인 |
|---|---|
| 1 | 두 클라가 매칭되어 같은 매치로 묶인다 |
| 2 | 룸이 뜨고 두 클라가 입장한다 |
| 3 | 게임이 정상 진행된다 |
| 4 | 매칭 중 취소가 동작한다 — 취소 흐름이 `userIds[0]`으로 바뀌었으므로 이번에 꼭 눌러 볼 것 |

티켓이 실제로 목록으로 저장되는지 DB로 확인한다(매칭 대기 중에):
```bash
PG=$(kubectl get pods -n default -o name | grep postgres | head -1)
kubectl exec -n default $PG -- psql -U postgres -c 'SELECT id, "userIds", "queueId", "gameModeIds", "mapIds" FROM "MatchmakingTicket";'
```
기대: `userIds={<uuid>}`, `gameModeIds={1}`, `mapIds={1}` — 단수 요청이 원소 1개짜리 목록으로 감싸여 저장됐다는 증거.

- [ ] **Step 7: ROADMAP 갱신**

클라 저장소 워크트리(`C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/.claude/worktrees/docs+matchmaking-standardization`)에서 `docs/ROADMAP.md`의 매치메이킹 트랙 절에 있는 `▶ **다음 = 슬라이스 3**...` 줄을 아래로 교체한다:

```markdown
- ✅ **슬라이스 3 — 티켓 모델 확장 (백엔드 전용)** — 티켓이 `creator`/`gameModeId`/`mapId` 대신
  `userIds[]`/`gameModeIds[]`/`mapIds[]`를 든다. 클라와 게임 서버는 한 줄도 안 바뀌었다 — 클라는
  티켓의 `ticketId`만 쓰고 로비 서버는 존재만 확인하기 때문이다. 요청은 여전히 단수로 오고
  `matchmaking.service`가 `[값]`으로 감싼다(그 이음매는 클라가 여러 개를 고를 수 있게 되면 사라진다).
  **눈에 보이는 변화 0** — 슬라이스 4의 Director가 필요로 하는 저장 모양을 미리 갖추는 작업이다.
  대기방은 후보의 첫 원소를 쓰고 후보가 비면 던진다; "빈 목록 = 제한 없음"은 슬라이스 4에서 살아난다.
  plan `2026-07-30-matchmaking-slice3-ticket-model`, spec §8 "슬라이스 3 확정 사항".
- ▶ **다음 = 슬라이스 4**(Director 전환 — `Updater`/`WaitingRoom` 삭제, Director+MatchFunction+Evaluator 신설).
```

- [ ] **Step 8: 커밋 + 머지 + 푸시**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/.claude/worktrees/docs+matchmaking-standardization"
git add docs/ROADMAP.md
git commit -m "$(cat <<'EOF'
docs(roadmap): 슬라이스 3 완료 기록

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git merge --no-ff worktree-docs+matchmaking-standardization -m "Merge docs+matchmaking-standardization: 슬라이스 3 완료 기록"
git push origin main
```

---

## 이 슬라이스에서 하지 않는 것 (경계)

| 안 하는 것 | 어디서 |
|---|---|
| 클라·게임 서버 코드 (한 줄도 안 바뀐다) | — |
| `RequestMatchmakingDto`를 배열로 (클라에 랜덤·다중선택 UI가 없다) | E(로비 선택 UI) |
| "빈 목록 = 제한 없음"을 **실제로 해석**하는 것 | 슬라이스 4 |
| 후보 목록 중에서 고르기, 여러 티켓의 후보 교집합 | 슬라이스 4 |
| `WaitingRoom`·`Updater` 삭제, Director 신설 | 슬라이스 4 |
| 파티(여러 명) 실제 지원 — 취소 시 전원 위치 정리 등 | 파티 기능 착수 시 |
| `Location.WaitingRoom` → `Matchmaking` 개명 | 슬라이스 5 |
