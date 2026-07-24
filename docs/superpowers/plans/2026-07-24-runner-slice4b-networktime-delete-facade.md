# Runner Slice 4b — NetworkTime 주입 + static Runner 삭제 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`).

**Goal:** static `Runner` facade(`current`/`NetworkTime`)를 제거한다. `INetworkTime`을 DI 등록해 `LOPRunner`가 주입받아 `LOPTickUpdater`에 넘기고, DebugHud·is-running 가드를 주입 `IRunner`로 옮기며, static `Runner` 클래스와 `CreateNetworkTime` 훅을 통째로 삭제한다.

**Architecture:** `IRunner.networkTime`(과 `tickUpdater`)는 유지하고, 그 값의 **출처**를 `CreateNetworkTime()`(하드코딩 `new`)에서 **DI 주입**으로 바꾼다. `LOPTickUpdater`는 컨테이너 주입을 받지 않는 씬 컴포넌트라(그리고 클라의 `[Inject] LeadState`가 잠복 미주입 상태 — 이를 깨우면 넷코드 동작이 바뀜) `[SceneInject]` 대신 **`LOPRunner`가 주입받은 `INetworkTime`을 sibling `LOPTickUpdater`에 직접 세팅**(hand-off)한다. is-running(`Runner.current != null`)은 `runner.gameState`로 판정한다.

**Tech Stack:** Unity, C#, VContainer, GameFramework.

## Global Constraints

- **동작 변화 최소**: 순수 배선. **LeadState 잠복 미주입 상태 유지**(LOPTickUpdater를 `[SceneInject]`하지 말 것 — hand-off만). is-running 매핑은 enum 순서로 **정확히 등가**(아래).
- **원자적 3-레포 변경**: static `Runner` 삭제는 모든 `Runner.*` 참조가 옮겨져야 컴파일. 모든 편집 후 양쪽 에디터 컴파일 검증 → 레포별 커밋.
- **커밋 스테이징 명시 파일만**. `git add -A`/`.` 금지. **서버 픽스처**(`DefaultVolumeProfile.asset`, `ConfigureRoomComponent.cs`, `GameRuleSystem.cs`) 커밋 금지 — `git status --short` 확인.
- **클라 컴파일은 워크트리서 불가**(에디터 main 바인딩) → 정적 검증 + 머지 후 main 에디터 게이트. 이 워크트리는 로컬 main 기준으로 리셋됨(최신).
- **새 단위 테스트 없음**(배선) — 검증 = 양쪽 컴파일 + GF EditMode + **머지 후 플레이 스모크 권장**(넷코드 인접).

## Repos & Branches (생성됨)

- GameFramework `C:\Users\re5na\workspace\LOP\GameFramework` — `refactor/runner-slice4b` (base 7351387)
- Server `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server` — `refactor/runner-slice4b` (base 33864da)
- Client — 이 워크트리 `worktree-slice4b`(로컬 main 리셋됨)

Unity: 클라 `LeagueOfPhysical-Client@de70658b9450cbb4`, 서버 `LeagueOfPhysical-Server@f99391fa2dbaaf3c`.

## is-running 등가 매핑 (정확)

`Runner.current`는 `Run()`에서 세팅, `DeinitializeAsync`에서만 해제 → `current != null` ⟺ **Run 이후**(gameState ∈ {Playing, Paused, GameOver}). enum 선언 순서가 수명 순(None=0<Initializing<Initialized<Playing<Paused<GameOver)이라:
- `Runner.current == null` → `runner.gameState < RunnerState.Playing`
- `Runner.current != null` → `runner.gameState >= RunnerState.Playing`

---

### Task 1: NetworkTime 주입 + static Runner 삭제 (원자적, 3-레포)

모든 편집 → 컴파일 검증 → 레포별 커밋.

#### 파트 A — GameFramework

- [ ] **Step 1: `RunnerBase.cs` 편집**
  - `public INetworkTime networkTime { get; private set; }` → `{ get; protected set; }` (하위 클래스가 세팅).
  - `InitializeAsync`에서 `networkTime = CreateNetworkTime();` 줄 **삭제**(networkTime은 이제 LOPRunner가 세팅).
  - `Run(...)`에서 `Runner.current = this;` 줄 **삭제**.
  - `DeinitializeAsync`에서 아래 블록 **삭제**:
    ```csharp
    // teardown 시점에 정적 current를 정리한다 (Stop()은 일시정지라 current를 유지).
    if (Runner.current == this)
    {
        Runner.current = null;
    }
    ```
    (`networkTime = null; tickUpdater = null;` 등 나머지는 유지.)
  - 파일 하단 `protected virtual INetworkTime CreateNetworkTime() => null;` 메서드 + 그 위 `/// <summary>...` 문서주석 **삭제**.

- [ ] **Step 2: `Runner.cs` 삭제** — Step 8의 `git rm`으로 `.cs`+`.meta` 함께.

#### 파트 B — Client (이 워크트리)

- [ ] **Step 3: `Game/LOPRunner.cs`**
  - `[Inject]` 필드 목록에 추가: `[Inject] private INetworkTime networkTimeSource;` (`using GameFramework.Netcode;` 이미 있음).
  - `protected override INetworkTime CreateNetworkTime() => new MirrorNetworkTime();` 줄 **삭제**.
  - `InitializeAsync`의 `await base.InitializeAsync();` **바로 다음 줄**에 hand-off 추가:
    ```csharp
    networkTime = networkTimeSource;
    ((LOPTickUpdater)tickUpdater).networkTime = networkTimeSource;
    ```

- [ ] **Step 4: `Netcode/LOPTickUpdater.cs`**
  - 클래스에 필드 추가: `public GameFramework.Netcode.INetworkTime networkTime;`
  - `OnElapsedTimeUpdate`에서 `Runner.NetworkTime.predictedTime` → `networkTime.PredictedTime`.

- [ ] **Step 5: `Game/MessageHandler/GameEntityMessageHandler.cs`**
  - 생성자에 `IRunner runner` 파라미터 추가 + `private readonly IRunner runner;` 필드 + 대입.
  - `OnEntitySnapsToC`의 가드 교체: `if (Runner.current == null)` → `if (runner.gameState < RunnerState.Playing)`.

- [ ] **Step 6: `UI/DebugHud/DebugHudViewModel.cs`**
  - 생성자에 `IRunner runner` 추가 + `private readonly IRunner runner;` 필드 + 대입.
  - getter 교체:
    ```csharp
    public bool IsRunning => runner.gameState >= RunnerState.Playing;
    public long Tick => runner.tickUpdater.tick;
    public double ElapsedTime => runner.tickUpdater.elapsedTime;
    public double RttMs => runner.networkTime.Rtt * 1000;
    public long ServerTickEstimate => (long)(runner.networkTime.ServerNow / runner.tickUpdater.interval);
    public long Lead => runner.tickUpdater.tick - ServerTickEstimate;
    ```

- [ ] **Step 7: 클라 `Game/GameLifetimeScope.cs`** — `INetworkTime` 등록 추가(넷코드 블록 근처, 예: `LeadState` 등록 줄 인근):
  ```csharp
  builder.Register<GameFramework.Netcode.INetworkTime, MirrorNetworkTime>(Lifetime.Singleton);
  ```
  (`DebugHudViewModel`은 이미 `Register<DebugHudViewModel>` 됨 — `IRunner`가 스코프에 등록돼 있어 새 ctor 파라미터가 자동 주입됨.)

#### 파트 C — Server

- [ ] **Step 9(서버 LOPRunner): `Game/LOPRunner.cs`**
  - `[Inject] private INetworkTime networkTimeSource;` 추가.
  - `protected override INetworkTime CreateNetworkTime() => new MirrorNetworkTime();` **삭제**.
  - `await base.InitializeAsync();` 다음 줄에 hand-off 추가(클라와 동일):
    ```csharp
    networkTime = networkTimeSource;
    ((LOPTickUpdater)tickUpdater).networkTime = networkTimeSource;
    ```

- [ ] **Step 10(서버 LOPTickUpdater): `Netcode/LOPTickUpdater.cs`**
  - `public GameFramework.Netcode.INetworkTime networkTime;` 필드 추가.
  - `OnElapsedTimeUpdate`: `elapsedTime = Runner.NetworkTime.serverNow;` → `elapsedTime = networkTime.ServerNow;`.

- [ ] **Step 11(서버 DebugHud): `UI/DebugHud/DebugHudViewModel.cs`**
  - 생성자에 `IRunner runner` 추가 + 필드 + 대입(현재 ctor 없음 → 새로 추가). getter:
    ```csharp
    public bool IsRunning => runner.gameState >= RunnerState.Playing;
    public long Tick => runner.tickUpdater.tick;
    public double ElapsedTime => runner.tickUpdater.elapsedTime;
    ```

- [ ] **Step 12(서버 DebugHudHost): `UI/DebugHud/DebugHudHost.cs`**
  - 클래스에 `[SceneInjectMonoBehaviour]` 특성 추가 + `[Inject] private IRunner runner;` 필드(+`using GameFramework; using VContainer;`).
  - `new DebugHudViewModel()` → `new DebugHudViewModel(runner)`.
  - ⚠️ **검증**: `DebugHudHost`가 GameLifetimeScope가 InjectSceneObjects 하는 씬(게임 씬)에 있어야 `runner`가 주입됨. 아니면 `runner`가 null → NPE. 컴파일 후 이 배선이 실제로 주입되는지(플레이 스모크에서 서버 HUD가 tick 표시하는지)로 확인. **주입이 안 되면 BLOCKED 보고**(대안: DebugHudViewModel을 스코프 등록 + View가 resolve).

- [ ] **Step 13: 서버 `Game/GameLifetimeScope.cs`** — `INetworkTime` 등록 추가(기존 `ITickUpdater` 등록 L62 인근):
  ```csharp
  builder.Register<GameFramework.Netcode.INetworkTime, MirrorNetworkTime>(Lifetime.Singleton);
  ```

#### 삭제 실행 + 검증 + 커밋

- [ ] **Step 8(삭제): static Runner 제거**
  ```bash
  git -C "C:/Users/re5na/workspace/LOP/GameFramework" rm Runtime/Scripts/Game/Runner.cs Runtime/Scripts/Game/Runner.cs.meta
  ```

- [ ] **Step 14: 잔재 확인** — 3레포 소스에 `Runner.current`/`Runner.Time`/`Runner.NetworkTime`/`CreateNetworkTime` 이 **0**이어야:
  ```bash
  grep -rn "Runner\.current\|Runner\.Time\|Runner\.NetworkTime\|CreateNetworkTime" \
    "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/.claude/worktrees/slice4b/Assets/Scripts" \
    "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts" \
    "C:/Users/re5na/workspace/LOP/GameFramework/Runtime"
  # 기대: 0 (Mirror의 NetworkClient.Cleanup 등 무관 항목 제외)
  ```

- [ ] **Step 15: 컴파일 검증(양쪽 에디터)**
  - 서버: `refresh_unity(mode="force", scope="all", compile="request", unity_instance="...Server...")` (Runner.cs 삭제 있으니 scope=all) → `read_console(types=["error"])` 0.
  - 클라: `refresh_unity(...client, scope=all...)` → `read_console(...client...)` 0. (⚠️ 워크트리 클라 편집은 여기서 컴파일 안 됨 → 서버 그린 + 정적 grep으로 대신, 실제 클라는 머지 후 게이트.)

- [ ] **Step 16: GF EditMode 무회귀** — `run_tests(mode="EditMode", assembly_names=["baegames.GameFramework.Runtime.Tests"], unity_instance="...Client...")` PASS.

- [ ] **Step 17: 커밋 (GF)** — 명시 파일만
  ```bash
  cd "C:/Users/re5na/workspace/LOP/GameFramework"
  git add Runtime/Scripts/Game/RunnerBase.cs
  git status --short   # M RunnerBase.cs, D Runner.cs(+meta, Step 8에서 스테이징)
  git commit -m "refactor(runner): static Runner facade 삭제 + networkTime DI화

Runner.current/Runner.NetworkTime 제거. networkTime 출처를 CreateNetworkTime→DI 주입으로.
RunnerBase.networkTime protected set. is-running은 use-side gameState로.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
  ```

- [ ] **Step 18: 커밋 (Client 워크트리)** — 명시 파일만
  ```bash
  cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/.claude/worktrees/slice4b"
  git add Assets/Scripts/Game/LOPRunner.cs Assets/Scripts/Netcode/LOPTickUpdater.cs \
          Assets/Scripts/Game/MessageHandler/GameEntityMessageHandler.cs \
          Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs Assets/Scripts/Game/GameLifetimeScope.cs
  git commit -m "refactor(runner): 클라 INetworkTime 주입 hand-off + Runner facade 제거

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
  ```

- [ ] **Step 19: 커밋 (Server)** — ⚠️ 픽스처 제외
  ```bash
  cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
  git add Assets/Scripts/Game/LOPRunner.cs Assets/Scripts/Netcode/LOPTickUpdater.cs \
          Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs Assets/Scripts/UI/DebugHud/DebugHudHost.cs \
          Assets/Scripts/Game/GameLifetimeScope.cs
  git status --short   # 위 5파일만 스테이징, 픽스처 3개는 ' M' 유지
  git commit -m "refactor(runner): 서버 INetworkTime 주입 hand-off + Runner facade 제거

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
  ```

## 완료 후

- 최종 whole-branch 리뷰(3-레포). `finishing-a-development-branch` 로컬 `--no-ff` 머지 → **머지 후 main 클라 에디터 컴파일(scope=all force) + 플레이 스모크**(클·서 룸 접속, 이동/틱 HUD/서버 HUD tick 표시 확인 — DebugHudHost 주입 검증 포함). 서버 픽스처 보존 확인.

## 스코프 밖

`ICleanup`·death cascade 등 기타 findings는 각 슬라이스. 리플렉션 이벤트버스(A)·god-object(I)=슬라이스 5. 네이밍(E/F/H)=슬라이스 6.
