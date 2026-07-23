# 매치메이킹 UI 코디네이터 리팩터 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `MatchMakingViewModel`이 화면 전환을 직접 하던 규칙 위반을 없애고, 대기 오버레이 열고/닫기를 전용 `MatchmakingCoordinator`로 분리한다.

**Architecture:** VM은 매칭 상태를 R3 `ReadOnlyReactiveProperty<bool> IsMatching`으로만 노출한다(신호). 새 `MatchmakingCoordinator`(VContainer 엔트리포인트)가 그 신호를 구독해 `IWindowManager`로 대기 오버레이를 열고/닫고 취소 버튼을 배선한다. FSM(`MatchStateMachine`)과 상태들은 손대지 않는다.

**Tech Stack:** Unity(UI Toolkit), R3(ReactiveProperty), VContainer(DI/엔트리포인트), UniTask.

## Global Constraints

- **순수 리팩터 — 동작 변화 0.** 사용자가 보는 매칭 흐름은 리팩터 전후 동일해야 한다.
- **피처 브랜치**에서 작업(현재 `refactor/matchmaking-ui-coordinator`). main 직접 커밋 금지.
- **새 `.cs` 파일은 Unity가 생성한 `.meta`와 함께 커밋**한다. `.meta`는 직접 만들지 않는다(Unity Editor가 생성).
- **주석 최소화·쉽게.** 코드로 자명한 것은 주석 없이. 비자명한 의도만 한 줄로.
- **테스트 전략 = 컴파일 체크 + 플레이테스트.** 클라 코드가 Assembly-CSharp라 단위 테스트 인프라가 아직 없음(.NET SDK 미설치, `Assets/Tests` 없음). 각 변경 후 Unity 컴파일 오류 0 확인 + 마지막에 매칭 흐름 end-to-end 플레이테스트로 동작 보존 검증.
- **UnityMCP 대상 고정:** 모든 UnityMCP 호출에 `unity_instance`를 클라(`LeagueOfPhysical-Client@<hash>`)로 명시. `mcpforunity://instances`에서 이름이 `LeagueOfPhysical-Client`인 인스턴스 id를 먼저 확인.

---

## 파일 구조

| 파일 | 책임 | 변경 |
|---|---|---|
| `Assets/Scripts/UI/MatchMaking/MatchMakingViewModel.cs` | 매칭 상태를 R3 신호로 노출 + Play/Cancel 커맨드 | 수정 |
| `Assets/Scripts/UI/MatchMaking/MatchmakingCoordinator.cs` | 신호 구독 → 대기 오버레이 열고/닫기 (네비게이션) | **신규** |
| `Assets/Scripts/UI/MatchMaking/MatchMakingView.cs` | Play 버튼 → `vm.Play()` (얇은 바인더) | 수정 |
| `Assets/Scripts/Lobby/LobbyLifetimeScope.cs` | VM 등록 Scoped화 + 코디네이터 엔트리포인트 등록 | 수정 |
| `Assets/Scripts/UI/Core/WindowManager.cs` | (곁다리) 틀린 UGUI 주석 정정 | 수정 |

---

## Task 1: 틀린 UGUI 주석 정정 (곁다리)

**Files:**
- Modify: `Assets/Scripts/UI/Core/WindowManager.cs:75-77`

**Interfaces:**
- Consumes: (없음)
- Produces: (없음 — 주석만)

독립적이고 사소한 정리. 다른 작업과 무관하게 먼저/따로 리뷰 가능.

- [ ] **Step 1: 주석 정정**

`Assets/Scripts/UI/Core/WindowManager.cs`의 `Open<T>` 안(현재 75~77행) 주석을 교체한다.

기존:
```csharp
            // 입력을 안 막는 View의 전체화면 래퍼는 포인터를 통과시킨다(자식 위젯만 picking).
            // 안 그러면 전체화면 래퍼가 포인터를 먹어 아래 UGUI(조그패드 등)가 막힌다.
            // 모달·전체화면 오버레이(로딩/매칭)는 막아야 하므로 제외.
```

교체:
```csharp
            // 입력을 안 막는 View의 전체화면 래퍼는 포인터를 통과시킨다(자식 위젯만 picking).
            // 안 그러면 전체화면 래퍼가 포인터를 먹어 아래 화면(조그패드 등)의 입력이 막힌다.
            // 모달·전체화면 오버레이(로딩/매칭)는 막아야 하므로 제외.
```

("아래 UGUI(조그패드 등)가 막힌다" → "아래 화면(조그패드 등)의 입력이 막힌다". 조그패드도 이제 UI Toolkit이라 UGUI 언급이 틀린 잔재.)

- [ ] **Step 2: 컴파일 확인**

UnityMCP로 클라 인스턴스에 `refresh_unity` 후 `read_console`로 오류 0 확인.
Expected: 컴파일 오류/경고 없음(주석만 변경).

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/UI/Core/WindowManager.cs
git commit -m "docs(ui): WindowManager 주석의 UGUI 잔재 정정 (조그패드도 UI Toolkit)"
```

---

## Task 2: VM·View·코디네이터·스코프 원자 리팩터

**Files:**
- Modify: `Assets/Scripts/UI/MatchMaking/MatchMakingViewModel.cs` (전체 교체)
- Create: `Assets/Scripts/UI/MatchMaking/MatchmakingCoordinator.cs`
- Modify: `Assets/Scripts/UI/MatchMaking/MatchMakingView.cs` (전체 교체)
- Modify: `Assets/Scripts/Lobby/LobbyLifetimeScope.cs:28-29` (등록 3줄)

**Interfaces:**
- Consumes:
  - `MatchStateMachine` (GameFramework `StateMachine<MatchEvent>`) — `event onStateChange(IState<MatchEvent> prev, IState<MatchEvent> cur)`, `void Start()`, `void Stop()`, `void Fire(MatchEvent)`.
  - `IMatchMakingDataStore` — `matchType`/`subGameId`/`mapId` setter.
  - `IWindowManager` — `T Open<T>() where T : UIView`, `void Close(UIView)`.
  - `MatchingWaitingView` — `void SetCancelCallback(System.Action)`.
  - `InWaitingRoom`, `MatchEvent`, `GameMode` (namespace `LOP`).
- Produces:
  - `MatchMakingViewModel.IsMatching : ReadOnlyReactiveProperty<bool>`
  - `MatchMakingViewModel.Play() : void`, `Cancel() : void`, `StartFlow() : void`
  - `MatchmakingCoordinator : IStartable, IDisposable` (ctor: `IWindowManager`, `MatchMakingViewModel`)

> 이 네 파일은 **컴파일 상호 의존**이다(VM 공개 멤버 변경이 View·코디네이터·스코프에 동시 영향). 그래서 한 태스크로 묶어 마지막에 함께 컴파일한다. 스텝은 잘게 나누되, 컴파일·커밋은 태스크 끝에서 한 번.

- [ ] **Step 1: `MatchMakingViewModel.cs` 전체 교체**

`Assets/Scripts/UI/MatchMaking/MatchMakingViewModel.cs`를 아래로 교체한다.

```csharp
using GameFramework;
using R3;
using System;

namespace LOP.UI
{
    /// <summary>
    /// 매칭 기능의 프레젠테이션 어댑터. Model인 MatchStateMachine(FSM)을 주시해 매칭 진행 상태를
    /// R3 신호(IsMatching)로 노출하고, Play/Cancel 커맨드를 FSM 이벤트로 전달한다.
    /// 대기 오버레이 열고/닫기(네비게이션)는 이 VM이 아니라 MatchmakingCoordinator가 담당한다
    /// — VM은 도메인 신호만 노출한다(아키텍처: 작은 흐름=VM / 큰 흐름=코디네이터).
    /// </summary>
    public class MatchMakingViewModel : IDisposable
    {
        private readonly MatchStateMachine _matchStateMachine;
        private readonly IMatchMakingDataStore _matchMakingDataStore;

        private readonly ReactiveProperty<bool> _isMatching = new(false);

        /// <summary>매칭 진행 중 여부. 코디네이터가 구독해 대기 오버레이를 열고/닫는다.</summary>
        public ReadOnlyReactiveProperty<bool> IsMatching => _isMatching;

        public MatchMakingViewModel(
            MatchStateMachine matchStateMachine,
            IMatchMakingDataStore matchMakingDataStore)
        {
            _matchStateMachine = matchStateMachine;
            _matchMakingDataStore = matchMakingDataStore;
        }

        /// <summary>흐름 시작. FSM 구독 + 시작(현재 위치 확인 → 적절한 상태로 진입). 코디네이터가 호출한다.</summary>
        public void StartFlow()
        {
            _matchStateMachine.onStateChange += OnStateChange;
            _matchStateMachine.Start();
        }

        /// <summary>Play 버튼 커맨드. 매칭 파라미터 세팅 후 FSM에 PlayClicked 발행.</summary>
        public void Play()
        {
            _matchMakingDataStore.matchType = GameMode.Normal;
            _matchMakingDataStore.subGameId = "FlapWang";
            _matchMakingDataStore.mapId = "FlapWangMap";

            _matchStateMachine.Fire(MatchEvent.PlayClicked);
        }

        /// <summary>취소 커맨드(대기 화면 취소 버튼). FSM에 CancelClicked 발행.</summary>
        public void Cancel()
        {
            _matchStateMachine.Fire(MatchEvent.CancelClicked);
        }

        private void OnStateChange(IState<MatchEvent> previous, IState<MatchEvent> current)
        {
            _isMatching.Value = current is InWaitingRoom;
        }

        public void Dispose()
        {
            _matchStateMachine.onStateChange -= OnStateChange;
            _matchStateMachine.Stop();
            _isMatching.Dispose();
        }
    }
}
```

변경 요지: `IWindowManager` 의존·`_waitingView`·`Open/Close`·`SetCancelCallback` 제거. `IsMatching` bool → `ReadOnlyReactiveProperty<bool>`. `Start()` → `StartFlow()`.

- [ ] **Step 2: `MatchmakingCoordinator.cs` 신규 생성**

`Assets/Scripts/UI/MatchMaking/MatchmakingCoordinator.cs`를 아래 내용으로 만든다.

```csharp
using R3;
using System;
using VContainer.Unity;

namespace LOP.UI
{
    /// <summary>
    /// 매칭 흐름의 화면 전환(네비게이션) 담당. MatchMakingViewModel의 IsMatching 신호를 구독해
    /// 대기 오버레이(MatchingWaitingView)를 열고/닫고, 취소 버튼을 VM의 Cancel 커맨드에 배선한다.
    /// VM은 신호만 노출하고 화면 교체는 여기서 한다(아키텍처: 작은 흐름=VM / 큰 흐름=코디네이터).
    /// </summary>
    public class MatchmakingCoordinator : IStartable, IDisposable
    {
        private readonly IWindowManager _windowManager;
        private readonly MatchMakingViewModel _viewModel;

        private IDisposable _subscription;
        private MatchingWaitingView _waitingView;

        public MatchmakingCoordinator(IWindowManager windowManager, MatchMakingViewModel viewModel)
        {
            _windowManager = windowManager;
            _viewModel = viewModel;
        }

        public void Start()
        {
            // ReactiveProperty는 구독 즉시 현재값을 replay하므로 StartFlow 전에 구독해도 안전.
            _subscription = _viewModel.IsMatching.Subscribe(OnMatchingChanged);
            _viewModel.StartFlow();
        }

        private void OnMatchingChanged(bool matching)
        {
            if (matching)
            {
                if (_waitingView == null)
                {
                    _waitingView = _windowManager.Open<MatchingWaitingView>();
                    _waitingView.SetCancelCallback(_viewModel.Cancel);
                }
            }
            else if (_waitingView != null)
            {
                _windowManager.Close(_waitingView);
                _waitingView = null;
            }
        }

        public void Dispose()
        {
            _subscription?.Dispose();

            if (_waitingView != null)
            {
                _windowManager.Close(_waitingView);
                _waitingView = null;
            }
        }
    }
}
```

- [ ] **Step 3: `MatchMakingView.cs` 전체 교체**

`Assets/Scripts/UI/MatchMaking/MatchMakingView.cs`를 아래로 교체한다(`vm.Start()`·`vm.Dispose()` 호출 제거, 낡은 클래스 주석 갱신).

```csharp
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 로비 매치메이킹 화면 View. Play 버튼 클릭을 ViewModel 커맨드로 전달하는 얇은 바인더.
    /// 흐름 시작과 대기 오버레이 제어는 MatchmakingCoordinator가 담당하므로 여기선 다루지 않는다.
    /// </summary>
    public class MatchMakingView : UIView
    {
        private readonly MatchMakingViewModel _viewModel;

        private Button _playButton;

        public MatchMakingView(MatchMakingViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();

            _playButton = Root.Q<Button>("play-button");
            _playButton.clicked += OnPlayClicked;
        }

        public override void OnClose()
        {
            if (_playButton != null) _playButton.clicked -= OnPlayClicked;
            base.OnClose();
        }

        private void OnPlayClicked() => _viewModel.Play();
    }
}
```

(VM은 이제 Scoped라 스코프가 Dispose를 소유 → View는 VM을 Dispose하지 않는다. 기존 `Dispose` override는 VM Dispose만 했으므로 제거.)

- [ ] **Step 4: `LobbyLifetimeScope.cs` 등록 변경**

`Assets/Scripts/Lobby/LobbyLifetimeScope.cs`의 28~29행을 찾아 교체한다.

기존:
```csharp
            builder.Register<MatchMakingViewModel>(Lifetime.Transient);
            builder.Register<MatchMakingView>(Lifetime.Transient);
```

교체:
```csharp
            // VM은 Scoped — View와 Coordinator가 같은 인스턴스를 공유해야 신호가 이어진다.
            builder.Register<MatchMakingViewModel>(Lifetime.Scoped);
            builder.Register<MatchMakingView>(Lifetime.Transient);
            builder.RegisterEntryPoint<MatchmakingCoordinator>();
```

(`RegisterEntryPoint`는 `VContainer.Unity` — 파일에 이미 `using VContainer.Unity;` 있음. 스코프가 `IStartable.Start`/`IDisposable.Dispose`를 구동한다.)

- [ ] **Step 5: Unity 컴파일 확인**

UnityMCP 클라 인스턴스에 `refresh_unity`(scope=all) 후 `editor_state`의 `isCompiling`이 false 될 때까지 대기 → `read_console`로 오류 0 확인.
Expected: 컴파일 오류 없음. (신규 `MatchmakingCoordinator.cs.meta`가 Unity에 의해 생성됨.)

만약 오류가 나면:
- `MatchMakingViewModel` 관련 미해결 참조 → 위 4개 파일 중 하나가 옛 API(`Start`/bool `IsMatching`)를 아직 참조하는지 확인.
- `RegisterEntryPoint` 미해결 → `LobbyLifetimeScope.cs`의 `using VContainer.Unity;` 확인.

- [ ] **Step 6: 커밋 (`.meta` 포함)**

```bash
git add Assets/Scripts/UI/MatchMaking/MatchMakingViewModel.cs \
        Assets/Scripts/UI/MatchMaking/MatchmakingCoordinator.cs \
        Assets/Scripts/UI/MatchMaking/MatchmakingCoordinator.cs.meta \
        Assets/Scripts/UI/MatchMaking/MatchMakingView.cs \
        Assets/Scripts/Lobby/LobbyLifetimeScope.cs
git commit -m "refactor(ui): 매칭 대기 오버레이를 MatchmakingCoordinator로 분리

VM은 IsMatching을 R3 신호로만 노출(WindowManager 의존 제거), 신규
MatchmakingCoordinator가 신호 구독 → 오버레이 열고/닫기 + 취소 배선.
VM Transient→Scoped(View·Coordinator 공유). 동작 변화 0."
```

> `git status`로 `MatchmakingCoordinator.cs.meta`가 실제로 생성됐는지 확인 후 add. 없으면 Unity refresh가 안 끝난 것 — 재확인.

---

## Task 3: 매칭 흐름 플레이테스트 (동작 보존 검증)

**Files:** (없음 — 검증만)

**Interfaces:**
- Consumes: Task 1·2의 커밋된 변경.
- Produces: (없음)

순수 리팩터라 자동 단위 테스트 인프라가 없으므로, 매칭 흐름을 실제로 돌려 리팩터 전과 **동작이 같은지** 확인한다.

> **선행 조건:** 로컬 서버(매칭/룸) + DB + auth 픽스처가 떠 있어야 매칭이 진행된다(WebAPI `GetUserLocation` 폴링·룸 접속). 로컬 테스트 픽스처는 프로젝트 메모리("Local test auth fixture") 참고 — DB 리셋 시 현재 게스트 uuid를 서버 playerList에 추가 후 서버 재시작 필요. 백엔드를 못 띄우는 상황이면 이 태스크는 **사용자 수동 검증**으로 넘긴다.

- [ ] **Step 1: 플레이 모드 진입 + 로비까지 이동**

UnityMCP 클라 인스턴스에서 `manage_editor`로 플레이 진입 → Entrance(로그인) → Lobby 도달. `read_console`로 예외 0 확인.
Expected: 로비 화면에 `MatchMakingView`(Play 버튼) 표시. 예외 없음.

- [ ] **Step 2: Play → 대기 오버레이 표시 확인**

Play 버튼 클릭(또는 해당 상호작용). 서버가 매칭을 받아 사용자 위치가 WaitingRoom이 되면 FSM이 `InWaitingRoom`으로 전이 → 코디네이터가 `MatchingWaitingView`(대기 오버레이)를 연다.
Expected: 대기 오버레이가 뜨고 입력이 아래 화면으로 안 샌다(Loading 밴드, `BlocksUnderlyingInput=true`). `read_console` 예외 0.

- [ ] **Step 3: 취소 → 오버레이 닫힘 확인**

대기 오버레이의 취소 버튼 클릭 → `vm.Cancel()` → FSM `CancelClicked` → `InWaitingRoom` 이탈 → `IsMatching=false` → 코디네이터가 오버레이 Close.
Expected: 대기 오버레이가 닫히고 로비 화면으로 복귀. 예외 없음.

- [ ] **Step 4: 스코프 teardown 확인 (로비 이탈)**

매칭 성사로 룸/게임 씬에 진입하거나 로비를 벗어날 때, `LobbyLifetimeScope` 파괴로 코디네이터·VM이 Dispose되고 열려 있던 오버레이가 닫히는지 확인(오버레이가 남아 떠 있지 않아야 함). `read_console`로 Dispose 관련 예외(이중 Dispose 등) 0 확인.
Expected: 잔존 오버레이 없음, Dispose 예외 없음.

- [ ] **Step 5: 플레이 모드 종료**

`manage_editor`로 플레이 종료. 최종 `read_console` 확인.
Expected: 흐름 전체에서 예외 0, 동작이 리팩터 전과 동일.

---

## 완료 기준

- Task 1·2 커밋 완료, Unity 컴파일 오류 0.
- Task 3 플레이테스트에서 매칭 시작→대기 오버레이→취소→닫힘 흐름이 리팩터 전과 동일하게 동작.
- `MatchMakingViewModel`에 `IWindowManager`·`Open`/`Close` 흔적 없음, `IsMatching`이 R3 신호.
- 스코프 위치(Lobby)는 그대로 — front-end 세션 스코프 이동은 다음 플로우 설계에서.
