# 매치메이킹 슬라이스 5 — 개명 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 대기방(waiting room) 시절의 이름을 매칭 어휘로 통일한다 — `Location.WaitingRoom` → `Matchmaking`, `WaitingRoomLocationDetail` → `MatchmakingLocationDetail`, FSM 상태 `InWaitingRoom` → `InMatchmaking`, 죽은 응답 코드 제거.

**Architecture:** 순수 개명이다. **동작 변화 0** — `Location` enum의 정수값(`Matchmaking = 1`)이 그대로라 와이어 포맷이 바뀌지 않는다. 대기방이라는 *기능*은 슬라이스 4b에서 이미 사라졌고, 이 슬라이스는 그 잔재 *이름*만 걷어낸다.

**Tech Stack:** TypeScript 5.7 / Prisma 6 / Unity C# / pnpm workspace

## Global Constraints

- **순수 개명이다. 동작을 바꾸지 마라.** 로직 개선·구조 변경·리팩터를 곁들이지 마라. 이름과 그에 딸린 참조만 바꾼다.
- **`Location` enum의 정수값을 바꾸지 마라** — `None = 0`, `Matchmaking = 1`, `GameRoom = 2`. 값이 바뀌면 클·서 와이어가 깨진다.
- **`MatchmakingViewModel`의 하드코딩(`queueId`/`gameModeId`/`mapId`)은 건드리지 마라.** 그 제거는 로비 선택 UI(별도 spec §11-E) 몫이다. 이 파일에서 바뀌는 것은 `InWaitingRoom` → `InMatchmaking` 참조뿐이다.
- Unity 파일 개명은 **`.cs`와 `.meta`를 함께 `git mv`** 한다. `.meta`를 새로 만들거나 지우지 마라 — GUID가 바뀌면 에셋 참조가 깨진다.
- 주석은 **한국어**로.
- 새 의존성 금지.

---

## File Structure

| 저장소 | 파일 | 하는 일 |
|---|---|---|
| lop-backend | `packages/database/prisma/schema.prisma` + 새 마이그레이션 | DB enum 값 개명 |
| lop-backend | `apps/{lobby,matchmaking,room}-server/src/interfaces/user-location.interface.ts` | enum 멤버 + 클래스 개명 |
| lop-backend | `apps/lobby-server/src/{mappers/entities/user.location.mapper.ts, services/user-location.service.ts}` | 참조 |
| lop-backend | `apps/matchmaking-server/src/services/matchmaking.service.ts` + 테스트 2개 | 참조 |
| LeagueOfPhysical-Client | `Assets/Scripts/` 11파일 (2개는 파일명 개명) | enum·클래스·상태·이벤트·응답코드 |
| LeagueOfPhysical-Server | `Assets/Scripts/WebAPI/ResponseCode.cs` | 죽은 응답 코드 제거 |

---

## Task 1: 백엔드 개명 + DB enum 마이그레이션

**Files:**
- Modify: `packages/database/prisma/schema.prisma`
- Create: `packages/database/prisma/migrations/20260731120000_rename_location_matchmaking/migration.sql`
- Modify: `apps/lobby-server/src/interfaces/user-location.interface.ts`
- Modify: `apps/lobby-server/src/mappers/entities/user.location.mapper.ts`
- Modify: `apps/lobby-server/src/services/user-location.service.ts`
- Modify: `apps/matchmaking-server/src/interfaces/user-location.interface.ts`
- Modify: `apps/matchmaking-server/src/services/matchmaking.service.ts`
- Modify: `apps/matchmaking-server/src/services/__tests__/matchmaking.service.cancel.test.ts`
- Modify: `apps/matchmaking-server/src/services/__tests__/matchmakingCancelPolicy.test.ts`
- Modify: `apps/room-server/src/interfaces/user-location.interface.ts`

**Interfaces:**
- Consumes: (없음)
- Produces: `Location.Matchmaking` (값 1), `MatchmakingLocationDetail { matchmakingTicketId }`

- [ ] **Step 1: 무엇을 바꿔야 하는지 먼저 센다**

Run (repo 루트 `C:/Users/re5na/workspace/LOP/lop-backend`에서):
```bash
grep -rn "WaitingRoom" apps/*/src packages/database/prisma/schema.prisma
```
Expected: 위 Files 목록의 파일들만 나온다. 목록 밖의 파일이 나오면 **멈추고 보고한다.**

- [ ] **Step 2: prisma enum을 바꾼다**

`packages/database/prisma/schema.prisma`의 `enum Location`에서 `WaitingRoom`을 `Matchmaking`으로 바꾼다. **순서를 바꾸지 마라** (`None`, `Matchmaking`, `GameRoom`):

```prisma
enum Location {
  None
  Matchmaking
  GameRoom
}
```

- [ ] **Step 3: 마이그레이션을 손으로 쓴다**

이 저장소는 마이그레이션을 **손으로 작성한다**(`prisma migrate dev` 금지 — 로컬 `.env`도 셰도 DB도 없다).
새 폴더 `packages/database/prisma/migrations/20260731120000_rename_location_matchmaking/`에 `migration.sql`:

```sql
-- 대기방이 사라졌으므로(슬라이스 4b) 이 값의 뜻은 이미 "매칭 풀에서 대기 중"이다. 이름만 맞춘다.
-- RENAME VALUE는 기존 행의 값을 그대로 따라가므로 데이터 이관이 필요 없다.
ALTER TYPE "Location" RENAME VALUE 'WaitingRoom' TO 'Matchmaking';
```

> 폴더 타임스탬프는 직전(`20260731110000_matchmaking_ticket_consumed`)보다 커야 한다.
> **DB에 적용하지 마라** — 배포 시 ArgoCD PreSync Job이 한다.

- [ ] **Step 4: TS enum과 클래스를 바꾼다 — 3개 앱 모두**

`apps/{lobby-server,matchmaking-server,room-server}/src/interfaces/user-location.interface.ts` 각각에서:

```typescript
export enum Location {
    None = 0,
    Matchmaking = 1,
    GameRoom = 2,
}
```

그리고 `WaitingRoomLocationDetail` 클래스를 `MatchmakingLocationDetail`로 개명한다(본문은 그대로 — 이미 필드가 `matchmakingTicketId` 하나다). 클래스 주석에서 "(클래스·enum 이름은 슬라이스 5에서 Matchmaking으로 개명한다)"라는 예고 문구가 있으면 **지운다** — 이제 그 일이 끝났다.

- [ ] **Step 5: 나머지 참조를 따라 바꾼다**

`Location.WaitingRoom` → `Location.Matchmaking`, `WaitingRoomLocationDetail` → `MatchmakingLocationDetail`,
그리고 그 타입을 담던 지역 변수명(`waitingRoomLocationDetail` 등)도 새 이름에 맞춘다. 대상:
`user.location.mapper.ts`, `user-location.service.ts`(lobby), `matchmaking.service.ts`, 테스트 2개.

> `Entity.Location.WaitingRoom`(prisma 생성 타입)도 `Entity.Location.Matchmaking`이 된다.

- [ ] **Step 6: 남은 흔적이 없는지 확인**

Run:
```bash
grep -rn "WaitingRoom\|waitingRoom" apps/*/src packages/database/prisma/schema.prisma
```
Expected: 출력 없음

- [ ] **Step 7: 생성·빌드·테스트**

Run:
```bash
pnpm --filter @lop/database run generate && pnpm --filter matchmaking-server run build && pnpm --filter lobby-server run build && pnpm --filter room-server run build && pnpm --filter matchmaking-server test
```
Expected: 전부 성공, 154 tests 통과

- [ ] **Step 8: 커밋**

```bash
git add -A apps packages
git commit -m "refactor(location): WaitingRoom -> Matchmaking 개명 (동작 변화 없음)"
```

---

## Task 2: 클라이언트 개명

**Files:**
- Rename: `Assets/Scripts/Domain/WaitingRoomLocationDetail.cs` → `MatchmakingLocationDetail.cs` (+ `.meta`)
- Rename: `Assets/Scripts/Matchmaking/MatchStateMachine/States/InWaitingRoom.cs` → `InMatchmaking.cs` (+ `.meta`)
- Modify: `Assets/Scripts/Domain/Location.cs`
- Modify: `Assets/Scripts/Lobby/LobbyLifetimeScope.cs`
- Modify: `Assets/Scripts/Matchmaking/MatchStateMachine/MatchEvent.cs`
- Modify: `Assets/Scripts/Matchmaking/MatchStateMachine/States/{CancelMatchmaking,CheckMatch,RequestMatchmaking}.cs`
- Modify: `Assets/Scripts/UI/Matchmaking/MatchmakingViewModel.cs`
- Modify: `Assets/Scripts/WebAPI/Dto/Response/GetUserLocationResponse.Deserialize.cs`
- Modify: `Assets/Scripts/WebAPI/ResponseCode.cs`

**Interfaces:**
- Consumes: Task 1이 백엔드에서 같은 이름으로 바꿨다(와이어는 정수라 무관하지만 어휘를 맞춘다)
- Produces: (없음 — 클라 내부 개명)

> **⚠️ 이 태스크는 컴파일 검증이 불가능하다.** 연결된 Unity 에디터는 워크트리가 아니라 **main 체크아웃**을 본다. 그래서 여기서는 **텍스트 정확성**(전수 grep으로 잔재 0)까지만 확인하고, 진짜 컴파일 확인은 머지 후 컨트롤러가 에디터에서 한다. 컴파일러 대신 grep이 안전망이니 **Step 8의 확인을 대충 하지 마라.**

- [ ] **Step 1: 파일 두 개를 개명한다 (`.meta` 포함)**

Run (`C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/.claude/worktrees/docs+matchmaking-standardization`에서):

```bash
git mv Assets/Scripts/Domain/WaitingRoomLocationDetail.cs Assets/Scripts/Domain/MatchmakingLocationDetail.cs
git mv Assets/Scripts/Domain/WaitingRoomLocationDetail.cs.meta Assets/Scripts/Domain/MatchmakingLocationDetail.cs.meta
git mv Assets/Scripts/Matchmaking/MatchStateMachine/States/InWaitingRoom.cs Assets/Scripts/Matchmaking/MatchStateMachine/States/InMatchmaking.cs
git mv Assets/Scripts/Matchmaking/MatchStateMachine/States/InWaitingRoom.cs.meta Assets/Scripts/Matchmaking/MatchStateMachine/States/InMatchmaking.cs.meta
```

Expected: 에러 없음. `.meta` 파일 내용은 **열지도 고치지도 마라** — GUID가 그대로여야 한다.

- [ ] **Step 2: `Location` enum**

`Assets/Scripts/Domain/Location.cs`:

```csharp
    [Serializable]
    public enum Location
    {
        None = 0,
        Matchmaking = 1,
        GameRoom = 2,
    }
```

- [ ] **Step 3: `MatchmakingLocationDetail`**

`Assets/Scripts/Domain/MatchmakingLocationDetail.cs`의 클래스명을 바꾸고 **죽은 필드를 지운다**
(백엔드가 슬라이스 4b부터 `waitingRoomId`를 보내지 않는다):

```csharp
    [Serializable]
    public class MatchmakingLocationDetail : LocationDetail
    {
        public string matchmakingTicketId;
    }
```

- [ ] **Step 4: `MatchEvent`**

`Assets/Scripts/Matchmaking/MatchStateMachine/MatchEvent.cs`에서 `LocationIsWaitingRoom` →
`LocationIsMatchmaking`으로 바꾼다(열거 순서·다른 멤버는 그대로).

- [ ] **Step 5: FSM 상태 개명**

`InMatchmaking.cs`에서 클래스명 `InWaitingRoom` → `InMatchmaking`, 생성자명도 함께.
`Location.WaitingRoom` → `Location.Matchmaking`.

`CheckMatch.cs` / `RequestMatchmaking.cs`에서 타입·필드·인자명을 따라 바꾼다:
`Func<InWaitingRoom> inWaitingRoom` → `Func<InMatchmaking> inMatchmaking`,
`MatchEvent.LocationIsWaitingRoom` → `MatchEvent.LocationIsMatchmaking`,
`Location.WaitingRoom => MatchEvent.LocationIsWaitingRoom` → `Location.Matchmaking => MatchEvent.LocationIsMatchmaking`.

`CancelMatchmaking.cs`:

```csharp
            if (userDataStore.userLocation.locationDetail is not MatchmakingLocationDetail matchmakingLocationDetail)
            {
                Debug.LogError("User is not in matchmaking.");
                return MatchEvent.RecheckRequested;
            }

            var cancelMatchmaking = await WebAPI.CancelMatchmaking(matchmakingLocationDetail.matchmakingTicketId);
```

- [ ] **Step 6: 등록·뷰모델·역직렬화**

`Assets/Scripts/Lobby/LobbyLifetimeScope.cs`: `RegisterState<InWaitingRoom>(builder);` → `RegisterState<InMatchmaking>(builder);`

`Assets/Scripts/UI/Matchmaking/MatchmakingViewModel.cs`: `current is InWaitingRoom` → `current is InMatchmaking`.
**이 파일에서 그 외에는 아무것도 바꾸지 마라** — 하드코딩은 로비 선택 UI 슬라이스 몫이다.

`Assets/Scripts/WebAPI/Dto/Response/GetUserLocationResponse.Deserialize.cs`:

```csharp
                    case Location.Matchmaking:
                        getUserLocationResponse.userLocation.locationDetail = locationDetail.ToObject<MatchmakingLocationDetail>();
                        break;
```

- [ ] **Step 7: 죽은 응답 코드 제거**

`Assets/Scripts/WebAPI/ResponseCode.cs`에서 아래 블록 전체를 지운다(백엔드는 4b에서 이미 지웠다):

```csharp
        #region WaitingRoom
        public const int WAITING_ROOM_NOT_EXIST = 40000;
        public const int FAIL_TO_LEAVE_WAITING_ROOM = 40001;
        #endregion
```

- [ ] **Step 8: 잔재 확인 (컴파일러 대신인 안전망 — 꼼꼼히)**

Run:
```bash
grep -rn "WaitingRoom\|waitingRoom\|WAITING_ROOM" Assets/Scripts/
```
Expected: **출력 없음**

Run:
```bash
git status --short Assets/
```
Expected: 개명 2쌍(`R` 4건: `.cs`+`.meta` × 2)과 수정 9건만. `.meta`가 `D`(삭제)나 `??`(신규)로 잡히면 **잘못된 것이다** — 되돌리고 `git mv`로 다시 하라.

- [ ] **Step 9: 커밋**

```bash
git add -A Assets
git commit -m "refactor(location): WaitingRoom -> Matchmaking 개명 (클라, 동작 변화 없음)"
```

---

## Task 3: 게임 서버 죽은 응답 코드 제거

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/WebAPI/ResponseCode.cs`

**Interfaces:**
- Consumes: (없음)
- Produces: (없음)

> 이 저장소는 **별도 git 저장소**다(`LeagueOfPhysical-Server`). 자기 피처 브랜치에서 작업하고 `--no-ff`로 main에 머지한다.

- [ ] **Step 1: 아무도 안 쓰는지 확인**

Run (`C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server`에서):
```bash
grep -rn "WAITING_ROOM_NOT_EXIST\|FAIL_TO_LEAVE_WAITING_ROOM" Assets/
```
Expected: `ResponseCode.cs`의 선언 2줄만. 다른 사용처가 나오면 **멈추고 보고한다.**

- [ ] **Step 2: 피처 브랜치**

```bash
git checkout -b feature/matchmaking-slice5-rename
```

- [ ] **Step 3: 블록 제거**

`Assets/Scripts/WebAPI/ResponseCode.cs`에서 아래를 통째로 지운다:

```csharp
        #region WaitingRoom
        public const int WAITING_ROOM_NOT_EXIST = 40000;
        public const int FAIL_TO_LEAVE_WAITING_ROOM = 40001;
        #endregion
```

- [ ] **Step 4: 확인**

Run: `grep -rn "WAITING_ROOM\|WaitingRoom" Assets/Scripts/`
Expected: 출력 없음

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/WebAPI/ResponseCode.cs
git commit -m "refactor(location): 죽은 WaitingRoom 응답 코드 제거"
```

---

## 머지·배포·검증 (사람이 수행 — 서브에이전트 아님)

> 태스크가 아니다. 모든 태스크와 최종 리뷰가 끝난 뒤 **컨트롤러가 직접** 수행한다.

### 컴파일 검증 (이 슬라이스의 진짜 관문)

1. 클라 워크트리 브랜치를 **main에 머지**한다 (에디터가 main 체크아웃을 보기 때문).
2. 게임 서버 피처 브랜치를 main에 머지한다.
3. **UnityMCP로 클·서 양쪽 콘솔을 확인**한다 — `unity_instance`를 각각 명시해서
   (`LeagueOfPhysical-Client@<hash>` / 서버 인스턴스). 컴파일 에러 0을 확인한다.
   - 인스턴스 id는 `mcpforunity://instances`에서 이름으로 찾는다.
   - 필요하면 `refresh_unity`로 재스캔.

### 배포 전 조건

- 유저 위치가 `Matchmaking`(=옛 `WaitingRoom`)인 행 **0개** — DB enum 개명과 앱 롤아웃 사이에
  옛 파드가 옛 값을 쓰면 실패한다. 대기자가 없으면 무해하다.
- 대기 티켓 0

### 배포

1. `lop-backend` 머지 후 push → `backend-deploy`를 **`app: all`** 로 실행
   (마이그레이션이 있으므로 `db-migrate`가 반드시 포함돼야 한다)
2. ArgoCD 롤아웃 확인

### E2E

- [ ] 클라 2대 매칭 → 입장 → 게임 진행
- [ ] 매칭 취소 정상
- [ ] DB `UserLocation.location`이 `Matchmaking`으로 저장되는지 확인 (enum 개명이 실제로 먹었다는 증거)
