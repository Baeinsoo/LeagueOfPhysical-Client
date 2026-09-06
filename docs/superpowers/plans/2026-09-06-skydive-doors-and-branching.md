# Skydive 여닫이 문과 갈림길 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 선반마다 구멍을 둘로 늘리고 그중 하나에 여닫이 문을 달아, 매 칸에 "다이브로 달려들까 대자로 돌아갈까"의 선택을 만든다.

**Architecture:** 문 자세는 **틱의 순수 함수**(레이저와 같은 성질)라 스냅샷·되감기에 실을 것이 없다. 문은 콜라이더라 닫히는 동안 사람을 밀어내고, **완전히 닫힌 패널과 몸이 겹칠 때만** 죽는다 — 끼임 판정 장치를 따로 만들지 않는다. 죽음 판정은 서버만.

**Tech Stack:** C# / Unity 6000.3.16f1 / VContainer / 공유 패키지(LOP-Shared) / EditMode 테스트

**Spec:** `docs/superpowers/specs/2026-09-06-skydive-doors-and-branching-design.md`

## Global Constraints

- **문은 방해자를 신경 쓰지 않는다.** 막혀도 멈추거나 되열리지 않는다. 문 위치는 항상 `f(틱)`이다. 이 성질이 깨지면 스냅샷·되감기 설계가 통째로 무너진다(스펙 §2.2).
- **문에 상태를 두지 않는다.** 카운터·타이머·"몇 틱째" 같은 것을 만들지 않는다.
- **죽음 판정은 서버만.** 클라는 문을 벽으로만 대한다(스펙 §2.4).
- **판정과 그림은 같은 식을 쓴다.** 넣는 값만 다르다(판정=정수 틱, 그림=소수 틱). 식이 둘이면 보이는 자세와 맞는 자세가 갈린다.
- **World 타입은 풀 네임스페이스로 한정한다** — `GameFramework.World.Entity` 등. `using GameFramework.World;`를 추가하지 않는다(`Component` 이름 충돌).
- 몸 캡슐 규격은 `KinematicMover.Cast`와 같아야 한다: 축은 `[base + radius, base + height − radius]`.
- 주석은 최소화하고 **비자명한 의도(왜)**만 쉬운 말로 쓴다.

## File Structure

| 파일 | 책임 |
|---|---|
| `LOP-Shared/Runtime/Scripts/Game/Door.cs` | 문 하나의 불변 데이터 |
| `LOP-Shared/Runtime/Scripts/Game/DoorGeometry.cs` | 틱 → 자세, 패널 위치, 크러시 판정 (전부 순수) |
| `LOP-Shared/Runtime/Scripts/Game/DoorField.cs` | 맵에 놓인 문 목록 |
| `LOP-Shared/Runtime/Scripts/Game/DoorVolume.cs` | 맵 씬 마커 + 패널 트랜스폼 소유 |
| `LOP-Shared/Runtime/Scripts/Game/SkydiveRespawn.cs` | 부활 로직 (레이저·문이 공유) |
| `LOP-Server/Assets/Scripts/Game/TickSystems/SkydiveDoorSystem.cs` | 닫힌 패널과 겹친 사람 → 부활 |
| `LOP-Client/Assets/Scripts/Game/SkydiveDoorView.cs` | 소수 틱으로 패널을 그린다 |
| `LOP-Client/Assets/Scripts/Editor/SkydiveCourseBuilder.cs` | 구멍 목록 · `DoorSpec` 표 · 검사 셋 |

---

### Task 1: `Door` + `DoorGeometry` — 틱에서 자세를 뽑는다

**Files:**
- Create: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/Door.cs`
- Create: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/DoorGeometry.cs`
- Test: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Tests/EditMode/DoorGeometryTests.cs`

**Interfaces:**
- Produces: `LOP.Door` (readonly struct), `LOP.DoorGeometry.Openness(in Door, double tick) → float 0..1`, `DoorGeometry.PanelCenter(in Door, int index, float openness) → System.Numerics.Vector3`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`DoorGeometryTests.cs`:

```csharp
using NUnit.Framework;

namespace LOP.Tests
{
    public class DoorGeometryTests
    {
        //  주기 20 = 열림 6 + 닫는중 4 + 닫힘 6 + 여는중 4
        static Door Make(int phase = 0) => new Door(
            center: new System.Numerics.Vector3(0f, 100f, 0f),
            halfWidth: 5f, halfDepth: 5f, thickness: 0.5f, axisAngle: 0f,
            period: 20, openTicks: 6, moveTicks: 4, phase: phase);

        [Test]
        public void 열림_구간은_1이다()
        {
            Assert.That(DoorGeometry.Openness(Make(), 0), Is.EqualTo(1f));
            Assert.That(DoorGeometry.Openness(Make(), 5), Is.EqualTo(1f));
        }

        [Test]
        public void 닫힘_구간은_0이다()
        {
            //  10 = 6 + 4. 닫힘은 [10, 16)
            Assert.That(DoorGeometry.Openness(Make(), 10), Is.EqualTo(0f));
            Assert.That(DoorGeometry.Openness(Make(), 15), Is.EqualTo(0f));
        }

        [Test]
        public void 닫히는_중에는_1에서_0으로_줄어든다()
        {
            float a = DoorGeometry.Openness(Make(), 6);
            float b = DoorGeometry.Openness(Make(), 8);
            Assert.That(a, Is.GreaterThan(b));
            Assert.That(b, Is.GreaterThan(0f));
        }

        [Test]
        public void 여는_중에는_0에서_1로_늘어난다()
        {
            //  여는 중은 [16, 20)
            Assert.That(DoorGeometry.Openness(Make(), 18),
                Is.GreaterThan(DoorGeometry.Openness(Make(), 16)));
        }

        [Test]
        public void 위상은_주기를_밀어준다()
        {
            //  phase 10이면 tick 0이 원래 tick 10(닫힘)과 같아야 한다
            Assert.That(DoorGeometry.Openness(Make(phase: 10), 0), Is.EqualTo(0f));
        }

        [Test]
        public void 음수_틱도_주기_안으로_접힌다()
        {
            //  되감기 재생이 음수 틱을 물을 일은 없지만, 접기 계산의 부호 실수를 여기서 잡는다.
            Assert.That(DoorGeometry.Openness(Make(), -20), Is.EqualTo(DoorGeometry.Openness(Make(), 0)));
        }

        [Test]
        public void 같은_틱은_같은_답이다()
        {
            //  누가 문에 상태를 넣으면 여기서 깨진다.
            for (int i = 0; i < 5; i++)
            {
                Assert.That(DoorGeometry.Openness(Make(), 7), Is.EqualTo(DoorGeometry.Openness(Make(), 7)));
            }
        }

        [Test]
        public void 닫히면_두_패널이_구멍을_정확히_덮는다()
        {
            Door d = Make();
            var a = DoorGeometry.PanelCenter(d, 0, 0f);
            var b = DoorGeometry.PanelCenter(d, 1, 0f);
            //  각 패널은 반폭의 절반 길이라, 중심이 ±halfWidth/2에 있으면 둘이 딱 맞물린다.
            Assert.That(a.X, Is.EqualTo(-2.5f).Within(1e-4f));
            Assert.That(b.X, Is.EqualTo(2.5f).Within(1e-4f));
        }

        [Test]
        public void 열리면_두_패널이_구멍_밖으로_물러난다()
        {
            Door d = Make();
            var a = DoorGeometry.PanelCenter(d, 0, 1f);
            //  물러난 패널의 안쪽 끝(-2.5 + 2.5 = ... )이 구멍 가장자리(-5) 밖에 있어야 한다.
            Assert.That(a.X + 2.5f, Is.LessThanOrEqualTo(-5f + 1e-4f));
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

클라 에디터에서 EditMode 실행(필터 `DoorGeometryTests`). 기대: `Door` / `DoorGeometry` 타입이 없어 컴파일 실패.

- [ ] **Step 3: 최소 구현**

`Door.cs`:

```csharp
namespace LOP
{
    /// <summary>
    /// 여닫이 문 하나. <b>상태가 없다</b> — 틱만 넣으면 자세가 나오므로 스냅샷에 실을 것도,
    /// 되감기에서 되돌릴 것도 없다(<see cref="Laser"/>와 같은 성질).
    /// </summary>
    public readonly struct Door
    {
        /// <summary>구멍 중심. 닫혔을 때 두 패널이 맞물리는 자리다.</summary>
        public readonly System.Numerics.Vector3 Center;

        /// <summary>덮는 폭의 절반(=구멍 반폭). 패널 하나는 이 값의 절반 길이다.</summary>
        public readonly float HalfWidth;

        /// <summary>미끄러지는 방향과 직교하는 쪽 절반.</summary>
        public readonly float HalfDepth;

        /// <summary>패널 두께(세로).</summary>
        public readonly float Thickness;

        /// <summary>패널이 미끄러지는 방향(XZ 평면 각, 라디안).</summary>
        public readonly float AxisAngle;

        public readonly int Period;
        public readonly int OpenTicks;

        /// <summary>여닫는 데 걸리는 틱. 이 움직임 자체가 예고다.</summary>
        public readonly int MoveTicks;

        public readonly int Phase;

        public Door(System.Numerics.Vector3 center, float halfWidth, float halfDepth,
                    float thickness, float axisAngle,
                    int period, int openTicks, int moveTicks, int phase)
        {
            Center = center;
            HalfWidth = halfWidth;
            HalfDepth = halfDepth;
            Thickness = thickness;
            AxisAngle = axisAngle;
            Period = period;
            OpenTicks = openTicks;
            MoveTicks = moveTicks;
            Phase = phase;
        }
    }
}
```

`DoorGeometry.cs`:

```csharp
using System;

namespace LOP
{
    /// <summary>
    /// 문의 자세를 틱에서 뽑는다. <b>판정과 그림이 이 한 식을 같이 쓴다</b> — 판정은 정수 틱,
    /// 그림은 소수 틱(프레임 사이)을 넣는다. 식이 둘이면 보이는 자세와 맞는 자세가 갈린다.
    /// </summary>
    public static class DoorGeometry
    {
        /// <summary>0 = 완전히 닫힘, 1 = 완전히 열림.</summary>
        public static float Openness(in Door door, double tick)
        {
            if (door.Period <= 0)
            {
                return 1f;   // 주기가 없으면 늘 열린 구멍이다
            }

            double raw = tick + door.Phase;
            double t = raw - Math.Floor(raw / door.Period) * door.Period;

            double closing = door.OpenTicks + door.MoveTicks;
            double opening = door.Period - door.MoveTicks;

            if (t < door.OpenTicks)
            {
                return 1f;
            }
            if (t < closing)
            {
                return (float)(1.0 - (t - door.OpenTicks) / door.MoveTicks);
            }
            if (t < opening)
            {
                return 0f;
            }
            return (float)((t - opening) / door.MoveTicks);
        }

        /// <summary>
        /// 패널 <paramref name="index"/>(0 또는 1)의 중심. 닫히면 각각 구멍의 반쪽을 덮고,
        /// 열리면 구멍 밖으로 완전히 물러난다.
        /// </summary>
        public static System.Numerics.Vector3 PanelCenter(in Door door, int index, float openness)
        {
            float sign = index == 0 ? -1f : 1f;
            float half = door.HalfWidth * 0.5f;
            float offset = half + door.HalfWidth * openness;
            float c = MathF.Cos(door.AxisAngle);
            float s = MathF.Sin(door.AxisAngle);
            return door.Center + new System.Numerics.Vector3(c, 0f, s) * (sign * offset);
        }
    }
}
```

- [ ] **Step 4: 통과를 확인한다**

EditMode 필터 `DoorGeometryTests` → 9개 전부 통과.

- [ ] **Step 5: 커밋**

```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared add Runtime/Scripts/Game/Door.cs Runtime/Scripts/Game/Door.cs.meta Runtime/Scripts/Game/DoorGeometry.cs Runtime/Scripts/Game/DoorGeometry.cs.meta Tests/EditMode/DoorGeometryTests.cs Tests/EditMode/DoorGeometryTests.cs.meta
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared commit -m "feat(skydive): 문 자세를 틱에서 뽑는다"
```

---

### Task 2: 크러시 판정 — 닫힌 패널과 몸이 겹치나

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/DoorGeometry.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/DoorCrushTests.cs`

**Interfaces:**
- Consumes: `Door`, `DoorGeometry.Openness`, `DoorGeometry.PanelCenter`
- Produces: `DoorGeometry.Crushes(in Door, long tick, System.Numerics.Vector3 bottom, System.Numerics.Vector3 top, float radius) → bool`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

```csharp
using NUnit.Framework;

namespace LOP.Tests
{
    public class DoorCrushTests
    {
        const float Radius = 0.4f;

        static Door Make() => new Door(
            center: new System.Numerics.Vector3(0f, 100f, 0f),
            halfWidth: 5f, halfDepth: 5f, thickness: 0.5f, axisAngle: 0f,
            period: 20, openTicks: 6, moveTicks: 4, phase: 0);

        //  몸은 선 캡슐이다 — 이동 커널과 같은 규격으로 축을 반지름만큼 안으로 당긴다.
        static void Body(float x, float y, float z, out System.Numerics.Vector3 b, out System.Numerics.Vector3 t)
        {
            b = new System.Numerics.Vector3(x, y + Radius, z);
            t = new System.Numerics.Vector3(x, y + 1.8f - Radius, z);
        }

        [Test]
        public void 닫힌_패널_안에_있으면_죽는다()
        {
            Body(0f, 99.8f, 0f, out var b, out var t);   // 패널 높이(100)에 몸이 걸침
            Assert.That(DoorGeometry.Crushes(Make(), 12, b, t, Radius), Is.True);
        }

        [Test]
        public void 열려_있으면_안_죽는다()
        {
            Body(0f, 99.8f, 0f, out var b, out var t);
            Assert.That(DoorGeometry.Crushes(Make(), 0, b, t, Radius), Is.False);
        }

        [Test]
        public void 닫히는_중에는_안_죽는다()
        {
            //  닫히는 동안은 벽일 뿐이다 — 밀려날 기회를 준다.
            Body(0f, 99.8f, 0f, out var b, out var t);
            Assert.That(DoorGeometry.Crushes(Make(), 8, b, t, Radius), Is.False);
        }

        [Test]
        public void 패널보다_아래로_지나갔으면_안_죽는다()
        {
            //  구멍 안이어도 이미 통과했으면 산다. "부피"는 구멍이 아니라 패널이다.
            Body(0f, 95f, 0f, out var b, out var t);
            Assert.That(DoorGeometry.Crushes(Make(), 12, b, t, Radius), Is.False);
        }

        [Test]
        public void 옆으로_벗어나_있으면_안_죽는다()
        {
            Body(20f, 99.8f, 0f, out var b, out var t);
            Assert.That(DoorGeometry.Crushes(Make(), 12, b, t, Radius), Is.False);
        }

        [Test]
        public void 문턱의_양쪽을_잰다()
        {
            //  패널 바깥 끝은 x=5. 몸 반지름 0.4를 더해 4.99와 5.5를 양쪽에서 확인한다.
            Body(5.5f + Radius + 0.01f, 99.8f, 0f, out var outside, out var outsideTop);
            Assert.That(DoorGeometry.Crushes(Make(), 12, outside, outsideTop, Radius), Is.False, "바깥");

            Body(4.9f, 99.8f, 0f, out var inside, out var insideTop);
            Assert.That(DoorGeometry.Crushes(Make(), 12, inside, insideTop, Radius), Is.True, "안쪽");
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다** — `Crushes`가 없어 컴파일 실패.

- [ ] **Step 3: 구현을 `DoorGeometry`에 더한다**

```csharp
        /// <summary>
        /// 이 틱에 <b>완전히 닫힌</b> 패널과 몸이 겹치나. 닫히는 중에는 벽일 뿐이라 false다 —
        /// 밀려날 기회를 다 준 뒤에 묻는다.
        ///
        /// <para>몸이 선 캡슐(위아래 끝의 x·z가 같다)이라 거리를 닫힌 식으로 낼 수 있다:
        /// 각 축의 초과분을 재서 합치면 상자까지의 최단거리다.</para>
        /// </summary>
        public static bool Crushes(in Door door, long tick,
                                   System.Numerics.Vector3 bottom, System.Numerics.Vector3 top, float radius)
        {
            if (Openness(door, tick) > 0f)
            {
                return false;
            }

            //  문이 미끄러지는 방향을 x축으로 두고 본다 — 상자가 축에 정렬돼 계산이 단순해진다.
            float c = MathF.Cos(-door.AxisAngle);
            float s = MathF.Sin(-door.AxisAngle);
            System.Numerics.Vector3 d = bottom - door.Center;
            float localX = d.X * c - d.Z * s;
            float localZ = d.X * s + d.Z * c;

            float halfPanel = door.HalfWidth * 0.5f;
            float halfThick = door.Thickness * 0.5f;
            float panelY = door.Center.Y;

            for (int index = 0; index < 2; index++)
            {
                float sign = index == 0 ? -1f : 1f;
                float panelX = sign * halfPanel;

                float dx = MathF.Max(MathF.Abs(localX - panelX) - halfPanel, 0f);
                float dz = MathF.Max(MathF.Abs(localZ) - door.HalfDepth, 0f);
                float dy = MathF.Max(MathF.Max(panelY - halfThick - top.Y,
                                               bottom.Y - (panelY + halfThick)), 0f);

                if (dx * dx + dy * dy + dz * dz <= radius * radius)
                {
                    return true;
                }
            }
            return false;
        }
```

- [ ] **Step 4: 통과를 확인한다** — 6개 전부 통과.

- [ ] **Step 5: 커밋**

```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared add Runtime/Scripts/Game/DoorGeometry.cs Tests/EditMode/DoorCrushTests.cs Tests/EditMode/DoorCrushTests.cs.meta
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared commit -m "feat(skydive): 닫힌 패널과 몸이 겹치는지 판정한다"
```

---

### Task 3: `DoorField` + `DoorVolume` — 맵이 문을 등록한다

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/DoorField.cs`
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/DoorVolume.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/DoorFieldTests.cs`

**Interfaces:**
- Produces: `DoorField.Add/Remove/All`, `DoorVolume.ToDoor()`, `DoorVolume.Pose(double tick)`

`LaserVolume`/`LaserField`를 **그대로 따른다** — 새 어휘를 만들지 않는다. 특히:

- `[SceneInjectMonoBehaviour]` + `[Inject] Construct(DoorField)` 로 스스로 등록
- **등록할 때의 값을 들고 있다가 `OnDestroy`에서 뺀다** — 라운드가 여러 판이면 맵을 다시 로드하는데 안 빼면 문이 두 배가 된다
- **첫 틱에 필드를 비우지 않는다** — 등록이 첫 틱보다 앞선다(레이저 슬라이스에서 잡은 함정)

- [ ] **Step 1: 테스트** — `Add` 두 번 하면 둘 다 들어가고, `Remove`하면 빠지고, 같은 것을 두 번 `Add`해도 하나다.
- [ ] **Step 2: 실패 확인**
- [ ] **Step 3: `DoorField`는 `LaserField`를 복제하되 타입만 바꾼다. `DoorVolume`은 `LaserVolume`을 따르되 필드가 `HalfWidth`/`HalfDepth`/`Thickness`/`AxisAngleDegrees`/`Period`/`OpenTicks`/`MoveTicks`/`Phase`이고, 자식 패널 트랜스폼 둘(`PanelA`/`PanelB`)을 `public Transform`으로 노출한다. `Pose(double tick)`가 `DoorGeometry.PanelCenter`로 두 패널의 로컬 위치를 세팅한다.**
- [ ] **Step 4: 통과 확인**
- [ ] **Step 5: 커밋**

---

### Task 4: `SkydiveWorld` 배선 — 매 틱 문을 그 틱 자세로

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveWorld.cs`
- Modify: `LOP-Client/Assets/Scripts/Game/SkydiveLifetimeScope.cs`
- Modify: `LOP-Server/Assets/Scripts/Game/SkydiveLifetimeScope.cs`
- Modify: `LeagueOfPhysical-Shared/Tests/EditMode/SkydiveWorldTests.cs`, `LOP-Client/Assets/Tests/Editor/SkydiveCorrectionFixture.cs` (생성자 인자 추가)

**Interfaces:**
- Consumes: `DoorField`, `DoorVolume.Pose`
- Produces: `SkydiveWorld` 생성자에 `DoorField doorField` 추가

- [ ] **Step 1**: `SkydiveWorld.Mutation`에서 **이동 전에** 문 자세를 세팅한다. 위치는 밀어내기(`Depenetrate`) **직전**이다:

```csharp
            //  문을 이 틱 자세로 돌려놓고 엔진에 반영한다. 되감기 재생 중에도 같은 자리에서
            //  닫히므로 재생이 라이브와 같은 답을 낸다 — 뷰가 프레임마다 옮기면 이 성질이 깨진다.
            PoseDoors(tick);

            for (int i = 0; i < _divers.Count; i++)
            {
                ClearVelocityIntoSurface(_divers[i], _motionBridge.Depenetrate(_divers[i]));
            }
```

```csharp
        private void PoseDoors(long tick)
        {
            System.Collections.Generic.IReadOnlyList<DoorVolume> doors = _doorField.All;
            if (doors.Count == 0)
            {
                return;
            }
            for (int i = 0; i < doors.Count; i++)
            {
                doors[i].Pose(tick);
            }
            //  트랜스폼을 방금 바꿨다. 겹침 질의가 옛 자리를 보지 않도록 여기서 한 번 맞춘다.
            _motionBridge.SyncTransforms();
        }
```

- [ ] **Step 2**: 두 스코프에 `builder.Register<DoorField>(Lifetime.Singleton);`과 `c.Resolve<DoorField>()`를 더한다. **클라에도 반드시 등록한다** — 마커의 `[Inject]`가 이걸 요구하므로 등록이 없으면 씬 주입이 그 자리에서 끊긴다(레이저에서 겪은 것).
- [ ] **Step 3**: 테스트 호출부 둘에 `new DoorField()`를 더한다.
- [ ] **Step 4**: 클라 EditMode 전체 통과 확인.
- [ ] **Step 5**: 세 레포 커밋.

---

### Task 5: 부활 로직을 공유로 뽑고 서버 `SkydiveDoorSystem`

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveRespawn.cs`
- Modify: `LOP-Server/Assets/Scripts/Game/TickSystems/SkydiveLaserSystem.cs` (뽑아낸 것을 쓰게)
- Create: `LOP-Server/Assets/Scripts/Game/TickSystems/SkydiveDoorSystem.cs`
- Modify: `LOP-Server/Assets/Scripts/Game/SkydiveLifetimeScope.cs`
- Test: `LOP-Server/Assets/Tests/Editor/SkydiveDoorSystemTests.cs`

**Interfaces:**
- Produces: `SkydiveRespawn.To(GameFramework.World.Entity, float deathY, SkydiveConfig, IReadOnlyList<float> shelfYs, float spawnY, IReadOnlyDictionary<float, Vector3> respawnPoints, ref int spreadOrder)`

**왜 뽑나**: 레이저와 문이 같은 부활을 한다. 복제하면 한쪽만 고쳐지는 날이 온다.

- [ ] **Step 1**: `SkydiveLaserSystem.Respawn`의 본문을 `SkydiveRespawn.To`로 옮기고, 레이저는 그것을 부르게 바꾼다. **기존 레이저 테스트 4개가 그대로 통과해야 한다** — 통과하면 뽑기가 동작을 안 바꿨다는 증거다.
- [ ] **Step 2**: `SkydiveDoorSystem` 테스트를 쓴다:
  - 닫힌 문 안에 있으면 마지막 선반으로 되돌아간다
  - 열려 있으면 안 되돌아간다
  - 닫히는 중이면 안 되돌아간다
  - 부활은 텔레포트 카운트를 올린다
- [ ] **Step 3**: `SkydiveDoorSystem`을 쓴다. `SkydiveLaserSystem`과 같은 모양이되 스윕이 필요 없다(문은 안 움직이는 판정 — 겹침만 본다):

```csharp
        public void Tick(long tick, float deltaTime)
        {
            CollectDivers();
            IReadOnlyList<DoorVolume> doors = doorField.All;
            for (int i = 0; i < divers.Count; i++)
            {
                GameFramework.World.Entity diver = divers[i];
                Vector3 p = GameFramework.World.EntityMotionExtensions.GetPosition(diver);
                //  이동이 쓰는 캡슐과 같은 규격이어야 한다 — 축을 반지름만큼 안으로 당긴다.
                var bottom = new System.Numerics.Vector3(p.x, p.y + config.BodyRadius, p.z);
                var top = new System.Numerics.Vector3(p.x, p.y + config.BodyHeight - config.BodyRadius, p.z);

                for (int d = 0; d < doors.Count; d++)
                {
                    if (DoorGeometry.Crushes(doors[d].ToDoor(), tick, bottom, top, config.BodyRadius))
                    {
                        Respawn(diver, p.y);
                        break;
                    }
                }
            }
        }
```

- [ ] **Step 4**: 서버 EditMode 통과 확인(레이저 4개 + 문 4개).
- [ ] **Step 5**: 커밋.

---

### Task 6: 굽기 — 구멍 목록과 검사 셋

**Files:**
- Modify: `LOP-Client/Assets/Scripts/Editor/SkydiveCourseBuilder.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveCourseLayout.cs` (부활 지점이 구멍 목록과 안 겹치게)
- Test: `LOP-Client/Assets/Tests/Editor/SkydiveDoorBuildTests.cs`

- [ ] **Step 1**: `Shelf`를 구멍 하나에서 **둘**로 바꾼다. 표에 `FastHole`(문 달림)과 `SafeHole`(항상 열림)을 둔다.
- [ ] **Step 2**: 검사 셋을 쓴다. 기존 `Verify`를 **도달 가능한 구멍 집합을 굴리는** 형태로 바꾼다.

```csharp
        //  구멍이 둘이 되면 기존 검사가 "어느 하나엔 닿는가"로 느슨해진다. 느슨해진 만큼
        //  검사로 되잡는다. 앞 선반에서 도달 가능했던 구멍들만 다음 칸의 출발점이 된다 —
        //  "어딘가에서 닿으면 됨"이 아니라 "실제로 갈 수 있었던 자리에서 닿아야 함"이다.
        private static bool ReachableChain(bool safeOnly, out string report)
        {
            var lines = new List<string>();
            bool ok = true;

            //  출발은 스폰 한 점이다.
            var from = new List<Vector2> { new Vector2(0f, 0f) };
            float previousY = LOP.SkydiveCourseLayout.SpawnY;

            foreach (var shelf in Shelves)
            {
                float fall = previousY - shelf.Y;
                float spread = SkydiveReach.MaxHorizontal(fall, SpreadFallSpeed, SpreadMoveSpeed, SpreadTurnAccel);
                float dive = SkydiveReach.MaxHorizontal(fall, DiveFallSpeed, DiveMoveSpeed, DiveTurnAccel);

                var next = new List<Vector2>();
                foreach (var hole in shelf.Holes)
                {
                    if (safeOnly && hole.HasDoor)
                    {
                        continue;   // ② 문을 하나도 못 뚫는 사람의 경로
                    }
                    //  구멍 반쪽만큼은 덤이다 — 중심까지 안 가도 가장자리로 들어가면 통과다.
                    float best = float.MaxValue;
                    foreach (var p in from)
                    {
                        best = Mathf.Min(best, Vector2.Distance(p, new Vector2(hole.X, hole.Z)));
                    }
                    if (best <= spread + hole.Half)
                    {
                        next.Add(new Vector2(hole.X, hole.Z));
                    }
                    lines.Add($"y={shelf.Y:0} {(hole.HasDoor ? "빠른" : "안전")}: 이동 {best:0.0}m " +
                              $"/ 대자 {spread:0.0}m / 다이브 {dive:0.0}m");
                }

                if (next.Count == 0)
                {
                    lines.Add($"  [X] y={shelf.Y:0}에 닿는 구멍이 없다{(safeOnly ? " (안전 경로)" : "")}");
                    ok = false;
                    break;
                }
                from = next;
                previousY = shelf.Y;
            }

            report = string.Join("
", lines);
            return ok;
        }

        //  ③ 두 길이 실제로 다른 자세를 요구하는가. 이 조건이 성립하면 "빠른 길이 진짜 빠른가"를
        //  따로 증명할 필요가 없다 — 다이브로 갈 수 있다는 것 자체가 더 빠르다는 뜻이다.
        private static string FindRouteNotSplit()
        {
            var from = new List<Vector2> { new Vector2(0f, 0f) };
            float previousY = LOP.SkydiveCourseLayout.SpawnY;

            foreach (var shelf in Shelves)
            {
                float fall = previousY - shelf.Y;
                float spread = SkydiveReach.MaxHorizontal(fall, SpreadFallSpeed, SpreadMoveSpeed, SpreadTurnAccel);
                float dive = SkydiveReach.MaxHorizontal(fall, DiveFallSpeed, DiveMoveSpeed, DiveTurnAccel);

                var next = new List<Vector2>();
                foreach (var hole in shelf.Holes)
                {
                    float best = float.MaxValue;
                    foreach (var p in from)
                    {
                        best = Mathf.Min(best, Vector2.Distance(p, new Vector2(hole.X, hole.Z)));
                    }
                    float reach = best - hole.Half;

                    if (hole.HasDoor && reach > dive)
                    {
                        return $"y={shelf.Y:0}의 빠른 구멍이 다이브로 안 닿는다({reach:0.0} > {dive:0.0})";
                    }
                    if (hole.HasDoor == false && reach <= dive)
                    {
                        return $"y={shelf.Y:0}의 안전한 구멍이 다이브로도 닿는다({reach:0.0} ≤ {dive:0.0}) — 문이 무의미해진다";
                    }
                    next.Add(new Vector2(hole.X, hole.Z));
                }
                from = next;
                previousY = shelf.Y;
            }
            return null;
        }
```

- [ ] **Step 3**: 각 검사의 **거절** 테스트를 쓴다. 그리고 **절제**: 검사를 껐을 때 그 거절 케이스가 통과하는지 확인해 테스트가 다른 이유로 초록이 아님을 증명한다.
- [ ] **Step 4**: 통과 확인.
- [ ] **Step 5**: 커밋.

---

### Task 7: 굽기 — 문 패널 생성

**Files:**
- Modify: `LOP-Client/Assets/Scripts/Editor/SkydiveCourseBuilder.cs`

- [ ] **Step 1**: `DoorSpec` 표(레이저의 `LaserSpec`과 같은 모양)를 더한다.
- [ ] **Step 2**: `CreateDoor`가 허브(`DoorVolume`)와 자식 패널 둘(박스 + 콜라이더 + 렌더러)을 만든다. **콜라이더는 남긴다** — 문은 벽이어야 하므로 레이저 뷰처럼 지우면 안 된다.
- [ ] **Step 3**: 검사가 **옛 코스를 지우기 전에** 도는지 확인한다(거절되면 씬이 그대로여야 한다).
- [ ] **Step 4**: 실제로 굽고 씬에 문이 들어갔는지 센다.
- [ ] **Step 5**: 커밋(클라 + 아트).

---

### Task 8: 클라 뷰 — 소수 틱으로 그린다

**Files:**
- Create: `LOP-Client/Assets/Scripts/Game/SkydiveDoorView.cs`
- Modify: `LOP-Client/Assets/Scripts/Game/SkydiveLifetimeScope.cs`

- [ ] **Step 1: 뷰를 쓴다**

```csharp
using System.Collections.Generic;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 문 패널을 <b>프레임마다</b> 그 순간 자세로 옮긴다.
    ///
    /// <para>시뮬은 초당 50번인데 화면은 60번 이상 그려진다. 시뮬이 잡아 준 정수 틱 자세만 쓰면
    /// 여섯 프레임 중 하나가 제자리라 계단처럼 떤다.</para>
    ///
    /// <para><b>판정은 안 건드린다</b>: 다음 틱에 시뮬이 <c>PoseDoors</c>로 정수 틱 자세를 다시
    /// 잡고 그 자세에서 질의한다. 되감기 재생도 같은 값을 본다.</para>
    /// </summary>
    public class SkydiveDoorView : ILateTickable
    {
        private readonly GameFramework.Runner.IRunner runner;
        private readonly DoorField doorField;

        public SkydiveDoorView(GameFramework.Runner.IRunner runner, DoorField doorField)
        {
            this.runner = runner;
            this.doorField = doorField;
        }

        public void LateTick()
        {
            IReadOnlyList<DoorVolume> doors = doorField.All;
            if (doors.Count == 0 || runner?.tickUpdater == null)
            {
                return;
            }
            double interval = runner.tickUpdater.interval;
            if (interval <= 0d)
            {
                return;
            }
            //  틱 사이 어디쯤인지까지 담은 소수 틱. 판정이 쓰는 식에 이 값을 그대로 넣으므로
            //  보이는 자세와 맞는 자세가 같은 곡선 위에 있다.
            double renderTick = runner.tickUpdater.elapsedTime / interval;
            for (int i = 0; i < doors.Count; i++)
            {
                doors[i].Pose(renderTick);
            }
        }
    }
}
```

- [ ] **Step 2: 스코프에 등록한다** (`SkydiveLaserView` 옆)

```csharp
            //  시뮬은 50Hz인데 화면은 더 빨라, 틱 자세만 쓰면 문이 계단처럼 떤다.
            builder.RegisterEntryPoint<SkydiveDoorView>().AsSelf();
```

- [ ] **Step 3: 컴파일 확인** — 클라 EditMode 전체 통과.
- [ ] **Step 4: 굽고 실행해 눈으로 확인** — 문이 매끄럽게 여닫히는가.
- [ ] **Step 5: 커밋**

---

## 마무리 (계획 밖, 사람이 판단)

- 로컬 배포(게임서버 + 콘텐츠) 후 **실제로 플레이**해서: 문이 보이나 / 밀려나나 / 꽉 끼면 죽나 / 두 길이 실제로 갈리나
- 스펙 §10의 위험 2(**두 길의 재미가 안 갈릴 위험**)는 플레이로만 확인된다
- 슬라이스 6 측정 시나리오에 **문틈 몸싸움**을 추가할 것(스펙 §4)
