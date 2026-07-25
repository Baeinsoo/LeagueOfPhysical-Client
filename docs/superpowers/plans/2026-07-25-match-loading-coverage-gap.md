# 매치 진입 로딩 커버리지 갭 해소 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 매치메이킹 성사 후 룸 연결(`InGameRoom`) → 씬 로드 → 게임 준비 구간의 무커버 갭을 없애, 로딩 화면이 "룸 연결 시점부터 게임이 실제로 시작될 때까지" 연속으로 떠 있게 만든다.

**Architecture:** MVVM-C. 새 `MatchLoadingViewModel`(Root)이 유저 위치(`GetUserLocationResponse` 관찰)와 게임 라이브 사실을 조합해 `IsLoading = (위치==GameRoom) && !gameLive`를 파생하고, 새 `MatchLoadingCoordinator`(Root, `IStartable`)가 그 신호를 구독해 `GameLoadingView`를 Open/Close한다(씬 경계를 넘어 뷰를 소유). 게임 씬의 `LOPGameSceneCoordinator`는 러너 `Playing`에 `NotifyGameLive()`로 사실만 보고한다. 매칭 측은 무변경.

**Tech Stack:** Unity (Assembly-CSharp 클라), VContainer(DI), R3(reactive), MessagePipe(메시지 버스), UI Toolkit + 자체 WindowManager.

## Global Constraints

- **네임스페이스**: 새 타입은 `namespace LOP.UI`(로딩 관련) 또는 기존 파일의 네임스페이스를 유지. `LOP.UI`는 `LOP` 하위라 `GetUserLocationResponse`/`Location` 등 `LOP` 타입을 별도 `using LOP;` 없이 참조 가능(기존 `MatchmakingViewModel`과 동일).
- **reactive = R3**: 상태는 `ReactiveProperty<T>`/`ReadOnlyReactiveProperty<T>`, 구독 해제는 `CompositeDisposable` + `AddTo`.
- **DI = VContainer**: 순수 C#은 생성자 주입, MonoBehaviour는 `[Inject]` 필드 주입(프로젝트 관례, 유지).
- **git**: main 직접 커밋 금지. 이 워크트리 브랜치(`worktree-feature+match-loading-coverage-gap`)에서 작업, 완료 후 `--no-ff` 머지.
- **Unity 컴파일·검증 제약(중요)**: 이 워크트리의 클라 `Assets/` 코드는 연결된 Unity 에디터(main 체크아웃 바인딩)가 못 본다. **머지 전에는 컴파일/플레이 검증이 불가**하고, 클라 코드 단위 테스트도 Assembly-CSharp asmdef 참조 불가로 어렵다. 따라서 Task 1~4는 **코드 작성 + 커밋**까지만 하고, **컴파일·플레이 검증은 머지 후 UnityMCP(client 인스턴스)로 수행**한다(Task 5). UnityMCP 호출은 CLAUDE.md대로 `unity_instance`에 client id를 매번 명시한다.
- **.meta 파일**: 새 `.cs`의 `.meta`는 Unity가 생성한다. 워크트리에선 에디터가 못 봐 생성 안 되므로, **머지 후 main 에디터 리프레시 때 생성된 `.meta`를 커밋**한다(직접 만들지 않는다).
- **필드 경로 확인됨**: `GetUserLocationResponse.userLocation.location`은 `Location` enum(`None`/`WaitingRoom`/`GameRoom`) — 기존 `CheckMatch`/`InWaitingRoom`이 같은 경로로 읽는다.

---

## 파일 구조

- **생성** `Assets/Scripts/UI/Loading/MatchLoadingViewModel.cs` — 위치+gameLive → `IsLoading` 파생 VM.
- **생성** `Assets/Scripts/UI/Loading/MatchLoadingCoordinator.cs` — `IsLoading` 구독 → `GameLoadingView` Open/Close, 뷰 소유.
- **수정** `Assets/Scripts/RootLifetimeScope.cs` — VM(Singleton) + Coordinator(EntryPoint) 등록.
- **수정** `Assets/Scripts/Game/LOPGameSceneCoordinator.cs` — VM 주입, `Playing`에 `NotifyGameLive()`, 기존 `Open/Close<GameLoadingView>` 및 `IWindowManager`/`gameLoadingView` 제거.

---

## Task 1: `MatchLoadingViewModel` (파생 상태 VM)

**Files:**
- Create: `Assets/Scripts/UI/Loading/MatchLoadingViewModel.cs`

**Interfaces:**
- Consumes: `ISubscriber<GetUserLocationResponse>`(MessagePipe, root 브로커 등록됨), `GetUserLocationResponse.userLocation.location: Location`.
- Produces: `MatchLoadingViewModel.IsLoading: ReadOnlyReactiveProperty<bool>`, `MatchLoadingViewModel.NotifyGameLive(): void`, 생성자 `MatchLoadingViewModel(ISubscriber<GetUserLocationResponse>)`.

- [ ] **Step 1: VM 파일 작성**

`Assets/Scripts/UI/Loading/MatchLoadingViewModel.cs`:

```csharp
using MessagePipe;
using R3;
using System;

namespace LOP.UI
{
    /// <summary>
    /// 매치 진입 로딩 화면의 표시 여부를 계산하는 VM.
    /// 백엔드 유저 위치(GameRoom 여부)와 게임 라이브 사실을 조합해 IsLoading을 파생한다.
    /// 어떤 View도 직접 바인딩하지 않고, MatchLoadingCoordinator가 구독해 창을 여닫는다.
    /// </summary>
    public sealed class MatchLoadingViewModel : IDisposable
    {
        private readonly ReactiveProperty<bool> _isLoading = new(false);
        private readonly IDisposable _subscription;

        private bool _inGameRoom;   // 위치 관찰 결과
        private bool _gameLive;     // 게임 씬이 보고한 사실

        /// <summary>로딩 창을 여닫는 근거. 코디네이터가 구독한다.</summary>
        public ReadOnlyReactiveProperty<bool> IsLoading => _isLoading;

        public MatchLoadingViewModel(ISubscriber<GetUserLocationResponse> locationSubscriber)
        {
            _subscription = locationSubscriber.Subscribe(OnLocation);
        }

        private void OnLocation(GetUserLocationResponse response)
        {
            _inGameRoom = response.userLocation.location == Location.GameRoom;
            // 룸을 벗어나면(연결 실패로 로비 복귀 등) 다음 매치를 위해 gameLive를 리셋한다.
            if (!_inGameRoom) _gameLive = false;
            Recompute();
        }

        /// <summary>게임 씬이 "게임이 실제로 시작됨"을 사실로 보고한다.</summary>
        public void NotifyGameLive()
        {
            _gameLive = true;
            Recompute();
        }

        private void Recompute() => _isLoading.Value = _inGameRoom && !_gameLive;

        public void Dispose()
        {
            _subscription.Dispose();
            _isLoading.Dispose();
        }
    }
}
```

- [ ] **Step 2: 커밋**

```bash
git add Assets/Scripts/UI/Loading/MatchLoadingViewModel.cs
git commit -m "feat(ui): MatchLoadingViewModel — 위치+gameLive에서 IsLoading 파생

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

> 검증 노트: 클라 Assembly-CSharp라 EditMode 단위 테스트 불가. 로직(`inGameRoom && !gameLive`)은 자명하며, 실제 위험은 배선(구독·전환·씬 넘김)이라 Task 5 플레이테스트로 통합 검증한다.

---

## Task 2: `MatchLoadingCoordinator` (네비게이션)

**Files:**
- Create: `Assets/Scripts/UI/Loading/MatchLoadingCoordinator.cs`

**Interfaces:**
- Consumes: `MatchLoadingViewModel.IsLoading`, `IWindowManager.Open<GameLoadingView>()`/`Close(UIView)`, `GameLoadingView`(기존, `UILayer.Loading`).
- Produces: `MatchLoadingCoordinator`(VContainer `IStartable` 엔트리포인트) — 생성자 `MatchLoadingCoordinator(MatchLoadingViewModel, IWindowManager)`.

- [ ] **Step 1: 코디네이터 파일 작성**

`Assets/Scripts/UI/Loading/MatchLoadingCoordinator.cs`:

```csharp
using R3;
using System;
using VContainer.Unity;

namespace LOP.UI
{
    /// <summary>
    /// IsLoading(파생 상태)을 보고 로딩 창을 여닫는 코디네이터.
    /// 창 하나의 수명이 씬 경계(로비 → 게임)를 넘으므로 앱(Root) 스코프가 소유한다
    /// — 씬 스코프 코디네이터는 MatchFound 때 파괴되어 창을 계속 쥘 수 없다.
    /// </summary>
    public sealed class MatchLoadingCoordinator : IStartable, IDisposable
    {
        private readonly MatchLoadingViewModel _viewModel;
        private readonly IWindowManager _windowManager;
        private readonly CompositeDisposable _disposables = new();

        private GameLoadingView _view;

        public MatchLoadingCoordinator(MatchLoadingViewModel viewModel, IWindowManager windowManager)
        {
            _viewModel = viewModel;
            _windowManager = windowManager;
        }

        public void Start()
        {
            _viewModel.IsLoading
                .Subscribe(on => { if (on) Show(); else Hide(); })
                .AddTo(_disposables);
        }

        private void Show()
        {
            if (_view == null) _view = _windowManager.Open<GameLoadingView>();
        }

        private void Hide()
        {
            if (_view != null)
            {
                _windowManager.Close(_view);
                _view = null;
            }
        }

        public void Dispose()
        {
            _disposables.Dispose();
            Hide();
        }
    }
}
```

- [ ] **Step 2: 커밋**

```bash
git add Assets/Scripts/UI/Loading/MatchLoadingCoordinator.cs
git commit -m "feat(ui): MatchLoadingCoordinator — IsLoading 구독해 로딩 창 open/close

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: DI 등록 (`RootLifetimeScope`)

**Files:**
- Modify: `Assets/Scripts/RootLifetimeScope.cs` (기존 `new UIInstaller().Install(builder);` 직후)

**Interfaces:**
- Consumes: Task 1 `MatchLoadingViewModel`, Task 2 `MatchLoadingCoordinator`. `RegisterMessageBroker<GetUserLocationResponse>`(같은 파일에 이미 있음), `UIInstaller`가 등록하는 `IWindowManager`.
- Produces: root 컨테이너에 `MatchLoadingViewModel`(Singleton) + `MatchLoadingCoordinator`(EntryPoint) 등록.

- [ ] **Step 1: 등록 추가**

`Assets/Scripts/RootLifetimeScope.cs`에서 `new UIInstaller().Install(builder);` 줄 **직후**에 삽입:

```csharp
            new UIInstaller().Install(builder);

            // 매치 진입 로딩 화면(룸 연결~게임 준비 구간을 연속으로 덮음).
            // VM은 유저 위치(GetUserLocationResponse)를 관찰해 IsLoading을 파생하고,
            // 코디네이터가 그 신호로 로딩 창을 여닫는다(씬 경계를 넘어 뷰를 소유).
            builder.Register<MatchLoadingViewModel>(Lifetime.Singleton);
            builder.RegisterEntryPoint<MatchLoadingCoordinator>();
```

(`using LOP.UI;`는 파일 상단에 이미 있음 — 추가 불필요. `RegisterEntryPoint`/`Lifetime`도 이미 사용 중.)

- [ ] **Step 2: 커밋**

```bash
git add Assets/Scripts/RootLifetimeScope.cs
git commit -m "feat(di): MatchLoadingViewModel(Singleton) + MatchLoadingCoordinator(EntryPoint) 등록

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: `LOPGameSceneCoordinator` — 사실 보고로 전환

**Files:**
- Modify: `Assets/Scripts/Game/LOPGameSceneCoordinator.cs` (전체 교체)

**Interfaces:**
- Consumes: Task 1 `MatchLoadingViewModel.NotifyGameLive()`. 기존 `LOPRunner.onGameStateChanged`, `RunnerState.Playing`, `IPlayerContext`, `CameraController`, `GameInfoToC`.
- Produces: (없음 — 말단 소비자)

- [ ] **Step 1: 파일 전체 교체**

`Assets/Scripts/Game/LOPGameSceneCoordinator.cs`를 아래로 교체(기존 `IWindowManager windowManager`·`UIView gameLoadingView`·`Initialized`/`Playing` Open/Close 제거, VM 주입 + `Playing`에 `NotifyGameLive()`):

```csharp
using Cysharp.Threading.Tasks;
using GameFramework;
using GameFramework.Runner;
using LOP.UI;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace LOP
{
    [SceneInjectMonoBehaviour]
    public class LOPGameSceneCoordinator : MonoBehaviour
    {
        [Inject]
        private CameraController cameraController;

        [Inject]
        private IPlayerContext playerContext;

        [Inject]
        private MatchLoadingViewModel matchLoadingViewModel;

        private LOPRunner runner;
        private System.IDisposable gameInfoSubscription;

        private void Awake()
        {
            // LOPRunner은 이 코디네이터 GameObject의 자식("LOPGameEngine")에 있다.
            runner = GetComponentInChildren<LOPRunner>();
            runner.onGameStateChanged += OnGameStateChanged;

            // MonoBehaviour가 Awake에서 구독 — 주입 타이밍 의존을 피해 GlobalMessagePipe로 구독(구 정적 버스와 동형).
            gameInfoSubscription = GlobalMessagePipe.GetSubscriber<GameInfoToC>().Subscribe(OnGameInfoToC);
        }

        private void OnDestroy()
        {
            runner.onGameStateChanged -= OnGameStateChanged;
            runner = null;

            gameInfoSubscription?.Dispose();
        }

        private void OnGameStateChanged(RunnerState gameState)
        {
            // 로딩 화면은 룸 연결 시점(위치=GameRoom)부터 이미 떠 있다.
            // 게임이 실제로 시작되면 그 사실만 보고하고, 창을 내리는 판단은 VM/코디네이터가 한다.
            if (gameState == RunnerState.Playing)
            {
                matchLoadingViewModel.NotifyGameLive();
            }
        }

        private async void OnGameInfoToC(GameInfoToC gameInfoToC)
        {
            await UniTask.WaitUntil(() => playerContext.actor != null && playerContext.actor.visualGameObject != null);

            cameraController.SetTarget(playerContext.actor.visualGameObject.transform);
        }
    }
}
```

- [ ] **Step 2: 커밋**

```bash
git add Assets/Scripts/Game/LOPGameSceneCoordinator.cs
git commit -m "refactor(game): LOPGameSceneCoordinator가 Playing에 NotifyGameLive 보고 — 직접 open/close 제거

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 5: 통합 검증 (머지 후 UnityMCP 컴파일 + 플레이테스트)

**Files:** (없음 — 검증 전용. 컴파일 오류 발견 시 해당 파일 수정 후 커밋)

**Interfaces:**
- Consumes: Task 1~4 전체.

> **왜 여기서 검증하나**: 워크트리 클라 코드는 main 에디터가 못 봐 머지 전 컴파일 불가(Global Constraints 참조). 그래서 컴파일·`.meta` 생성·플레이 검증을 머지 후 한 번에 한다.

- [ ] **Step 1: 코드 셀프리뷰**

Task 1~4 diff를 다시 읽어 확인:
- `MatchLoadingViewModel`이 `GetUserLocationResponse.userLocation.location`을 읽고 `Location.GameRoom` 비교(기존 `CheckMatch`와 동일 경로).
- `MatchLoadingCoordinator`가 `IStartable`이고 root EntryPoint로 등록됨.
- `LOPGameSceneCoordinator`에서 `IWindowManager`·`gameLoadingView` 잔재 없음, `Playing`에만 `NotifyGameLive()`.
- `GameLoadingView`(기존, `UILayer.Loading`, `BlocksUnderlyingInput=true`)는 변경 없음.

- [ ] **Step 2: main에 `--no-ff` 머지**

```bash
git checkout main
git merge --no-ff worktree-feature+match-loading-coverage-gap -m "Merge feature/match-loading-coverage-gap: 매치 진입 로딩 커버리지 갭 해소"
```

- [ ] **Step 3: UnityMCP로 리프레시 + 콘솔 확인 (client 인스턴스)**

`mcpforunity://instances`에서 `LeagueOfPhysical-Client@<hash>` id를 확인한 뒤:
- `refresh_unity(unity_instance="LeagueOfPhysical-Client@<hash>")`로 임포트/컴파일 유도.
- `editor_state`의 `isCompiling`이 false가 될 때까지 대기.
- `read_console(unity_instance="LeagueOfPhysical-Client@<hash>")`로 **에러 0** 확인. 컴파일 에러가 있으면 원인 수정 → 커밋 → 재확인.

기대: 컴파일 에러 없음. Unity가 새 `.cs` 2개의 `.meta`를 생성.

- [ ] **Step 4: 생성된 `.meta` 커밋**

```bash
git add Assets/Scripts/UI/Loading/*.meta
# UI/Loading 폴더가 새로 생겼다면 그 폴더 .meta도 함께
git status --short   # 생성된 .meta 확인
git commit -m "chore(meta): MatchLoading VM/Coordinator .meta 커밋"
```

- [ ] **Step 5: 플레이테스트 — 두 경로에서 갭 소멸 확인**

로컬 서버 필요(룸 서버 접속). 아래를 육안 확인:
- **경로 A(새 매칭)**: 로비 Play → 매칭 대기창 → (룸 배정) → **로딩창이 끊김 없이 이어짐** → 씬 로드/접속 내내 로딩 유지 → 게임 시작(`Playing`) 시 로딩 닫힘. 매칭 대기창↔로딩 사이 **무커버 프레임 없음**.
- **경로 B(재접속)**: 앱 시작 시 위치가 이미 GameRoom인 상태로 진입 → 곧장 **로딩창** → 접속 완료까지 유지 → `Playing`에 닫힘.
- **회귀 확인**: 게임 중/후 로딩이 다시 뜨지 않음. 매치 종료 후 로비 복귀 시 로딩 잔상 없음.

> 재접속(경로 B)은 파킹된 백엔드 위치-정리 버그와 얽힐 수 있음(매치 종료 시 위치가 안 비면 재접속 루프). 이는 이 작업 범위 밖 — 로딩 커버리지만 본다.

- [ ] **Step 6: 완료 정리**

플레이테스트 통과 확인 후, 워크트리 정리(`ExitWorktree`)와 브랜치 후처리는 `superpowers:finishing-a-development-branch`로 진행.

---

## Self-Review (계획 작성자 체크)

- **스펙 커버리지**: 갭 원인(§근본원인)→Task 1·4가 해소. 위치 관찰(VM)→Task 1. open/close 코디네이터→Task 2. DI→Task 3. 게임 보고→Task 4. 두 경로·엣지·회귀→Task 5 플레이테스트. 매칭 측 무변경 준수. ✅
- **플레이스홀더 스캔**: 모든 코드 스텝에 실제 전체 코드 포함. TBD/TODO 없음. ✅
- **타입 일관성**: `IsLoading: ReadOnlyReactiveProperty<bool>`(Task 1) ↔ 구독(Task 2) 일치. `NotifyGameLive()`(Task 1) ↔ 호출(Task 4) 일치. 생성자 시그니처(VM: `ISubscriber<GetUserLocationResponse>`, Coordinator: `MatchLoadingViewModel, IWindowManager`) ↔ DI 등록(Task 3)에서 자동 주입 가능. `GameLoadingView`/`IWindowManager`/`UILayer.Loading` 기존 심볼. ✅
- **테스트 제약**: 클라 Assembly-CSharp EditMode 불가를 명시하고 통합 플레이테스트로 acceptance 잡음(스펙의 테스트 방침과 일치). ✅
