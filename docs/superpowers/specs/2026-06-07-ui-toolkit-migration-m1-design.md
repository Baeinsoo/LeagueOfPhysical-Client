# UI Toolkit 전환 — M1: 토대 + UI 관리 모델 + LoginPopup 파일럿

**Date:** 2026-06-07
**Branch (제안):** `feature/ui-toolkit-migration-m1`
**Related:** [아키텍처 가이드라인](../../architecture-guidelines.md) · [LOP 저장소 토폴로지](../../lop-repo-topology.md)

## Goal

현재 100% UGUI인 클라 UI를 가이드라인 정합(**UI Toolkit + MVVM + R3**)으로 점진 전환하는 작업의 **첫 마일스톤(M1)**. M1은 이후 모든 화면이 따를 **재사용 패턴과 UI 관리 인프라를 확립**하고, 가장 단순한 화면(**LoginPopup**)을 끝-끝으로 전환해 검증한다.

부수 목표: 현재 더미 수준인 `PopupManager`를 **일반적인 UI 관리 모델**(레이어 + 모달 스택 + 비동기 결과 + 라이프사이클 훅)로 재설계해 클라에 구축한다.

### 동기

- 가이드라인은 "UI = UI Toolkit + MVVM + R3"를 표준으로 규정하나, 현재는 UGUI + Presenter/MonoSingleton + UniRx + `Update()` 폴링으로 어긋나 있다.
- 아트 품질이 낮아 옮기는 김에 **소폭 정리**(재디자인 아님, USS로 스타일만 정돈).
- Unity 6.3(6000.3.16f1)은 **UI Toolkit World Space를 정식 지원**([매뉴얼](https://docs.unity3d.com/6000.3/Documentation/Manual/ui-systems/world-space-ui.html))하므로 월드 추적 UI(머리 위 HP바·데미지 숫자)까지 포함해 **100% UI Toolkit**이 기술적으로 가능하다.

## 전체 로드맵 (M1은 이 중 첫 조각)

전체 전환은 단일 spec/plan으로 묶기엔 커서, 화면 단위 점진 전환 결정에 따라 마일스톤으로 분해한다. **각 마일스톤이 자기 spec→plan→구현 사이클을 가진다.**

| 마일스톤 | 내용 | 상태 |
|---|---|---|
| **M1 — 토대 + 파일럿** | R3 추가 + 클라 UI 관리 인프라(`UIManager`, `UIRoot` 문서, 레이어/모달 스택) + base View/ViewModel 패턴 + **LoginPopup** 끝-끝 전환 | **이 spec** |
| **M2 — 화면 고정 UI** | MatchMaking/`MatchingWaitingUI`, `GameLoadingUI`, **StatsPopup**(라이브 데이터 바인딩 예시) | 예정 |
| **M3 — 인게임 월드 HUD** | World Space `PanelSettings` 스파이크 → `CharacterUI`(머리 위 HP바), `DamageUI`/`DamageView`(데미지 숫자) | 예정 |
| **M4 — 터치 입력** | `GamePad`/`JoyStick`/`CameraTouchController`를 UI Toolkit 포인터 이벤트로 | 예정 |
| **M5 — 정리/승격** | 클라에서 `CanvasManager`/UGUI `PopupManager` 사용 제거, UI 관리 인프라를 GameFramework로 승격 | 예정 |

M2~M5는 박제만 한다. M1 완료 후 그 패턴을 기준으로 각 마일스톤을 그때 brainstorm/plan.

## Architecture (1-pager)

```
                 PanelSettings (Screen-space, 클라)
                          │
              ┌───────────┴───────────────┐
              │  UIRoot (UIDocument, DontDestroyOnLoad)
              │   └ 레이어 컨테이너 VisualElement (문서 순서 = z-order)
              │       #hud  <  #popup  <  #loading  <  #toast  <  #system
              └───────────┬───────────────┘
                          ▲
                          │ open/close, 레이어 배치, 모달 스택+백드롭
              ┌───────────┴───────────────┐
              │  UIManager (IUIManager, DI Singleton)
              │   - Open<TView>(layer) / Close(view)
              │   - 모달 popup 스택 (push/pop, back/ESC, 백드롭)
              │   - IObjectResolver로 View 생성 → ViewModel 자동 주입
              └───────────┬───────────────┘
                          │ 생성
        ┌─────────────────┼──────────────────────────┐
        ▼                 ▼                           ▼
   UIView (base, 순수 C# 컨트롤러)            UIPopup : UIView (모달)
   - VisualElement root (UXML 클론)           - 백드롭, autoClose, 결과 반환
   - OnOpen/OnClose 훅, CompositeDisposable    
        │
        │ 구독/커맨드 (R3)
        ▼
   ViewModel (순수 C#, ReactiveProperty 소유, IDisposable)
        │ pull
        ▼
   Model (기존 도메인 — 데이터스토어 / 엔티티 컴포넌트 / 상태머신 / 네트워크 메시지)
```

핵심 디자인 결정:

- **View = 순수 C# 컨트롤러** (MonoBehaviour 아님). `UIManager`가 단일 `UIRoot` UIDocument의 레이어 컨테이너에 UXML을 클론하고 컨트롤러를 생성·주입한다. → **단일 PanelSettings/단일 패널**, 패널 난립 없음, MVVM에 자연스러움. (대안 "화면당 UIDocument MonoBehaviour"는 아래 Open Decisions 참고)
- **DI는 `IObjectResolver`로**: `UIManager.Open<TView>()`가 `resolver.Resolve<TView>()`로 View를 만들면 VContainer가 ViewModel을 자동 주입. 현 `[DIMonoBehaviour]` 씬 스캔 대신 명시적 resolve.
- **R3 신규 도입, 공존**: 새 UI만 R3. 기존 UniRx(비-UI 포함)는 그대로 두고 전역 스왑은 별도 작업.
- **클라 전용 인프라**: GameFramework의 `Popup`/`PopupManager`(UGUI 결합)는 건드리지 않고 클라에 새 인프라 구축. 안정화 후 M5에서 GameFramework 승격.
- **공존 기간**: M1 시점엔 `CanvasManager`(로딩/토스트 캔버스)와 GameFramework `PopupManager`(StatsPopup 등)가 **그대로 살아 있다**. LoginPopup만 새 `UIManager`로 이동. 두 시스템이 한동안 병존.

## UI 관리 모델 (일반형 — 더미 `PopupManager` 대체)

### 현재 (더미)

`GameFramework.PopupManager`: `HashSet<IPopup>`에 평면 보관, `GetPopup<T>`(프리팹 인스턴스화)→`Show`→`Close`(파괴). 스택·모달·백드롭·트랜지션·결과 반환 없음. `Canvas`에 직접 결합. 씬 전환 시 `autoClose` 팝업 정리.

### 신규 — `UIManager` (클라)

**레이어** (`enum UILayer`, z-order 낮음→높음):

| 레이어 | 용도 | M1 사용 |
|---|---|---|
| `Hud` | 인게임 월드/오버레이 HUD | (M3) |
| `Popup` | 모달 팝업 스택 | ✅ LoginPopup |
| `Loading` | 로딩 화면 | (M2) |
| `Toast` | 일시 알림 | (M2) |
| `System` | 시스템 UI | (M2) |

각 레이어 = `UIRoot` UXML의 컨테이너 `VisualElement`. 문서 순서가 곧 z-order.

**API (M1 구현분):**

```csharp
public interface IUIManager
{
    // 비모달 뷰(오버레이/HUD) 또는 모달 팝업을 열고 컨트롤러 반환.
    // 모달(UIPopup)이면 스택 push + 백드롭 표시.
    TView Open<TView>(UILayer layer) where TView : UIView;

    // 뷰를 닫고 dispose. 모달이면 스택 pop + 백드롭 갱신.
    void Close(UIView view);

    // 모달 스택 최상단 닫기 (back/ESC 대응).
    bool CloseTop();
}
```

- **모달 스택**: `Open`된 `UIPopup`은 스택에 push. 최상단 모달 바로 아래 **백드롭 `VisualElement`**(입력 차단 + 딤) 자동 삽입. `Close`/`CloseTop`/백드롭 클릭(팝업의 `autoClose`에 따라)으로 pop.
- **생성·주입**: `UIManager`가 주입받은 `IObjectResolver`로 `Resolve<TView>()` → ViewModel 자동 주입 → `UIRoot`의 해당 레이어에 `view.Root` 부착 → `view.OnOpen()` 호출.
- **수명**: `Close` 시 `view.OnClose()` → `view.Dispose()`(CompositeDisposable 해제) → VisualElement 분리.

**M1에서 의도적으로 미구현(훅만/추후):**

- **Open/Close 트랜지션·애니메이션**: base에 `virtual UniTask PlayOpenAsync()/PlayCloseAsync()`(기본 no-op) 훅만 두고 실제 연출은 필요해질 때.
- **씬 레벨 페이지 네비게이션(push/pop pages)**: LOP의 최상위 화면은 씬(Entrance/Lobby/Room/LOPGame)이 전환하므로 페이지 네비게이션 스택은 **YAGNI** — 두지 않음. 관리 모델은 팝업·오버레이만 책임.
- **씬 전환 자동 닫기**: 현 `PopupManager.AutoCloseAll` 대응은 M2(여러 화면이 들어올 때) 정합.

### Base 클래스

```csharp
// 순수 C# 뷰 컨트롤러. UXML 클론을 받아 바인딩.
public abstract class UIView : IDisposable
{
    public VisualElement Root { get; }              // UIManager가 UXML 클론 주입
    protected CompositeDisposable Disposables { get; }

    public virtual void OnOpen() { }
    public virtual void OnClose() { }
    protected virtual UniTask PlayOpenAsync() => UniTask.CompletedTask;
    protected virtual UniTask PlayCloseAsync() => UniTask.CompletedTask;
    public void Dispose() { /* Disposables.Dispose() */ }
}

// 모달 팝업. 백드롭/autoClose. 결과 반환은 파생에서 R3/UniTask로 노출.
public abstract class UIPopup : UIView
{
    public virtual bool AutoClose => true;          // 백드롭 클릭/씬전환 시 자동 닫힘
}
```

> UXML/USS 에셋은 View와 짝지어 로드한다(예: `Resources`/Addressables/`UxmlReference` 어트리뷰트). 정확한 로딩 방식은 plan에서 확정(아래 Open Decisions).

## LoginPopup 파일럿 전환

### 현재

- `LoginPopup : GameFramework.Popup` — UGUI `Button` 3개(GPGS/GameCenter/Guest), `onGuestLoginClick` 이벤트, `Show()`에서 플랫폼별 버튼 토글. 버튼은 UniRx `onClick.AsObservable()`.
- 소비자 `LoginComponent.Execute()`(`IEntranceComponent`): `PopupManager.instance.GetPopup<LoginPopup>()` → `onGuestLoginClick` 구독 → `Show()` → `UniTask.WaitUntil(loginResult != null)` → `Close()`.

### 신규 구조

**`LoginViewModel`** (순수 C#, DI):
- `Observable<LoginType> OnLoginRequested` (R3 `Subject` 노출) — Guest/GPGS/GameCenter 중 요청된 타입 발행.
- 플랫폼별 버튼 노출 여부: `bool ShowGuest/ShowGpgs/ShowGameCenter` (생성 시 `Application.platform`/전처리기로 결정. ViewModel은 순수 C#이라 플랫폼 분기 데이터만 보유, UI 토글은 View가).
- 커맨드: `RequestLogin(LoginType)` → `OnLoginRequested`에 발행.

**`LoginView : UIPopup`**:
- UXML: 버튼 3개(`#guest-login`, `#gpgs-login`, `#gamecenter-login`) + 백드롭은 매니저 제공.
- `OnOpen`: ViewModel의 `ShowXxx`에 따라 버튼 `display` 토글. 각 버튼 `clicked` → `viewModel.RequestLogin(...)`. (R3로 구독, `Disposables`에 등록)
- USS: 현 레이아웃 충실 이식 + 소폭 정돈.

**`LoginComponent` 재작성** (R3 결과 await):
```csharp
public async Task Execute()
{
    var autoLoginResult = await LoginService.instance.TryAutoLogin();
    if (autoLoginResult.success) return;

    var view = uiManager.Open<LoginView>(UILayer.Popup);
    LoginType type = await view.ViewModel.OnLoginRequested.FirstAsync();   // R3 → UniTask
    var loginResult = LoginService.instance.Login(type);
    uiManager.Close(view);

    if (!loginResult.success) throw new Exception(loginResult.reason);
}
```
- `LoginComponent`의 `IUIManager` 획득 방식(주입 vs 루트 스코프 resolve)은 plan에서 확정(엔트런스 컴포넌트 생성 경로 확인 필요).

### 제거/정리

- 기존 `Assets/Scripts/Popup/LoginPopup.cs` + `Assets/Popups/LoginPopup.prefab`(UGUI) 제거.
- `PrefabReferences`에서 LoginPopup 등록 제거(있다면).
- `CanvasManager`/GameFramework `PopupManager`/`Popup`은 **유지**(StatsPopup 등이 아직 사용).

## R3 셋업

- 클라 `Packages/manifest.json` + `Assets/NuGetForUnity/packages.config`에 R3 추가.
- 정확한 의존 구성(예: NuGetForUnity `R3` 코어 + `Microsoft.Bcl.TimeProvider`/`ObservableCollections` + UPM `R3.Unity` 통합 — `AddTo`, frame provider 제공) 은 **plan의 첫 셋업 태스크에서 확정**한다. 프로젝트는 이미 NuGetForUnity + UPM 병용.
- 검증: R3 `using R3;` 컴파일 통과 + 기존 UniRx 코드 동시 컴파일 통과(공존 확인).

## DI 결선

| 등록 | 스코프 | 비고 |
|---|---|---|
| `IUIManager` → `UIManager` | Root (앱 전역, DontDestroyOnLoad) | 기존 `CanvasManager`/`PopupManager`가 앱 전역인 것과 동일 위상 |
| `UIRoot`(UIDocument+PanelSettings GameObject) | Root | `UIManager`가 참조 보유 |
| `LoginView`, `LoginViewModel` | Transient | `UIManager`가 `IObjectResolver`로 resolve 시 ViewModel 자동 주입 |

> `UIManager`를 MonoBehaviour(UIRoot에 부착)로 둘지 순수 C#+별도 부트스트랩 MonoBehaviour로 둘지는 plan에서 확정. 어느 쪽이든 Root 스코프 Singleton으로 노출.

## 테스트 전략

- **`LoginViewModel`**(순수 C#): 가능하면 EditMode 단위 테스트. 단, **클라 전 코드가 단일 Assembly-CSharp라 EditMode asmdef가 직접 참조 불가**(기존 제약 — 클라 PlayMode는 리플렉션+빌드세팅 씬 패턴). 따라서 ViewModel 단위 테스트는 리플렉션 패턴이 가능하면 추가하되, **무리하지 않고 런타임 수동 검증을 1차**로 둔다(이전 World Core 슬라이스들과 동일 기조).
- **런타임 수동 검증** (Unity Play Mode):
  1. Entrance 진입 → 자동 로그인 실패 시 **로그인 팝업(UI Toolkit) 표시**
  2. Guest 로그인 버튼 클릭 → `LoginService.Login(Guest)` 호출 → 팝업 닫힘 → 정상 진행
  3. 플랫폼별 버튼 노출 토글 동작(에디터/Android/iOS)
  4. 모달 백드롭 표시 + 하단 입력 차단
  5. 에러 0건 (DI 누락 NRE 없음)

## Wiring & 변경 요약

| 파일/에셋 | 변경 |
|---|---|
| `Packages/manifest.json`, `NuGetForUnity/packages.config` | R3 추가 (M1) |
| 신규 `UIManager`, `UIView`, `UIPopup`, `UILayer` | 클라 UI 관리 인프라 |
| 신규 `UIRoot` UXML/USS + `PanelSettings` 에셋 | 레이어 컨테이너 |
| 신규 `LoginView`(+UXML/USS), `LoginViewModel` | 파일럿 |
| `LoginComponent.cs` | `IUIManager` + R3 결과 await로 재작성 |
| `Assets/Scripts/Popup/LoginPopup.cs`, `Assets/Popups/LoginPopup.prefab` | 제거 |
| Root `LifetimeScope` | `IUIManager`/`UIRoot`/`LoginView`/`LoginViewModel` 등록 |
| `CanvasManager`, GameFramework `PopupManager`/`Popup` | **유지**(M2+에서 정리) |

## Out of Scope

- M2~M5 전부(다른 화면, 월드 HUD, 터치 입력, GameFramework 승격).
- 전역 UniRx → R3 스왑(비-UI 코드 포함).
- Open/Close 트랜지션·애니메이션 구현(훅만).
- 씬 레벨 페이지 네비게이션 스택(YAGNI — 씬이 담당).
- UI 재디자인(스타일 소폭 정돈만).
- World Space `PanelSettings`(M3).

## Open Decisions (plan에서 해소)

- [ ] **View 호스팅 모델 확정** — 채택안: **단일 `UIRoot` UIDocument + 순수 C# View 컨트롤러 + UXML 클론**(패널 난립 없음, MVVM 정합). 대안: 화면당 UIDocument MonoBehaviour(현 `[DIMonoBehaviour]` 스캔과 더 가깝지만 패널 다수). 채택안으로 가되 plan 초반 스파이크로 확정.
- [ ] **UXML/USS ↔ View 로딩 방식** — `Resources` vs Addressables vs 직접 참조 에셋. 프로젝트가 맵 로딩에 Addressables 사용 중 → 통일 후보.
- [ ] **R3 정확한 패키지 구성** — 코어/Unity 통합/전이 의존 버전. plan 첫 태스크.
- [ ] **`UIManager` 형태** — MonoBehaviour(UIRoot 부착) vs 순수 C#+부트스트랩. Root 스코프 Singleton 노출은 공통.
- [ ] **`LoginComponent`의 `IUIManager` 획득** — 주입 vs 루트 스코프 resolve. 엔트런스 컴포넌트 생성 경로 확인.
- [ ] **`PanelSettings` 스케일 모드** — 현 Canvas Scaler 설정(해상도 대응) 대응값.

## 진행

- [x] 브레인스토밍 합의 (목표=가이드라인 정합, 화면 단위 점진, 100% UI Toolkit(월드 포함), R3 공존, 클라 우선 인프라, 로드맵 M1~M5 분해, 일반형 UI 관리 모델)
- [ ] 이 spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 M1 구현 plan 작성
- [ ] subagent-driven 또는 inline 실행

M1 완료 후 main tip = 신규 commit(피처 브랜치 `--no-ff` 머지). 이후 M2를 그때 brainstorm.
