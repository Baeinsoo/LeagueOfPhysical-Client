# Skydive 바람 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 맵에 놓인 원기둥 볼륨이 바람을 만들고, 떨어지는 몸이 **자세에 따라 다른 속도로** 그 바람에 실리게 한다.

**Architecture:** 바람은 위치만 넣으면 답이 나오는 정적 데이터(`WindField`)이고, 시간을 타지 않아 되감기가 공짜다. 몸은 `WindDrift` 컴포넌트에 "지금까지 실린 바람"을 들고, `WindDriftSystem`이 자세별 속도로 그 값을 목표 바람에 붙인다. `SkydiveMoveSystem`은 그 값을 **목표 속도에 더한다** — 이 게임의 속도는 목표로 끌려가는 구조라 속도에 직접 더하면 다음 틱에 지워진다. 볼륨은 맵 씬의 `[SceneInjectMonoBehaviour]` 마커가 맵 로드 시 스스로 등록한다.

**Tech Stack:** Unity 6000.3.16f1 / C# / VContainer / NUnit EditMode / Luban MasterData

**Spec:** `docs/superpowers/specs/2026-09-03-skydive-wind-design.md`

---

## Global Constraints

- **레포 7개가 걸린다.** 각각에서 `feature/skydive-wind` 브랜치를 파고, 브랜치는 **`origin/main` 기준**으로 딴다(로컬 main이 뒤처져 있을 수 있다):
  `LeagueOfPhysical-Shared` · `LeagueOfPhysical-Client` · `LeagueOfPhysical-Server` · `LeagueOfPhysical-MasterData-Client` · `LeagueOfPhysical-MasterData-Server` · `infrastructure` · `LeagueOfPhysical-Art`
- **`git add -A` / `git commit -a` 금지.** 바꾼 파일만 경로로 지정하고, 커밋 전 `git diff --cached --name-only`로 스테이지된 것이 의도한 파일뿐인지 확인한다. 클라·서버 워킹트리에는 **의도적으로 커밋하지 않는 로컬 픽스처**가 늘 있다(`Assets/Art` 서브모듈 포인터, `ProjectSettings/*.asset`, `Jua-Regular SDF.asset`).
- **푸시는 하지 않는다.** 이 계획의 범위는 커밋까지다. 머지·푸시는 사람이 `finishing-a-development-branch`에서 한다.
- **테스트 실행 전에 반드시 컴파일을 먼저 통과시킨다.** 컴파일 에러가 있는 채로 `run_tests`를 부르면 에디터가 물린다(상태 조회는 캐시로 답해 살아 있어 보인다). 순서:
  ```bash
  unity command recompile --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
  unity command recompile_status --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
  ```
  `recompile_status`의 `status`가 `up_to_date`면 **재컴파일을 안 한 것**이다. 결정타는 `unity command console --project-path ... --json`으로 CS 에러가 없는지 확인하는 것.
- **테스트 실행:**
  ```bash
  unity command run_tests --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json -- --mode editor --filter <테스트클래스이름>
  unity command test_status --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
  ```
  LOP-Shared 테스트도 **클라 프로젝트에서** 돈다(`Packages/manifest.json`의 `testables`에 `com.baegames.lop.shared`가 있다). 결과 키는 대문자다(`FullName`, `Status`).
- **World 타입은 항상 풀 네임스페이스로 쓴다** — `GameFramework.World.Entity`, `GameFramework.World.Component`. `using GameFramework.World;`를 추가하지 않는다(`UnityEngine.Component`와 이름이 겹친다).
- **시뮬 코드에 `Vector3`는 `System.Numerics.Vector3`다.** `WindCylinder`/`WindField`/`WindDrift`/`WindDriftSystem` 네 파일에는 **`using UnityEngine;`을 쓰지 않는다**(모호성). Unity 쪽 값이 필요한 자리는 `WindVolume` 하나뿐이고 거기서 `.ToNumerics()`로 넘긴다.
- **주석은 한국어로, 최소한으로.** 코드로 자명한 것은 안 쓰고 **비자명한 의도(왜)** 만 쉬운 말로 남긴다. 전문용어를 설명 없이 던지지 않는다.
- **`.meta` 파일은 반드시 함께 커밋한다.** 직접 만들거나 고치지 않는다 — 유니티가 만든 것만 커밋한다. 새 `.cs`를 만든 뒤 `.meta`가 안 보이면 에디터가 스캔할 시간을 준다(`unity command recompile`이 그 계기가 된다).
- **exp/sin/cos 같은 초월함수를 시뮬에 쓰지 않는다.** IEEE 754가 마지막 자릿수를 보장하지 않아 클라(윈도우·안드로이드)와 서버(리눅스)가 갈릴 수 있다. 덧셈·뺄셈·곱셈·나눗셈·`sqrt`만 쓴다.
- **튜닝 상수 (스펙 5.5, 그대로 옮길 것):** `GlideWindLag = 0.2`, `SpreadWindLag = 2.06`, `DiveWindLag = 3.10`

### 지금 코스 (참조용)

```
선반 Y   구멍 (x, z)   구멍 한 변    이전 구멍에서 옆으로 가야 하는 거리
2600     (  0,   0)      30          —
2200     ( 30,   0)      24          30
1800     ( 30,  30)      20          30
1400     (-25,  30)      20          55
1000     (-25, -30)      16          60
 600     ( 30, -25)      16          55
 200     (  0,  25)      16          58
```

코스 폭 200 m (x, z ∈ [−100, 100]), 출발 y = 3000, 바닥 y = 0.

### 지금 튜닝값 (`TbSkydiveConfig` id=1)

```
SpreadFallSpeed 60   DiveFallSpeed 90   GlideFallSpeed 6
SpreadMoveSpeed 12   DiveMoveSpeed  9   GlideMoveSpeed 14
SpreadTurnAccel 22   DiveTurnAccel  6   GlideTurnAccel 18
FallApproach    29   FallBrake    150   PostureRate     4
BodyRadius     0.4   BodyHeight   1.8   GroundY         0
StaminaMax     300   GlideDrain    20   GroundRecover  40   EmergencyGlideTime 1
GroundMoveSpeed  4   GroundAccel  100   JumpPower      11   PoseClearance      5
```

---

## 파일 구조

| 파일 | 책임 |
|---|---|
| `LOP-Shared/Runtime/Scripts/Game/WindCylinder.cs` | 바람 원기둥 하나 — 위치·크기·바람 + 포함 판정 |
| `LOP-Shared/Runtime/Scripts/Game/WindField.cs` | 이 판의 바람 전부 — 등록/해제/`SampleAt` |
| `LOP-Shared/Runtime/Scripts/Game/WindDrift.cs` | 몸이 지금까지 실린 바람 (World Core 컴포넌트) |
| `LOP-Shared/Runtime/Scripts/Game/WindDriftSystem.cs` | 자세별 속도로 `WindDrift`를 목표 바람에 붙인다 |
| `LOP-Shared/Runtime/Scripts/Game/WindVolume.cs` | 맵 씬 마커 — 맵 로드 시 `WindField`에 자기를 넣는다 |
| `LOP-Shared/Runtime/Scripts/Game/SkydiveWindReach.cs` | 밀린 거리·자력 이동 계산 — 코스가 막혔는지 검사하는 순수 산수 |
| `LOP-Shared/Runtime/Scripts/Game/SkydiveConfig.cs` | 튜닝값 3개 추가 |
| `LOP-Shared/Runtime/Scripts/Game/SkydiveMoveSystem.cs` | 목표 속도에 바람을 더한다 |
| `LOP-Shared/Runtime/Scripts/Game/SkydiveWorld.cs` | 틱 안에 바람 단계를 넣는다 |
| `LOP-Shared/Runtime/Scripts/Game/SkydiveSavedState.cs` | 되감기에 `WindDrift` 포함 |
| `Client/Assets/Scripts/Editor/SkydiveCourseBuilder.cs` | 볼륨 8개와 보이는 막대를 굽는다 |

---

## Task 1: `WindCylinder` + `WindField`

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/WindCylinder.cs`
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/WindField.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/WindFieldTests.cs`

**Interfaces:**
- Consumes: (없음)
- Produces:
  - `LOP.WindCylinder` — `ctor(System.Numerics.Vector3 center, float radius, float height, System.Numerics.Vector3 wind)`, 읽기 전용 필드 `Center`/`Radius`/`Height`/`Wind`, `bool Contains(System.Numerics.Vector3 point)`
  - `LOP.WindField` — `void Add(WindCylinder)`, `bool Remove(WindCylinder)`, `System.Numerics.Vector3 SampleAt(System.Numerics.Vector3 position)`, `int Count { get; }`

- [ ] **Step 1: 브랜치를 딴다**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git fetch origin
git checkout -b feature/skydive-wind origin/main
git rev-list --left-right --count origin/main...HEAD
```
Expected: `0	0`

- [ ] **Step 2: 실패하는 테스트를 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/WindFieldTests.cs`:

```csharp
using NUnit.Framework;
using System.Numerics;

namespace LOP.Tests
{
    public class WindFieldTests
    {
        const float Tolerance = 1e-4f;

        static WindCylinder Updraft(float y, float radius = 10f, float height = 100f, float up = 14f)
            => new WindCylinder(new Vector3(0f, y, 0f), radius, height, new Vector3(0f, up, 0f));

        [Test]
        public void 빈_필드는_0을_준다()
        {
            var field = new WindField();
            Assert.AreEqual(0f, field.SampleAt(new Vector3(0f, 1000f, 0f)).Length(), Tolerance);
        }

        [Test]
        public void 안에_있으면_그_바람이_나온다()
        {
            var field = new WindField();
            field.Add(Updraft(1000f));

            var wind = field.SampleAt(new Vector3(0f, 1000f, 0f));

            Assert.AreEqual(14f, wind.Y, Tolerance);
        }

        [Test]
        public void 가로로_벗어나면_0이다()
        {
            var field = new WindField();
            field.Add(Updraft(1000f, radius: 10f));

            Assert.AreEqual(0f, field.SampleAt(new Vector3(10.1f, 1000f, 0f)).Length(), Tolerance);
        }

        [Test]
        public void 세로로_벗어나면_0이다()
        {
            var field = new WindField();
            field.Add(Updraft(1000f, height: 100f));

            Assert.AreEqual(0f, field.SampleAt(new Vector3(0f, 1050.1f, 0f)).Length(), Tolerance);
        }

        [Test]
        public void 경계는_포함이다()
        {
            var field = new WindField();
            field.Add(Updraft(1000f, radius: 10f, height: 100f));

            Assert.AreEqual(14f, field.SampleAt(new Vector3(10f, 1050f, 0f)).Y, Tolerance);
        }

        [Test]
        public void 겹친_볼륨은_더해진다()
        {
            var field = new WindField();
            field.Add(Updraft(1000f));
            field.Add(new WindCylinder(new Vector3(0f, 1000f, 0f), 10f, 100f, new Vector3(5f, 0f, 0f)));

            var wind = field.SampleAt(new Vector3(0f, 1000f, 0f));

            Assert.AreEqual(5f, wind.X, Tolerance);
            Assert.AreEqual(14f, wind.Y, Tolerance);
        }

        // 등록 순서는 씬 순회 순서라 정해져 있지 않은데, 부동소수 덧셈은 순서가 바뀌면
        // 마지막 자릿수가 바뀐다. 클·서가 그것 때문에 갈리면 안 된다.
        [Test]
        public void 등록_순서가_달라도_같은_값이_나온다()
        {
            var a = new WindCylinder(new Vector3(0f, 1000f, 0f), 50f, 100f, new Vector3(0.1f, 0f, 0f));
            var b = new WindCylinder(new Vector3(0f, 1005f, 0f), 50f, 100f, new Vector3(0.2f, 0f, 0f));
            var c = new WindCylinder(new Vector3(0f, 995f, 0f), 50f, 100f, new Vector3(0.3f, 0f, 0f));

            var forward = new WindField();
            forward.Add(a); forward.Add(b); forward.Add(c);

            var backward = new WindField();
            backward.Add(c); backward.Add(b); backward.Add(a);

            var point = new Vector3(0f, 1000f, 0f);
            Assert.AreEqual(forward.SampleAt(point).X, backward.SampleAt(point).X);
        }

        [Test]
        public void 뺀_볼륨은_더는_안_센다()
        {
            var field = new WindField();
            var cylinder = Updraft(1000f);
            field.Add(cylinder);

            Assert.IsTrue(field.Remove(cylinder));
            Assert.AreEqual(0, field.Count);
            Assert.AreEqual(0f, field.SampleAt(new Vector3(0f, 1000f, 0f)).Length(), Tolerance);
        }

        [Test]
        public void 같은_볼륨을_두_번_넣어도_한_번만_센다()
        {
            var field = new WindField();
            var cylinder = Updraft(1000f);
            field.Add(cylinder);
            field.Add(cylinder);

            Assert.AreEqual(1, field.Count);
            Assert.AreEqual(14f, field.SampleAt(new Vector3(0f, 1000f, 0f)).Y, Tolerance);
        }
    }
}
```

- [ ] **Step 3: 컴파일이 깨지는 것을 확인한다**

```bash
unity command recompile --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
unity command console --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
```
Expected: `WindCylinder`/`WindField`를 못 찾는다는 CS0246 에러

- [ ] **Step 4: `WindCylinder`를 만든다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/WindCylinder.cs`:

```csharp
using System.Numerics;

namespace LOP
{
    /// <summary>
    /// 바람이 부는 원기둥 하나. 맵의 <see cref="WindVolume"/> 마커가 만들어
    /// <see cref="WindField"/>에 넣는다.
    ///
    /// 세로로 세운 기류 기둥이든 넓적하게 눕힌 횡풍 구간이든 같은 모양이라 판정도 한 벌이다.
    /// </summary>
    public sealed class WindCylinder
    {
        public readonly Vector3 Center;
        public readonly float Radius;
        public readonly float Height;

        /// <summary>방향 × 세기 (m/s).</summary>
        public readonly Vector3 Wind;

        public WindCylinder(Vector3 center, float radius, float height, Vector3 wind)
        {
            Center = center;
            Radius = radius;
            Height = height;
            Wind = wind;
        }

        public bool Contains(Vector3 point)
        {
            float half = Height * 0.5f;
            float dy = point.Y - Center.Y;
            if (dy < -half || dy > half)
            {
                return false;
            }

            float dx = point.X - Center.X;
            float dz = point.Z - Center.Z;
            return dx * dx + dz * dz <= Radius * Radius;
        }
    }
}
```

- [ ] **Step 5: `WindField`를 만든다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/WindField.cs`:

```csharp
using System.Collections.Generic;
using System.Numerics;

namespace LOP
{
    /// <summary>
    /// 이 판의 바람 전부. 위치를 넣으면 그 지점의 바람이 나온다.
    ///
    /// 시간을 타지 않는다 — 클라가 과거 틱으로 되감아 다시 달려도 같은 답이 나온다.
    /// (움직이는 강체를 이번에 안 만드는 이유가 정확히 이 성질의 반대다.)
    /// </summary>
    public class WindField
    {
        private readonly List<WindCylinder> cylinders = new List<WindCylinder>();

        public int Count => cylinders.Count;

        public void Add(WindCylinder cylinder)
        {
            if (cylinder == null || cylinders.Contains(cylinder))
            {
                return;
            }

            cylinders.Add(cylinder);

            // 겹친 바람을 더하는 순서를 고정한다. 씬에서 들어오는 순서는 정해져 있지 않은데,
            // 부동소수 덧셈은 순서가 바뀌면 마지막 자릿수가 바뀌어 클·서가 갈린다.
            cylinders.Sort(CompareForStableSum);
        }

        public bool Remove(WindCylinder cylinder) => cylinders.Remove(cylinder);

        public Vector3 SampleAt(Vector3 position)
        {
            var total = Vector3.Zero;
            for (int i = 0; i < cylinders.Count; i++)
            {
                if (cylinders[i].Contains(position))
                {
                    total += cylinders[i].Wind;
                }
            }
            return total;
        }

        private static int CompareForStableSum(WindCylinder left, WindCylinder right)
        {
            int result = left.Center.Y.CompareTo(right.Center.Y);
            if (result != 0) return result;
            result = left.Center.X.CompareTo(right.Center.X);
            if (result != 0) return result;
            result = left.Center.Z.CompareTo(right.Center.Z);
            if (result != 0) return result;
            result = left.Radius.CompareTo(right.Radius);
            if (result != 0) return result;
            return left.Height.CompareTo(right.Height);
        }
    }
}
```

- [ ] **Step 6: 테스트가 통과하는지 확인한다**

```bash
unity command recompile --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
unity command console --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
unity command run_tests --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json -- --mode editor --filter WindFieldTests
unity command test_status --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
```
Expected: 9개 전부 `Passed`

- [ ] **Step 7: 커밋한다**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/WindCylinder.cs Runtime/Scripts/Game/WindCylinder.cs.meta \
        Runtime/Scripts/Game/WindField.cs Runtime/Scripts/Game/WindField.cs.meta \
        Tests/EditMode/WindFieldTests.cs Tests/EditMode/WindFieldTests.cs.meta
git diff --cached --name-only
git commit -m "feat(skydive): 위치를 넣으면 바람이 나오는 원기둥 장

시간을 타지 않아 되감아 다시 달려도 같은 답이 나온다. 겹친 바람을 더하는
순서를 고정한다 — 씬 순회 순서에 맡기면 부동소수 덧셈의 마지막 자릿수가
클·서에서 갈린다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: 마스터데이터 `WindLag` 3열 + `SkydiveConfig` 3필드

**Files:**
- Modify: `infrastructure/table/Datas/#SkydiveConfig.xlsx` (열 3개 추가)
- Modify (생성물): `LeagueOfPhysical-MasterData-Client/Runtime.Generated/**`, `LeagueOfPhysical-MasterData-Server/Runtime.Generated/**`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveConfig.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/SkydiveConfigProvider.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/SkydiveConfigProvider.cs`
- Modify (호출부 4곳): `LeagueOfPhysical-Shared/Tests/EditMode/SkydiveMoveSystemTests.cs:13`, `SkydiveWorldTests.cs:13`, `StaminaSystemTests.cs:11`, `LeagueOfPhysical-Client/Assets/Tests/Editor/SkydiveCorrectionFixture.cs:18`

**Interfaces:**
- Consumes: (없음)
- Produces: `LOP.SkydiveConfig`에 `float GlideWindLag`, `float SpreadWindLag`, `float DiveWindLag` — 생성자 인자는 **맨 뒤에 이 순서로** 붙는다: `..., float fallBrake, float glideWindLag, float spreadWindLag, float diveWindLag`

- [ ] **Step 1: 세 레포에 브랜치를 딴다**

```bash
for r in infrastructure LeagueOfPhysical-MasterData-Client LeagueOfPhysical-MasterData-Server LeagueOfPhysical-Client LeagueOfPhysical-Server; do
  cd "C:/Users/re5na/workspace/LOP/$r"
  git fetch origin
  git checkout -b feature/skydive-wind origin/main
done
```
클라 레포에 이미 `docs/skydive-wind-spec` 브랜치가 있다면 그것을 `feature/skydive-wind`로 이어서 쓴다(`git checkout docs/skydive-wind-spec`). 스펙 커밋이 거기 있다.

- [ ] **Step 2: xlsx에 열 3개를 붙인다**

```bash
cd C:/Users/re5na/workspace/LOP/infrastructure/table
python - <<'PY'
import openpyxl
path = 'Datas/#SkydiveConfig.xlsx'
wb = openpyxl.load_workbook(path)
ws = wb.worksheets[0]
cols = [('glide_wind_lag', 0.2), ('spread_wind_lag', 2.06), ('dive_wind_lag', 3.10)]
start = ws.max_column + 1
for i, (name, value) in enumerate(cols):
    c = start + i
    ws.cell(1, c).value = name      # ##var
    ws.cell(2, c).value = 'float'   # ##type
    ws.cell(3, c).value = None      # ##group — 클·서 공통
    ws.cell(4, c).value = name      # ##
    ws.cell(5, c).value = value     # id=1 행
wb.save(path)
print('열', ws.max_column)
PY
```
Expected: `열 28`

- [ ] **Step 3: 생성한다**

```bash
cd C:/Users/re5na/workspace/LOP/infrastructure/table
bash gen.sh
```
Expected: 에러 없이 끝난다. 실패하면 `.NET` 런타임 문제이므로 **거기서 멈추고 보고한다** — 손으로 `.cs`를 고치면 다음 생성 때 조용히 되돌아간다.

- [ ] **Step 4: 생성물을 확인한다**

```bash
grep -n "WindLag" C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client/Runtime.Generated/Scripts/MasterData/SkydiveConfig.cs
grep -n "WindLag" C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server/Runtime.Generated/Scripts/MasterData/SkydiveConfig.cs
```
Expected: 양쪽에 `GlideWindLag`/`SpreadWindLag`/`DiveWindLag` 세 필드

`##group`을 비웠으므로 **클·서 양쪽에 다 나와야 한다.** 한쪽에만 있으면 group 칸을 잘못 채운 것이다.

- [ ] **Step 5: `SkydiveConfig`에 필드 3개를 붙인다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveConfig.cs` — `PoseClearance` 뒤에 필드 선언을 추가한다:

```csharp
        /// <summary>
        /// 패러세일이 바람에 <b>완전히 실리는 데</b> 걸리는 시간(초). 자세가 바람에 다르게
        /// 반응하는 이유는 이 값 하나다 — 넓게 편 몸은 공기가 세게 붙잡아 금방 같이 흐르고,
        /// 좁힌 몸은 공기를 뚫고 지나가 늦게 실린다.
        ///
        /// <para>물리에서 <c>종단속도 ÷ FallApproach</c>로 유도된 값이다. 낙하 가속을 만지면
        /// 이 셋도 같이 따라와야 앞뒤가 맞는다.</para>
        /// </summary>
        public readonly float GlideWindLag;
        /// <summary>대자가 바람에 완전히 실리는 데 걸리는 시간(초).</summary>
        public readonly float SpreadWindLag;
        /// <summary>다이브가 바람에 완전히 실리는 데 걸리는 시간(초). 가장 길다.</summary>
        public readonly float DiveWindLag;
```

생성자 인자를 **맨 뒤에** 추가한다:

```csharp
            float groundMoveSpeed, float groundAccel, float jumpPower, float poseClearance, float fallBrake,
            float glideWindLag, float spreadWindLag, float diveWindLag)
```

생성자 본문 끝에 대입을 추가한다:

```csharp
            GlideWindLag = glideWindLag;
            SpreadWindLag = spreadWindLag;
            DiveWindLag = diveWindLag;
```

- [ ] **Step 6: 양쪽 provider가 새 열을 넘기게 한다**

`LeagueOfPhysical-Client/Assets/Scripts/Game/SkydiveConfigProvider.cs`와
`LeagueOfPhysical-Server/Assets/Scripts/Game/SkydiveConfigProvider.cs` **둘 다**, 마지막 인자 줄을 이렇게 바꾼다:

```csharp
                r.GroundMoveSpeed, r.GroundAccel, r.JumpPower, r.PoseClearance, r.FallBrake,
                r.GlideWindLag, r.SpreadWindLag, r.DiveWindLag);
```

- [ ] **Step 7: 테스트 호출부 4곳을 고친다**

네 파일 모두 `new SkydiveConfig(` 호출의 마지막 인자 뒤에 세 값을 붙인다. 예:

```csharp
                groundMoveSpeed: 4f, groundAccel: 100f, jumpPower: 11f, poseClearance: 5f, fallBrake: 150f,
                glideWindLag: 0.2f, spreadWindLag: 2.06f, diveWindLag: 3.1f);
```

대상: `LeagueOfPhysical-Shared/Tests/EditMode/SkydiveMoveSystemTests.cs`, `SkydiveWorldTests.cs`, `StaminaSystemTests.cs`, `LeagueOfPhysical-Client/Assets/Tests/Editor/SkydiveCorrectionFixture.cs`.

- [ ] **Step 8: 컴파일과 기존 테스트가 통과하는지 확인한다**

```bash
unity command recompile --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
unity command console --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
unity command run_tests --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json -- --mode editor --filter Skydive
unity command test_status --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
```
Expected: CS 에러 0, Skydive 테스트 전부 `Passed` (행동은 안 바뀌었다)

- [ ] **Step 9: 다섯 레포에 각각 커밋한다**

```bash
cd C:/Users/re5na/workspace/LOP/infrastructure
git add table/Datas/#SkydiveConfig.xlsx
git diff --cached --name-only
git commit -m "feat(masterdata): 자세별 바람 지연 3열 추가

바람에 완전히 실리는 데 걸리는 시간. 종단속도 / FallApproach로 유도한 값이라
낙하 가속을 만지면 같이 움직여야 한다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"

cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client
git add Runtime.Generated
git diff --cached --name-only
git commit -m "chore(masterdata): 바람 지연 3열 재생성

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"

cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server
git add Runtime.Generated
git diff --cached --name-only
git commit -m "chore(masterdata): 바람 지연 3열 재생성

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"

cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/SkydiveConfig.cs Tests/EditMode/SkydiveMoveSystemTests.cs Tests/EditMode/SkydiveWorldTests.cs Tests/EditMode/StaminaSystemTests.cs
git diff --cached --name-only
git commit -m "feat(skydive): 자세별 바람 지연을 튜닝값으로 받는다

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"

cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Game/SkydiveConfigProvider.cs Assets/Tests/Editor/SkydiveCorrectionFixture.cs
git diff --cached --name-only
git commit -m "feat(skydive): 바람 지연 3열을 시뮬에 넘긴다

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"

cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git add Assets/Scripts/Game/SkydiveConfigProvider.cs
git diff --cached --name-only
git commit -m "feat(skydive): 바람 지연 3열을 시뮬에 넘긴다

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

> `.meta` 삭제가 스테이지에 섞여 있으면 `gen.sh`의 meta 복구가 안 돈 것이다. `git diff --cached --name-only`로 확인하고, `.meta` 삭제가 보이면 **거기서 멈추고 보고한다.**

---

## Task 3: `WindDrift` 컴포넌트 + `WindDriftSystem`

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/WindDrift.cs`
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/WindDriftSystem.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/WindDriftSystemTests.cs`

**Interfaces:**
- Consumes: `LOP.WindField.SampleAt(System.Numerics.Vector3)`, `LOP.SkydiveConfig.{GlideWindLag, SpreadWindLag, DiveWindLag}`
- Produces:
  - `LOP.WindDrift : GameFramework.World.Component` — 공개 필드 `System.Numerics.Vector3 Value`
  - `LOP.WindDriftSystem` — `void Tick(GameFramework.World.Entity entity, float deltaTime, in SkydiveConfig config, WindField field)`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/WindDriftSystemTests.cs`:

```csharp
using NUnit.Framework;
using System.Numerics;

namespace LOP.Tests
{
    public class WindDriftSystemTests
    {
        const float Tolerance = 1e-3f;

        static SkydiveConfig Config()
            => new SkydiveConfig(
                spreadFallSpeed: 60f, diveFallSpeed: 90f, glideFallSpeed: 6f,
                spreadMoveSpeed: 12f, diveMoveSpeed: 9f, glideMoveSpeed: 14f,
                spreadTurnAccel: 22f, diveTurnAccel: 6f, glideTurnAccel: 18f,
                fallApproach: 29f, postureRate: 4f,
                bodyRadius: 0.4f, bodyHeight: 1.8f, groundY: 0f,
                staminaMax: 100f, glideDrain: 20f, groundRecover: 40f, emergencyGlideTime: 1f,
                groundMoveSpeed: 4f, groundAccel: 100f, jumpPower: 11f, poseClearance: 5f, fallBrake: 150f,
                glideWindLag: 0.2f, spreadWindLag: 2.0f, diveWindLag: 4.0f);

        // 온 코스를 덮는 상승풍 14. 위치를 안 옮겨도 늘 안에 있다.
        static WindField Everywhere(float up = 14f)
        {
            var field = new WindField();
            field.Add(new WindCylinder(new Vector3(0f, 1000f, 0f), 1000f, 2000f, new Vector3(0f, up, 0f)));
            return field;
        }

        static GameFramework.World.Entity Diver(
            float axis = 0f, bool gliding = false,
            SkydiveMotionState state = SkydiveMotionState.Skydiving)
        {
            var entity = new GameFramework.World.Entity("diver-1");
            entity.Add(new GameFramework.World.Transform { Position = new Vector3(0f, 1000f, 0f) });
            entity.Add(new Posture { Axis = axis, Gliding = gliding });
            entity.Add(new MotionState { Value = state });
            entity.Add(new WindDrift());
            return entity;
        }

        static void Run(WindDriftSystem system, GameFramework.World.Entity entity,
                        WindField field, float seconds, float dt = 0.05f)
        {
            int steps = (int)System.Math.Round(seconds / dt);
            for (int i = 0; i < steps; i++)
            {
                system.Tick(entity, dt, Config(), field);
            }
        }

        [Test]
        public void 대자는_SpreadWindLag초에_바람을_다_탄다()
        {
            var entity = Diver(axis: 0f);
            Run(new WindDriftSystem(), entity, Everywhere(), seconds: 2.0f);

            Assert.AreEqual(14f, entity.Get<WindDrift>().Value.Y, Tolerance);
        }

        [Test]
        public void 대자가_다_타기_전에는_비율만큼만_탄다()
        {
            var entity = Diver(axis: 0f);
            Run(new WindDriftSystem(), entity, Everywhere(), seconds: 1.0f);

            // 일정 속도로 다가가므로 절반 시간이면 절반이다.
            Assert.AreEqual(7f, entity.Get<WindDrift>().Value.Y, Tolerance);
        }

        [Test]
        public void 다이브는_같은_시간에_절반만_탄다()
        {
            var entity = Diver(axis: 1f);
            Run(new WindDriftSystem(), entity, Everywhere(), seconds: 2.0f);

            // DiveWindLag 4초 중 2초 = 절반
            Assert.AreEqual(7f, entity.Get<WindDrift>().Value.Y, Tolerance);
        }

        [Test]
        public void 패러세일은_0점2초면_다_탄다()
        {
            var entity = Diver(gliding: true);
            Run(new WindDriftSystem(), entity, Everywhere(), seconds: 0.2f, dt: 0.05f);

            Assert.AreEqual(14f, entity.Get<WindDrift>().Value.Y, Tolerance);
        }

        [Test]
        public void 자세_축_중간은_두_지연_사이다()
        {
            var entity = Diver(axis: 0.5f);
            Run(new WindDriftSystem(), entity, Everywhere(), seconds: 1.5f);

            // 지연 = (2 + 4) / 2 = 3초. 1.5초면 절반.
            Assert.AreEqual(7f, entity.Get<WindDrift>().Value.Y, Tolerance);
        }

        // 들어갈 때만 시간이 걸리고 나올 때 즉시 풀리면, 볼륨을 스치기만 해도 바람이 남지 않는다.
        [Test]
        public void 볼륨을_나가면_같은_시간에_0으로_돌아온다()
        {
            var system = new WindDriftSystem();
            var entity = Diver(axis: 0f);
            Run(system, entity, Everywhere(), seconds: 2.0f);
            Assert.AreEqual(14f, entity.Get<WindDrift>().Value.Y, Tolerance);

            Run(system, entity, new WindField(), seconds: 2.0f);

            Assert.AreEqual(0f, entity.Get<WindDrift>().Value.Y, Tolerance);
        }

        // 발을 딛고 있으면 땅이 잡아 준다. 안 그러면 발판 위에서 걷다가 바람에 끌려간다.
        [Test]
        public void 걸을_때는_바람에_안_실린다()
        {
            var entity = Diver(state: SkydiveMotionState.Walking);
            Run(new WindDriftSystem(), entity, Everywhere(), seconds: 2.0f);

            Assert.AreEqual(0f, entity.Get<WindDrift>().Value.Y, Tolerance);
        }

        [Test]
        public void 옆바람도_같은_규칙으로_실린다()
        {
            var field = new WindField();
            field.Add(new WindCylinder(new Vector3(0f, 1000f, 0f), 1000f, 2000f, new Vector3(20f, 0f, 0f)));
            var entity = Diver(axis: 0f);

            Run(new WindDriftSystem(), entity, field, seconds: 2.0f);

            Assert.AreEqual(20f, entity.Get<WindDrift>().Value.X, Tolerance);
            Assert.AreEqual(0f, entity.Get<WindDrift>().Value.Y, Tolerance);
        }

        [Test]
        public void WindDrift가_없는_몸은_그냥_넘어간다()
        {
            var entity = new GameFramework.World.Entity("no-drift");
            entity.Add(new GameFramework.World.Transform { Position = new Vector3(0f, 1000f, 0f) });

            Assert.DoesNotThrow(() => new WindDriftSystem().Tick(entity, 0.05f, Config(), Everywhere()));
        }
    }
}
```

- [ ] **Step 2: 컴파일이 깨지는 것을 확인한다**

```bash
unity command recompile --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
unity command console --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
```
Expected: `WindDrift`/`WindDriftSystem`을 못 찾는다는 CS0246

- [ ] **Step 3: `WindDrift`를 만든다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/WindDrift.cs`:

```csharp
namespace LOP
{
    /// <summary>
    /// 이 몸이 <b>지금까지 실린</b> 바람. 볼륨에 들어가면 그 바람으로 자라고, 나오면 0으로
    /// 돌아간다 — 걸리는 시간이 자세마다 다르다(<see cref="WindDriftSystem"/>).
    ///
    /// <para>이 지연이 있어서 볼륨 경계를 칼같이 잘라도 된다. 들락날락이 저절로 부드러워지므로
    /// 경계를 흐리게 만드는 코드가 따로 필요 없다.</para>
    ///
    /// 데이터만 — 바꾸는 것은 <see cref="WindDriftSystem"/>이다.
    /// </summary>
    public class WindDrift : GameFramework.World.Component
    {
        public System.Numerics.Vector3 Value;
    }
}
```

- [ ] **Step 4: `WindDriftSystem`을 만든다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/WindDriftSystem.cs`:

```csharp
using System;
using System.Numerics;

namespace LOP
{
    /// <summary>
    /// 몸을 바람에 실어 준다. 자세가 정하는 것은 <b>얼마나 빨리 실리나</b> 하나다 — 넓게 편
    /// 몸은 공기가 세게 붙잡아 금방 같이 흐르고, 좁힌 몸은 공기를 뚫고 지나가 늦게 실린다.
    /// 그래서 짧은 구간에서는 편 자세만 바람을 다 받는다.
    /// </summary>
    public class WindDriftSystem
    {
        public void Tick(GameFramework.World.Entity entity, float deltaTime,
                         in SkydiveConfig config, WindField field)
        {
            var drift = entity.Get<WindDrift>();
            var transform = entity.Get<GameFramework.World.Transform>();
            if (drift == null || transform == null || field == null)
            {
                return;
            }

            var state = entity.Get<MotionState>()?.Value ?? SkydiveMotionState.Falling;

            // 발을 딛고 있으면 땅이 잡아 준다 — 걷는 몸을 바람이 밀지 않는다.
            Vector3 target = state == SkydiveMotionState.Walking
                ? Vector3.Zero
                : field.SampleAt(transform.Position);

            float lag = LagOf(entity.Get<Posture>(), config);
            if (lag <= 0f)
            {
                drift.Value = target;   // 지연 없음. 나누기 전에 걸러낸다
                return;
            }

            // 들어갈 때도 나올 때도 lag초가 걸리게 한다. 목표만 보고 속도를 정하면 나올 때는
            // 목표가 0이라 속도도 0이 되어 영영 안 빠진다.
            float reference = Math.Max(target.Length(), drift.Value.Length());
            drift.Value = MoveTowards(drift.Value, target, reference / lag * deltaTime);
        }

        private static float LagOf(Posture posture, in SkydiveConfig config)
        {
            if (posture == null)
            {
                return config.SpreadWindLag;
            }
            if (posture.Gliding)
            {
                return config.GlideWindLag;
            }

            float axis = posture.Axis < 0f ? 0f : (posture.Axis > 1f ? 1f : posture.Axis);
            return config.SpreadWindLag + (config.DiveWindLag - config.SpreadWindLag) * axis;
        }

        private static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxStep)
        {
            Vector3 diff = target - current;
            float distance = diff.Length();
            if (distance <= maxStep || distance == 0f)
            {
                return target;
            }
            return current + diff * (maxStep / distance);
        }
    }
}
```

- [ ] **Step 5: 테스트가 통과하는지 확인한다**

```bash
unity command recompile --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
unity command console --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
unity command run_tests --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json -- --mode editor --filter WindDriftSystemTests
unity command test_status --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
```
Expected: 9개 전부 `Passed`

- [ ] **Step 6: 커밋한다**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/WindDrift.cs Runtime/Scripts/Game/WindDrift.cs.meta \
        Runtime/Scripts/Game/WindDriftSystem.cs Runtime/Scripts/Game/WindDriftSystem.cs.meta \
        Tests/EditMode/WindDriftSystemTests.cs Tests/EditMode/WindDriftSystemTests.cs.meta
git diff --cached --name-only
git commit -m "feat(skydive): 몸이 자세별 속도로 바람에 실린다

넓게 편 몸은 공기가 세게 붙잡아 금방 같이 흐르고, 좁힌 몸은 공기를 뚫고
지나가 늦게 실린다. 들어갈 때와 나올 때가 같은 시간이 걸리게 해서, 볼륨을
스치기만 해도 바람이 남지 않게 한다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: `SkydiveMoveSystem`이 목표 속도에 바람을 얹는다

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveMoveSystem.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/SkydiveMoveSystemTests.cs` (테스트 추가)

**Interfaces:**
- Consumes: `LOP.WindDrift.Value`
- Produces: 시그니처 변경 없음. `SkydiveMoveSystem.Tick`이 엔티티의 `WindDrift`를 읽어 목표에 반영한다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`SkydiveMoveSystemTests.cs`의 기존 `Diver` 헬퍼 아래에 헬퍼와 테스트를 추가한다. 기존 `Diver`는 `WindDrift`를 안 붙이므로 새 헬퍼를 쓴다:

```csharp
        static Entity DiverInWind(float axis, bool gliding, Vector3 velocity, Vector3 wind,
                                  float h = 0f, float v = 0f)
        {
            var entity = Diver(axis, gliding, velocity, Vector3.zero, h, v);
            entity.Add(new WindDrift { Value = wind.ToNumerics() });
            return entity;
        }

        static void Settle(SkydiveMoveSystem system, Entity entity, int ticks = 200, float dt = 0.02f)
        {
            for (int i = 0; i < ticks; i++)
            {
                system.Tick(entity, dt, Config());
            }
        }

        [Test]
        public void 상승풍을_다_탄_패러세일은_위로_간다()
        {
            // 목표 하강 6 − 상승풍 14 = −8. 즉 초속 8로 올라간다.
            var entity = DiverInWind(axis: 0f, gliding: true,
                                     velocity: new Vector3(0f, -6f, 0f), wind: new Vector3(0f, 14f, 0f));

            Settle(new SkydiveMoveSystem(), entity);

            Assert.AreEqual(8f, entity.Get<Velocity>().Linear.Y, Tolerance);
        }

        [Test]
        public void 상승풍을_조금만_탄_다이브는_거의_그대로_떨어진다()
        {
            // 40m 구간을 다이브로 지나면 상승풍 14 중 약 3만 탄다(스펙 3.5).
            var entity = DiverInWind(axis: 1f, gliding: false,
                                     velocity: new Vector3(0f, -90f, 0f), wind: new Vector3(0f, 3f, 0f));

            Settle(new SkydiveMoveSystem(), entity);

            float fall = -entity.Get<Velocity>().Linear.Y;
            Assert.That(fall, Is.GreaterThan(90f * 0.95f), "다이브는 상승풍을 거의 안 받아야 한다");
        }

        [Test]
        public void 손을_떼면_횡풍_속도로_흘러간다()
        {
            var entity = DiverInWind(axis: 0f, gliding: false,
                                     velocity: Vector3.zero, wind: new Vector3(10f, 0f, 0f));

            Settle(new SkydiveMoveSystem(), entity);

            Assert.AreEqual(10f, entity.Get<Velocity>().Linear.X, Tolerance);
        }

        [Test]
        public void 스틱으로_밀면_횡풍을_상류로_이길_수_있다()
        {
            // 대자 최고 속도 12 > 횡풍 10 → 상류로 초속 2
            var entity = DiverInWind(axis: 0f, gliding: false,
                                     velocity: Vector3.zero, wind: new Vector3(10f, 0f, 0f), h: -1f);

            Settle(new SkydiveMoveSystem(), entity);

            Assert.AreEqual(-2f, entity.Get<Velocity>().Linear.X, Tolerance);
        }

        [Test]
        public void 최고_속도보다_센_횡풍은_못_이긴다()
        {
            // 대자 최고 속도 12 < 횡풍 15 → 끝까지 밀어도 하류로 초속 3 (항공의 바람 삼각형)
            var entity = DiverInWind(axis: 0f, gliding: false,
                                     velocity: Vector3.zero, wind: new Vector3(15f, 0f, 0f), h: -1f);

            Settle(new SkydiveMoveSystem(), entity);

            Assert.AreEqual(3f, entity.Get<Velocity>().Linear.X, Tolerance);
        }

        [Test]
        public void WindDrift가_없는_몸은_예전과_똑같이_떨어진다()
        {
            var entity = Diver(axis: 0f, gliding: false, velocity: Vector3.zero, position: Vector3.zero);

            Settle(new SkydiveMoveSystem(), entity);

            Assert.AreEqual(-60f, entity.Get<Velocity>().Linear.Y, Tolerance);
        }
```

- [ ] **Step 2: 테스트가 실패하는 것을 확인한다**

```bash
unity command recompile --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
unity command console --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
unity command run_tests --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json -- --mode editor --filter SkydiveMoveSystemTests
unity command test_status --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
```
Expected: 새 테스트 5개 실패(바람이 아직 안 먹는다), `WindDrift가_없는_몸은...`은 통과

- [ ] **Step 3: `SkydiveMoveSystem`에 바람을 얹는다**

`Tick` 안에서 `var state = ...` 다음 줄에 추가한다:

```csharp
            // 바람은 목표를 옮긴다. 속도에 직접 더하면 다음 틱 수렴이 도로 지운다 — 이 게임의
            // 속도는 "목표로 끌려가는" 구조라 목표를 건드려야 남는다.
            var wind = entity.Get<WindDrift>()?.Value ?? System.Numerics.Vector3.Zero;
```

`targetFall`을 정하는 if/else 블록 **바로 아래**, `fallStep`을 고르기 전에 한 줄 추가한다:

```csharp
            // 상승풍이면 목표 하강 속도가 줄고, 14를 넘으면 부호가 뒤집혀 올라간다.
            targetFall -= wind.Y;
```

좌우 호출에 바람을 넘긴다:

```csharp
            else if (state == SkydiveMotionState.Skydiving)
            {
                Drift(ref linear, posture, inputX, inputZ, deltaTime, config, wind);
            }
```

`Drift`의 시그니처와 마지막 두 줄을 바꾼다:

```csharp
        private static void Drift(ref System.Numerics.Vector3 linear, Posture posture,
            float inputX, float inputZ, float deltaTime, in SkydiveConfig config,
            System.Numerics.Vector3 wind)
        {
```

```csharp
            // 최고 속도는 공기에 대한 값이다 — 몸을 기울여 공기를 밀어 방향을 얻으니 물리적으로도
            // 그렇다. 그래서 바람이 최고 속도보다 세면 끝까지 밀어도 상류로 못 간다.
            linear.X = Approach(linear.X, inputX * maxSide + wind.X, turnAccel * deltaTime);
            linear.Z = Approach(linear.Z, inputZ * maxSide + wind.Z, turnAccel * deltaTime);
```

> **`Falling`(선 채로 낙하)에서는 좌우 바람이 안 먹는다** — 그 상태는 원래 좌우 입력을 안 받고, 1초를 안 넘게 짧다. 세로 바람은 `targetFall`이 분기 밖에 있어 그대로 먹는다.

- [ ] **Step 4: 테스트가 통과하는지 확인한다**

```bash
unity command recompile --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
unity command console --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
unity command run_tests --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json -- --mode editor --filter SkydiveMoveSystemTests
unity command test_status --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
```
Expected: 새 6개 포함 전부 `Passed`

- [ ] **Step 5: 커밋한다**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/SkydiveMoveSystem.cs Tests/EditMode/SkydiveMoveSystemTests.cs
git diff --cached --name-only
git commit -m "feat(skydive): 바람이 목표 속도를 옮긴다

속도에 직접 더하면 다음 틱 수렴이 도로 지운다. 좌우 최고 속도는 공기에 대한
값이라, 바람이 그보다 세면 끝까지 밀어도 상류로 못 간다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 5: `SkydiveWorld` 배선 + 되감기

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveWorld.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveSavedState.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/SkydiveWorldTests.cs` (테스트 추가 + 생성자 호출부 수정)

**Interfaces:**
- Consumes: `LOP.WindDriftSystem.Tick(...)`, `LOP.WindField`
- Produces: `SkydiveWorld` 생성자가 바뀐다 —
  `SkydiveWorld(EntityRegistry, WorldEventBuffer, SkydiveMoveSystem, StaminaSystem, WindDriftSystem, WindField, SkydiveConfig, ICollisionQuery, int layerMask)`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`SkydiveWorldTests.cs`의 `World` 헬퍼를 바꾸고 테스트를 추가한다:

```csharp
        static SkydiveWorld World(EntityRegistry registry,
                                  GameFramework.Physics.ICollisionQuery query = null,
                                  WindField wind = null)
            => new SkydiveWorld(registry, new WorldEventBuffer(),
                                new SkydiveMoveSystem(), new StaminaSystem(),
                                new WindDriftSystem(), wind ?? new WindField(), Config(),
                                query ?? new HalfSpaceQuery(), layerMask: ~0);
```

기존 `Diver` 헬퍼에 `e.Add(new WindDrift());`를 `e.Add(new MotionState());` 다음 줄에 추가한다.

테스트를 추가한다:

```csharp
        [Test]
        public void 상승풍_속에서는_천천히_떨어진다()
        {
            var registry = new EntityRegistry();
            registry.Add(Diver("a"));

            var wind = new WindField();
            wind.Add(new WindCylinder(
                new System.Numerics.Vector3(0f, 1000f, 0f), 1000f, 2000f,
                new System.Numerics.Vector3(0f, 14f, 0f)));
            var world = World(registry, wind: wind);

            var noWindRegistry = new EntityRegistry();
            noWindRegistry.Add(Diver("a"));
            var noWindWorld = World(noWindRegistry);

            for (long tick = 1; tick <= 100; tick++)
            {
                world.Tick(tick, 0.02f);
                noWindWorld.Tick(tick, 0.02f);
            }

            Assert.That(HeightOf(registry, "a"), Is.GreaterThan(HeightOf(noWindRegistry, "a")),
                        "상승풍을 받은 쪽이 덜 내려가야 한다");
        }

        [Test]
        public void 되감으면_실린_바람도_돌아온다()
        {
            var registry = new EntityRegistry();
            registry.Add(Diver("a"));

            var wind = new WindField();
            wind.Add(new WindCylinder(
                new System.Numerics.Vector3(0f, 1000f, 0f), 1000f, 2000f,
                new System.Numerics.Vector3(9f, 0f, 0f)));
            var world = World(registry, wind: wind);

            for (long tick = 1; tick <= 40; tick++)
            {
                world.Tick(tick, 0.02f);
                world.SaveState(tick);
            }
            float atTwenty = registry.Get("a").Get<WindDrift>().Value.X;

            for (long tick = 41; tick <= 80; tick++)
            {
                world.Tick(tick, 0.02f);
                world.SaveState(tick);
            }
            Assert.That(registry.Get("a").Get<WindDrift>().Value.X, Is.Not.EqualTo(atTwenty));

            Assert.IsTrue(world.LoadState(40));

            Assert.AreEqual(atTwenty, registry.Get("a").Get<WindDrift>().Value.X, Tolerance);
        }
```

- [ ] **Step 2: 테스트가 실패하는 것을 확인한다**

```bash
unity command recompile --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
unity command console --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
```
Expected: `SkydiveWorld` 생성자 인자 개수가 안 맞는다는 CS1729

- [ ] **Step 3: `SkydiveWorld`에 바람 단계를 넣는다**

필드와 생성자 인자를 추가한다(`_staminaSystem` 다음):

```csharp
        private readonly WindDriftSystem _windDriftSystem;
        private readonly WindField _windField;
```

```csharp
        public SkydiveWorld(
            GameFramework.World.EntityRegistry entityRegistry,
            GameFramework.World.WorldEventBuffer eventBuffer,
            SkydiveMoveSystem moveSystem,
            StaminaSystem staminaSystem,
            WindDriftSystem windDriftSystem,
            WindField windField,
            SkydiveConfig config,
            ICollisionQuery collisionQuery,
            int layerMask)
            : base(entityRegistry, eventBuffer)
        {
            _moveSystem = moveSystem;
            _staminaSystem = staminaSystem;
            _windDriftSystem = windDriftSystem;
            _windField = windField;
            _config = config;
            _collisionQuery = collisionQuery;
            _layerMask = layerMask;
        }
```

`Mutation`에서 `ApplyPostureInput` 루프와 `_moveSystem.Tick` 루프 **사이에** 넣는다:

```csharp
            // 자세가 정해진 뒤에 실린다 — 자세가 곧 "얼마나 빨리 실리나"이므로, 앞에 두면
            // 한 틱 전 자세로 바람을 받게 된다.
            for (int i = 0; i < _divers.Count; i++)
            {
                _windDriftSystem.Tick(_divers[i], deltaTime, _config, _windField);
            }
```

클래스 XML 주석의 틱 설명도 고친다:

```csharp
    /// 한 틱: ① 입력을 자세로 반영(축은 정해진 속도로만 움직인다) → ② 바람에 실린다(자세가
    /// 빠르기를 정한다) → ③ 자세와 바람이 목표 속도를 정한다 → ④ 맵에 막히면 벽까지만 옮긴다
    /// (미끄러짐·접지 판정) → ⑤ 방금 나온 접지로 스태미나 소모·회복.
    /// 레이저 판정은 Detection에 들어오지만(슬라이스 4) 지금은 비어 있다.
```

- [ ] **Step 4: `SkydiveSavedState`에 바람을 담는다**

필드와 생성자 인자, `Capture`, `RestoreTo`에 각각 추가한다:

```csharp
        public readonly System.Numerics.Vector3 Drift;
```

생성자 인자 맨 뒤에 `System.Numerics.Vector3 drift`를 붙이고 `Drift = drift;`를 대입한다.

`Capture`의 반환에 마지막 인자로:

```csharp
                entity.Get<WindDrift>()?.Value ?? System.Numerics.Vector3.Zero);
```

`RestoreTo` 끝에:

```csharp
            // 바람도 되돌린다 — 안 되돌리면 재생 중 실린 바람이 라이브와 달라져 같은 입력이
            // 다른 궤적을 만든다.
            var drift = entity.Get<WindDrift>();
            if (drift != null)
            {
                drift.Value = Drift;
            }
```

- [ ] **Step 5: 테스트가 통과하는지 확인한다**

```bash
unity command recompile --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
unity command console --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
unity command run_tests --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json -- --mode editor --filter SkydiveWorldTests
unity command test_status --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
```
Expected: 새 2개 포함 전부 `Passed`

- [ ] **Step 6: 커밋한다**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/SkydiveWorld.cs Runtime/Scripts/Game/SkydiveSavedState.cs Tests/EditMode/SkydiveWorldTests.cs
git diff --cached --name-only
git commit -m "feat(skydive): 틱에 바람 단계를 넣고 되감기에 담는다

자세를 정한 뒤에 실린다 — 자세가 곧 실리는 빠르기라 앞에 두면 한 틱 전
자세로 바람을 받는다. 실린 바람을 되감기에 안 담으면 재생이 라이브와 갈린다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 6: `WindVolume` 마커 + 양쪽 DI 배선

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/WindVolume.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/baegames.LOP.Shared.Runtime.asmdef` (VContainer 추가)
- Modify: `LeagueOfPhysical-Shared/Tests/EditMode/baegames.LOP.Shared.Tests.EditMode.asmdef` (VContainer 추가)
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/SkydiveLifetimeScope.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/SkydiveLifetimeScope.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Entity/SkydivePlayerCreator.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Entity/SkydivePlayerCreator.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/WindVolumeTests.cs`

**Interfaces:**
- Consumes: `LOP.WindField`, `LOP.WindCylinder`
- Produces: `LOP.WindVolume : MonoBehaviour` — 공개 필드 `float Radius`, `float Height`, `UnityEngine.Vector3 Wind`; `[Inject] public void Construct(WindField field)`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/WindVolumeTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class WindVolumeTests
    {
        const float Tolerance = 1e-3f;

        static WindVolume Place(Vector3 position, Vector3 wind, float radius = 25f, float height = 120f)
        {
            var go = new GameObject("wind-test");
            go.transform.position = position;
            var volume = go.AddComponent<WindVolume>();
            volume.Radius = radius;
            volume.Height = height;
            volume.Wind = wind;
            return volume;
        }

        [Test]
        public void 주입받으면_스스로_등록한다()
        {
            var field = new WindField();
            var volume = Place(new Vector3(0f, 1000f, 0f), new Vector3(0f, 14f, 0f));
            try
            {
                volume.Construct(field);

                Assert.AreEqual(1, field.Count);
                Assert.AreEqual(14f,
                    field.SampleAt(new System.Numerics.Vector3(0f, 1000f, 0f)).Y, Tolerance);
            }
            finally
            {
                Object.DestroyImmediate(volume.gameObject);
            }
        }

        // 라운드가 여러 판이면 맵을 다시 로드한다. 안 빼면 바람이 두 배가 된다.
        [Test]
        public void 파괴되면_스스로_빠진다()
        {
            var field = new WindField();
            var volume = Place(new Vector3(0f, 1000f, 0f), new Vector3(0f, 14f, 0f));
            volume.Construct(field);

            Object.DestroyImmediate(volume.gameObject);

            Assert.AreEqual(0, field.Count);
        }

        [Test]
        public void 마커_위치가_원기둥_중심이_된다()
        {
            var field = new WindField();
            var volume = Place(new Vector3(30f, 1900f, 30f), new Vector3(0f, 14f, 0f), radius: 25f);
            try
            {
                volume.Construct(field);

                Assert.AreEqual(14f,
                    field.SampleAt(new System.Numerics.Vector3(30f, 1900f, 30f)).Y, Tolerance);
                Assert.AreEqual(0f,
                    field.SampleAt(new System.Numerics.Vector3(0f, 1900f, 0f)).Length(), Tolerance);
            }
            finally
            {
                Object.DestroyImmediate(volume.gameObject);
            }
        }
    }
}
```

- [ ] **Step 2: asmdef 두 개에 VContainer를 넣는다**

`LeagueOfPhysical-Shared/Runtime/baegames.LOP.Shared.Runtime.asmdef`의 `references`에 `"VContainer"`를 추가한다:

```json
    "references": ["baegames.GameFramework.Runtime", "baegames.GameFramework.World", "Unity.Addressables", "Unity.ResourceManager", "VContainer"],
```

`LeagueOfPhysical-Shared/Tests/EditMode/baegames.LOP.Shared.Tests.EditMode.asmdef`의 `references`에도 `"VContainer"`를 추가한다.

> asmdef 참조는 전이되지 않는다 — `GameFramework.Runtime`이 VContainer를 참조해도 LOP-Shared가 물려받지 않는다. 토폴로지 문서(`lop-repo-topology.md`)의 use-side 계약이 "Shared가 사용하기 시작할 때 references에 추가"로 이미 허용한 변경이다.

- [ ] **Step 3: `WindVolume`을 만든다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/WindVolume.cs`:

```csharp
using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 맵 씬에 놓는 바람 표시. 맵이 올라올 때 <see cref="WindField"/>를 주입받아 스스로 등록한다
    /// (<c>GameLifetimeScope</c>가 <c>sceneLoaded</c>를 듣고 <c>InjectSceneObjects</c>를 부른다).
    ///
    /// <see cref="SpawnPoint"/>와 같은 이유로 <b>공용 패키지</b>에 있다: 맵 씬은 클라에서 만들고
    /// 서버가 읽는데, 스크립트가 한쪽에만 있으면 반대쪽에서 missing script가 되고 그 빈 컴포넌트가
    /// 씬 주입을 끊는다.
    ///
    /// <para>(Unreal의 <c>APhysicsVolume</c>에 대응 — 볼륨이 그 안의 운동 규칙을 덮어쓴다.
    /// Unity의 <c>WindZone</c>은 나뭇잎·천 연출 전용이라 캐릭터에 안 먹어 쓸 수 없다.)</para>
    /// </summary>
    [SceneInjectMonoBehaviour]
    public class WindVolume : MonoBehaviour
    {
        /// <summary>원기둥 반지름. 기류 기둥은 좁게, 횡풍 구간은 코스를 다 덮게 넓힌다.</summary>
        public float Radius = 25f;

        /// <summary>원기둥 높이. 이 값이 <b>누가 바람을 느끼는지</b>를 정한다 — 짧으면 패러세일만, 구간을 다 덮으면 셋 다.</summary>
        public float Height = 120f;

        /// <summary>방향 × 세기 (m/s).</summary>
        public Vector3 Wind = new Vector3(0f, 14f, 0f);

        private WindField field;
        private WindCylinder cylinder;

        [Inject]
        public void Construct(WindField field)
        {
            this.field = field;
            cylinder = new WindCylinder(
                transform.position.ToNumerics(), Radius, Height, Wind.ToNumerics());
            field.Add(cylinder);
        }

        private void OnDestroy()
        {
            // 라운드가 여러 판이면 맵을 다시 로드한다 — 안 빼면 바람이 두 배가 된다.
            if (field != null && cylinder != null)
            {
                field.Remove(cylinder);
            }
        }

        // 배치가 곧 코스 설계다. 에디터에서 어디에 얼마나 부는지 보이게 한다.
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireCube(transform.position, new Vector3(Radius * 2f, Height, Radius * 2f));
            Gizmos.DrawRay(transform.position, Wind);
        }
    }
}
```

- [ ] **Step 4: 양쪽 `SkydiveLifetimeScope`에 등록한다**

`LeagueOfPhysical-Client/Assets/Scripts/Game/SkydiveLifetimeScope.cs` — `builder.Register<StaminaSystem>` 줄 다음에 추가하고, `SkydiveWorld` 생성 인자를 바꾼다:

```csharp
            builder.Register<StaminaSystem>(Lifetime.Singleton);
            // 맵 씬의 WindVolume 마커가 맵 로드 시 여기에 자기를 넣는다.
            builder.Register<WindField>(Lifetime.Singleton);
            builder.Register<WindDriftSystem>(Lifetime.Singleton);
            builder.Register<SkydiveWorld>(c => new SkydiveWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<SkydiveMoveSystem>(),
                c.Resolve<StaminaSystem>(),
                c.Resolve<WindDriftSystem>(),
                c.Resolve<WindField>(),
                c.Resolve<SkydiveConfig>(),
                c.Resolve<GameFramework.Physics.ICollisionQuery>(),
```

`LeagueOfPhysical-Server/Assets/Scripts/Game/SkydiveLifetimeScope.cs`에도 **똑같이** 두 줄을 등록하고 생성 인자를 바꾼다. 한쪽만 하면 컴파일은 통과하고 그 게임만 못 들어간다.

- [ ] **Step 5: 양쪽 `SkydivePlayerCreator`에 컴포넌트를 붙인다**

`LeagueOfPhysical-Client/Assets/Scripts/Entity/SkydivePlayerCreator.cs`와
`LeagueOfPhysical-Server/Assets/Scripts/Entity/SkydivePlayerCreator.cs` **둘 다**, `worldEntity.Add(new Stamina { ... });` 다음 줄에 추가한다:

```csharp
            worldEntity.Add(new WindDrift());
```

- [ ] **Step 6: 테스트가 통과하는지 확인한다**

```bash
unity command recompile --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
unity command console --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
unity command run_tests --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json -- --mode editor --filter WindVolumeTests
unity command test_status --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
```
Expected: 3개 전부 `Passed`

- [ ] **Step 7: 등록처 개수를 눈으로 대조한다**

```bash
grep -rn "Register<WindField>" C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts
grep -rln "class SkydiveLifetimeScope" C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts
```
Expected: 양쪽 다 2줄씩 — 등록 개수와 `SkydiveLifetimeScope` 개수가 같아야 한다

- [ ] **Step 8: 세 레포에 커밋한다**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/WindVolume.cs Runtime/Scripts/Game/WindVolume.cs.meta \
        Runtime/baegames.LOP.Shared.Runtime.asmdef \
        Tests/EditMode/baegames.LOP.Shared.Tests.EditMode.asmdef \
        Tests/EditMode/WindVolumeTests.cs Tests/EditMode/WindVolumeTests.cs.meta
git diff --cached --name-only
git commit -m "feat(skydive): 맵의 바람 마커가 스스로 등록한다

맵 씬 오브젝트에 DI를 주입하는 통로를 그대로 쓴다 — 시뮬이 첫 틱에 씬을 훑는
우회가 필요 없다. 라운드 재로드에서 바람이 두 배가 되지 않게 파괴 시 뺀다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"

cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Game/SkydiveLifetimeScope.cs Assets/Scripts/Entity/SkydivePlayerCreator.cs
git diff --cached --name-only
git commit -m "feat(skydive): 바람 장과 몸의 실린 바람을 배선한다

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"

cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git add Assets/Scripts/Game/SkydiveLifetimeScope.cs Assets/Scripts/Entity/SkydivePlayerCreator.cs
git diff --cached --name-only
git commit -m "feat(skydive): 바람 장과 몸의 실린 바람을 배선한다

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 7: `SkydiveWindReach` — 코스가 막혔는지 검사하는 산수

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveWindReach.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/SkydiveWindReachTests.cs`

**Interfaces:**
- Consumes: (없음)
- Produces: `static class LOP.SkydiveWindReach` —
  - `static float DriftDistance(float windSpeed, float bandHeight, float fallSpeed, float lag)`
  - `static float SelfReach(float moveSpeed, float turnAccel, float dropHeight, float fallSpeed)`
  - `static bool CanReach(float requiredX, float requiredZ, float driftX, float driftZ, float selfReach)`

> **왜 이 계산이 필요한가.** 역풍은 밀린 거리와 필요 이동이 *더해져서*, 구간 전체를 덮는 12 m/s 역풍 하나로 **아무도 못 지나가는 구간**이 만들어진다. 그렇게 되면 에러 없이 "이 판은 왜 안 끝나지"로만 보인다. 배치를 굽기 전에 이 산수로 막는다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/SkydiveWindReachTests.cs`:

```csharp
using NUnit.Framework;

namespace LOP.Tests
{
    public class SkydiveWindReachTests
    {
        // 스펙 5.5의 값
        const float SpreadFall = 60f, SpreadMove = 12f, SpreadTurn = 22f, SpreadLag = 2.06f;
        const float DiveFall = 90f, DiveMove = 9f, DiveTurn = 6f, DiveLag = 3.10f;

        [Test]
        public void 구간을_다_덮는_강한_순풍은_다이브를_58미터_민다()
        {
            float drift = SkydiveWindReach.DriftDistance(
                windSpeed: 20f, bandHeight: 400f, fallSpeed: DiveFall, lag: DiveLag);

            Assert.AreEqual(57.8f, drift, 0.5f);
        }

        [Test]
        public void 같은_바람이_대자는_113미터_민다()
        {
            float drift = SkydiveWindReach.DriftDistance(
                windSpeed: 20f, bandHeight: 400f, fallSpeed: SpreadFall, lag: SpreadLag);

            Assert.AreEqual(112.8f, drift, 0.5f);
        }

        // 구간보다 짧게 머물면 아직 다 안 실려서, 실린 비율을 시간에 곱한 만큼만 밀린다.
        [Test]
        public void 짧은_구간은_거의_안_민다()
        {
            float drift = SkydiveWindReach.DriftDistance(
                windSpeed: 10f, bandHeight: 40f, fallSpeed: SpreadFall, lag: SpreadLag);

            Assert.AreEqual(1.08f, drift, 0.1f);
        }

        [Test]
        public void 바람이_없으면_안_민다()
        {
            Assert.AreEqual(0f, SkydiveWindReach.DriftDistance(0f, 400f, SpreadFall, SpreadLag), 1e-4f);
            Assert.AreEqual(0f, SkydiveWindReach.DriftDistance(20f, 0f, SpreadFall, SpreadLag), 1e-4f);
        }

        [Test]
        public void 대자는_한_구간에_77미터쯤_간다()
        {
            float reach = SkydiveWindReach.SelfReach(SpreadMove, SpreadTurn, dropHeight: 400f, fallSpeed: SpreadFall);

            Assert.AreEqual(76.8f, reach, 0.5f);
        }

        [Test]
        public void 다이브는_한_구간에_33미터쯤_간다()
        {
            float reach = SkydiveWindReach.SelfReach(DiveMove, DiveTurn, dropHeight: 400f, fallSpeed: DiveFall);

            Assert.AreEqual(33.2f, reach, 0.5f);
        }

        // 순풍이 목표 쪽으로 밀면 자력이 모자라도 닿는다 — 이 코스의 요점(스펙 5.4).
        [Test]
        public void 순풍을_타면_다이브도_60미터를_간다()
        {
            Assert.IsTrue(SkydiveWindReach.CanReach(
                requiredX: 0f, requiredZ: -60f, driftX: 0f, driftZ: -57.8f, selfReach: 33.2f));
        }

        [Test]
        public void 순풍이_없으면_다이브는_60미터를_못_간다()
        {
            Assert.IsFalse(SkydiveWindReach.CanReach(0f, -60f, 0f, 0f, 33.2f));
        }

        // 역풍은 밀린 거리와 필요 이동이 더해진다.
        [Test]
        public void 구간을_다_덮는_역풍은_대자도_못_지나가게_만든다()
        {
            float drift = SkydiveWindReach.DriftDistance(12f, 400f, SpreadFall, SpreadLag);

            Assert.IsFalse(SkydiveWindReach.CanReach(-55f, 0f, drift, 0f, 76.8f));
        }

        [Test]
        public void 짧게_깐_역풍은_대자가_버틴다()
        {
            float drift = SkydiveWindReach.DriftDistance(10f, 150f, SpreadFall, SpreadLag);

            Assert.IsTrue(SkydiveWindReach.CanReach(-55f, 0f, drift, 0f, 76.8f));
        }
    }
}
```

- [ ] **Step 2: 테스트가 실패하는 것을 확인한다**

```bash
unity command recompile --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
unity command console --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
```
Expected: `SkydiveWindReach`를 못 찾는다는 CS0103

- [ ] **Step 3: `SkydiveWindReach`를 만든다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveWindReach.cs`:

```csharp
namespace LOP
{
    /// <summary>
    /// 배치가 통과 가능한지 재는 산수. <see cref="SkydiveReach"/>가 "구멍 사이가 닿는 거리인가"를
    /// 보듯, 여기는 <b>바람이 낀 채로도 닿는가</b>를 본다.
    ///
    /// <para>역풍은 밀린 거리와 필요 이동이 더해져서, 구간 전체를 덮는 12 m/s 역풍 하나만으로
    /// 아무도 못 지나가는 구간이 만들어진다. 그러면 에러 없이 판이 안 끝나는 것으로만 보이므로
    /// 굽기 전에 여기서 막는다.</para>
    /// </summary>
    public static class SkydiveWindReach
    {
        /// <summary>
        /// 바람이 미는 거리. 몸은 <c>lag</c>초에 걸쳐 일정 속도로 바람에 실리므로, 실린 비율은
        /// <c>min(1, 지난시간/lag)</c>이고 밀린 거리는 그것을 머문 시간만큼 쌓은 값이다.
        /// </summary>
        public static float DriftDistance(float windSpeed, float bandHeight, float fallSpeed, float lag)
        {
            if (fallSpeed <= 0f || bandHeight <= 0f)
            {
                return 0f;
            }

            float time = bandHeight / fallSpeed;
            if (lag <= 0f)
            {
                return windSpeed * time;
            }

            return time >= lag
                ? windSpeed * (time - lag * 0.5f)               // 다 실린 뒤로는 그대로 흐른다
                : windSpeed * time * time / (2f * lag);         // 다 실리기 전에 빠져나간다
        }

        /// <summary>
        /// 자기 힘으로 갈 수 있는 옆 거리. 최고 속도까지 붙는 데 걸리는 시간만큼 손해를 뺀다.
        /// </summary>
        public static float SelfReach(float moveSpeed, float turnAccel, float dropHeight, float fallSpeed)
        {
            if (fallSpeed <= 0f || dropHeight <= 0f)
            {
                return 0f;
            }

            float time = dropHeight / fallSpeed;
            if (turnAccel <= 0f)
            {
                return 0f;
            }

            float rampTime = moveSpeed / turnAccel;
            return rampTime >= time
                ? 0.5f * turnAccel * time * time                // 최고 속도에 닿기 전에 구간이 끝난다
                : moveSpeed * (time - rampTime * 0.5f);
        }

        /// <summary>
        /// 바람에 밀린 자리에서 자기 힘으로 구멍까지 닿나. 순풍이면 밀린 만큼이 이득이고
        /// 역풍이면 그만큼 더 가야 하는데, 그 둘이 이 뺄셈 하나로 같이 나온다.
        /// </summary>
        public static bool CanReach(float requiredX, float requiredZ,
                                    float driftX, float driftZ, float selfReach)
        {
            float dx = requiredX - driftX;
            float dz = requiredZ - driftZ;
            return dx * dx + dz * dz <= selfReach * selfReach;
        }
    }
}
```

- [ ] **Step 4: 테스트가 통과하는지 확인한다**

```bash
unity command recompile --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
unity command console --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
unity command run_tests --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json -- --mode editor --filter SkydiveWindReachTests
unity command test_status --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
```
Expected: 10개 전부 `Passed`

- [ ] **Step 5: 커밋한다**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/SkydiveWindReach.cs Runtime/Scripts/Game/SkydiveWindReach.cs.meta \
        Tests/EditMode/SkydiveWindReachTests.cs Tests/EditMode/SkydiveWindReachTests.cs.meta
git diff --cached --name-only
git commit -m "feat(skydive): 바람이 낀 코스가 통과 가능한지 재는 산수

역풍은 밀린 거리와 필요 이동이 더해져서, 구간 전체를 덮는 12m/s 역풍 하나로
아무도 못 지나가는 구간이 생긴다. 에러 없이 판이 안 끝나는 것으로만 보이므로
굽기 전에 막는다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 8: 코스 빌더가 바람을 굽는다

**Files:**
- Create: `LeagueOfPhysical-Art/Materials/SkydiveWind.mat` (클라에서는 `Assets/Art/Materials/SkydiveWind.mat`)
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Editor/SkydiveCourseBuilder.cs`
- Test: `LeagueOfPhysical-Client/Assets/Tests/Editor/SkydiveWindBuildTests.cs`

**Interfaces:**
- Consumes: `LOP.WindVolume`, `LOP.SkydiveWindReach`
- Produces:
  - `internal static GameObject SkydiveCourseBuilder.CreateWindVolume(Transform parent, string name, Vector3 center, float radius, float height, Vector3 wind, Material material)`
  - `internal static readonly WindSpec[] SkydiveCourseBuilder.Winds` (`WindSpec`: `Name`, `Center`, `Radius`, `Height`, `Wind` — 전부 `internal readonly`)

- [ ] **Step 1: 머티리얼을 만든다**

Art 레포에 브랜치를 판다:

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Art
git fetch origin
git checkout -b feature/skydive-wind origin/main
```

기존 `SkydiveCloud.mat`(URP Unlit, 반투명, 양면)을 복사해 이름과 색만 바꾼다. 구름은 흰색 알파 0.1이라, 바람은 **옅은 하늘색에 알파를 좀 더 올려** 구분되게 한다.

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Art/Materials
cp SkydiveCloud.mat SkydiveWind.mat
sed -i 's/^  m_Name: SkydiveCloud$/  m_Name: SkydiveWind/' SkydiveWind.mat
sed -i 's/^    - _BaseColor: {r: 1, g: 1, b: 1, a: 0.1}$/    - _BaseColor: {r: 0.72, g: 0.88, b: 1, a: 0.28}/' SkydiveWind.mat
sed -i 's/^    - _Color: {r: 1, g: 1, b: 1, a: 0.1}$/    - _Color: {r: 0.72, g: 0.88, b: 1, a: 0.28}/' SkydiveWind.mat
grep -n "m_Name: SkydiveWind\|_BaseColor\|_Color:" SkydiveWind.mat
```
Expected: `m_Name: SkydiveWind` 한 줄과 색 두 줄이 `a: 0.28`로 바뀌어 있다. 안 바뀌었으면 원본 줄이 위 패턴과 다른 것이니 파일을 직접 열어 고친다.

```bash
unity command recompile --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
ls C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Art/Materials/SkydiveWind.mat.meta
```
Expected: 유니티가 `.meta`를 만들어 뒀다

- [ ] **Step 2: 실패하는 테스트를 쓴다**

`LeagueOfPhysical-Client/Assets/Tests/Editor/SkydiveWindBuildTests.cs`:

```csharp
using LOP;
using LOP.EditorTools;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SkydiveWindBuildTests
{
    private const string SkydiveMapPath = "Assets/Art/Scenes/SkydiveMap.unity";

    // 콜라이더가 붙으면 키네마틱 이동이 벽으로 인식해 바람 위에 착지한다 — 구름에서 겪은 함정.
    [Test]
    public void 바람_시각물에는_콜라이더가_없다()
    {
        GameObject volume = SkydiveCourseBuilder.CreateWindVolume(
            null, "wind-test", new Vector3(0f, 1000f, 0f), 25f, 120f, new Vector3(0f, 14f, 0f), null);

        try
        {
            Assert.That(volume.GetComponentsInChildren<Collider>(true), Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(volume);
        }
    }

    [Test]
    public void 구운_볼륨에_마커가_붙는다()
    {
        GameObject volume = SkydiveCourseBuilder.CreateWindVolume(
            null, "wind-test", new Vector3(30f, 1900f, 30f), 25f, 120f, new Vector3(0f, 14f, 0f), null);

        try
        {
            var marker = volume.GetComponent<WindVolume>();
            Assert.IsNotNull(marker, "마커가 없으면 맵을 읽어도 바람이 안 생긴다");
            Assert.AreEqual(25f, marker.Radius);
            Assert.AreEqual(120f, marker.Height);
            Assert.AreEqual(14f, marker.Wind.y);
        }
        finally
        {
            Object.DestroyImmediate(volume);
        }
    }

    [Test]
    public void 구운_맵의_바람에는_콜라이더가_없다()
    {
        Scene scene = EditorSceneManager.OpenScene(SkydiveMapPath, OpenSceneMode.Additive);
        try
        {
            // 이름이 아니라 마커 컴포넌트로 센다 — 막대도 "Wind_"로 시작해서 이름으로 세면
            // 볼륨 8개가 아니라 막대까지 120개가 잡힌다.
            int volumeCount = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (WindVolume marker in root.GetComponentsInChildren<WindVolume>(true))
                {
                    volumeCount++;
                    Assert.That(marker.GetComponentsInChildren<Collider>(true), Is.Empty,
                                $"{marker.name}에 콜라이더가 남아 있다 — 캐릭터가 바람 위에 착지한다");
                }
            }

            Assert.That(volumeCount, Is.EqualTo(SkydiveCourseBuilder.Winds.Length),
                        "구운 맵의 바람 개수가 표와 다르다 — 다시 구워야 한다");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, removeScene: true);
        }
    }

    // 역풍은 밀린 거리와 필요 이동이 더해져 구간을 막을 수 있다. 표가 그렇게 되어 있으면
    // 굽기 전에 여기서 걸린다.
    [Test]
    public void 모든_구간을_적어도_한_자세는_지날_수_있다()
    {
        string failure = SkydiveCourseBuilder.FindImpassableSection();

        Assert.IsNull(failure, failure);
    }
}
```

- [ ] **Step 3: 테스트가 실패하는 것을 확인한다**

```bash
unity command recompile --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
unity command console --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
```
Expected: `CreateWindVolume` / `Winds` / `FindImpassableSection`을 못 찾는다는 CS0117

- [ ] **Step 4: 바람 표와 굽기를 코스 빌더에 넣는다**

`SkydiveCourseBuilder.cs`의 `CloudMaterialPath` 옆에 경로를 추가한다:

```csharp
        private const string WindMaterialPath = "Assets/Art/Materials/SkydiveWind.mat";
```

`Shelves` 표 아래에 바람 표를 추가한다. **이 표가 곧 바람 설계다(스펙 5.4).**

```csharp
        internal readonly struct WindSpec
        {
            public readonly string Name;
            public readonly Vector3 Center;
            public readonly float Radius;
            public readonly float Height;
            public readonly Vector3 Wind;

            public WindSpec(string name, Vector3 center, float radius, float height, Vector3 wind)
            {
                Name = name;
                Center = center;
                Radius = radius;
                Height = height;
                Wind = wind;
            }
        }

        // 반지름 150은 코스 폭(±100)을 다 덮는다 — 옆으로 피해 갈 수 있으면 그 구간이 아무것도
        // 안 묻게 된다. 피할 수 있어야 하는 것은 기둥(반지름 25)뿐이다.
        internal static readonly WindSpec[] Winds =
        {
            // 2600→2200 가르치기 ①: 짧은 순풍. 펴면 실려 가는데, 순풍이라 손해는 없다.
            new WindSpec("Wind_2400_Tail", new Vector3(0f, 2400f, 0f), 150f, 40f, new Vector3(10f, 0f, 0f)),

            // 2200→1800 가르치기 ②: 구멍(30,30) 위의 기둥. 펴면 위로 밀려 못 내려간다.
            new WindSpec("Wind_1900_Updraft", new Vector3(30f, 1900f, 30f), 25f, 120f, new Vector3(0f, 14f, 0f)),

            // 1800→1400: 역풍. 구멍은 −X 쪽인데 바람은 +X다. 구간 전체로 깔면 55m 이동에
            //            68m 역풍이 더해져 아무도 못 지나가므로 높이를 150으로 잘라 둔다.
            new WindSpec("Wind_1600_Head", new Vector3(0f, 1600f, 0f), 150f, 150f, new Vector3(10f, 0f, 0f)),

            // 1400→1000 ★ 이 코스의 요점: 구간 전체를 덮는 강한 순풍. 타면 다이브로도 60m를 간다.
            new WindSpec("Wind_1200_Strong", new Vector3(0f, 1200f, 0f), 150f, 400f, new Vector3(0f, 0f, -20f)),

            // 1000→600: 길 좌우의 기둥 둘. 가운데 15m 통로만 천을 펴고 지날 수 있다.
            new WindSpec("Wind_800_UpdraftL", new Vector3(-30f, 800f, -27f), 25f, 120f, new Vector3(0f, 14f, 0f)),
            new WindSpec("Wind_800_UpdraftR", new Vector3(35f, 800f, -27f), 25f, 120f, new Vector3(0f, 14f, 0f)),

            // 600→200: +Z 순풍이 50m 이동을 절반쯤 대신해 준다.
            new WindSpec("Wind_400_Tail", new Vector3(0f, 400f, 0f), 150f, 250f, new Vector3(0f, 0f, 12f)),

            // 마지막 구멍(0,25) 위의 기둥 — 착지를 패러세일로 때우지 못하게 한다.
            new WindSpec("Wind_300_Updraft", new Vector3(0f, 300f, 25f), 25f, 120f, new Vector3(0f, 14f, 0f)),
        };
```

굽기 메서드를 추가한다. 구름 굽기 블록 다음에 넣는다:

```csharp
            Material windMaterial = AssetDatabase.LoadAssetAtPath<Material>(WindMaterialPath);
            if (windMaterial == null)
            {
                // 기본 머티리얼은 불투명이라 막대가 시야를 가린다. 마커는 그대로 굽고 막대만 생략한다.
                Debug.LogWarning($"[Skydive] 바람 머티리얼이 없다 — {WindMaterialPath}. 막대 없이 마커만 굽는다.");
            }

            var winds = new GameObject("Winds");
            winds.transform.SetParent(root.transform, worldPositionStays: false);
            for (int i = 0; i < Winds.Length; i++)
            {
                var spec = Winds[i];
                CreateWindVolume(winds.transform, spec.Name, spec.Center,
                                 spec.Radius, spec.Height, spec.Wind, windMaterial);
            }
```

굽기 시작 부분(`root`를 새로 만든 직후)에 검사를 넣는다:

```csharp
            string impassable = FindImpassableSection();
            if (impassable != null)
            {
                Debug.LogError($"[Skydive] 굽지 않는다 — {impassable}");
                return;
            }
```

메서드 둘을 추가한다:

```csharp
        internal static GameObject CreateWindVolume(Transform parent, string name, Vector3 center,
                                                    float radius, float height, Vector3 wind,
                                                    Material material)
        {
            var go = new GameObject(name);
            if (parent != null)
            {
                go.transform.SetParent(parent, worldPositionStays: false);
            }
            go.transform.localPosition = center;

            var marker = go.AddComponent<LOP.WindVolume>();
            marker.Radius = radius;
            marker.Height = height;
            marker.Wind = wind;

            if (material == null)
            {
                return go;
            }

            // 안 보이는 바람은 왜 밀렸는지 못 읽는 장치가 된다. 바람 방향으로 늘인 막대를
            // 흩뿌려 방향이 눈에 보이게 한다 — 지나가며 스치는 것 자체가 속도감이기도 하다.
            const int BarCount = 14;
            const float BarLength = 14f;
            const float BarThickness = 0.5f;
            var rotation = wind.sqrMagnitude > 1e-6f
                ? Quaternion.LookRotation(wind.normalized)
                : Quaternion.identity;

            for (int k = 0; k < BarCount; k++)
            {
                // 황금각 나선 — 난수 없이 고르게 흩어진다. 다시 구워도 같은 자리에 나온다.
                float t = (k + 0.5f) / BarCount;
                float angle = k * 2.39996f;
                float r = radius * Mathf.Sqrt(t);

                var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.name = $"{name}_Bar{k}";
                bar.transform.SetParent(go.transform, worldPositionStays: false);
                bar.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * r, (t - 0.5f) * height, Mathf.Sin(angle) * r);
                bar.transform.localRotation = rotation;
                bar.transform.localScale = new Vector3(BarThickness, BarThickness, BarLength);

                var collider = bar.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.DestroyImmediate(collider);
                }
                bar.GetComponent<MeshRenderer>().sharedMaterial = material;
            }

            return go;
        }

        /// <summary>
        /// 바람 때문에 아무 자세로도 못 지나가는 구간이 있으면 그 설명을, 없으면 null을 준다.
        /// 역풍은 밀린 거리와 필요 이동이 더해져 구간을 막을 수 있는데, 그러면 에러 없이
        /// 판이 안 끝나는 것으로만 보인다.
        /// </summary>
        internal static string FindImpassableSection()
        {
            for (int i = 1; i < Shelves.Length; i++)
            {
                float upperY = Shelves[i - 1].Y;
                float lowerY = Shelves[i].Y;
                float drop = upperY - lowerY;
                float requiredX = Shelves[i].HoleX - Shelves[i - 1].HoleX;
                float requiredZ = Shelves[i].HoleZ - Shelves[i - 1].HoleZ;

                if (PosturePasses(upperY, lowerY, drop, requiredX, requiredZ,
                                  SpreadFallSpeed, SpreadMoveSpeed, SpreadTurnAccel, SpreadWindLag) ||
                    PosturePasses(upperY, lowerY, drop, requiredX, requiredZ,
                                  DiveFallSpeed, DiveMoveSpeed, DiveTurnAccel, DiveWindLag))
                {
                    continue;
                }

                return $"{upperY:0} → {lowerY:0} 구간을 대자로도 다이브로도 못 지나간다. 바람 표를 고쳐라.";
            }
            return null;
        }

        private static bool PosturePasses(float upperY, float lowerY, float drop,
                                          float requiredX, float requiredZ,
                                          float fallSpeed, float moveSpeed, float turnAccel, float lag)
        {
            float driftX = 0f;
            float driftZ = 0f;
            for (int w = 0; w < Winds.Length; w++)
            {
                var spec = Winds[w];
                float overlap = Overlap(upperY, lowerY, spec);
                if (overlap <= 0f)
                {
                    continue;
                }
                // 세로 바람은 옆으로 안 민다 — 낙하 속도를 바꾸지만 그 영향은 작아 여기선 안 본다.
                driftX += SkydiveWindReach.DriftDistance(spec.Wind.x, overlap, fallSpeed, lag);
                driftZ += SkydiveWindReach.DriftDistance(spec.Wind.z, overlap, fallSpeed, lag);
            }

            float reach = SkydiveWindReach.SelfReach(moveSpeed, turnAccel, drop, fallSpeed);
            return SkydiveWindReach.CanReach(requiredX, requiredZ, driftX, driftZ, reach);
        }

        // 볼륨이 이 구간과 겹치는 세로 길이.
        private static float Overlap(float upperY, float lowerY, in WindSpec spec)
        {
            float top = Mathf.Min(upperY, spec.Center.y + spec.Height * 0.5f);
            float bottom = Mathf.Max(lowerY, spec.Center.y - spec.Height * 0.5f);
            return Mathf.Max(0f, top - bottom);
        }
```

`SpreadWindLag`/`DiveWindLag` 상수를 검사 기준값 블록(`SpreadFallSpeed` 등이 있는 곳)에 추가한다:

```csharp
        private const float SpreadWindLag = 2.06f;
        private const float DiveWindLag = 3.10f;
```

- [ ] **Step 5: 앞의 두 테스트가 통과하는지 확인한다**

```bash
unity command recompile --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
unity command console --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
unity command run_tests --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json -- --mode editor --filter SkydiveWindBuildTests
unity command test_status --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
```
Expected: `바람_시각물에는_콜라이더가_없다` · `구운_볼륨에_마커가_붙는다` · `모든_구간을_적어도_한_자세는_지날_수_있다` 통과. `구운_맵의_바람에는_콜라이더가_없다`는 **실패**한다(아직 안 구웠다 — Task 9에서 통과한다).

- [ ] **Step 6: 커밋한다**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Editor/SkydiveCourseBuilder.cs Assets/Tests/Editor/SkydiveWindBuildTests.cs Assets/Tests/Editor/SkydiveWindBuildTests.cs.meta
git diff --cached --name-only
git commit -m "feat(skydive): 코스 빌더가 바람 볼륨 8개를 굽는다

표가 곧 바람 설계다. 굽기 전에 통과 가능성을 검사해, 역풍이 구간을 막아
판이 안 끝나는 사고를 막는다. 안 보이는 바람은 왜 밀렸는지 못 읽으므로
바람 방향으로 늘인 막대를 흩뿌려 방향이 보이게 한다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 9: 맵을 다시 굽는다

**Files:**
- Modify: `LeagueOfPhysical-Art/Scenes/SkydiveMap.unity` (클라에서는 `Assets/Art/Scenes/SkydiveMap.unity`)
- Modify: `LeagueOfPhysical-Client` 서브모듈 포인터

**Interfaces:**
- Consumes: `SkydiveCourseBuilder`의 굽기 메뉴
- Produces: 바람 볼륨 8개가 들어간 `SkydiveMap.unity`

- [ ] **Step 1: 에디터가 Play 중이 아닌지 확인한다**

```bash
unity command editor_status --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
```
Expected: `isPlaying`이 false. true면 **거기서 멈추고 사람에게 정지를 요청한다** — Play 중에는 씬을 저장할 수 없다.

- [ ] **Step 2: 맵 씬을 열고 굽는다**

굽기는 `LOP/Skydive/코스 굽기` 메뉴이고, 뒤에 있는 메서드는 `LOP.EditorTools.SkydiveCourseBuilder.Build()`(public static)다. 스크립트로 부른다.

```bash
unity command open_scene --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json -- --path "Assets/Art/Scenes/SkydiveMap.unity"

cat > "$CLAUDE_JOB_DIR/tmp/bake_skydive.cs" <<'EOF'
LOP.EditorTools.SkydiveCourseBuilder.Build();
UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
UnityEngine.Debug.Log("[bake] saved");
EOF

unity command eval_file --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json -- --path "$CLAUDE_JOB_DIR/tmp/bake_skydive.cs"
unity command console --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
```
Expected: 콘솔에 `[Skydive] 코스를 구웠다`와 `[bake] saved`. `굽지 않는다 —`로 시작하는 에러가 보이면 **바람 표가 구간을 막은 것**이니 Task 8의 표를 고친다.

- [ ] **Step 3: 구운 결과를 검사한다**

```bash
unity command run_tests --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json -- --mode editor --filter SkydiveWindBuildTests
unity command test_status --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
```
Expected: 4개 전부 `Passed` — `구운_맵의_바람에는_콜라이더가_없다`가 이제 통과한다

- [ ] **Step 4: 전체 EditMode 테스트를 돌린다**

```bash
unity command run_tests --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json -- --mode editor
unity command test_status --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client --json
```
Expected: 실패 0. 이 슬라이스 전에 통과하던 테스트가 하나도 안 깨져야 한다.

- [ ] **Step 5: Art 레포에 커밋한다**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Art
git status --short
git add Materials/SkydiveWind.mat Materials/SkydiveWind.mat.meta Scenes/SkydiveMap.unity
git diff --cached --name-only
git commit -m "feat(skydive): 맵에 바람 볼륨 8개를 굽는다

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

> `Assets/Art`는 서브모듈이다. 위 `git` 명령은 서브모듈 안에서 도는 것이고, `LeagueOfPhysical-Art` 레포의 `feature/skydive-wind` 브랜치에 올라간다. **워킹트리에 사용자의 다른 로컬 픽스처(`PolyOne/` 등)가 있을 수 있으니 `git add`에 경로를 반드시 명시한다.**

- [ ] **Step 6: 클라 레포에 서브모듈 포인터를 커밋한다**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Art
git diff --cached --name-only
git commit -m "chore(art): 바람이 들어간 맵으로 포인터를 옮긴다

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

> **포인터를 안 옮기면 CI와 서버가 옛 맵을 본다.** 그리고 머지 후 `content-deploy`(어드레서블 업로드)를 안 하면 **에디터는 멀쩡한데 폰·서버만 옛 맵**이 나온다 — 그 배포는 이 계획 밖이고, 사람이 머지 시점에 한다.

---

## 마무리 — 사람이 할 일

이 계획은 **커밋까지**다. 이후는 `finishing-a-development-branch`로 사람이 한다:

1. 7개 레포 각각에서 `CLAUDE.md`의 푸시 규약대로 머지·푸시 (한 줄씩 결과 확인, `&&` 금지, force push 금지)
2. 클라 레포에서 `content-deploy -f target=gameserver` (어드레서블 업로드)
3. 게임서버 이미지 재빌드 — **서버 레포가 바뀌었으므로 태그가 움직인다**(마스터데이터만 바꾼 것이 아니다)
4. 두 클라 실플레이로 손맛 확인: 같은 구간을 대자·다이브·패러세일로 지나며 비교, 러버밴딩이 늘지 않았는지 확인
5. 스펙 5.5의 시작값을 손맛에 맞게 조인다 — 볼륨은 맵 씬, 지연은 마스터데이터
