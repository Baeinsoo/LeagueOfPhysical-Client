# 아키텍처 가이드라인

Unity 프로젝트의 설계 패턴·폴더 구조·레이어 책임·코딩 컨벤션을 정리한 문서. 다른 프로젝트에서 사용한 구조를 도메인 비종속적으로 재정리한 것이다.


## 설계 패턴 & 원칙

- **기본 패턴**: CBD (Component-Based Design)
- **폴더 구조**: Feature-based — 기능 단위로 관련 코드(Model, System, Component, UI)를 한 곳에 모음
- **데이터/로직 분리**: 불변 설정 데이터는 ScriptableObject, 런타임 게임 상태와 비즈니스 로직은 순수 C#
- **순수 C# 모델**: Anemic Domain Model — 모델은 데이터와 파생 속성(읽기 전용 계산값)만 소유한다. 생성자에서 구조적 무결성(null 검사, 필수 필드 유효성)은 검증하되, 상태 변경 메서드나 도메인 처리 로직은 두지 않는다. 모든 처리 로직은 System에 둔다
- **이벤트/반응형**: R3 (Reactive Extensions) 통일 — SO 이벤트 채널(Ryan Hipple) 패턴은 사용하지 않음
- **게임플레이 월드**: CBD 기반 MonoBehaviour — MonoBehaviour는 View 또는 Controller 역할을 할 수 있으며, 역할이 명확하면 분리한다. Unity 엔진 기능(Collider 반응, Animator 구동 등)과 강결합되어 분리가 오히려 복잡성을 높이는 경우에만 하나의 MonoBehaviour가 두 역할을 겸한다. 비즈니스 로직과 게임 상태는 순수 C# 레이어에 위임
- **UI**: MVVM + Unity UI Toolkit (UXML/USS). ViewModel(순수 C#)과 View를 분리하되, **R3 중심 바인딩**(ViewModel이 R3로 상태/이벤트 노출 → View가 구독)을 사용한다. 화면 전시·중첩은 **레이어 × 스택 윈도우 매니저**로 관리. 상세는 "UI 아키텍처" 절 참고
- **의존성 주입**: VContainer

## 폴더 구조

기능 단위로 코드를 모으고, 레이어별로 asmdef를 분리한다. 아래 asmdef 라벨의 어셈블리명은 프로젝트 네이밍 규칙을 따르는 것으로, 여기서는 레이어 이름만 표기한다.

```
Assets/Scripts/
  Core/                     # [asmdef: Core]
    Interfaces/             # 공통 인터페이스 (IDisposable 래퍼, IService 등)
    Extensions/             # 확장 메서드
    Constants/              # 전역 상수
  Infrastructure/           # [asmdef: Infrastructure]
    Audio/                  # AudioService
    SceneManagement/        # SceneLoader, SceneTransition
    Pooling/                # ObjectPool
    Save/                   # 저장/로드 시스템
    Input/                  # InputAction 래핑, 입력 추상화
    EventBus/               # R3 기반 글로벌 이벤트 버스
  Features/                 # [asmdef: Features]
    {FeatureName}/          # 피처 단위 폴더 (예: Combat, Inventory)
      Models/               # 게임 상태 모델 (순수 C#)
      Systems/              # 게임 로직 (순수 C#, VContainer ITickable/IStartable)
      Components/           # MonoBehaviour (View+Controller 통합)
      UI/
        ViewModels/         # ViewModel (순수 C#)
        Views/              # View — UXML 트리 소유 + ViewModel의 R3 구독 (VisualElement 파생 또는 컨트롤러)
        Uxml/               # UXML 레이아웃
        Uss/                # USS 스타일시트
      Data/                 # ScriptableObject (불변 설정 데이터)
      {FeatureName}Installer.cs   # VContainer LifetimeScope (피처별)
  Shared/                   # [asmdef: Shared]
    Data/                   # 여러 피처가 공유하는 ScriptableObject
    UI/                     # 공통 UI 컴포넌트 (HealthBar, Tooltip 등)
      Uxml/
      Uss/
  Installers/               # [asmdef: Installers]
    RootInstaller.cs        # 프로젝트 루트 LifetimeScope
    SceneInstaller.cs       # 씬 단위 LifetimeScope
Assets/Tests/
  EditMode/                 # [asmdef: Tests.EditMode]
  PlayMode/                 # [asmdef: Tests.PlayMode]
```

> **피처 폴더 규칙**: 모든 기능은 `Features/{FeatureName}/` 아래에 관련 코드를 모은다. 하위 폴더(Models, Systems, Components, UI, Data)는 해당 피처에 필요한 것만 생성한다 — 빈 폴더를 미리 만들지 않는다.

## 레이어 책임

| 레이어 | 타입 | 역할 |
|---|---|---|
| Core | 순수 C# | 인터페이스, 확장 메서드, 상수 — 다른 레이어에 의존하지 않음 |
| Infrastructure | 순수 C# + MonoBehaviour | 크로스커팅 서비스 (Audio, Scene, Pool, Save, EventBus) |
| Features/*/Models | 순수 C# (Anemic Domain Model) | 피처별 게임 상태, 도메인 모델 — 데이터와 파생 속성만 소유. 생성자에서 구조적 무결성만 검증 |
| Features/*/Systems | 순수 C# | 여러 모델 간 조율, 외부 의존이 필요한 로직 (VContainer `ITickable`/`IStartable`로 생명주기 구동) |
| Features/*/Data | ScriptableObject | 피처별 디자이너 설정값, 불변 데이터 |
| Features/*/UI | ViewModel + View | 피처별 MVVM UI — ViewModel은 순수 C#(R3로 상태/이벤트 노출), View는 UXML 트리를 소유하고 R3를 구독하는 얇은 바인더 |
| Features/*/Components | MonoBehaviour | Unity 엔진 연동 — View 또는 Controller 역할 (역할이 명확하면 분리, 강결합 시 통합) |
| Shared | 혼합 | 여러 피처가 공유하는 데이터, UI 컴포넌트 |
| Installers | VContainer LifetimeScope | 루트/씬 단위 DI 바인딩 등록 |

> **게임플레이 월드**: MonoBehaviour는 View 또는 Controller 역할을 할 수 있으며, 역할이 명확하면 분리한다. Unity 엔진 기능(Collider 반응, Animator 구동 등)과 강결합되어 분리가 오히려 복잡성을 높이는 경우에만 하나의 MonoBehaviour가 두 역할을 겸한다. 비즈니스 로직과 게임 상태는 반드시 순수 C# 레이어(Models/Systems)에 둘 것.
>
> **UI**: ViewModel(순수 C#)과 View를 분리하는 MVVM을 적용한다. View는 UXML/USS 트리를 소유하고 ViewModel의 R3 상태를 구독해 갱신하는 얇은 바인더다(VisualElement 파생 또는 컨트롤러 — 둘 다 허용). 화면 관리·바인딩 결정은 "UI 아키텍처" 절 참고.
>
> **Model/System 로직 분배 기준**:
> - **Model 내부**: 데이터 프로퍼티, 파생/계산 속성(읽기 전용), 생성자의 구조적 무결성 검증(null 검사, 필수 필드 유효성)
> - **System**: 상태 변경, 단일 모델 범위의 도메인 로직(예: `TakeDamage`, `Heal`), 여러 모델 간 조율, 외부 의존성이 필요한 로직(예: 데미지 계산, 턴 진행)
>
> **Systems 생명주기**: `Features/*/Systems`의 순수 C# 클래스는 VContainer의 `ITickable`(매 프레임), `IStartable`(초기화), `IAsyncStartable`(비동기 초기화)을 구현하여 MonoBehaviour 없이 생명주기를 갖는다.

## Assembly Definition 전략

의존 방향을 컴파일 타임에 강제하고, 증분 컴파일을 최적화하기 위해 asmdef를 레이어별로 분리한다.

```
의존 방향 (→ = 참조 가능):

Installers → Features, Infrastructure, Shared, Core
Features → Infrastructure, Shared, Core
Infrastructure → Core
Shared → Core
Tests.EditMode → Features, Infrastructure, Shared, Core
Tests.PlayMode → 전체

※ Features 간 직접 참조 금지 — 피처 간 통신은 Infrastructure/EventBus(R3)를 통해서만
※ Core는 어떤 레이어도 참조하지 않음 (최하위 의존성)
```

| Assembly Definition | 경로 | 참조 대상 |
|---|---|---|
| Core | `Assets/Scripts/Core/` | (없음) |
| Infrastructure | `Assets/Scripts/Infrastructure/` | Core |
| Shared | `Assets/Scripts/Shared/` | Core |
| Features | `Assets/Scripts/Features/` | Infrastructure, Shared, Core |
| Installers | `Assets/Scripts/Installers/` | Features, Infrastructure, Shared, Core |
| Tests.EditMode | `Assets/Tests/EditMode/` | Features, Infrastructure, Shared, Core |
| Tests.PlayMode | `Assets/Tests/PlayMode/` | 전체 |

> 모든 asmdef에 `VContainer`, `UniTask`, `R3` 패키지 참조를 필요에 따라 추가한다. `Auto Referenced`는 false로 설정하여 불필요한 컴파일 의존을 방지한다.

## R3 (Reactive Extensions) 사용 기준

이벤트와 반응형 데이터 바인딩은 **R3로 통일**한다. ScriptableObject 이벤트 채널(Ryan Hipple 패턴)은 사용하지 않는다.

**사용 영역:**

| 용도 | R3 타입 | 예시 |
|---|---|---|
| 상태 변경 알림 | `ReactiveProperty<T>` | `ReactiveProperty<int> Hp` — Model이 소유, ViewModel/Component가 구독 |
| 일회성 이벤트 | `Observable` (Subject) | `Subject<DamageEvent>` — System이 발행, Component가 구독 |
| 글로벌 이벤트 버스 | `MessageBroker` (R3 기반) | 피처 간 통신 — `Infrastructure/EventBus/` |
| UI 바인딩 | `ReadOnlyReactiveProperty<T>` | ViewModel이 Model의 RP를 변환해 노출, **View가 구독**해 VisualElement 갱신 (Unity6 네이티브 런타임 바인딩 대신 R3 중심 — "UI 아키텍처" 결정 참고) |
| 컬렉션 변경 | `ObservableList<T>` | 인벤토리 아이템 목록 등 |

**구독 해제 규칙:**
- MonoBehaviour: `this.destroyCancellationToken` 또는 `AddTo(this)` 사용
- 순수 C# (VContainer 관리): `IDisposable`로 `CompositeDisposable`에 모아서 `Dispose()`
- ViewModel: `CompositeDisposable` 패턴, View가 해제 시 함께 Dispose

**피처 간 통신:**
- 피처 간 직접 참조 금지 — `Infrastructure/EventBus/`의 R3 기반 메시지 브로커를 통해서만 통신
- 이벤트 정의는 발행하는 피처의 `Models/` 또는 `Shared/`에 위치

## UI 아키텍처 (UI Toolkit + MVVM)

### 바인딩 — R3 중심
- ViewModel(순수 C#)이 상태를 R3 `ReadOnlyReactiveProperty<T>`로, 일회성/커맨드를 `Observable`(Subject)로 노출한다. View는 이를 **구독**해 `VisualElement`를 갱신하고, 사용자 입력(버튼 `clicked` 등)은 ViewModel 커맨드로 전달한다.
- **결정 — Unity 6 런타임 데이터 바인딩 미채택(R3 유지)**: Unity 6의 런타임 데이터 바인딩은 *값(프로퍼티) 바인딩* 수준으로, 커맨드 바인딩 부재 · `[CreateProperty]`/`INotifyBindablePropertyChanged` 보일러플레이트 · 미성숙 이슈가 있다. 반응형 컴포지션은 R3가 우월하므로 **R3 중심 바인딩을 기본**으로 한다. 값이 많은 정적 화면에서 선언적 이득이 분명하면 네이티브 바인딩을 *선택적으로* 병용할 수 있다.

### View 구조
- 화면은 UXML/USS로 정의하고, View는 그 트리를 소유하며 ViewModel의 R3를 구독하는 **얇은 바인더**다. 구현은 **`VisualElement` 파생 컴포넌트(`[UxmlElement]`)** 또는 **컨트롤러 클래스** 중 무엇이든 가능하다(둘 다 UI Toolkit에서 통용). View에는 비즈니스 로직을 두지 않는다.
- 구독은 View 수명에 묶어 `CompositeDisposable`로 해제한다.

### 화면 관리 — 레이어 × 스택 윈도우 매니저
화면 전시·중첩·전환은 **레이어(z-순서 밴드) × 스택** 모델의 윈도우 매니저가 관리한다(Unreal CommonUI/Lyra, UnityScreenNavigator 등에서 검증된 표준):

- **레이어(밴드)**: z-순서가 고정된 소수의 상호작용 등급. 각 화면은 자기 밴드를 선언한다. 하위 밴드는 상위 밴드 위로 올라가지 못하므로 "시스템/모달 항상 최상단" 같은 요구를 **밴드 우선순위로 선언적 보장**한다. 기본 밴드: `Window`(주 화면) < `Popup`(모달 다이얼로그) < `Notification`(토스트) < `System`(시스템/치명/항상 최상단). 밴드 세트는 프로젝트에 맞게 확장 가능.
- **스택(밴드 내부)**: push/pop(LIFO)으로 중첩. 모달 push 시 백드롭(딤 + 입력 차단)을 top 아래에 삽입. 입력 포커스는 *가장 높은 가시 밴드의 top*.
- **렌더 구성**: 단일 루트 UIDocument 안에 밴드 컨테이너 `VisualElement`를 z-순서대로 두고, 각 컨테이너가 자기 스택을 관리. UI Toolkit 전용 성숙 라이브러리가 드물어 보통 직접 구현한다.
- **월드 추적 UI**(머리 위 체력바·플로팅 데미지 등)는 이 스크린스페이스 매니저 밖이며, **World Space `PanelSettings`**(별도 렌더)로 다룬다.

### DI
- UI 인프라(윈도우 매니저 등)는 **전용 UI Installer**로 등록해 앱/씬 스코프와의 결합을 분리한다.

> 업계 근거: Unreal Engine CommonUI/Lyra(레이어별 activatable 위젯 스택, ZOrder, push/pop, 최상위 가시 레이어 top에 입력 포커스), UnityScreenNavigator(Page/Modal/Sheet 스택 + 모달 입력 차단). 소규모 프로젝트에서는 밴드 수를 최소화해 과설계를 피한다.

## ScriptableObject 사용 기준

ScriptableObject는 **디자이너가 에디터에서 편집하는 불변 설정 데이터 전용**으로 사용한다.

**사용하는 경우:**
- 디자이너가 조정하는 설정값 (ItemData, EnemyData 등 Config/Tuning)
- enum 대신 타입 안전 상수 (ItemType.asset 등 에셋으로 참조)

**사용하지 않는 경우:**
- 이벤트 채널 — R3 Observable로 대체
- 공유 변수/런타임 상태 — R3 ReactiveProperty로 대체
- 플레이어 런타임 상태 (HP, 위치 등) — SO는 에디터에서 값이 유지되어 테스트 오염 위험
- 씬별로 다른 인스턴스가 필요한 데이터 — 모든 씬이 같은 에셋을 공유하므로 격리 불가
- 복잡한 비즈니스 로직 — 순수 C# 클래스가 테스트 가능성 높음
- 자주 생성/소멸되는 객체 — SO는 에셋이므로 런타임 동적 생성에 부적합

**원칙:** SO는 `Features/*/Data/` 또는 `Shared/Data/`에 위치하며, 기획에서 정의하고 게임 중 변하지 않는 불변 데이터만 담는다. 플레이 중 변하는 상태는 순수 C# 모델 + R3 ReactiveProperty에 둘 것.

## 렌더러 구성

듀얼 플랫폼 타깃팅을 위해 렌더러 에셋을 분리한다.
- `Assets/Settings/PC_Renderer.asset` — 고품질 PC 렌더링
- `Assets/Settings/Mobile_Renderer.asset` — 모바일 최적화 렌더링

포스트 프로세싱은 `Assets/Settings/DefaultVolumeProfile.asset`로 제어한다.

## Input System

입력은 New Unity Input System(레거시 `Input` 클래스 미사용)을 사용하며 `.inputactions` 에셋으로 구성한다. 새 입력 바인딩은 이 에셋에 추가한다.

## 테스팅

테스트 코드 작성 및 유닛 테스트 수행은 기본 개발 방식이다.

- 새 기능 구현 시 테스트 코드를 함께 작성할 것
- 순수 C# 클래스(Models, Systems 등)는 EditMode 테스트로 Unity 없이 단위 테스트 가능
- MonoBehaviour 관련 통합 테스트는 PlayMode 테스트 사용
- 테스트는 `Assets/Tests/EditMode/` 또는 `Assets/Tests/PlayMode/`에 위치
- 실행: Unity Editor의 **Window > General > Test Runner**

## 향후 고려사항

게임 설계가 구체화되면 논의할 항목들:

- [ ] **피처별 asmdef 분리** — 단일 `Features` 어셈블리로는 피처 간 직접 참조를 컴파일 타임에 차단할 수 없음. 피처가 5개 이상이고 **팀 규모가 커져서 피처 오너십 분리가 필요할 때** 피처별 asmdef로 분리 검토. 소규모 팀에서는 단일 `Features` asmdef 유지가 실용적
- [ ] **게임 상태(State) 관리 패턴** — Menu → Loading → Gameplay → Pause → GameOver 등 앱 레벨 상태 전환 전략. VContainer `LifetimeScope` 계층과 연동하는 State Machine 또는 씬 기반 상태 관리
- [ ] **씬 전략** — 단일 씬 vs 멀티 씬(Additive Loading) 결정, Addressables 도입 여부
- [ ] **로깅/디버그 전략** — `Debug.Log` 래핑, 조건부 로깅, 릴리즈 빌드 시 로그 스트리핑

## 코드 컨벤션

- **C# 표준 코딩 컨벤션**을 따른다 ([Microsoft C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions))
- 모든 게임 스크립트는 C#이며 `Assets/` 아래에 둔다
- 스크립트는 기능/시스템 단위 하위 디렉터리로 정리한다
- 프로젝트는 **Linear color space**를 사용한다 (Player Settings)

### 주석 컨벤션

주석은 **최소화**한다. 코드로 자명한 내용(무엇을 하는지)은 주석을 달지 않고, **비자명한 의도(왜)**나 **도메인 로직**처럼 코드만으로 드러나지 않는 것만 설명한다.

- **public 타입·멤버**를 문서화할 때는 `/// <summary>` XML 문서화 주석을 쓴다 (C# 표준, IntelliSense·문서화 도구가 읽음)
- **구현 내부**의 의도 설명이 꼭 필요하면 간결한 `//` 한 줄로 한다
- 코드를 그대로 풀어 읽는 식의 주석(`// game을 생성한다` 위에 `_game = Create()`)은 달지 않는다

### Git 워크플로우

- **main 브랜치에 직접 커밋 금지** — 모든 기능 구현/수정은 반드시 피처 브랜치에서 작업
- 작업 시작 시(코드 변경 전) 워크트리를 생성하고 피처 브랜치에서 진행
- 완료 후 main에 `--no-ff` 머지 (merge commit 생성)
- spec/plan 문서 커밋도 피처 브랜치에서 수행

### Unity .meta 파일 관리

- Unity가 생성하는 `.meta` 파일은 **반드시 git에 커밋**해야 한다 — GUID와 import 설정을 담고 있어 누락 시 에셋 참조가 깨짐
- 새 스크립트/에셋/폴더를 생성한 뒤에는 Unity가 생성한 `.meta` 파일을 함께 커밋할 것
- `.meta` 파일은 직접 생성하거나 수정하지 않는다 — Unity Editor가 자동 생성한 것만 커밋
