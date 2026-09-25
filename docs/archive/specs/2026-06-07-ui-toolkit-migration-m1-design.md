# UI Toolkit 전환 — M1: 토대 + UI 관리 모델 + LoginPopup 파일럿

**Date:** 2026-06-07 (개정 2026-06-09)
**Branch (제안):** `feature/ui-toolkit-migration-m1`
**Related:** [아키텍처 가이드라인](../../architecture-guidelines.md) · [LOP 저장소 토폴로지](../../lop-repo-topology.md)

## Goal

현재 100% UGUI인 클라 UI를 가이드라인 정합(**UI Toolkit + MVVM + R3**)으로 점진 전환하는 작업의 **첫 마일스톤(M1)**. M1은 이후 모든 화면이 따를 **재사용 패턴과 UI 관리 인프라를 확립**하고, 가장 단순한 화면(**LoginPopup**)을 끝-끝으로 전환해 검증한다.

부수 목표: 현재 더미 수준인 `PopupManager`를 **일반적(업계 표준) UI 관리 모델 — 레이어(밴드) × 스택 윈도우 매니저**로 재설계해 클라에 구축한다.

### 개정 노트 (2026-06-09)

초기 구현(고정 5-레이어 enum `Hud/Popup/Loading/Toast/System`, 수동 `Q` 컨트롤러 View, `RootLifetimeScope`/`EntranceLifetimeScope` 직접 등록)을 다음으로 **개정**한다. 근거는 업계 표준 교차검증(Unreal CommonUI/Lyra, UnityScreenNavigator) 및 Unity 6 바인딩 수준 재평가:

1. **윈도우 매니저 = 레이어(원칙적 밴드) × 스택** — 임의 레이어 enum 폐기. 밴드는 상호작용 등급(`Window/Popup/Notification/System`), 각 밴드가 자체 스택. "시스템/모달 항상 최상단"을 밴드 우선순위로 선언적 보장.
2. **R3 중심 바인딩 유지(Unity 6 네이티브 런타임 바인딩 미채택)** — U6 바인딩은 값 바인딩 한정·커맨드 부재·보일러플레이트·미성숙. ViewModel이 R3로 상태/이벤트 노출, View가 구독.
3. **전용 `UIInstaller`** — UI 인프라 등록을 앱 스코프(Root/Entrance)에서 분리.

### 동기

- 가이드라인은 "UI = UI Toolkit + MVVM + R3"를 표준으로 규정하나, 현재는 UGUI + Presenter/MonoSingleton + UniRx + `Update()` 폴링으로 어긋나 있다.
- 아트 품질이 낮아 옮기는 김에 **소폭 정리**(재디자인 아님, USS로 스타일만 정돈).
- Unity 6.3은 **UI Toolkit World Space를 정식 지원**하므로 월드 추적 UI(M3)까지 포함해 **100% UI Toolkit**이 가능.

## 전체 로드맵 (M1은 이 중 첫 조각)

| 마일스톤 | 내용 | 상태 |
|---|---|---|
| **M1 — 토대 + 파일럿** | R3 추가 + 클라 UI 관리 인프라(윈도우 매니저, `UIRoot` 문서, 레이어×스택) + base View/ViewModel 패턴 + **LoginPopup** 끝-끝 전환 | **이 spec** |
| **M2 — 화면 고정 UI** | MatchMaking/`MatchingWaitingUI`, `GameLoadingUI`, **StatsPopup**(라이브 R3 바인딩 예시) | 예정 |
| **M3 — 인게임 월드 HUD** | World Space `PanelSettings` 스파이크 → `CharacterUI`(머리 위 HP바), `DamageUI`/`DamageView` | 예정 |
| **M4 — 터치 입력** | `GamePad`/`JoyStick`/`CameraTouchController`를 UI Toolkit 포인터 이벤트로 | 예정 |
| **M5 — 정리/승격** | 클라에서 `CanvasManager`/UGUI `PopupManager` 사용 제거, UI 관리 인프라를 GameFramework로 승격 | 예정 |

## Architecture (1-pager)

```
                 PanelSettings (Screen-space, 클라)
                          │
              ┌───────────┴──────────────────────────┐
              │  UIRoot (UIDocument, DontDestroyOnLoad)
              │   └ 밴드 컨테이너 VisualElement (z-order 낮음→높음)
              │       #window  <  #popup  <  #notification  <  #system
              │       (각 밴드 = 자체 스택; 모달 밴드는 top 아래 백드롭)
              └───────────┬──────────────────────────┘
                          ▲
                          │ Open/Close/Back, 밴드 배치, 밴드별 스택+백드롭
              ┌───────────┴──────────────────────────┐
              │  WindowManager (IWindowManager)
              │   - Open<T>() : T 를 UI 스코프에서 resolve → 선언된 밴드 스택에 push
              │   - Close(window) / Back()
              │   - IObjectResolver(UI 스코프)로 View+ViewModel 생성
              └───────────┬──────────────────────────┘
                          │ 생성
                          ▼
   UIView (얇은 바인더; UXML 트리 소유)
   - Root(VisualElement, UXML 클론), Layer(밴드), IsModal
   - OnOpen/OnClose, CompositeDisposable, (no-op)PlayOpen/CloseAsync
   - ViewModel의 R3 구독 → VisualElement 갱신, 입력 → 커맨드
        │ 구독/커맨드 (R3)
        ▼
   ViewModel (순수 C#)
   - 상태: ReadOnlyReactiveProperty<T> / 이벤트·커맨드: Observable(Subject), IDisposable
        │ pull
        ▼
   Model (기존 도메인 — 데이터스토어 / 엔티티 컴포넌트 / 상태머신 / 네트워크)
```

핵심 디자인 결정:

- **레이어(밴드) × 스택** — 고정 임의 enum 대신 상호작용 등급 밴드. 각 밴드가 자체 스택. 입력 포커스 = 가장 높은 가시 밴드의 top. ("UI 아키텍처" 가이드라인 절과 동일 모델.)
- **R3 중심 바인딩** — ViewModel이 R3로 상태/이벤트 노출, View가 구독. U6 네이티브 바인딩 미채택.
- **얇은 View** — UXML 트리 소유 + R3 구독 바인더. 비즈니스 로직 없음. (VisualElement 파생/컨트롤러 둘 다 허용 — M1은 컨트롤러형 `UIView` 사용, UXML은 클론.)
- **전용 `UIInstaller`** — `IWindowManager` + 모든 View/ViewModel 등록을 한 모듈에. 앱 루트 스코프에 `Install`(코드 결합 분리). View는 윈도우 매니저가 자기 스코프 `IObjectResolver`로 resolve → 부모/자식 스코프 가시성 문제 회피.
- **클라 전용 인프라** — GameFramework의 `Popup`/`PopupManager`(UGUI)는 미변경. 안정화 후 M5에서 GameFramework 승격.
- **공존 기간** — M1엔 `CanvasManager`/GameFramework `PopupManager`(StatsPopup 등)가 그대로. LoginPopup만 새 매니저로 이동.

## UI 관리 모델 — 레이어(밴드) × 스택 윈도우 매니저

### 현재 (더미)

`GameFramework.PopupManager`: `HashSet<IPopup>` 평면 보관, `GetPopup→Show→Close`. 스택·모달·밴드·백드롭·결과 반환 없음. `Canvas` 결합.

### 신규 — `WindowManager` (클라)

**밴드** (`enum UILayer`, z-order 낮음→높음):

| 밴드 | 성격 | 스택/모달 | M1 |
|---|---|---|---|
| `Window` | 주 화면/페이지 | 스택 | (M2) |
| `Popup` | 모달 다이얼로그 | 스택 + 백드롭 | ✅ LoginView |
| `Notification` | 토스트/일시 알림 | 비모달 큐 | (M2) |
| `System` | 시스템/치명/항상 최상단 | 스택(+필요시 백드롭) | (M2) |

각 밴드 = `UIRoot`의 컨테이너 `VisualElement`(z-order 순서). **각 밴드가 자기 스택을 관리** — 하위 밴드는 상위 밴드 위로 못 올라감. 인게임 HUD/월드 추적 UI는 이 매니저 밖(M3, World Space PanelSettings).

**API (M1 구현분):**

```csharp
public interface IWindowManager
{
    // T를 UI 스코프에서 resolve(ViewModel 자동 주입) → T.Layer 밴드 스택에 push.
    // 모달이면 백드롭 삽입. 생성된 View 반환.
    T Open<T>() where T : UIView;

    // 닫고 dispose. 스택 pop + (모달이면) 백드롭 갱신.
    void Close(UIView view);

    // 가장 높은 가시 밴드의 top을 닫는다(back/ESC). 닫았으면 true.
    bool Back();

    // 결과 반환 모달(다이얼로그 서비스): 모달을 열고 ViewModel이 만든 결과를 await로 반환, 확정 후 자동 Close.
    // 소비자는 View/VM을 만지지 않고 결과만 받는다. View는 IResultView<TResult>로 VM 결과를 포워딩.
    UniTask<TResult> OpenModalAsync<TView, TResult>() where TView : UIView, IResultView<TResult>;
}
```

- **스택/백드롭**: 모달 `UIView`(IsModal) push 시 해당 밴드 컨테이너에서 top 아래 **백드롭 `VisualElement`**(딤+입력 차단) 삽입. `Close`/`Back`/백드롭 클릭(AutoClose)으로 pop.
- **생성·주입**: `WindowManager`가 주입받은 `IObjectResolver`(UI 스코프)로 `Resolve<T>()` → ViewModel 자동 주입 → UXML 클론을 `view.Initialize(root)` → `T.Layer` 밴드에 부착 → `view.OnOpen()`.
- **수명**: `Close` 시 `OnClose()` → `Dispose()`(CompositeDisposable 해제) → VisualElement 분리.

**M1 의도적 미구현(훅/추후):** Open/Close 트랜지션(훅만 no-op), 씬 레벨 페이지 네비게이션(YAGNI — 씬이 담당), 씬 전환 자동 닫기(M2).

### Base 클래스

```csharp
// 얇은 View 바인더. UXML 트리 소유 + ViewModel R3 구독.
public abstract class UIView : IDisposable
{
    public VisualElement Root { get; private set; }
    public abstract UILayer Layer { get; }       // 어느 밴드에 속하는가
    public virtual bool IsModal => false;         // 모달이면 백드롭+입력 차단
    protected CompositeDisposable Disposables { get; } = new();

    public virtual void Initialize(VisualElement root) { Root = root; }
    public virtual void OnOpen() { }
    public virtual void OnClose() { }
    protected virtual UniTask PlayOpenAsync() => UniTask.CompletedTask;
    protected virtual UniTask PlayCloseAsync() => UniTask.CompletedTask;
    public virtual void Dispose() { Disposables.Dispose(); }
}

// 모달 팝업: Popup 밴드 + 백드롭. 결과 반환은 파생에서 R3로.
public abstract class UIPopup : UIView
{
    public override UILayer Layer => UILayer.Popup;
    public override bool IsModal => true;
    public virtual bool AutoClose => true;        // 백드롭 클릭 시 닫힘
}
```

> UXML/USS는 View와 짝지어 로드(`UIViewCatalog` SO: viewName→UXML/USS 매핑). View는 R3 구독으로 동적 값을 갱신하고, 입력은 ViewModel 커맨드로 전달한다.

## LoginPopup 파일럿 전환

### 현재
- `LoginPopup : GameFramework.Popup` — UGUI 버튼 3개, `onGuestLoginClick`, `Show()` 플랫폼 토글, UniRx.
- `LoginComponent.Execute()`: `PopupManager.GetPopup<LoginPopup>()` → 이벤트 구독 → `Show()` → `UniTask.WaitUntil` → `Close()`.

### 신규 구조 (결과 반환 모달 — VM이 로그인 처리, 소비자는 결과만)

업계 MVVM 다이얼로그 서비스 패턴(Prism `IDialogService`/`IDialogAware`) 적용. 로그인은 표시할 라이브 상태가 없는(정적 버튼 + 단일 결과) 화면이라 **결과는 R3가 아니라 UniTask**로 다룬다. (R3는 라이브 상태 바인딩용 — M2 StatsPopup에서 발현.)

**`LoginViewModel`** (순수 C#, IDisposable):
- `UniTask<LoginResult> ResultAsync` — `UniTaskCompletionSource<LoginResult>` 기반. 매니저가 await.
- `bool ShowGuest/ShowGpgs/ShowGameCenter` — 플랫폼 분기(전처리기).
- `RequestLogin(LoginType)` — **서비스 레이어(`LoginService.Login`)를 직접 호출**해 로그인 수행 → 결과 확정(`TrySetResult`). VM이 로그인 use-case를 소유.

**`LoginView : UIPopup, IResultView<LoginResult>`** (Layer=Popup, IsModal, **AutoClose=false** — 필수 모달):
- **ViewModel을 외부에 노출하지 않음**(private). `ResultAsync`는 VM 결과를 포워딩.
- UXML 버튼 3개. `OnOpen`: `ShowXxx`로 `display` 설정, 각 버튼 `clicked` → `viewModel.RequestLogin(...)`.
- USS: 현 레이아웃 충실 이식 + 소폭 정돈.

**`LoginComponent` 재작성** (얇은 플로우 코디네이터 — 자동 로그인 판단 + 게이트만):
```csharp
[Inject] private IWindowManager windowManager;

public async Task Execute()
{
    var autoLoginResult = await LoginService.instance.TryAutoLogin();
    if (autoLoginResult.success) return;

    LoginResult loginResult = await windowManager.OpenModalAsync<LoginView, LoginResult>();

    if (loginResult.success == false) throw new Exception(loginResult.reason);
}
```
소비자는 View/VM을 만지지 않고 **타입 안전한 결과만** 받는다. "타입 받기→서비스 호출" 2단계 컨트롤러 노릇은 VM으로 내려감.

> `LoginService`는 현재 MonoSingleton(`LoginService.instance`)이라 VM이 정적 접근한다. 이상적으론 `ILoginService` DI 주입이나, MonoSingleton DI화는 별도 정리 대상(이번 범위 밖).

### 제거/정리
- 기존 `Assets/Scripts/Popup/LoginPopup.cs` + `Assets/Popups/LoginPopup.prefab` 제거, `PrefabReferences` 엔트리 제거.
- `CanvasManager`/GameFramework `PopupManager`/`Popup`은 **유지**(미이전 화면 사용).

## R3 셋업 (현황)

- **R3 코어 1.3.1**을 NuGetForUnity로 설치 완료(+전이 의존: `Microsoft.Bcl.TimeProvider`/`AsyncInterfaces`, `System.Threading.Channels`/`ComponentModel.Annotations`, `Unsafe 6.0.0`). UniRx와 공존 컴파일 통과.
- **R3.Unity(UPM)는 보류** — Windows PackageCache EPERM(파일 잠금)으로 클론 실패. M1이 쓰는 API(`Subject`/`FirstAsync`/`CompositeDisposable`)는 코어에 전부 존재. 프레임/타임 프로바이더가 필요한 시점(M2/M3)에 **OpenUPM tarball**로 추가 예정.

## DI — 전용 `UIInstaller`

UI 인프라 등록을 한 모듈로 분리(앱 스코프 결합 차단). 앱 루트 스코프에서 `builder.Install(...)`로 적용.

| 등록 | 수명 | 비고 |
|---|---|---|
| `IWindowManager` → `WindowManager` | Singleton | UIRoot 프리팹 인스턴스(DontDestroyOnLoad), `IObjectResolver` 보유 |
| `UIViewCatalog` | Singleton | viewName→UXML/USS 매핑 SO |
| `LoginView`, `LoginViewModel` | Transient | 매니저가 `Open<LoginView>()` 시 resolve(VM 자동 주입) |

> `WindowManager`는 `UIRoot.prefab`(UIDocument+PanelSettings) 위 MonoBehaviour. `RegisterComponentInNewPrefab(...).DontDestroyOnLoad()`로 등록. 등록 코드는 `RootLifetimeScope`가 아니라 `UIInstaller`에 둔다.

## 테스트 전략

- **`LoginViewModel`**(순수 C#): 클라 단일 Assembly-CSharp 제약으로 EditMode asmdef 직접 참조 불가 → **런타임 수동 검증이 1차**(이전 슬라이스 기조).
- **런타임 수동 검증** (Play Mode): ① 자동 로그인 실패 시 로그인 팝업(UI Toolkit) 표시 ② Guest 클릭 → `Login(Guest)` → 팝업 닫힘 → 진행 ③ 플랫폼 버튼 토글 ④ 모달 백드롭+입력 차단 ⑤ 에러 0건.

## Wiring & 변경 요약

| 파일/에셋 | 변경 |
|---|---|
| `NuGetForUnity/packages.config` | R3 코어 추가 (완료) |
| 신규 `WindowManager`/`IWindowManager`, `UIView`, `UIPopup`, `UILayer`(밴드), `UIViewCatalog` | 클라 UI 관리 인프라 |
| 신규 `UIInstaller` | UI DI 등록 모듈 |
| 신규 `UIRoot` 프리팹(Resources) + `PanelSettings`(+테마) | 밴드 컨테이너 호스트 |
| 신규 `LoginView`(+UXML/USS), `LoginViewModel` | 파일럿 |
| `LoginComponent.cs` | `IWindowManager` + R3 결과 await로 재작성 |
| 앱 루트 `LifetimeScope` | `UIInstaller` 한 줄 Install (개별 UI 등록은 인스톨러로 이관) |
| `Assets/Scripts/Popup/LoginPopup.cs`, `Assets/Popups/LoginPopup.prefab` | 제거 |
| `CanvasManager`, GameFramework `PopupManager`/`Popup` | 유지(M5에서 정리) |

## Out of Scope

- M2~M5 전부. 전역 UniRx→R3 스왑. Open/Close 트랜지션 구현(훅만). 씬 레벨 페이지 네비게이션. UI 재디자인. World Space PanelSettings(M3). R3.Unity 도입(보류).

## Open Decisions (plan에서 해소)

- [ ] **밴드 세트 최종** — `Window/Popup/Notification/System` 기본. M1은 `Popup`만 실사용. 확장은 필요 시.
- [ ] **`UIInstaller` 적용 지점** — 루트 스코프 `Install` vs 전용 `UILifetimeScope`(루트 자식·씬 부모). M1은 루트 `Install`(단순). 강한 격리 필요 시 별도 스코프로.
- [ ] **`Back()` 입력 소스** — ESC/안드로이드 back 바인딩은 Input System 연동(M2+). M1은 메서드만.
- [ ] **`PanelSettings` 스케일 모드** — 현 Canvas Scaler 대응값(런타임 검증에서 조정).

## 진행

- [x] 브레인스토밍 합의 + 업계 표준 교차검증(CommonUI/Lyra, UnityScreenNavigator, U6 바인딩 재평가)
- [x] 가이드라인(`architecture-guidelines.md`) "UI 아키텍처" 절 갱신
- [x] 이 spec 개정 (레이어×스택 / R3 중심 / UIInstaller)
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 M1 구현 plan **개정** (기존 구현 → 이 설계로 리팩터)
- [ ] inline 실행

> 비고: 현재 `feature/ui-toolkit-migration-m1` 브랜치엔 *초기 설계*(고정 레이어 enum / 수동 View / Root·Entrance 등록)로 동작하는 구현이 커밋되어 있다. 이 spec 개정에 맞춰 해당 구현을 리팩터한다(처음부터 재작성 아님 — 진화).
