# Runner Slice 2 — 상태 enum화 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Runner의 빈 마커 인터페이스 상태(`IGameState` + 마커 클래스 9개)를 typed `enum RunnerState`로 교체한다.

**Architecture:** `IGameState`와 클·서 `LOPGameState.cs`의 마커 클래스를 삭제하고, GameFramework에 `enum RunnerState { None, Initializing, Initialized, Playing, Paused, GameOver }`를 추가한다. `RunnerBase`/`IRunner`의 `gameState` 프로퍼티·`onGameStateChanged` 이벤트는 **이름 그대로 두고 타입만** `IGameState`→`RunnerState`로 바꾼다. 호출부(대입 5곳 × 2사이드, switch 3곳)를 enum 값으로 옮긴다. `None`이 기본(미초기화)값을 대체한다.

**Tech Stack:** Unity, C#, GameFramework 패키지(`namespace GameFramework`).

## Global Constraints

- **원자적 3-레포 변경**: `IGameState` 삭제는 모든 호출부가 enum으로 옮겨져야 컴파일된다. GF는 클·서 공유라, **모든 편집을 끝낸 뒤** 양쪽 에디터 컴파일을 검증하고 그다음 레포별로 커밋한다. 중간 상태는 컴파일 안 됨(정상).
- **멤버 이름 유지**: `gameState` 프로퍼티·`onGameStateChanged` 이벤트 **이름은 바꾸지 않는다**(타입만 교체). 이름 리팩터링은 Slice 6(네이밍) 몫.
- **가지치기**: `Preparing`/`Prepared`/`Error`는 만들지 않는다(한 번도 안 쓰임, YAGNI). `None`은 enum 0=기본값으로 유지.
- **커밋 스테이징은 명시된 파일만**. `git add -A`/`.` 금지. **특히 서버 레포엔 관련 없는 미커밋 로컬 픽스처**(`DefaultVolumeProfile.asset`, `ConfigureRoomComponent.cs`, `GameRuleSystem.cs`)가 있으니 절대 함께 커밋 금지 — `git add` 후 `git status --short`로 확인.
- **새 단위 테스트 없음**: enum + MonoBehaviour 프로퍼티라 EditMode 단위테스트 대상 아님(Slice 1 Task 2와 동일). 검증 = 양쪽 에디터 컴파일 그린 + GF EditMode 무회귀. 플레이 스모크는 컨트롤러/사람 몫.

## Repos & Branches (착수 전 생성됨)

- **GameFramework** `C:\Users\re5na\workspace\LOP\GameFramework` — 브랜치 `refactor/runner-slice2-state-enum` (base aea3107)
- **LeagueOfPhysical-Server** `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server` — 브랜치 `refactor/runner-slice2-state-enum` (base b5a356d)
- **LeagueOfPhysical-Client** — 이 워크트리 (`worktree-refactor-runner-slice2-state-enum`)

Unity: 클라 `LeagueOfPhysical-Client@de70658b9450cbb4`, 서버 `LeagueOfPhysical-Server@f99391fa2dbaaf3c`. GF 변경은 양쪽 에디터가 공유하므로 둘 다 컴파일 확인.

## File Structure

| 파일 | 작업 |
|---|---|
| GF `Runtime/Scripts/Game/RunnerState.cs` | Create (enum) |
| GF `Runtime/Scripts/Game/IGameState.cs` (+`.meta`) | Delete |
| GF `Runtime/Scripts/Game/IRunner.cs` | Modify (타입 2곳) |
| GF `Runtime/Scripts/Game/RunnerBase.cs` | Modify (타입 3곳) |
| Client `Assets/Scripts/Game/LOPGameState.cs` (+`.meta`) | Delete |
| Client `Assets/Scripts/Game/LOPRunner.cs` | Modify (대입 5곳) |
| Client `Assets/Scripts/Game/LOPGamePresenter.cs` | Modify (param + case 2곳) |
| Client `Assets/Scripts/Room/LOPRoom.cs` | Modify (param + case 1곳) |
| Server `Assets/Scripts/Game/LOPGameState.cs` (+`.meta`) | Delete |
| Server `Assets/Scripts/Game/LOPRunner.cs` | Modify (대입 5곳) |
| Server `Assets/Scripts/Room/LOPRoom.cs` | Modify (param + case 1곳) |

---

### Task 1: 상태 enum 마이그레이션 (원자적, 3-레포)

모든 편집을 먼저 하고, 그다음 컴파일 검증, 그다음 레포별 커밋. 순서 중요.

**Interfaces:**
- Produces: `public enum GameFramework.RunnerState { None, Initializing, Initialized, Playing, Paused, GameOver }`. `IRunner.gameState`는 `RunnerState`, `IRunner.onGameStateChanged`는 `event Action<RunnerState>`.

#### 편집 파트 A — GameFramework

- [ ] **Step 1: `RunnerState.cs` 생성**

Create `C:\Users\re5na\workspace\LOP\GameFramework\Runtime\Scripts\Game\RunnerState.cs`:

```csharp
namespace GameFramework
{
    /// <summary>
    /// Runner(호스트) 수명 상태. 행동 없는 "단계 라벨"이라 상태 플래그(enum)로 표현한다.
    /// None은 미초기화 기본값. 전이 규칙·상태별 행동은 없다(그건 매치/앱 FSM의 몫).
    /// </summary>
    public enum RunnerState
    {
        None,
        Initializing,
        Initialized,
        Playing,
        Paused,
        GameOver,
    }
}
```

- [ ] **Step 2: `IGameState.cs` 삭제** — `git rm`로 `.cs`+`.meta` 함께 (Step 8에서 실행). 지금은 표시만.

- [ ] **Step 3: `IRunner.cs` 타입 교체**

`C:\Users\re5na\workspace\LOP\GameFramework\Runtime\Scripts\Game\IRunner.cs`:
- `event Action<IGameState> onGameStateChanged;` → `event Action<RunnerState> onGameStateChanged;`
- `IGameState gameState { get; }` → `RunnerState gameState { get; }`

- [ ] **Step 4: `RunnerBase.cs` 타입 교체**

`C:\Users\re5na\workspace\LOP\GameFramework\Runtime\Scripts\Game\RunnerBase.cs`:
- `public event Action<IGameState> onGameStateChanged;` → `public event Action<RunnerState> onGameStateChanged;`
- `private IGameState _gameState;` → `private RunnerState _gameState;`
- `public IGameState gameState` → `public RunnerState gameState`

(setter의 `if (_gameState == value)` 값 비교는 enum에 그대로 유효. 초기값은 자동으로 `RunnerState.None`.)

#### 편집 파트 B — Client (이 워크트리)

- [ ] **Step 5: `LOPGameState.cs` 삭제** — Step 9에서 `git rm`. 지금 표시만.

- [ ] **Step 6: Client `LOPRunner.cs` 대입 5곳**

`Assets/Scripts/Game/LOPRunner.cs` (워크트리 루트 기준):
- `gameState = Initializing.State;` → `gameState = RunnerState.Initializing;`
- `gameState = Initialized.State;` → `gameState = RunnerState.Initialized;`
- `gameState = Playing.State;` → `gameState = RunnerState.Playing;`
- `gameState = Paused.State;` → `gameState = RunnerState.Paused;`
- `gameState = GameOver.State;` → `gameState = RunnerState.GameOver;`

- [ ] **Step 7: Client `LOPGamePresenter.cs`**

`Assets/Scripts/Game/LOPGamePresenter.cs`:
- `private void OnGameStateChanged(IGameState gameState)` → `private void OnGameStateChanged(RunnerState gameState)`
- `case Initialized:` → `case RunnerState.Initialized:`
- `case Playing:` → `case RunnerState.Playing:`

- [ ] **Step 8: Client `LOPRoom.cs`**

`Assets/Scripts/Room/LOPRoom.cs`:
- `private void OnGameStateChanged(IGameState gameState)` → `private void OnGameStateChanged(RunnerState gameState)`
- `case GameOver:` → `case RunnerState.GameOver:`

#### 편집 파트 C — Server

- [ ] **Step 9: Server `LOPRunner.cs` 대입 5곳**

`C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server\Assets\Scripts\Game\LOPRunner.cs`:
- `gameState = Initializing.State;` → `gameState = RunnerState.Initializing;`
- `gameState = Initialized.State;` → `gameState = RunnerState.Initialized;`
- `gameState = Playing.State;` → `gameState = RunnerState.Playing;`
- `gameState = Paused.State;` → `gameState = RunnerState.Paused;`
- `gameState = GameOver.State;` → `gameState = RunnerState.GameOver;`

- [ ] **Step 10: Server `LOPRoom.cs`**

`C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server\Assets\Scripts\Room\LOPRoom.cs`:
- `private void OnGameStateChanged(IGameState gameState)` → `private void OnGameStateChanged(RunnerState gameState)`
- `case GameOver:` → `case RunnerState.GameOver:`

#### 삭제 실행 (git rm — .cs+.meta 함께)

- [ ] **Step 11: 마커/인터페이스 파일 삭제**

```bash
git -C "C:/Users/re5na/workspace/LOP/GameFramework" rm Runtime/Scripts/Game/IGameState.cs Runtime/Scripts/Game/IGameState.cs.meta
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/.claude/worktrees/refactor-runner-slice2-state-enum" rm Assets/Scripts/Game/LOPGameState.cs Assets/Scripts/Game/LOPGameState.cs.meta
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" rm Assets/Scripts/Game/LOPGameState.cs Assets/Scripts/Game/LOPGameState.cs.meta
```

#### 컴파일 검증 (양쪽 에디터)

- [ ] **Step 12: 클라 에디터 컴파일**

`refresh_unity(mode="force", scope="scripts", compile="request", unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")` → `isCompiling==false` 대기 → `read_console(types=["error"], unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")` 에러 0. (이 리프레시가 새 `RunnerState.cs`의 `.meta`도 생성.)

- [ ] **Step 13: 서버 에디터 컴파일**

`refresh_unity(mode="force", scope="scripts", compile="request", unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")` → 대기 → `read_console(types=["error"], unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")` 에러 0.

- [ ] **Step 14: GF EditMode 무회귀**

`run_tests(mode="EditMode", unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")` → 전체 그린(비동기면 `get_test_job` 폴링). (RunnerState.cs.meta가 Step 12에서 생성됐는지 확인 — 없으면 refresh 재실행.)

#### 커밋 (레포별, 명시 파일만)

- [ ] **Step 15: GF 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/GameFramework"
git add Runtime/Scripts/Game/RunnerState.cs Runtime/Scripts/Game/RunnerState.cs.meta \
        Runtime/Scripts/Game/IRunner.cs Runtime/Scripts/Game/RunnerBase.cs
# (IGameState 삭제는 Step 11의 git rm으로 이미 스테이징됨)
git status --short   # 예상: A RunnerState.cs(+meta), M IRunner.cs, M RunnerBase.cs, D IGameState.cs(+meta)
git commit -m "refactor(runner): IGameState 마커 → enum RunnerState

빈 마커 인터페이스 + 마커 클래스 9개를 typed enum으로 교체.
멤버 이름(gameState/onGameStateChanged)은 유지(타입만 변경, 이름은 Slice 6).
미사용값(Preparing/Prepared/Error) 가지치기, None=기본값.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 16: Client 커밋 (이 워크트리)**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/.claude/worktrees/refactor-runner-slice2-state-enum"
git add Assets/Scripts/Game/LOPRunner.cs Assets/Scripts/Game/LOPGamePresenter.cs Assets/Scripts/Room/LOPRoom.cs
# (LOPGameState 삭제는 Step 11에서 스테이징됨)
git status --short   # 예상: M LOPRunner.cs, M LOPGamePresenter.cs, M LOPRoom.cs, D LOPGameState.cs(+meta)
git commit -m "refactor(runner): 클라 상태 대입/switch를 RunnerState enum으로

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 17: Server 커밋 — ⚠️ 픽스처 제외, 명시 파일만**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git add Assets/Scripts/Game/LOPRunner.cs Assets/Scripts/Room/LOPRoom.cs
# (LOPGameState 삭제는 Step 11에서 스테이징됨)
git status --short   # 예상: M LOPRunner.cs, M LOPRoom.cs, D LOPGameState.cs(+meta)
#   그리고 3개 픽스처(DefaultVolumeProfile.asset, ConfigureRoomComponent.cs, GameRuleSystem.cs)는
#   여전히 ' M'(unstaged)여야 함. 스테이징됐으면 git restore --staged로 빼고 다시 확인.
git commit -m "refactor(runner): 서버 상태 대입/switch를 RunnerState enum으로

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

## 완료 후 (통합 검증 & 머지)

- [ ] 최종 whole-branch 리뷰(3-레포 결합 diff).
- [ ] `finishing-a-development-branch`로 GF·Server·Client 브랜치 `--no-ff` 머지, 워크트리 정리. 서버 픽스처 미커밋 보존 확인.

## 스코프 밖 (의도적)

- `gameState`→`state`, `onGameStateChanged`→`onStateChanged` 등 **멤버 이름 변경 = Slice 6(네이밍)**.
- `Preparing`/`Prepared`/`Error` 상태 = 필요해질 때 추가(YAGNI).
- 나머지 findings(A/B/E/F/G/H/I) = 각자 슬라이스.
