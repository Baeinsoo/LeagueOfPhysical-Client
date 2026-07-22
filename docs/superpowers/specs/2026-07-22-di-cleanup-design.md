# DI 정리 — 생성자 주입 전환 · SceneLifetimeScope 제거 · 씬 주입 속성 리네임

VContainer DI 사용을 표준 관례에 맞게 정리한다. 서로 독립적인 세 작업으로 나뉜다.

- **A. 생성자 주입 전환** — 클라 순수 C# 서비스의 `[Inject]` 필드 주입 → 생성자 주입
- **B. SceneLifetimeScope 제거** — 죽은 정적 서비스 로케이터 + 클래스 자체 제거
- **C. 씬 주입 속성 리네임** — `[DIMonoBehaviour]`/`[DIGameObject]` → 씬 전용임을 이름에 명시

세 작업은 의존이 없다. 실행 순서는 **B → C → A**(작고 안전한 것 먼저, 크고 순환이 드러날 수 있는 A를 마지막).

## 배경 — 왜 하는가

VContainer 관례는 "순수 C#는 생성자 주입, MonoBehaviour는 `[Inject]` 메서드/필드"다. 현재 코드는 순수 C# 서비스까지 거의 전부 `[Inject]` 필드를 쓴다. 결과:

- 의존성이 생성자에 드러나지 않아 가독성·테스트성이 낮다.
- **필드 주입은 객체 생성 *후* 주입하므로, 순환 의존이 있어도 컨테이너가 빌드 시 검출하지 못하고 조용히 넘어간다.** 생성자 주입이면 `VContainerException: Circular dependency detected`로 빌드 시 터진다. 즉 지금은 순환을 영리하게 끊는 게 아니라 필드 주입이 순환을 덮고 있다.

또한 `SceneLifetimeScope`가 노출한 정적 `Resolve<T>()`/`Inject()`는 서비스 로케이터 안티패턴이나, **조사 결과 클라·서버 어디에서도 호출되지 않는 죽은 코드**다. 씬 주입 속성(`[DIMonoBehaviour]`/`[DIGameObject]`)은 씬 로드 시점 스캔이라 런타임 생성물을 커버하지 못하는데, 이름이 그 한계를 드러내지 않는다.

## 조사로 확정된 사실

- `SceneLifetimeScope.instance`/`Inject`/`Resolve` 정적 멤버는 **호출처 0**(클라·서버 전체).
- `SceneLifetimeScope`가 실제로 하는 유일한 일은 `Awake`에서 `Container.InjectSceneObjects(gameObject.scene)` 1회.
- `GameLifetimeScope`는 `SceneLifetimeScope`를 상속하지 않고 `LifetimeScope`를 직접 상속하며, `InjectSceneObjects`를 **`RegisterBuildCallback` 안에서** 호출한다. → 같은 일을 하는 방식이 이미 두 갈래로 갈려 있다.
- GameFramework/Shared 패키지엔 `[Inject]`가 **0개**. 생성자 전환은 전적으로 클라 내부 작업이며 공유 패키지·서버 런타임 코드에 churn이 없다.
- 클라에서 `[Inject]`를 쓰는 파일은 35개(MonoBehaviour + 순수 C# 혼재).
- GameFramework Editor에 `DIAttributeValidator`가 있어 `[DIGameObject]`/`[DIMonoBehaviour]`가 MonoBehaviour에 붙었는지 검사한다. 리네임 시 함께 갱신 대상.

## A. 생성자 주입 전환

### 범위

**클라 순수 C# 클래스만.** 파일별로 MonoBehaviour 상속 여부를 확인해 순수 C#만 전환한다. 후보(최종은 구현 시 확정):

- 메시지 핸들러 — `GameInfoMessageHandler`, `GameEntityMessageHandler`, `GameInputMessageHandler`, `GameInputTimingMessageHandler`, `GameWorldEventMessageHandler`, `RoomSessionMessageHandler`
- 넷코드 — `Reconciler`, `WorldEventSink`, `RemoteEntityInterpolator`, `LocalEntityInterpolator`, `LOPTickUpdater`, `RemoteInterpolationClock`
- 엔티티 — `EntityBinder`, `EntitySpawner`, `CharacterCreator`, `ItemCreator`
- 게임 — `AbilityActivator`, `AbilityDataProvider`, `StatusEffectDataProvider`, `PlayerHudCoordinator`
- UI — 순수 C# ViewModel(`DebugHudViewModel` 등)
- Entrance 컴포넌트 — 순수 C#이면 포함(`LoginComponent`/`CheckUserComponent`/`JoinLobbyComponent`/`LoadMasterDataComponent`)

### 제외

**MonoBehaviour는 생성자 주입 불가**이므로 제외하고 `[Inject]` 필드/메서드를 유지한다: `LOPRunner`, `LOPGamePresenter`, `LOPRoom`, `LOPNetworkAuthenticator`, `LoginService`, `WindowManager`, `LOPEntityView`, `CharacterNameplate`, `DamageFloaterEmitter`, `EntranceScene` 등. 이들은 씬 주입 속성 대상이기도 하다.

### 전환 형태

```csharp
// Before
public class EntitySpawner
{
    [Inject] private EntityRegistry entityRegistry;
    [Inject] private ActorRegistry actorRegistry;
}

// After
public class EntitySpawner
{
    private readonly EntityRegistry entityRegistry;
    private readonly ActorRegistry actorRegistry;

    public EntitySpawner(EntityRegistry entityRegistry, ActorRegistry actorRegistry)
    {
        this.entityRegistry = entityRegistry;
        this.actorRegistry = actorRegistry;
    }
}
```

VContainer 등록(`Register<EntitySpawner>(Lifetime.Singleton)`)은 그대로 두면 컨테이너가 생성자를 찾아 주입한다. 등록 코드 변경은 없다.

### 순환 검출 처리

전환 도중 `Circular dependency detected`가 나면, 그 순환이 이번 정리가 드러내려던 진짜 순환이다. 발견 시 **바로 고치지 말고 기록**한다(어느 두 클래스가 서로 참조하는지). 후속 6번(FSM)·7번(순서 의존) 판단의 입력이 된다. 필드 주입으로 되돌려 덮지 않는다.

### 테스트

생성자 주입으로 바뀌면 `new`로 인스턴스화가 가능해진다. 의존성이 인터페이스/스텁 가능한 클래스는 EditMode 단위 테스트를 추가한다. 단, 클라 코드는 Assembly-CSharp라 asmdef가 참조할 수 없으므로, 테스트가 가능한 것은 순수 로직이 패키지(GameFramework/Shared)에 있는 경우로 제한된다. 클라 Assembly-CSharp에 묶인 클래스는 단위 테스트 대상에서 제외하고 전환만 한다.

## B. SceneLifetimeScope 제거

### 단계

1. 정적 멤버 `instance`/`Inject`/`Resolve` 삭제 — 호출처 0.
2. `SceneLifetimeScope` 클래스 + `.meta` 삭제.
3. `EntranceLifetimeScope`/`LobbyLifetimeScope`/`RoomLifetimeScope`를 `LifetimeScope` 직접 상속으로 변경.
4. 각 스코프의 `RegisterBuildCallback`에서 `container.InjectSceneObjects(gameObject.scene)` 호출 — `GameLifetimeScope`와 동일 패턴으로 통일.
5. 반복되는 그 호출은 확장 헬퍼로 추출해 4개 스코프가 공유한다(선택). 예: `builder.InjectThisScene(this)` — `this`(LifetimeScope)의 `gameObject.scene`을 빌드 콜백에서 주입.
6. **서버도 동일 구조**(`SceneLifetimeScope` + Entrance/Room 상속)이므로 같은 변경을 적용한다.

### 주의

- `LobbyLifetimeScope`는 현재 `base.Configure`를 부르지 않는다 — 상속 제거 후에도 동작 동일(직접 `InjectSceneObjects` 호출로 대체).
- 씬 오브젝트 주입 시점이 `Awake`(base 클래스 방식)에서 빌드 콜백(build-time)으로 바뀌지만, 둘 다 컨테이너 빌드 시점이라 실효 동일.

## C. 씬 주입 속성 리네임

씬 로드 시점 스캔이라는 한계를 이름으로 드러낸다. 스캔 로직은 그대로 두고 이름만 바꾼다(동적 생성물 커버는 범위 밖 — 지금처럼 `EntityBinder`의 수동 주입 유지).

### 리네임

- `DIMonoBehaviourAttribute` → `SceneInjectMonoBehaviourAttribute` (단일 컴포넌트 주입, `IObjectResolver.Inject`)
- `DIGameObjectAttribute` → `SceneInjectGameObjectAttribute` (하위 트리 전체 주입, `IObjectResolver.InjectGameObject`)

두 속성의 구분(단일 vs 하위 트리)은 실제 동작 차이라 유지한다.

### 갱신 대상

- GameFramework `Runtime/Scripts/DI/` 속성 정의 2개 (파일명도 클래스명과 일치하게 rename, `.meta`는 git mv)
- GameFramework `Editor/Scripts/DI/DIAttributeValidator.cs` — `typeof` 참조 갱신
- `Runtime/Scripts/Extensions/DI.Extensions.cs` — `FindGameObjectsWithAttribute<...>`/`FindComponentsWithAttribute<...>`의 타입 인자
- XML 문서 주석("SceneLifetimeScope의 스캔에 의해…") — B에서 클래스가 사라지므로 문구도 "씬 스코프의 스캔"으로 정정
- 클라 사용처: `LOPRunner`, `LOPGamePresenter`, `LoginService`, `LOPNetworkAuthenticator`
- 서버 사용처: `LOPRunner`, `LOPNetworkAuthenticator`

## 산업 표준 매핑

- **생성자 주입 우선**: VContainer 공식 문서 — 순수 C#는 생성자 주입, MonoBehaviour는 `[Inject]` 메서드. 생성자 주입은 순환을 빌드 타임에 `VContainerException`으로 검출.
- **씬 오브젝트 자동 주입**: VContainer 네이티브 `autoInjectGameObjects`(인스펙터 리스트) + `Container.InjectGameObject`. 본 프로젝트의 속성 스캔은 이 리스트를 속성 기반으로 대체한 것(리팩터 안전). 런타임 생성물은 `IObjectResolver.Instantiate`/`InjectGameObject`가 표준 — 스캔과 별개 축.
- **서비스 로케이터 회피**: `IObjectResolver.Resolve` 직접 호출은 공식 문서가 비권장. 정적 `Resolve` 제거는 이에 정합.

## 범위 밖 (후속 판단)

- **6. 매칭 FSM의 `IObjectResolver.Resolve` → `Func<T>` 팩토리** — A 완료 후 순환 상황 확인하고 진행 여부 결정.
- **7. HUD 등록 순서 의존성(EntityBinder → PlayerHudCoordinator)** — A 완료 후 결정.
- **동적 생성물 경로 정리**(런타임 스폰을 `resolver.Instantiate`/팩토리로 통일) — 이번엔 안 함(사용자 결정 (가): 이름만 정직하게).

## 검증

- 클라·서버 양쪽 컴파일 통과(변경이 6레포 중 GameFramework/클라/서버 3곳에 걸침).
- 플레이 스모크: 엔트런스 로그인 → 로비 매칭 → 룸/게임 진입까지 씬 오브젝트 주입이 정상 동작(`LOPRunner` 등 `[SceneInject…]` 대상이 주입되는지)하는지 확인.
- A 전환 후 게임 스코프 빌드가 순환 예외 없이 통과하는지(또는 순환이 드러나면 기록).
