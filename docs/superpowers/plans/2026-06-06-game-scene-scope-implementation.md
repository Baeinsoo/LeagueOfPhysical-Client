# Game을 LOPGame 씬으로 분리 — 구현 Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax. 이 작업은 Unity 씬 작업(MCP)이 많아 **inline 실행**이 적합하다. 각 Task 끝에서 컴파일을 확인하고, 런타임 검증은 전환이 끝나는 Task 6에서 한다.

**Goal:** 게임 시스템(플레이 전체)을 `LOPGame` 전용 씬으로 옮기고, `LOPRoom`이 그 씬을 Room 스코프의 자식으로 additive 로딩하도록 전환한다.

**Architecture:** VContainer `LifetimeScope.EnqueueParent(roomScope)` + `Enqueue(RegisterInstance(gameInfo))`로 LOPGame 씬의 `GameLifetimeScope`를 Room 자식으로 빌드. 게임 `[DI*]`가 한 씬에 응집되어 컨테이너 경계 = 씬 경계. 씬 로딩은 `SceneManager`(Additive).

**Tech Stack:** Unity 6000.3, VContainer, UniTask, Mirror, UnityMCP(클라 인스턴스 `LeagueOfPhysical-Client@<hash>`).

**Spec:** `docs/superpowers/specs/2026-06-06-game-scene-scope-design.md`

> ⚠️ **전환은 빅뱅 성격이다.** 게임 객체가 반쯤 옮겨진 상태로는 동작하지 않으므로, Task 1~5는 컴파일만 확인하고 런타임 동작 검증은 Task 6에 모은다. 위험을 줄이려 inline + Task별 사용자 체크포인트로 진행한다.

---

## 게임 객체 분류 (Room 씬 → 어디로)

이번 세션에서 파악한 Room 씬 루트 기준.

| GameObject | 컴포넌트 | 행선지 |
|---|---|---|
| `RoomLifetimeScope` | RoomLifetimeScope | **Room 잔류** |
| `LOPRoom` | LOPRoom | **Room 잔류** |
| `NetworkManager` | LOPNetworkManager | **Room 잔류** |
| `LOPGame` | LOPGame, LOPGamePresenter | LOPGame |
| `LOPGame/LOPGameEngine` | LOPGameEngine, LOPEntityManager, LOPTickUpdater | LOPGame |
| `CameraController` | CameraController | LOPGame |
| `Main Camera` | Camera | LOPGame |
| `Canvas` (+ 하위 게임 UI: GamePad, CameraTouchController, *Button, TimeUI, CharacterUI, StatsPopup) | 게임 UI | LOPGame |
| `EventSystem` | EventSystem, InputSystemUIInputModule | LOPGame |
| `Directional Light` | Light | LOPGame |

> Room 씬은 전환 후 카메라/UI가 없다. Room 입장 직후 LOPGame 로드 전까지 화면이 비지만, 입장→로드가 즉시라 허용한다. (필요 시 Room 씬에 로딩 표시는 별도 작업.)

---

## Task 1: `GameLifetimeScope` + 씬 주입 확장

게임 스코프 클래스와, "한 씬의 `[DI*]`를 주어진 컨테이너로 주입"하는 확장을 만든다. (게임 객체가 LOPGame 씬에 응집되므로 `GameLifetimeScope`는 자기 씬만 주입한다.)

**Files:**
- Create: `Assets/Scripts/Game/GameLifetimeScope.cs`
- Modify: `Assets/Scripts/Extensions/DI.Extensions.cs`

- [ ] **Step 1: `DI.Extensions.cs`에 씬 주입 확장 추가**

기존 파일 끝의 `GetOrAddComponentWithInject` 아래(클래스 안)에 추가:

```csharp
        // 한 씬의 [DIGameObject]/[DIMonoBehaviour] 마킹 객체를 주어진 컨테이너로 주입한다.
        public static void InjectSceneObjects(this IObjectResolver resolver, Scene scene)
        {
            foreach (var DIGameObject in scene.FindGameObjectsWithAttribute<DIGameObjectAttribute>().OrEmpty())
            {
                resolver.InjectGameObject(DIGameObject);
            }

            foreach (var DIMonoBehaviour in scene.FindComponentsWithAttribute<DIMonoBehaviourAttribute>().OrEmpty())
            {
                resolver.Inject(DIMonoBehaviour);
            }
        }
```

파일 상단 using에 `using GameFramework;`, `using UnityEngine.SceneManagement;`, `using VContainer.Unity;`가 없으면 추가. (`GameFramework` = 확장/attribute, `SceneManagement` = `Scene`, `VContainer.Unity` = `InjectGameObject`)

- [ ] **Step 2: `GameLifetimeScope.cs` 작성**

```csharp
using GameFramework;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    // LOPGame 씬 루트의 게임 스코프. EnqueueParent(Room)로 로드되면 Room 자식으로 자동 빌드된다.
    // 게임 객체가 LOPGame 씬에 응집되므로 자기 씬만 주입한다. gameInfo 등 런타임 인자는
    // LOPRoom이 Enqueue(RegisterInstance(...))로 주입한다.
    public class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private LOPGame game;
        [SerializeField] private LOPGameEngine gameEngine;
        [SerializeField] private CameraController cameraController;

        protected override void Configure(IContainerBuilder builder)
        {
            // World Core
            builder.Register<GameFramework.World.EntityRegistry>(Lifetime.Singleton);
            builder.Register<GameFramework.World.WorldEventBuffer>(Lifetime.Singleton);
            builder.Register<GameFramework.World.HealthSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.World.WorldEventApplicator>(Lifetime.Singleton);
            builder.Register<WorldEventBridge>(Lifetime.Singleton);

            // 게임 MonoBehaviour 인스턴스 (LOPGame 씬 내부 참조 — 안정적)
            builder.RegisterComponent(game).As<IGame>();
            builder.RegisterComponent(gameEngine).As<IGameEngine>();
            builder.RegisterComponent(cameraController);

            // 게임 서비스
            builder.Register<IGameMessageHandler, GameEntityMessageHandler>(Lifetime.Transient);
            builder.Register<IGameMessageHandler, GameInputMessageHandler>(Lifetime.Transient);
            builder.Register<IGameMessageHandler, GameDamageMessageHandler>(Lifetime.Transient);
            builder.Register<PlayerInputManager>(Lifetime.Singleton).AsSelf();
            builder.Register<IActionManager, LOPActionManager>(Lifetime.Singleton);
            builder.Register<IMovementManager, LOPMovementManager>(Lifetime.Singleton);
            builder.Register<IEntityCreator, CharacterCreator>(Lifetime.Singleton);
            builder.Register<IEntityCreator, ItemCreator>(Lifetime.Singleton);
            builder.Register<IEntityFactory, EntityFactory>(Lifetime.Singleton);

            // 빌드 완료 후 자기 씬의 [DI*]를 이 컨테이너로 주입
            builder.RegisterBuildCallback(container =>
            {
                container.InjectSceneObjects(gameObject.scene);
            });
        }
    }
}
```

- [ ] **Step 3: 컴파일 확인**

UnityMCP `refresh_unity`(compile, force, all) → `read_console`(error). 새 파일 import를 위해 `scope=all`. (신규 `.cs`는 첫 컴파일에서 "type not found"가 날 수 있으니 한 번 더 `all` refresh 후 0 에러 확인.)
Expected: error 0건. (아직 `GameLifetimeScope`를 쓰는 곳이 없어 통과.)

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/Game/GameLifetimeScope.cs Assets/Scripts/Game/GameLifetimeScope.cs.meta Assets/Scripts/Extensions/DI.Extensions.cs
git commit -m "feat(di): add GameLifetimeScope + InjectSceneObjects extension"
```

---

## Task 2: `LOPGame` 빈 씬 생성 + Build Settings 등록

**Files:**
- Create: `Assets/Scenes/LOPGame.unity`
- Modify: Build Settings (scene list)

- [ ] **Step 1: 빈 씬 생성**

UnityMCP `manage_scene(action=create, name="LOPGame", path="Assets/Scenes/LOPGame.unity")` (클라 인스턴스). 기본 Camera/Light는 Task 3에서 Room으로부터 옮겨오므로, 생성된 기본 오브젝트는 Task 3에서 정리한다.

- [ ] **Step 2: Build Settings에 추가**

UnityMCP `manage_build(action=scenes, ...)`로 `Assets/Scenes/LOPGame.unity`를 빌드 씬 목록에 추가(enabled). `SceneManager.LoadSceneAsync("LOPGame", Additive)`가 이름으로 찾을 수 있어야 한다.

- [ ] **Step 3: 확인**

`manage_scene(action=get_build_settings)`로 LOPGame가 목록에 enabled로 있는지 확인.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scenes/LOPGame.unity Assets/Scenes/LOPGame.unity.meta "ProjectSettings/EditorBuildSettings.asset"
git commit -m "feat(scene): add empty LOPGame scene + register in build settings"
```

---

## Task 3: 게임 객체를 LOPGame 씬으로 이동 + `GameLifetimeScope` 배치

Room 씬의 게임 객체를 LOPGame 씬으로 옮긴다. `move_to_scene`은 GameObject를 통째로 옮기므로 **씬 내부 직렬화 참조는 보존**되고, cross-scene이 되는 참조(룸↔게임)만 Task 4에서 끊는다.

> 도메인 리로드로 instanceID가 바뀔 수 있으니, 각 단계에서 `find_gameobjects`/`manage_scene get_hierarchy`로 **현재 instanceID를 재조회**해 사용한다.

**Files:**
- Modify: `Assets/Scenes/Room.unity`, `Assets/Scenes/LOPGame.unity`

- [ ] **Step 1: Room 씬 로드 + 게임 객체 instanceID 재조회**

`manage_scene(action=load, path="Assets/Scenes/Room.unity")` 후 `get_hierarchy`로 루트 목록 + instanceID 확보. 분류표(위)의 LOPGame 행 객체들: `LOPGame`, `CameraController`, `Main Camera`, `Canvas`, `EventSystem`, `Directional Light`.

- [ ] **Step 2: LOPGame 씬을 additive로 함께 로드**

`manage_scene(action=load, path="Assets/Scenes/LOPGame.unity", additive=true)`. 두 씬이 동시에 열린 상태에서 이동한다. LOPGame 생성 시 만들어진 기본 `Main Camera`/`Directional Light`는 삭제(`manage_gameobject delete`) — Room에서 옮겨올 것과 중복.

- [ ] **Step 3: 게임 객체를 LOPGame 씬으로 이동**

각 루트 객체에 대해 `manage_scene(action=move_to_scene, target=<instanceID>, scene_name="LOPGame")` (또는 해당 MCP 시그니처). 대상: `LOPGame`, `CameraController`, `Main Camera`, `Canvas`, `EventSystem`, `Directional Light`. (`LOPGameEngine`은 `LOPGame`의 자식이라 함께 따라온다.)

- [ ] **Step 4: `GameLifetimeScope` GameObject 생성 (LOPGame 씬)**

`manage_gameobject(action=create, name="GameLifetimeScope", components_to_add=["GameLifetimeScope"])` — **LOPGame 씬에 생성되도록** active 씬을 LOPGame로 두거나 생성 후 `move_to_scene`. 그 후 `manage_components(set_property)`로 `game`/`gameEngine`/`cameraController`를 LOPGame 씬의 해당 **컴포넌트 instanceID**로 연결 (LOPGame 컴포넌트, LOPGameEngine 컴포넌트, CameraController 컴포넌트).

- [ ] **Step 5: 두 씬 저장**

`manage_scene(action=save)` (각 씬). Room 씬에는 게임 객체가 사라지고, LOPGame 씬에 게임 객체 + GameLifetimeScope가 있어야 한다.

- [ ] **Step 6: 컴파일/로드 확인**

`read_console(error)` — 이 시점엔 RoomLifetimeScope가 아직 사라진 게임 객체(game/gameEngine/cameraController)를 `[SerializeField]`로 참조해 **missing**일 수 있다(런타임 전이라 컴파일 에러는 아님). Task 4에서 정리한다. 컴파일 에러 0 확인.

- [ ] **Step 7: 커밋**

```bash
git add Assets/Scenes/Room.unity Assets/Scenes/LOPGame.unity
git commit -m "refactor(scene): move game objects from Room to LOPGame scene"
```

---

## Task 4: `RoomLifetimeScope` 정리 — 연결/세션만 남김

게임 등록(World Core·게임 서비스·게임 MonoBehaviour)을 모두 제거한다. 이미 `GameLifetimeScope`가 등록하므로 중복 제거이자, Room 씬에서 사라진 게임 객체 참조를 끊는 작업이다.

**Files:**
- Modify: `Assets/Scripts/Room/RoomLifetimeScope.cs`

- [ ] **Step 1: `RoomLifetimeScope` 교체**

전체를 다음으로:

```csharp
using GameFramework;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    public class RoomLifetimeScope : SceneLifetimeScope
    {
        [SerializeField] private LOPRoom room;
        [SerializeField] private LOPNetworkManager networkManager;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            builder.RegisterComponent(room);
            builder.RegisterComponent(networkManager);

            builder.Register<IRoomMessageHandler, GameMessageHandler>(Lifetime.Transient);

            builder.Register<ISessionManager, SessionManager>(Lifetime.Singleton);
            builder.Register<IPlayerContext, PlayerContext>(Lifetime.Singleton);
            builder.Register<GameDataStore>(Lifetime.Singleton).As<IGameDataStore, IDataStore>();

            #region RegisterBuildCallback
            builder.RegisterBuildCallback(container =>
            {
            });
            #endregion
        }
    }
}
```

> 제거된 것: World Core 5개, `RegisterComponent(game/gameEngine/cameraController)`, `IGameMessageHandler` 3종, `PlayerInputManager`, `IActionManager`/`IMovementManager`, `IEntityCreator` 2종, `IEntityFactory`, 그리고 `game`/`gameEngine`/`cameraController` `[SerializeField]`. → 전부 `GameLifetimeScope`로 이동했다.
> 유지: `IPlayerContext`/`GameDataStore`는 Room 잔류(자식 Game이 resolve). `StatsPopup`이 `SceneLifetimeScope.Inject`(Room 컨테이너)로 `IPlayerContext`를 받던 경로는 Task 6에서 재검토.

- [ ] **Step 2: 컴파일 확인**

`refresh_unity` + `read_console(error)`. Expected: error 0건.

- [ ] **Step 3: Room 씬에서 끊긴 직렬화 슬롯 정리**

Room 씬 `RoomLifetimeScope` 컴포넌트에서 제거된 필드(`game`/`gameEngine`/`cameraController`) 슬롯이 사라졌는지 확인. `room`/`networkManager`는 여전히 연결돼 있어야 함(`manage_components`로 확인). 어긋나면 재연결 후 `manage_scene save`.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/Room/RoomLifetimeScope.cs Assets/Scenes/Room.unity
git commit -m "refactor(di): strip game registrations from RoomLifetimeScope (moved to GameLifetimeScope)"
```

---

## Task 5: `LOPRoom` — LOPGame 씬 로드로 게임 수명 제어

`LOPRoom`이 `[Inject] IGame`(이제 Room에 없음) 대신, LOPGame 씬을 Room 자식으로 로드하고 거기서 `IGame`을 resolve한다.

**Files:**
- Modify: `Assets/Scripts/Room/LOPRoom.cs`

- [ ] **Step 1: 필드 교체**

```csharp
        // game은 LOPGame 씬(자식 스코프)에서 resolve한다. 부모(Room)는 [Inject]로 받지 못한다.
        private IGame game;
        [Inject] private LOPNetworkManager networkManager;
        [Inject] private IRoomDataStore roomDataStore;
        [Inject] private IGameDataStore gameDataStore;
        [Inject] private IUserDataStore userDataStore;
        [Inject] private IEnumerable<IRoomMessageHandler> roomMessageHandlers;

        private const string LOPGameScene = "LOPGame";
        private GameLifetimeScope gameScope;
```

> `IRoom.game`이 인터페이스 멤버라면 `public IGame game => game;`로 노출해야 한다. 구현 시 `IRoom` 정의 확인 후, get-only 프로퍼티가 필요하면 백킹 필드명을 `_game`으로 바꾸고 `public IGame game => _game;`로 제공.

- [ ] **Step 2: `InitializeAsync`에서 LOPGame 로드 + resolve**

`roomMessageHandler.Register()` 루프 다음, `game.onGameStateChanged` 이전을 교체:

```csharp
            var roomScope = LifetimeScope.Find<RoomLifetimeScope>();
            var gameInfo = gameDataStore.gameInfo;   // 인자 (null일 수 있으면 가드)

            using (LifetimeScope.EnqueueParent(roomScope))
            using (LifetimeScope.Enqueue(b => b.RegisterInstance(gameInfo)))
            {
                await SceneManager.LoadSceneAsync(LOPGameScene, LoadSceneMode.Additive);
            }

            gameScope = LifetimeScope.Find<GameLifetimeScope>();
            game = gameScope.Container.Resolve<IGame>();

            game.onGameStateChanged += OnGameStateChanged;
            await game.InitializeAsync();
```

> ⚠️ **타이밍 주의:** 현재 `JoinRoomServerAsync`가 `gameDataStore.gameInfo`를 채운다(`InitializeAsync` *이후* 호출). 즉 `InitializeAsync` 시점엔 `gameInfo`가 아직 null이다. 구현 시 둘 중 하나로 정리:
> - (A) LOPGame 로드를 `JoinRoomServerAsync` 이후(`StartGameAsync` 직전)로 옮긴다. ← 권장
> - (B) `gameInfo` 없이 로드하고, `Run` 시점에 주입한다.
> 이 plan은 (A)를 채택한다 — 아래 Step 3 참조. Step 2의 로드 블록은 Step 3 위치로 이동.

- [ ] **Step 3: 로드 위치 조정 — `StartGameAsync`로 이동**

`Awake`의 호출 순서를 유지하되, LOPGame 로드+resolve+InitializeAsync를 `StartGameAsync`에서 수행하도록 재배치한다. 구현 시 `InitializeAsync`는 룸 핸들러 등록까지만, `StartGameAsync`에서 `gameInfo` 확정 후 LOPGame 로드 → resolve → `game.InitializeAsync()` → `game.Run(...)` 순으로. (정확한 본문은 구현 시 `LOPRoom` 전체를 보고 재구성 — Open Question.)

- [ ] **Step 4: `DeinitializeAsync` — 언로드**

```csharp
            if (game != null)
            {
                await game.DeinitializeAsync();
                game.onGameStateChanged -= OnGameStateChanged;
                game = null;
            }
            if (SceneManager.GetSceneByName(LOPGameScene).isLoaded)
            {
                await SceneManager.UnloadSceneAsync(LOPGameScene);  // GameLifetimeScope 자동 dispose
            }
```

(기존 `roomMessageHandler.Unregister()` / `Clear()` 흐름은 유지.)

- [ ] **Step 5: 컴파일 확인**

`refresh_unity` + `read_console(error)`. Expected: error 0건. (`using VContainer;` 필요 — 이미 있음.)

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/Room/LOPRoom.cs
git commit -m "feat(room): load LOPGame scene as child scope, resolve IGame from it"
```

---

## Task 6: 정적 경로 정합 + 런타임 검증

**Files:**
- 확인: `Assets/Scripts/Popup/StatsPopup.cs`, `Assets/Scripts/Extensions/DI.Extensions.cs`

- [ ] **Step 1: `SceneLifetimeScope.instance` 정적 경로 점검**

`StatsPopup`은 LOPGame 씬으로 옮겨갔고 `[Inject] IPlayerContext`를 받는다. `StatsPopup.Awake`의 `SceneLifetimeScope.Inject(this)`는 **Room 컨테이너**(`SceneLifetimeScope.instance`)로 주입하는데, `IPlayerContext`는 Room에 있으므로 동작은 한다. 단 LOPGame 씬 객체는 `GameLifetimeScope`가 `RegisterBuildCallback`에서 이미 주입하므로(`[DIMonoBehaviour]`면), `StatsPopup`의 자체 `SceneLifetimeScope.Inject` 호출이 중복/불필요해질 수 있다. 구현 시: `StatsPopup`이 `[DIMonoBehaviour]`인지 확인하고, LOPGame 컨테이너 주입으로 일원화할지 결정. (둘 다 `IPlayerContext`를 같은 인스턴스로 주입하므로 기능상 안전 — 정리는 선택.)

- [ ] **Step 2: 컴파일 최종 확인**

`refresh_unity(all)` + `read_console(error)`. Expected: 0건.

- [ ] **Step 3: 런타임 검증 (플레이)**

Entrance → 매칭 → 룸 입장. 콘솔/화면 확인:
1. LOPGame 씬이 additive 로드되고 `GameLifetimeScope`가 Room 자식으로 빌드됨 (DI 에러 0)
2. 캐릭터 스폰/이동, 카메라(CameraController), 게임패드 입력 정상
3. 데미지/사망 (World Core), StatsPopup 스탯 표시 정상
4. 게임오버 → Lobby 전환 시 LOPGame 씬 언로드 + `GameLifetimeScope` dispose (재입장 시 누수 0)

- [ ] **Step 4: 검증 통과 후 main 머지**

```bash
git checkout main
git merge --no-ff feature/game-scene-scope -m "Merge feature/game-scene-scope"
git branch -d feature/game-scene-scope
# push는 사용자 확인 후
```

---

## Open Questions (구현 중 해소)

- `LifetimeScope.Find<GameLifetimeScope>()`가 다중 씬에서 안전한지 vs 로드된 LOPGame 씬 루트에서 `GetComponentInChildren<GameLifetimeScope>()`로 확정.
- `IRoom.game` 인터페이스 멤버 여부 → `LOPRoom`의 `game` 노출 형태.
- `gameInfo` 주입 타이밍 (Task 5 Step 3 — A안 채택).
- `StatsPopup` 등 정적 주입 경로 일원화 여부 (Task 6 Step 1).
- LOPGame 씬 이동 시 `EventSystem`/`Canvas`가 Room 씬에도 필요한지(전환 후 Room 단독 화면). 현재는 LOPGame로 일괄 이동.

## Self-Review 메모

- spec의 모든 섹션(씬 구조, DI 흐름, 씬 로딩 SceneManager, GameLifetimeScope 구성, Room↔Game 관계, 영향 파일)이 Task 1~6에 매핑됨.
- placeholder 없음 — 단 Task 5 Step 3은 `LOPRoom` 전체 재구성이 필요해 "구현 시 본문 확정"으로 남김(Open Question). 이는 기존 코드 의존이라 의도적.
