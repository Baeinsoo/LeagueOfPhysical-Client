# 지속 상태 복제 + 애니메이션 파생 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 접지·어빌리티 시전·상태이상을 스냅샷으로 복제하고, 애니메이션을 그 상태에서 파생시켜 늦게 접속하거나 이벤트를 놓친 클라도 올바른 모션을 보게 한다.

**Architecture:** 서버가 `EntitySnap`에 세 값을 추가로 실어 보낸다. 클라는 **원격 엔티티에만** 그 값을 반영하고(내 캐릭은 로컬 예측이 진실), 뷰가 매 프레임 그 상태를 읽어 `Animator.Play(state, layer, 진행도)`로 그린다. 일회성 `SetTrigger` 경로를 걷어낸다.

**Tech Stack:** C# / Unity 6 / protobuf (protoc) / Luban 마스터데이터 / Mirror / VContainer / NUnit(EditMode)

**Spec:** `docs/superpowers/specs/2026-07-25-animation-state-sync-design.md`

## Global Constraints

- **저장소 6개**를 건드린다: `GameFramework` · `LeagueOfPhysical-Shared` · `LeagueOfPhysical-MasterData-Client` · `infrastructure` · `LeagueOfPhysical-Client` · `LeagueOfPhysical-Server`. **각 저장소에서 피처 브랜치를 만들어 작업하고, main 직접 커밋 금지.**
- **패키지(`GameFramework`/`LOP-Shared`/`MasterData-*`)는 `file:` 참조라 워크트리가 아닌 원본 폴더에서 편집한다.** 편집 즉시 클·서 Unity 에디터에 반영되므로 컴파일·EditMode 테스트가 바로 가능하다.
- **클·서 `Assets/` 코드는 워크트리에서 컴파일 검증이 불가능하다** — 연결된 Unity 에디터가 main 체크아웃을 보기 때문. 해당 태스크는 코드를 작성하고, 컴파일 확인은 **머지 후** main 에디터 리프레시로 한다.
- **LOP 측 파일에서 World 타입은 항상 풀 네임스페이스로 한정한다** (`GameFramework.World.Entity`). `using GameFramework.World;`를 추가하지 않는다 — `Component`가 `UnityEngine.Component`와 충돌한다. World 어셈블리 **내부** 코드는 짧은 이름을 써도 된다.
- **`.meta` 파일은 반드시 함께 커밋한다.** 새 `.cs`를 만든 뒤 Unity가 생성한 `.meta`를 같이 add.
- **proto 재생성은 `Scripts/generate_protos.sh`를 쓰되, 이 계획의 신규 메시지는 top-level 패킷이 아니다**(`@auto_generate` 주석 없음) → `MessageIds.cs`가 바뀌지 않아야 한다. 재생성 후 `git diff Runtime.Generated/Scripts/MessageIds.cs`가 **비어 있는지 반드시 확인**한다. 바뀌었으면 wire 계약이 깨진 것이므로 되돌린다.
- **UnityMCP 호출에는 항상 `unity_instance`를 명시**한다. 클라 인스턴스 id는 `mcpforunity://instances`에서 `name == "LeagueOfPhysical-Client"`인 항목의 `id`.
- **Luban 데이터 파일(`.xlsx`)은 `openpyxl`(설치돼 있음, 3.1.5)로 편집한다.** Excel 수작업 불필요. 편집 후 `infrastructure/table/gen.sh`를 실행해 `.cs`+`.bytes`를 재생성한다.
- **Luban Excel-embedded 형식** (모든 `#*.xlsx` 공통, 1행부터):

  | 행 | 내용 |
  |---|---|
  | 1 | `##var` + 컬럼명들 (리스트 컬럼은 `effects#sep=,` 처럼 구분자 지정) |
  | 2 | `##type` + 타입들 (`int`/`string`/`long`/`float`/`bool`/`list,AbilityEffect`) |
  | 3 | `##group` + 컬럼별 그룹 (`c`=클라 전용, `s`=서버 전용, 빈칸=양쪽) |
  | 4 | `##` + 표시용 이름들 |
  | 5~ | 데이터 (**첫 칸은 항상 빈 문자열**, 그 다음부터 값) |

  `__tables__.xlsx`는 헤더가 1행뿐이고(`##var full_name value_type read_schema_from_file input index mode group comment tags output`) 2행부터 데이터다. 테이블 단위 그룹은 여기 `group` 칸으로 준다(예: `TbSkinAsset`=`c`, `TbCombatConfig`=`s`).

  다형 effect 리스트는 `TypeName,인자,인자,...`를 이어 붙인 한 문자열이다. 실제 예:
  - haste(id 1): `StatusEffectApplyEffect,1`
  - dash(id 2): `MotionEffect,15`
  - attack(id 3): `DamageEffect,10,2,90,KnockbackEffect,5,12,0.8`
- 진행도(`normalizedTime`)는 `0.0`~`1.0` float. 페이즈는 `LOP.AbilityPhase` (`Ready`/`Startup`/`Active`/`Recovery`).

---

# 슬라이스 1 — 접지

## Task 1: `GroundState` 컴포넌트 (GameFramework)

**Files:**
- Create: `GameFramework/Runtime/Scripts/World/Components/GroundState.cs`
- Test: `GameFramework/Tests/EditMode/GroundStateTests.cs`

**Interfaces:**
- Consumes: `GameFramework.World.Component` (기존 추상 클래스), `GameFramework.World.Entity`
- Produces: `GameFramework.World.GroundState` — `bool IsGrounded { get; set; }`, 기본값 `false`

- [ ] **Step 1: 실패하는 테스트 작성**

`GameFramework/Tests/EditMode/GroundStateTests.cs`:

```csharp
using NUnit.Framework;
using GameFramework.World;

namespace GameFramework.Tests.EditMode
{
    public class GroundStateTests
    {
        [Test]
        public void DefaultsToNotGrounded()
        {
            var state = new GroundState();
            Assert.IsFalse(state.IsGrounded);
        }

        [Test]
        public void AttachesToEntityAndRoundTrips()
        {
            var entity = new Entity("e1");
            entity.Add(new GroundState { IsGrounded = true });

            Assert.IsTrue(entity.Get<GroundState>().IsGrounded);
            Assert.AreSame(entity, entity.Get<GroundState>().Owner);
        }
    }
}
```

- [ ] **Step 2: 테스트가 실패하는지 확인**

UnityMCP `run_tests` (클라 인스턴스, EditMode, filter `GroundStateTests`).
기대: 컴파일 실패 — `GroundState` 타입 없음.

- [ ] **Step 3: 최소 구현**

`GameFramework/Runtime/Scripts/World/Components/GroundState.cs`:

```csharp
namespace GameFramework.World
{
    /// <summary>
    /// 캐릭터의 지면 접촉 상태. 키네마틱 이동이 매 틱 갱신하고, 뷰(애니)와 네트워크 스냅샷이 읽는다.
    /// 지금은 지상/공중 두 상태뿐이라 bool — 수영·비행이 생기면 MovementMode enum이 될 자리.
    /// </summary>
    public class GroundState : Component
    {
        public bool IsGrounded { get; set; }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

UnityMCP `run_tests` (EditMode, filter `GroundStateTests`). 기대: 2/2 PASS.

- [ ] **Step 5: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/GameFramework
git checkout -b feature/ground-state
git add Runtime/Scripts/World/Components/GroundState.cs Runtime/Scripts/World/Components/GroundState.cs.meta \
        Tests/EditMode/GroundStateTests.cs Tests/EditMode/GroundStateTests.cs.meta
git commit -m "feat(world): GroundState 컴포넌트 — 지면 접촉 상태"
```

---

## Task 2: `KinematicMoveSystem`이 접지를 기록 (LOP-Shared)

`KinematicMover`가 이미 접지를 계산해 `KinematicMoveResult.grounded`로 돌려주는데, `KinematicMoveSystem.Tick`이 `position`·`velocity`만 쓰고 버리고 있다. 그 값을 `GroundState`에 쓴다.

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/KinematicMoveSystem.cs:27-44`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/KinematicMoveSystemGroundStateTests.cs`

**Interfaces:**
- Consumes: `GameFramework.World.GroundState` (Task 1)
- Produces: 없음 (기존 `Tick(Entity, float)` 시그니처 유지)

- [ ] **Step 1: 실패하는 테스트 작성**

`LeagueOfPhysical-Shared/Tests/EditMode/KinematicMoveSystemGroundStateTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using GameFramework.Physics;
using GameFramework.World;

namespace LOP.Tests.EditMode
{
    public class KinematicMoveSystemGroundStateTests
    {
        // 바닥에 닿는 상황 — 아래로 쓸면 즉시(거리 0) 막힌다.
        private class GroundedQuery : ICollisionQuery
        {
            public CollisionHit CapsuleCast(Vector3 point1, Vector3 point2, float radius,
                                            Vector3 direction, float distance, int layerMask)
            {
                if (direction.y < 0f)
                {
                    return new CollisionHit(true, 0f, Vector3.up, point1);
                }
                return CollisionHit.None;
            }
        }

        // 아무것도 막지 않음 — 공중.
        private class EmptyQuery : ICollisionQuery
        {
            public CollisionHit CapsuleCast(Vector3 point1, Vector3 point2, float radius,
                                            Vector3 direction, float distance, int layerMask)
                => CollisionHit.None;
        }

        private static Entity MakeCharacter()
        {
            var entity = new Entity("c1");
            entity.Add(new GameFramework.World.Transform());
            entity.Add(new Velocity());
            entity.Add(new GroundState());
            return entity;
        }

        [Test]
        public void WritesGroundedTrueWhenDownwardSweepBlocked()
        {
            var entity = MakeCharacter();
            var system = new KinematicMoveSystem(new GroundedQuery(), layerMask: ~0);

            system.Tick(entity, 0.02f);

            Assert.IsTrue(entity.Get<GroundState>().IsGrounded);
        }

        [Test]
        public void WritesGroundedFalseWhenNothingBlocks()
        {
            var entity = MakeCharacter();
            entity.Get<GroundState>().IsGrounded = true;   // 이전 틱 잔재
            var system = new KinematicMoveSystem(new EmptyQuery(), layerMask: ~0);

            system.Tick(entity, 0.02f);

            Assert.IsFalse(entity.Get<GroundState>().IsGrounded);
        }

        [Test]
        public void DoesNotThrowWhenGroundStateAbsent()
        {
            var entity = new Entity("c2");
            entity.Add(new GameFramework.World.Transform());
            entity.Add(new Velocity());
            var system = new KinematicMoveSystem(new EmptyQuery(), layerMask: ~0);

            Assert.DoesNotThrow(() => system.Tick(entity, 0.02f));
        }
    }
}
```

> `GroundedQuery`가 접지로 판정되게 하려면 `KinematicMover.Move` 안에서 접지가 어떤 조건으로
> `true`가 되는지(`KinematicMover.cs:90-112`)와 일치해야 한다. 테스트가 예상과 다르게 실패하면
> 그 구간을 읽고 **스텁의 반환값만** 맞춘다 — 프로덕션 코드를 테스트에 맞추지 않는다.

- [ ] **Step 2: 테스트가 실패하는지 확인**

UnityMCP `run_tests` (EditMode, filter `KinematicMoveSystemGroundStateTests`).
기대: `WritesGroundedTrueWhenDownwardSweepBlocked` FAIL (접지가 기록되지 않아 `false`).

- [ ] **Step 3: 최소 구현**

`KinematicMoveSystem.cs:42-43` 뒤에 추가:

```csharp
            transform.Position = result.position.ToNumerics();
            velocity.Linear = result.velocity.ToNumerics();

            var groundState = entity.Get<GameFramework.World.GroundState>();
            if (groundState != null)
            {
                groundState.IsGrounded = result.grounded;
            }
```

- [ ] **Step 4: 테스트 통과 확인**

UnityMCP `run_tests` (EditMode, filter `KinematicMoveSystemGroundStateTests`). 기대: 3/3 PASS.

- [ ] **Step 5: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git checkout -b feature/ground-state
git add Runtime/Scripts/Game/KinematicMoveSystem.cs \
        Tests/EditMode/KinematicMoveSystemGroundStateTests.cs Tests/EditMode/KinematicMoveSystemGroundStateTests.cs.meta
git commit -m "feat(kinematic): 접지 결과를 GroundState에 기록 — 버려지던 값 사용"
```

---

## Task 3: 클·서 `CharacterCreator`가 `GroundState`를 붙인다

**Files:**
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Entity/CharacterCreator.cs:55` 근처
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Entity/CharacterCreator.cs:55` 근처

**Interfaces:**
- Consumes: `GameFramework.World.GroundState` (Task 1)
- Produces: 모든 캐릭터 엔티티가 `GroundState`를 보유

- [ ] **Step 1: 클라 수정**

`LeagueOfPhysical-Client/Assets/Scripts/Entity/CharacterCreator.cs`의 `worldEntity.Add(new Abilities());` 바로 아래에 추가:

```csharp
            worldEntity.Add(new GameFramework.World.GroundState());
```

- [ ] **Step 2: 서버 수정**

`LeagueOfPhysical-Server/Assets/Scripts/Entity/CharacterCreator.cs`의 같은 자리에 **동일한 한 줄**을 추가한다.

- [ ] **Step 3: 커밋 (각 저장소)**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git checkout -b feature/ground-state
git add Assets/Scripts/Entity/CharacterCreator.cs
git commit -m "feat(entity): 캐릭터에 GroundState 부착"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git checkout -b feature/ground-state
git add Assets/Scripts/Entity/CharacterCreator.cs
git commit -m "feat(entity): 캐릭터에 GroundState 부착"
```

> 컴파일 검증은 슬라이스 1 머지 후 main 에디터에서 한다(Global Constraints).

---

## Task 4: `grounded` 와이어 — proto + 서버 채움 + 클라 반영

**Files:**
- Modify: `LeagueOfPhysical-Shared/Protos/EntitySnap.proto`
- Regenerate: `LeagueOfPhysical-Shared/Runtime.Generated/Scripts/Protobuf/EntitySnap*.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/EntitySnapshotBroadcastSystem.cs:57-95`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Netcode/EntitySnap.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/MessageHandler/GameEntityMessageHandler.cs:135-149`

**Interfaces:**
- Consumes: `GameFramework.World.GroundState` (Task 1)
- Produces: wire 필드 `EntitySnap.grounded` (번호 8), 클라 DTO `LOP.EntitySnap.grounded` (bool)

- [ ] **Step 1: proto에 필드 추가**

`LeagueOfPhysical-Shared/Protos/EntitySnap.proto`:

```protobuf
syntax = "proto3";
import "ProtoVector3.proto";
import "ProtoMotionContribution.proto";
message EntitySnap
{
	string entity_id = 1;
	ProtoVector3 position = 2;
	ProtoVector3 rotation = 3;
	ProtoVector3 velocity = 4;
	int32 max_HP = 5;
	int32 current_HP = 6;
	repeated ProtoMotionContribution motion_contributions = 7;
	bool grounded = 8;
}
```

- [ ] **Step 2: 재생성 후 MessageIds가 안 바뀌었는지 확인**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Scripts
./generate_protos.sh
cd ..
git diff --stat Runtime.Generated/Scripts/MessageIds.cs
```

기대: `MessageIds.cs`에 **변경 없음**(출력 비어 있음). 변경됐다면 wire 계약이 깨진 것이므로
`git checkout Runtime.Generated/Scripts/MessageIds.cs`로 되돌리고 원인을 조사한다.

- [ ] **Step 3: 서버가 채운다**

`EntitySnapshotBroadcastSystem.BuildAllEntitySnaps`의 `snap` 초기화 직후(`MotionContributions` 루프 앞)에 추가:

```csharp
                snap.Grounded = worldEntity.Get<GameFramework.World.GroundState>()?.IsGrounded ?? false;
```

- [ ] **Step 4: 클라 DTO에 필드 추가**

`LeagueOfPhysical-Client/Assets/Scripts/Netcode/EntitySnap.cs`의 `velocity` 아래에 추가:

```csharp
        public bool grounded { get; set; }
```

AutoMapper가 이름이 같은 프로퍼티를 자동 매핑하므로 `ProtoMapperProfile` 수정은 불필요하다.

- [ ] **Step 5: 클라가 원격 엔티티에 반영**

`GameEntityMessageHandler.OnEntitySnapsToC`의 `else` 블록(원격 분기), `healthSystem.ApplyAuthoritativeState(...)` 처리 **뒤**, `AddServerEntitySnap` **앞**에 추가:

```csharp
                    GameFramework.World.GroundState groundState =
                        entityRegistry.Get(serverEntitySnap.EntityId)?.Get<GameFramework.World.GroundState>();
                    if (groundState != null)
                    {
                        groundState.IsGrounded = serverEntitySnap.Grounded;
                    }
```

내 캐릭 분기(`reconciler.AddServerSnap`)는 **수정하지 않는다** — 내 캐릭 접지는 로컬 `KinematicMoveSystem`이 이미 쓴다.

- [ ] **Step 6: 커밋 (3개 저장소)**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git add Protos/EntitySnap.proto Runtime.Generated/
git commit -m "feat(wire): EntitySnap에 grounded 필드 추가"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git add Assets/Scripts/Game/TickSystems/EntitySnapshotBroadcastSystem.cs
git commit -m "feat(snapshot): grounded 브로드캐스트"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Netcode/EntitySnap.cs Assets/Scripts/Game/MessageHandler/GameEntityMessageHandler.cs
git commit -m "feat(snapshot): 원격 엔티티 grounded 반영"
```

---

## Task 5: 뷰가 `GroundState`를 쓰고 `"Plane"` 판정을 삭제

**Files:**
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Entity/LOPEntityView.cs:82-109`

**Interfaces:**
- Consumes: `GameFramework.World.GroundState` (Task 1, Task 4가 원격에 채움)
- Produces: 없음

- [ ] **Step 1: `UpdateRunAnimation`을 `GroundState` 기반으로 교체**

`LOPEntityView.cs`의 `UpdateRunAnimation` 본문에서 `IsGrounded(...)` 호출을 바꾼다:

```csharp
            const float walkThreshold = 0.01f;
            var worldEntity = entityRegistry.Get(entityId);
            Vector3 v = worldEntity != null ? GameFramework.World.EntityMotionExtensions.GetVelocity(worldEntity) : Vector3.zero;
            float horizontalSpeedSquared = v.x * v.x + v.z * v.z;
            bool grounded = worldEntity?.Get<GameFramework.World.GroundState>()?.IsGrounded ?? false;
            animator.SetBool("Run", horizontalSpeedSquared > walkThreshold * walkThreshold && grounded);
```

- [ ] **Step 2: `IsGrounded` 메서드 삭제**

`LOPEntityView.cs:103-109`의 아래 블록을 통째로 제거한다:

```csharp
        // TODO: 고도화 필요! (접지 판정 — 구 LOPActor에서 이전)
        private static bool IsGrounded(Vector3 position)
        {
            Vector3 checkPosition = position + Vector3.down * 0.2f;
            Collider[] colliders = Physics.OverlapSphere(checkPosition, 0.4f);
            return System.Linq.Enumerable.Any(colliders, col => col.gameObject.name == "Plane");
        }
```

- [ ] **Step 3: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Entity/LOPEntityView.cs
git commit -m "refactor(view): 접지를 GroundState에서 읽고 'Plane' 이름 판정 삭제"
```

- [ ] **Step 4: 슬라이스 1 머지 + 인게임 검증**

6개 중 4개 저장소(GameFramework / LOP-Shared / Client / Server)의 `feature/ground-state`를 각각 main에 `--no-ff` 머지한다. 이후 main 에디터에서:

1. UnityMCP `refresh_unity`(클·서) → `read_console`로 컴파일 에러 0 확인
2. 2에디터 실행 — 상대 캐릭터가 점프·낙하할 때 걷기 애니가 공중에서 꺼지는지 육안 확인
3. 회귀: 내 캐릭 점프/걷기가 이전과 동일한지 확인

---

# 슬라이스 2 — 어빌리티 시전 상태

## Task 6–7: `AbilityPlayback` 커널 + `ForPresentation` 팩토리 (LOP-Shared)

> **Task 6과 7은 한 단위로 구현·리뷰한다.** Task 6의 테스트가 Task 7이 만드는
> `ActiveAbility.ForPresentation`을 쓰기 때문에 6만으로는 컴파일되지 않는다. 한 번에 구현하고
> 한 번 커밋한다.

### Task 6: `AbilityPlayback` 진행도 커널

시전 진행도·페이즈를 절대 틱에서 환산하는 순수 함수. 이 작업에서 **틀리기 쉬운 유일한 계산**이라 테스트로 덮는다.

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/Ability/AbilityPlayback.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/AbilityPlaybackTests.cs`

**Interfaces:**
- Consumes: `LOP.ActiveAbility`(`AbilityId`/`StartupEndTick`/`ActiveEndTick`/`RecoveryEndTick`), `LOP.AbilityPhase`
- Produces:
  ```csharp
  public static bool Solve(in ActiveAbility active, long currentTick, long totalTicks,
                           out AbilityPhase phase, out float normalizedTime)
  ```
  `totalTicks <= 0`이거나 `currentTick`이 시전 구간 밖이면 `false` 반환(출력은 `Ready`/`0f`).

- [ ] **Step 1: 실패하는 테스트 작성**

`LeagueOfPhysical-Shared/Tests/EditMode/AbilityPlaybackTests.cs`:

```csharp
using NUnit.Framework;

namespace LOP.Tests.EditMode
{
    public class AbilityPlaybackTests
    {
        // startup 10틱, active 20틱, recovery 10틱 = 총 40틱. 발동 100틱 → 종료 140틱.
        private const long Total = 40;
        private static ActiveAbility Make() =>
            ActiveAbility.ForPresentation(abilityId: 7, startupEndTick: 110, activeEndTick: 130, recoveryEndTick: 140);

        [Test]
        public void StartOfCastIsStartupAtZero()
        {
            Assert.IsTrue(AbilityPlayback.Solve(Make(), 100, Total, out var phase, out float t));
            Assert.AreEqual(AbilityPhase.Startup, phase);
            Assert.AreEqual(0f, t, 1e-4f);
        }

        [Test]
        public void MidStartup()
        {
            Assert.IsTrue(AbilityPlayback.Solve(Make(), 105, Total, out var phase, out float t));
            Assert.AreEqual(AbilityPhase.Startup, phase);
            Assert.AreEqual(0.125f, t, 1e-4f);   // (105-100)/40
        }

        [Test]
        public void StartupEndTickBelongsToActive()
        {
            Assert.IsTrue(AbilityPlayback.Solve(Make(), 110, Total, out var phase, out _));
            Assert.AreEqual(AbilityPhase.Active, phase);
        }

        [Test]
        public void ActiveEndTickBelongsToRecovery()
        {
            Assert.IsTrue(AbilityPlayback.Solve(Make(), 130, Total, out var phase, out _));
            Assert.AreEqual(AbilityPhase.Recovery, phase);
        }

        [Test]
        public void LastTickIsAlmostOne()
        {
            Assert.IsTrue(AbilityPlayback.Solve(Make(), 139, Total, out _, out float t));
            Assert.AreEqual(0.975f, t, 1e-4f);   // (139-100)/40
        }

        [Test]
        public void AtOrAfterEndTickIsNotPlaying()
        {
            Assert.IsFalse(AbilityPlayback.Solve(Make(), 140, Total, out var phase, out float t));
            Assert.AreEqual(AbilityPhase.Ready, phase);
            Assert.AreEqual(0f, t);
        }

        [Test]
        public void BeforeActivationIsNotPlaying()
        {
            Assert.IsFalse(AbilityPlayback.Solve(Make(), 99, Total, out _, out _));
        }

        [Test]
        public void NonPositiveTotalIsNotPlaying()
        {
            Assert.IsFalse(AbilityPlayback.Solve(Make(), 105, 0, out _, out _));
        }
    }
}
```

- [ ] **Step 2: 테스트가 실패하는지 확인**

UnityMCP `run_tests` (EditMode, filter `AbilityPlaybackTests`).
기대: 컴파일 실패 — `AbilityPlayback`과 `ActiveAbility.ForPresentation` 없음.

> `ForPresentation`은 Task 7에서 만든다. 이 테스트는 Task 7 완료 후에 통과한다 — 두 태스크를
> 이어서 진행하고, Task 7의 Step 4에서 두 테스트 클래스를 함께 돌린다.

- [ ] **Step 3: 커널 구현**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/Ability/AbilityPlayback.cs`:

```csharp
namespace LOP
{
    /// <summary>
    /// 시전 진행도 환산 커널(순수). 페이즈 경계가 절대 틱이므로 "지금 몇 틱"만 알면 산수로 풀린다 —
    /// 클라는 원격 어빌리티를 시뮬하지 않고 이 함수로 그림만 맞춘다.
    /// 컨텍스트 없는 순수 계산이라 static이며 *System 이름을 붙이지 않는다.
    /// </summary>
    public static class AbilityPlayback
    {
        /// <summary>
        /// <paramref name="currentTick"/> 시점의 페이즈와 전체 진행도(0~1)를 구한다.
        /// 시전 중이 아니면 false(출력은 Ready/0).
        /// </summary>
        /// <param name="totalTicks">startup+active+recovery 합 — 발동 틱을 역산하는 데 쓴다.</param>
        public static bool Solve(in ActiveAbility active, long currentTick, long totalTicks,
                                 out AbilityPhase phase, out float normalizedTime)
        {
            phase = AbilityPhase.Ready;
            normalizedTime = 0f;

            if (totalTicks <= 0)
            {
                return false;
            }

            long activationTick = active.RecoveryEndTick - totalTicks;
            if (currentTick < activationTick || currentTick >= active.RecoveryEndTick)
            {
                return false;
            }

            normalizedTime = (float)(currentTick - activationTick) / totalTicks;

            // 경계 틱은 다음 페이즈에 속한다 — AbilitySystem.Tick의 `currentTick >= 경계` 전진과 같은 규칙.
            if (currentTick < active.StartupEndTick)
            {
                phase = AbilityPhase.Startup;
            }
            else if (currentTick < active.ActiveEndTick)
            {
                phase = AbilityPhase.Active;
            }
            else
            {
                phase = AbilityPhase.Recovery;
            }
            return true;
        }
    }
}
```

- [ ] **Step 4: Task 7로 이어서 진행**

이 태스크는 단독 커밋하지 않는다 — Task 7 완료 후 함께 커밋한다.

---

### Task 7: `ActiveAbility.ForPresentation` 팩토리

원격 엔티티의 시전 상태를 연출용으로만 부분 복원하는 생성 경로.

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/Ability/Abilities.cs:26-62`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/AbilityPlaybackTests.cs` (Task 6에서 이미 사용 중)

**Interfaces:**
- Consumes: `LOP.ActiveAbility` 기존 생성자
- Produces:
  ```csharp
  public static ActiveAbility ForPresentation(int abilityId, long startupEndTick,
                                              long activeEndTick, long recoveryEndTick)
  ```
  `Phase`는 `Startup`, `Target`은 `null`, `Effects`는 빈 배열, 이동 스케일은 `1f`, `BlockJump`는 `false`.

- [ ] **Step 1: 팩토리 추가**

`Abilities.cs`의 `ActiveAbility` struct 안, 기존 생성자 아래에 추가:

```csharp
        /// <summary>
        /// 연출용 부분 복원 — 어빌리티 id와 페이즈 경계만 채운다(원격 엔티티 스냅샷 반영용).
        /// 효과 목록·이동 스케일·점프 봉인 같은 시뮬 파라미터는 비운다: 클라는 원격 어빌리티를 실행하지 않는다.
        /// Phase는 뷰가 <see cref="AbilityPlayback.Solve"/>로 매 프레임 다시 구하므로 의미 없는 초기값이다.
        /// </summary>
        public static ActiveAbility ForPresentation(int abilityId, long startupEndTick,
                                                    long activeEndTick, long recoveryEndTick)
        {
            return new ActiveAbility(abilityId, AbilityPhase.Startup,
                startupEndTick, activeEndTick, recoveryEndTick,
                null, System.Array.Empty<AbilityEffect>(), 1f, 1f, 1f, false);
        }
```

> 파라미터 순서는 `AbilitySystem.TryActivate`의 호출부와 동일하다:
> `(abilityId, phase, startupEnd, activeEnd, recoveryEnd, target, effects, startupMoveScale,
> activeMoveScale, recoveryMoveScale, blockJump)`.

- [ ] **Step 2: `Target` 필드에 취지 주석 추가**

`ActiveAbility`의 `public readonly Entity Target;` 위에:

```csharp
        /// <summary>
        /// 발동 전에 미리 지목한 대상. 현재 모든 어빌리티가 self 또는 광역 스윕이라 항상 시전자가
        /// 들어가며 읽는 곳이 없다 — 대상 지목형 스킬이 생길 때를 위한 자리.
        /// 명중해서 정해지는 대상은 여기가 아니라 <see cref="AttackHitContext.LandedTargets"/>에 있다.
        /// </summary>
```

- [ ] **Step 3: 테스트 통과 확인**

UnityMCP `run_tests` (EditMode, filter `AbilityPlaybackTests`). 기대: 8/8 PASS.

- [ ] **Step 4: 커밋 (Task 6 + Task 7 함께)**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git checkout -b feature/ability-cast-state
git add Runtime/Scripts/Game/Ability/AbilityPlayback.cs Runtime/Scripts/Game/Ability/AbilityPlayback.cs.meta \
        Runtime/Scripts/Game/Ability/Abilities.cs \
        Tests/EditMode/AbilityPlaybackTests.cs Tests/EditMode/AbilityPlaybackTests.cs.meta
git commit -m "feat(ability): 시전 진행도 커널 + 연출용 ActiveAbility 복원 팩토리"
```

---

## Task 8: 시전 상태 와이어 — proto + 서버 채움

**Files:**
- Modify: `LeagueOfPhysical-Shared/Protos/EntitySnap.proto`
- Regenerate: `LeagueOfPhysical-Shared/Runtime.Generated/Scripts/Protobuf/EntitySnap*.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/EntitySnapshotBroadcastSystem.cs`

**Interfaces:**
- Consumes: `LOP.Abilities.ActiveAbility`
- Produces: wire 필드 `active_ability_id`(9, int32), `ability_end_tick`(10, int64)

- [ ] **Step 1: proto에 필드 추가**

`EntitySnap.proto`의 `bool grounded = 8;` 아래에:

```protobuf
	int32 active_ability_id = 9;    // 0 = 시전 중 아님
	int64 ability_end_tick  = 10;   // 시전이 끝나는 절대 틱(= RecoveryEndTick)
```

- [ ] **Step 2: 재생성 + MessageIds 무변경 확인**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Scripts
./generate_protos.sh
cd ..
git diff --stat Runtime.Generated/Scripts/MessageIds.cs
```

기대: 출력 비어 있음.

- [ ] **Step 3: 서버가 채운다**

`BuildAllEntitySnaps`의 `snap.Grounded = ...` 아래에 추가:

```csharp
                var activeAbility = worldEntity.Get<Abilities>()?.ActiveAbility;
                if (activeAbility != null)
                {
                    snap.ActiveAbilityId = activeAbility.Value.AbilityId;
                    snap.AbilityEndTick = activeAbility.Value.RecoveryEndTick;
                }
```

- [ ] **Step 4: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git add Protos/EntitySnap.proto Runtime.Generated/
git commit -m "feat(wire): EntitySnap에 시전 상태(어빌리티 id + 종료 틱) 추가"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git checkout -b feature/ability-cast-state
git add Assets/Scripts/Game/TickSystems/EntitySnapshotBroadcastSystem.cs
git commit -m "feat(snapshot): 시전 상태 브로드캐스트"
```

---

## Task 9: 클라가 원격 시전 상태를 복원

`ability_end_tick` 하나에서 페이즈 경계 3개를 마스터데이터로 역산한다.

**Files:**
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Netcode/EntitySnap.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/MessageHandler/GameEntityMessageHandler.cs`

**Interfaces:**
- Consumes: `LOP.ActiveAbility.ForPresentation` (Task 7), `LOP.AbilityDataProvider.TryGet(int, out AbilityData)`
- Produces: 원격 엔티티의 `Abilities.ActiveAbility`가 스냅샷과 동기

- [ ] **Step 1: 클라 DTO에 필드 추가**

`Assets/Scripts/Netcode/EntitySnap.cs`의 `grounded` 아래:

```csharp
        public int activeAbilityId { get; set; }
        public long abilityEndTick { get; set; }
```

- [ ] **Step 2: 핸들러에 `AbilityDataProvider` 주입**

`GameEntityMessageHandler`의 필드·생성자 파라미터·대입에 추가한다. 기존 `reconciler` 항목 바로 뒤에 같은 형태로:

```csharp
        private readonly AbilityDataProvider abilityDataProvider;
```

생성자 파라미터 목록의 `Reconciler reconciler,` 다음에 `AbilityDataProvider abilityDataProvider,`를 넣고,
본문에 `this.abilityDataProvider = abilityDataProvider;`를 추가한다.

- [ ] **Step 3: 원격 분기에 복원 로직 추가**

Task 4에서 넣은 `groundState` 반영 블록 바로 아래에:

```csharp
                    GameFramework.World.Entity remoteEntity = entityRegistry.Get(serverEntitySnap.EntityId);
                    Abilities remoteAbilities = remoteEntity?.Get<Abilities>();
                    if (remoteAbilities != null)
                    {
                        if (serverEntitySnap.ActiveAbilityId == 0)
                        {
                            remoteAbilities.ActiveAbility = null;
                        }
                        else if (abilityDataProvider.TryGet(serverEntitySnap.ActiveAbilityId, out AbilityData abilityData))
                        {
                            // 종료 틱 하나에서 경계를 역산 — 클·서가 같은 마스터데이터를 보므로 값이 일치한다.
                            long recoveryEnd = serverEntitySnap.AbilityEndTick;
                            long activeEnd = recoveryEnd - abilityData.RecoveryTicks;
                            long startupEnd = activeEnd - abilityData.ActiveTicks;
                            remoteAbilities.ActiveAbility = ActiveAbility.ForPresentation(
                                serverEntitySnap.ActiveAbilityId, startupEnd, activeEnd, recoveryEnd);
                        }
                    }
```

- [ ] **Step 4: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git checkout -b feature/ability-cast-state
git add Assets/Scripts/Netcode/EntitySnap.cs Assets/Scripts/Game/MessageHandler/GameEntityMessageHandler.cs
git commit -m "feat(snapshot): 원격 시전 상태 복원 — 종료 틱에서 페이즈 경계 역산"
```

---

## Task 10: `TbAbilityView` 클라 전용 테이블

**Files:**
- Modify (Excel): `infrastructure/table/Datas/__tables__.xlsx`
- Create (Excel): `infrastructure/table/Datas/#AbilityView.xlsx`
- Modify (Excel): `infrastructure/table/Datas/#Ability.xlsx` (`cue` 컬럼 제거)
- Regenerate: `LeagueOfPhysical-MasterData-Client/Runtime.Generated/**`

**Interfaces:**
- Produces: `LOP.MasterData.Tables.TbAbilityView` — `GetOrDefault(int)` → `AbilityView { int Id; string AnimState; int AnimLayer; }`

- [ ] **Step 1: `#AbilityView.xlsx` 생성**

어빌리티 id는 `#Ability.xlsx`에서 확인된다 — **1 = haste, 2 = dash, 3 = attack, 4 = global_attack**
(`CharacterCreator`가 1·2·3을 `Grant`한다). 연출이 필요한 것은 공격 계열뿐이다:

```python
import openpyxl
wb = openpyxl.Workbook()
ws = wb.active
for r in [
    ['##var',   'id',  'anim_state', 'anim_layer'],
    ['##type',  'int', 'string',     'int'],
    ['##group', '',    '',           ''],
    ['##',      'id',  'anim_state', 'anim_layer'],
    ['',        '3',   'Attack01',   '1'],   # attack
    ['',        '4',   'Attack01',   '1'],   # global_attack (테스트용)
]:
    ws.append(r)
wb.save('Datas/#AbilityView.xlsx')
```

haste·dash는 행을 넣지 않는다 — `GetOrDefault`가 `null`을 돌려주면 연출 없음으로 취급한다
(`anim_state`가 빈 문자열이어도 동일).

> `Attack01`과 레이어 번호는 **실제 값으로 대체해야 한다.** 캐릭터 프리팹의 Animator Controller를
> 열어 공격 스테이트의 정확한 이름과 레이어 인덱스를 확인한다. 현재 코드가 후보로 던지던 이름이
> `"Attack 01"` / `"Attack"` / `"Melee Attack"` 셋이었으므로 캐릭터마다 다를 수 있다 — 다르면
> 스펙 §6의 미결(키를 `(캐릭터, 어빌리티)`로 확장) 판단이 필요하니 그 시점에 보고한다.

- [ ] **Step 2: `__tables__.xlsx`에 테이블 등록**

기존 `TbStatusEffect` 행과 같은 형태로 한 줄 추가한다(컬럼 순서:
`##var, full_name, value_type, read_schema_from_file, input, index, mode, group, comment, tags, output`):

```
['', 'TbAbilityView', 'AbilityView', 'TRUE', '#AbilityView.xlsx', 'id', 'map', 'c', 'AbilityView', '', '']
```

**`group`(8번째 칸)을 반드시 `c`로 지정**한다 — 클라 전용이라 서버 타깃 생성에서 제외되어야 한다
(`TbSkinAsset` 행이 같은 방식의 선례).

- [ ] **Step 3: `#Ability.xlsx`에서 `cue` 컬럼 제거**

`cue`는 11번째 컬럼이며 `##group`이 `c`다. `TbAbilityView`가 역할을 대신하므로
`##var`/`##type`/`##group`/`##` 4개 헤더 행과 데이터 행(5~8행)에서 해당 열을 통째로 삭제한다
(`openpyxl`의 `ws.delete_cols(11)`).

- [ ] **Step 4: 재생성**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table
./gen.sh
```

기대: `[done]` 출력. 이후 확인:

```bash
ls ../../LeagueOfPhysical-MasterData-Client/Runtime.Generated/Scripts/MasterData/ | grep -i abilityview
ls ../../LeagueOfPhysical-MasterData-Server/Runtime.Generated/Scripts/MasterData/ | grep -i abilityview
```

기대: **클라에만** `AbilityView.cs`/`TbAbilityView.cs`가 있고 **서버에는 없다**.

- [ ] **Step 5: `cue`를 쓰던 클라 코드 수정**

`LeagueOfPhysical-Client/Assets/Scripts/Netcode/WorldEventSink.cs`가 `TbAbility...Cue`를 읽고 있다.
`AbilityActivatedEvent` 발행 자체는 유지하되(스펙 §5-4), cue 조회를 없애고 어빌리티 id를 그대로 싣는다:

```csharp
                    case GameFramework.World.AbilityActivatedEvent ae:
                        // 발동 순간의 일회성 연출용(사운드·캐스팅 VFX) — 지속 모션은 스냅샷에서 파생한다.
                        // 현재 구독자 없음: 사운드를 붙일 때 이 경로를 쓴다.
                        abilityActivatedPublisher.Publish(ae.entityId, new AbilityActivated(ae.abilityId));
                        break;
```

`LOP.Event.Entity.AbilityActivated`의 필드를 `string cue` → `int abilityId`로 바꾼다
(`Assets/Scripts/Entity/Event.Entity.cs`). `md` 의존이 사라지면 `WorldEventSink` 생성자에서
`LOPMasterData` 파라미터를 제거하고, DI 등록부(`GameLifetimeScope`)도 함께 정리한다.

- [ ] **Step 6: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure
git checkout -b feature/ability-view-table
git add table/Datas/
git commit -m "feat(masterdata): TbAbilityView 클라 전용 테이블 추가, TbAbility.cue 제거"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client
git checkout -b feature/ability-view-table
git add Runtime.Generated/
git commit -m "chore(gen): TbAbilityView 생성물"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server
git checkout -b feature/ability-view-table
git add Runtime.Generated/
git commit -m "chore(gen): TbAbility.cue 제거 반영"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Netcode/WorldEventSink.cs Assets/Scripts/Entity/Event.Entity.cs Assets/Scripts/Game/GameLifetimeScope.cs
git commit -m "refactor(view): cue 문자열 대신 어빌리티 id 전달"
```

---

## Task 11: `EntityRenderClock` + 뷰를 상태 기반으로 전환

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Entity/EntityRenderClock.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Entity/LOPEntityView.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/GameLifetimeScope.cs` (DI 등록)

**Interfaces:**
- Consumes: `IPlayerContext.entityId`, `IRunner.tickUpdater.tick`, `RemoteInterpolationClock.RenderTime`/`HasSnapshot`, `IGameDataStore.gameInfo.Interval`, `AbilityPlayback.Solve` (Task 6), `AbilityDataProvider` (기존), `LOP.MasterData.Tables.TbAbilityView` (Task 10)
- Produces: `LOP.EntityRenderClock.TickFor(string entityId) → long`

- [ ] **Step 1: `EntityRenderClock` 작성**

`Assets/Scripts/Entity/EntityRenderClock.cs`:

```csharp
namespace LOP
{
    /// <summary>
    /// 이 엔티티의 연출을 "몇 틱 시점"으로 그릴지 답한다.
    /// 내 캐릭은 예측 틱(지금), 원격은 보간 재생 시계(과거) — 위치와 애니가 같은 시점을 쓰게 한다.
    /// </summary>
    public class EntityRenderClock
    {
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.Runner.IRunner runner;
        private readonly RemoteInterpolationClock remoteClock;
        private readonly IGameDataStore gameDataStore;

        public EntityRenderClock(IPlayerContext playerContext, GameFramework.Runner.IRunner runner,
                                 RemoteInterpolationClock remoteClock, IGameDataStore gameDataStore)
        {
            this.playerContext = playerContext;
            this.runner = runner;
            this.remoteClock = remoteClock;
            this.gameDataStore = gameDataStore;
        }

        public long TickFor(string entityId)
        {
            if (entityId != null && entityId == playerContext.entityId)
            {
                return runner.tickUpdater?.tick ?? 0;
            }

            float interval = gameDataStore.gameInfo.Interval;
            if (remoteClock.HasSnapshot == false || interval <= 0f)
            {
                return runner.tickUpdater?.tick ?? 0;
            }
            return (long)(remoteClock.RenderTime / interval);
        }
    }
}
```

> 이 네 타입은 `GameEntityMessageHandler`가 이미 같은 형태로 주입받고 있다
> (`IRunner runner` / `IPlayerContext playerContext` / `IGameDataStore gameDataStore` /
> `RemoteInterpolationClock remoteInterpolationClock`).

- [ ] **Step 2: DI 등록**

`GameLifetimeScope`에서 `RemoteInterpolationClock`을 등록하는 줄 근처에 추가:

```csharp
            builder.Register<EntityRenderClock>(Lifetime.Singleton);
```

- [ ] **Step 3: 뷰에 시전 애니 구동 추가**

`LOPEntityView.cs`:

주입 필드 추가 (`entityRegistry` 아래):

```csharp
        [Inject] private EntityRenderClock renderClock;
        [Inject] private AbilityDataProvider abilityDataProvider;
        [Inject] private LOP.MasterData.LOPMasterData masterData;
```

상태 필드 추가:

```csharp
        // 직전 프레임에 그린 시전. 어빌리티가 바뀌는 순간을 잡아 Play를 한 번만 부르기 위한 것.
        private int playingAbilityId;
        private const float ResyncThreshold = 0.1f;
```

`Update`를 다음으로 교체:

```csharp
        private void Update()
        {
            UpdateRunAnimation();
            UpdateAbilityAnimation();
        }
```

`UpdateAbilityAnimation` 추가:

```csharp
        // 시전 모션은 지속 상태(ActiveAbility)에서 파생한다 — 트리거와 달리 중간부터 재생할 수 있어
        // 늦게 접속하거나 이벤트를 놓친 클라도 올바른 지점을 본다.
        private void UpdateAbilityAnimation()
        {
            if (entityId == null || visualGameObject == null)
            {
                return;
            }
            Animator animator = visualGameObject.GetComponent<Animator>();
            if (animator == null)
            {
                return;
            }

            var active = entityRegistry.Get(entityId)?.Get<Abilities>()?.ActiveAbility;
            if (active == null || abilityDataProvider.TryGet(active.Value.AbilityId, out AbilityData data) == false)
            {
                playingAbilityId = 0;
                return;
            }

            long totalTicks = data.StartupTicks + data.ActiveTicks + data.RecoveryTicks;
            long tick = renderClock.TickFor(entityId);
            if (AbilityPlayback.Solve(active.Value, tick, totalTicks, out _, out float normalizedTime) == false)
            {
                playingAbilityId = 0;
                return;
            }

            var view = masterData.Tables.TbAbilityView.GetOrDefault(active.Value.AbilityId);
            if (view == null || string.IsNullOrEmpty(view.AnimState))
            {
                playingAbilityId = 0;
                return;   // 연출 없는 어빌리티(대시 등)
            }

            // 매 프레임 Play하면 블렌딩·전이가 죽는다 → 시작 순간과 크게 어긋난 때만 개입.
            bool justStarted = playingAbilityId != active.Value.AbilityId;
            bool drifted = false;
            if (justStarted == false)
            {
                var info = animator.GetCurrentAnimatorStateInfo(view.AnimLayer);
                drifted = Mathf.Abs(info.normalizedTime - normalizedTime) > ResyncThreshold;
            }

            if (justStarted || drifted)
            {
                animator.Play(view.AnimState, view.AnimLayer, normalizedTime);
                playingAbilityId = active.Value.AbilityId;
            }
        }
```

- [ ] **Step 4: 트리거 경로 삭제**

`LOPEntityView.cs`에서 제거한다:
- `CueTriggers` 정적 dict (`:113-117`)
- `OnAbilityActivated` 메서드 (`:119-134`)
- `Start`의 `AbilityActivated` 구독 줄 (`:47`)

`OnEntityDamage`의 `"Hit"` 트리거는 **유지**한다 — 피격 리액션은 순간 연출이라 이벤트가 맞다.

- [ ] **Step 5: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Entity/EntityRenderClock.cs Assets/Scripts/Entity/EntityRenderClock.cs.meta \
        Assets/Scripts/Entity/LOPEntityView.cs Assets/Scripts/Game/GameLifetimeScope.cs
git commit -m "feat(view): 시전 애니를 상태에서 파생 — 트리거 경로 제거"
```

- [ ] **Step 6: 슬라이스 2 머지 + 인게임 검증**

6개 저장소의 슬라이스 2 브랜치를 각각 main에 `--no-ff` 머지 후, main 에디터에서:

1. UnityMCP `refresh_unity`(클·서) → `read_console` 컴파일 에러 0
2. 2에디터 실행, 상대 공격 → 모션이 나오는지
3. Mirror Latency Simulation 손실 20~30% 설정 후 공격 연타 → **모션 누락 없음**
4. 회귀: 대시·헤이스트가 이전과 동일

---

# 슬라이스 3 — 상태이상

## Task 12: `TargetMode` + 핸들러 대상 분기 (LOP-Shared)

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/Ability/AbilityEffect.cs:26-35`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/Ability/StatusEffectApplyEffectHandler.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/StatusEffectApplyTargetModeTests.cs`

**Interfaces:**
- Consumes: `LOP.AttackHitContext.LandedTargets`(`IReadOnlyCollection<string>`), `GameFramework.World.EntityRegistry`
- Produces:
  - `public enum TargetMode { Self, HitTargets }`
  - `StatusEffectApplyEffect(int statusEffectId, TargetMode targetMode = TargetMode.Self)` — `TargetMode Target` 필드 노출
  - `StatusEffectApplyEffectHandler(StatusEffectSystem, Func<int, StatusEffectData?>, EntityRegistry)` — 생성자에 레지스트리 추가

- [ ] **Step 1: 실패하는 테스트 작성**

`LeagueOfPhysical-Shared/Tests/EditMode/StatusEffectApplyTargetModeTests.cs`:

```csharp
using System;
using NUnit.Framework;
using GameFramework.World;

namespace LOP.Tests.EditMode
{
    public class StatusEffectApplyTargetModeTests
    {
        private const int SlowId = 100;

        private static StatusEffectData SlowData() => new StatusEffectData(
            SlowId, DurationPolicy.Duration, 60,
            new[] { new StatusModifierSpec((int)EntityStatType.MoveSpeed, -0.3f, ModifierType.PercentAdd) },
            StatusStackPolicy.Refresh, 1);

        private static Entity MakeActor(string id)
        {
            var e = new Entity(id);
            e.Add(new StatusEffects());
            e.Add(new Stats());
            return e;
        }

        private static bool HasSlow(Entity e) =>
            e.Get<StatusEffects>().Effects.Exists(x => x.EffectId == SlowId);

        private (StatusEffectApplyEffectHandler handler, EntityRegistry registry) Build()
        {
            var registry = new EntityRegistry();
            var handler = new StatusEffectApplyEffectHandler(
                new StatusEffectSystem(new StatsSystem()), _ => SlowData(), registry);
            return (handler, registry);
        }

        [Test]
        public void SelfModeAppliesToCasterOnly()
        {
            var (handler, registry) = Build();
            var caster = MakeActor("caster");
            var victim = MakeActor("victim");
            registry.Add(caster);
            registry.Add(victim);

            var hit = new AttackHitContext();
            hit.MarkLanded("victim");
            var ctx = new AbilityEffectContext(caster, caster, 10, 0, hit);

            handler.OnActiveEnter(ctx, new StatusEffectApplyEffect(SlowId, TargetMode.Self));

            Assert.IsTrue(HasSlow(caster));
            Assert.IsFalse(HasSlow(victim));
        }

        [Test]
        public void HitTargetsModeAppliesToLandedOnly()
        {
            var (handler, registry) = Build();
            var caster = MakeActor("caster");
            var hitVictim = MakeActor("hit");
            var missedVictim = MakeActor("missed");
            registry.Add(caster);
            registry.Add(hitVictim);
            registry.Add(missedVictim);

            var hit = new AttackHitContext();
            hit.MarkLanded("hit");
            var ctx = new AbilityEffectContext(caster, caster, 10, 0, hit);

            handler.OnActiveEnter(ctx, new StatusEffectApplyEffect(SlowId, TargetMode.HitTargets));

            Assert.IsTrue(HasSlow(hitVictim));
            Assert.IsFalse(HasSlow(missedVictim));
            Assert.IsFalse(HasSlow(caster));
        }

        [Test]
        public void HitTargetsModeWithNoLandedTargetsDoesNothing()
        {
            var (handler, registry) = Build();
            var caster = MakeActor("caster");
            registry.Add(caster);

            var ctx = new AbilityEffectContext(caster, caster, 10, 0, new AttackHitContext());

            Assert.DoesNotThrow(() =>
                handler.OnActiveEnter(ctx, new StatusEffectApplyEffect(SlowId, TargetMode.HitTargets)));
            Assert.IsFalse(HasSlow(caster));
        }

        [Test]
        public void DefaultModeIsSelf()
        {
            Assert.AreEqual(TargetMode.Self, new StatusEffectApplyEffect(SlowId).Target);
        }
    }
}
```

> `StatusEffectSystem`의 생성자는 `StatusEffectSystem(StatsSystem statsSystem)` 하나뿐이다.
> `StatusModifierSpec`의 인자 순서는 기존 `StatusEffectSystemTests.cs`를 참고한다.

- [ ] **Step 2: 테스트가 실패하는지 확인**

UnityMCP `run_tests` (EditMode, filter `StatusEffectApplyTargetModeTests`).
기대: 컴파일 실패 — `TargetMode` 없음, 생성자 인자 수 불일치.

- [ ] **Step 3: `TargetMode` + effect 필드 추가**

`AbilityEffect.cs`의 `StatusEffectApplyEffect`를 교체:

```csharp
    /// <summary>상태효과를 누구에게 걸지. 발동 전에 지목하는 대상(ActiveAbility.Target)과는 다른 축이다.</summary>
    public enum TargetMode
    {
        /// <summary>시전자 자신.</summary>
        Self,
        /// <summary>이번 발동에서 명중한 대상 전원(AttackHitContext).</summary>
        HitTargets,
    }

    /// <summary>상태효과를 건다(버프/디버프). 적용된 효과는 독립 <see cref="StatusEffects"/> 컴포넌트로 살아간다(수명 분리).</summary>
    public sealed class StatusEffectApplyEffect : AbilityEffect
    {
        public readonly int StatusEffectId;     // TbStatusEffect 참조(런타임 데이터는 핸들러가 resolve)
        public readonly TargetMode Target;

        public StatusEffectApplyEffect(int statusEffectId, TargetMode target = TargetMode.Self)
        {
            StatusEffectId = statusEffectId;
            Target = target;
        }
    }
```

- [ ] **Step 4: 핸들러 분기 구현**

`StatusEffectApplyEffectHandler.cs` 전체를 교체:

```csharp
using System;

namespace LOP
{
    /// <summary>
    /// <see cref="StatusEffectApplyEffect"/> 핸들러(코어). Active 진입 시 효과 id를 설정으로 resolve해
    /// <see cref="StatusEffectSystem.Apply"/>. 적용된 효과는 독립 <see cref="StatusEffects"/>로 살아간다(수명 분리).
    /// <para>대상은 effect의 <see cref="TargetMode"/>가 정한다 — Self는 시전자, HitTargets는 이번 발동에서
    /// 명중한 대상 전원(넉백과 같은 on-hit 라이더).</para>
    /// <para>resolve(MasterData)는 <c>resolver</c> 델리게이트 심으로 주입 — 코어는 MasterData를 직접 참조하지 않는다.</para>
    /// </summary>
    public class StatusEffectApplyEffectHandler : AbilityEffectHandler<StatusEffectApplyEffect>
    {
        private readonly StatusEffectSystem _statusEffectSystem;
        private readonly Func<int, StatusEffectData?> _resolver;
        private readonly GameFramework.World.EntityRegistry _entityRegistry;

        public StatusEffectApplyEffectHandler(StatusEffectSystem statusEffectSystem,
                                              Func<int, StatusEffectData?> resolver,
                                              GameFramework.World.EntityRegistry entityRegistry)
        {
            _statusEffectSystem = statusEffectSystem;
            _resolver = resolver;
            _entityRegistry = entityRegistry;
        }

        protected override void OnActiveEnter(AbilityEffectContext ctx, StatusEffectApplyEffect effect)
        {
            var data = _resolver(effect.StatusEffectId);
            if (data == null)
            {
                return;
            }

            if (effect.Target == TargetMode.Self)
            {
                if (ctx.Caster != null)
                {
                    _statusEffectSystem.Apply(ctx.Caster, data.Value, ctx.Caster.Id, ctx.CurrentTick);
                }
                return;
            }

            if (ctx.HitContext == null || ctx.Caster == null)
            {
                return;
            }
            foreach (string id in ctx.HitContext.LandedTargets)
            {
                GameFramework.World.Entity target = _entityRegistry.Get(id);
                if (target != null)
                {
                    _statusEffectSystem.Apply(target, data.Value, ctx.Caster.Id, ctx.CurrentTick);
                }
            }
        }
    }
}
```

- [ ] **Step 5: 테스트 통과 확인**

UnityMCP `run_tests` (EditMode, filter `StatusEffectApplyTargetModeTests`). 기대: 4/4 PASS.
이어서 전체 LOP-Shared EditMode를 돌려 회귀가 없는지 확인한다.

- [ ] **Step 6: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git checkout -b feature/status-effect-sync
git add Runtime/Scripts/Game/Ability/AbilityEffect.cs \
        Runtime/Scripts/Game/Ability/StatusEffectApplyEffectHandler.cs \
        Tests/EditMode/StatusEffectApplyTargetModeTests.cs Tests/EditMode/StatusEffectApplyTargetModeTests.cs.meta
git commit -m "feat(ability): 상태효과 부여 대상을 TargetMode로 데이터화 — 명중자 디버프 개방"
```

---

## Task 13: `target_mode` 마스터데이터 컬럼 + provider 매핑

**Files:**
- Modify (Excel): `infrastructure/table/Datas/__beans__.xlsx` (`StatusEffectApplyEffect` bean)
- Modify (Excel): `infrastructure/table/Datas/#Ability.xlsx` (기존 haste 행에 `self` 명시)
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/AbilityDataProvider.cs:50-52`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/AbilityDataProvider.cs` (같은 위치)
- Modify: 클·서 `GameLifetimeScope.cs` — 핸들러 생성자에 `EntityRegistry` 전달

**Interfaces:**
- Consumes: `LOP.TargetMode` (Task 12)
- Produces: `LOP.MasterData.StatusEffectApplyEffect.TargetMode` (string 컬럼)

- [ ] **Step 1: bean에 컬럼 추가**

`__beans__.xlsx`에서 `StatusEffectApplyEffect` bean 정의를 찾아 `status_effect_id` 다음에
`target_mode` (타입 `string`) 컬럼을 추가한다. 먼저 파일 구조를 덤프해 정확한 행·열을 확인한다:

```python
import openpyxl
wb = openpyxl.load_workbook('Datas/__beans__.xlsx')
for name in wb.sheetnames:
    ws = wb[name]
    print('===', name)
    for i, r in enumerate(ws.iter_rows(values_only=True), 1):
        cells = [('' if c is None else str(c)) for c in r]
        while cells and cells[-1] == '':
            cells.pop()
        if cells:
            print(i, cells)
```

기존 `duration_policy`/`stack_policy`가 string 컬럼 + 런타임 `Enum.Parse` 방식이므로 동일하게 간다
(enum 타입을 새로 만들지 않는다).

- [ ] **Step 2: 기존 haste 데이터에 `Self` 명시**

`#Ability.xlsx`의 haste 행(5행) `effects` 칸(12번째 컬럼)을 바꾼다. bean에 컬럼이 하나 늘었으므로
값도 하나 늘어야 한다:

```
현재: StatusEffectApplyEffect,1
변경: StatusEffectApplyEffect,1,Self
```

- [ ] **Step 3: 재생성**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table
./gen.sh
```

- [ ] **Step 4: 클·서 provider 매핑 수정**

클·서 `AbilityDataProvider.MapEffects`의 해당 case를 바꾼다 (**두 파일 모두 동일하게**):

```csharp
                    case LOP.MasterData.StatusEffectApplyEffect s:
                        result.Add(new StatusEffectApplyEffect(
                            s.StatusEffectId,
                            (TargetMode)System.Enum.Parse(typeof(TargetMode), s.TargetMode)));
                        break;
```

- [ ] **Step 5: DI 등록 수정**

클·서 `GameLifetimeScope.cs`가 핸들러를 명시적 람다로 만들고 있으므로 세 번째 인자를 추가한다
(클라 기준 `:48-50`, 서버도 동일한 블록):

```csharp
            builder.Register<IAbilityEffectHandler>(c => new StatusEffectApplyEffectHandler(
                c.Resolve<StatusEffectSystem>(),
                id => c.Resolve<StatusEffectDataProvider>().Get(id),
                c.Resolve<GameFramework.World.EntityRegistry>()), Lifetime.Singleton);
```

- [ ] **Step 6: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure
git checkout -b feature/status-effect-sync
git add table/Datas/
git commit -m "feat(masterdata): StatusEffectApplyEffect.target_mode 컬럼"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client && git checkout -b feature/status-effect-sync && git add Runtime.Generated/ && git commit -m "chore(gen): target_mode 반영"
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server && git checkout -b feature/status-effect-sync && git add Runtime.Generated/ && git commit -m "chore(gen): target_mode 반영"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git checkout -b feature/status-effect-sync
git add Assets/Scripts/Game/AbilityDataProvider.cs Assets/Scripts/Game/GameLifetimeScope.cs
git commit -m "feat(masterdata): target_mode 매핑"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git checkout -b feature/status-effect-sync
git add Assets/Scripts/Game/AbilityDataProvider.cs Assets/Scripts/Game/GameLifetimeScope.cs
git commit -m "feat(masterdata): target_mode 매핑"
```

---

## Task 14: 슬로우 데이터 추가

**Files:**
- Modify (Excel): `infrastructure/table/Datas/#StatusEffect.xlsx`
- Modify (Excel): `infrastructure/table/Datas/#Ability.xlsx`

**Interfaces:**
- Produces: `TbStatusEffect`에 슬로우 행, `attack` 어빌리티의 effect 목록에 `StatusEffectApplyEffect(slow, HitTargets)`

- [ ] **Step 1: 슬로우 상태이상 행 추가**

`#StatusEffect.xlsx`의 haste 행(현재 5행) 아래 6행에 추가한다. 현재 haste 행은
`['', '1', 'haste', '이동속도 +30% 증가', 'Duration', '100', 'MoveSpeed', '0.3', 'PercentAdd', 'Refresh', '1']`
이므로 같은 형태로:

```python
import openpyxl
wb = openpyxl.load_workbook('Datas/#StatusEffect.xlsx')
ws = wb[wb.sheetnames[0]]
row = ['', '2', 'slow', '이동속도 -5% 감소', 'Duration', '30',
       'MoveSpeed', '-0.05', 'PercentAdd', 'Refresh', '1']
for col, value in enumerate(row, start=1):
    ws.cell(row=6, column=col, value=value)
wb.save('Datas/#StatusEffect.xlsx')
```

검증용이라 약하게 시작한다(−5%, 30틱 ≈ 1초). 밸런스는 나중에 조정.

- [ ] **Step 2: `attack` 어빌리티에 부여 효과 추가**

`#Ability.xlsx`의 `attack` 행(7행)의 `effects` 칸(12번째 컬럼)을 바꾼다.

```
현재: DamageEffect,10,2,90,KnockbackEffect,5,12,0.8
변경: DamageEffect,10,2,90,KnockbackEffect,5,12,0.8,StatusEffectApplyEffect,2,HitTargets
```

**효과 순서 주의**: `DamageEffect`가 명중자를 정하므로 **데미지 항목보다 뒤**에 와야 한다
(넉백과 같은 규칙 — on-hit 라이더는 히트 정의자 다음). 위 문자열이 그 순서를 지킨다.

`StatusEffectApplyEffect,2,HitTargets`의 `2`는 Step 1에서 만든 슬로우 id, `HitTargets`는
Task 13에서 추가한 `target_mode` 컬럼 값이다.

- [ ] **Step 3: 재생성 + 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table
./gen.sh
cd /c/Users/re5na/workspace/LOP/infrastructure
git add table/Datas/
git commit -m "feat(masterdata): 슬로우 상태이상 + attack에 명중자 부여 효과"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client && git add Runtime.Generated/ && git commit -m "chore(gen): 슬로우 데이터"
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server && git add Runtime.Generated/ && git commit -m "chore(gen): 슬로우 데이터"
```

---

## Task 15: 상태이상 와이어 — proto + 서버 채움 + 클라 반영

**Files:**
- Create: `LeagueOfPhysical-Shared/Protos/ProtoActiveEffect.proto`
- Modify: `LeagueOfPhysical-Shared/Protos/EntitySnap.proto`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/EntitySnapshotBroadcastSystem.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Netcode/EntitySnap.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/MessageHandler/GameEntityMessageHandler.cs`

**Interfaces:**
- Consumes: `LOP.StatusEffects.Effects`(`List<ActiveEffect>`), `LOP.ActiveEffect(int, long, int, string, string)`
- Produces: wire 필드 `status_effects`(11, repeated `ProtoActiveEffect`)

- [ ] **Step 1: `ProtoActiveEffect.proto` 생성**

`ProtoMotionContribution.proto`와 같은 형식(top-level 패킷 아님 — `@auto_generate` 주석 없음):

```protobuf
syntax = "proto3";

message ProtoActiveEffect
{
	int32 effect_id   = 1;
	int64 expire_tick = 2;
	int32 stack_count = 3;
}
```

- [ ] **Step 2: `EntitySnap.proto`에 목록 추가**

import 한 줄과 필드 한 줄:

```protobuf
import "ProtoActiveEffect.proto";
...
	repeated ProtoActiveEffect status_effects = 11;
```

- [ ] **Step 3: 재생성 + MessageIds 무변경 확인**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Scripts
./generate_protos.sh
cd ..
git diff --stat Runtime.Generated/Scripts/MessageIds.cs
```

기대: 출력 비어 있음.

- [ ] **Step 4: 서버가 채운다**

`BuildAllEntitySnaps`의 시전 상태 블록 아래에:

```csharp
                var statusEffects = worldEntity.Get<StatusEffects>();
                if (statusEffects != null)
                {
                    foreach (var e in statusEffects.Effects)
                    {
                        snap.StatusEffects.Add(new ProtoActiveEffect
                        {
                            EffectId = e.EffectId,
                            ExpireTick = e.ExpireTick,
                            StackCount = e.StackCount,
                        });
                    }
                }
```

- [ ] **Step 5: 클라 DTO에 필드 추가**

`Assets/Scripts/Netcode/EntitySnap.cs`의 `abilityEndTick` 아래:

```csharp
        // AutoMapper 대상 아님 — 핸들러가 수동으로 채운다(contributions와 같은 이유).
        public List<ActiveEffect> statusEffects { get; set; } = new List<ActiveEffect>();
```

- [ ] **Step 6: 클라가 원격 엔티티에 반영**

Task 9의 시전 상태 블록 아래에:

```csharp
                    StatusEffects remoteEffects = remoteEntity?.Get<StatusEffects>();
                    if (remoteEffects != null)
                    {
                        // 스냅샷이 전량 권위 — 통째로 교체한다(HP와 같은 규칙).
                        remoteEffects.Effects.Clear();
                        foreach (var pe in serverEntitySnap.StatusEffects.OrEmpty())
                        {
                            remoteEffects.Effects.Add(new ActiveEffect(
                                pe.EffectId, pe.ExpireTick, pe.StackCount,
                                sourceEntityId: null, sourceId: $"se:{pe.EffectId}"));
                        }
                    }
```

> 원격 엔티티는 클라가 시뮬하지 않으므로 스탯 모디파이어를 다시 적용하지 않는다 —
> 이동 속도 결과는 어차피 서버가 보낸 위치·속도 스냅샷에 이미 반영돼 있다.

- [ ] **Step 7: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git add Protos/ Runtime.Generated/
git commit -m "feat(wire): EntitySnap에 상태이상 목록 추가"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git add Assets/Scripts/Game/TickSystems/EntitySnapshotBroadcastSystem.cs
git commit -m "feat(snapshot): 상태이상 브로드캐스트"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Netcode/EntitySnap.cs Assets/Scripts/Game/MessageHandler/GameEntityMessageHandler.cs
git commit -m "feat(snapshot): 원격 상태이상 반영"
```

---

## Task 16: `TbStatusEffectView` + 뷰 연출 훅

실제 이펙트 에셋이 없으므로 **자리와 배선만** 만든다. 데이터가 비면 아무 일도 일어나지 않는다.

**Files:**
- Create (Excel): `infrastructure/table/Datas/#StatusEffectView.xlsx`
- Modify (Excel): `infrastructure/table/Datas/__tables__.xlsx`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Entity/LOPEntityView.cs`

**Interfaces:**
- Consumes: `LOP.StatusEffects.Effects`, `LOP.MasterData.Tables.TbStatusEffectView`
- Produces: `TbStatusEffectView` — `StatusEffectView { int Id; string VfxAddress; }`

- [ ] **Step 1: 테이블 생성 + 등록**

`#StatusEffectView.xlsx`를 Luban Excel-embedded 형식(Global Constraints의 헤더 4행)으로 만든다:

```python
import openpyxl
wb = openpyxl.Workbook()
ws = wb.active
for r in [
    ['##var',   'id',  'vfx_address'],
    ['##type',  'int', 'string'],
    ['##group', '',    ''],
    ['##',      'id',  'vfx_address'],
    ['',        '1',   ''],          # haste — 아트 미도착
    ['',        '2',   ''],          # slow  — 아트 미도착
]:
    ws.append(r)
wb.save('Datas/#StatusEffectView.xlsx')
```

`__tables__.xlsx`에 한 줄 추가 (**`group`은 `c`**):

```
['', 'TbStatusEffectView', 'StatusEffectView', 'TRUE', '#StatusEffectView.xlsx', 'id', 'map', 'c', 'StatusEffectView', '', '']
```

- [ ] **Step 2: 재생성 + 클라 전용인지 확인**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table
./gen.sh
ls ../../LeagueOfPhysical-MasterData-Server/Runtime.Generated/Scripts/MasterData/ | grep -i statuseffectview
```

기대: 서버 쪽 결과가 **비어 있음**.

- [ ] **Step 3: 뷰에 연출 훅 추가**

`LOPEntityView.cs`의 `Update`에 한 줄, 그리고 메서드 추가:

```csharp
        private void Update()
        {
            UpdateRunAnimation();
            UpdateAbilityAnimation();
            UpdateStatusEffectVfx();
        }
```

```csharp
        // 현재 붙어 있는 상태이상 VFX id 집합 — 늘어난 것만 켜고 사라진 것만 끄기 위한 것.
        private readonly System.Collections.Generic.Dictionary<int, GameObject> statusVfx =
            new System.Collections.Generic.Dictionary<int, GameObject>();

        // 상태이상 연출. 지금은 테이블의 vfx_address가 전부 비어 있어 실질 no-op —
        // 아트가 들어오면 데이터만 채우면 동작한다.
        private void UpdateStatusEffectVfx()
        {
            if (entityId == null || visualGameObject == null)
            {
                return;
            }
            var effects = entityRegistry.Get(entityId)?.Get<StatusEffects>();
            if (effects == null)
            {
                return;
            }

            var alive = new System.Collections.Generic.HashSet<int>();
            foreach (var e in effects.Effects)
            {
                alive.Add(e.EffectId);
                if (statusVfx.ContainsKey(e.EffectId))
                {
                    continue;
                }
                var row = masterData.Tables.TbStatusEffectView.GetOrDefault(e.EffectId);
                if (row == null || string.IsNullOrEmpty(row.VfxAddress))
                {
                    continue;
                }
                statusVfx[e.EffectId] = null;   // 자리 예약 — 아트 도착 시 Addressables 로드로 대체
            }

            var gone = new System.Collections.Generic.List<int>();
            foreach (var kv in statusVfx)
            {
                if (alive.Contains(kv.Key) == false)
                {
                    if (kv.Value != null)
                    {
                        Destroy(kv.Value);
                    }
                    gone.Add(kv.Key);
                }
            }
            foreach (int id in gone)
            {
                statusVfx.Remove(id);
            }
        }
```

`Cleanup()`에 정리 추가:

```csharp
            foreach (var kv in statusVfx)
            {
                if (kv.Value != null)
                {
                    Destroy(kv.Value);
                }
            }
            statusVfx.Clear();
```

- [ ] **Step 4: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure
git add table/Datas/
git commit -m "feat(masterdata): TbStatusEffectView 클라 전용 테이블"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client && git add Runtime.Generated/ && git commit -m "chore(gen): TbStatusEffectView"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Entity/LOPEntityView.cs
git commit -m "feat(view): 상태이상 연출 훅 — 데이터 비면 no-op"
```

- [ ] **Step 5: 슬라이스 3 머지 + 최종 검증**

전 저장소 슬라이스 3 브랜치를 main에 `--no-ff` 머지 후:

1. UnityMCP `refresh_unity`(클·서) → `read_console` 컴파일 에러 0
2. LOP-Shared·GameFramework 전체 EditMode 실행 → 전부 PASS
3. 인게임 (2에디터):
   - 상대를 때린다 → 상대에게 슬로우가 걸리고 **내 화면에서** 상대 이동이 느려지는지
   - 헤이스트 사용 → 이전과 동일하게 자기에게만 걸리는지 (회귀)
   - 손실 20~30% 환경에서 공격 연타 → 모션 누락 없음
   - **자족성 확인**: `GameWorldEventMessageHandler.OnWorldEventBatchToC` 본문 첫 줄에
     `return;`을 임시로 넣어 연출 이벤트 수신을 끊고 실행 → 스킬 모션이 여전히 정상 재생되면
     "늦게 들어와도 보인다"가 성립. 확인 후 `return;` 제거
   - 회귀: 걷기 / 점프 / 대시 / 피격 리액션

---

## 완료 기준

- [ ] 6개 저장소 전부 컴파일 클린
- [ ] GameFramework·LOP-Shared EditMode 전부 PASS (신규 18케이스 포함)
- [ ] 손실 20~30% 환경에서 스킬 모션 누락 0
- [ ] 연출 이벤트를 완전히 끊어도 스킬 모션이 정상 재생 (자족성)
- [ ] 상대를 때리면 상대에게 슬로우가 걸리고 내 화면에 반영
- [ ] `LOPEntityView`에 `"Plane"` 문자열과 `CueTriggers` dict가 없음
- [ ] 회귀 없음: 걷기 / 점프 / 대시 / 헤이스트 / 피격 리액션
- [ ] `docs/ROADMAP.md`에 완료 기록 추가
