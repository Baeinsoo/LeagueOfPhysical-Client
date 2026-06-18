# Server World Motion Parity (EntityBinder + Transform/Velocity Mirror) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 클라 World Core 모션 이행(Slice B)을 서버에 패리티로 이식한다 — `EntityCreated` 수명 신호 + `EntityBinder` 구조 위에 `World.Transform`/`World.Velocity` 단방향 미러(`WorldMotionSync`)를 얹는다. 순수 그림자(reader 없음, 동작 변화 0).

**Architecture:** 서버 `CharacterCreator`가 World.Entity에 Transform/Velocity 초기값을 등록하고, `LOPEntityManager.CreateEntity`가 `EntityCreated`를 발행하면 `EntityBinder`가 `WorldMotionSync`를 캐릭터에 부착한다. `WorldMotionSync`는 매 틱 `AfterPhysicsSimulation`(서버 `LOPEntityController.SyncPhysics()` readback 직후)에 `LOPEntity → World` 단방향 미러. 클라와 거의 동형이며 뷰 부착만 빠진다.

**Tech Stack:** Unity (서버, 단일 Assembly-CSharp), VContainer DI, GameFramework `World`(순수 C# 코어, file: 공유 패키지 — Transform/Velocity 이미 존재), `System.Numerics`, EventBus(GameFramework).

---

## Repo & Branch

전 작업이 **서버 repo 하나**에서 일어난다 (GameFramework는 *불변* — Transform/Velocity는 클라 Slice B에서 이미 추가됨).

| 항목 | 값 |
|---|---|
| 코드 repo | `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server` |
| 브랜치 | `feature/world-core-motion-server` (Task 1에서 생성) |
| spec/plan | 클라 repo (이미 커밋됨, 변경 없음) |

main 직접 커밋 금지. 모든 commit은 서버 repo 피처 브랜치에.

## UnityMCP 인스턴스 핀 — **서버** (모든 컴파일 검증에 필수)

이번 작업은 사용자가 명시 지시한 **서버** 작업이다(평소엔 클라만 — CLAUDE.md). UnityMCP 호출마다 `unity_instance`를 **서버**로 명시한다.

1. `mcpforunity://instances` 리소스를 읽어 `name == "LeagueOfPhysical-Server"`인 인스턴스의 전체 `id`(`Name@hash`)를 얻는다. (작성 시점: `LeagueOfPhysical-Server@f99391fa2dbaaf3c` — hash는 바뀔 수 있으니 매번 resolve.)
2. 모든 `refresh_unity`/`read_console` 호출에 `unity_instance="<그 서버 id>"`를 전달한다. **클라 인스턴스로 라우팅 금지.**

## File Structure (서버)

| 파일 | 변경 | 책임 |
|---|---|---|
| `Assets/Scripts/World/NumericsConversionExtensions.cs` | Create | `UnityEngine → System.Numerics` 경계 변환(`ToNumerics`) |
| `Assets/Scripts/EntityCreator/CharacterCreator.cs` | Modify | World.Entity에 Transform/Velocity 초기값 등록 |
| `Assets/Scripts/World/WorldMotionSync.cs` | Create | per-entity 단방향 모션 미러 어댑터 |
| `Assets/Scripts/Entity/Event.Entity.cs` | Modify | `EntityCreated` struct 추가 |
| `Assets/Scripts/Entity/LOPEntityManager.cs` | Modify | `CreateEntity`에서 `EntityCreated` 발행 |
| `Assets/Scripts/Game/EntityBinder.cs` | Create | `EntityCreated`에 반응해 `WorldMotionSync` 부착(뷰 없음) |
| `Assets/Scripts/Game/GameLifetimeScope.cs` | Modify | `EntityBinder`를 `IGameMessageHandler`로 등록 |

**Task 순서 근거 (각 중간 상태가 컴파일·안전):** 변환(1) → 데이터 등록(2) → 어댑터(3) → 수명 신호(4) → 바인더+활성화(5). 바인더가 활성화되는 Task 5 시점엔 Transform/Velocity가 이미 등록(Task 2)돼 미러가 즉시 동작. 각 중간 커밋은 컴파일되고 부작용 없음(미등록/미구독 시 no-op).

> **테스트 전략:** 신규 자동화 테스트 없음. 서버는 단일 Assembly-CSharp라 EditMode asmdef 참조 불가(전 슬라이스 기조 동일). 코어 `Transform`/`Velocity` 생성/기본값은 GameFramework EditMode에 이미 존재. 핵심 검증 = **서버 컴파일 + 수동 플레이**. MonoBehaviour 글루라 TDD 부적용.

---

## Task 1: NumericsConversionExtensions (서버 신규)

**Files:** Create `Assets/Scripts/World/NumericsConversionExtensions.cs`

- [ ] **Step 1: 피처 브랜치 생성 (서버 repo)**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git checkout -b feature/world-core-motion-server
git branch --show-current   # → feature/world-core-motion-server
```
(이미 있으면 `git checkout feature/world-core-motion-server`.)

- [ ] **Step 2: 파일 생성**

`Assets/Scripts/World/NumericsConversionExtensions.cs` (클라 동일물 포팅, 주석의 "서버"만 반영):

```csharp
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// UnityEngine → System.Numerics 변환. 코어(noEngineReferences) 경계 어댑터 전용 —
    /// 코어는 System.Numerics만, 서버(엔진)는 UnityEngine만 보고 이 변환이 둘을 잇는다.
    /// (World→Unity 역변환은 pull 소비가 생기는 후속 슬라이스에서 추가.)
    /// </summary>
    public static class NumericsConversionExtensions
    {
        public static System.Numerics.Vector3 ToNumerics(this Vector3 v)
            => new System.Numerics.Vector3(v.x, v.y, v.z);

        public static System.Numerics.Quaternion ToNumerics(this Quaternion q)
            => new System.Numerics.Quaternion(q.x, q.y, q.z, q.w);
    }
}
```

- [ ] **Step 3: 컴파일 확인** — UnityMCP(서버 핀):
  - `refresh_unity(unity_instance="<server id>")`
  - `read_console(unity_instance="<server id>", types=["error"])`
  Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git add Assets/Scripts/World/NumericsConversionExtensions.cs Assets/Scripts/World/NumericsConversionExtensions.cs.meta
git commit -m "feat(world): add UnityEngine->System.Numerics conversion extensions (server)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
(`.meta`는 Unity가 생성 — `git add` 전 reimport로 생성됐는지 확인. 없으면 `git add` 시 자동 포함 안 되니, refresh 후 `git status`로 `.cs.meta` 존재 확인 후 추가.)

---

## Task 2: CharacterCreator — World.Transform/Velocity 등록

**Files:** Modify `Assets/Scripts/EntityCreator/CharacterCreator.cs`

> 기존 World.Entity 등록 블록(현재 Health만)에 Transform/Velocity 초기값 추가. 클라 CharacterCreator와 동형.

- [ ] **Step 1: 등록 블록 교체**

현재 (World Core 블록):
```csharp
            // --- World Core (병렬·추가) — 마이그레이션 Slice 1: Walking Skeleton ---
            var worldEntity = new GameFramework.World.Entity(creationData.entityId);
            var worldHealth = new GameFramework.World.Health(creationData.maxHP) { Current = creationData.currentHP };
            worldEntity.Add(worldHealth);
            entityRegistry.Add(worldEntity);
            Debug.Log($"[World] Registered entity {worldEntity.Id} Health={worldHealth.Current}/{worldHealth.Max}");
            // --- end World Core slice 1 ---
```
를 다음으로 교체 (Transform/Velocity 추가):
```csharp
            // --- World Core (병렬·추가) — Slice 1: Health, 서버 Motion: Transform/Velocity ---
            var worldEntity = new GameFramework.World.Entity(creationData.entityId);
            var worldHealth = new GameFramework.World.Health(creationData.maxHP) { Current = creationData.currentHP };
            worldEntity.Add(worldHealth);
            worldEntity.Add(new GameFramework.World.Transform
            {
                Position = entity.position.ToNumerics(),
                Rotation = Quaternion.Euler(entity.rotation).ToNumerics(),
            });
            worldEntity.Add(new GameFramework.World.Velocity { Linear = entity.velocity.ToNumerics() });
            entityRegistry.Add(worldEntity);
            Debug.Log($"[World] Registered entity {worldEntity.Id} Health={worldHealth.Current}/{worldHealth.Max}");
            // --- end World Core ---
```

> `entity`는 이 메서드의 `LOPEntity`(서버 CharacterCreator 21줄). `Quaternion`은 파일 상단 `using UnityEngine;`로 가용. `ToNumerics`는 Task 1(같은 `namespace LOP`)로 가용. `entity.position/rotation/velocity`는 `entity.Initialize(creationData)` 후 세팅돼 있음(클라 동일 패턴).

- [ ] **Step 2: 컴파일 확인** — UnityMCP(서버 핀): `refresh_unity` → `read_console(types=["error"])`. Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git add Assets/Scripts/EntityCreator/CharacterCreator.cs
git commit -m "feat(world): register World.Transform/Velocity initial values in CharacterCreator (server)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: WorldMotionSync (서버 신규)

**Files:** Create `Assets/Scripts/World/WorldMotionSync.cs`

> 클라 동일물 포팅 (verbatim). 서버는 `IGameEngine.AddListener`/`GameEngineListen`/`AfterPhysicsSimulation`/`ICleanup`를 이미 `LOPEntityController`에서 사용 — 모든 의존 충족.

- [ ] **Step 1: 파일 생성**

`Assets/Scripts/World/WorldMotionSync.cs`:

```csharp
using GameFramework;
using LOP.Event.LOPGameEngine.Update;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 코어 밖(어댑터) 단방향 모션 미러. 매 틱 AfterPhysicsSimulation(= SyncPhysics 직후)에
    /// LOPEntity(진실원본)의 position/rotation/velocity를 변환해 순수 C#
    /// World.Transform/World.Velocity(미러)에 기록한다. 코어는 이 동기화를 모른다(Model A/DIP).
    /// </summary>
    public class WorldMotionSync : MonoBehaviour, ICleanup
    {
        [Inject]
        private IGameEngine gameEngine;

        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;

        private LOPEntity entity;
        private GameFramework.World.Transform worldTransform;
        private GameFramework.World.Velocity worldVelocity;

        public void SetEntity(LOPEntity entity)
        {
            this.entity = entity;
        }

        protected virtual void Start()
        {
            GameFramework.World.Entity worldEntity = entityRegistry.Get(entity.entityId);
            worldTransform = worldEntity?.Get<GameFramework.World.Transform>();
            worldVelocity = worldEntity?.Get<GameFramework.World.Velocity>();
            gameEngine.AddListener(this);
        }

        public void Cleanup()
        {
            gameEngine.RemoveListener(this);
            worldTransform = null;
            worldVelocity = null;
            entity = null;
        }

        [GameEngineListen(typeof(AfterPhysicsSimulation))]
        private void OnAfterPhysicsSimulation()
        {
            if (worldTransform != null)
            {
                worldTransform.Position = entity.position.ToNumerics();
                worldTransform.Rotation = Quaternion.Euler(entity.rotation).ToNumerics();
            }

            if (worldVelocity != null)
            {
                worldVelocity.Linear = entity.velocity.ToNumerics();
            }
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인** — UnityMCP(서버 핀): `refresh_unity` → `read_console(types=["error"])`. Expected: 0 errors. (아직 누구도 생성 안 함 — 정상.)

- [ ] **Step 3: Commit**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git add Assets/Scripts/World/WorldMotionSync.cs Assets/Scripts/World/WorldMotionSync.cs.meta
git commit -m "feat(world): add WorldMotionSync one-way LOPEntity->World mirror adapter (server)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: EntityCreated 수명 신호 (struct + 발행)

**Files:**
- Modify `Assets/Scripts/Entity/Event.Entity.cs`
- Modify `Assets/Scripts/Entity/LOPEntityManager.cs`

> 서버엔 `EntityCreated`가 없다(클라 전용). 추가 + `CreateEntity`에서 발행. 클라 LOPEntityManager와 동형.

- [ ] **Step 1: EntityCreated struct 추가**

`Assets/Scripts/Entity/Event.Entity.cs` — `namespace LOP.Event.Entity` 안, 기존 `PropertyChange` struct 바로 뒤에 추가 (파일은 `using UnityEngine;`만 있으므로 `IEntity`는 풀 네임스페이스로):

```csharp
    public struct EntityCreated
    {
        public GameFramework.IEntity entity;
        public EntityCreated(GameFramework.IEntity entity)
        {
            this.entity = entity;
        }
    }
```

- [ ] **Step 2: CreateEntity에서 발행**

`Assets/Scripts/Entity/LOPEntityManager.cs` 상단 using 블록에 추가:
```csharp
using LOP.Event.Entity;
```
그리고 `CreateEntity` 메서드에서 `entityMap[entity.entityId] = entity;` 바로 다음 줄에 발행 추가:
```csharp
            entityMap[entity.entityId] = entity;

            EventBus.Default.Publish(nameof(EntityCreated), new EntityCreated(entity));
```
(그 아래 기존 user-entity 매핑 `if (creationData is CharacterCreationData ...)` 블록은 그대로.)

- [ ] **Step 3: 컴파일 확인** — UnityMCP(서버 핀): `refresh_unity` → `read_console(types=["error"])`. Expected: 0 errors. (발행되지만 구독자 없음 — 무해.)

- [ ] **Step 4: Commit**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git add Assets/Scripts/Entity/Event.Entity.cs Assets/Scripts/Entity/LOPEntityManager.cs
git commit -m "feat(entity): publish EntityCreated lifecycle signal in LOPEntityManager (server)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 5: EntityBinder (서버 신규) + DI 등록 — 활성화

**Files:**
- Create `Assets/Scripts/Game/EntityBinder.cs`
- Modify `Assets/Scripts/Game/GameLifetimeScope.cs`

> 클라 EntityBinder를 포팅하되 **뷰 부착 제거** — `WorldMotionSync`만 부착. 이 Task가 미러를 활성화한다(앞 Task들로 Transform/Velocity 등록·발행·어댑터 준비 완료).

- [ ] **Step 1: EntityBinder 생성**

`Assets/Scripts/Game/EntityBinder.cs`:

```csharp
using GameFramework;
using LOP.Event.Entity;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 엔티티 수명 신호(<see cref="EntityCreated"/>)에 반응해 엔티티별 바깥 어댑터를 생성·연결한다
    /// (분리형 바인딩 시스템 — 설계 문서의 "뷰 스포너/바인딩 시스템" 역할). 서버는 뷰가 없어
    /// World 미러 어댑터(<see cref="WorldMotionSync"/>)만 부착한다. 크리에이터는 엔티티(모델/코어
    /// 데이터)만, 바깥 어댑터 바인딩은 이 바인더가 붙인다.
    ///
    /// 파괴는 엔티티 GameObject(root) 파괴 + ICleanup 경로가 처리한다(생성물이 같은 root 자식이라 함께 정리됨).
    /// </summary>
    public class EntityBinder : IGameMessageHandler
    {
        [Inject] private IObjectResolver objectResolver;

        public void Register()
        {
            EventBus.Default.Subscribe<EntityCreated>(nameof(EntityCreated), OnEntityCreated);
        }

        public void Unregister()
        {
            EventBus.Default.Unsubscribe<EntityCreated>(nameof(EntityCreated), OnEntityCreated);
        }

        private void OnEntityCreated(EntityCreated entityCreated)
        {
            if (entityCreated.entity is not LOPEntity entity)
            {
                return;
            }

            // 모션 미러는 캐릭터 엔티티에만 (아이템 등 제외).
            if (entity.GetEntityComponent<CharacterComponent>() == null)
            {
                return;
            }

            GameObject root = entity.transform.parent.gameObject;

            WorldMotionSync worldMotionSync = root.CreateChildWithComponent<WorldMotionSync>();
            objectResolver.Inject(worldMotionSync);
            worldMotionSync.SetEntity(entity);
        }
    }
}
```

- [ ] **Step 2: GameLifetimeScope에 등록**

`Assets/Scripts/Game/GameLifetimeScope.cs` — 기존 3개 `IGameMessageHandler` 등록 블록:
```csharp
            builder.Register<IGameMessageHandler, GameInfoMessageHandler>(Lifetime.Transient);
            builder.Register<IGameMessageHandler, GameEntityMessageHandler>(Lifetime.Transient);
            builder.Register<IGameMessageHandler, GameInputMessageHandler>(Lifetime.Transient);
```
뒤에 한 줄 추가:
```csharp
            builder.Register<IGameMessageHandler, GameInfoMessageHandler>(Lifetime.Transient);
            builder.Register<IGameMessageHandler, GameEntityMessageHandler>(Lifetime.Transient);
            builder.Register<IGameMessageHandler, GameInputMessageHandler>(Lifetime.Transient);
            builder.Register<IGameMessageHandler, EntityBinder>(Lifetime.Transient);
```

- [ ] **Step 3: 컴파일 확인** — UnityMCP(서버 핀): `refresh_unity` → `read_console(types=["error"])`. Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git add Assets/Scripts/Game/EntityBinder.cs Assets/Scripts/Game/EntityBinder.cs.meta Assets/Scripts/Game/GameLifetimeScope.cs
git commit -m "feat(world): EntityBinder attaches WorldMotionSync on EntityCreated (server)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 6: 런타임 검증 (수동)

> 자동화 테스트 없음(단일 Assembly-CSharp). 서버 플레이로 미러 동작 + 회귀 0 확인.

- [ ] **Step 1: 임시 디버그 로그 추가** — `WorldMotionSync.OnAfterPhysicsSimulation`의 transform 미러 직후에 임시로:
```csharp
                // TEMP 검증 로그 (확인 후 제거)
                Debug.Log($"[MotionSync] {entity.entityId} world={worldTransform.Position} entity={entity.position.ToNumerics()}");
```
refresh + read_console로 0 errors 확인.

- [ ] **Step 2: 서버 플레이 + 관찰** — 서버 에디터 Play(룸 연결). 콘솔에서:
  - [ ] `[MotionSync] {id} world=... entity=...`에서 **world == entity** 이고, 캐릭터 이동 시 값이 함께 변함(추종).
  - [ ] `[World] Registered entity {id} Health=...` 스폰 로그 정상.
  - [ ] 데미지/사망 등 기존 흐름 정상(`[World] Death entity ...`), 콘솔 **에러 0**(DI 누락 NRE 없음 — EntityBinder/WorldMotionSync 주입 정상).
  - [ ] 스폰/디스폰 반복 시 `WorldMotionSync` 정리 정상(에러 0 — root 파괴 + ICleanup).

- [ ] **Step 3: 임시 로그 제거 + 최종 커밋** — Step 1의 TEMP 로그 줄 삭제, refresh로 0 errors 확인 후:
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git add Assets/Scripts/World/WorldMotionSync.cs
git commit -m "chore(world): remove temporary motion-sync verification log (server)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 4: 검증 실패 시** — superpowers:systematic-debugging. 예상 위험: (a) `WorldMotionSync.Start`에서 Transform/Velocity null → CharacterCreator 등록(Task 2) 확인 + 등록이 EntityCreated 발행보다 먼저인지(타이밍) 확인. (b) 리스너 미발화 → `gameEngine.AddListener` + `[GameEngineListen]` 경로 확인.

---

## Self-Review (작성자 체크 결과)

- **Spec coverage:** spec의 7개 변경 → Task 1(NumericsConversionExtensions), 2(CharacterCreator), 3(WorldMotionSync), 4(EntityCreated struct + LOPEntityManager publish), 5(EntityBinder + GameLifetimeScope) 전부 매핑. 검증·문서 정책도 반영. ✓
- **Placeholder scan:** TBD/TODO 없음. 모든 코드 step에 실제 코드 블록. 서버 `<server id>`는 구현 시 resolve 의도(플레이스홀더 아님). ✓
- **Type consistency:** `ToNumerics`(Task1) ↔ 사용(Task2,3). `EntityCreated(GameFramework.IEntity)`(Task4 struct) ↔ 발행(Task4) ↔ 구독(Task5). `WorldMotionSync.SetEntity`(Task3) ↔ 호출(Task5). `IGameMessageHandler EntityBinder`(Task5 정의) ↔ 등록(Task5). ✓
- **컴파일 순서:** 각 Task 종료가 컴파일·무해(미등록/미구독 no-op). 활성화는 Task 5. ✓
- **단일 repo:** 전부 서버 repo `feature/world-core-motion-server`. GameFramework 불변. ✓
