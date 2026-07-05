# Velocity Motor 단일 권위 + 기여 모델 (슬라이스 1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `MovementSystem`을 velocity 유일 writer로 만든다 — 대시는 이동 시스템이 `ActiveAbility`에서 파생(CMC movement-mode 방식), 외부 힘은 `MotionContributions`(Additive) 리스트로. `MotionEffectHandler`의 velocity 직접 쓰기를 제거하고, 동작·재조정은 현재와 동일(무회귀). 넉백은 슬라이스 2.

**Architecture:** 순수 코어(LOP-Shared)에 기여 모델(`MotionContribution`/`MotionContributions`/`MotionContributionSystem`)을 추가한다. `MovementSystem.Tick`에 `currentTick`을 더해 대시를 활성 창 `[StartupEndTick, ActiveEndTick)`으로 파생(`AbilitySystem.TryGetActiveMotionEffect`)하고, 외부 Additive 기여를 `MotionContributionSystem.Resolve`로 합성해 `World.Velocity`에 한 번만 쓴다. 리스트는 슬라이스 1엔 엔티티에 안 붙는다(이동 시스템 null-safe; 첫 실사용=슬라이스 2 넉백).

**Tech Stack:** Unity, C#, NUnit(EditMode), 순수 C# World Core(GameFramework.World / LOP-Shared), VContainer.

## Global Constraints

- **브랜치 (main 직접 커밋 금지):** 클라 = `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client`(브랜치 `feature/velocity-motor-contribution`, 이미 존재). LOP-Shared·서버는 각 레포에서 동명 브랜치 생성(Preflight).
- **레포 경계:** 기여 모델·`MovementSystem`·`AbilitySystem`·`LOPWorld`·`MotionEffectHandler`·EditMode 테스트 = **LOP-Shared**. `Reconciler`·클라 `GameLifetimeScope` = **LOP-Client**. 서버 `GameLifetimeScope` = **LOP-Server**.
- **테스트 배치:** 순수 C#(LOP-Shared)만 EditMode. 클라/서버 코드는 Assembly-CSharp라 asmdef 참조 불가 → **컴파일 + 플레이 검증**.
- **테스트/컴파일 실행:** UnityMCP, 반드시 클라 인스턴스 지정 `unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4"`(서버 에디터도 연결돼 있으니 절대 서버 타깃 금지; 해시 실패 시 `mcpforunity://instances`에서 name=`LeagueOfPhysical-Client` 재해석). `run_tests`는 `filter` 대신 `group_names`(정규화 클래스명) 사용. `.cs` 편집 후 `refresh_unity` → `read_console`로 컴파일 완료·에러 0 확인 후 테스트.
- **.meta 커밋:** 새 `.cs`마다 Unity 생성 `.meta` 함께 add. 직접 만들지 말 것.
- **Vector3 규약:** `MotionContribution`/`MotionContributionSystem`은 **`System.Numerics.Vector3`**(World 컴포넌트 순수 규약, `Velocity`와 동일). `MovementSystem`은 `UnityEngine.Vector3` 내부 사용 → `ToNumerics()`/`ToUnity()`(GameFramework)로 변환.
- **Anemic:** 컴포넌트에 로직 없음. 해소/프루닝은 `MotionContributionSystem`(순수 static, `MovementSystem.ProcessMovement` 커널과 같은 결).
- **파리티:** 대시(창 `[StartupEnd, ActiveEnd)`, `forward×Speed`, 입력 락, Y 보존)·걷기·점프·정지가 리팩터 전후 동일.
- **커밋 trailer:** 각 커밋 끝 `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`. 클·서의 무관 dirty 파일(Assets/Art, UIRoot.prefab, GraphicsSettings.asset)은 절대 add 금지.

### Preflight (코드 변경 전 1회)

- [ ] LOP-Shared·서버에 브랜치 생성:
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared" && git checkout -b feature/velocity-motor-contribution
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" && git checkout -b feature/velocity-motor-contribution
```

---

### Task 1: `MotionContribution` + `MotionContributions` 컴포넌트 (LOP-Shared)

기여 한 개(값+모드+우선순위+활성 창)와 그 리스트를 담는 순수 데이터.

**Files:**
- Create: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/MotionContribution.cs`
- Create: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/MotionContributions.cs`
- Test: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Tests/EditMode/MotionContributionTests.cs`

**Interfaces:**
- Produces: `enum MotionContributionMode { Override, Additive }`; `struct MotionContribution(System.Numerics.Vector3 horizontal, MotionContributionMode mode, int priority, long startTick, long endTick)` with fields `Horizontal/Mode/Priority/StartTick/EndTick` and `bool IsActiveAt(long tick)`; `class MotionContributions : Component { List<MotionContribution> Items { get; } }`.

- [ ] **Step 1: 실패 테스트 작성**

`Tests/EditMode/MotionContributionTests.cs`:
```csharp
using System.Numerics;
using NUnit.Framework;

namespace LOP.Tests
{
    public class MotionContributionTests
    {
        [Test]
        public void IsActiveAt_WithinWindow_True_BoundariesHalfOpen()
        {
            var c = new MotionContribution(new Vector3(1, 0, 0), MotionContributionMode.Override, 0, 10, 20);
            Assert.IsFalse(c.IsActiveAt(9), "start 이전");
            Assert.IsTrue(c.IsActiveAt(10), "start 포함");
            Assert.IsTrue(c.IsActiveAt(19));
            Assert.IsFalse(c.IsActiveAt(20), "end 제외(half-open)");
        }

        [Test]
        public void Component_HoldsItems()
        {
            var comp = new MotionContributions();
            Assert.AreEqual(0, comp.Items.Count);
            comp.Items.Add(new MotionContribution(new Vector3(2, 0, 0), MotionContributionMode.Additive, 1, 0, 5));
            Assert.AreEqual(1, comp.Items.Count);
            Assert.AreEqual(MotionContributionMode.Additive, comp.Items[0].Mode);
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

Run: `run_tests(mode="EditMode", group_names=["LOP.Tests.MotionContributionTests"], unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")`
Expected: 컴파일 에러(`MotionContribution`/`MotionContributions` 미정의).

- [ ] **Step 3: 최소 구현**

`Runtime/Scripts/Game/MotionContribution.cs`:
```csharp
using System.Numerics;

namespace LOP
{
    /// <summary>이동 시스템이 base velocity 위에 얹는 기여의 합성 방식.</summary>
    public enum MotionContributionMode { Override, Additive }

    /// <summary>
    /// 이동 시스템에 얹히는 수평 velocity 기여 하나(순수 데이터). 활성 창 <c>[StartTick, EndTick)</c> 동안만 적용.
    /// 산업 표준: Unreal CMC RootMotionSource(AccumulateMode Override/Additive + Priority + Duration).
    /// </summary>
    public readonly struct MotionContribution
    {
        public readonly Vector3 Horizontal;              // 수평(x,z); y 미사용
        public readonly MotionContributionMode Mode;
        public readonly int Priority;                    // Override 경합 시 큰 값 우선
        public readonly long StartTick;
        public readonly long EndTick;                    // 활성 = StartTick <= tick < EndTick

        public MotionContribution(Vector3 horizontal, MotionContributionMode mode, int priority, long startTick, long endTick)
        {
            Horizontal = horizontal;
            Mode = mode;
            Priority = priority;
            StartTick = startTick;
            EndTick = endTick;
        }

        public bool IsActiveAt(long tick) => tick >= StartTick && tick < EndTick;
    }
}
```

`Runtime/Scripts/Game/MotionContributions.cs`:
```csharp
using System.Collections.Generic;
using GameFramework.World;

namespace LOP
{
    /// <summary>엔티티에 얹힌 외부 이동 기여(넉백·외력) 컬렉션(데이터 컴포넌트). 프루닝/해소는 <see cref="MotionContributionSystem"/>.</summary>
    public class MotionContributions : Component
    {
        public List<MotionContribution> Items { get; } = new List<MotionContribution>();
    }
}
```

- [ ] **Step 4: 테스트 통과 확인** — `refresh_unity` → `read_console`(에러 0) → `run_tests(... group_names=["LOP.Tests.MotionContributionTests"] ...)` → PASS(2).

- [ ] **Step 5: 커밋** (LOP-Shared)
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared" && git add Runtime/Scripts/Game/MotionContribution.cs Runtime/Scripts/Game/MotionContribution.cs.meta Runtime/Scripts/Game/MotionContributions.cs Runtime/Scripts/Game/MotionContributions.cs.meta Tests/EditMode/MotionContributionTests.cs Tests/EditMode/MotionContributionTests.cs.meta && git commit -m "feat(velocity): MotionContribution + MotionContributions component

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: `MotionContributionSystem` — Prune/Resolve (LOP-Shared)

base 수평 + 활성 기여를 모드/우선순위로 합성하는 순수 static 유틸.

**Files:**
- Create: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/MotionContributionSystem.cs`
- Test: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Tests/EditMode/MotionContributionSystemTests.cs`

**Interfaces:**
- Consumes: `MotionContribution`, `MotionContributions`(Task 1).
- Produces: `static void MotionContributionSystem.Prune(MotionContributions contributions, long currentTick)`; `static System.Numerics.Vector3 MotionContributionSystem.Resolve(System.Numerics.Vector3 baseHorizontal, MotionContributions contributions, long currentTick)`.

- [ ] **Step 1: 실패 테스트 작성**

`Tests/EditMode/MotionContributionSystemTests.cs`:
```csharp
using System.Numerics;
using NUnit.Framework;

namespace LOP.Tests
{
    public class MotionContributionSystemTests
    {
        private static MotionContributions With(params MotionContribution[] items)
        {
            var c = new MotionContributions();
            c.Items.AddRange(items);
            return c;
        }

        [Test]
        public void Resolve_NoContributions_ReturnsBase()
        {
            var v = MotionContributionSystem.Resolve(new Vector3(3, 0, 4), null, 0);
            Assert.AreEqual(new Vector3(3, 0, 4), v);
        }

        [Test]
        public void Resolve_ActiveOverride_ReplacesBase()
        {
            var c = With(new MotionContribution(new Vector3(9, 0, 0), MotionContributionMode.Override, 0, 0, 10));
            var v = MotionContributionSystem.Resolve(new Vector3(1, 0, 0), c, 5);
            Assert.AreEqual(new Vector3(9, 0, 0), v);
        }

        [Test]
        public void Resolve_HighestPriorityOverrideWins()
        {
            var c = With(
                new MotionContribution(new Vector3(9, 0, 0), MotionContributionMode.Override, 0, 0, 10),
                new MotionContribution(new Vector3(5, 0, 0), MotionContributionMode.Override, 7, 0, 10));
            var v = MotionContributionSystem.Resolve(new Vector3(1, 0, 0), c, 5);
            Assert.AreEqual(new Vector3(5, 0, 0), v, "priority 7 > 0");
        }

        [Test]
        public void Resolve_AdditivesSumOnTopOfOverride()
        {
            var c = With(
                new MotionContribution(new Vector3(9, 0, 0), MotionContributionMode.Override, 0, 0, 10),
                new MotionContribution(new Vector3(0, 0, 2), MotionContributionMode.Additive, 0, 0, 10),
                new MotionContribution(new Vector3(0, 0, 3), MotionContributionMode.Additive, 0, 0, 10));
            var v = MotionContributionSystem.Resolve(new Vector3(1, 0, 0), c, 5);
            Assert.AreEqual(new Vector3(9, 0, 5), v, "override(9,0,0) + additives(0,0,5)");
        }

        [Test]
        public void Resolve_InactiveContributions_Ignored()
        {
            var c = With(new MotionContribution(new Vector3(9, 0, 0), MotionContributionMode.Override, 0, 0, 10));
            var v = MotionContributionSystem.Resolve(new Vector3(1, 0, 0), c, 20); // 창 밖
            Assert.AreEqual(new Vector3(1, 0, 0), v, "창 밖 기여 무시 → base");
        }

        [Test]
        public void Prune_RemovesExpired_KeepsActive()
        {
            var c = With(
                new MotionContribution(Vector3.Zero, MotionContributionMode.Additive, 0, 0, 10),   // end 10
                new MotionContribution(Vector3.Zero, MotionContributionMode.Additive, 0, 0, 30));   // end 30
            MotionContributionSystem.Prune(c, 10);   // 10 >= 10 만료 / 10 < 30 유지
            Assert.AreEqual(1, c.Items.Count);
            Assert.AreEqual(30, c.Items[0].EndTick);
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인** — `run_tests(... group_names=["LOP.Tests.MotionContributionSystemTests"] ...)` → 컴파일 에러(`MotionContributionSystem` 미정의).

- [ ] **Step 3: 최소 구현**

`Runtime/Scripts/Game/MotionContributionSystem.cs`:
```csharp
using System.Numerics;

namespace LOP
{
    /// <summary>
    /// 이동 기여의 프루닝/해소(순수 static — 상태 없음, <see cref="MovementSystem.ProcessMovement"/> 커널과 같은 결).
    /// 합성 규칙(CMC/Mover 표준): 최고 우선순위 활성 Override가 base를 대체하고, 활성 Additive는 그 위에 가산.
    /// </summary>
    public static class MotionContributionSystem
    {
        public static void Prune(MotionContributions contributions, long currentTick)
        {
            contributions?.Items.RemoveAll(c => currentTick >= c.EndTick);
        }

        public static Vector3 Resolve(Vector3 baseHorizontal, MotionContributions contributions, long currentTick)
        {
            Vector3 root = baseHorizontal;

            if (contributions != null)
            {
                bool hasOverride = false;
                int bestPriority = int.MinValue;
                Vector3 overrideValue = Vector3.Zero;
                foreach (var c in contributions.Items)
                {
                    if (c.Mode == MotionContributionMode.Override && c.IsActiveAt(currentTick) &&
                        (!hasOverride || c.Priority > bestPriority))
                    {
                        hasOverride = true;
                        bestPriority = c.Priority;
                        overrideValue = c.Horizontal;
                    }
                }
                if (hasOverride)
                {
                    root = overrideValue;
                }
            }

            Vector3 sum = root;
            if (contributions != null)
            {
                foreach (var c in contributions.Items)
                {
                    if (c.Mode == MotionContributionMode.Additive && c.IsActiveAt(currentTick))
                    {
                        sum += c.Horizontal;
                    }
                }
            }
            return sum;
        }
    }
}
```

- [ ] **Step 4: 통과 확인** — refresh → console(에러 0) → `run_tests(... group_names=["LOP.Tests.MotionContributionSystemTests"] ...)` → PASS(6).

- [ ] **Step 5: 커밋** (LOP-Shared)
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared" && git add Runtime/Scripts/Game/MotionContributionSystem.cs Runtime/Scripts/Game/MotionContributionSystem.cs.meta Tests/EditMode/MotionContributionSystemTests.cs Tests/EditMode/MotionContributionSystemTests.cs.meta && git commit -m "feat(velocity): MotionContributionSystem — Prune/Resolve (Override replaces + Additive sums)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: `AbilitySystem.TryGetActiveMotionEffect` 창 검사 헬퍼 (LOP-Shared)

이동 시스템이 대시를 파생할 수 있게, `ActiveAbility`의 경계틱 창 `[StartupEndTick, ActiveEndTick)` 안이면 그 `MotionEffect`를 돌려주는 static 헬퍼. (기존 `HasActiveMotionEffect`는 페이즈 기반 — 입력 락용으로 유지; 이건 창 기반 — 이동 시스템용, 전이 틱 파리티.)

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/Ability/AbilitySystem.cs` (`HasActiveMotionEffect` 아래 static 추가)
- Test: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Tests/EditMode/ActiveMotionEffectWindowTests.cs`

**Interfaces:**
- Consumes: `Abilities`, `ActiveAbility`(`StartupEndTick`/`ActiveEndTick`/`Effects`), `MotionEffect`.
- Produces: `static bool AbilitySystem.TryGetActiveMotionEffect(GameFramework.World.Entity entity, long currentTick, out MotionEffect motionEffect)`.

- [ ] **Step 1: 실패 테스트 작성**

`Tests/EditMode/ActiveMotionEffectWindowTests.cs`:
```csharp
using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    public class ActiveMotionEffectWindowTests
    {
        private static Entity DasherWithWindow(long startupEnd, long activeEnd)
        {
            var e = new Entity("d");
            var abilities = new Abilities();
            // Phase는 무관(창 기반 판정) — 일부러 Startup으로 둬 전이-틱 파리티를 검증.
            abilities.ActiveAbility = new ActiveAbility(2, AbilityPhase.Startup, startupEnd, activeEnd, activeEnd + 5,
                null, new AbilityEffect[] { new MotionEffect(15f) });
            e.Add(abilities);
            return e;
        }

        [Test]
        public void InsideWindow_ReturnsMotionEffect_IgnoringPhase()
        {
            var e = DasherWithWindow(10, 20);
            Assert.IsTrue(AbilitySystem.TryGetActiveMotionEffect(e, 10, out var me), "start 포함(전이 틱)");
            Assert.AreEqual(15f, me.Speed);
            Assert.IsTrue(AbilitySystem.TryGetActiveMotionEffect(e, 19, out _));
        }

        [Test]
        public void OutsideWindow_False()
        {
            var e = DasherWithWindow(10, 20);
            Assert.IsFalse(AbilitySystem.TryGetActiveMotionEffect(e, 9, out _), "start 이전");
            Assert.IsFalse(AbilitySystem.TryGetActiveMotionEffect(e, 20, out _), "end 제외");
        }

        [Test]
        public void NoActiveAbility_False()
        {
            var e = new Entity("x");
            e.Add(new Abilities());
            Assert.IsFalse(AbilitySystem.TryGetActiveMotionEffect(e, 5, out _));
        }
    }
}
```

- [ ] **Step 2: 실패 확인** — `run_tests(... group_names=["LOP.Tests.ActiveMotionEffectWindowTests"] ...)` → 컴파일 에러(미정의).

- [ ] **Step 3: 구현** — `AbilitySystem.cs`의 `HasActiveMotionEffect` 메서드 **바로 아래**에 추가:
```csharp
        /// <summary>
        /// 활성 창 <c>[StartupEndTick, ActiveEndTick)</c> 안이면 진행 중 어빌리티의 <see cref="MotionEffect"/>를 돌려준다.
        /// 페이즈가 아니라 경계틱으로 판정 → 대시 전이 틱에도 same-tick 파생(이동 시스템이 대시를 파생할 때 사용).
        /// </summary>
        public static bool TryGetActiveMotionEffect(Entity entity, long currentTick, out MotionEffect motionEffect)
        {
            motionEffect = null;
            var active = entity?.Get<Abilities>()?.ActiveAbility;
            if (active == null)
            {
                return false;
            }
            var a = active.Value;
            if (currentTick < a.StartupEndTick || currentTick >= a.ActiveEndTick || a.Effects == null)
            {
                return false;
            }
            foreach (var effect in a.Effects)
            {
                if (effect is MotionEffect me)
                {
                    motionEffect = me;
                    return true;
                }
            }
            return false;
        }
```

- [ ] **Step 4: 통과 확인** — refresh → console(에러 0) → `run_tests(... group_names=["LOP.Tests.ActiveMotionEffectWindowTests"] ...)` → PASS(3).

- [ ] **Step 5: 커밋** (LOP-Shared)
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared" && git add Runtime/Scripts/Game/Ability/AbilitySystem.cs Tests/EditMode/ActiveMotionEffectWindowTests.cs Tests/EditMode/ActiveMotionEffectWindowTests.cs.meta && git commit -m "feat(velocity): AbilitySystem.TryGetActiveMotionEffect (window-based, for motor dash derivation)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 4: `MovementSystem.Tick` 시그니처에 `currentTick` 추가 — 배선만 (LOP-Shared + 클라)

`Tick(entity, deltaTime)` → `Tick(entity, currentTick, deltaTime)`. **동작 무변경**(currentTick 아직 미사용). 시그니처 파급을 로직과 분리해 안전 증분.

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/MovementSystem.cs:64` (메서드 시그니처)
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/LOPWorld.cs:27` (호출)
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Tests/EditMode/MovementSystemTests.cs` (모든 `system.Tick(entity, Dt)` 호출)
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Entity/Reconciler.cs` (재생 루프의 `movementSystem.Tick` 호출)

**Interfaces:**
- Produces: `void MovementSystem.Tick(GameFramework.World.Entity entity, long currentTick, float deltaTime)` (currentTick 미사용, 다음 태스크에서 사용).

- [ ] **Step 1: MovementSystem.Tick 시그니처 변경** — 본문은 그대로.

기존 (`MovementSystem.cs:64`):
```csharp
        public void Tick(GameFramework.World.Entity entity, float deltaTime)
```
변경 후:
```csharp
        public void Tick(GameFramework.World.Entity entity, long currentTick, float deltaTime)
```

- [ ] **Step 2: 호출처 갱신 (LOP-Shared LOPWorld)** — `LOPWorld.cs:27`
```csharp
                _movementSystem.Tick(entity, deltaTime);
```
→
```csharp
                _movementSystem.Tick(entity, tick, deltaTime);
```

- [ ] **Step 3: 호출처 갱신 (LOP-Shared 테스트)** — `MovementSystemTests.cs`의 `MovementSystemTickTests` 안 모든 `system.Tick(entity, Dt)`(6곳: 라인 130,141,152,163,174,191)를 `system.Tick(entity, 0, Dt)`로 바꾼다. (currentTick=0; 대시 테스트 `ActiveMotionEffect_SkipsMovement`의 ActiveAbility 창은 `[0,100)`이라 tick 0에서도 현행 페이즈-기반 게이트로 동일 동작 — 이 태스크는 무변경 확인이 목적.)

- [ ] **Step 4: 호출처 갱신 (클라 Reconciler)** — `Reconciler.cs` 재생 루프의 이동 호출:
```csharp
                movementSystem.Tick(worldEntity, deltaTime);
```
→
```csharp
                movementSystem.Tick(worldEntity, t, deltaTime);
```
(`t`는 재생 루프의 틱 변수. 재생 루프 시그니처는 `for (long t = anchorTick + 1; t < currentTick; t++)`.)

- [ ] **Step 5: 컴파일 + 기존 테스트 무변경 확인** — `refresh_unity` → `read_console`(에러 0, 양쪽) → 기존 MovementSystem 테스트 그대로 통과:
Run: `run_tests(mode="EditMode", group_names=["LOP.Tests.MovementSystemTests","LOP.Tests.MovementSystemTickTests"], unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")`
Expected: 기존 개수 전부 PASS(동작 무변경).

- [ ] **Step 6: 커밋** (양쪽 레포)
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared" && git add Runtime/Scripts/Game/MovementSystem.cs Runtime/Scripts/Game/LOPWorld.cs Tests/EditMode/MovementSystemTests.cs && git commit -m "refactor(velocity): thread currentTick through MovementSystem.Tick (no behavior change)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" && git add Assets/Scripts/Entity/Reconciler.cs && git commit -m "refactor(velocity): pass replay tick to MovementSystem.Tick (no behavior change)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 5: `MovementSystem.Tick` 단일 writer 로직 — 대시 파생 + 기여 합성 (LOP-Shared)

이동 시스템을 유일 writer로: 대시면 `forward×Speed`(파생 Override, 입력 락), 아니면 걷기; 그 위에 외부 Additive 기여 합성. `HasActiveMotionEffect` bow-out 제거.

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/MovementSystem.cs` (`Tick` 본문)
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Tests/EditMode/MovementSystemTests.cs` (대시 테스트 갱신 + 기여 합성 테스트 추가)

**Interfaces:**
- Consumes: `AbilitySystem.TryGetActiveMotionEffect`(Task 3), `MotionContributionSystem.Resolve/Prune`(Task 2), `MotionContributions`(Task 1).
- Produces: `MovementSystem.Tick`가 velocity 유일 writer(대시 파생 + Additive 합성).

- [ ] **Step 1: 대시 단위 테스트 갱신 + 기여 합성 테스트 작성** — `MovementSystemTickTests`의 `ActiveMotionEffect_SkipsMovement`를 아래로 **교체**하고 두 테스트 추가.

교체 (기존 `ActiveMotionEffect_SkipsMovement`, `MovementSystemTests.cs:181-194`):
```csharp
        [Test]
        public void ActiveMotionEffect_DerivesDashVelocity_FromFacing_IgnoresInput()
        {
            // 대시 활성 창 안 → 이동 시스템이 forward×Speed를 직접 쓴다(입력 무시). 기본 rotation=identity → forward=+z.
            var entity = CreateControlledEntity(new Vector3(15f, 0f, 0f), new InputCommand { Vertical = 1f });
            var abilities = new Abilities();
            abilities.ActiveAbility = new ActiveAbility(2, AbilityPhase.Startup, 0, 100, 200, null,
                new AbilityEffect[] { new MotionEffect(15f) });   // 창 [0,100)
            entity.Add(abilities);

            system.Tick(entity, 0, Dt);   // 0 ∈ [0,100)

            Vector3 v = entity.Get<GameFramework.World.Velocity>().Linear.ToUnity();
            Assert.That(v.x, Is.EqualTo(0f).Within(Tolerance), "입력 무시(락)");
            Assert.That(v.z, Is.EqualTo(15f).Within(Tolerance), "forward(+z)×15");
        }

        [Test]
        public void OutsideDashWindow_WalksNormally()
        {
            // 창 밖 tick이면 대시 아님 → 걷기(입력대로).
            var entity = CreateControlledEntity(Vector3.zero, new InputCommand { Horizontal = 1f });
            var abilities = new Abilities();
            abilities.ActiveAbility = new ActiveAbility(2, AbilityPhase.Startup, 0, 5, 10, null,
                new AbilityEffect[] { new MotionEffect(15f) });   // 창 [0,5)
            entity.Add(abilities);

            system.Tick(entity, 5, Dt);   // 5 ∉ [0,5) → 걷기

            Assert.That(entity.Get<GameFramework.World.Velocity>().Linear.ToUnity().x, Is.EqualTo(5f).Within(Tolerance));
        }

        [Test]
        public void AdditiveContribution_AddsOnTopOfWalk()
        {
            // 외부 Additive 기여(넉백류)는 걷기 위에 가산된다.
            var entity = CreateControlledEntity(Vector3.zero, new InputCommand { Horizontal = 1f }); // 걷기 → x=5
            var contribs = new MotionContributions();
            contribs.Items.Add(new MotionContribution(new System.Numerics.Vector3(0f, 0f, 4f),
                MotionContributionMode.Additive, 0, 0, 10));
            entity.Add(contribs);

            system.Tick(entity, 0, Dt);

            Vector3 v = entity.Get<GameFramework.World.Velocity>().Linear.ToUnity();
            Assert.That(v.x, Is.EqualTo(5f).Within(Tolerance), "걷기 x=5");
            Assert.That(v.z, Is.EqualTo(4f).Within(Tolerance), "additive z=4 가산");
        }
```

- [ ] **Step 2: 실패 확인** — `run_tests(... group_names=["LOP.Tests.MovementSystemTickTests"] ...)` → 새 대시/합성 테스트 FAIL(아직 bow-out 로직 — 대시 velocity 안 씀, additive 미합성).

- [ ] **Step 3: `MovementSystem.Tick` 본문 재작성** — 기존 `Tick` 본문(`MovementSystem.cs:64-107`, 시그니처는 Task 4에서 이미 `(entity, currentTick, deltaTime)`)을 아래로 교체:
```csharp
        public void Tick(GameFramework.World.Entity entity, long currentTick, float deltaTime)
        {
            var buffer = entity.Get<InputBuffer>();
            if (buffer == null)
            {
                return;   // 입력 비조종(AI/원격/아이템) — 버퍼 없음
            }

            var input = buffer.Current;
            if (input == null)
            {
                return;   // 이번 틱 확정된 커맨드 없음
            }

            var worldVelocity = entity.Get<GameFramework.World.Velocity>();
            Vector3 velocity = worldVelocity.Linear.ToUnity();   // Y 보존용

            Vector3 baseHorizontal;
            if (AbilitySystem.TryGetActiveMotionEffect(entity, currentTick, out var motion))
            {
                // 대시(파생 Override): 바라보는 방향으로 speed. 입력 무시(락) + 회전 미변경 + 점프 무시(현행 bow-out과 동일).
                Vector3 forward = entity.Get<GameFramework.World.Transform>().Rotation.ToUnity() * Vector3.forward;
                baseHorizontal = new Vector3(forward.x, 0f, forward.z).normalized * motion.Speed;
            }
            else
            {
                var stats = entity.Get<GameFramework.World.Stats>();
                float speed = statsSystem.GetValue(stats, (int)GameFramework.World.EntityStatType.MoveSpeed);
                var result = ProcessMovement(new MovementInput(
                    velocity, input.Horizontal, input.Vertical, speed, MaxAcceleration, deltaTime));
                baseHorizontal = new Vector3(result.velocity.x, 0f, result.velocity.z);
                if (input.Jump)
                {
                    velocity.y = statsSystem.GetValue(stats, (int)GameFramework.World.EntityStatType.JumpPower);
                }
                if (result.hasRotation)
                {
                    entity.Get<GameFramework.World.Transform>().Rotation = Quaternion.Euler(result.rotation).ToNumerics();
                }
            }

            // 외부 기여(Additive; 슬라이스1엔 인스턴스 없음 — null-safe) 합성. 만료 기여는 프루닝.
            var contributions = entity.Get<MotionContributions>();
            MotionContributionSystem.Prune(contributions, currentTick);
            Vector3 finalHorizontal = MotionContributionSystem
                .Resolve(baseHorizontal.ToNumerics(), contributions, currentTick).ToUnity();

            velocity.x = finalHorizontal.x;
            velocity.z = finalHorizontal.z;
            worldVelocity.Linear = velocity.ToNumerics();
        }
```

- [ ] **Step 4: 통과 확인** — refresh → console(에러 0) → 전체 이동 테스트:
Run: `run_tests(mode="EditMode", group_names=["LOP.Tests.MovementSystemTests","LOP.Tests.MovementSystemTickTests"], unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")`
Expected: 전부 PASS(기존 걷기/점프/정지 무변경 + 새 대시 파생/창밖 걷기/additive 합성). **주의:** 이 시점엔 `MotionEffectHandler`(Task 6서 제거)가 아직 살아 있어 대시 velocity를 한 번 더(같은 값) 덮어써도 무해(idempotent) — 순수 테스트엔 executor를 안 돌리므로 영향 없음.

- [ ] **Step 5: 커밋** (LOP-Shared)
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared" && git add Runtime/Scripts/Game/MovementSystem.cs Tests/EditMode/MovementSystemTests.cs && git commit -m "feat(velocity): MovementSystem is sole velocity writer (dash derived + additive contributions)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 6: `MotionEffectHandler` 제거 + DI 등록 제거 (LOP-Shared + 클라 + 서버)

대시 velocity는 이제 이동 시스템이 파생하므로 핸들러는 불필요.

**Files:**
- Delete: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/Ability/MotionEffectHandler.cs` (+ `.meta`)
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Game/GameLifetimeScope.cs:45` (등록 제거)
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Game/GameLifetimeScope.cs:37` (등록 제거)

**Interfaces:**
- Consumes: Task 5(이동 시스템이 대시 파생). Produces: `MotionEffectHandler` 부재.

- [ ] **Step 1: 클라 DI 등록 제거** — `GameLifetimeScope.cs:45`
```csharp
            builder.Register<MotionEffectHandler>(Lifetime.Singleton).As<IAbilityEffectHandler>();
```
이 줄 삭제. (다른 핸들러 `StatusEffectApplyEffectHandler` 등록은 유지.)

- [ ] **Step 2: 서버 DI 등록 제거** — 서버 `GameLifetimeScope.cs:37`의 동일 줄 삭제.

- [ ] **Step 3: `MotionEffectHandler.cs` 삭제** (LOP-Shared)
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared" && git rm Runtime/Scripts/Game/Ability/MotionEffectHandler.cs Runtime/Scripts/Game/Ability/MotionEffectHandler.cs.meta
```

- [ ] **Step 4: 컴파일 확인 (양쪽 에디터)** — `refresh_unity(scope=all, unity_instance=...)` 후 `read_console`로 **클라·서버 모두 컴파일 에러 0** 확인. (file: 패키지 `.cs` 삭제는 stale CS2001이 뜰 수 있어 scope=all refresh 필요 — [[deleting-package-files-cs2001]].) `MotionEffectHandler` 참조가 남아있지 않은지(등록 2곳만 있었음) 확인.

- [ ] **Step 5: 커밋** (3개 레포)
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared" && git commit -m "refactor(velocity): remove MotionEffectHandler (dash now derived by motor)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" && git add Assets/Scripts/Game/GameLifetimeScope.cs && git commit -m "refactor(velocity): drop MotionEffectHandler registration (client)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" && git add Assets/Scripts/Game/GameLifetimeScope.cs && git commit -m "refactor(velocity): drop MotionEffectHandler registration (server)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 7: 플레이 무회귀 검증 (클라 + 서버 — 수동)

**Files:** (코드 변경 없음)

- [ ] **Step 1: 클·서 실행** — 서버 + 클라 에디터 플레이(로컬 2-에디터).
- [ ] **Step 2: 대시 파리티** — 대시(어빌리티 id 2)를 지상/공중/방향전환에서 발동. 확인: 속도·방향·거리·입력 락·전이 틱 시작이 **리팩터 전과 동일**(러버밴딩·끊김 없음).
- [ ] **Step 3: 무회귀** — 걷기/방향전환(드리프트 없음)/점프/정지(칼정지)가 이전과 동일. 서버 AI 이동 정상.
- [ ] **Step 4: 재조정 무회귀** — RTT 주입(LatencySimulation) + 재조정 중 대시가 이전(ability-replay 슬라이스)과 동일하게 재현(러버밴딩 없음).
- [ ] **Step 5: 콘솔** — 클·서 `read_console` 에러 0.
- [ ] **Step 6: 결과 기록** — spec 하단 "구현 후기"에 한 단락(대시 파리티·무회귀). 코드 변경 없으므로 spec 갱신 커밋과 함께.

---

## 마무리 (모든 태스크 후)

- [ ] 3개 레포 브랜치 push + 각 `main` `--no-ff` 머지(사용자 확인 후, 함께).
- [ ] 슬라이스 2(넉백 + Additive 실사용 + 스냅샷/wire + 재조정) 별도 brainstorm→spec→plan.

## 자기 검토 메모 (작성자)

- **스펙 커버리지:** 모델(§A)=T1/T2. 대시 파생(§C)=T3+T5. 순서/시그니처(§D)=T4+T5. `MotionEffectHandler` 제거=T6. 파리티/무회귀=T5 테스트+T7. 리스트는 슬라이스1에 엔티티 미부착(§C/영향파일)=의도적(이동 시스템 null-safe, T5 additive 테스트는 합성 컴포넌트로 검증).
- **파리티 근거:** 대시 창 `[StartupEnd, ActiveEnd)`·`forward×Speed`·입력락·Y보존·점프무시가 현 bow-out+handler 넷 동작과 동일(§C). T5 대시 테스트가 값 고정.
- **idempotent 인터림:** T5(이동 시스템이 대시 씀) 후 T6(핸들러 제거) 전까지 핸들러가 같은 값 재기록 — 무해. 순수 EditMode엔 executor 미구동이라 무영향.
- **타입 일관성:** `Tick(entity, long currentTick, float deltaTime)`, `TryGetActiveMotionEffect(entity, currentTick, out MotionEffect)`, `MotionContributionSystem.Resolve/Prune`(System.Numerics), `MotionContribution`(System.Numerics) — 태스크 간 일치 확인.
