# Game을 GamePlay 씬으로 분리 — Room 자식 스코프 동적 로딩

**Date:** 2026-06-06
**Branch (제안):** `feature/game-scene-scope`
**Related:** [World Core 연결 아키텍처](../../world-core-connection-architecture.md) · [LOP 저장소 토폴로지](../../lop-repo-topology.md) · [아키텍처 가이드라인](../../architecture-guidelines.md)

## Goal

게임 시스템(플레이 전체 — 게임 로직·엔진·매니저·World Core + 게임 UI + 카메라 + 입력)을 **별도 `GamePlay` 씬**으로 빼고, `LOPRoom`이 인자(`gameInfo`)와 함께 그 씬을 **Room 스코프의 자식 컨테이너**로 동적 로딩한다. 게임 종료 시 씬 언로드로 스코프와 게임 객체가 통째로 정리된다.

핵심: **컨테이너 경계 = 씬 경계**. 게임 `[DI*]` 객체가 한 씬 안에 응집되어 DI 경계가 자명해진다.

## 배경 / 동기

현재 구조에서 게임 시스템은 `Room` 씬에 상주하고, 게임 관련 `[DIMonoBehaviour]`/`[DIGameObject]` 객체가 씬 전반에 흩어져 있다:

- `LOPGame` 서브트리: `LOPGame`, `LOPGamePresenter`, `LOPGameEngine`, `LOPEntityManager`, `LOPTickUpdater`
- 별도 루트: `CameraController`
- `Canvas` 하위: `GamePad`, `CameraTouchController`, `StatsPopup` 등 게임 UI

World Core(`EntityRegistry`/`WorldEventBuffer`/`HealthSystem`/`WorldEventApplicator`/`WorldEventBridge`)는 직전 정리에서 `SceneLifetimeScope` → `RoomLifetimeScope`로 이동했다(커밋 `2c3c0d3`). 하지만 게임 객체가 Room 씬에 섞여 있는 본질은 그대로다.

### 이전 시도와 그 한계 (폐기됨)

`GameLifetimeScope`를 **Room 씬에 정적 GameObject로 배치**하고, 흩어진 게임 `[DI*]`를 씬 스캔으로 긁어 자식 컨테이너에 주입하는 방식을 시도했다가 폐기했다. 한계:

- 게임 `[DI*]`가 Room 씬 전반에 흩어져 있어 **"어느 객체가 어느 컨테이너로 가야 하는가"를 자동으로 가를 깔끔한 메커니즘이 없었다** (서브트리 기준도, 단일 씬 스캔도 부적합).
- `RoomLifetimeScope`의 씬 스캔을 끄고 `GameLifetimeScope`가 전담 주입하는 식의 우회가 필요해 복잡도가 높았다.

→ **게임을 별도 씬으로 물리 분리하면 이 문제가 통째로 사라진다.** 게임 객체가 `GamePlay` 씬 안에만 있으니, 그 씬의 `LifetimeScope`가 자기 씬만 책임지면 된다 (컨테이너 경계 = 씬 경계).

## 설계 결정 (브레인스토밍 합의)

| 축 | 결정 | 비고 |
|---|---|---|
| 분리 단위 | **GamePlay 전용 씬** (프리팹 아님) | 범위가 플레이 전체(카메라/Canvas/EventSystem 포함)라 "씬 레벨 콘텐츠" 단위인 씬이 자연스러움 |
| 범위 | 플레이 전체 — 로직+엔진+매니저+World Core + 게임 UI + 카메라 + 입력 | "시스템만" 분리가 아니라 게임 플레이 화면 전체 |
| 맵/환경 | **별도** — `LOPGame`이 additive 로드 (현행 유지) | 게임 시스템과 맵은 이미 분리되어 있음 |
| DI 부모 지정 | `LifetimeScope.EnqueueParent(roomScope)` | 로드되는 씬의 `LifetimeScope`를 Room 자식으로 |
| 런타임 인자 주입 | `LifetimeScope.Enqueue(b => b.RegisterInstance(gameInfo))` | `gameInfo` 등을 자식 스코프에 주입 |
| 씬 로딩 메커니즘 | **SceneManager / Addressables 중 선택 가능** (DI와 직교) | 아래 "씬 로딩 메커니즘" 참고 |
| 컨테이너 경계 | 씬 경계 = 컨테이너 경계 | 게임 `[DI*]` 응집 |

## 씬 / 스코프 구조

```
Root 스코프 (앱 전역)
  └─ Room 스코프 (RoomLifetimeScope)         ← Room 씬: 연결/세션만
       │   RoomLifetimeScope, LOPRoom, LOPNetworkManager
       │   ISessionManager, IPlayerContext, GameDataStore, IRoomMessageHandler
       │
       └─ Game 스코프 (GameLifetimeScope)      ← GamePlay 씬: additive, Room의 자식
             LOPGame, LOPGameEngine(+EntityManager/TickUpdater), LOPGamePresenter
             게임 UI(Canvas: GamePad/StatsPopup/…), CameraController, 입력
             World Core(EntityRegistry/WorldEventBuffer/HealthSystem/Applicator/Bridge)
             게임 서비스(IGameMessageHandler들, PlayerInputManager,
                        IActionManager/IMovementManager, IEntityCreator들, IEntityFactory)
             + RegisterInstance(gameInfo)   ← Enqueue로 주입된 런타임 인자

  Map 씬 (환경/맵/조명)                         ← additive, LOPGame이 로드 (DI 무관)
```

- **부모(Room)는 자식(Game)을 못 보고, 자식(Game)은 부모(Room)를 본다** (VContainer 단방향 가시성). 그래서 게임 객체는 Room 서비스(`IPlayerContext` 등)를 resolve 가능하고, `LOPRoom`은 게임 객체를 `[Inject]`로 받지 못한다(아래 "Room ↔ Game 관계" 참고).
- 게임 `[DI*]`가 전부 `GamePlay` 씬 안에 있으므로, `GameLifetimeScope`가 자기 씬만 주입하면 된다.

## DI 흐름

VContainer의 `EnqueueParent`/`Enqueue`는 씬 로딩 방식과 **무관**하다 — "다음에 로드되는 씬의 `LifetimeScope`를 이 부모 밑에 이 추가 등록과 함께 빌드하라"일 뿐이다.

```csharp
// LOPRoom — 게임 시작
using (LifetimeScope.EnqueueParent(roomScope))                   // 부모 = Room 스코프
using (LifetimeScope.Enqueue(b => b.RegisterInstance(gameInfo))) // 자식에 인자 주입
{
    // 방법 A: await SceneManager.LoadSceneAsync("GamePlay", LoadSceneMode.Additive);
    // 방법 B: await Addressables.LoadSceneAsync(gamePlayKey, LoadSceneMode.Additive);
}
// → GamePlay 씬의 GameLifetimeScope가 Room 자식으로 자동 빌드 + gameInfo 주입 완료

var gameScope = LifetimeScope.Find<GameLifetimeScope>();          // 스코프 핸들 획득
var game = gameScope.Container.Resolve<IGame>();
game.onGameStateChanged += OnGameStateChanged;
await game.InitializeAsync();
// ... StartGame 시 game.Run(...)

// 게임 종료 → GamePlay 씬 언로드 → GameLifetimeScope 자동 dispose (게임 객체 통째 정리)
```

- `using` 블록이 `await Load...` 동안 유지되므로, 로드 완료 시점에 호출되는 `GameLifetimeScope.Awake`(자동 빌드)가 `EnqueueParent`/`Enqueue` 범위 안에서 실행된다 → Room 부모 + `gameInfo` 주입이 보장된다.
- `autoRun = true` 기본값으로 둔다. 씬 로드 시 VContainer가 부모(Room)→자식(Game) 순서를 자동 처리하므로 수동 `Build()` 호출이 필요 없다.

## 왜 씬인가 (vs 프리팹)

범위가 **플레이 전체**(카메라·Canvas·EventSystem·입력 포함)라 무게중심이 씬으로 넘어간다.

| 기준 | 씬 방식 (채택) | 프리팹 방식 |
|---|---|---|
| 카메라/Canvas/EventSystem | ✅ 씬이 담는 자연스러운 단위 | ⚠️ 넣을 순 있으나 어색·비대 |
| 에디터 편집/미리보기 | ✅ 게임 화면을 씬으로 봄 | ⚠️ 프리팹 모드 |
| 여러 게임 모드 확장 | ✅ 씬 여러 개 | ✅ 프리팹 여러 개 |
| 스코프 핸들 획득 | ⚠️ 로드 후 씬에서 찾아야 (`LifetimeScope.Find`) | ✅ `CreateChildFromPrefab`이 인스턴스 즉시 반환 |
| 정리 | ✅ 씬 언로드 = 자동 dispose | ✅ `gameScope.Dispose()` |

> **"로직만" 분리였다면 프리팹**(가볍고 `CreateChildFromPrefab` 한 줄로 핸들 획득)이 유리했을 것이다. 범위가 플레이 전체로 커지면서 씬으로 결정. 향후 "로직만 빼는 경량 모듈"이 필요해지면 프리팹 + `CreateChildFromPrefab(prefab, b => b.RegisterInstance(...))`을 대안으로 둔다.

## 씬 로딩 메커니즘 (SceneManager / Addressables — 선택 가능)

DI 구조와 **직교적**이다. `EnqueueParent`/`Enqueue` 코드는 두 방식에서 동일하고, 로드/언로드 짝만 맞추면 된다.

| | SceneManager | Addressables |
|---|---|---|
| 씬 등록 | Build Settings | Addressable 그룹 |
| 로드 | `SceneManager.LoadSceneAsync(name, Additive)` | `Addressables.LoadSceneAsync(key, Additive)` |
| 언로드 | `SceneManager.UnloadSceneAsync(scene)` | `Addressables.UnloadSceneAsync(handle)` |
| 적합 | 씬이 빌드에 항상 포함, 단순 | 번들 분리, 동적 다운로드, 원격 콘텐츠, 명시적 메모리 release |

- **둘 다 옵션으로 열어둔다.** 단순함이 우선이면 `SceneManager`, 번들/원격 콘텐츠 일관성이 우선이면 `Addressables`.
- 참고: 현재 `LOPGame`은 맵 씬(`FlapWangMap`)을 `Addressables.LoadSceneAsync`로 로드 중이다. GamePlay 씬도 Addressables에 태우면 로딩 파이프라인이 통일된다는 부수 이점이 있으나, 강제는 아니다.
- 구현 시 로드/언로드 핸들(SceneManager `Scene` 또는 Addressables `SceneInstance` 핸들)을 `LOPRoom`(또는 게임 수명 관리 주체)이 보관해 언로드에 사용한다.

## GameLifetimeScope 구성

`GamePlay` 씬 루트의 `GameLifetimeScope : LifetimeScope`:

- **등록 (`Configure`)**:
  - World Core: `EntityRegistry`, `WorldEventBuffer`, `HealthSystem`, `WorldEventApplicator`, `WorldEventBridge`
  - 게임 MonoBehaviour 인스턴스: `RegisterComponent(game).As<IGame>()`, `RegisterComponent(gameEngine).As<IGameEngine>()`, `RegisterComponent(cameraController)` 등 — **씬 내부 `[SerializeField]` 참조라 안정적**(흩어짐 없음)
  - 게임 서비스: `IGameMessageHandler` 3종, `PlayerInputManager`, `IActionManager`/`IMovementManager`, `IEntityCreator` 2종, `IEntityFactory`
  - `gameInfo`는 `Enqueue`로 외부에서 주입됨 (Configure에 직접 등록하지 않음)
- **자기 씬 주입**: 게임 `[DI*]` 객체가 모두 이 씬 안에 있으므로, 빌드 완료 후 자기 씬의 `[DIMonoBehaviour]`/`[DIGameObject]`를 자기 컨테이너로 주입한다. (직전 정리로 `SceneLifetimeScope`가 베이스에서 담당하던 주입 로직을 재사용하거나, GamePlay 씬 전용으로 구현)

> Room 씬에서 게임 객체들을 GamePlay 씬으로 옮기는 작업이 동반된다 — 이때 `[SerializeField]` 참조(게임 매니저↔컴포넌트, UI 바인딩 등)가 **모두 GamePlay 씬 내부 참조로 재배선**되어야 한다. cross-scene 직렬화 참조는 Unity가 지원하지 않으므로, Room↔Game 경계를 넘는 참조는 DI(resolve) 또는 런타임 연결로 처리한다.

## Room ↔ Game 관계

- **`LOPRoom`(Room 스코프)이 게임 수명을 제어**한다: GamePlay 씬 로드 → `game` resolve → `onGameStateChanged` 구독 → `InitializeAsync`/`Run`, 종료 시 씬 언로드.
- `LOPRoom`은 부모라 `[Inject] IGame`을 받지 못한다. `gameScope.Container.Resolve<IGame>()` 또는 `LifetimeScope.Find<GameLifetimeScope>()` 후 resolve로 핸들을 얻는다.
- `game`/`gameEngine` 등은 `IGameEngine`·`IGameMessageHandler` 같은 **게임 서비스에 의존**하므로 반드시 Game 컨테이너(자식)에서 주입돼야 한다. 부모(Room)에 두면 자식 등록을 못 봐 resolve가 실패한다 (이전 시도에서 확인된 제약).

## 영향 받는 파일 (구현 시, 개략)

> 실제 단계·세부는 구현 plan에서 확정. 여기서는 범위만.

- **신규**: `GamePlay.unity` 씬, `GameLifetimeScope.cs`
- **씬 재구성**: Room 씬의 게임 객체(LOPGame/엔진/매니저/게임 UI/카메라/입력) → GamePlay 씬으로 이동 + 직렬화 참조 재배선
- **수정**: `LOPRoom.cs`(게임 씬 로드/언로드 + scope resolve로 게임 수명 제어), `RoomLifetimeScope.cs`(World Core·게임 서비스 등록을 GameLifetimeScope로 이관, Room은 연결/세션만)
- **확인**: `StatsPopup` 등 `SceneLifetimeScope.Inject` 정적 경로 사용자 — GamePlay 씬으로 옮겨가면 Game 컨테이너로 주입되도록 정합

## Out of Scope

- **실제 구현** — 별도 plan에서 단계화 (`writing-plans`).
- **여러 게임 모드/맵 동적 선택** — 현재 단일 게임. `gameInfo`에 식별자를 담아 씬/Addressable 키를 고르는 확장은 구조상 자연스럽지만 이번 범위 밖.
- **라이프사이클 독립 고도화**(리매치 시 Room 연결 유지한 채 Game만 재생성) — 씬 언로드/재로드로 자연 지원되지만, 예측/롤백(Stage ④)과의 연동은 별도.
- **씬 로딩 메커니즘 최종 확정** — 둘 다 옵션으로 열어둠. 구현 시 결정.

## Open Questions (구현 plan에서 해소)

- `roomScope` 참조 획득: `LifetimeScope.Find<RoomLifetimeScope>()` vs `LOPRoom`에 주입/직렬화.
- `GameLifetimeScope` 핸들 획득의 정확한 방법: `LifetimeScope.Find<GameLifetimeScope>()`가 다중 씬 환경에서 안전한지, 아니면 로드된 씬의 루트에서 `GetComponentInChildren`으로 확정할지.
- 자기 씬 주입 로직을 `SceneLifetimeScope`에서 공유할지(확장 메서드 추출), GamePlay 전용으로 둘지.
- 게임 UI(Canvas)와 입력의 GamePlay 씬 이동 시 EventSystem/Canvas 중복(Room 씬에도 있을 수 있음) 정리.
- 씬 로딩 메커니즘(SceneManager vs Addressables) 최종 선택.

## 진행

- [x] 브레인스토밍 합의 (씬 분리, 플레이 전체, 맵 별도, EnqueueParent/Enqueue, 로딩 직교)
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan 작성 (사용자가 구현 착수할 때)

이 spec은 **설계 박제**가 목적이다. 즉시 구현하지 않으며, 추후 작업 착수 시 이 문서를 기준으로 plan을 작성한다.
