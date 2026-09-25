# 매치 진입 로딩 커버리지 갭 해소 설계

매치메이킹 성사 후 **룸 연결 → 씬 로드 → 게임 준비** 구간에서 아무 오버레이도 화면을 안 덮는 갭을 없앤다. 로딩 화면이 "룸 연결이 필요한 시점부터 게임이 실제로 시작될 때까지" 연속으로 떠 있게 만든다.

## 배경 — 무엇이 문제인가

로그인 이후 화면 흐름(로비 → 매칭 → 게임)에서, 유저가 보는 오버레이는 상태에 따라 이렇게 바뀐다:

| 구간 | 매칭 FSM 상태 | 오버레이 |
|---|---|---|
| 로비 대기 | `Idle` | 없음(로비 홈, 정상) |
| 매칭 대기 | `InWaitingRoom` | `MatchingWaitingView` |
| **룸 연결** | **`InGameRoom`** | ❌ **없음 (갭)** |
| **씬 로드 + 룸서버 접속** | `MatchFound` → 앱 FSM `InMatch` | ❌ **없음 (갭)** |
| 게임 준비 | 러너 `Initialized` | `GameLoadingView` |
| 플레이 | 러너 `Playing` | 닫힘 |

즉 **룸 연결(`InGameRoom`)이 시작되는 순간 매칭 대기창이 닫히고, 게임 씬에서 러너가 `Initialized`에 도달할 때까지 아무것도 화면을 안 덮는다.** 유저가 언급한 증상: "매치메이킹 되고 룸 입장 사이에 매칭 준비창이나 로딩창이 안 뜨는 구간이 존재".

### 근본 원인

`MatchmakingViewModel.cs`에서 매칭 진행 신호를 `InWaitingRoom`일 때만 참으로 둔다:

```csharp
private void OnStateChange(...) => _isMatching.Value = current is InWaitingRoom;
```

`MatchmakingCoordinator`는 이 `IsMatching`을 구독해 매칭 대기창을 여닫으므로, `InWaitingRoom → InGameRoom` 전이 순간 `IsMatching`이 거짓이 되어 대기창이 닫힌다. 그런데 `InGameRoom`(룸 연결)과 그 뒤 씬 로드 구간을 덮는 오버레이는 아무도 안 띄운다.

로딩창을 여닫는 유일한 주체는 게임 씬의 `LOPGameSceneCoordinator`인데, 러너 `Initialized`에야 연다:

```csharp
case RunnerState.Initialized: gameLoadingView = windowManager.Open<GameLoadingView>(); break;
case RunnerState.Playing:      windowManager.Close(gameLoadingView); break;
```

`Initialized`는 게임 씬 로드 + 룸서버 접속이 끝나야 도달하므로, 그 이전(`InGameRoom` + 씬 로드)이 통째로 무커버.

### 이 갭에 도달하는 두 경로

`InGameRoom`(룸 연결 구간)에는 두 길로 들어온다 — 설계는 **둘 다** 커버한다:

1. **새 매칭**: 로비 → Play → `InWaitingRoom` → 룸 배정 → `InGameRoom`
2. **재접속(앱 시작 시 이미 룸에 있음)**: `CheckMatch`가 위치=GameRoom 판정 → 곧장 `InGameRoom` (매칭 대기창이 애초에 없었음)

두 경로 모두 위치가 `GameRoom`이 되면서 `InGameRoom`에 진입한다는 공통점이 있다. 이 공통 신호(위치)를 트리거로 삼는다.

## 핵심 아이디어

로딩 화면이 떠야 하는지는 **두 subsystem의 사실**에 걸쳐 있다:

```
로딩 표시 = (유저 위치 == GameRoom) AND (게임이 아직 Playing 아님)
```

- **켜는 근거 = 백엔드 유저 위치**: 위치가 `GameRoom`이 되면 "게임에 들어가는 중" → 로딩 켠다.
- **끄는 근거 = 러너 `Playing`**: 게임이 실제로 플레이 가능해지면 로딩 끈다. **이건 위치에 안 담긴다** — 위치는 게임 내내 `GameRoom`으로 유지되므로 위치만으로는 "끌 때"를 못 정한다. 러너가 `Playing`에 도달했다는 *게임 씬 런타임 사실*이 필요하다.

이 두 입력을 하나의 파생 상태 `IsLoading`으로 계산하고, 그 상태에 따라 로딩 창을 여닫는다.

## 아키텍처 — 역할 분리 (MVVM-C)

세 역할을 서로 다른 객체가 맡는다. **상태 계산 / 네비게이션 / 사실 보고**를 분리한다.

```
GetUserLocationResponse (위치 관찰)  ─┐
                                      ├─> MatchLoadingViewModel ──IsLoading──> MatchLoadingCoordinator ──Open/Close──> WindowManager
LOPGameSceneCoordinator (Playing 보고)─┘        (파생 상태)                        (네비게이션)                         (메커니즘)
```

이 프로젝트의 계층 규약과 정합한다(아키텍처 문서 "흐름의 경계 — VM(작은 흐름) vs 코디네이터(큰 흐름)", "화면 관리 — 레이어 × 스택 윈도우 매니저"):

- **VM**: 도메인 신호를 화면용 상태로 파생해 노출(로직·상태).
- **코디네이터**: 그 상태를 보고 화면을 여닫는다(네비게이션 = 큰 흐름).
- **WindowManager**: 창을 어떻게 띄우나(메커니즘, 도메인 무지).

### 1. `MatchLoadingViewModel` (신규, Root 스코프)

로딩이 떠야 하는지를 하나의 reactive 값으로 계산해 노출하는 프레젠테이션 상태. (Model 아님 — Model은 World Core의 anemic 도메인 데이터. 이건 위치·러너 신호에서 화면용 상태를 파생하는 VM 성격.)

```csharp
namespace LOP.UI
{
    /// 매치 진입 로딩 화면의 표시 여부를 계산하는 VM.
    /// 백엔드 유저 위치(GameRoom 여부)와 게임 라이브 사실을 조합해 IsLoading을 파생한다.
    public sealed class MatchLoadingViewModel : System.IDisposable
    {
        private readonly ReactiveProperty<bool> _isLoading = new(false);
        private readonly System.IDisposable _subscription;

        private bool _inGameRoom;   // 위치 관찰 결과
        private bool _gameLive;     // 게임 씬이 보고한 사실

        /// 로딩 창을 여닫는 근거. 코디네이터가 구독한다.
        public ReadOnlyReactiveProperty<bool> IsLoading => _isLoading;

        public MatchLoadingViewModel(ISubscriber<GetUserLocationResponse> locationSubscriber)
        {
            _subscription = locationSubscriber.Subscribe(OnLocation);
        }

        private void OnLocation(GetUserLocationResponse response)
        {
            _inGameRoom = response.userLocation.location == Location.GameRoom;
            // 룸을 벗어나면(연결 실패로 로비 복귀 등) 다음 매치를 위해 gameLive 리셋.
            if (!_inGameRoom) _gameLive = false;
            Recompute();
        }

        /// 게임 씬이 "게임이 실제로 시작됨"을 사실로 보고.
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

- `GetUserLocationResponse`는 이미 root MessagePipe 브로커로 등록돼 있고(`RootLifetimeScope`), `UserDataStore`가 같은 방식으로 구독한다 — 동일 패턴을 따른다.
- 입력이 위치·gameLive 둘이든 더 늘든 `Recompute` 한 곳에서만 상태가 바뀐다.

### 2. `MatchLoadingCoordinator` (신규, Root 스코프, `IStartable`)

`IsLoading` **하나만** 구독해 로딩 창을 여닫는 네비게이션 담당. 로딩 뷰 단일 인스턴스를 자기가 쥐어 씬 언로드에도 생존시킨다(`WindowManager`가 `DontDestroyOnLoad` 앱 전역이라 뷰가 씬을 넘어 살아남는다).

```csharp
namespace LOP.UI
{
    /// IsLoading(파생 상태)을 보고 로딩 창을 여닫는 코디네이터.
    /// 창 하나의 수명이 씬 경계(로비 → 게임)를 넘으므로 앱(Root) 스코프가 소유한다
    /// — 씬 스코프 코디네이터는 MatchFound 때 파괴되어 창을 계속 쥘 수 없다.
    public sealed class MatchLoadingCoordinator : IStartable, System.IDisposable
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

        private void Show() { if (_view == null) _view = _windowManager.Open<GameLoadingView>(); }
        private void Hide() { if (_view != null) { _windowManager.Close(_view); _view = null; } }

        public void Dispose()
        {
            _disposables.Dispose();
            Hide();
        }
    }
}
```

- 멱등: 이미 열림/닫힘이면 no-op → 이중 인스턴스·이중 닫기 없음.
- `IStartable`로 앱 시작 시 `IsLoading`을 구독한다(로딩이 필요한 `InGameRoom` 구간보다 먼저 살아 있어야 한다).

### 3. `LOPGameSceneCoordinator` 변경 — 사실 보고만

직접 `windowManager.Open/Close<GameLoadingView>` 호출을 버리고, "게임 라이브됨"이라는 *사실*만 VM에 보고한다. 뷰를 여닫으라 지시하지 않는다(결정은 VM+코디네이터가).

```csharp
[Inject] private MatchLoadingViewModel matchLoadingViewModel;   // 신규 주입

private void OnGameStateChanged(RunnerState gameState)
{
    if (gameState == RunnerState.Playing)
        matchLoadingViewModel.NotifyGameLive();
}
```

- 기존 `Initialized`에 여는 처리 삭제(로딩은 위치 관찰로 이미 떠 있다).
- 카메라 타겟 로직(`OnGameInfoToC`)은 그대로 둔다.

### 4. 매칭 측 — 무변경

`MatchmakingViewModel`·`MatchmakingCoordinator`는 **손대지 않는다**. 로딩 VM이 위치를 독립적으로 관찰하므로 `IsConnecting` 같은 신호나 poke가 불필요하다. 매칭 대기창은 지금처럼 `IsMatching`으로 동작. → 결합 최소, 새·재접속 두 경로 자동 커버.

### 5. DI 등록 (`RootLifetimeScope`)

```csharp
builder.Register<MatchLoadingViewModel>(Lifetime.Singleton);   // 게임 코디네이터가 주입
builder.RegisterEntryPoint<MatchLoadingCoordinator>();          // 앱 시작 시 IsLoading 구독
```

- 둘 다 root 스코프(`UIInstaller` 등록 근처).
- `MatchLoadingViewModel`은 코디네이터가 의존하므로 앱 시작 시 생성 → 생성자에서 위치 구독을 시작(InGameRoom보다 먼저 살아 있음).
- `LOPGameSceneCoordinator`(게임 씬 `[SceneInjectMonoBehaviour]`)는 root의 `MatchLoadingViewModel` 싱글턴을 필드 주입으로 받는다.

## 데이터 흐름

| 구간 | 위치 | `_inGameRoom` | `_gameLive` | `IsLoading` | 로딩 창 | 주체 |
|---|---|---|---|---|---|---|
| 매칭 대기 | WaitingRoom | F | F | F | ✕ (매칭창) | MatchmakingCoordinator |
| **룸 연결** | **GameRoom** | T | F | **T** | **Open** | 위치 관찰 → VM → Coordinator |
| 씬 로드 + 접속 | GameRoom | T | F | T | 유지 | Coordinator (root, 참조 보유) |
| 게임 준비 | GameRoom | T | F | T | 유지 | — |
| **Playing** | GameRoom | T | **T** | **F** | **Close** | LOPGameSceneCoordinator → NotifyGameLive |
| 연결 실패 → 로비 | GameRoom→None | F | F(리셋) | F | Close | 위치 관찰 → VM → Coordinator |

**단일 뷰 인스턴스**가 룸 연결부터 `Playing`까지 씬 경계를 넘어 연속으로 떠 있다 → 갭 소멸.

## 엣지 케이스 / 범위

- **파킹된 백엔드 재접속-루프 버그와 독립.** 이 설계는 로딩 프레젠테이션만 손댄다. `_gameLive` 리셋은 "위치가 GameRoom 이탈"에 의존하는데, 백엔드가 매치 종료 시 유저 위치를 안 비우는 파킹 버그(`flow-slice-d` 후속)가 있으면 그 리셋이 로컬에서 안 될 수 있다. 그건 별개 백엔드/RoomServer 작업이며 이 설계 범위 밖 — 여기서는 백엔드가 매치 종료 시 위치를 비운다는 전제를 따른다.
- **연결 완전 실패(재시도 초과).** `InGameRoom → RecheckRequested → CheckMatch → Idle`이면 위치가 GameRoom을 벗어나 `IsLoading=false` → 로딩이 로비에 안 남는다.
- **연결 실패 재시도 중 깜빡임.** `InGameRoom → CheckMatch → 다시 InGameRoom`이면 잠깐 Hide→Show 가능. v1 수용(실제 재시도 상황). 거슬리면 "connecting 슈퍼상태 유지"로 후속 개선.
- **에디터에서 Room 씬 직접 실행.** 로비를 안 거쳐 위치가 기본값(None)이라 로딩이 안 뜬다. 에디터 전용 경로라 수용(실제 매치 진입 전환이 없으므로 로딩이 필요 없음).

## 테스트

- 로직은 "위치 이벤트 + gameLive 보고 → `IsLoading`" 파생 규칙(작은 상태기계). 규칙 자체는 `MatchLoadingViewModel` 안에서 자기완결.
- 클라 코드가 전부 Assembly-CSharp라 EditMode asmdef 참조 불가(기존 제약). 가능하면 fake `ISubscriber<GetUserLocationResponse>`로 `IsLoading` 파생 단위 검증(위치=GameRoom→true, NotifyGameLive→false, 위치 이탈→리셋), 어려우면 PlayMode 리플렉션.
- **1차 acceptance = 플레이테스트**: (a) 새 매칭, (b) 재접속 두 경로에서 매칭 대기 → 로딩 → 인게임 사이에 **무커버 구간이 사라졌는지** 육안 확인. 갭 해소는 본질적으로 통합/시각 기준.

## 변경 파일

- 신규: `Assets/Scripts/UI/Loading/MatchLoadingViewModel.cs` (+ `.meta`)
- 신규: `Assets/Scripts/UI/Loading/MatchLoadingCoordinator.cs` (+ `.meta`)
- `RootLifetimeScope.cs`: 등록 2줄
- `LOPGameSceneCoordinator.cs`: `MatchLoadingViewModel` 주입 + `Playing`에 `NotifyGameLive()`, 기존 `Open/Close<GameLoadingView>` 제거

## 산업 표준 매핑

- **MVVM-C (Model-View-ViewModel-Coordinator)**: 화면 전환(네비게이션)을 VM에서 분리해 코디네이터가 소유. VM은 상태(`IsLoading`)만 파생·노출하고, 코디네이터가 그 상태를 보고 창을 push/pop. iOS Coordinator 패턴(Soroush Khanlou), Prism의 navigation/dialog service와 대응.
- **글로벌 로딩/busy 오버레이의 두 표준 구현** — 이 설계는 **(B) 스택 창(push/pop) + 코디네이터**를 채택. 대안 (A) "상주 뷰가 VM의 `IsLoading`을 바인딩해 자기 visibility를 토글"(WPF/Avalonia `BusyIndicator ← IsBusy` 관용구)도 유효하나, 이 프로젝트가 모든 화면을 레이어×스택 윈도우 매니저의 push/pop으로 통일하므로 일관성을 위해 B를 선택. (A는 로딩 뷰를 스택 밖 상주 특수 케이스로 만들고 입력차단 엣지 처리를 뷰에 얹게 됨.)
- **결정 기록**: 로딩은 "중첩되는 다이얼로그"가 아니라 "단일 모드 베일"이지만, 밴드(`UILayer.Loading`)에서 매칭창과 같은 push/pop idiom을 공유해 화면 관리 모델을 단일화한다.

## 상태

설계 확정 대기(유저 리뷰). 승인 후 구현 계획(`writing-plans`)으로 이행.
