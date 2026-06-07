# UI Toolkit 전환 M1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 클라 로그인 팝업을 UI Toolkit + MVVM + R3로 전환하고, 이후 모든 화면이 재사용할 일반형 UI 관리 인프라(`UIManager`: 레이어 + 모달 스택 + 비동기 결과 + 라이프사이클)를 클라에 구축한다.

**Architecture:** 단일 `UIRoot` 프리팹(UIDocument + screen-space PanelSettings) 위의 `UIManager`(MonoBehaviour)를 `RootLifetimeScope`에서 앱 전역 싱글톤으로 등록한다. UI는 순수 C# `UIView`/`UIPopup` 컨트롤러 + `UIViewCatalog`(ScriptableObject)가 매핑하는 UXML/USS로 구성하며, ViewModel(순수 C#, R3 `ReactiveProperty`/`Subject` 소유)에 바인딩한다. DI 해소는 화면을 쓰는 스코프(여기선 Entrance)에서 수행하고, `UIManager`는 레이아웃·모달 스택·라이프사이클만 책임진다(부모/자식 스코프 가시성 문제 회피).

**Tech Stack:** Unity 6000.3.16f1, UI Toolkit(`com.unity.modules.uielements`), R3(신규), VContainer 1.16.2, UniTask, GameFramework(공유 패키지 — 이번엔 미변경).

---

## 사전 메모 (모든 태스크 공통)

- **UnityMCP 타게팅**: 모든 UnityMCP 호출에 `unity_instance`를 명시한다. `mcpforunity://instances`에서 `name == "LeagueOfPhysical-Client"`인 인스턴스의 전체 `id`(`Name@hash`)를 찾아 사용. 서버 인스턴스에 절대 작업하지 않는다.
- **컴파일 검증**: 스크립트 변경 후 `refresh_unity` → `editor_state`의 `isCompiling`이 false가 될 때까지 대기 → `read_console`(error 필터)로 0 에러 확인. (모두 `unity_instance` 명시.)
- **테스트 제약(중요)**: 클라 전 코드가 단일 `Assembly-CSharp`라 EditMode `asmdef`가 직접 참조 불가(문서화된 기존 제약). M1은 **컴파일 게이트 + 런타임 수동 검증**을 1차 검증으로 삼는다(이전 World Core 슬라이스들과 동일 기조). 강제 단위 테스트를 만들지 않는다.
- **R3/UI Toolkit API 미확정 리스크**: R3의 정확한 disposable/await API와 UI Toolkit 일부 API는 Task 1/Task 4 검증 단계에서 컴파일로 확정한다. 컴파일 에러 시 해당 태스크의 코드를 R3/UITK 실제 시그니처에 맞춰 조정한다.
- **네임스페이스**: 모든 신규 클라 코드는 `namespace LOP` 또는 `namespace LOP.UI`(UI 인프라). 기존 코드가 `namespace LOP`이므로 호출부 충돌 없음.
- **브랜치**: 작업은 `feature/ui-toolkit-migration-m1`(이미 생성됨)에서. 각 태스크 끝 커밋.

---

## File Structure

신규 (클라):
- `Assets/Scripts/UI/Core/UILayer.cs` — 레이어 enum
- `Assets/Scripts/UI/Core/UIViewCatalog.cs` — view 이름 → UXML/USS 매핑 SO
- `Assets/Scripts/UI/Core/UIView.cs` — 순수 C# 뷰 베이스
- `Assets/Scripts/UI/Core/UIPopup.cs` — 모달 팝업 베이스
- `Assets/Scripts/UI/Core/IUIManager.cs` — 매니저 인터페이스
- `Assets/Scripts/UI/Core/UIManager.cs` — 매니저 구현(MonoBehaviour)
- `Assets/Scripts/UI/Login/LoginViewModel.cs`
- `Assets/Scripts/UI/Login/LoginView.cs`
- `Assets/UI/Login/LoginView.uxml`
- `Assets/UI/Login/LoginView.uss`
- 에셋: `Assets/UI/UIPanelSettings.asset`(PanelSettings), `Assets/UI/UIViewCatalog.asset`, `Assets/UI/UIRoot.prefab`

수정 (클라):
- `Assets/Scripts/RootLifetimeScope.cs` — UIRoot 프리팹 등록 + 직렬화 필드
- `Assets/Scripts/Entrance/EntranceLifetimeScope.cs` — `LoginView`/`LoginViewModel` 등록
- `Assets/Scripts/Entrance/EntranceComponent/LoginComponent.cs` — 재작성
- `Packages/manifest.json`, `Assets/NuGetForUnity/packages.config` — R3 추가
- `Assets/Resources/PrefabReferences.asset` — LoginPopup 엔트리 제거

제거 (클라):
- `Assets/Scripts/Popup/LoginPopup.cs`(+`.meta`)
- `Assets/Popups/LoginPopup.prefab`(+`.meta`)

미변경: `CanvasManager`, GameFramework `PopupManager`/`Popup`/`PrefabReferences`(StatsPopup 등 아직 사용).

---

## Phase A — R3 도입

### Task 1: R3 패키지 추가 및 공존 검증

**Files:**
- Modify: `Packages/manifest.json`
- Modify: `Assets/NuGetForUnity/packages.config`

- [ ] **Step 1: R3 코어를 NuGetForUnity로 추가**

`Assets/NuGetForUnity/packages.config`에 R3 코어와 전이 의존을 추가한다. AutoMapper/Protobuf 항목과 같은 형식으로:

```xml
<package id="R3" version="1.2.9" manuallyInstalled="true" />
<package id="ObservableCollections" version="3.3.3" />
<package id="Microsoft.Bcl.TimeProvider" version="8.0.1" />
```

(정확한 최신 호환 버전은 Unity의 NuGet → Manage NuGet Packages에서 `R3` 검색으로 확정. `System.Runtime.CompilerServices.Unsafe`는 이미 존재.)

- [ ] **Step 2: R3 Unity 통합(UPM)을 manifest에 추가**

`Packages/manifest.json`의 `dependencies`에 R3.Unity 통합 패키지를 추가(`AddTo`, Unity frame provider, `ObservableTrackerWindow` 제공):

```json
"com.cysharp.r3": "https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity",
```

- [ ] **Step 3: Unity에서 NuGet 복원 + 컴파일**

Unity Editor에서 NuGet → Manage NuGet Packages → Restore로 R3 코어 DLL을 복원한다. `refresh_unity` 후 `isCompiling`이 끝나길 대기.

- [ ] **Step 4: R3 + UniRx 공존 컴파일 확인**

`read_console`(error/warning) 확인. 기대: **0 컴파일 에러**. 기존 UniRx 코드(`LOPEntityView`, `MatchMakingPresenter` 등)도 그대로 컴파일되어야 함(공존).

검증 보조 — 임시 스모크 스크립트를 만들어 R3 기본 API가 컴파일되는지 확인:

```csharp
// Assets/Scripts/UI/_R3Smoke.cs  (검증 후 삭제)
using R3;
namespace LOP.UI
{
    internal static class _R3Smoke
    {
        internal static System.Threading.Tasks.Task<int> Probe()
        {
            var subject = new Subject<int>();
            var disposables = new CompositeDisposable();
            subject.Subscribe(_ => { }).AddTo(disposables);
            var task = subject.FirstAsync();
            subject.OnNext(1);
            disposables.Dispose();
            return task;
        }
    }
}
```

`refresh_unity` → `read_console`. 기대: 0 에러. **만약 `CompositeDisposable`/`AddTo`/`FirstAsync` 시그니처가 다르면**(R3 버전차) 여기서 실제 API를 확인하고, 이후 태스크들의 R3 사용부(`LoginViewModel`, `LoginView`, `LoginComponent`)를 동일 시그니처로 맞춘다. 확인 후 `_R3Smoke.cs`(+`.meta`) 삭제.

- [ ] **Step 5: Commit**

```bash
git add Packages/manifest.json Assets/NuGetForUnity/packages.config Assets/NuGetForUnity/
git commit -m "build(ui): add R3 (coexists with UniRx) for UI Toolkit migration"
```

---

## Phase B — UI 관리 인프라

### Task 2: UILayer enum + UIViewCatalog ScriptableObject

**Files:**
- Create: `Assets/Scripts/UI/Core/UILayer.cs`
- Create: `Assets/Scripts/UI/Core/UIViewCatalog.cs`

- [ ] **Step 1: UILayer 작성**

```csharp
namespace LOP.UI
{
    /// <summary>UI 레이어. 열거 순서가 z-order(낮음→높음).</summary>
    public enum UILayer
    {
        Hud = 0,     // (M3) 인게임 월드/오버레이
        Popup = 1,   // 모달 팝업 스택
        Loading = 2, // (M2) 로딩
        Toast = 3,   // (M2) 토스트
        System = 4,  // (M2) 시스템
    }
}
```

- [ ] **Step 2: UIViewCatalog 작성**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>View 타입 이름 → UXML/USS 매핑. 디자이너가 에디터에서 편집하는 불변 설정 데이터.</summary>
    [CreateAssetMenu(fileName = "UIViewCatalog", menuName = "LOP/UI/UIViewCatalog")]
    public class UIViewCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string viewName;          // typeof(TView).Name
            public VisualTreeAsset uxml;
            public StyleSheet uss;            // 선택(없으면 null)
        }

        [SerializeField] private List<Entry> entries = new();

        public bool TryGet(string viewName, out Entry entry)
        {
            foreach (var e in entries)
            {
                if (e.viewName == viewName)
                {
                    entry = e;
                    return true;
                }
            }
            entry = default;
            return false;
        }
    }
}
```

- [ ] **Step 3: 컴파일 확인**

`refresh_unity` → `read_console`. 기대: 0 에러.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/UI/Core/UILayer.cs Assets/Scripts/UI/Core/UIViewCatalog.cs Assets/Scripts/UI/Core/UILayer.cs.meta Assets/Scripts/UI/Core/UIViewCatalog.cs.meta
git commit -m "feat(ui): add UILayer enum and UIViewCatalog SO"
```

---

### Task 3: UIView + UIPopup 베이스

**Files:**
- Create: `Assets/Scripts/UI/Core/UIView.cs`
- Create: `Assets/Scripts/UI/Core/UIPopup.cs`

- [ ] **Step 1: UIView 작성**

```csharp
using System;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>순수 C# 뷰 컨트롤러 베이스. UIManager가 UXML 클론을 Initialize로 주입한다.</summary>
    public abstract class UIView : IDisposable
    {
        public VisualElement Root { get; private set; }

        protected CompositeDisposable Disposables { get; } = new();

        /// <summary>UIManager가 UXML 클론 직후 1회 호출. 파생은 base 호출 후 바인딩.</summary>
        public virtual void Initialize(VisualElement root)
        {
            Root = root;
        }

        /// <summary>레이어에 부착되고 표시 직전 호출.</summary>
        public virtual void OnOpen() { }

        /// <summary>레이어에서 제거되기 직전 호출.</summary>
        public virtual void OnClose() { }

        /// <summary>(M1 no-op 훅) 열기 연출.</summary>
        protected virtual UniTask PlayOpenAsync() => UniTask.CompletedTask;

        /// <summary>(M1 no-op 훅) 닫기 연출.</summary>
        protected virtual UniTask PlayCloseAsync() => UniTask.CompletedTask;

        public virtual void Dispose()
        {
            Disposables.Dispose();
        }
    }
}
```

- [ ] **Step 2: UIPopup 작성**

```csharp
namespace LOP.UI
{
    /// <summary>모달 팝업 베이스. 백드롭은 UIManager가 제공. AutoClose면 백드롭 클릭/스택 정리 시 닫힘.</summary>
    public abstract class UIPopup : UIView
    {
        public virtual bool AutoClose => true;
    }
}
```

- [ ] **Step 3: 컴파일 확인**

`refresh_unity` → `read_console`. 기대: 0 에러. (R3 `CompositeDisposable`이 Task 1에서 확인한 시그니처와 일치하는지 여기서 재확인.)

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/UI/Core/UIView.cs Assets/Scripts/UI/Core/UIPopup.cs Assets/Scripts/UI/Core/UIView.cs.meta Assets/Scripts/UI/Core/UIPopup.cs.meta
git commit -m "feat(ui): add UIView/UIPopup base controllers"
```

---

### Task 4: IUIManager + UIManager(레이어/모달 스택/백드롭)

**Files:**
- Create: `Assets/Scripts/UI/Core/IUIManager.cs`
- Create: `Assets/Scripts/UI/Core/UIManager.cs`

- [ ] **Step 1: IUIManager 작성**

```csharp
namespace LOP.UI
{
    public interface IUIManager
    {
        /// <summary>이미 DI로 생성된 view를 해당 레이어에 열고, 모달이면 스택 push + 백드롭 표시.</summary>
        void Open(UIView view, UILayer layer);

        /// <summary>view를 닫고 dispose. 모달이면 스택 pop + 백드롭 갱신.</summary>
        void Close(UIView view);

        /// <summary>모달 스택 최상단을 닫는다(back/ESC). 닫았으면 true.</summary>
        bool CloseTop();
    }
}
```

- [ ] **Step 2: UIManager 작성**

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// UIRoot 프리팹(UIDocument + PanelSettings) 위의 앱 전역 매니저.
    /// 레이어 컨테이너 생성, 화면 open/close, 모달 스택 + 백드롭만 책임진다.
    /// DI 해소(View/ViewModel resolve)는 호출자 스코프가 수행하고, 생성된 view 인스턴스를 Open에 넘긴다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class UIManager : MonoBehaviour, IUIManager
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private UIViewCatalog catalog;

        private readonly Dictionary<UILayer, VisualElement> _layers = new();
        private readonly Dictionary<UIView, VisualElement> _roots = new();
        private readonly List<UIPopup> _modalStack = new();
        private VisualElement _backdrop;

        private void Awake()
        {
            if (document == null) document = GetComponent<UIDocument>();
            var root = document.rootVisualElement;
            root.style.flexGrow = 1;

            // 레이어 컨테이너를 enum 순서(z-order)대로 생성.
            foreach (UILayer layer in System.Enum.GetValues(typeof(UILayer)))
            {
                var container = new VisualElement { name = $"layer-{layer}" };
                container.style.position = Position.Absolute;
                container.style.left = 0;
                container.style.right = 0;
                container.style.top = 0;
                container.style.bottom = 0;
                container.pickingMode = PickingMode.Ignore; // 레이어 자체는 입력 통과
                root.Add(container);
                _layers[layer] = container;
            }

            _backdrop = new VisualElement { name = "modal-backdrop" };
            _backdrop.style.position = Position.Absolute;
            _backdrop.style.left = 0;
            _backdrop.style.right = 0;
            _backdrop.style.top = 0;
            _backdrop.style.bottom = 0;
            _backdrop.style.backgroundColor = new Color(0f, 0f, 0f, 0.5f);
            _backdrop.pickingMode = PickingMode.Position; // 하단 입력 차단
            _backdrop.RegisterCallback<PointerDownEvent>(_ =>
            {
                if (_modalStack.Count > 0 && _modalStack[^1].AutoClose)
                {
                    CloseTop();
                }
            });
        }

        public void Open(UIView view, UILayer layer)
        {
            if (!catalog.TryGet(view.GetType().Name, out var entry) || entry.uxml == null)
            {
                Debug.LogError($"[UIManager] UIViewCatalog에 '{view.GetType().Name}' UXML 매핑이 없습니다.");
                return;
            }

            var viewRoot = entry.uxml.Instantiate();
            viewRoot.style.flexGrow = 1;
            if (entry.uss != null) viewRoot.styleSheets.Add(entry.uss);

            view.Initialize(viewRoot);
            _roots[view] = viewRoot;

            var container = _layers[layer];
            container.Add(viewRoot);

            if (view is UIPopup popup)
            {
                _modalStack.Add(popup);
                PositionBackdrop(layer, viewRoot);
            }

            view.OnOpen();
        }

        public void Close(UIView view)
        {
            view.OnClose();

            if (_roots.TryGetValue(view, out var viewRoot))
            {
                viewRoot.RemoveFromHierarchy();
                _roots.Remove(view);
            }

            if (view is UIPopup popup)
            {
                _modalStack.Remove(popup);
                if (_modalStack.Count > 0)
                {
                    var top = _modalStack[^1];
                    if (_roots.TryGetValue(top, out var topRoot))
                    {
                        PositionBackdrop(UILayer.Popup, topRoot);
                    }
                }
                else
                {
                    _backdrop.RemoveFromHierarchy();
                }
            }

            view.Dispose();
        }

        public bool CloseTop()
        {
            if (_modalStack.Count == 0) return false;
            Close(_modalStack[^1]);
            return true;
        }

        // 백드롭을 top 팝업 바로 아래에 배치(top만 위, 그 아래 전체 차단).
        private void PositionBackdrop(UILayer layer, VisualElement topRoot)
        {
            var container = _layers[layer];
            if (_backdrop.parent != container) container.Add(_backdrop);
            int topIndex = container.IndexOf(topRoot);
            int desired = topIndex; // backdrop을 topRoot 바로 앞 인덱스로
            _backdrop.RemoveFromHierarchy();
            desired = Mathf.Clamp(container.IndexOf(topRoot), 0, container.childCount);
            container.Insert(desired, _backdrop);
        }
    }
}
```

- [ ] **Step 3: 컴파일 확인 + UITK API 확정**

`refresh_unity` → `read_console`. 기대: 0 에러. **만약 `Instantiate()`/`styleSheets`/`RemoveFromHierarchy`/`IndexOf`/`Insert`/`pickingMode` 등 UITK API 시그니처 에러**가 나면 Unity 6.3 실제 API로 조정(예: `uxml.Instantiate()`는 `TemplateContainer` 반환 — VisualElement 호환). 0 에러까지 수정.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/UI/Core/IUIManager.cs Assets/Scripts/UI/Core/UIManager.cs Assets/Scripts/UI/Core/IUIManager.cs.meta Assets/Scripts/UI/Core/UIManager.cs.meta
git commit -m "feat(ui): add IUIManager + UIManager (layers, modal stack, backdrop)"
```

---

### Task 5: UI 에셋 생성 + RootLifetimeScope 등록

**Files:**
- Create: `Assets/UI/UIPanelSettings.asset` (PanelSettings)
- Create: `Assets/UI/UIViewCatalog.asset`
- Create: `Assets/UI/UIRoot.prefab`
- Modify: `Assets/Scripts/RootLifetimeScope.cs`

- [ ] **Step 1: PanelSettings 에셋 생성**

Project 창에서 `Assets/UI/` 생성 → 우클릭 → Create → UI Toolkit → Panel Settings Asset → `UIPanelSettings`. 설정:
- Theme Style Sheet: 기본(Unity 기본 테마)
- Scale Mode: `Scale With Screen Size`
- Reference Resolution: 현 UGUI Canvas Scaler 값에 맞춤(미상이면 1920×1080로 두고 Task 11 런타임에서 조정)
- Render Mode: `Overlay`(스크린스페이스 — 기본)

- [ ] **Step 2: UIViewCatalog 에셋 생성**

`Assets/UI/`에서 우클릭 → Create → LOP → UI → UIViewCatalog → `UIViewCatalog`. (엔트리는 Task 8에서 LoginView 추가.)

- [ ] **Step 3: UIRoot 프리팹 생성**

빈 GameObject `UIRoot` 생성 → `UIDocument` 컴포넌트 추가(Panel Settings = `UIPanelSettings`, Source Asset = 비움) → `UIManager` 컴포넌트 추가 → `UIManager`의 `document`에 같은 GameObject의 UIDocument, `catalog`에 `UIViewCatalog.asset` 할당. 이 GameObject를 `Assets/UI/UIRoot.prefab`으로 저장 후 씬에서 삭제(프리팹만 사용).

- [ ] **Step 4: RootLifetimeScope에 직렬화 필드 + 등록 추가**

`Assets/Scripts/RootLifetimeScope.cs`를 수정한다. 클래스 상단에 `using LOP.UI;`와 `using UnityEngine;`(없으면) 추가. 필드와 등록 추가:

```csharp
using LOP.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    public class RootLifetimeScope : LifetimeScope
    {
        [SerializeField] private UIManager uiRootPrefab;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<LOP.MasterData.LOPMasterData>(Lifetime.Singleton);

            builder.Register<UserDataStore>(Lifetime.Singleton)
                .As<IUserDataStore>()
                .As<IDataStore>()
                .AsSelf();

            builder.Register<MatchMakingDataStore>(Lifetime.Singleton)
                .As<IMatchMakingDataStore>()
                .As<IDataStore>()
                .AsSelf();

            builder.Register<RoomDataStore>(Lifetime.Singleton)
                .As<IRoomDataStore>()
                .As<IDataStore>()
                .AsSelf();

            builder.Register<RoomConnector>(Lifetime.Transient);

            builder.RegisterComponentInNewPrefab(uiRootPrefab, Lifetime.Singleton)
                .As<IUIManager>()
                .DontDestroyOnLoad();

            #region RegisterBuildCallback
            builder.RegisterBuildCallback(container =>
            {
            });
            #endregion
        }
    }
}
```

(기존 `Configure` 본문은 그대로 두고 `RegisterComponentInNewPrefab` 한 줄과 필드/using만 추가하는 것. 위 블록은 결과 전체.)

- [ ] **Step 5: 인스펙터에서 프리팹 할당**

`RootLifetimeScope` 컴포넌트가 있는 GameObject(루트 스코프가 존재하는 씬/프리팹)를 찾아, `Ui Root Prefab` 필드에 `Assets/UI/UIRoot.prefab`을 할당.

- [ ] **Step 6: 컴파일 + 해소 스모크**

`refresh_unity` → `read_console`. 기대: 0 에러. (`RegisterComponentInNewPrefab` 시그니처가 VContainer 1.16.2와 맞는지 확인 — 에러 시 `RegisterComponentInNewPrefab<UIManager>(uiRootPrefab, Lifetime.Singleton)`로 제네릭 명시 등 조정.)

- [ ] **Step 7: Commit**

```bash
git add Assets/UI/ Assets/Scripts/RootLifetimeScope.cs
git commit -m "feat(ui): UIRoot prefab + PanelSettings + catalog; register UIManager app-global"
```

---

## Phase C — LoginView MVVM

### Task 6: LoginViewModel

**Files:**
- Create: `Assets/Scripts/UI/Login/LoginViewModel.cs`

- [ ] **Step 1: LoginViewModel 작성**

원 동작 보존: Guest 항상 노출, GPGS는 Android(비에디터)만, GameCenter는 미노출.

```csharp
using System;
using R3;

namespace LOP.UI
{
    /// <summary>로그인 팝업 ViewModel. 순수 C#. 선택된 LoginType을 OnLoginRequested로 1회 발행.</summary>
    public class LoginViewModel : IDisposable
    {
        private readonly Subject<LoginType> _loginRequested = new();

        public Observable<LoginType> OnLoginRequested => _loginRequested;

        public bool ShowGuest { get; }
        public bool ShowGpgs { get; }
        public bool ShowGameCenter { get; }

        public LoginViewModel()
        {
            ShowGuest = true;
            ShowGameCenter = false;
#if !UNITY_EDITOR && UNITY_ANDROID
            ShowGpgs = true;
#else
            ShowGpgs = false;
#endif
        }

        public void RequestLogin(LoginType loginType)
        {
            _loginRequested.OnNext(loginType);
        }

        public void Dispose()
        {
            _loginRequested.Dispose();
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인**

`refresh_unity` → `read_console`. 기대: 0 에러. (`Subject<T>`/`Observable<T>` R3 시그니처 확정.)

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/Login/LoginViewModel.cs Assets/Scripts/UI/Login/LoginViewModel.cs.meta
git commit -m "feat(ui): add LoginViewModel (R3)"
```

---

### Task 7: LoginView UXML + USS

**Files:**
- Create: `Assets/UI/Login/LoginView.uxml`
- Create: `Assets/UI/Login/LoginView.uss`

- [ ] **Step 1: LoginView.uxml 작성**

`Assets/UI/Login/`에 작성. 버튼 3개(`guest-login`/`gpgs-login`/`gamecenter-login`)를 중앙 카드에 배치:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement name="login-root" class="login-root">
        <ui:VisualElement name="login-card" class="login-card">
            <ui:Label text="LEAGUE OF PHYSICAL" class="login-title" />
            <ui:Button name="guest-login" text="Guest Login" class="login-button" />
            <ui:Button name="gpgs-login" text="Google Play" class="login-button" />
            <ui:Button name="gamecenter-login" text="Game Center" class="login-button" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 2: LoginView.uss 작성**

현 레이아웃 충실 이식 + 소폭 정돈:

```css
.login-root {
    flex-grow: 1;
    align-items: center;
    justify-content: center;
}

.login-card {
    width: 480px;
    padding: 32px;
    border-radius: 12px;
    background-color: rgba(20, 20, 28, 0.92);
    align-items: stretch;
}

.login-title {
    font-size: 28px;
    color: rgb(235, 235, 245);
    -unity-text-align: middle-center;
    margin-bottom: 24px;
}

.login-button {
    height: 56px;
    margin-top: 12px;
    font-size: 18px;
    border-radius: 8px;
}
```

- [ ] **Step 3: 임포트 확인**

`refresh_unity` → `read_console`. 기대: UXML/USS 임포트 에러 0. (UI Builder로 열어 미리보기 권장.)

- [ ] **Step 4: Commit**

```bash
git add Assets/UI/Login/
git commit -m "feat(ui): add LoginView UXML + USS"
```

---

### Task 8: LoginView 컨트롤러 + 카탈로그 등록

**Files:**
- Create: `Assets/Scripts/UI/Login/LoginView.cs`
- Modify: `Assets/UI/UIViewCatalog.asset` (에디터에서 엔트리 추가)

- [ ] **Step 1: LoginView 작성**

```csharp
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>로그인 팝업 View. 버튼 클릭을 ViewModel 커맨드로 전달, 플랫폼별 버튼 노출 토글.</summary>
    public class LoginView : UIPopup
    {
        public LoginViewModel ViewModel { get; }

        private Button _guestButton;
        private Button _gpgsButton;
        private Button _gamecenterButton;

        public LoginView(LoginViewModel viewModel)
        {
            ViewModel = viewModel;
        }

        public override void OnOpen()
        {
            base.OnOpen();

            _guestButton = Root.Q<Button>("guest-login");
            _gpgsButton = Root.Q<Button>("gpgs-login");
            _gamecenterButton = Root.Q<Button>("gamecenter-login");

            SetVisible(_guestButton, ViewModel.ShowGuest);
            SetVisible(_gpgsButton, ViewModel.ShowGpgs);
            SetVisible(_gamecenterButton, ViewModel.ShowGameCenter);

            _guestButton.clicked += OnGuestClicked;
            _gpgsButton.clicked += OnGpgsClicked;
            _gamecenterButton.clicked += OnGameCenterClicked;
        }

        public override void OnClose()
        {
            if (_guestButton != null) _guestButton.clicked -= OnGuestClicked;
            if (_gpgsButton != null) _gpgsButton.clicked -= OnGpgsClicked;
            if (_gamecenterButton != null) _gamecenterButton.clicked -= OnGameCenterClicked;

            base.OnClose();
        }

        private void OnGuestClicked() => ViewModel.RequestLogin(LoginType.Guest);
        private void OnGpgsClicked() => ViewModel.RequestLogin(LoginType.GooglePlayGame);
        private void OnGameCenterClicked() => ViewModel.RequestLogin(LoginType.GameCenter);

        private static void SetVisible(VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public override void Dispose()
        {
            ViewModel.Dispose();
            base.Dispose();
        }
    }
}
```

- [ ] **Step 2: 카탈로그에 엔트리 추가**

`Assets/UI/UIViewCatalog.asset`을 인스펙터에서 열고 `entries`에 1개 추가:
- `viewName`: `LoginView`
- `uxml`: `Assets/UI/Login/LoginView.uxml`
- `uss`: `Assets/UI/Login/LoginView.uss`

- [ ] **Step 3: 컴파일 확인**

`refresh_unity` → `read_console`. 기대: 0 에러.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/UI/Login/LoginView.cs Assets/Scripts/UI/Login/LoginView.cs.meta Assets/UI/UIViewCatalog.asset
git commit -m "feat(ui): add LoginView controller + catalog entry"
```

---

### Task 9: DI 등록 + LoginComponent 재작성

**Files:**
- Modify: `Assets/Scripts/Entrance/EntranceLifetimeScope.cs`
- Modify: `Assets/Scripts/Entrance/EntranceComponent/LoginComponent.cs`

- [ ] **Step 1: EntranceLifetimeScope에 View/ViewModel 등록**

`Configure`에 두 줄 추가(`using LOP.UI;` 추가). 기존 등록은 유지:

```csharp
using LOP.UI;
using VContainer;
// ... 기존 using

namespace LOP
{
    public class EntranceLifetimeScope : SceneLifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            builder.Register<IEntranceComponent, LoginComponent>(Lifetime.Transient);
            builder.Register<IEntranceComponent, CheckUserComponent>(Lifetime.Transient);
            builder.Register<IEntranceComponent, JoinLobbyComponent>(Lifetime.Transient);
            builder.Register<IEntranceComponent, LoadMasterDataComponent>(Lifetime.Transient);

            builder.Register<LoginViewModel>(Lifetime.Transient);
            builder.Register<LoginView>(Lifetime.Transient);
        }
    }
}
```

- [ ] **Step 2: LoginComponent 재작성**

`PopupManager`/`onGuestLoginClick`/`UniTask.WaitUntil` 제거. `IObjectResolver`(Entrance 스코프)로 `LoginView` 생성 → `IUIManager.Open` → R3 결과 await → `Login` → `Close`:

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
        [Inject] private IObjectResolver resolver;
        [Inject] private IUIManager uiManager;

        public async Task Execute()
        {
            var autoLoginResult = await LoginService.instance.TryAutoLogin();
            if (autoLoginResult.success)
            {
                return;
            }

            var view = resolver.Resolve<LoginView>();
            uiManager.Open(view, UILayer.Popup);

            LoginType loginType = await view.ViewModel.OnLoginRequested.FirstAsync();

            LoginResult loginResult = LoginService.instance.Login(loginType);

            uiManager.Close(view);

            if (loginResult.success == false)
            {
                throw new Exception(loginResult.reason);
            }
        }
    }
}
```

(주의: 기존 `LoginComponent`는 필드 주입이 없었지만, 같은 파일의 `CheckUserComponent`가 `[Inject]` 필드 주입을 쓰는 순수 C# 컴포넌트이므로 동일 패턴이 유효 — VContainer가 `IEnumerable<IEntranceComponent>` 해소 시 필드 주입 수행.)

- [ ] **Step 3: 컴파일 확인**

`refresh_unity` → `read_console`. 기대: 0 에러. (`FirstAsync()`가 `Task<LoginType>` 반환인지 R3 시그니처로 확정 — 다르면 `await view.ViewModel.OnLoginRequested.FirstAsync(cancellationToken)` 또는 `.FirstAsync().AsTask()` 등으로 조정.)

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Entrance/EntranceLifetimeScope.cs Assets/Scripts/Entrance/EntranceComponent/LoginComponent.cs
git commit -m "feat(ui): wire LoginView/VM in DI; rewrite LoginComponent to UIManager + R3"
```

---

### Task 10: 기존 UGUI LoginPopup 제거

**Files:**
- Delete: `Assets/Scripts/Popup/LoginPopup.cs` (+`.meta`)
- Delete: `Assets/Popups/LoginPopup.prefab` (+`.meta`)
- Modify: `Assets/Resources/PrefabReferences.asset` (LoginPopup 엔트리 제거)

- [ ] **Step 1: PrefabReferences.asset에서 LoginPopup 제거**

인스펙터에서 `Assets/Resources/PrefabReferences.asset`의 `prefabs` 배열에서 `LoginPopup` 엔트리를 제거(슬롯 삭제).

- [ ] **Step 2: 스크립트/프리팹 삭제**

```bash
git rm Assets/Scripts/Popup/LoginPopup.cs Assets/Scripts/Popup/LoginPopup.cs.meta
git rm Assets/Popups/LoginPopup.prefab Assets/Popups/LoginPopup.prefab.meta
```

- [ ] **Step 3: 컴파일 + 미참조 확인**

`refresh_unity` → `read_console`. 기대: 0 에러(어디서도 `LoginPopup` 타입을 참조하지 않음 — Task 9에서 유일 사용처 제거됨). 잔여 참조 에러가 나면 그 호출부를 정리.

- [ ] **Step 4: Commit**

```bash
git add Assets/Resources/PrefabReferences.asset
git commit -m "chore(ui): remove legacy UGUI LoginPopup (script, prefab, PrefabReferences entry)"
```

---

## Phase D — 런타임 검증

### Task 11: Entrance 플로우 런타임 수동 검증

**Files:** (코드 변경 없음 — 관찰/조정)

- [ ] **Step 1: Entrance 씬 진입(자동 로그인 실패 경로)**

자동 로그인이 성공하지 않도록 `PlayerPrefs`의 `LOGIN_TYPE_KEY`를 제거한 상태로 시작(에디터: Edit → Clear All PlayerPrefs 또는 첫 실행). Unity Play 모드로 `Entrance` 씬 진입.

- [ ] **Step 2: 관찰 항목**

다음을 확인(`read_console`로 에러 0 동반):
1. **UI Toolkit 로그인 팝업 표시** — 중앙 카드 + Guest 버튼. (Android 비에디터 빌드에서만 GPGS 버튼 노출, 에디터에선 Guest만.)
2. **모달 백드롭** — 반투명 딤이 깔리고 하단 입력이 차단됨.
3. **Guest 로그인 클릭** → `LoginService.Login(Guest)` 호출 → 팝업이 닫히고 Entrance 플로우가 다음 컴포넌트로 진행(성공 시 Lobby 씬 로드).
4. **에러 0건** — DI 누락 NRE 없음, 카탈로그 매핑 누락 로그 없음.

- [ ] **Step 3: PanelSettings 스케일 조정(필요 시)**

해상도/스케일이 기존 UGUI와 어긋나면 `UIPanelSettings`의 Reference Resolution/Scale Mode를 조정해 의도한 크기로 맞춤. 변경 시 재실행 관찰.

- [ ] **Step 4: 검증 결과 기록 + Commit(조정이 있었다면)**

조정한 에셋이 있으면 커밋:

```bash
git add Assets/UI/UIPanelSettings.asset
git commit -m "fix(ui): tune login panel scale to match prior layout"
```

검증 통과면 M1 구현 완료. `superpowers:finishing-a-development-branch`로 `feature/ui-toolkit-migration-m1`을 main에 `--no-ff` 머지(push는 사용자 지시 시).

---

## Self-Review 결과

- **Spec coverage**: R3 도입(Task 1) ✓, UI 관리 모델=레이어+모달스택+백드롭+라이프사이클(Task 2–4) ✓, 단일 UIRoot/순수 C# View/카탈로그(Task 2,3,5,8) ✓, app-global 등록(Task 5) ✓, LoginView/VM + R3 결과 await(Task 6–9) ✓, 레거시 제거(Task 10) ✓, 런타임 검증 + Assembly-CSharp 테스트 제약 명시(사전 메모, Task 11) ✓, 트랜지션/페이지네비 미구현(UIView 훅만, YAGNI) ✓.
- **Placeholder scan**: 코드 단계는 전부 실제 코드 포함. R3/UITK/ VContainer API "확정" 단계는 placeholder가 아니라 **컴파일 게이트로 실제 시그니처를 확정**하는 의도된 검증(그린필드 도입의 알려진 미지수).
- **Type consistency**: `IUIManager.Open(UIView, UILayer)`/`Close(UIView)`/`CloseTop()` ↔ `LoginComponent`/`UIManager` 일치. `UIView.Initialize/OnOpen/OnClose/Dispose/Root/Disposables` ↔ `LoginView` 일치. `LoginViewModel.OnLoginRequested/RequestLogin/ShowXxx/Dispose` ↔ `LoginView`/`LoginComponent` 일치. `UIViewCatalog.TryGet(string, out Entry)`/`Entry.uxml/uss` ↔ `UIManager` 일치. `LoginType.Guest/GooglePlayGame/GameCenter`, `LoginResult.success/reason` ↔ 실제 정의 일치.
```
