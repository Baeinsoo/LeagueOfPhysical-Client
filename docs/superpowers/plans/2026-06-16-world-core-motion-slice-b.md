# World Core Motion Slice B (기초) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 순수 C# `GameFramework.World.Transform`(Position, Rotation:Quaternion) + `GameFramework.World.Velocity`(Linear)를 `System.Numerics`로 신설하고, 클라 어댑터 `WorldMotionSync`가 매 틱 `LOPEntity`(진실원본) → World로 단방향 미러한다.

**Architecture:** Model A/DIP — 코어는 순수 데이터(엔진 비참조)만. 동기화(읽기+변환+쓰기)는 **코어 밖** 클라 어댑터(`WorldMotionSync`)가 전담. 읽는 소비자·netcode·이벤트→pull 없음. 미러는 reader 없이 향후 슬라이스를 위한 기반.

**Tech Stack:** C# (`System.Numerics`), Unity (Assembly-CSharp 단일), VContainer, GameFramework.World, UnityMCP(컴파일 검증).

**Spec:** `docs/superpowers/specs/2026-06-16-world-core-motion-slice-b-design.md`

**저장소 2곳:**
- **GameFramework** (`C:/Users/re5na/workspace/LOP/GameFramework`) — 코어 컴포넌트. 현재 `main` → **피처 브랜치 생성 필요**(`feature/world-core-motion-slice-b`).
- **LOP-Client** (`C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client`) — 어댑터/배선. 브랜치 `feature/world-core-motion-slice-b` (이미 체크아웃).

---

## File Structure

| 파일 | 저장소 | 책임 |
|---|---|---|
| `Runtime/Scripts/World/Components/Transform.cs` (신규) | GameFramework | 순수 C# 공간 포즈(Position, Rotation) |
| `Runtime/Scripts/World/Components/Velocity.cs` (신규) | GameFramework | 순수 C# 선속도(Linear) |
| `Assets/Scripts/World/NumericsConversionExtensions.cs` (신규) | Client | UnityEngine→System.Numerics 변환(경계 어댑터) |
| `Assets/Scripts/World/WorldMotionSync.cs` (신규) | Client | per-entity 단방향 미러 어댑터(AfterPhysicsSimulation) |
| `Assets/Scripts/EntityCreator/CharacterCreator.cs` (수정) | Client | World.Entity에 Transform/Velocity 초기값 추가(코어 데이터만) |
| `Assets/Scripts/Game/EntityViewSpawner.cs` → `EntityBinder.cs` (리네임+수정) | Client | `EntityViewSpawner`→`EntityBinder` 일반화; EntityCreated 반응해 `WorldMotionSync`도 생성 |
| `Assets/Scripts/Game/GameLifetimeScope.cs` (수정) | Client | DI 등록 타입명 `EntityViewSpawner`→`EntityBinder` |

> **신규 `.cs`는 Unity가 `.meta`를 생성**한다. 직접 만들지 말고, 파일 작성 후 Unity 새로고침으로 생성된 `.meta`를 `.cs`와 함께 커밋한다(CLAUDE.md). 본 plan에서는 컨트롤러가 UnityMCP 새로고침으로 `.meta`를 만들고 커밋에 포함한다.

---

## Verification Procedure (각 Task의 "검증" 단계)

> UnityMCP 툴 deferred — 먼저 로드: `ToolSearch("select:refresh_unity,read_console")`. 인스턴스는 `mcpforunity://instances`에서 해석(클라 `LeagueOfPhysical-Client@<hash>`, 서버 `LeagueOfPhysical-Server@<hash>` — hash 매번 조회). **모든 호출에 `unity_instance` 명시**(CLAUDE.md).

A. **`.meta` 생성 + 컴파일**: `refresh_unity(unity_instance="<id>", mode="force", scope="all", compile="request", wait_for_ready=true)` → readiness 후 `read_console(unity_instance="<id>", action="get", types=["error"], count="40", format="plain")`.
   - `scope="all"`을 쓰는 이유: 신규 `.cs`의 import(.meta 생성) + 컴파일을 함께 트리거.
B. **기대**: 이번에 만진 타입 관련 컴파일 에러 **0건**.
C. 코어(GameFramework)는 클·서가 `file:`로 공유 → **Task 1은 클라+서버 양쪽** 검증. Task 2(클라 전용)는 클라만.

---

## Task 1: 코어 컴포넌트 `Transform` + `Velocity` (GameFramework)

**Files:**
- Create: `C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/World/Components/Transform.cs`
- Create: `C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/World/Components/Velocity.cs`

- [ ] **Step 1: GameFramework 피처 브랜치 보장**

```bash
cd "C:/Users/re5na/workspace/LOP/GameFramework"
git rev-parse --abbrev-ref HEAD   # main이면 아래로 전환
git checkout -b feature/world-core-motion-slice-b
```
(이미 적절한 피처 브랜치면 생략. main 직접 커밋 금지 — CLAUDE.md.)

- [ ] **Step 2: `Transform.cs` 작성** (정확한 내용)

```csharp
using System.Numerics;

namespace GameFramework.World
{
    /// <summary>엔티티의 공간 포즈(위치+회전). 순수 데이터(Anemic) — 로직은 System에 둔다.</summary>
    public class Transform : Component
    {
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; } = Quaternion.Identity;
    }
}
```

- [ ] **Step 3: `Velocity.cs` 작성** (정확한 내용)

```csharp
using System.Numerics;

namespace GameFramework.World
{
    /// <summary>엔티티의 운동(선속도). 각속도는 현재 미사용 — 필요 시 Angular 추가. 순수 데이터.</summary>
    public class Velocity : Component
    {
        public Vector3 Linear { get; set; }
    }
}
```

- [ ] **Step 4: `.meta` 생성 + 양쪽 컴파일 검증** — Verification Procedure(A/B) 를 **클라·서버 두 인스턴스 모두** 수행. 기대: 양쪽 새 에러 0건. (`System.Numerics`는 Unity BCL 기본 포함 — 별도 asmdef 참조 불필요. 만약 "type or namespace 'Numerics' not found"면 STOP하고 보고.)

- [ ] **Step 5: Commit** (GameFramework 저장소, `.cs` + Unity가 생성한 `.meta` 함께)

```bash
cd "C:/Users/re5na/workspace/LOP/GameFramework"
git add Runtime/Scripts/World/Components/Transform.cs Runtime/Scripts/World/Components/Transform.cs.meta Runtime/Scripts/World/Components/Velocity.cs Runtime/Scripts/World/Components/Velocity.cs.meta
git commit -m "$(cat <<'EOF'
feat(world): add Transform and Velocity core components (System.Numerics)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: 클라 어댑터 `WorldMotionSync` + 변환 + Creator 배선 (Client)

**Files:**
- Create: `Assets/Scripts/World/NumericsConversionExtensions.cs`
- Create: `Assets/Scripts/World/WorldMotionSync.cs`
- Modify: `Assets/Scripts/EntityCreator/CharacterCreator.cs`
- Rename+Modify: `Assets/Scripts/Game/EntityViewSpawner.cs` → `Assets/Scripts/Game/EntityBinder.cs`
- Modify: `Assets/Scripts/Game/GameLifetimeScope.cs`

- [ ] **Step 1: `NumericsConversionExtensions.cs` 작성** (정확한 내용 — Slice B는 UnityEngine→System.Numerics만 사용)

```csharp
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// UnityEngine → System.Numerics 변환. 코어(noEngineReferences) 경계 어댑터 전용 —
    /// 코어는 System.Numerics만, 클라(엔진)는 UnityEngine만 보고 이 변환이 둘을 잇는다.
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

- [ ] **Step 2: `WorldMotionSync.cs` 작성** (정확한 내용)

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

- [ ] **Step 3: `CharacterCreator.cs` 의 World Core 블록 수정** (Transform/Velocity 초기값 — 코어 데이터만; WorldMotionSync 생성은 Step 4 EntityBinder가 담당)

Replace:
```csharp
            // --- World Core (병렬·추가) — 마이그레이션 Slice 1: Walking Skeleton ---
            var worldEntity = new GameFramework.World.Entity(creationData.entityId);
            var worldHealth = new GameFramework.World.Health(creationData.maxHP) { Current = creationData.currentHP };
            worldEntity.Add(worldHealth);
            entityRegistry.Add(worldEntity);
            Debug.Log($"[World] Registered entity {worldEntity.Id} Health={worldHealth.Current}/{worldHealth.Max}");
            // --- end World Core slice 1 ---
```
With:
```csharp
            // --- World Core (병렬·추가) — Slice 1: Health, Slice B: Transform/Velocity ---
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
            // --- end World Core slice 1+B ---
```

(`entity.position/rotation/velocity`는 `entity.Initialize(creationData)` 후 설정돼 있음. `Quaternion.Euler`/`.ToNumerics()`는 `using UnityEngine;`/LOP 네임스페이스라 추가 using 불필요.)

- [ ] **Step 4: `EntityViewSpawner` → `EntityBinder` 리네임 + `WorldMotionSync` 생성 추가**

`EntityViewSpawner`는 평범한 `IGameMessageHandler` 클래스(MonoBehaviour 아님)라 prefab/GUID 영향 없음 — 리네임 안전. 설계 문서의 "뷰 스포너/바인딩 시스템" 역할에 정합(개념 문서는 클래스명을 박지 않으므로 문서 변경 없음).

**4a. 파일 리네임 (`.cs`+`.meta`):**
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git mv Assets/Scripts/Game/EntityViewSpawner.cs Assets/Scripts/Game/EntityBinder.cs
git mv Assets/Scripts/Game/EntityViewSpawner.cs.meta Assets/Scripts/Game/EntityBinder.cs.meta
```

**4b. `EntityBinder.cs` — 클래스명 변경 + 주석 일반화.** Replace:
```csharp
    /// <summary>
    /// 엔티티 수명 신호를 구독해 장식 프레젠테이션(데미지 에미터 <see cref="DamageFloaterEmitter"/>, 머리 위 HP
    /// <see cref="CharacterNameplate"/>)를 엔티티별로 생성한다. 엔티티 생성(크리에이터)과 장식
    /// 프레젠테이션 생성을 분리 — 크리에이터는 엔티티(모델/엔진 강결합 표현)만, 장식 오버레이는
    /// 이 스포너가 수명 신호에 반응해 띄운다(분리형).
    ///
    /// 파괴는 엔티티 GameObject(root) 파괴 + ICleanup 경로가 처리한다(장식 뷰가 같은 root 자식이라
    /// 함께 정리됨) — 별도 추적 불필요.
    /// </summary>
    public class EntityViewSpawner : IGameMessageHandler
```
With:
```csharp
    /// <summary>
    /// 엔티티 수명 신호(<see cref="EntityCreated"/>)에 반응해 엔티티별 바깥 컴포넌트를 생성·연결한다
    /// (분리형 바인딩 시스템 — 설계 문서의 "뷰 스포너/바인딩 시스템" 역할). 생성 대상: 장식 뷰
    /// (<see cref="DamageFloaterEmitter"/>, <see cref="CharacterNameplate"/>) + World 미러 어댑터(<see cref="WorldMotionSync"/>).
    /// 크리에이터는 엔티티(모델/코어 데이터)만, 바깥 표현/바인딩은 이 바인더가 붙인다.
    ///
    /// 파괴는 엔티티 GameObject(root) 파괴 + ICleanup 경로가 처리한다(생성물이 같은 root 자식이라 함께 정리됨).
    /// </summary>
    public class EntityBinder : IGameMessageHandler
```

**4c. `EntityBinder.cs` — `OnEntityCreated`에 `WorldMotionSync` 생성 추가.** Replace:
```csharp
            CharacterNameplate nameplate = root.CreateChildWithComponent<CharacterNameplate>();
            objectResolver.Inject(nameplate);
            nameplate.SetEntity(entity);
        }
```
With:
```csharp
            CharacterNameplate nameplate = root.CreateChildWithComponent<CharacterNameplate>();
            objectResolver.Inject(nameplate);
            nameplate.SetEntity(entity);

            WorldMotionSync worldMotionSync = root.CreateChildWithComponent<WorldMotionSync>();
            objectResolver.Inject(worldMotionSync);
            worldMotionSync.SetEntity(entity);
        }
```

**4d. DI 등록 갱신 — `Assets/Scripts/Game/GameLifetimeScope.cs`.** Replace:
```csharp
            builder.Register<IGameMessageHandler, EntityViewSpawner>(Lifetime.Transient);
```
With:
```csharp
            builder.Register<IGameMessageHandler, EntityBinder>(Lifetime.Transient);
```

- [ ] **Step 5: `.meta` 생성 + 클라 컴파일 검증** — Verification Procedure(A/B) 를 **클라 인스턴스**에 수행. 기대: 새 에러 0건.

- [ ] **Step 6: Commit** (Client 저장소 — 신규 `.cs`+`.meta` 2쌍 + CharacterCreator + EntityBinder 리네임/수정 + GameLifetimeScope)

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git add Assets/Scripts/World/NumericsConversionExtensions.cs Assets/Scripts/World/NumericsConversionExtensions.cs.meta \
        Assets/Scripts/World/WorldMotionSync.cs Assets/Scripts/World/WorldMotionSync.cs.meta \
        Assets/Scripts/EntityCreator/CharacterCreator.cs \
        Assets/Scripts/Game/EntityBinder.cs \
        Assets/Scripts/Game/GameLifetimeScope.cs
git commit -m "$(cat <<'EOF'
feat(world): WorldMotionSync adapter + rename EntityViewSpawner to EntityBinder (Slice B)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```
(`git mv`가 EntityViewSpawner 삭제 + `EntityBinder.cs.meta`(GUID 유지) 스테이징을 이미 처리 — 위 `git add`는 내용 편집/신규분만.)

---

## Task 3: 런타임 검증 (수동 — 사용자)

자동화 테스트 없음(단일 Assembly-CSharp). 미러가 진실원본을 추종하는지 확인한다.

- [ ] **Step 1 (선택, 임시 로그):** 검증을 위해 `WorldMotionSync.OnAfterPhysicsSimulation` 끝에 임시 로그를 잠깐 넣어도 됨 — 예: `Debug.Log($"[Motion] {entity.entityId} world={worldTransform.Position} entity={entity.position}");`. 확인 후 **반드시 제거**(커밋하지 않음).
- [ ] **Step 2: 클라 Play 진입**, 캐릭터 이동.
- [ ] **Step 3: 확인**
  - 임시 로그 시 `world.Position`이 `entity.position`과 매 틱 일치(이동 중에도 추종).
  - 기존 동작 회귀 없음 — 이동/물리/네임플레이트/데미지 모두 정상(이번 변경은 *추가만*, 기존 경로 불변).
  - 스폰/디스폰 정상, 콘솔 에러 0(`WorldMotionSync`도 `ICleanup`로 정리됨).
- [ ] **Step 4:** 임시 로그를 넣었다면 제거 확인.

> 통과하면 Slice B(기초) 완료. 후속(별도 spec): position 진실원본 승격 + 이벤트→pull + 물리/뷰/입력/reconciler 전환(Stage ④) + `IPhysicsSimulator` DIP + (필요 시) angular velocity·World→Unity 역변환.

---

## 진행

- [ ] Task 1 — 코어 Transform/Velocity (GameFramework)
- [ ] Task 2 — 클라 어댑터 WorldMotionSync + 변환 + Creator 데이터 + EntityBinder 리네임/생성
- [ ] Task 3 — 런타임 검증(수동)
