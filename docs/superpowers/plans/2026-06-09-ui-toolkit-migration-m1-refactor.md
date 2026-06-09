# UI Toolkit M1 리팩터 Implementation Plan (레이어×스택 + R3 중심 + UIInstaller)

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans. Steps use checkbox (`- [ ]`).

**Goal:** 기 구현된 M1 UI 인프라를 개정 설계로 리팩터 — 임의 레이어 enum → 원칙적 밴드(`Window/Popup/Notification/System`)×스택 윈도우 매니저, `Open<T>()`(IObjectResolver resolve), 전용 `UIInstaller`(앱 스코프 분리). 동작(로그인 팝업)은 동일 유지.

**Architecture:** `WindowManager`(IWindowManager, UIRoot 프리팹 위 MonoBehaviour)가 밴드별 컨테이너+스택을 관리. `Open<T>()`가 UI 스코프 `IObjectResolver`로 View+ViewModel을 resolve해 `T.Layer` 밴드 스택에 push, 모달이면 백드롭. DI 등록은 `UIInstaller`(루트에 Install). R3 중심 바인딩 유지.

**Tech Stack:** Unity 6000.3.16f1, UI Toolkit, R3(코어), VContainer 1.16.2, UniTask.

---

## 공통 메모
- UnityMCP 호출은 모두 `unity_instance="LeagueOfPhysical-Client@<hash>"` 명시. 컴파일 게이트: `refresh_unity(force,compile)` → idle → `read_console(error)` 0건.
- 단일 Assembly-CSharp 제약 → 런타임 수동 검증이 1차.
- 브랜치: `feature/ui-toolkit-migration-m1` 계속.
- **rename 주의**: `UIManager.cs`→`WindowManager.cs`는 `.cs.meta`를 **함께 rename(GUID 보존)**해야 `UIRoot.prefab`의 `m_Script` 참조가 안 깨진다. MonoBehaviour는 파일명==클래스명 필수.

---

## Task R1: UILayer 밴드 재정의 + UIView에 Layer/IsModal

**Files:** `Assets/Scripts/UI/Core/UILayer.cs`, `UIView.cs`, `UIPopup.cs`

- [ ] **Step 1: UILayer.cs 교체**
```csharp
namespace LOP.UI
{
    /// <summary>UI 밴드. 열거 순서가 z-order(낮음→높음). 하위 밴드는 상위 밴드 위로 못 올라간다.</summary>
    public enum UILayer
    {
        Window = 0,        // 주 화면/페이지
        Popup = 1,         // 모달 다이얼로그 (백드롭)
        Notification = 2,  // 토스트/일시 알림
        System = 3,        // 시스템/치명/항상 최상단
    }
}
```

- [ ] **Step 2: UIView.cs에 Layer/IsModal 추가** (기존 멤버 유지, 아래 두 멤버 추가)
```csharp
        public abstract UILayer Layer { get; }
        public virtual bool IsModal => false;
```
(위치: `public VisualElement Root { get; private set; }` 아래에 추가.)

- [ ] **Step 3: UIPopup.cs 교체**
```csharp
namespace LOP.UI
{
    /// <summary>모달 팝업 베이스. Popup 밴드 + 백드롭. AutoClose면 백드롭 클릭 시 닫힘.</summary>
    public abstract class UIPopup : UIView
    {
        public override UILayer Layer => UILayer.Popup;
        public override bool IsModal => true;
        public virtual bool AutoClose => true;
    }
}
```

- [ ] **Step 4: 컴파일 게이트** — `read_console(error)` 0건. (UIManager가 아직 옛 enum 값 `Hud/Loading/Toast`을 참조하지 않으므로 통과; UIManager는 Enum 순회 + `UILayer.Popup`만 사용.)

- [ ] **Step 5: Commit**
```bash
git add Assets/Scripts/UI/Core/UILayer.cs Assets/Scripts/UI/Core/UIView.cs Assets/Scripts/UI/Core/UIPopup.cs
git commit -m "refactor(ui): UILayer를 원칙적 밴드로 재정의 + UIView.Layer/IsModal"
```

---

## Task R2: UIManager→WindowManager 리네임 + 밴드별 스택 + Open<T>

**Files:** rename `IUIManager.cs`→`IWindowManager.cs`, `UIManager.cs`→`WindowManager.cs` (+`.meta` 동반), 본문 교체.

- [ ] **Step 1: 파일 리네임 (meta 동반, GUID 보존)**
```bash
git mv Assets/Scripts/UI/Core/IUIManager.cs Assets/Scripts/UI/Core/IWindowManager.cs
git mv Assets/Scripts/UI/Core/IUIManager.cs.meta Assets/Scripts/UI/Core/IWindowManager.cs.meta
git mv Assets/Scripts/UI/Core/UIManager.cs Assets/Scripts/UI/Core/WindowManager.cs
git mv Assets/Scripts/UI/Core/UIManager.cs.meta Assets/Scripts/UI/Core/WindowManager.cs.meta
```

- [ ] **Step 2: IWindowManager.cs 본문 교체**
```csharp
namespace LOP.UI
{
    public interface IWindowManager
    {
        /// <summary>T를 UI 스코프에서 resolve(ViewModel 자동 주입) → T.Layer 밴드 스택에 push. 모달이면 백드롭.</summary>
        T Open<T>() where T : UIView;

        /// <summary>닫고 dispose. 밴드 스택 pop + (모달이면) 백드롭 갱신.</summary>
        void Close(UIView view);

        /// <summary>가장 높은 가시 밴드의 top을 닫는다(back/ESC). 닫았으면 true.</summary>
        bool Back();
    }
}
```

- [ ] **Step 3: WindowManager.cs 본문 교체** (직렬화 필드 `document`/`catalog` 이름 유지 → 프리팹 데이터 보존)
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace LOP.UI
{
    /// <summary>
    /// UIRoot 프리팹(UIDocument+PanelSettings) 위 앱 전역 윈도우 매니저.
    /// 밴드(z-order) × 밴드별 스택, 모달 백드롭, Open/Close/Back을 담당.
    /// View 생성은 UI 스코프 IObjectResolver로 resolve(ViewModel 자동 주입).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class WindowManager : MonoBehaviour, IWindowManager
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private UIViewCatalog catalog;
        [Inject] private IObjectResolver resolver;

        private readonly Dictionary<UILayer, VisualElement> _bands = new();
        private readonly Dictionary<UILayer, List<UIView>> _stacks = new();
        private readonly Dictionary<UILayer, VisualElement> _backdrops = new();
        private readonly Dictionary<UIView, VisualElement> _roots = new();

        private void Awake()
        {
            if (document == null) document = GetComponent<UIDocument>();
            var root = document.rootVisualElement;
            root.style.flexGrow = 1;

            foreach (UILayer layer in Enum.GetValues(typeof(UILayer)))
            {
                var band = new VisualElement { name = $"band-{layer}" };
                band.style.position = Position.Absolute;
                band.style.left = 0; band.style.right = 0; band.style.top = 0; band.style.bottom = 0;
                band.pickingMode = PickingMode.Ignore;
                root.Add(band);
                _bands[layer] = band;
                _stacks[layer] = new List<UIView>();
            }
        }

        public T Open<T>() where T : UIView
        {
            var view = resolver.Resolve<T>();

            if (!catalog.TryGet(typeof(T).Name, out var entry) || entry.uxml == null)
            {
                Debug.LogError($"[WindowManager] UIViewCatalog에 '{typeof(T).Name}' UXML 매핑이 없습니다.");
                return view;
            }

            var viewRoot = entry.uxml.Instantiate();
            viewRoot.style.flexGrow = 1;
            if (entry.uss != null) viewRoot.styleSheets.Add(entry.uss);

            view.Initialize(viewRoot);
            _roots[view] = viewRoot;

            var band = _bands[view.Layer];
            band.Add(viewRoot);
            _stacks[view.Layer].Add(view);

            if (view.IsModal) PositionBackdrop(view.Layer);

            view.OnOpen();
            return view;
        }

        public void Close(UIView view)
        {
            if (view == null) return;

            view.OnClose();

            var layer = view.Layer;
            if (_roots.TryGetValue(view, out var viewRoot))
            {
                viewRoot.RemoveFromHierarchy();
                _roots.Remove(view);
            }
            if (_stacks.TryGetValue(layer, out var stack)) stack.Remove(view);

            if (view.IsModal) PositionBackdrop(layer);

            view.Dispose();
        }

        public bool Back()
        {
            var layers = (UILayer[])Enum.GetValues(typeof(UILayer));
            for (int i = layers.Length - 1; i >= 0; i--)
            {
                var stack = _stacks[layers[i]];
                if (stack.Count > 0)
                {
                    Close(stack[stack.Count - 1]);
                    return true;
                }
            }
            return false;
        }

        // 백드롭을 해당 밴드 최상단 모달 바로 아래에 배치(모달 없으면 제거).
        private void PositionBackdrop(UILayer layer)
        {
            var band = _bands[layer];
            var stack = _stacks[layer];

            UIView topModal = null;
            for (int i = stack.Count - 1; i >= 0; i--)
            {
                if (stack[i].IsModal) { topModal = stack[i]; break; }
            }

            if (!_backdrops.TryGetValue(layer, out var backdrop))
            {
                backdrop = CreateBackdrop(layer);
                _backdrops[layer] = backdrop;
            }

            backdrop.RemoveFromHierarchy();
            if (topModal == null || !_roots.TryGetValue(topModal, out var topRoot)) return;

            int idx = band.IndexOf(topRoot);
            if (idx < 0) idx = band.childCount;
            band.Insert(idx, backdrop);
        }

        private VisualElement CreateBackdrop(UILayer layer)
        {
            var b = new VisualElement { name = $"backdrop-{layer}" };
            b.style.position = Position.Absolute;
            b.style.left = 0; b.style.right = 0; b.style.top = 0; b.style.bottom = 0;
            b.style.backgroundColor = new Color(0f, 0f, 0f, 0.5f);
            b.pickingMode = PickingMode.Position;
            b.RegisterCallback<PointerDownEvent>(_ =>
            {
                var stack = _stacks[layer];
                for (int i = stack.Count - 1; i >= 0; i--)
                {
                    if (stack[i].IsModal)
                    {
                        if (stack[i] is UIPopup p && p.AutoClose) Close(stack[i]);
                        break;
                    }
                }
            });
            return b;
        }
    }
}
```

- [ ] **Step 4: 컴파일 게이트** — 이 시점엔 `RootLifetimeScope`/`EntranceLifetimeScope`/`LoginComponent`가 아직 옛 `IUIManager`/`UIManager`/`Open(view,layer)`를 참조해 **에러 발생**한다. R3까지 한 번에 고치므로, R3/R4 적용 후 게이트 통과를 확인한다. (여기선 컴파일 통과를 강제하지 않음 — R4 끝에 통과.)

- [ ] **Step 5: 진행** (커밋은 R4 후 일괄 — 중간 상태가 컴파일 깨짐)

---

## Task R3: UIInstaller 신설 + 스코프 등록 이관

**Files:** new `Assets/Scripts/UI/UIInstaller.cs`; modify `RootLifetimeScope.cs`, `EntranceLifetimeScope.cs`

- [ ] **Step 1: UIInstaller.cs 작성**
```csharp
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP.UI
{
    /// <summary>UI 인프라 DI 등록 모듈. 앱 루트 스코프에서 Install. 앱 스코프와 코드 결합 분리.</summary>
    public class UIInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            var uiRoot = Resources.Load<WindowManager>("UI/UIRoot");
            builder.RegisterComponentInNewPrefab(uiRoot, Lifetime.Singleton)
                .DontDestroyOnLoad()
                .As<IWindowManager>();

            builder.Register<LoginViewModel>(Lifetime.Transient);
            builder.Register<LoginView>(Lifetime.Transient);
        }
    }
}
```

- [ ] **Step 2: RootLifetimeScope.cs 수정** — 기존 UIManager 등록(Resources.Load + RegisterComponentInNewPrefab `RoomConnector` 아래 3줄)을 `builder.Install(new UIInstaller());` 한 줄로 교체.
```csharp
            builder.Register<RoomConnector>(Lifetime.Transient);

            builder.Install(new UIInstaller());

            #region RegisterBuildCallback
```
(`using LOP.UI;`는 유지.)

- [ ] **Step 3: EntranceLifetimeScope.cs 수정** — `LoginViewModel`/`LoginView` 등록 2줄 제거(이제 UIInstaller가 담당). `using LOP.UI;`가 다른 데서 안 쓰이면 제거.
```csharp
            builder.Register<IEntranceComponent, LoadMasterDataComponent>(Lifetime.Transient);
        }
```
(즉 `base.Configure` + 4개 IEntranceComponent 등록만 남고, LoginView/VM 2줄과 불필요해진 `using LOP.UI;` 제거.)

- [ ] **Step 4: 진행** (커밋 R4 후 일괄)

---

## Task R4: LoginComponent를 Open<T>로 + 컴파일 게이트 + 커밋

**Files:** `Assets/Scripts/Entrance/EntranceComponent/LoginComponent.cs`

- [ ] **Step 1: LoginComponent.cs 교체**
```csharp
using GameFramework;
using LOP.UI;
using R3;
using System;
using System.Threading.Tasks;
using VContainer;

namespace LOP
{
    public class LoginComponent : IEntranceComponent
    {
        [Inject] private IWindowManager windowManager;

        public async Task Execute()
        {
            var autoLoginResult = await LoginService.instance.TryAutoLogin();
            if (autoLoginResult.success)
            {
                return;
            }

            var view = windowManager.Open<LoginView>();

            LoginType loginType = await view.ViewModel.OnLoginRequested.FirstAsync();

            LoginResult loginResult = LoginService.instance.Login(loginType);

            windowManager.Close(view);

            if (loginResult.success == false)
            {
                throw new Exception(loginResult.reason);
            }
        }
    }
}
```

- [ ] **Step 2: 컴파일 게이트** — `refresh_unity(force,compile)` → idle → `read_console(error)` **0건**. (`LoginView`는 `UIPopup` 상속이라 Layer/IsModal 자동, 변경 불필요.) 에러 시 잔여 `IUIManager`/`UIManager`/`Open(view,layer)` 참조를 찾아 정리.

- [ ] **Step 3: 프리팹 m_Script 참조 무결성 확인** — `Assets/Resources/UI/UIRoot.prefab`의 `WindowManager` 컴포넌트 `m_Script` guid가 보존됐는지(=`WindowManager.cs.meta` guid와 일치) 확인. `read_console(all)`에 "missing script"/import 에러 0건.

- [ ] **Step 4: Commit (R2~R4 일괄)**
```bash
git add -A -- Assets/Scripts/UI Assets/Scripts/RootLifetimeScope.cs Assets/Scripts/Entrance/EntranceLifetimeScope.cs Assets/Scripts/Entrance/EntranceComponent/LoginComponent.cs
git commit -m "refactor(ui): WindowManager(밴드x스택) + Open<T> + UIInstaller; LoginComponent 갱신"
```

---

## Task R5: 런타임 재검증

**Files:** (코드 변경 없음; 임시 디버그 메뉴는 검증 후 제거)

- [ ] **Step 1: 캐시 로그인 제거** — 이전 검증에서 Guest 로그인이 `LOGIN_TYPE_KEY`를 다시 설정했으므로 자동 로그인이 성공함. 임시 에디터 메뉴로 제거:
  - `Assets/Editor/DevLoginMenu.cs` 작성 (`[MenuItem("LOP/Dev/Clear Login PlayerPrefs")]` → `PlayerPrefs.DeleteKey("LOGIN_TYPE_KEY"); PlayerPrefs.Save();`), 컴파일, `execute_menu_item("LOP/Dev/Clear Login PlayerPrefs")`, 로그 확인.

- [ ] **Step 2: Play 모드 진입 + 콘솔** — `manage_editor(play)`, `read_console(error)` 0건. (도메인 리로드 후 인스턴스 재조회.)

- [ ] **Step 3: 스크린샷 확인** — `manage_camera(screenshot, game_view, include_image)` → 로그인 팝업(모달 백드롭 + Guest 버튼)이 `Popup` 밴드에 표시되는지.

- [ ] **Step 4: 사용자 클릭 검증** — Guest 클릭 → 팝업 닫힘 → Lobby 진행 → 에러 0건 (사용자 확인).

- [ ] **Step 5: 정리** — Play stop, `Assets/Editor/DevLoginMenu.cs`(+meta) 삭제, `Assets/Screenshots` 정리, 컴파일 게이트, 필요시 커밋.

---

## Self-Review
- **밴드×스택**: R1(enum+Layer/IsModal) + R2(WindowManager 밴드별 스택/백드롭) ✅
- **R3 중심**: ViewModel R3 Subject + View 구독 유지(LoginView OnOpen), Open<T> 후 `OnLoginRequested.FirstAsync()` ✅
- **UIInstaller**: R3(신설 + Root Install + Entrance 등록 제거) ✅
- **Open<T> resolve**: WindowManager.[Inject] IObjectResolver + UIInstaller가 같은 루트 컨테이너에 View 등록 → resolve 가능 ✅
- **rename 안전**: .meta GUID 보존(git mv) → 프리팹 m_Script 무결 (R4 Step3 확인) ✅
- **타입 일관성**: `IWindowManager.Open<T>()`/`Close`/`Back` ↔ WindowManager ↔ LoginComponent 일치. `UIView.Layer/IsModal` ↔ UIPopup ↔ LoginView 일치. `catalog.TryGet`/`Entry.uxml/uss` 유지.
