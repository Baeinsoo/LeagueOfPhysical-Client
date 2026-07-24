# Flow Slice C — 프론트엔드 네비게이션(상점/설정/프로필 셸) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 로비 홈 하단 네비바(상점/설정/프로필)를 실제로 배선한다 — 버튼 → `FrontEndCoordinator`가 해당 셸 윈도우를 열고, 셸의 back으로 로비 홈 복귀.

**Architecture:** `LobbyHomeViewModel`(신규)이 네비 신호(R3 `Observable<FrontEndDestination>`)를 노출 → `FrontEndCoordinator`(신규, Lobby 스코프 엔트리포인트)가 구독해 `WindowManager`로 셸 윈도우(`Window` 밴드) push, 셸의 back → close(pop). 셸 3개는 거의 동일한 플레이스홀더라 공유 `ShellView` 베이스 + 얇은 3서브클래스(`ShopView`/`SettingsView`/`ProfileView`) + 공유 UXML. **방금 머지된 `MatchmakingCoordinator`가 이 코디네이터 패턴의 템플릿.**

**Tech Stack:** Unity UI Toolkit(UXML/USS + `UIViewCatalog`), VContainer(엔트리포인트), R3(`Observable`/`Subject`), 기존 `WindowManager`/`UIView`.

## Global Constraints

- **동작 보존**: 로비 홈의 PLAY→매칭 흐름은 변화 없음(Slice B 그대로). 이번엔 네비바만 추가.
- **피처 브랜치**에서 작업(신규 브랜치 `feature/flow-slice-c-frontend-nav`). main 직접 커밋 금지.
- **새 .cs/.uxml/.uss는 Unity 생성 .meta와 함께 커밋**(폴더 .meta 포함 — Slice B에서 폴더 메타 누락 겪음). `.meta` 직접 생성 금지.
- **테스트 전략 = 컴파일 체크 + 플레이테스트**(클라 단위 테스트 인프라 없음). 각 변경 후 Unity 컴파일 0 확인 + 마지막에 네비 플레이테스트.
- **UnityMCP 대상 고정**: 모든 호출에 `unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4"` 명시(id 틀리면 에러 메시지가 실제 해시 노출). general-purpose 서브에이전트는 `mcpforunity://instances` resource를 못 읽으니 이 id를 직접 사용.
- **뷰 = 얇은 바인더 / VM = 신호 노출 / 코디네이터 = 네비게이션**(아키텍처 규칙). 셸은 내용 없는 플레이스홀더(back만).
- 셸 back 콜백은 `MatchingWaitingView.SetCancelCallback` 패턴을 따른다(코디네이터가 콜백 주입).

## 범위 밖 (후속)

- 각 셸의 실제 내용(상점 품목·설정 항목·프로필 데이터) — 화면별 콘텐츠 스펙.
- 결과 화면(Slice D), 앱 FSM 씬 전환 일원화(Slice A).
- 네비 전환 애니메이션.

---

## 파일 구조

| 파일 | 책임 | 변경 |
|---|---|---|
| `Assets/UI/Shell/ShellView.uxml` | 셸 공유 레이아웃(title `shell-title` + `back-button`) | 신규 |
| `Assets/UI/Shell/ShellView.uss` | 셸 공유 스타일 | 신규 |
| `Assets/UI/UIViewCatalog.asset` | `ShopView`/`SettingsView`/`ProfileView` 3엔트리(모두 ShellView uxml/uss) | 수정 |
| `Assets/Scripts/UI/Shell/ShellView.cs` | 셸 베이스: title 세팅 + `SetBackCallback` + back 버튼 | 신규 |
| `Assets/Scripts/UI/Shell/ShopView.cs` `SettingsView.cs` `ProfileView.cs` | 얇은 서브클래스(title만) | 신규 |
| `Assets/Scripts/UI/LobbyHome/FrontEndDestination.cs` | enum {Shop, Settings, Profile} | 신규 |
| `Assets/Scripts/UI/LobbyHome/LobbyHomeViewModel.cs` | 네비 신호 노출(`Observable<FrontEndDestination>` + `Navigate(dest)`) | 신규 |
| `Assets/Scripts/UI/LobbyHome/LobbyHomeView.cs` | 네비 버튼 → `LobbyHomeViewModel.Navigate` 배선 추가 | 수정 |
| `Assets/Scripts/UI/LobbyHome/FrontEndCoordinator.cs` | 네비 신호 구독 → 셸 open/pop | 신규 |
| `Assets/Scripts/Lobby/LobbyLifetimeScope.cs` | VM·셸·코디네이터 등록 | 수정 |

---

## Task 1: ShellView UXML/USS (공유 셸 레이아웃)

**Files:**
- Create: `Assets/UI/Shell/ShellView.uxml`, `Assets/UI/Shell/ShellView.uss`

**Interfaces:**
- Produces: UXML 요소 `shell-title`(Label), `back-button`(Button) — Task 2의 ShellView가 조회.

- [x] **Step 1: `ShellView.uxml`**

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement name="shell-root" class="shell-root">
        <ui:VisualElement name="shell-header" class="shell-header">
            <ui:Button name="back-button" text="◀ 뒤로" class="shell-back" />
            <ui:Label name="shell-title" text="" class="shell-title" />
        </ui:VisualElement>
        <ui:VisualElement name="shell-body" class="shell-body">
            <ui:Label text="(준비 중)" class="shell-placeholder" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

- [x] **Step 2: `ShellView.uss`**

```css
.shell-root { flex-grow: 1; background-color: rgb(30, 30, 38); }
.shell-header { flex-direction: row; align-items: center; height: 72px; padding: 12px; }
.shell-back { width: 96px; height: 48px; }
.shell-title { flex-grow: 1; font-size: 28px; -unity-font-style: bold; -unity-text-align: middle-center; color: white; }
.shell-body { flex-grow: 1; justify-content: center; align-items: center; }
.shell-placeholder { font-size: 22px; color: rgb(150, 150, 160); }
```

- [x] **Step 3: Unity 임포트 + 커밋**

`refresh_unity`(scope=all, mode=force, unity_instance 위 id) → `editor_state.isCompiling` false → `read_console`(errors) 0. `git status`로 `.uxml.meta`·`.uss.meta` + 폴더 `Assets/UI/Shell.meta` 생성 확인(폴더 메타도 커밋).
```bash
git add Assets/UI/Shell/ Assets/UI/Shell.meta
git commit -m "feat(ui): 셸 공유 UXML/USS(title + back)"
```

---

## Task 2: ShellView 베이스 + 3 서브클래스

**Files:**
- Create: `Assets/Scripts/UI/Shell/ShellView.cs`, `ShopView.cs`, `SettingsView.cs`, `ProfileView.cs`

**Interfaces:**
- Consumes: Task 1의 `shell-title`/`back-button`, 기존 `UIView`/`UILayer`.
- Produces: `abstract ShellView : UIView` (`void SetBackCallback(System.Action)`, `abstract string Title`); `ShopView`/`SettingsView`/`ProfileView : ShellView`. Task 4/5가 `WindowManager.Open<ShopView>()` 등으로 사용.

- [x] **Step 1: `ShellView.cs` (베이스)**

```csharp
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 프론트엔드 셸(상점/설정/프로필) 공유 베이스. title을 세팅하고 back 버튼을 콜백에 잇는 얇은 바인더.
    /// 여는 쪽(FrontEndCoordinator)이 SetBackCallback으로 닫기 동작을 배선한다(MatchingWaitingView 패턴).
    /// 내용은 플레이스홀더 — 화면별 콘텐츠는 후속 스펙.
    /// </summary>
    public abstract class ShellView : UIView
    {
        private Button _backButton;
        private System.Action _onBack;

        public override UILayer Layer => UILayer.Window;

        /// <summary>헤더에 표시할 셸 제목(서브클래스가 지정).</summary>
        protected abstract string Title { get; }

        public void SetBackCallback(System.Action onBack) => _onBack = onBack;

        public override void OnOpen()
        {
            base.OnOpen();

            var titleLabel = Root.Q<Label>("shell-title");
            if (titleLabel != null) titleLabel.text = Title;

            _backButton = Root.Q<Button>("back-button");
            _backButton.clicked += OnBackClicked;
        }

        public override void OnClose()
        {
            if (_backButton != null) _backButton.clicked -= OnBackClicked;
            base.OnClose();
        }

        private void OnBackClicked() => _onBack?.Invoke();
    }
}
```

- [x] **Step 2: 3 서브클래스**

`Assets/Scripts/UI/Shell/ShopView.cs`:
```csharp
namespace LOP.UI
{
    /// <summary>상점 셸(플레이스홀더). 내용은 후속 스펙.</summary>
    public class ShopView : ShellView
    {
        protected override string Title => "상점";
    }
}
```
`SettingsView.cs`:
```csharp
namespace LOP.UI
{
    /// <summary>설정 셸(플레이스홀더). 내용은 후속 스펙.</summary>
    public class SettingsView : ShellView
    {
        protected override string Title => "설정";
    }
}
```
`ProfileView.cs`:
```csharp
namespace LOP.UI
{
    /// <summary>프로필 셸(플레이스홀더). 내용은 후속 스펙.</summary>
    public class ProfileView : ShellView
    {
        protected override string Title => "프로필";
    }
}
```

- [x] **Step 3: 임포트 + 커밋**

`refresh_unity`(위 id) → `read_console` 0 errors. `.cs.meta` 4개 + 폴더 `Assets/Scripts/UI/Shell.meta` 생성 확인.
```bash
git add Assets/Scripts/UI/Shell/ Assets/Scripts/UI/Shell.meta
git commit -m "feat(ui): ShellView 베이스 + Shop/Settings/Profile 서브클래스(플레이스홀더)"
```

---

## Task 3: 카탈로그에 3 셸 등록 (모두 ShellView uxml/uss)

**Files:**
- Modify: `Assets/UI/UIViewCatalog.asset`

**Interfaces:**
- Consumes: Task 1 `ShellView.uxml`/`.uss`, Task 2 뷰 타입명.
- Produces: `viewName: ShopView`/`SettingsView`/`ProfileView` 3엔트리 — 모두 ShellView의 uxml/uss GUID를 참조(한 uxml을 여러 viewName이 공유).

> `Open<ShopView>()` → `typeof(ShopView).Name`="ShopView" 조회. 3엔트리 모두 같은 ShellView.uxml/uss GUID를 가리키면 됨(카탈로그는 viewName→에셋 매핑이라 공유 허용).

- [x] **Step 1: GUID 확보 + 3엔트리 추가**

`Assets/UI/Shell/ShellView.uxml.meta`·`ShellView.uss.meta`의 `guid:` 읽기. 기존 엔트리(예: LobbyHomeView) 형식을 본떠 `UIViewCatalog.asset`의 `entries`에 3항목 추가:
- `viewName: ShopView` / `viewName: SettingsView` / `viewName: ProfileView` — 각각 `uxml`=ShellView.uxml GUID, `uss`=ShellView.uss GUID, `fileID`/`type`은 기존 엔트리와 동일.

(UnityMCP `manage_scriptable_object` 또는 기존 엔트리 YAML 미러링. 기존 엔트리는 건드리지 않음.)

- [x] **Step 2: 임포트 확인 + 커밋**

`refresh_unity`(위 id) → `read_console` 0 errors. diff에 3엔트리 추가·기존 엔트리 불변 확인.
```bash
git add Assets/UI/UIViewCatalog.asset
git commit -m "feat(ui): UIViewCatalog에 Shop/Settings/Profile 셸 매핑 추가"
```

---

## Task 4: LobbyHomeViewModel (네비 신호) + FrontEndDestination

**Files:**
- Create: `Assets/Scripts/UI/LobbyHome/FrontEndDestination.cs`, `Assets/Scripts/UI/LobbyHome/LobbyHomeViewModel.cs`

**Interfaces:**
- Produces: `enum FrontEndDestination { Shop, Settings, Profile }`; `LobbyHomeViewModel : IDisposable` — `ReadOnlyReactiveProperty`가 아니라 일회성 네비 신호이므로 `Observable<FrontEndDestination> NavigationRequested` + `void Navigate(FrontEndDestination)`. Task 5(View)가 `Navigate` 호출, Task 6(Coordinator)이 `NavigationRequested` 구독.

- [x] **Step 1: `FrontEndDestination.cs`**

```csharp
namespace LOP.UI
{
    /// <summary>로비 홈에서 이동 가능한 프론트엔드 목적지.</summary>
    public enum FrontEndDestination
    {
        Shop,
        Settings,
        Profile,
    }
}
```

- [x] **Step 2: `LobbyHomeViewModel.cs`**

```csharp
using System;
using R3;

namespace LOP.UI
{
    /// <summary>
    /// 로비 홈 허브의 ViewModel. 네비게이션 "요청"을 도메인 신호로만 노출한다(화면 교체는 하지 않음).
    /// 신호를 듣고 실제 윈도우를 여는 것은 FrontEndCoordinator의 책임(아키텍처: VM=신호 / 코디네이터=네비게이션).
    /// 일회성 이벤트라 ReactiveProperty가 아니라 Subject/Observable을 쓴다.
    /// </summary>
    public class LobbyHomeViewModel : IDisposable
    {
        private readonly Subject<FrontEndDestination> _navigationRequested = new();

        /// <summary>네비 버튼이 눌렸을 때의 목적지 신호. FrontEndCoordinator가 구독한다.</summary>
        public Observable<FrontEndDestination> NavigationRequested => _navigationRequested;

        /// <summary>네비 커맨드(View가 버튼 클릭 시 호출).</summary>
        public void Navigate(FrontEndDestination destination) => _navigationRequested.OnNext(destination);

        public void Dispose()
        {
            _navigationRequested.Dispose();
        }
    }
}
```

- [x] **Step 3: 임포트 + 커밋**

`refresh_unity`(위 id) → `read_console` 0 errors. `.cs.meta` 2개 생성 확인.
```bash
git add Assets/Scripts/UI/LobbyHome/FrontEndDestination.cs Assets/Scripts/UI/LobbyHome/FrontEndDestination.cs.meta \
        Assets/Scripts/UI/LobbyHome/LobbyHomeViewModel.cs Assets/Scripts/UI/LobbyHome/LobbyHomeViewModel.cs.meta
git commit -m "feat(ui): LobbyHomeViewModel 네비 신호(NavigationRequested)"
```

---

## Task 5: LobbyHomeView에 네비 버튼 배선

**Files:**
- Modify: `Assets/Scripts/UI/LobbyHome/LobbyHomeView.cs`

**Interfaces:**
- Consumes: `LobbyHomeViewModel`(Task 4, `Navigate`), 기존 `MatchmakingViewModel`(Play), UXML 버튼 `nav-shop`/`nav-settings`/`nav-profile`(Slice B에서 배치됨).

- [x] **Step 1: `LobbyHomeView.cs` 전체 교체**

```csharp
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 로비 홈 허브 View(프론트엔드 베이스). Play는 매칭 커맨드, 네비바 버튼은 LobbyHomeViewModel 네비 커맨드로
    /// 전달하는 얇은 바인더. 매칭 흐름·대기 오버레이는 MatchmakingCoordinator, 네비 화면 교체는 FrontEndCoordinator가 담당.
    /// </summary>
    public class LobbyHomeView : UIView
    {
        private readonly MatchmakingViewModel _matchmaking;
        private readonly LobbyHomeViewModel _viewModel;

        private Button _playButton;
        private Button _shopButton;
        private Button _settingsButton;
        private Button _profileButton;

        public LobbyHomeView(MatchmakingViewModel matchmaking, LobbyHomeViewModel viewModel)
        {
            _matchmaking = matchmaking;
            _viewModel = viewModel;
        }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();

            _playButton = Root.Q<Button>("play-button");
            _shopButton = Root.Q<Button>("nav-shop");
            _settingsButton = Root.Q<Button>("nav-settings");
            _profileButton = Root.Q<Button>("nav-profile");

            _playButton.clicked += OnPlayClicked;
            _shopButton.clicked += OnShopClicked;
            _settingsButton.clicked += OnSettingsClicked;
            _profileButton.clicked += OnProfileClicked;
        }

        public override void OnClose()
        {
            if (_playButton != null) _playButton.clicked -= OnPlayClicked;
            if (_shopButton != null) _shopButton.clicked -= OnShopClicked;
            if (_settingsButton != null) _settingsButton.clicked -= OnSettingsClicked;
            if (_profileButton != null) _profileButton.clicked -= OnProfileClicked;
            base.OnClose();
        }

        private void OnPlayClicked() => _matchmaking.Play();
        private void OnShopClicked() => _viewModel.Navigate(FrontEndDestination.Shop);
        private void OnSettingsClicked() => _viewModel.Navigate(FrontEndDestination.Settings);
        private void OnProfileClicked() => _viewModel.Navigate(FrontEndDestination.Profile);
    }
}
```

- [x] **Step 2: 임포트 확인 + 커밋** (등록은 Task 7에서, 아직 LobbyHomeViewModel 미등록이라 컴파일만 확인)

`refresh_unity`(위 id) → `read_console` 0 errors.
```bash
git add Assets/Scripts/UI/LobbyHome/LobbyHomeView.cs
git commit -m "feat(ui): LobbyHomeView 네비 버튼 → LobbyHomeViewModel.Navigate 배선"
```

---

## Task 6: FrontEndCoordinator (신호 → 셸 open/pop)

**Files:**
- Create: `Assets/Scripts/UI/LobbyHome/FrontEndCoordinator.cs`

**Interfaces:**
- Consumes: `IWindowManager`(`Open<T>`/`Close`), `LobbyHomeViewModel`(`NavigationRequested`), `ShopView`/`SettingsView`/`ProfileView`(Task 2), R3.
- Produces: `FrontEndCoordinator : IStartable, IDisposable` (ctor `(IWindowManager, LobbyHomeViewModel)`).

> 한 번에 하나의 셸만 연다: 새 목적지 요청 시 열린 셸이 있으면 닫고 새로 연다. 셸 back → 그 셸 닫기(로비 홈 복귀).

- [x] **Step 1: `FrontEndCoordinator.cs`**

```csharp
using R3;
using System;
using VContainer.Unity;

namespace LOP.UI
{
    /// <summary>
    /// 프론트엔드 네비게이션 담당. LobbyHomeViewModel의 네비 신호를 구독해 상점/설정/프로필 셸 윈도우를 열고,
    /// 셸의 back으로 닫는다(로비 홈 위 push/pop). VM은 신호만 노출, 화면 교체는 여기서(작은 흐름=VM / 큰 흐름=코디네이터).
    /// MatchmakingCoordinator와 같은 코디네이터 패턴.
    /// </summary>
    public class FrontEndCoordinator : IStartable, IDisposable
    {
        private readonly IWindowManager _windowManager;
        private readonly LobbyHomeViewModel _viewModel;

        private IDisposable _subscription;
        private ShellView _currentShell;

        public FrontEndCoordinator(IWindowManager windowManager, LobbyHomeViewModel viewModel)
        {
            _windowManager = windowManager;
            _viewModel = viewModel;
        }

        public void Start()
        {
            _subscription = _viewModel.NavigationRequested.Subscribe(OnNavigationRequested);
        }

        private void OnNavigationRequested(FrontEndDestination destination)
        {
            CloseCurrentShell();

            _currentShell = destination switch
            {
                FrontEndDestination.Shop => _windowManager.Open<ShopView>(),
                FrontEndDestination.Settings => _windowManager.Open<SettingsView>(),
                FrontEndDestination.Profile => _windowManager.Open<ProfileView>(),
                _ => null,
            };

            _currentShell?.SetBackCallback(CloseCurrentShell);
        }

        private void CloseCurrentShell()
        {
            if (_currentShell != null)
            {
                _windowManager.Close(_currentShell);
                _currentShell = null;
            }
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            CloseCurrentShell();
        }
    }
}
```

- [x] **Step 2: 임포트 확인 + 커밋** (등록은 Task 7)

`refresh_unity`(위 id) → `read_console` 0 errors.
```bash
git add Assets/Scripts/UI/LobbyHome/FrontEndCoordinator.cs Assets/Scripts/UI/LobbyHome/FrontEndCoordinator.cs.meta
git commit -m "feat(ui): FrontEndCoordinator(네비 신호 → 셸 open/pop)"
```

---

## Task 7: LobbyLifetimeScope 등록

**Files:**
- Modify: `Assets/Scripts/Lobby/LobbyLifetimeScope.cs`

**Interfaces:**
- Consumes: Task 2 셸 뷰, Task 4 `LobbyHomeViewModel`, Task 6 `FrontEndCoordinator`.
- Produces: 네비가 실제로 동작(로비 홈 네비 버튼 → 셸 open/pop).

> `LobbyHomeViewModel`은 View·Coordinator가 공유해야 하므로 **Scoped**. 셸 뷰는 Transient + 각 팩토리 기여(코디네이터가 `Open<T>` 시 이 스코프 resolver 사용). `FrontEndCoordinator`는 엔트리포인트.

- [x] **Step 1: 등록 추가**

`LobbyLifetimeScope.Configure`에 다음을 추가한다(기존 Matchmaking 등록·빌드콜백은 유지). VM·셸 등록은 빌드콜백 앞, 셸 팩토리 기여는 빌드콜백 안:

```csharp
            // 프론트엔드 네비(Slice C)
            builder.Register<LobbyHomeViewModel>(Lifetime.Scoped);   // View·Coordinator 공유
            builder.Register<ShopView>(Lifetime.Transient);
            builder.Register<SettingsView>(Lifetime.Transient);
            builder.Register<ProfileView>(Lifetime.Transient);
            builder.RegisterEntryPoint<FrontEndCoordinator>();
```

그리고 빌드콜백 안(기존 `Open<LobbyHomeView>()` 근처)에서 셸 팩토리 3개를 WindowManager에 기여하고, 로비 스코프 파괴 시 해제하도록 핸들을 보관한다. 필드 추가:
```csharp
        private IDisposable _shopViewRegistration;
        private IDisposable _settingsViewRegistration;
        private IDisposable _profileViewRegistration;
```
빌드콜백 안(windowManager 확보 후):
```csharp
                _shopViewRegistration = windowManager.RegisterViewFactory<ShopView>(() => container.Resolve<ShopView>());
                _settingsViewRegistration = windowManager.RegisterViewFactory<SettingsView>(() => container.Resolve<SettingsView>());
                _profileViewRegistration = windowManager.RegisterViewFactory<ProfileView>(() => container.Resolve<ProfileView>());
```
`OnDestroy`에서 기존 `_lobbyHomeViewRegistration?.Dispose();` 뒤에:
```csharp
            _shopViewRegistration?.Dispose();
            _settingsViewRegistration?.Dispose();
            _profileViewRegistration?.Dispose();
```

- [x] **Step 2: 컴파일 확인 + 커밋**

`refresh_unity`(위 id) → `read_console` 0 errors.
```bash
git add Assets/Scripts/Lobby/LobbyLifetimeScope.cs
git commit -m "feat(ui): FrontEnd 네비 등록(LobbyHomeViewModel·셸·FrontEndCoordinator)"
```

---

## Task 8: 플레이테스트

**Files:** (없음 — 검증)

> **선행**: 로컬 서버 필요. 백엔드 불가 시 사용자 수동 검증.

- [ ] **Step 1: 로비 진입 → 네비**

플레이 → 로비 홈. 하단 **상점/프로필/설정** 버튼 클릭 → 각 셸(제목 + "(준비 중)" + 뒤로) 열림. **뒤로** → 로비 홈 복귀. 다른 셸로 바로 전환 시 이전 셸 닫히고 새 셸 열림.
Expected: 셸 open/pop 정상, `read_console` 예외 0.

- [ ] **Step 2: Play 보존 확인**

PLAY → 매칭 대기 오버레이 정상(Slice B 동작 불변).
Expected: 매칭 정상, 예외 0.

- [ ] **Step 3: 로비 이탈 정리**

로비를 벗어날 때(매칭 성사 등) 열린 셸이 남지 않고 `FrontEndCoordinator`/`LobbyHomeViewModel`이 정리되는지(Dispose 예외 0).

---

## 완료 기준

- 로비 홈 네비바 버튼 → 해당 셸 윈도우 open, 셸 back → 로비 홈 복귀, 한 번에 하나의 셸.
- PLAY→매칭 동작 보존, 컴파일 0 errors.
- 셸 내용은 플레이스홀더(후속), 결과 화면=Slice D, 앱 FSM=Slice A.
