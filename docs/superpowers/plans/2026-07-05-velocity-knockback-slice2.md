# Velocity 슬라이스 2 — 넉백 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 공격에 맞으면 뒤로 밀리는 넉백을 서버 권위로 구현하고, 로컬 캐릭이 맞는 경우까지 스냅샷/wire/재조정을 완주해 러버밴딩 없이 재현한다.

**Architecture:** 넉백 = 슬라이스 1의 Additive `MotionContribution`(brake-to-desired 모터가 raw 임펄스를 지우므로) + 지수 감쇠(= 물리 drag의 순수 함수, 클·서 결정론 재생). 서버 `KnockbackEffectHandler`가 대상에 기여를 등록 → 서버 sim이 velocity에 반영 → 활성 기여를 `EntitySnap` 스냅샷에 실어 클라로 보냄 → 클라 `Reconciler`가 **스냅에서** 기여를 복원(내 예측 히스토리 아님 — 서버가 가한 것이라 클라가 예측 안 함)해 재생이 결정론적으로 재현. 원격 대상은 position 스냅샷으로 공짜.

**Tech Stack:** C# / Unity 6, VContainer(DI), Protobuf(wire, LOP-Shared), System.Numerics(코어 시뮬), NUnit(EditMode). 3 레포: **Shared**(`LeagueOfPhysical-Shared`), **Server**(`LeagueOfPhysical-Server`), **Client**(`LeagueOfPhysical-Client`).

## Global Constraints

- **브랜치:** 세 레포 모두 `feature/velocity-knockback-slice2`에서 작업(main 직접 커밋 금지). Client는 이미 생성됨.
- **네임스페이스:** 코어/시뮬 타입은 `namespace LOP`. World 타입은 항상 풀 한정 `GameFramework.World.X`(짧은 `using GameFramework.World;` 금지 — `Component` 모호성).
- **좌표 타입:** LOP-Shared 시뮬 파일(`MotionContribution`/`MotionContributionSystem`)은 `System.Numerics.Vector3`. 클·서 side 코드는 `UnityEngine.Vector3` + `.ToNumerics()`/`.ToUnity()` 변환.
- **결정론:** 감쇠는 `(초기 임펄스, StartTick, DecayPerTick, currentTick)`의 순수 함수 — 물리/전역상태 의존 금지.
- **wire:** proto 재생성 시 **MessageIds.cs의 ID가 바뀌면 안 됨**(wire break). 새 메시지 `ProtoMotionContribution`는 sent(ToC/ToS)가 아니라 sub-message라 ID 미부여 — 재생성 후 `git diff`로 ID 불변 확인(메모리 gotcha).
- **넉백 기여의 진실원본 = 서버 스냅샷.** 클라는 넉백을 예측·생성하지 않는다(서버가 가함). `PredictedAbilityState`에 넉백 기여를 넣지 말 것 — 클라 예측 히스토리는 서버 넉백을 담지 않아 복원 시 유실된다(agent가 권한 "fold into PredictedAbilityState"는 이 이유로 오답).
- **테스트 경계:** 순수 시뮬(Shared)은 EditMode TDD. 물리 targeting(OverlapSphere)·클라 `Reconciler` 재생 라운드트립은 클라 Assembly-CSharp asmdef 제약상 EditMode 불가 → **플레이 검증**(Task 8).
- **.meta:** 새 `.cs`/`.proto` 파일 생성 후 Unity가 만든 `.meta`를 함께 커밋.
- **커밋 트레일러:** 각 커밋 끝에 `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.

경로 약칭: `SHARED=C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Shared`, `SERVER=C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server`, `CLIENT=C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client`.

---

### Task 1: MotionContribution 지수 감쇠 (Shared, TDD)

Additive 기여가 활성 창 동안 `v₀ × DecayPerTick^(tick − StartTick)`로 감쇠하게 한다. `DecayPerTick=1`이면 슬라이스 1 상수 거동(무회귀).

**Files:**
- Modify: `SHARED\Runtime\Scripts\Game\MotionContribution.cs`
- Modify: `SHARED\Runtime\Scripts\Game\MotionContributionSystem.cs:16-53` (Resolve)
- Test: `SHARED\Tests\EditMode\MotionContributionSystemTests.cs`

**Interfaces:**
- Produces: `MotionContribution(Vector3 horizontal, MotionContributionMode mode, int priority, long startTick, long endTick, float decayPerTick)` (6-arg ctor) + `public readonly float DecayPerTick`. 기존 5-arg ctor 유지(decay=1).
- Produces: `MotionContributionSystem.Resolve` 동작 불변(시그니처 동일), Additive에 감쇠 적용.

- [ ] **Step 1: 감쇠 테스트 추가 (실패)**

`MotionContributionSystemTests.cs`의 클래스 안에 추가:

```csharp
[Test]
public void Resolve_AdditiveDecaysExponentially()
{
    // v0=(0,0,10), k=0.5, 창[0,100)
    var c = With(new MotionContribution(new Vector3(0, 0, 10), MotionContributionMode.Additive, 0, 0, 100, 0.5f));
    Assert.Less(Vector3.Distance(system.Resolve(Vector3.Zero, c, 0), new Vector3(0, 0, 10)), 1e-4f, "elapsed 0 → v0");
    Assert.Less(Vector3.Distance(system.Resolve(Vector3.Zero, c, 1), new Vector3(0, 0, 5)), 1e-4f, "elapsed 1 → v0*0.5");
    Assert.Less(Vector3.Distance(system.Resolve(Vector3.Zero, c, 2), new Vector3(0, 0, 2.5f)), 1e-4f, "elapsed 2 → v0*0.25");
}

[Test]
public void Resolve_DecayOne_IsConstant_NoRegression()
{
    var c = With(new MotionContribution(new Vector3(0, 0, 5), MotionContributionMode.Additive, 0, 0, 100, 1f));
    Assert.Less(Vector3.Distance(system.Resolve(Vector3.Zero, c, 0), new Vector3(0, 0, 5)), 1e-4f);
    Assert.Less(Vector3.Distance(system.Resolve(Vector3.Zero, c, 50), new Vector3(0, 0, 5)), 1e-4f, "k=1 → 상수");
}
```

- [ ] **Step 2: 컴파일 실패 확인**

Unity Test Runner(EditMode)에서 `MotionContributionSystemTests` 실행 — `MotionContribution`에 6-arg ctor 없어 **컴파일 에러**. (UnityMCP `run_tests` mode=EditMode 또는 에디터 Test Runner. 컴파일 에러면 `read_console`로 확인.)
Expected: FAIL — "No overload for method '.ctor' takes 6 arguments".

- [ ] **Step 3: MotionContribution에 DecayPerTick 추가**

`MotionContribution.cs`를 아래로 교체(필드 + 6-arg ctor + 기존 5-arg 위임):

```csharp
using System.Numerics;

namespace LOP
{
    /// <summary>이동 시스템이 base velocity 위에 얹는 기여의 합성 방식.</summary>
    public enum MotionContributionMode { Override, Additive }

    /// <summary>
    /// 이동 시스템에 얹히는 수평 velocity 기여 하나(순수 데이터). 활성 창 <c>[StartTick, EndTick)</c> 동안만 적용.
    /// Additive는 <c>DecayPerTick</c>로 매 틱 지수 감쇠(=물리 drag의 순수 함수). 산업 표준: Unreal CMC RootMotionSource.
    /// </summary>
    public readonly struct MotionContribution
    {
        public readonly Vector3 Horizontal;              // 수평(x,z) 초기 임펄스 v0; y 미사용
        public readonly MotionContributionMode Mode;
        public readonly int Priority;                    // Override 경합 시 큰 값 우선
        public readonly long StartTick;
        public readonly long EndTick;                    // 활성 = StartTick <= tick < EndTick
        public readonly float DecayPerTick;              // Additive 감쇠 계수 k(0<k<=1). 1=상수

        public MotionContribution(Vector3 horizontal, MotionContributionMode mode, int priority, long startTick, long endTick)
            : this(horizontal, mode, priority, startTick, endTick, 1f)
        {
        }

        public MotionContribution(Vector3 horizontal, MotionContributionMode mode, int priority, long startTick, long endTick, float decayPerTick)
        {
            Horizontal = horizontal;
            Mode = mode;
            Priority = priority;
            StartTick = startTick;
            EndTick = endTick;
            DecayPerTick = decayPerTick;
        }

        public bool IsActiveAt(long tick) => tick >= StartTick && tick < EndTick;
    }
}
```

- [ ] **Step 4: Resolve에 감쇠 적용**

`MotionContributionSystem.cs`의 Additive 합산 루프(현재 `sum += c.Horizontal;`, 44-50행)를 교체:

```csharp
            Vector3 sum = root;
            if (contributions != null)
            {
                foreach (var c in contributions.Items)
                {
                    if (c.Mode == MotionContributionMode.Additive && c.IsActiveAt(currentTick))
                    {
                        float factor = System.MathF.Pow(c.DecayPerTick, currentTick - c.StartTick);
                        sum += c.Horizontal * factor;
                    }
                }
            }
            return sum;
```

- [ ] **Step 5: 테스트 통과 확인**

Unity Test Runner EditMode에서 `MotionContributionSystemTests` 실행.
Expected: PASS — 신규 2개 + 기존 6개(5-arg ctor는 decay=1 위임이라 무회귀) 모두 통과.

- [ ] **Step 6: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared"
git checkout -b feature/velocity-knockback-slice2 2>/dev/null || git checkout feature/velocity-knockback-slice2
git add Runtime/Scripts/Game/MotionContribution.cs Runtime/Scripts/Game/MotionContributionSystem.cs Tests/EditMode/MotionContributionSystemTests.cs
git commit -m "feat(motion): MotionContribution 지수 감쇠(DecayPerTick) — Additive drag 곡선

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: KnockbackEffect + radial 넉백 팩토리 (Shared, TDD)

넉백 effect 데이터 타입과, 공격자→대상 방향 Additive 기여를 만드는 순수 커널을 추가한다.

**Files:**
- Modify: `SHARED\Runtime\Scripts\Game\Ability\AbilityEffect.cs` (KnockbackEffect 추가)
- Modify: `SHARED\Runtime\Scripts\Game\MotionContributionSystem.cs` (static CreateRadialKnockback 추가)
- Test: `SHARED\Tests\EditMode\MotionContributionSystemTests.cs`

**Interfaces:**
- Produces: `public sealed class KnockbackEffect : AbilityEffect` with `float Strength, float Range, float Angle, int DurationTicks, float DecayPerTick` and ctor `KnockbackEffect(float strength, float range, float angle, int durationTicks, float decayPerTick)`.
- Produces: `public static MotionContribution MotionContributionSystem.CreateRadialKnockback(Vector3 attackerPos, Vector3 targetPos, float strength, int durationTicks, float decayPerTick, long currentTick)` — Additive, dir=수평(target−attacker) 정규화×strength, 창 `[currentTick, currentTick+durationTicks)`.

- [ ] **Step 1: 팩토리 테스트 추가 (실패)**

`MotionContributionSystemTests.cs`에 추가(정적 메서드라 `system` 불필요):

```csharp
[Test]
public void CreateRadialKnockback_PushesAwayFromAttacker()
{
    // attacker (0,0,0), target (3,0,4) → 방향 (0.6,0,0.8), strength 10 → (6,0,8)
    var c = MotionContributionSystem.CreateRadialKnockback(
        Vector3.Zero, new Vector3(3, 0, 4), strength: 10f, durationTicks: 12, decayPerTick: 0.8f, currentTick: 5);
    Assert.Less(Vector3.Distance(c.Horizontal, new Vector3(6, 0, 8)), 1e-4f, "radial away × strength");
    Assert.AreEqual(MotionContributionMode.Additive, c.Mode);
    Assert.AreEqual(5, c.StartTick);
    Assert.AreEqual(17, c.EndTick, "start + duration");
    Assert.AreEqual(0.8f, c.DecayPerTick, 1e-6f);
}

[Test]
public void CreateRadialKnockback_IgnoresY()
{
    // 높이 차가 있어도 수평만 — target y는 무시
    var c = MotionContributionSystem.CreateRadialKnockback(
        new Vector3(0, 5, 0), new Vector3(3, 0, 4), 10f, 12, 0.8f, 0);
    Assert.Less(Vector3.Distance(c.Horizontal, new Vector3(6, 0, 8)), 1e-4f);
}

[Test]
public void CreateRadialKnockback_SamePosition_ZeroPush()
{
    var c = MotionContributionSystem.CreateRadialKnockback(
        new Vector3(2, 0, 2), new Vector3(2, 0, 2), 10f, 12, 0.8f, 0);
    Assert.Less(c.Horizontal.Length(), 1e-4f, "겹친 위치 → 0(NaN 방지)");
}
```

- [ ] **Step 2: 컴파일 실패 확인**

Test Runner EditMode 실행. Expected: FAIL — `CreateRadialKnockback` 미정의 컴파일 에러.

- [ ] **Step 3: CreateRadialKnockback 구현**

`MotionContributionSystem.cs`의 클래스 안(예: Resolve 아래)에 추가:

```csharp
        /// <summary>공격자→대상 방향으로 미는 Additive 넉백 기여 하나(순수 커널 — 서버 핸들러/테스트 공용). y는 무시.</summary>
        public static MotionContribution CreateRadialKnockback(
            Vector3 attackerPos, Vector3 targetPos, float strength, int durationTicks, float decayPerTick, long currentTick)
        {
            Vector3 away = new Vector3(targetPos.X - attackerPos.X, 0f, targetPos.Z - attackerPos.Z);
            Vector3 dir = away.LengthSquared() > 1e-8f ? Vector3.Normalize(away) : Vector3.Zero;
            return new MotionContribution(dir * strength, MotionContributionMode.Additive, 0,
                currentTick, currentTick + durationTicks, decayPerTick);
        }
```

- [ ] **Step 4: KnockbackEffect 추가**

`AbilityEffect.cs`의 `MotionEffect` 아래에 추가:

```csharp
    /// <summary>대상을 공격자 반대 방향으로 민다(넉백). 서버 핸들러가 부채꼴 대상을 찾아 Additive 기여를 등록.
    /// 클라엔 핸들러 미등록 → 클라는 스냅샷으로 결과를 받음(데미지처럼 서버권위).</summary>
    public sealed class KnockbackEffect : AbilityEffect
    {
        public readonly float Strength;      // 초기 세기 v0
        public readonly float Range;         // 판정 반경
        public readonly float Angle;         // 부채꼴 전체 각(도)
        public readonly int DurationTicks;   // 밀림 지속(활성 창)
        public readonly float DecayPerTick;  // 지수 감쇠(0<k<=1)

        public KnockbackEffect(float strength, float range, float angle, int durationTicks, float decayPerTick)
        {
            Strength = strength;
            Range = range;
            Angle = angle;
            DurationTicks = durationTicks;
            DecayPerTick = decayPerTick;
        }
    }
```

- [ ] **Step 5: 테스트 통과 확인**

Test Runner EditMode `MotionContributionSystemTests` 실행. Expected: PASS(신규 3 + 기존 전부).

- [ ] **Step 6: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared"
git add Runtime/Scripts/Game/Ability/AbilityEffect.cs Runtime/Scripts/Game/MotionContributionSystem.cs Tests/EditMode/MotionContributionSystemTests.cs
git commit -m "feat(ability): KnockbackEffect + CreateRadialKnockback 순수 팩토리

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: 서버 — 넉백 핸들러 + 컴포넌트 부여 + 발동 배선

서버가 공격 Active 진입 시 부채꼴 대상에 넉백 기여를 등록한다. `MotionContributions` 컴포넌트를 엔티티에 부여하고, TEMP로 공격(DamageEffect)에 넉백을 얹는다.

**Files:**
- Create: `SERVER\Assets\Scripts\Game\KnockbackEffectHandler.cs`
- Modify: `SERVER\Assets\Scripts\Game\GameLifetimeScope.cs:43` (DI 등록)
- Modify: `SERVER\Assets\Scripts\EntityCreator\CharacterCreator.cs` (~91: MotionContributions 부여)
- Modify: `SERVER\Assets\Scripts\Game\AbilityDataProvider.cs:53-55` (TEMP 발동 배선)

**Interfaces:**
- Consumes: `KnockbackEffect`, `MotionContributionSystem.CreateRadialKnockback(...)`, `MotionContributions` (Task 1·2).
- Consumes: `DamageEffectHandler` 패턴(OverlapSphere+IsInAttackSector), `AbilityEffectContext { Caster, CurrentTick, EntityManager }`, `GameFramework.World.EntityRegistry.Get(entityId)`.
- Produces: 서버 엔티티에 `MotionContributions` 컴포넌트 존재; `KnockbackEffectHandler`가 `IAbilityEffectHandler`로 DI 등록.

> 물리 targeting은 EditMode 불가(콜라이더 필요) → 이 태스크는 컴파일 + Task 8 플레이로 검증. 넉백 방향·감쇠 수학은 Task 2에서 이미 단위 테스트됨.

- [ ] **Step 1: KnockbackEffectHandler 작성**

`SERVER\Assets\Scripts\Game\KnockbackEffectHandler.cs` 생성(`DamageEffectHandler` 이식):

```csharp
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// <see cref="KnockbackEffect"/> 핸들러(서버 전용). Active 진입 시 1회, 시전자 앞 부채꼴 대상을
    /// 공격자 반대 방향으로 미는 Additive 기여를 대상 <see cref="MotionContributions"/>에 등록한다.
    /// 클라 미등록 → executor가 KnockbackEffect를 무시(클라는 스냅샷으로 결과 수신). entityRegistry로 대상
    /// side→World 매핑(combatSystem처럼 DI, 어빌리티 그래프와 무관).
    /// </summary>
    public class KnockbackEffectHandler : AbilityEffectHandler<KnockbackEffect>
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public KnockbackEffectHandler(GameFramework.World.EntityRegistry entityRegistry)
        {
            this.entityRegistry = entityRegistry;
        }

        protected override void OnActiveEnter(AbilityEffectContext ctx, KnockbackEffect effect)
        {
            var attacker = ctx.EntityManager.GetEntity<LOPEntity>(ctx.Caster.Id);
            if (attacker == null)
            {
                return;
            }

            LayerMask layerMask = LayerMask.GetMask("Default");
            Collider[] hits = Physics.OverlapSphere(attacker.position, effect.Range, layerMask);
            foreach (var hit in hits)
            {
                if (hit.transform.name == "Plane")
                {
                    continue;
                }
                if (IsInAttackSector(attacker, hit.transform.position, effect.Range, effect.Angle) == false)
                {
                    continue;
                }

                var target = hit.transform.parent?.parent?.GetComponentInChildren<LOPEntity>();
                if (target == null || target.entityId == attacker.entityId)
                {
                    continue;
                }

                var contributions = entityRegistry.Get(target.entityId)?.Get<MotionContributions>();
                if (contributions == null)
                {
                    continue;
                }

                contributions.Items.Add(MotionContributionSystem.CreateRadialKnockback(
                    attacker.position.ToNumerics(), target.position.ToNumerics(),
                    effect.Strength, effect.DurationTicks, effect.DecayPerTick, ctx.CurrentTick));
            }
        }

        // 시전자 정면 부채꼴(전체 각 angle도) 안이고 range 이내인지. DamageEffectHandler.IsInAttackSector 이식.
        private static bool IsInAttackSector(LOPEntity attacker, Vector3 targetPosition, float range, float angle)
        {
            Vector3 toTarget = targetPosition - attacker.position;
            if (toTarget.magnitude > range)
            {
                return false;
            }
            Vector3 forward = Quaternion.Euler(attacker.rotation) * Vector3.forward;
            float dot = Vector3.Dot(forward.normalized, toTarget.normalized);
            float targetAngle = Mathf.Acos(dot) * Mathf.Rad2Deg;
            return targetAngle <= (angle * 0.5f);
        }
    }
}
```

- [ ] **Step 2: DI 등록**

`SERVER\Assets\Scripts\Game\GameLifetimeScope.cs`의 `DamageEffectHandler` 등록(43행) 바로 아래에 추가:

```csharp
            builder.Register<KnockbackEffectHandler>(Lifetime.Singleton).As<IAbilityEffectHandler>();
```

- [ ] **Step 3: 서버 creator에 MotionContributions 부여**

`SERVER\Assets\Scripts\EntityCreator\CharacterCreator.cs`의 `worldEntity.Add(new Abilities());`(~91행) 바로 위 또는 아래에 추가:

```csharp
            worldEntity.Add(new MotionContributions());
```

- [ ] **Step 4: TEMP 발동 배선 — 공격에 넉백 얹기**

`SERVER\Assets\Scripts\Game\AbilityDataProvider.cs`의 `case LOP.MasterData.DamageEffect d:` 블록(53-55행)을 교체:

```csharp
                    case LOP.MasterData.DamageEffect d:
                        result.Add(new DamageEffect(d.Amount, d.Range, d.Angle));
                        // TEMP(슬라이스2): 공격에 넉백을 얹는다. MasterData KnockbackEffect 승격은 후속.
                        result.Add(new KnockbackEffect(strength: 8f, range: d.Range, angle: d.Angle,
                            durationTicks: 12, decayPerTick: 0.8f));
                        break;
```

- [ ] **Step 5: 컴파일 확인**

서버 Unity에서 `refresh_unity` 후 `read_console`로 컴파일 에러 없음 확인(UnityMCP, `unity_instance`는 서버 인스턴스 — 이 태스크는 서버 대상이므로 사용자 확인 후 서버 인스턴스로).
Expected: 컴파일 성공, 에러 0.

- [ ] **Step 6: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git checkout -b feature/velocity-knockback-slice2 2>/dev/null || git checkout feature/velocity-knockback-slice2
git add Assets/Scripts/Game/KnockbackEffectHandler.cs Assets/Scripts/Game/KnockbackEffectHandler.cs.meta Assets/Scripts/Game/GameLifetimeScope.cs Assets/Scripts/EntityCreator/CharacterCreator.cs Assets/Scripts/Game/AbilityDataProvider.cs
git commit -m "feat(knockback): 서버 KnockbackEffectHandler + MotionContributions 부여 + TEMP 발동

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 4: 와이어 — ProtoMotionContribution + EntitySnap 필드 (Shared proto)

로컬 캐릭 상태 스냅샷(`EntitySnap`)에 활성 넉백 기여 목록을 실을 수 있게 proto를 확장한다.

**Files:**
- Create: `SHARED\Protos\ProtoMotionContribution.proto`
- Modify: `SHARED\Protos\EntitySnap.proto`
- Regenerate: `SHARED\Runtime.Generated\Scripts\Protobuf\*` (스크립트)

**Interfaces:**
- Produces: `ProtoMotionContribution` proto message { `Horizontal`(ProtoVector3), `Mode`(int32), `Priority`(int32), `StartTick`(int64), `EndTick`(int64), `DecayPerTick`(float) }.
- Produces: `EntitySnap.MotionContributions` (repeated ProtoMotionContribution, field 7).

- [ ] **Step 1: ProtoMotionContribution.proto 생성**

`SHARED\Protos\ProtoMotionContribution.proto`:

```proto
syntax = "proto3";
import "ProtoVector3.proto";

message ProtoMotionContribution
{
	ProtoVector3 horizontal = 1;
	int32 mode = 2;
	int32 priority = 3;
	int64 start_tick = 4;
	int64 end_tick = 5;
	float decay_per_tick = 6;
}
```

- [ ] **Step 2: EntitySnap.proto에 필드 추가**

`SHARED\Protos\EntitySnap.proto`를 교체:

```proto
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
}
```

- [ ] **Step 3: MessageIds 스냅샷(재생성 전)**

재생성이 ID를 흔들지 않았는지 대조할 기준을 남긴다:

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared"
cp Runtime.Generated/Scripts/MessageIds.cs /tmp/MessageIds.before.cs
```

- [ ] **Step 4: proto 재생성**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Scripts"
./generate_protos.sh
```
Expected: "All proto-related scripts executed successfully." + `Runtime.Generated/Scripts/Protobuf/ProtoMotionContribution.cs`, 갱신된 `EntitySnap.cs` 생성.

- [ ] **Step 5: MessageIds 불변 확인 (wire break 방지)**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared"
diff /tmp/MessageIds.before.cs Runtime.Generated/Scripts/MessageIds.cs
```
Expected: **차이 없음**(ProtoMotionContribution은 sent 메시지가 아니라 ID 미부여, EntitySnap 필드 추가는 ID 무관). 차이가 나면 중단하고 원인 조사(메모리 gotcha — sent 메시지 ID가 밀리면 wire break).

- [ ] **Step 6: Shared 컴파일 확인**

Shared를 참조하는 Unity(클 또는 서)에서 `refresh_unity` + `read_console`. Expected: 컴파일 성공(생성 코드 유효).

- [ ] **Step 7: 커밋 (생성물 .meta 포함)**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared"
git add Protos/ProtoMotionContribution.proto Protos/ProtoMotionContribution.proto.meta Protos/EntitySnap.proto Runtime.Generated/Scripts/Protobuf/
git commit -m "feat(wire): ProtoMotionContribution + EntitySnap.motion_contributions

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 5: 서버 — EntitySnap에 활성 기여 채우기

서버가 각 엔티티의 World `MotionContributions`를 스냅샷에 직렬화한다.

**Files:**
- Modify: `SERVER\Assets\Scripts\Entity\LOPEntityManager.cs:170-189` (GetAllEntitySnaps)

**Interfaces:**
- Consumes: `EntitySnap.MotionContributions` (Task 4), World `MotionContributions.Items` (Task 3에서 부여됨), `entityRegistry.Get(entity.entityId)`.
- Produces: 활성 기여가 있는 엔티티의 스냅에 `ProtoMotionContribution` 목록 포함.

- [ ] **Step 1: GetAllEntitySnaps 수정**

`LOPEntityManager.cs`의 `GetAllEntitySnaps()` 루프 본문(174-186행)을 교체:

```csharp
            foreach (var entity in GetEntities().OrEmpty())
            {
                var worldEntity = entityRegistry.Get(entity.entityId);
                GameFramework.World.Health health = worldEntity?.Get<GameFramework.World.Health>();
                var snap = new EntitySnap
                {
                    EntityId = entity.entityId,
                    Position = MapperConfig.mapper.Map<ProtoVector3>(entity.position),
                    Rotation = MapperConfig.mapper.Map<ProtoVector3>(entity.rotation),
                    Velocity = MapperConfig.mapper.Map<ProtoVector3>(entity.velocity),
                    MaxHP = health?.Max ?? 0,
                    CurrentHP = health?.Current ?? 0,
                };

                var contributions = worldEntity?.Get<MotionContributions>();
                if (contributions != null)
                {
                    foreach (var c in contributions.Items)
                    {
                        snap.MotionContributions.Add(new ProtoMotionContribution
                        {
                            Horizontal = new ProtoVector3 { X = c.Horizontal.X, Y = c.Horizontal.Y, Z = c.Horizontal.Z },
                            Mode = (int)c.Mode,
                            Priority = c.Priority,
                            StartTick = c.StartTick,
                            EndTick = c.EndTick,
                            DecayPerTick = c.DecayPerTick,
                        });
                    }
                }

                entitySnapList.Add(snap);
            }
```

- [ ] **Step 2: 컴파일 확인**

서버 Unity `refresh_unity` + `read_console`. Expected: 에러 0.

- [ ] **Step 3: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git add Assets/Scripts/Entity/LOPEntityManager.cs
git commit -m "feat(wire): 서버가 EntitySnap에 활성 MotionContributions 직렬화

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 6: 클라 — 스냅샷 수신 매핑 + 컴포넌트 부여

클라가 스냅에서 넉백 기여를 읽어 `EntitySnap` 모델에 담고, 로컬 엔티티에 `MotionContributions` 컴포넌트를 부여한다.

**Files:**
- Modify: `CLIENT\Assets\Scripts\Model\EntitySnap.cs` (contributions 필드)
- Modify: `CLIENT\Assets\Scripts\Game\MessageHandler\Game.Entity.MessageHandler.cs:38-55` (수신 매핑)
- Modify: `CLIENT\Assets\Scripts\EntityCreator\CharacterCreator.cs:103-104` (MotionContributions 부여)

**Interfaces:**
- Consumes: `EntitySnap.MotionContributions` proto(Task 4), `MotionContribution`/`MotionContributionMode`(Shared).
- Produces: 클라 모델 `EntitySnap.contributions` (List<MotionContribution>) 채워짐; 로컬 엔티티에 World `MotionContributions` 존재.

- [ ] **Step 1: 클라 EntitySnap 모델에 contributions 추가**

`CLIENT\Assets\Scripts\Model\EntitySnap.cs`를 교체:

```csharp
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;

namespace LOP
{
    public class EntitySnap
    {
        public long tick { get; set; }
        public string entityId { get; set; }
        public UnityEngine.Vector3 position { get; set; }
        public UnityEngine.Vector3 rotation { get; set; }
        public UnityEngine.Vector3 velocity { get; set; }
        public double timestamp { get; set; }

        // 서버 권위 외부 이동 기여(넉백 등). AutoMapper 대상 아님 — 핸들러가 수동으로 채운다.
        public List<MotionContribution> contributions { get; set; } = new List<MotionContribution>();
    }
}
```

> `MotionContribution.Horizontal`은 `System.Numerics.Vector3`라 `using System.Numerics;` 추가. 기존 `UnityEngine.Vector3` 프로퍼티는 풀 한정으로 모호성 제거.

- [ ] **Step 2: 수신 핸들러에서 기여 매핑**

`CLIENT\Assets\Scripts\Game\MessageHandler\Game.Entity.MessageHandler.cs`의 `OnEntitySnapsToC`에서 `entitySnap`을 만든 뒤(현재 `entitySnap.timestamp = ...` 직후, `reconciler.AddServerSnap` 이전) 기여를 채운다. 해당 블록을 교체:

```csharp
        EntitySnap entitySnap = MapperConfig.mapper.Map<EntitySnap>(serverEntitySnap);
        entitySnap.tick = entitySnapsToC.Tick;
        entitySnap.timestamp = entitySnapsToC.Tick * gameDataStore.gameInfo.Interval;

        entitySnap.contributions.Clear();
        foreach (var pc in serverEntitySnap.MotionContributions.OrEmpty())
        {
            entitySnap.contributions.Add(new MotionContribution(
                new System.Numerics.Vector3(pc.Horizontal.X, pc.Horizontal.Y, pc.Horizontal.Z),
                (MotionContributionMode)pc.Mode, pc.Priority, pc.StartTick, pc.EndTick, pc.DecayPerTick));
        }
```

> `AutoMapper.Map<EntitySnap>`는 `contributions`를 건드리지 않는다(맵 미설정) — 파라미터 없는 ctor의 필드 이니셜라이저로 빈 리스트가 만들어져 있고, 위에서 Clear+채움. 원격 엔티티도 이 매핑을 거치지만 `ServerStateReconciler` 경로는 contributions를 읽지 않으므로 무해.

- [ ] **Step 3: 클라 creator에 MotionContributions 부여**

`CLIENT\Assets\Scripts\EntityCreator\CharacterCreator.cs`의 `worldEntity.Add(new Abilities());`(103행) 근처에 추가:

```csharp
            worldEntity.Add(new MotionContributions());
```

- [ ] **Step 4: 컴파일 확인**

클라 Unity `refresh_unity` + `read_console`(CLAUDE.md대로 `unity_instance`=클라 인스턴스 명시). Expected: 에러 0.

- [ ] **Step 5: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git add Assets/Scripts/Model/EntitySnap.cs "Assets/Scripts/Game/MessageHandler/Game.Entity.MessageHandler.cs" Assets/Scripts/EntityCreator/CharacterCreator.cs
git commit -m "feat(knockback): 클라 스냅에서 MotionContributions 수신 + 컴포넌트 부여

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 7: 클라 — Reconciler가 스냅에서 넉백 기여 복원

재조정 하드 복원 시, 넉백 기여를 **서버 스냅에서** 로컬 World 엔티티에 복원한다(내 예측 히스토리 아님). 재생 루프의 `MovementSystem.Tick`이 결정론 감쇠로 재현한다.

**Files:**
- Modify: `CLIENT\Assets\Scripts\Entity\Reconciler.cs:82-95` (하드 복원 블록)

**Interfaces:**
- Consumes: `snap.contributions`(Task 6), World `MotionContributions`(Task 6에서 로컬 엔티티에 부여), 기존 `worldEntity`.
- Produces: 재조정 후 로컬 엔티티 `MotionContributions`가 앵커 틱 서버 상태로 세팅됨 → 재생이 넉백 재현.

- [ ] **Step 1: 하드 복원에 기여 복원 추가**

`Reconciler.cs`의 하드 복원 블록(82-86행, `entity.velocity = snap.velocity;` … `Physics.SyncTransforms();`) 직후, `predictedAbilityStateHistory.TryGet` 이전에 삽입:

```csharp
            entity.position = snap.position;
            entity.rotation = snap.rotation;
            entity.velocity = snap.velocity;
            entity.PushMotionToPhysics();
            Physics.SyncTransforms();

            // 넉백 등 외부 이동 기여는 서버 권위 → 스냅에서 복원한다. 내 예측 히스토리(PredictedAbilityState)엔
            // 없다: 서버가 가한 것이라 클라가 예측·생성하지 않기 때문. position/velocity와 같은 권위 축.
            var motionContributions = worldEntity.Get<MotionContributions>();
            if (motionContributions != null)
            {
                motionContributions.Items.Clear();
                motionContributions.Items.AddRange(snap.contributions);
            }
```

> 재생 루프(109-140행)는 무변경 — `movementSystem.Tick(worldEntity, t, deltaTime)`이 내부에서 `MotionContributionSystem.Prune/Resolve`를 부르므로 복원된 기여가 매 재생 틱에 결정론적으로 감쇠·적용된다. errorGate가 스킵하면(위치 오차 < 6cm) 복원도 스킵되지만, 넉백은 위치를 벌리므로 곧 게이트가 열리고 이후 스냅이 기여를 계속 실어(자가치유) 반영된다.

- [ ] **Step 2: 컴파일 확인**

클라 Unity `refresh_unity` + `read_console`(`unity_instance`=클라). Expected: 에러 0.

- [ ] **Step 3: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git add Assets/Scripts/Entity/Reconciler.cs
git commit -m "feat(knockback): Reconciler가 서버 스냅에서 MotionContributions 복원

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 8: 끝-끝 플레이 검증

넉백이 로컬/원격 대상 모두에서 러버밴딩 없이 재현되고, 무회귀임을 확인한다. (물리 targeting·재조정 라운드트립은 클라 asmdef 제약상 EditMode 불가 → 플레이가 검증 도구.)

**Files:** (없음 — 관측/검증)

- [ ] **Step 1: 서버·클라 에디터 기동 + 로컬 룸 접속**

서버 에디터 Play → 클라 에디터 Play → 로컬 룸 접속(메모리 [local-test-auth-fixture]: DB 리셋 시 서버 하드코딩 playerList uuid에 현재 게스트 uuid 추가 후 서버 재시작 필요).
Expected: 캐릭터 스폰, 이동/공격 정상.

- [ ] **Step 2: 원격 대상 넉백 (공짜 경로)**

내 캐릭이 다른 엔티티(AI/더미)를 공격 → 그 엔티티가 뒤로 밀렸다가 감쇠하며 멈추는지 육안 확인(원격은 position 스냅 보간).
Expected: 대상이 공격 방향으로 밀림, 부드럽게 감속 정지.

- [ ] **Step 3: 로컬 대상 넉백 (재조정 경로) — RTT 주입**

AI/다른 플레이어가 **내 캐릭**을 공격하게 만든다(AI가 공격하는 상황 재현). Mirror LatencySimulation으로 RTT 50/150/300ms 주입하며 각 조건에서:
- 내 캐릭이 밀리는지, **러버밴딩(위치 튐/되돌아옴) 없이** 부드럽게 밀리는지.
- 밀리는 동안 걷기 입력이 부분적으로 먹히는지(Additive).
Expected: 세 RTT 모두 넉백 재현 + 러버밴딩 육안 소멸. (DebugHud reconciliation distance가 넉백 순간 튀었다가 수렴.)

- [ ] **Step 4: 무회귀 확인**

걷기, 정지(칼정지), 점프, 대시(지상/공중/방향전환)를 슬라이스 1과 동일하게 체감하는지 — 넉백 인프라가 기존 이동을 건드리지 않았는지.
Expected: 이동/대시 전부 무회귀.

- [ ] **Step 5: 결과 기록**

`docs\superpowers\specs\2026-07-05-velocity-knockback-slice2-design.md` 하단에 "구현 후기"를 추가(대시 재생 slice 후기 형식) — 검증 결과·수치·남은 이슈. 커밋.

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git add docs/superpowers/specs/2026-07-05-velocity-knockback-slice2-design.md
git commit -m "docs(velocity): 슬라이스 2 넉백 구현 후기 + 플레이 검증 결과

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## 통합 / 머지

세 레포(Shared→Server/Client 의존)를 각자 `feature/velocity-knockback-slice2`에서 작업했다. 순서: Shared 먼저(Task 1·2·4), 그 위에서 Server(Task 3·5)·Client(Task 6·7) 컴파일. 플레이 검증(Task 8) 통과 후 각 레포를 main에 `--no-ff` 머지(superpowers:finishing-a-development-branch로 마무리). Shared는 file: 참조라 클·서가 로컬 폴더를 직접 봄 — 브랜치 상태로 검증 가능.

## Out of Scope (spec과 동일)

- Override 완전 경직/CC, Y축(수직) 넉백, 넉백 중 충돌 반사.
- 축 B(Rigidbody→World.Entity velocity 권위, 4e keystone).
- MasterData `KnockbackEffect` 승격(현재 TEMP 코드 배선) — 후속.
- Damage↔Knockback targeting 공유(현재 각자 OverlapSphere).
