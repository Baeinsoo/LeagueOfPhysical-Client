# 공격 어빌리티 이동 정책 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 공격 어빌리티에 페이즈별 이동 배율(0~1) + 점프 차단을 데이터로 부여해, 공격 중 미끄러짐을 없애고 커밋을 준다. 플레이어·AI 공통, 회전은 자유.

**Architecture:** 어빌리티 설정(`TbAbility`)에 이동배율 3칸 + `BlockJump` 추가 → `AbilityData`/`ActiveAbility` struct가 실어 나름 → 공유 `MovementSystem.Tick`이 목표 수평속도에 현재 페이즈 배율을 곱하고(전원 공통, 넉백 folding 직전) 점프를 게이트. 배율·게이트는 `(어빌리티 상태, 틱)`의 순수 함수라 클라 예측·서버 권위·재생이 같은 코드 → 라이브==재생.

**Tech Stack:** C# (Unity 6000.3), LOP-Shared 패키지(순수 시뮬 로직 + NUnit EditMode), Luban(엑셀→.cs+.bytes 마스터데이터), UnityMCP(컴파일·테스트 구동).

## Global Constraints

- **하위호환**: 새 필드 기본값 `StartupMoveScale=1f`, `ActiveMoveScale=1f`, `RecoveryMoveScale=1f`, `BlockJump=false`. 데이터 미기입 시 기존 동작 그대로.
- **배율 적용 위치**: 공유 `MovementSystem.Tick`에서 `baseHorizontal`에 곱(전원 공통), **넉백(`MotionContributions`) folding 직전**. 회전은 배율 무관(속도에만).
- **경계틱 판정**: `GetMovementMultiplier`/`IsJumpBlocked`는 페이즈 enum이 아니라 `currentTick` vs 경계틱(`StartupEndTick`/`ActiveEndTick`/`RecoveryEndTick`)으로 판정(`TryGetActiveMotionEffect`와 동일).
- **결정론**: 배율/게이트는 부수효과·랜덤 없는 순수 읽기. 캔슬(조기종료) 없음(v2).
- **`ActiveAbility.WithPhase`**: 새 4필드도 복사해야 함(페이즈 전진 시 유실 금지).
- **멀티 repo**: LOP-Shared(시뮬 코드/테스트) · infrastructure/table + MasterData-Client/Server(데이터) · LOP-Client + LOP-Server(Provider). 커밋은 각 repo에서 `git -C`.
- **`dotnet` 미설치(이 환경)**: `gen.sh`(Luban regen)는 사용자가 dotnet 있는 환경에서 실행 — Task 4 체크포인트.

## 파일 구조 (생성/수정)

| 파일 | 책임 | Task |
|---|---|---|
| `LeagueOfPhysical-Shared/Runtime/Scripts/Game/Ability/AbilityData.cs` | 설정 struct — 4필드 추가 | 1 |
| `LeagueOfPhysical-Shared/Runtime/Scripts/Game/Ability/Abilities.cs` | `ActiveAbility` struct — 4필드 + `WithPhase` | 1 |
| `LeagueOfPhysical-Shared/Runtime/Scripts/Game/Ability/AbilitySystem.cs` | `GetMovementMultiplier`/`IsJumpBlocked` + `TryActivate` 복사 | 2 |
| `LeagueOfPhysical-Shared/Tests/EditMode/AbilityMovementPolicyTests.cs` (신규) | Task2 단위 테스트 | 2 |
| `LeagueOfPhysical-Shared/Runtime/Scripts/Game/MovementSystem.cs` | `Tick` — 배율 곱 + 점프 게이트 | 3 |
| `LeagueOfPhysical-Shared/Tests/EditMode/MovementSystemTests.cs` | Task3 통합 테스트 추가 | 3 |
| `infrastructure/table/Datas/#Ability.xlsx` | Luban 스키마 4컬럼 + 데이터 | 4 |
| `LeagueOfPhysical-MasterData-{Client,Server}/Runtime.Generated/...` | regen 산출물(.cs+.bytes) | 4 |
| `LeagueOfPhysical-Client/Assets/Scripts/Game/AbilityDataProvider.cs` | `Map` 4필드 배선 | 5 |
| `LeagueOfPhysical-Server/Assets/Scripts/Game/AbilityDataProvider.cs` | `Map` 4필드 배선 | 5 |

> **EditMode 테스트 구동**: LOP-Shared 테스트는 UnityMCP로 클라 인스턴스에서 실행 — `mcp__UnityMCP__run_tests`(mode=`EditMode`, testFilter=클래스명, `unity_instance="LeagueOfPhysical-Client@<hash>"`), 결과는 `get_test_job`. 또는 Unity **Window > General > Test Runner**. 코드 수정 후 `refresh_unity`로 컴파일 확인 먼저.

---

### Task 1: `AbilityData` + `ActiveAbility`에 이동정책 4필드 (optional 파라미터)

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/Ability/AbilityData.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/Ability/Abilities.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/AbilityMovementPolicyTests.cs` (신규, Step 1)

**Interfaces:**
- Produces: `AbilityData` ctor에 optional `float startupMoveScale=1f, float activeMoveScale=1f, float recoveryMoveScale=1f, bool blockJump=false` + 동명 readonly 필드(PascalCase). `ActiveAbility` 동일 4필드 + optional ctor 파라미터. `ActiveAbility.WithPhase`가 4필드 보존.
- 기존 7-인자 호출부(테스트 다수)는 optional이라 무수정 컴파일.

- [ ] **Step 1: 실패 테스트 작성** (신규 파일)

`LeagueOfPhysical-Shared/Tests/EditMode/AbilityMovementPolicyTests.cs`:
```csharp
using GameFramework.World;
using LOP;
using NUnit.Framework;

namespace LOP.Tests
{
    public class AbilityMovementPolicyTests
    {
        private static ActiveAbility MakeActive(float su, float ac, float re, bool jump)
            => new ActiveAbility(3, AbilityPhase.Startup, 10, 100, 200, null,
                                 new AbilityEffect[0], su, ac, re, jump);

        [Test]
        public void WithPhase_PreservesMoveScalesAndBlockJump()
        {
            var a = MakeActive(0.5f, 0f, 0.3f, true).WithPhase(AbilityPhase.Active);

            Assert.That(a.Phase, Is.EqualTo(AbilityPhase.Active));
            Assert.That(a.StartupMoveScale, Is.EqualTo(0.5f));
            Assert.That(a.ActiveMoveScale, Is.EqualTo(0f));
            Assert.That(a.RecoveryMoveScale, Is.EqualTo(0.3f));
            Assert.That(a.BlockJump, Is.True);
        }

        [Test]
        public void AbilityData_DefaultMovePolicy_IsUnrestricted()
        {
            var d = new AbilityData(3, 0, 0, 0, 1, 0, new AbilityEffect[0]);

            Assert.That(d.StartupMoveScale, Is.EqualTo(1f));
            Assert.That(d.ActiveMoveScale, Is.EqualTo(1f));
            Assert.That(d.RecoveryMoveScale, Is.EqualTo(1f));
            Assert.That(d.BlockJump, Is.False);
        }
    }
}
```

- [ ] **Step 2: 컴파일 실패 확인**

`refresh_unity`(scope=scripts, compile=request, client 인스턴스) 후 `read_console`(types=error).
Expected: `ActiveAbility` ctor에 11인자 없음 / `StartupMoveScale` 등 미정의 → 컴파일 에러.

- [ ] **Step 3: `AbilityData`에 4필드 추가**

`AbilityData.cs` — 필드 블록에 `Effects` 아래 추가:
```csharp
        public readonly AbilityEffect[] Effects;
        public readonly float StartupMoveScale;
        public readonly float ActiveMoveScale;
        public readonly float RecoveryMoveScale;
        public readonly bool BlockJump;
```
ctor를 optional 파라미터로 교체:
```csharp
        public AbilityData(int abilityId, long cooldownTicks, int mpCost,
                           long startupTicks, long activeTicks, long recoveryTicks,
                           AbilityEffect[] effects,
                           float startupMoveScale = 1f, float activeMoveScale = 1f,
                           float recoveryMoveScale = 1f, bool blockJump = false)
        {
            AbilityId = abilityId;
            CooldownTicks = cooldownTicks;
            MpCost = mpCost;
            StartupTicks = startupTicks;
            ActiveTicks = activeTicks;
            RecoveryTicks = recoveryTicks;
            Effects = effects;
            StartupMoveScale = startupMoveScale;
            ActiveMoveScale = activeMoveScale;
            RecoveryMoveScale = recoveryMoveScale;
            BlockJump = blockJump;
        }
```

- [ ] **Step 4: `ActiveAbility`에 4필드 + `WithPhase` 보존**

`Abilities.cs` — `ActiveAbility` 필드에 `Effects` 아래 추가:
```csharp
        public readonly AbilityEffect[] Effects;
        public readonly float StartupMoveScale;
        public readonly float ActiveMoveScale;
        public readonly float RecoveryMoveScale;
        public readonly bool BlockJump;
```
ctor를 optional 파라미터로 교체:
```csharp
        public ActiveAbility(int abilityId, AbilityPhase phase, long startupEndTick, long activeEndTick,
                             long recoveryEndTick, Entity target, AbilityEffect[] effects,
                             float startupMoveScale = 1f, float activeMoveScale = 1f,
                             float recoveryMoveScale = 1f, bool blockJump = false)
        {
            AbilityId = abilityId;
            Phase = phase;
            StartupEndTick = startupEndTick;
            ActiveEndTick = activeEndTick;
            RecoveryEndTick = recoveryEndTick;
            Target = target;
            Effects = effects;
            StartupMoveScale = startupMoveScale;
            ActiveMoveScale = activeMoveScale;
            RecoveryMoveScale = recoveryMoveScale;
            BlockJump = blockJump;
        }
```
`WithPhase`가 4필드도 옮기도록:
```csharp
        public ActiveAbility WithPhase(AbilityPhase phase)
            => new ActiveAbility(AbilityId, phase, StartupEndTick, ActiveEndTick, RecoveryEndTick, Target, Effects,
                                 StartupMoveScale, ActiveMoveScale, RecoveryMoveScale, BlockJump);
```

- [ ] **Step 5: 테스트 통과 확인**

`refresh_unity`(compile) → `read_console`(error 0) → `run_tests`(EditMode, filter=`LOP.Tests.AbilityMovementPolicyTests`, client).
Expected: `WithPhase_PreservesMoveScalesAndBlockJump`, `AbilityData_DefaultMovePolicy_IsUnrestricted` PASS. 기존 EditMode 전부 여전히 PASS(optional이라 호출부 무영향).

- [ ] **Step 6: 커밋**

```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared" add Runtime/Scripts/Game/Ability/AbilityData.cs Runtime/Scripts/Game/Ability/Abilities.cs Tests/EditMode/AbilityMovementPolicyTests.cs Tests/EditMode/AbilityMovementPolicyTests.cs.meta
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared" commit -m "feat: AbilityData/ActiveAbility에 이동정책 4필드(배율3+BlockJump, 기본 무제한)"
```
> `.meta`는 Unity가 새 테스트 파일에 자동 생성 — refresh 후 함께 커밋.

---

### Task 2: `AbilitySystem.GetMovementMultiplier` / `IsJumpBlocked` + `TryActivate` 복사

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/Ability/AbilitySystem.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/AbilityMovementPolicyTests.cs` (Task1 파일에 추가)

**Interfaces:**
- Consumes: Task1의 `ActiveAbility` 4필드 + `AbilityData` 4필드.
- Produces: `static float AbilitySystem.GetMovementMultiplier(Entity entity, long currentTick)` (진행중 어빌리티 없으면 1f, 있으면 경계틱이 속한 페이즈 배율, RecoveryEnd 이후 1f). `static bool AbilitySystem.IsJumpBlocked(Entity entity, long currentTick)` (발동 창 안 + `BlockJump`면 true). `TryActivate`가 `AbilityData`의 4필드를 `ActiveAbility`로 복사.

- [ ] **Step 1: 실패 테스트 추가** (`AbilityMovementPolicyTests.cs`)

`MakeActive` 아래에 추가:
```csharp
        private static Entity EntityWith(ActiveAbility? active)
        {
            var e = new Entity("e1");
            var ab = new Abilities();
            ab.ActiveAbility = active;
            e.Add(ab);
            return e;
        }

        [Test]
        public void GetMovementMultiplier_NoActiveAbility_ReturnsOne()
        {
            Assert.That(AbilitySystem.GetMovementMultiplier(EntityWith(null), 5), Is.EqualTo(1f));
        }

        [Test]
        public void GetMovementMultiplier_PicksPhaseScale_ByBoundaryTick()
        {
            // 경계: startupEnd=10, activeEnd=100, recoveryEnd=200. 배율 0.5/0/0.3
            var e = EntityWith(new ActiveAbility(3, AbilityPhase.Startup, 10, 100, 200, null,
                                                 new AbilityEffect[0], 0.5f, 0f, 0.3f, false));
            Assert.That(AbilitySystem.GetMovementMultiplier(e, 5),   Is.EqualTo(0.5f), "startup");
            Assert.That(AbilitySystem.GetMovementMultiplier(e, 50),  Is.EqualTo(0f),   "active");
            Assert.That(AbilitySystem.GetMovementMultiplier(e, 150), Is.EqualTo(0.3f), "recovery");
            Assert.That(AbilitySystem.GetMovementMultiplier(e, 200), Is.EqualTo(1f),   "종료 후");
        }

        [Test]
        public void IsJumpBlocked_TrueWithinWindow_FalseAfterOrFlagOff()
        {
            var blocking = EntityWith(new ActiveAbility(3, AbilityPhase.Active, 0, 5, 10, null,
                                                        new AbilityEffect[0], 1f, 1f, 1f, blockJump: true));
            Assert.IsTrue(AbilitySystem.IsJumpBlocked(blocking, 3),  "발동 창 안");
            Assert.IsFalse(AbilitySystem.IsJumpBlocked(blocking, 10), "RecoveryEnd 이후");

            var nonBlocking = EntityWith(new ActiveAbility(3, AbilityPhase.Active, 0, 5, 10, null,
                                                           new AbilityEffect[0]));
            Assert.IsFalse(AbilitySystem.IsJumpBlocked(nonBlocking, 3), "플래그 off");
        }

        [Test]
        public void TryActivate_CopiesMovePolicy_IntoActiveAbility()
        {
            var system = new AbilitySystem(new ManaSystem());
            var e = new Entity("caster");
            e.Add(new Abilities());
            system.Grant(e, 3);
            var data = new AbilityData(3, 10, 0, 2, 3, 2, new AbilityEffect[0],
                                       0.5f, 0f, 0.3f, blockJump: true);

            Assert.IsTrue(system.TryActivate(e, data, e, 0));

            var a = e.Get<Abilities>().ActiveAbility.Value;
            Assert.That(a.ActiveMoveScale, Is.EqualTo(0f));
            Assert.That(a.RecoveryMoveScale, Is.EqualTo(0.3f));
            Assert.That(a.BlockJump, Is.True);
        }
```

- [ ] **Step 2: 컴파일 실패 확인**

`refresh_unity`(compile) → `read_console`(error): `GetMovementMultiplier`/`IsJumpBlocked` 미정의.

- [ ] **Step 3: `AbilitySystem`에 두 메서드 추가**

`AbilitySystem.cs` — `TryGetActiveMotionEffect` 아래(발동 로직 전)에 추가:
```csharp
        /// <summary>진행 중 어빌리티의 현재 페이즈 이동배율(없으면 1=자유). 경계틱으로 판정 → 시스템 실행순서 무관.</summary>
        public static float GetMovementMultiplier(Entity entity, long currentTick)
        {
            var active = entity?.Get<Abilities>()?.ActiveAbility;
            if (active == null)
            {
                return 1f;
            }
            var a = active.Value;
            if (currentTick < a.StartupEndTick)  return a.StartupMoveScale;
            if (currentTick < a.ActiveEndTick)   return a.ActiveMoveScale;
            if (currentTick < a.RecoveryEndTick) return a.RecoveryMoveScale;
            return 1f;
        }

        /// <summary>발동 창(RecoveryEnd 전) 안이고 BlockJump면 점프 차단. GetMovementMultiplier와 같은 경계틱 판정.</summary>
        public static bool IsJumpBlocked(Entity entity, long currentTick)
        {
            var active = entity?.Get<Abilities>()?.ActiveAbility;
            return active != null && active.Value.BlockJump && currentTick < active.Value.RecoveryEndTick;
        }
```

- [ ] **Step 4: `TryActivate`가 4필드 복사**

`AbilitySystem.cs` `TryActivate` — `ActiveAbility` 생성부(현재 `data.Effects`로 끝나는 줄)를 교체:
```csharp
            abilities.ActiveAbility = new ActiveAbility(data.AbilityId, AbilityPhase.Startup,
                startupEnd, activeEnd, recoveryEnd, target, data.Effects,
                data.StartupMoveScale, data.ActiveMoveScale, data.RecoveryMoveScale, data.BlockJump);
```

- [ ] **Step 5: 테스트 통과 확인**

`refresh_unity`(compile) → `read_console`(error 0) → `run_tests`(EditMode, filter=`LOP.Tests.AbilityMovementPolicyTests`, client).
Expected: 신규 4개 + Task1 2개 모두 PASS.

- [ ] **Step 6: 커밋**

```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared" add Runtime/Scripts/Game/Ability/AbilitySystem.cs Tests/EditMode/AbilityMovementPolicyTests.cs
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared" commit -m "feat: AbilitySystem 이동배율/점프차단 조회 + TryActivate 정책 복사"
```

---

### Task 3: `MovementSystem.Tick` — 배율 곱 + 점프 게이트

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/MovementSystem.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/MovementSystemTests.cs` (`MovementSystemTickTests` 클래스에 추가)

**Interfaces:**
- Consumes: Task2 `AbilitySystem.GetMovementMultiplier`/`IsJumpBlocked`.
- Produces: `MovementSystem.Tick`이 (a) `baseHorizontal`에 현재 페이즈 배율 곱(전원 공통), (b) `BlockJump` 시 점프 무시. 회전은 무영향.

- [ ] **Step 1: 실패 테스트 추가** (`MovementSystemTests.cs`의 `MovementSystemTickTests` 클래스 안, 마지막 `}` 앞)

```csharp
        // 진행 중 어빌리티(배율/점프)를 붙인 조종 엔티티. 창 [10,100) active, [100,200) recovery.
        private static void AttachAbility(GameFramework.World.Entity e, float active, bool blockJump)
        {
            var ab = new Abilities();
            ab.ActiveAbility = new ActiveAbility(3, AbilityPhase.Active, 10, 100, 200, null,
                new AbilityEffect[0], 1f, active, 1f, blockJump);
            e.Add(ab);
        }

        [Test]
        public void ActiveAbility_RootsMovement_ButKeepsRotation()
        {
            // activeMoveScale=0 → 수평 0으로 정지, 그러나 방향은 입력(오른쪽=90도)따라 살아있음.
            var entity = CreateControlledEntity(Vector3.zero, new InputCommand { Horizontal = 1f });
            AttachAbility(entity, active: 0f, blockJump: false);

            system.Tick(entity, 50, Dt);   // 50 ∈ [10,100) active

            Assert.That(entity.Get<GameFramework.World.Velocity>().Linear.ToUnity().x, Is.EqualTo(0f).Within(Tolerance), "루팅");
            Assert.That(entity.Get<GameFramework.World.Transform>().Rotation.ToUnity().eulerAngles.y, Is.EqualTo(90f).Within(Tolerance), "회전 살아있음");
        }

        [Test]
        public void ActiveAbility_PartialScale_SlowsMovement()
        {
            // activeMoveScale=0.4 → 목표 5 × 0.4 = 2.
            var entity = CreateControlledEntity(Vector3.zero, new InputCommand { Horizontal = 1f });
            AttachAbility(entity, active: 0.4f, blockJump: false);

            system.Tick(entity, 50, Dt);

            Assert.That(entity.Get<GameFramework.World.Velocity>().Linear.ToUnity().x, Is.EqualTo(2f).Within(Tolerance));
        }

        [Test]
        public void BlockJump_IgnoresJumpInput()
        {
            var entity = CreateControlledEntity(new Vector3(0f, -3f, 0f), new InputCommand { Jump = true });
            AttachAbility(entity, active: 1f, blockJump: true);

            system.Tick(entity, 50, Dt);

            Assert.That(entity.Get<GameFramework.World.Velocity>().Linear.ToUnity().y, Is.EqualTo(-3f).Within(Tolerance), "점프 무시(y 미변경)");
        }

        [Test]
        public void AI_RootsResidualVelocity_DuringAttack()
        {
            // InputBuffer 없는 AI + 잔류 속도(5,0,0) + activeMoveScale=0 → 수평 0(미끄러짐 소멸).
            var entity = new GameFramework.World.Entity("ai");
            entity.Add(new GameFramework.World.Transform());
            entity.Add(new GameFramework.World.Velocity { Linear = new Vector3(5f, 0f, 0f).ToNumerics() });
            AttachAbility(entity, active: 0f, blockJump: false);

            system.Tick(entity, 50, Dt);

            Assert.That(entity.Get<GameFramework.World.Velocity>().Linear.ToUnity().x, Is.EqualTo(0f).Within(Tolerance));
        }
```

- [ ] **Step 2: 테스트 실패 확인**

`refresh_unity`(compile) → `run_tests`(EditMode, filter=`LOP.Tests.MovementSystemTickTests`, client).
Expected: `ActiveAbility_RootsMovement_ButKeepsRotation`·`ActiveAbility_PartialScale_SlowsMovement`·`AI_RootsResidualVelocity_DuringAttack` FAIL(배율 미적용 → x가 5), `BlockJump_IgnoresJumpInput` FAIL(y가 JumpPower로 바뀜). 컴파일은 통과.

- [ ] **Step 3: 점프 게이트**

`MovementSystem.cs` `Tick` — 걷기 분기의 점프 처리(`if (input.Jump)`)를 교체:
```csharp
                    if (input.Jump && !AbilitySystem.IsJumpBlocked(entity, currentTick))
                    {
                        velocity.y = statsSystem.GetValue(stats, (int)GameFramework.World.EntityStatType.JumpPower);
                    }
```

- [ ] **Step 4: 배율 곱 (전원 공통)**

`MovementSystem.cs` `Tick` — `if (input != null) { ... }` 블록이 닫힌 **직후**, `// 외부 기여(넉백 등)` 주석/`var contributions` 줄 **바로 앞**에 추가:
```csharp
            // 공격 등 진행 중 어빌리티의 현재 페이즈 이동배율을 수평속도에 곱한다(플레이어=모터 결과, AI=잔류속도).
            // 넉백 folding 전이라 외력은 안 깎임. 회전은 위에서 이미 세팅돼 배율 무관.
            baseHorizontal *= AbilitySystem.GetMovementMultiplier(entity, currentTick);
```

- [ ] **Step 5: 테스트 통과 확인**

`refresh_unity`(compile) → `read_console`(error 0) → `run_tests`(EditMode, filter=`LOP.Tests.MovementSystemTickTests`, client).
Expected: 신규 4개 PASS. 기존 `MovementSystemTickTests`(대시·걷기·점프·넉백)·`MovementSystemTests` 전부 PASS(무 어빌리티 시 배율 1.0 → 무영향).

- [ ] **Step 6: 커밋**

```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared" add Runtime/Scripts/Game/MovementSystem.cs Tests/EditMode/MovementSystemTests.cs
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared" commit -m "feat: MovementSystem.Tick 이동배율 적용 + 점프 게이트(플레이어·AI 공통)"
```

---

### Task 4: Luban `TbAbility` 4컬럼 + 데이터 + regen

**Files:**
- Modify: `infrastructure/table/Datas/#Ability.xlsx`
- Regenerate: `LeagueOfPhysical-MasterData-Client/Runtime.Generated/{Scripts/MasterData,StreamingAssets/MasterData}`, `LeagueOfPhysical-MasterData-Server/Runtime.Generated/{...}`

**Interfaces:**
- Produces: Luban 생성 `LOP.MasterData.Ability`에 `float StartupMoveScale`, `float ActiveMoveScale`, `float RecoveryMoveScale`, `bool BlockJump` 필드(양쪽 패키지). 데이터: haste(1)/dash(2)=`1/1/1/false`, attack(3)=`0/0/0/true`(완전 정지 + 점프 차단; 손맛은 이후 재튜닝).

- [ ] **Step 1: 엑셀에 4컬럼 추가** (openpyxl — 이 환경에서 실행 가능)

```bash
cd "C:/Users/re5na/workspace/LOP/infrastructure/table" && python -c "
import openpyxl
p = 'Datas/#Ability.xlsx'
wb = openpyxl.load_workbook(p)
ws = wb['Sheet']
# effects(L=12) 뒤에 M,N,O,P 추가. row1=##var row2=##type row3=##group(빈칸=클·서 양쪽) row4=##
cols = {13:('startup_move_scale','float'), 14:('active_move_scale','float'),
        15:('recovery_move_scale','float'), 16:('block_jump','bool')}
for c,(name,typ) in cols.items():
    ws.cell(1,c,name); ws.cell(2,c,typ); ws.cell(4,c,name)   # row3(group)은 빈칸 유지
# 데이터: haste(5),dash(6)=무제한 / attack(7)=완전정지+점프차단
for r in (5,6):
    ws.cell(r,13,1); ws.cell(r,14,1); ws.cell(r,15,1); ws.cell(r,16,'false')
ws.cell(7,13,0); ws.cell(7,14,0); ws.cell(7,15,0); ws.cell(7,16,'true')
wb.save(p)
print('OK')
"
```
Expected: `OK`.

- [ ] **Step 2: 편집 검증** (덤프로 4컬럼 확인)

```bash
cd "C:/Users/re5na/workspace/LOP/infrastructure/table" && python -c "
import openpyxl
ws = openpyxl.load_workbook('Datas/#Ability.xlsx', data_only=True)['Sheet']
for r in range(1,8):
    print(r, [ws.cell(r,c).value for c in range(13,17)])
"
```
Expected: r1=`['startup_move_scale','active_move_scale','recovery_move_scale','block_jump']`, r2=`['float','float','float','bool']`, r7=`[0,0,0,'true']`, r5/r6=`[1,1,1,'false']`.

- [ ] **Step 3: Luban regen** ⚠️ **체크포인트 — `dotnet` 필요(이 환경 미설치)**

dotnet SDK가 있는 환경에서:
```bash
cd "C:/Users/re5na/workspace/LOP/infrastructure/table" && ./gen.sh
```
Expected: `[gen] target=client ...` / `[gen] target=server ...` / `[done]`, 에러 없음.
> 실패 시(bool 파싱): Step1의 `'false'`/`'true'`를 `0`/`1`로 바꿔 재실행.

- [ ] **Step 4: 생성 코드 확인**

```bash
grep -n "MoveScale\|BlockJump" "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client/Runtime.Generated/Scripts/MasterData/Ability.cs" "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server/Runtime.Generated/Scripts/MasterData/Ability.cs"
```
Expected: 양쪽 파일에 `StartupMoveScale`/`ActiveMoveScale`/`RecoveryMoveScale`(float)·`BlockJump`(bool) 필드 + ctor의 `ReadFloat`/`ReadBool`.

- [ ] **Step 5: 커밋** (3개 repo)

```bash
git -C "C:/Users/re5na/workspace/LOP/infrastructure" add table/Datas/#Ability.xlsx
git -C "C:/Users/re5na/workspace/LOP/infrastructure" commit -m "data: TbAbility 이동정책 컬럼(move_scale 3 + block_jump), attack=완전정지"
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client" add -A Runtime.Generated
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client" commit -m "regen: Ability 이동정책 필드(client)"
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server" add -A Runtime.Generated
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server" commit -m "regen: Ability 이동정책 필드(server)"
```

---

### Task 5: `AbilityDataProvider.Map` 4필드 배선 (Client + Server)

**Files:**
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/AbilityDataProvider.cs:30`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/AbilityDataProvider.cs`

**Interfaces:**
- Consumes: Task4 생성 필드 `r.StartupMoveScale`/`r.ActiveMoveScale`/`r.RecoveryMoveScale`/`r.BlockJump`, Task1 `AbilityData` optional 파라미터.
- Produces: 런타임 `AbilityData`에 엑셀 이동정책이 실림(스텁 아님).

> Task4 regen이 끝나야 `r.*MoveScale`가 존재 → 컴파일됨. Task4 뒤에 실행.

- [ ] **Step 1: 클라 `Map` 교체**

`LeagueOfPhysical-Client/Assets/Scripts/Game/AbilityDataProvider.cs` — `return new AbilityData(...)`:
```csharp
            return new AbilityData(r.Id, r.CooldownTicks, r.MpCost,
                r.StartupTicks, r.ActiveTicks, r.RecoveryTicks,
                MapEffects(r.Effects),
                r.StartupMoveScale, r.ActiveMoveScale, r.RecoveryMoveScale, r.BlockJump);
```

- [ ] **Step 2: 서버 `Map` 교체** (동일 내용)

`LeagueOfPhysical-Server/Assets/Scripts/Game/AbilityDataProvider.cs` — 위와 동일하게 교체.

- [ ] **Step 3: 양쪽 컴파일 확인**

`refresh_unity`(scope=all, force, compile, client + server 각각) → `read_console`(error 0, 각 인스턴스).
Expected: 클·서 모두 CS 에러 0.

- [ ] **Step 4: 커밋** (2개 repo)

```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" add Assets/Scripts/Game/AbilityDataProvider.cs
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" commit -m "feat: AbilityDataProvider 이동정책 배선(client)"
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" add Assets/Scripts/Game/AbilityDataProvider.cs
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" commit -m "feat: AbilityDataProvider 이동정책 배선(server)"
```

---

### Task 6: 인게임 검증 (플레이)

**Files:** 없음 (검증 체크포인트).

- [ ] **Step 1: 양쪽 에디터 준비**

`refresh_unity`(scope=all, force, client + server) → `read_console`(error 0). 서버 → 클라 순으로 Play.

- [ ] **Step 2: 플레이어 공격 검증**
  - 이동하며 공격 → **미끄러짐 없이 딱 멈춤**(attack `0/0/0`).
  - 공격 중 방향키 → **캐릭터가 회전(조준)은 됨**, 위치는 안 밀림.
  - 공격 중 점프 → **점프 안 됨**(`block_jump=true`).
  - 공격 종료 후 → 정상 이동/점프 복귀.

- [ ] **Step 3: 몬스터(AI) 공격 검증**
  - 몬스터가 접근 후 공격 진입 시 **미끄러지지 않고 정지**해서 때림.

- [ ] **Step 4: reconciliation 확인**
  - 공격 중 러버밴딩/텔레포트 없음(라이브==재생). DebugHud reconciliation distance가 공격으로 튀지 않음.

> 손맛(19틱 startup 완전정지가 답답하면) 재튜닝은 `#Ability.xlsx` attack 행 값 조정 + regen(Task4 Step1·3 반복). 예: `startup=0.5, active=0, recovery=0.3`.

---

## Self-Review

**1. Spec coverage:**
- 데이터모델(3배율+BlockJump) → Task1(struct)+Task4(테이블). ✅
- GetMovementMultiplier/IsJumpBlocked 경계틱 → Task2. ✅
- MovementSystem.Tick 배율+점프게이트 → Task3. ✅
- 회전 분리 → Task3 `ActiveAbility_RootsMovement_ButKeepsRotation`. ✅
- AI 루팅(공유틱, activeMove=0) → Task3 `AI_RootsResidualVelocity_DuringAttack` + Task6 Step3. ✅
- 넷코드 결정론(공유 시뮬 순수함수) → Task3 위치(공유 Tick) + Task6 Step4. ✅
- 하위호환(기본 1/false) → Task1 `AbilityData_DefaultMovePolicy_IsUnrestricted`, optional 파라미터. ✅
- Provider 배선(클+서) → Task5. ✅
- 대시 무영향 → 기존 `MovementSystemTickTests` 대시 테스트가 Task3 Step5에서 여전히 PASS. ✅

**2. Placeholder scan:** TBD/TODO 없음. 모든 코드/명령 구체값. dotnet 미설치는 Task4 Step3 체크포인트로 명시(placeholder 아님). ✅

**3. Type consistency:** `StartupMoveScale`/`ActiveMoveScale`/`RecoveryMoveScale`(float)·`BlockJump`(bool)이 AbilityData·ActiveAbility·생성 Ability·Provider·테스트 전부 동일. `GetMovementMultiplier(Entity,long):float`·`IsJumpBlocked(Entity,long):bool` 시그니처가 Task2 정의 = Task3 호출 일치. `ActiveAbility` 11인자 ctor 순서(…, effects, startupMoveScale, activeMoveScale, recoveryMoveScale, blockJump)가 Task1 정의 = Task2/Task3 테스트 호출 일치. ✅
