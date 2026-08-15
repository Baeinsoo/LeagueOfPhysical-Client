# 매칭 실패 안내 — 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 매칭이 시간초과로 끝나면 "상대를 찾지 못했습니다" 안내가 뜨고, 유저가 취소해서 끝나면 안 뜬다.

**Architecture:** 매칭이 끝난 사유(`User`/`Timeout`)를 **유저 위치의 메모(`locationDetail`)** 에 실어 보낸다. 티켓의 생애·잠금·로비 자가치유는 하나도 건드리지 않는다. 사유를 쓰는 것은 **조건부 한 연산**(그 유저가 아직 그 티켓 때문에 매칭 중일 때만)이라, 그 사이 새 매칭을 시작한 사람을 튕기지 않는다. 클라는 이미 1초마다 위치를 폴링하므로 추가 조회 없이 사유를 받아, 시간초과일 때만 팝업을 띄운다.

**Tech Stack:** TypeScript / Node / Express / Prisma / class-validator (lop-backend, jest + ts-jest) · Unity C# / UniTask / R3 / VContainer / UI Toolkit (LeagueOfPhysical-Client)

**Spec:** `docs/superpowers/specs/2026-08-15-matchmaking-failure-notice-design.md` (클라 레포)

## Global Constraints

- **레포 2개 · 브랜치는 각 레포마다 따로.** 어떤 레포에서도 `main`에 직접 커밋하지 않는다.
  - `lop-backend` → 브랜치 `feature/matchmaking-failure-notice` (Task 1~5)
  - `LeagueOfPhysical-Client` → **이미 이 브랜치**(`feature/matchmaking-failure-notice`)에서 작업한다 (Task 6~8)
- **클라는 워크트리를 쓰지 않는다.** 연결된 Unity 에디터가 클라 레포의 **main 체크아웃 디렉터리**를 보므로, 워크트리에 짠 `Assets/` 코드는 컴파일 검증이 안 된다. 이 계획은 클라 main 체크아웃의 피처 브랜치에서 진행한다.
- **`.meta` 파일**: 새 `.cs`/`.uxml`/`.uss`를 만들면 Unity가 생성한 `.meta`를 **반드시 함께 커밋**한다. `.meta`를 직접 만들지 않는다.
- **백엔드 테스트는 빌드가 타입검사하지 않는다** — `apps/*/tsconfig.json`이 `__tests__`를 exclude한다. **검증 명령 맨 앞에 빌드를 둔다**: `pnpm --filter <app> build && pnpm --filter <app> test`.
- **사유 값은 정수 enum** — `User = 1`, `Timeout = 2`. 같은 응답의 `Location`이 정수 enum이라 표현을 섞지 않는다. 이름은 PlayFab `CancellationReason`에서 그대로 가져온다.
- **조건부 쓰기는 한 연산으로.** 읽고-판단하고-쓰기로 쪼개지 않는다(그 틈에 새 매칭이 끼면 유저를 튕긴다).
- **주석은 "왜"만, 일상어로** (`CLAUDE.md`). 코드로 자명한 것은 주석 없이 둔다.
- 답변·커밋 메시지·문서는 한국어.

---

## 파일 구조

| 파일 | 책임 | 태스크 |
|---|---|---|
| `lop-backend/packages/server-core/src/interfaces/user-location.interface.ts` (수정) | 사유 enum + `NoneLocationDetail`. 두 앱이 공유하는 계약 | 1 |
| `lop-backend/apps/lobby-server/src/daos/user-location.dao.postgres.ts` (수정) | 티켓 id 조건부 갱신 한 연산 | 2 |
| `lop-backend/apps/lobby-server/src/repositories/user-location.repository.ts` (수정) | 위 DAO를 도메인 타입으로 감쌈 | 2 |
| `lop-backend/apps/lobby-server/src/services/user-location.service.ts` (수정) | 해제 서비스 메서드 | 3 |
| `lop-backend/apps/lobby-server/src/dtos/user-location.dto.ts` (수정) | 해제 요청 DTO | 3 |
| `lop-backend/apps/lobby-server/src/controllers/user-location.controller.ts` (수정) | 해제 컨트롤러 | 3 |
| `lop-backend/apps/lobby-server/src/routes/internal.route.ts` (수정) | `PUT /internal/user/location/matchmaking-ended` | 3 |
| `lop-backend/apps/matchmaking-server/src/dtos/user-location.dto.ts` (수정) | 같은 DTO(보내는 쪽) | 4 |
| `lop-backend/apps/matchmaking-server/src/services/httpServices/lobbyServer.service.ts` (수정) | 로비 호출 | 4 |
| `lop-backend/apps/matchmaking-server/src/services/matchmaking.service.ts` (수정) | 취소 경로를 조건부 해제로 교체 | 4 |
| `lop-backend/apps/matchmaking-server/src/director/tick.ts` (수정) | 시간초과 경로에서 해제 호출 | 5 |
| `Assets/Scripts/Domain/CancellationReason.cs` (신규) | 클라 사유 enum | 6 |
| `Assets/Scripts/Domain/NoneLocationDetail.cs` (신규) | 클라 메모 타입 | 6 |
| `Assets/Scripts/WebAPI/Dto/Response/GetUserLocationResponse.Deserialize.cs` (수정) | `None` 분기 추가 | 6 |
| `Assets/Scripts/UI/Matchmaking/MatchmakingViewModel.cs` (수정) | `MatchmakingFailed` 도메인 신호 | 7 |
| `Assets/Scripts/UI/Matchmaking/MatchmakingFailedView.cs` (신규) | 팝업 View | 7 |
| `Assets/UI/.../MatchmakingFailedView.uxml` + `.uss` (신규) | 팝업 레이아웃 | 7 |
| `Assets/Scripts/UI/Matchmaking/MatchmakingCoordinator.cs` (수정) | 신호 구독 → 팝업 | 7 |
| `Assets/Scripts/UI/UIInstaller.cs` (수정) | 팝업 DI 등록 | 7 |
| `Assets/UI/UIViewCatalog.asset` (수정) | View 이름 → UXML 매핑 | 7 |
| `docs/ROADMAP.md` (수정) | 트랙 상태 갱신 | 8 |

---

### Task 1: 공유 계약 — 사유 enum과 `None` 메모 타입

**Files:**
- Modify: `lop-backend/packages/server-core/src/interfaces/user-location.interface.ts`

**Interfaces:**
- Produces: `enum CancellationReason { User = 1, Timeout = 2 }`, `class NoneLocationDetail extends LocationDetail { cancellationReason?: CancellationReason }`. Task 2~5가 전부 이 둘을 쓴다.

**작업 시작 전:** `lop-backend`에서 브랜치를 만든다.

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
git switch -c feature/matchmaking-failure-notice
```

- [ ] **Step 1: 계약을 추가한다**

`packages/server-core/src/interfaces/user-location.interface.ts`의 `MatchmakingLocationDetail` 아래에 붙인다.

```ts
/**
 * 매칭이 왜 끝났는지. 값 이름은 PlayFab CancellationReason을 따른다.
 * 지금 실재하는 둘만 둔다 — 늘릴 때도 그 표에서 고른다.
 */
export enum CancellationReason {
    User = 1,      //  유저가 취소함
    Timeout = 2,   //  큐 상한(ticket_ttl_seconds) 초과
}

/**
 * 아무 데도 속하지 않은 상태.
 * 직전 매칭이 어떻게 끝났는지를 아는 경우에만 사유가 붙는다 — 모르면 없다.
 */
export class NoneLocationDetail extends LocationDetail {
    cancellationReason?: CancellationReason;

    public constructor(cancellationReason?: CancellationReason) {
        super(Location.None);

        this.cancellationReason = cancellationReason;
    }
}
```

- [ ] **Step 2: 배럴에서 나가는지 확인한다**

Run:
```bash
cd /c/Users/re5na/workspace/LOP/lop-backend && grep -rn "user-location.interface" packages/server-core/src/index.ts packages/server-core/src/interfaces/index.ts 2>/dev/null
```
Expected: 이 인터페이스 파일이 배럴에서 re-export되고 있음(기존 `Location`/`LocationDetail`이 `@lop/server-core`로 import되고 있으므로 이미 나가는 상태다). 한 줄도 안 나오면 배럴에 `export * from './interfaces/user-location.interface';`를 추가한다.

- [ ] **Step 3: 빌드로 확인한다**

Run:
```bash
cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm --filter @lop/server-core build
```
Expected: 성공.

- [ ] **Step 4: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
git add packages/server-core/src/interfaces/user-location.interface.ts packages/server-core/src/index.ts
git commit -m "feat(server-core): 매칭 종료 사유 계약 추가

매칭이 왜 끝났는지를 유저 위치 메모에 실어 보내기 위한 타입.
값 이름은 PlayFab CancellationReason을 따르고, 인코딩은 같은 응답의
Location과 맞춰 정수로 둔다."
```

---

### Task 2: 로비 저장소 — 티켓 id 조건부 해제 (한 연산)

**Files:**
- Modify: `lop-backend/apps/lobby-server/src/daos/user-location.dao.postgres.ts` (`clearLocationIfUnchanged` 아래에 추가)
- Modify: `lop-backend/apps/lobby-server/src/repositories/user-location.repository.ts`
- Test: `lop-backend/apps/lobby-server/src/repositories/__tests__/user-location.repository.test.ts` (신규)

**Interfaces:**
- Consumes: Task 1의 `CancellationReason`, `NoneLocationDetail`
- Produces:
  - DAO `releaseMatchmakingIfTicketMatches(id: string, ticketId: string, noneDetailJson: string): Promise<number>`
  - Repository `releaseMatchmaking(userId: string, ticketId: string, reason: CancellationReason): Promise<boolean>` — Task 3이 부른다

- [ ] **Step 1: 실패하는 테스트를 쓴다**

새 파일 `apps/lobby-server/src/repositories/__tests__/user-location.repository.test.ts`:

```ts
import { CancellationReason, Location } from '@lop/server-core';

const releaseMatchmakingIfTicketMatches = jest.fn();

jest.mock('@daos/user-location.dao.postgres', () => ({
    __esModule: true,
    default: jest.fn(() => ({ releaseMatchmakingIfTicketMatches })),
}));

import UserLocationRepository from '@repositories/user-location.repository';

describe('UserLocationRepository.releaseMatchmaking', () => {
    beforeEach(() => jest.clearAllMocks());

    it('사유를 담은 None 메모로 해제를 요청한다', async () => {
        releaseMatchmakingIfTicketMatches.mockResolvedValue(1);

        const released = await new UserLocationRepository().releaseMatchmaking('U1', 'T1', CancellationReason.Timeout);

        expect(released).toBe(true);
        const [userId, ticketId, detailJson] = releaseMatchmakingIfTicketMatches.mock.calls[0];
        expect(userId).toBe('U1');
        expect(ticketId).toBe('T1');
        expect(JSON.parse(detailJson)).toEqual({ location: Location.None, cancellationReason: CancellationReason.Timeout });
    });

    //  조건이 안 맞았다 = 그 사이 유저가 다른 상태로 넘어갔다. 덮어쓰면 새 매칭 중인 사람을 튕긴다.
    it('갱신된 행이 없으면 false', async () => {
        releaseMatchmakingIfTicketMatches.mockResolvedValue(0);

        const released = await new UserLocationRepository().releaseMatchmaking('U1', 'T1', CancellationReason.User);

        expect(released).toBe(false);
    });

    //  티켓 id가 비면 조건이 사라져 그 유저의 위치를 무조건 덮게 된다.
    it('티켓 id가 비면 아무것도 하지 않는다', async () => {
        const released = await new UserLocationRepository().releaseMatchmaking('U1', '', CancellationReason.Timeout);

        expect(released).toBe(false);
        expect(releaseMatchmakingIfTicketMatches).not.toHaveBeenCalled();
    });
});
```

- [ ] **Step 2: 실패를 확인한다**

Run:
```bash
cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm --filter lobby-server test -- user-location.repository
```
Expected: FAIL — `releaseMatchmaking is not a function`.

- [ ] **Step 3: DAO에 조건부 갱신을 추가한다**

`apps/lobby-server/src/daos/user-location.dao.postgres.ts`의 `clearLocationIfUnchanged` **아래**에 추가한다(기존 메서드는 그대로 둔다 — 자가치유가 계속 쓴다).

```ts
    /**
     * "아직 이 티켓 때문에 매칭 중일 때만" 로비로 돌려놓는다.
     * 조건을 판본이 아니라 티켓 id로 거는 이유: 호출자(매칭서버)는 판본을 모르고, 우리가 실제로
     * 묻고 싶은 것이 "이 티켓 때문에 매칭 중이었나"이기 때문이다.
     */
    public async releaseMatchmakingIfTicketMatches(
        id: string,
        ticketId: string,
        noneDetailJson: string,
    ): Promise<number> {
        try {
            const { count } = await this.model.updateMany({
                where: {
                    id: id,
                    location: Entity.Location.Matchmaking,
                    locationDetail: { path: ['matchmakingTicketId'], equals: ticketId },
                },
                data: { location: Entity.Location.None, locationDetail: noneDetailJson, timestamp: new Date() },
            });
            return count;
        } catch (error) {
            return Promise.reject(error);
        }
    }
```

> `locationDetail`은 jsonb다. Prisma의 `path` 필터가 PostgreSQL에서 동작하지 않으면 **raw SQL 한 문장**(`UPDATE ... WHERE "locationDetail"->>'matchmakingTicketId' = $2`)으로 내려간다. **읽고-쓰기로 쪼개지 말 것** — 그 틈이 이 조건을 둔 이유 그 자체다.

- [ ] **Step 4: Repository에 래퍼를 추가한다**

`apps/lobby-server/src/repositories/user-location.repository.ts`의 `clearLocationIfUnchanged` 아래에 추가하고, 상단 import에 `CancellationReason`, `NoneLocationDetail`을 더한다.

```ts
    public async releaseMatchmaking(userId: string, ticketId: string, reason: CancellationReason): Promise<boolean> {
        try {
            //  티켓 id가 없으면 조건이 통째로 사라져 그 유저의 위치를 무조건 덮는다. 그럴 바엔 안 한다.
            if (!ticketId) {
                return false;
            }

            const count = await this.postgresDao.releaseMatchmakingIfTicketMatches(
                userId,
                ticketId,
                JSON.stringify(new NoneLocationDetail(reason)),
            );
            return count > 0;
        } catch (error) {
            return Promise.reject(error);
        }
    }
```

- [ ] **Step 5: 통과를 확인한다**

Run:
```bash
cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm --filter lobby-server build && pnpm --filter lobby-server test -- user-location.repository
```
Expected: 3건 PASS + 빌드 성공.

- [ ] **Step 6: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
git add apps/lobby-server/src/daos/user-location.dao.postgres.ts apps/lobby-server/src/repositories/user-location.repository.ts apps/lobby-server/src/repositories/__tests__/user-location.repository.test.ts
git commit -m "feat(lobby): 티켓 id 조건부로 매칭을 해제하는 경로

매칭이 끝난 사유를 위치 메모에 남기며 로비로 돌려놓는다. 조건이 없으면
그 사이 새 매칭을 시작한 사람의 위치를 덮어 대기 중에 튕긴다."
```

---

### Task 3: 로비 API — 내부 전용 해제 엔드포인트

**Files:**
- Modify: `lop-backend/apps/lobby-server/src/dtos/user-location.dto.ts`
- Modify: `lop-backend/apps/lobby-server/src/services/user-location.service.ts`
- Modify: `lop-backend/apps/lobby-server/src/controllers/user-location.controller.ts`
- Modify: `lop-backend/apps/lobby-server/src/routes/internal.route.ts`
- Test: `lop-backend/apps/lobby-server/src/services/__tests__/user-location.release.test.ts` (신규)

**Interfaces:**
- Consumes: Task 2의 `UserLocationRepository.releaseMatchmaking`
- Produces:
  - `class MatchmakingEndedDto { entries: MatchmakingEndedEntryDto[] }`, `class MatchmakingEndedEntryDto { userId: string; ticketId: string; reason: CancellationReason }`
  - `UserLocationService.releaseMatchmaking(dto: MatchmakingEndedDto): Promise<void>`
  - 라우트 `PUT /internal/user/location/matchmaking-ended` — Task 4가 부른다

- [ ] **Step 1: 실패하는 테스트를 쓴다**

새 파일 `apps/lobby-server/src/services/__tests__/user-location.release.test.ts`:

```ts
import { CancellationReason } from '@lop/server-core';

const releaseMatchmaking = jest.fn();

jest.mock('@repositories/user-location.repository', () => ({
    __esModule: true,
    default: jest.fn(() => ({ releaseMatchmaking })),
}));

import UserLocationService from '@services/user-location.service';

describe('UserLocationService.releaseMatchmaking', () => {
    beforeEach(() => {
        jest.clearAllMocks();
        releaseMatchmaking.mockResolvedValue(true);
    });

    it('항목마다 저장소를 부른다', async () => {
        await new UserLocationService().releaseMatchmaking({
            entries: [
                { userId: 'U1', ticketId: 'T1', reason: CancellationReason.Timeout },
                { userId: 'U2', ticketId: 'T1', reason: CancellationReason.Timeout },
            ],
        });

        expect(releaseMatchmaking).toHaveBeenCalledTimes(2);
        expect(releaseMatchmaking).toHaveBeenNthCalledWith(1, 'U1', 'T1', CancellationReason.Timeout);
        expect(releaseMatchmaking).toHaveBeenNthCalledWith(2, 'U2', 'T1', CancellationReason.Timeout);
    });

    //  한 명이 실패했다고 같은 배치의 나머지를 못 풀어주면 그 사람들이 대기 화면에 남는다.
    it('한 항목이 실패해도 나머지를 계속 처리한다', async () => {
        releaseMatchmaking
            .mockRejectedValueOnce(new Error('db down'))
            .mockResolvedValueOnce(true);

        await expect(new UserLocationService().releaseMatchmaking({
            entries: [
                { userId: 'U1', ticketId: 'T1', reason: CancellationReason.Timeout },
                { userId: 'U2', ticketId: 'T1', reason: CancellationReason.Timeout },
            ],
        })).resolves.toBeUndefined();

        expect(releaseMatchmaking).toHaveBeenCalledTimes(2);
    });

    it('빈 목록이면 아무것도 안 한다', async () => {
        await new UserLocationService().releaseMatchmaking({ entries: [] });

        expect(releaseMatchmaking).not.toHaveBeenCalled();
    });
});
```

- [ ] **Step 2: 실패를 확인한다**

Run:
```bash
cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm --filter lobby-server test -- user-location.release
```
Expected: FAIL — `releaseMatchmaking is not a function`.

- [ ] **Step 3: DTO를 추가한다**

`apps/lobby-server/src/dtos/user-location.dto.ts` 끝에 추가하고, 상단 import에 `CancellationReason`을 더한다.

```ts
export class MatchmakingEndedEntryDto {
    @IsString()
    public userId: string;

    @IsString()
    public ticketId: string;

    @IsEnum(CancellationReason)
    public reason: CancellationReason;
}

export class MatchmakingEndedDto {
    @IsArray()
    @ValidateNested({ each: true })
    @Type(() => MatchmakingEndedEntryDto)
    public entries: MatchmakingEndedEntryDto[] = [];
}
```

- [ ] **Step 4: 서비스에 메서드를 추가한다**

`apps/lobby-server/src/services/user-location.service.ts`의 `healIfStale` **위**(public 구역)에 추가한다. 상단 import에 `MatchmakingEndedDto`를 더한다.

```ts
    /**
     * 매칭이 끝난 사람들을 사유와 함께 로비로 돌려놓는다.
     * 한 명이 실패해도 던지지 않는다 — 같은 배치의 나머지가 대기 화면에 남으면 안 되고,
     * 실패해도 조회 시 자가치유가 (사유 없이) 받아준다.
     */
    public async releaseMatchmaking(dto: MatchmakingEndedDto): Promise<void> {
        for (const entry of dto.entries) {
            try {
                await this.userLocationRepository.releaseMatchmaking(entry.userId, entry.ticketId, entry.reason);
            } catch (error) {
                logger.error(`Failed to release matchmaking. userId: ${entry.userId}, ticketId: ${entry.ticketId}, error: ${error}`);
            }
        }
    }
```

파일 상단에 `logger` import가 없으면 추가한다:

```ts
import { logger } from '@lop/server-core/logger';
```

- [ ] **Step 5: 컨트롤러와 라우트를 잇는다**

`apps/lobby-server/src/controllers/user-location.controller.ts`에 추가한다(기존 컨트롤러 메서드의 시그니처·응답 모양을 그대로 따른다).

```ts
    public releaseMatchmaking = async (req: Request, res: Response, next: NextFunction) => {
        try {
            await this.userLocationService.releaseMatchmaking(req.body as MatchmakingEndedDto);

            res.status(200).json({ code: ResponseCode.SUCCESS });
        } catch (error) {
            next(error);
        }
    };
```

`apps/lobby-server/src/routes/internal.route.ts`의 기존 `PUT /internal/user/location` **아래**에 추가한다.

```ts
        this.router.put(
            `${this.path}/user/location/matchmaking-ended`,
            validationMiddleware(MatchmakingEndedDto, 'body'),
            this.userLocationController.releaseMatchmaking,
        );
```

> ⚠️ `router.use(this.path, internalApiKeyMiddleware)`가 이 등록보다 **위**에 있어야 키 검사가 걸린다. 기존 파일이 이미 그 순서이므로 **`use`보다 아래에** 추가할 것.

- [ ] **Step 6: 통과를 확인한다**

Run:
```bash
cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm --filter lobby-server build && pnpm --filter lobby-server test
```
Expected: 새 3건 + 기존 전부 PASS, 빌드 성공.

- [ ] **Step 7: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
git add apps/lobby-server/src
git commit -m "feat(lobby): 매칭 종료 해제 내부 엔드포인트

PUT /internal/user/location/matchmaking-ended — 매칭서버가 티켓이
끝났음을 알리면 조건부로 위치를 비우고 사유를 남긴다."
```

---

### Task 4: 매칭서버 — 취소 경로를 조건부 해제로 교체

**Files:**
- Modify: `lop-backend/apps/matchmaking-server/src/dtos/user-location.dto.ts`
- Modify: `lop-backend/apps/matchmaking-server/src/services/httpServices/lobbyServer.service.ts`
- Modify: `lop-backend/apps/matchmaking-server/src/services/matchmaking.service.ts` (`cancelMatchmaking`의 위치 갱신 부분)
- Test: `lop-backend/apps/matchmaking-server/src/services/__tests__/matchmakingCancelRelease.test.ts` (신규)

**Interfaces:**
- Consumes: Task 3의 `PUT /internal/user/location/matchmaking-ended`
- Produces: `LobbyServerService.notifyMatchmakingEnded(dto: MatchmakingEndedDto): Promise<void>` — Task 5가 그대로 쓴다

- [ ] **Step 1: 실패하는 테스트를 쓴다**

새 파일 `apps/matchmaking-server/src/services/__tests__/matchmakingCancelRelease.test.ts`:

```ts
import { CancellationReason, Location, ResponseCode } from '@lop/server-core';

const findMatchmakingTicketById = jest.fn();
const deleteMatchmakingTicketByIdIfUnconsumed = jest.fn();
const findUserById = jest.fn();
const getOrCreateUserLocationById = jest.fn();
const updateUserLocation = jest.fn();
const notifyMatchmakingEnded = jest.fn();

jest.mock('@services/matchmakingTicket.service', () => ({
    __esModule: true,
    default: jest.fn(() => ({ findMatchmakingTicketById, deleteMatchmakingTicketByIdIfUnconsumed })),
}));
jest.mock('@services/user.service', () => ({ __esModule: true, default: jest.fn(() => ({ findUserById })) }));
jest.mock('@services/user-location.service', () => ({
    __esModule: true,
    default: jest.fn(() => ({ getOrCreateUserLocationById, updateUserLocation, notifyMatchmakingEnded })),
}));

import MatchmakingService from '@services/matchmaking.service';

describe('MatchmakingService.cancelMatchmaking', () => {
    beforeEach(() => {
        jest.clearAllMocks();
        findMatchmakingTicketById.mockResolvedValue({ id: 'T1', userIds: ['U1'], matchId: null });
        deleteMatchmakingTicketByIdIfUnconsumed.mockResolvedValue(true);
        findUserById.mockResolvedValue({ user: { id: 'U1' } });
        getOrCreateUserLocationById.mockResolvedValue({ userLocation: { id: 'U1', location: Location.Matchmaking } });
        notifyMatchmakingEnded.mockResolvedValue(undefined);
    });

    it('취소 성공 시 User 사유로 해제를 알린다', async () => {
        const res = await new MatchmakingService().cancelMatchmaking('T1', 'U1');

        expect(res.code).toBe(ResponseCode.SUCCESS);
        expect(notifyMatchmakingEnded).toHaveBeenCalledWith({
            entries: [{ userId: 'U1', ticketId: 'T1', reason: CancellationReason.User }],
        });
    });

    //  조건 없는 갱신은 그 사이 새 매칭을 시작한 사람을 로비로 튕긴다.
    it('무조건 덮어쓰는 옛 경로를 쓰지 않는다', async () => {
        await new MatchmakingService().cancelMatchmaking('T1', 'U1');

        expect(updateUserLocation).not.toHaveBeenCalled();
    });

    it('티켓 삭제가 0건이면 해제를 알리지 않는다', async () => {
        deleteMatchmakingTicketByIdIfUnconsumed.mockResolvedValue(false);
        findMatchmakingTicketById.mockResolvedValue({ id: 'T1', userIds: ['U1'], matchId: null });

        await new MatchmakingService().cancelMatchmaking('T1', 'U1');

        expect(notifyMatchmakingEnded).not.toHaveBeenCalled();
    });
});
```

- [ ] **Step 2: 실패를 확인한다**

Run:
```bash
cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm --filter matchmaking-server test -- matchmakingCancelRelease
```
Expected: FAIL — `notifyMatchmakingEnded` 미호출(현 코드가 `updateUserLocation`을 부른다).

- [ ] **Step 3: DTO를 복사해 넣는다**

`apps/matchmaking-server/src/dtos/user-location.dto.ts` 끝에 추가한다(로비의 것과 **같은 모양**이어야 한다. 이 앱은 보내는 쪽이라 검증 데코레이터는 없어도 된다).

```ts
export class MatchmakingEndedEntryDto {
    public userId: string;
    public ticketId: string;
    public reason: CancellationReason;
}

export class MatchmakingEndedDto {
    public entries: MatchmakingEndedEntryDto[] = [];
}
```

상단 import에 `CancellationReason`을 더한다.

- [ ] **Step 4: 로비 호출을 추가한다**

`apps/matchmaking-server/src/services/httpServices/lobbyServer.service.ts`에 기존 `updateUserLocation`(44행 `/internal/user/location`) **바로 아래**에 같은 모양으로 추가한다.

```ts
    public async notifyMatchmakingEnded(dto: MatchmakingEndedDto): Promise<void> {
        try {
            const url = `http://${this.host}:${this.port}/internal/user/location/matchmaking-ended`;
            await this.httpService.put(url, dto);
        } catch (error) {
            return Promise.reject(error);
        }
    }
```

> 같은 파일의 기존 메서드가 쓰는 http 헬퍼·헤더 관례를 **그대로** 따를 것(내부 키 부착이 그 계층에 있다). 새 방식을 만들지 않는다.

`user-location.service.ts`(매칭서버 쪽 래퍼)가 로비 호출을 감싸고 있으면 같은 이름의 통과 메서드를 하나 더한다 — 위 테스트가 `@services/user-location.service`를 모킹하므로 그 경로에 `notifyMatchmakingEnded`가 있어야 한다.

- [ ] **Step 5: 취소 경로를 교체한다**

`apps/matchmaking-server/src/services/matchmaking.service.ts`의 `cancelMatchmaking`에서 **티켓 삭제 성공 이후**의 위치 갱신 블록을 교체한다.

교체 전(현재):
```ts
            //  update userMatchState
            const updateUserLocationDto: UpdateUserLocationDto = {
                userLocations: [{
                    userId: user.id,
                    location: Location.None,
                    locationDetail: { location: Location.None },
                }]
            };

            await this.userLocationService.updateUserLocation(updateUserLocationDto);
```

교체 후:
```ts
            //  조건부로 푼다 — 무조건 덮으면 그 사이 새 매칭을 시작한 사람을 대기 중에 로비로 튕긴다.
            await this.userLocationService.notifyMatchmakingEnded({
                entries: [{ userId: user.id, ticketId: ticketId, reason: CancellationReason.User }],
            });
```

- [ ] **Step 6: 통과를 확인한다**

Run:
```bash
cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm --filter matchmaking-server build && pnpm --filter matchmaking-server test
```
Expected: 새 3건 + 기존 전부 PASS, 빌드 성공.

- [ ] **Step 7: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
git add apps/matchmaking-server/src
git commit -m "feat(matchmaking): 취소 시 사유를 남기며 조건부로 해제한다

무조건 덮어쓰던 위치 갱신을 조건부 해제로 교체. 사유(User)가 위치 메모에
실려 클라가 취소와 실패를 구분할 수 있게 된다."
```

---

### Task 5: Director — 시간초과 티켓의 주인들을 풀어준다

**Files:**
- Modify: `lop-backend/apps/matchmaking-server/src/director/tick.ts` (쓸어담기 이후)
- Test: `lop-backend/apps/matchmaking-server/src/director/__tests__/tick.test.ts` (기존 파일에 describe 추가)

**Interfaces:**
- Consumes: Task 4의 `notifyMatchmakingEnded`, Task 1의 `CancellationReason`
- Produces: 틱 의존성에 `notifyMatchmakingEnded(dto: MatchmakingEndedDto): Promise<void>` 한 개 추가

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`apps/matchmaking-server/src/director/__tests__/tick.test.ts` 끝에 덧붙인다. **기존 파일이 틱 의존성을 어떻게 조립하는지 그대로 따를 것**(이 파일에 이미 `deps` 헬퍼가 있다 — 새로 만들지 말고 재사용하고, `notifyMatchmakingEnded`만 더한다).

```ts
describe('시간초과 티켓의 주인 해제', () => {
    it('쓸어담은 티켓의 유저들을 Timeout 사유로 한 번에 알린다', async () => {
        const notifyMatchmakingEnded = jest.fn().mockResolvedValue(undefined);
        //  상한을 넘긴 티켓 하나(2인). 기존 헬퍼가 만드는 큐 정책의 ticketTtlSeconds를 넘긴 createdAt.
        const expired = { id: 'T1', userIds: ['U1', 'U2'], queueId: 1, matchId: null, createdAt: new Date(0) };

        await runTick(makeDeps({ tickets: [expired], notifyMatchmakingEnded }));

        expect(notifyMatchmakingEnded).toHaveBeenCalledTimes(1);
        expect(notifyMatchmakingEnded).toHaveBeenCalledWith({
            entries: [
                { userId: 'U1', ticketId: 'T1', reason: CancellationReason.Timeout },
                { userId: 'U2', ticketId: 'T1', reason: CancellationReason.Timeout },
            ],
        });
    });

    it('쓸어담은 게 없으면 부르지 않는다', async () => {
        const notifyMatchmakingEnded = jest.fn().mockResolvedValue(undefined);

        await runTick(makeDeps({ tickets: [], notifyMatchmakingEnded }));

        expect(notifyMatchmakingEnded).not.toHaveBeenCalled();
    });

    //  로비가 죽어도 매칭은 계속 돌아야 한다. 못 푼 사람은 조회 시 자가치유가 (사유 없이) 받아준다.
    it('해제 알림이 실패해도 틱은 계속 돈다', async () => {
        const notifyMatchmakingEnded = jest.fn().mockRejectedValue(new Error('lobby down'));
        const expired = { id: 'T1', userIds: ['U1'], queueId: 1, matchId: null, createdAt: new Date(0) };

        await expect(runTick(makeDeps({ tickets: [expired], notifyMatchmakingEnded }))).resolves.toBeDefined();
    });
});
```

> `runTick`/`makeDeps`는 이 파일의 기존 헬퍼 이름에 맞춘다. 이름이 다르면 **기존 것을 그대로 쓰고 위 코드의 호출부만 바꾼다** — 새 하네스를 만들지 않는다.

- [ ] **Step 2: 실패를 확인한다**

Run:
```bash
cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm --filter matchmaking-server test -- tick
```
Expected: FAIL — `notifyMatchmakingEnded` 미호출.

- [ ] **Step 3: 틱에 배선한다**

`apps/matchmaking-server/src/director/tick.ts`의 의존성 인터페이스(35행 `deleteAbandonedTickets` 옆)에 추가한다.

```ts
    notifyMatchmakingEnded(dto: MatchmakingEndedDto): Promise<void>;
```

쓸어담기 성공 블록(현재 100~101행 `deleteAbandonedTickets` 직후, `sweptTicketIds`를 채우는 자리) 바로 뒤에 추가한다.

```ts
            //  티켓만 지우면 그 사람들은 로비가 위치를 조회할 때까지 대기 화면에 남고, 남아도
            //  "왜 끝났는지"를 모른다. 여기서 사유와 함께 풀어준다.
            const entries = swept.abandoned.flatMap(ticket =>
                ticket.userIds.map(userId => ({ userId: userId, ticketId: ticket.id, reason: CancellationReason.Timeout })),
            );

            try {
                await deps.notifyMatchmakingEnded({ entries: entries });
            } catch (error) {
                //  실패해도 던지지 않는다 — 매칭 루프가 멈추면 파드는 멀쩡한 채 매칭만 0건이 된다.
                //  못 푼 사람은 조회 시 자가치유가 (사유 없이) 받아준다.
                failures.push(`failed to notify matchmaking ended. ticketIds: ${swept.abandonedIds.join(',')}, error: ${error}`);
            }
```

이 코드는 티켓의 `userIds`가 필요하므로 `classifyAbandonedTickets`가 **id뿐 아니라 티켓 자체**도 돌려줘야 한다. `apps/matchmaking-server/src/director/abandonedTickets.ts`의 결과 타입에 한 필드를 더한다.

```ts
export interface AbandonedTicketResult {
    keep: MatchmakingTicket[];
    abandonedIds: string[];
    /** 버릴 티켓 자체. 주인들을 사유와 함께 풀어주려면 userIds가 필요하다. */
    abandoned: MatchmakingTicket[];
}
```

루프에서 `abandonedIds.push(ticket.id)` 옆에 `abandoned.push(ticket)`를 더하고, 반환 객체와 초기화(`const abandoned: MatchmakingTicket[] = []`)를 맞춘다.

- [ ] **Step 4: 통과를 확인한다**

Run:
```bash
cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm --filter matchmaking-server build && pnpm --filter matchmaking-server test
```
Expected: 전부 PASS(기존 `abandonedTickets.test.ts` 포함 — 반환 타입에 필드가 **추가**만 됐으므로 깨지지 않아야 한다), 빌드 성공.

- [ ] **Step 5: 전체 빌드와 테스트**

Run:
```bash
cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm build && pnpm test
```
Expected: 5개 패키지 빌드 + 전체 테스트 PASS.

- [ ] **Step 6: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
git add apps/matchmaking-server/src
git commit -m "feat(director): 시간초과 티켓의 주인들을 사유와 함께 풀어준다

지금은 티켓만 지우고 로비가 조회할 때까지 기다렸다. 이제 즉시 풀리고
Timeout 사유가 위치 메모에 남는다. 실패해도 틱은 계속 돈다."
```

---

### Task 6: 클라 — 사유를 받는 타입

**Files:**
- Create: `Assets/Scripts/Domain/CancellationReason.cs`
- Create: `Assets/Scripts/Domain/NoneLocationDetail.cs`
- Modify: `Assets/Scripts/WebAPI/Dto/Response/GetUserLocationResponse.Deserialize.cs`

**Interfaces:**
- Consumes: Task 1의 와이어 계약(정수 enum)
- Produces: `LOP.CancellationReason`, `LOP.NoneLocationDetail` — Task 7이 쓴다

> **테스트 없음(의도).** 클라 앱 코드는 전부 `Assembly-CSharp`이라 asmdef 참조가 불가능해 EditMode 유닛 테스트를 붙일 수 없다. 검증은 컴파일 + Task 8의 인게임이다.

- [ ] **Step 1: enum을 만든다**

`Assets/Scripts/Domain/CancellationReason.cs`:

```csharp
using System;

namespace LOP
{
    /// <summary>매칭이 왜 끝났는지. 값 이름·번호는 서버(@lop/server-core)와 일치해야 한다.</summary>
    [Serializable]
    public enum CancellationReason
    {
        None = 0,
        User = 1,
        Timeout = 2,
    }
}
```

- [ ] **Step 2: 메모 타입을 만든다**

`Assets/Scripts/Domain/NoneLocationDetail.cs`:

```csharp
using System;

namespace LOP
{
    [Serializable]
    public class NoneLocationDetail : LocationDetail
    {
        public CancellationReason cancellationReason;
    }
}
```

- [ ] **Step 3: 역직렬화에 분기를 더한다**

`GetUserLocationResponse.Deserialize.cs`의 switch에 `GameRoom` 분기 아래로 추가한다.

```csharp
                    case Location.None:
                        getUserLocationResponse.userLocation.locationDetail = locationDetail.ToObject<NoneLocationDetail>();
                        break;
```

- [ ] **Step 4: 컴파일을 확인한다**

Unity 에디터에서 컴파일 에러 0을 확인한다. UnityMCP를 쓸 경우 **`unity_instance`를 클라 인스턴스로 명시**한다(`mcpforunity://instances`에서 `LeagueOfPhysical-Client@<hash>` 확인). 에디터가 없으면 `Library/Bee` 응답 파일 + 번들 Roslyn으로 `Assets/Scripts` 범위만 컴파일한다.

Expected: 에러 0.

- [ ] **Step 5: 커밋 (.meta 포함)**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Domain/CancellationReason.cs Assets/Scripts/Domain/CancellationReason.cs.meta \
        Assets/Scripts/Domain/NoneLocationDetail.cs Assets/Scripts/Domain/NoneLocationDetail.cs.meta \
        Assets/Scripts/WebAPI/Dto/Response/GetUserLocationResponse.Deserialize.cs
git commit -m "feat(client): 매칭 종료 사유를 받는 타입

위치가 None일 때의 메모에 사유가 실려 온다. 역직렬화가 None 분기를
갖지 않아 지금은 베이스 타입으로 떨어지던 것을 함께 고친다."
```

---

### Task 7: 클라 — 신호와 팝업

**Files:**
- Modify: `Assets/Scripts/UI/Matchmaking/MatchmakingViewModel.cs`
- Create: `Assets/Scripts/UI/Matchmaking/MatchmakingFailedView.cs`
- Create: `Assets/UI/Matchmaking/MatchmakingFailedView.uxml`, `Assets/UI/Matchmaking/MatchmakingFailedView.uss`
- Modify: `Assets/Scripts/UI/Matchmaking/MatchmakingCoordinator.cs`
- Modify: `Assets/Scripts/UI/UIInstaller.cs`
- Modify: `Assets/UI/UIViewCatalog.asset` (에디터에서)

**Interfaces:**
- Consumes: Task 6의 `CancellationReason`/`NoneLocationDetail`, 기존 `IUserLocationService.UserLocation`
- Produces: `MatchmakingViewModel.MatchmakingFailed : Observable<CancellationReason>`

- [ ] **Step 1: ViewModel에 신호를 단다**

`MatchmakingViewModel.cs`를 고친다. 생성자에 `IUserLocationService`를 더하고, 필드·프로퍼티·발행 로직을 추가한다.

```csharp
        private readonly IUserLocationService _userLocationService;
        private readonly Subject<CancellationReason> _matchmakingFailed = new();

        /// <summary>매칭이 실패로 끝났다. 코디네이터가 구독해 안내를 띄운다(목적지는 VM이 모른다).</summary>
        public Observable<CancellationReason> MatchmakingFailed => _matchmakingFailed;

        public MatchmakingViewModel(
            MatchStateMachine matchStateMachine,
            IMatchmakingDataStore matchmakingDataStore,
            IUserLocationService userLocationService)
        {
            _matchStateMachine = matchStateMachine;
            _matchmakingDataStore = matchmakingDataStore;
            _userLocationService = userLocationService;
        }
```

`OnStateChange`를 교체한다.

```csharp
        private void OnStateChange(IState<MatchEvent> previous, IState<MatchEvent> current)
        {
            _isMatching.Value = current is InMatchmaking;

            //  대기를 벗어나는 순간에만 본다. FSM이 벗어나는 계기가 위치 변화 구독이므로,
            //  여기 도달했을 때 위치 값은 이미 새것(None + 사유)이다.
            if (previous is InMatchmaking && current is not InMatchmaking)
            {
                if (_userLocationService.UserLocation.CurrentValue.locationDetail is NoneLocationDetail detail
                    && detail.cancellationReason == CancellationReason.Timeout)
                {
                    _matchmakingFailed.OnNext(detail.cancellationReason);
                }
            }
        }
```

`Dispose`에 `_matchmakingFailed.Dispose();`를 더한다.

- [ ] **Step 2: 팝업 UXML/USS를 만든다**

`Assets/UI/Matchmaking/MatchmakingFailedView.uxml`:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement name="matchmaking-failed" class="mmf-root">
        <ui:VisualElement class="mmf-panel">
            <ui:Label name="mmf-message" class="mmf-message" text="상대를 찾지 못했습니다." />
            <ui:Button name="mmf-confirm" class="mmf-confirm" text="확인" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

`Assets/UI/Matchmaking/MatchmakingFailedView.uss`:

```css
.mmf-root {
    flex-grow: 1;
    align-items: center;
    justify-content: center;
}

.mmf-panel {
    min-width: 320px;
    padding: 24px;
    align-items: center;
    background-color: rgb(32, 34, 40);
    border-radius: 12px;
}

.mmf-message {
    margin-bottom: 20px;
    font-size: 20px;
    color: rgb(240, 240, 240);
    white-space: normal;
}

.mmf-confirm {
    min-width: 120px;
    height: 44px;
}
```

- [ ] **Step 3: View를 만든다**

`Assets/Scripts/UI/Matchmaking/MatchmakingFailedView.cs`:

```csharp
using System;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>매칭 실패 안내 팝업. 확인을 누르면 닫힌다.</summary>
    public class MatchmakingFailedView : UIPopup
    {
        private Button _confirmButton;

        /// <summary>확인 클릭. 코디네이터가 닫기를 배선한다(화면 교체는 View 책임이 아니다).</summary>
        public event Action Confirmed;

        public override void OnOpen()
        {
            base.OnOpen();

            _confirmButton = Root.Q<Button>("mmf-confirm");
            _confirmButton.clicked += OnConfirmClicked;
        }

        public override void OnClose()
        {
            if (_confirmButton != null)
            {
                _confirmButton.clicked -= OnConfirmClicked;
            }

            base.OnClose();
        }

        private void OnConfirmClicked() => Confirmed?.Invoke();
    }
}
```

- [ ] **Step 4: 코디네이터가 띄우게 한다**

`MatchmakingCoordinator.cs`를 고친다. 필드에 팝업 참조를 더하고, `Start`에서 신호를 구독한다.

```csharp
        private MatchmakingFailedView _failedView;
```

`Start`의 기존 구독을 이렇게 바꾼다(구독 두 개를 함께 들고 있어야 하므로 `CompositeDisposable`이 아니라 필드 두 개로 단순하게 둔다 — 이 클래스의 기존 방식과 맞춘다).

```csharp
        private IDisposable _matchingSubscription;
        private IDisposable _failedSubscription;

        public void Start()
        {
            // ReactiveProperty는 구독 즉시 현재값을 replay하므로 StartFlow 전에 구독해도 안전.
            _matchingSubscription = _viewModel.IsMatching.Subscribe(OnMatchingChanged);
            _failedSubscription = _viewModel.MatchmakingFailed.Subscribe(_ => ShowFailed());
            _viewModel.StartFlow();
        }
```

메서드를 추가한다.

```csharp
        private void ShowFailed()
        {
            //  연달아 실패해도 안내는 하나만 띄운다.
            if (_failedView != null)
            {
                return;
            }

            _failedView = _windowManager.Open<MatchmakingFailedView>();
            _failedView.Confirmed += CloseFailed;
        }

        private void CloseFailed()
        {
            if (_failedView == null)
            {
                return;
            }

            _failedView.Confirmed -= CloseFailed;
            _windowManager.Close(_failedView);
            _failedView = null;
        }
```

`Dispose`에서 두 구독과 팝업을 모두 정리한다.

```csharp
        public void Dispose()
        {
            _matchingSubscription?.Dispose();
            _failedSubscription?.Dispose();

            CloseFailed();

            if (_waitingView != null)
            {
                _windowManager.Close(_waitingView);
                _waitingView = null;
            }
        }
```

기존 `_subscription` 필드는 `_matchingSubscription`으로 이름이 바뀌었으므로 남은 참조가 없는지 확인한다.

- [ ] **Step 5: DI에 등록한다**

`Assets/Scripts/UI/UIInstaller.cs`의 `MatchingWaitingView` 등록 아래에 추가한다.

```csharp
            builder.Register<MatchmakingFailedView>(Lifetime.Transient);
```

- [ ] **Step 6: 카탈로그에 매핑을 넣는다 (에디터 작업)**

Unity 에디터에서 `Assets/UI/UIViewCatalog.asset`을 열고 Entries에 한 줄 추가한다.

| 필드 | 값 |
|---|---|
| `viewName` | `MatchmakingFailedView` |
| `uxml` | `Assets/UI/Matchmaking/MatchmakingFailedView.uxml` |
| `uss` | `Assets/UI/Matchmaking/MatchmakingFailedView.uss` |

> ⚠️ **이 줄이 없으면 팝업이 안 뜨고 콘솔에 `UIViewCatalog에 'MatchmakingFailedView' UXML 매핑이 없습니다`만 남는다.** `viewName`은 타입 이름과 **글자 그대로** 같아야 한다.

- [ ] **Step 7: 컴파일을 확인한다**

Unity 에디터에서 컴파일 에러 0을 확인한다(`unity_instance`를 클라로 명시).

Expected: 에러 0.

- [ ] **Step 8: 커밋 (.meta 포함)**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/UI/Matchmaking Assets/Scripts/UI/UIInstaller.cs Assets/UI/Matchmaking Assets/UI/UIViewCatalog.asset
git status --short   # .meta가 빠지지 않았는지 눈으로 확인
git commit -m "feat(client): 매칭 실패 안내 팝업

VM이 실패 신호만 노출하고 코디네이터가 팝업을 띄운다. 취소와 매치 성사는
사유가 없거나 User라 안내가 뜨지 않는다."
```

---

### Task 8: 통합 검증 + 문서 갱신

**Files:**
- Modify: `docs/ROADMAP.md` (유저 위치 트랙 표)

**Interfaces:**
- Consumes: Task 1~7 전부

- [ ] **Step 1: 백엔드를 로컬 클러스터에 올린다**

배포는 push다(ArgoCD GitOps). **마이그레이션이 없다** — 스키마 변경 없이 jsonb 안의 필드만 는다. 대상 앱은 **lobby-server와 matchmaking-server 둘 다**이고, `@lop/server-core`가 바뀌었으므로 **둘 다 다시 빌드돼야 한다**.

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend && git push -u origin feature/matchmaking-failure-notice
gh workflow run backend-deploy.yml -f app=all
kubectl get pods -w
```

⚠️ **막힌 롤아웃은 서비스를 안 죽이고 옛 버전이 계속 응답한다.** 동작이 그대로면 코드를 의심하기 전에 파드 이미지 태그부터 본다.

- [ ] **Step 2: 시간초과 안내 (이 슬라이스의 본 목적)**

상한을 짧게 만들어 재현한다. `TbQueue`의 `ticket_ttl_seconds`(현재 600)를 **30초**로 임시 조정하거나, DB에서 해당 티켓의 `createdAt`을 과거로 민다(후자가 빠르고 마스터데이터를 안 건드린다).

```bash
# 티켓을 11분 전에 만든 것으로 민다 (버려진 대기표 만료 검증에서 쓴 방법)
kubectl exec -it deploy/postgres -- psql -U <user> -d <db> \
  -c "UPDATE \"MatchmakingTicket\" SET \"createdAt\" = now() - interval '11 minutes';"
```

1. 클라 하나로 매칭 요청 → 대기 화면
2. 위 명령으로 티켓을 늙힌다
3. **다음 Director 틱(1초)에 대기 화면이 닫히고 "상대를 찾지 못했습니다" 팝업이 뜬다**
4. [확인] → 로비 → **새 매칭 요청이 정상 동작한다**(잠금이 제대로 풀렸다는 증거)

- [ ] **Step 3: 취소는 조용하다**

1. 매칭 요청 → 대기 화면
2. 취소 버튼
3. **팝업이 뜨지 않는다.** 로비로 조용히 복귀
4. 다시 매칭 요청이 정상 동작한다

- [ ] **Step 4: 매치 성사도 조용하다**

1. 클라 2개로 매칭 → 매치 성사
2. **팝업이 뜨지 않는다.** 로딩 → 게임 진입

- [ ] **Step 5: 조건부 쓰기가 실제로 막는지 (핵심 회귀 방지)**

시간초과 직후 **즉시** 새 매칭을 거는 상황을 만든다.

1. 클라 A로 매칭 요청
2. 티켓을 늙히고, 팝업이 뜨자마자 **바로** 다시 매칭 요청
3. **대기 화면이 유지된다** — 뒤늦게 도착한 해제가 새 매칭을 덮지 않는다
4. 로비 로그에 해제 0건(조건 불일치)이 보이면 정확히 그 동작이다

> ⚠️ 컨테이너 로그는 **UTC**다. 클라 로그와 시각을 비교하기 전에 `kubectl exec -- date -u`로 기준을 맞춘다.

- [ ] **Step 6: ROADMAP을 갱신한다**

`docs/ROADMAP.md`의 유저 위치 트랙 "다음에 할 것" 표에서 **"매칭 실패" 알림** 행을 완료로 바꾸고, 무엇을 했는지 한 줄(사유를 위치 메모에 실어 보냄 + Director가 즉시 풀어줌)과 spec/plan 링크를 남긴다. Done 원장에도 날짜와 한 줄을 더한다.

- [ ] **Step 7: 커밋 · 머지**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add docs/ROADMAP.md && git commit -m "docs(roadmap): 매칭 실패 안내 완료"
git switch main && git merge --no-ff feature/matchmaking-failure-notice

cd /c/Users/re5na/workspace/LOP/lop-backend
git switch main && git merge --no-ff feature/matchmaking-failure-notice && git push
```

머지 후 클라 Unity 에디터에서 컴파일 에러 0을 다시 확인한다.

- [ ] **Step 8: 메모리 갱신**

durable한 것을 남긴다: **"매칭이 왜 끝났는지는 출석부 메모에 실려 온다 — 티켓은 여전히 즉시 삭제되고, 티켓에 사유를 남기는 표준 방식은 잠금(유저당 티켓 1개)과 로비 자가치유 두 곳을 깨뜨려서 택하지 않았다."** 기존 `[[flow-slice-d-match-result]]`·`[[invariant-as-primary-key]]`와 이어 둔다.

---

## 자체 리뷰 결과

- **spec 커버리지**: §5 데이터 계약 → Task 1·6 / §6.1 조건부 해제 → Task 2·3 / §6.2 취소 → Task 4 / §6.3 시간초과 → Task 5 / §7 클라 배선 → Task 7 / §8 유실 허용 → Task 3·5의 "실패해도 안 던진다" 테스트 / §9 테스트 → 각 태스크 + Task 8. 빠진 요구사항 없음.
- **타입 일관성**: `CancellationReason`(Task 1 정의 → 2·3·4·5·6·7 소비), `NoneLocationDetail`(Task 1/6 각 사이드 정의 → 2·7 소비), `MatchmakingEndedDto`(Task 3 로비 정의 → 4 매칭서버 사본 → 5 소비), `releaseMatchmaking`(Task 2 repo → 3 service), `notifyMatchmakingEnded`(Task 4 정의 → 5 소비), `MatchmakingFailed`(Task 7 내부 정의·소비). 이름이 갈리는 곳 없음.
- **알려진 함정 반영**: 백엔드 테스트가 빌드 타입검사 밖(모든 검증 명령이 build 먼저) / `router.use`보다 아래에 라우트 등록 / `.meta` 커밋 / Unity 에디터는 main 체크아웃을 봄(워크트리 금지) / 막힌 롤아웃은 옛 버전이 응답 / 컨테이너 로그 UTC / `@lop/server-core` 변경이라 `app=all` 배포.
- **의도적으로 안 넣은 것**: 클라 EditMode 테스트(앱 코드가 `Assembly-CSharp`이라 불가 — Task 6·7에 명시), 팝업 문구 확정·자동 재매칭 버튼(spec §10 Open Decisions).
