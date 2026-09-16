# 양궁 사거리 맵 — 토대 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 양궁 사거리 맵이 올라설 **토대** 셋을 놓는다 — 맵별 설정, 링(띠) 채점, 판(Face) 과녁과 판정. 셋 다 **지금 동작을 한 군데도 안 바꾼다.**

**Architecture:** 새 개념을 더하되 전부 **기존 동작이 그 특수형**이 되게 한다. 지금의 "과녁당 고정 점수"는 *띠 하나짜리*가 되고, 지금의 구 과녁은 *모양이 `Sphere`* 인 것이 되며, 지금의 전역 설정 한 줄은 *원형 맵의 행*이 된다. 그래서 원형 맵이 그대로 도는 것이 곧 "안 깨졌다"의 증거다.

**Tech Stack:** Unity 6000.3.16f1 · C# · Luban(MasterData) · VContainer · NUnit(EditMode)

**Spec:** `docs/superpowers/specs/2026-09-15-archery-range-map-design.md` (§2 구조, §3 과녁과 판정, §7 기존 맵 보호, §8 테스트)

앞 슬라이스: `docs/superpowers/plans/2026-09-14-archery-slice4.md`

---

## Global Constraints

- **기존 원형 맵의 동작이 한 군데도 바뀌면 안 된다.** 점수도, 과녁 위치도, 판정 결과도 같아야 한다. 이것이 이 계획 전체의 합격 기준이다.
- **결정론이 이 모드의 생명줄이다.** 과녁은 통신하지 않고 `(matchSeed, waveIndex)`로 클·서가 각자 계산한다. **`ArcheryWaveGenerator.Fill`의 난수 소비 순서가 곧 네트워크 계약**이며, **이 계획은 그 순서를 바꾸지 않는다.** 난수 호출을 추가·삭제·이동하면 결함이다.
- **쌍둥이 파일**: 서버와 클라의 `Assets/Scripts/Game/ArcheryConfigProvider.cs`는 **10번째 줄(서로 상대편을 가리키는 주석)만 다르고** 나머지는 한 글자도 같아야 한다. 그 줄은 같게 만들지 마라.
- 시뮬에 들어가는 코드는 **구체 클래스를 클·서가 공유**한다 — 인터페이스 seam 금지.
- **`using GameFramework.World;`를 쓰지 않는다.** World 타입은 항상 풀 네임스페이스로 한정한다(`Component`가 `UnityEngine.Component`와 겹친다).
- **Anemic Domain Model** — 데이터 타입은 데이터와 읽기 전용 파생 속성만.
- 주석은 최소로, 일상어로. 비자명한 *의도(왜)* 만. 전문용어를 설명 없이 던지지 않는다.
- 새 `.cs`마다 Unity가 만든 `.meta`를 함께 커밋한다. `.meta`를 직접 만들지 않는다.
- **마스터데이터 컬럼은 반드시 맨 뒤에 추가한다.** Luban은 컬럼 *이름*이 아니라 *몇 번째 열*로 읽으므로, 중간에 끼우면 기존 값이 조용히 뒤바뀐다.
- **새 테이블을 추가하면 `LOPMasterData.TableFiles` 목록도 갱신해야 한다.** 빠뜨리면 로딩이 `KeyNotFoundException`으로 죽는다.
- **`git add -A` / `git commit -a` 금지.** 바꾼 파일만 경로로 지정하고 `git status --short`로 확인한다. 양쪽 Unity 레포에 커밋하면 안 되는 로컬 픽스처가 늘 떠 있다(`Assets/Art`, `Jua-Regular SDF.asset`, `ProjectSettings/*`, `URPDefaultResources/*`, `DefaultVolumeProfile.asset`, `AddressableAssetsData/*`, `ConfigureRoomComponent.cs`, `output/`, `.superpowers/`, `docs/superpowers/`).
- 커밋 트레일러(모든 커밋):
  ```
  Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01J3Xe7rZoLKi3dJFGKrLsGL
  ```

### 유니티 검증 (모든 태스크 공통)

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server run_tests --mode EditMode --filter <테스트클래스>
```

- LOP-Shared는 `file:` 패키지라 **서버 프로젝트가 컴파일한다** — Shared 테스트는 서버 에디터로 돌린다.
- **⭐ `--filter <테스트클래스>`를 써라.** 30초 안에 per-test 결과가 온다. 마지막에 한 번만 전체(`--mode EditMode`)를 돌린다.
- **⛔ 클라에서 `run_tests`를 돌리지 마라** — 러너가 이 환경에서 물린다. 컴파일 초록 + 콘솔 CS 에러 0이 클라의 게이트다. 콘솔은 `get_console_logs --types error --count 20` (`read_console`이 아니다).

**`run_tests` 전 반드시 둘 다 확인:**
1. `recompile_status`가 `status:completed` + `errors:[]`. ⚠️ `up_to_date`면 **재컴파일을 안 한 것**이라 초록의 증거가 아니다.
2. 씬이 dirty하지 않은가:
   `unity command --project-path <프로젝트> eval --code "return UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().isDirty;"`
   → 응답의 **`result` 필드**가 `true`면 테스트를 걸지 말고 **보고해라**(dirty면 저장 확인 모달이 떠서 에디터가 물리고, 재시작 말고는 못 푼다). 씬을 저장하지도 버리지도 마라.

⚠️ **`cancel_tests` 금지.** CLI 타임아웃이 나도 런은 계속 도니 `test_status`를 폴링한다.

---

## 파일 구조

### LOP-Shared (`C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared`)

| 파일 | 책임 |
|---|---|
| `Runtime/Scripts/Game/ArcheryRingBand.cs` (신규) | 띠 하나 — 바깥 경계 비율과 점수 |
| `Runtime/Scripts/Game/ArcheryTargetShape.cs` (신규) | 과녁 모양(`Sphere`/`Face`) |
| `Runtime/Scripts/Game/ArcheryTargetKind.cs` (수정) | 모양·띠 목록을 갖는다 |
| `Runtime/Scripts/Game/ArcheryTarget.cs` (수정) | 모양·띠·바라보는 방향을 갖는다 |
| `Runtime/Scripts/Game/ArcheryHitRules.cs` (수정) | 맞은 자리로 띠를 찾아 점수를 낸다 |
| `Runtime/Scripts/Game/ArcheryHitTest.cs` (수정) | 판 판정 + 모양별 진입점 + 맞은 자리 |
| `Runtime/Scripts/Game/ArcheryWaveGenerator.cs` (수정) | 새 생성자에 맞춘다 (난수 순서 불변) |
| `Tests/EditMode/ArcheryRingScoringTests.cs` (신규) | 띠 채점 |
| `Tests/EditMode/ArcheryFaceHitTests.cs` (신규) | 판 판정 |

### MasterData / infrastructure

| 파일 | 책임 |
|---|---|
| `infrastructure/table/Datas/#ArcheryTarget.xlsx` (수정) | `shape` 컬럼 추가 |
| `infrastructure/table/Datas/#ArcheryRing.xlsx` (신규) | 과녁 종류별 띠 |
| `infrastructure/table/Datas/#ArcheryConfig.xlsx` (수정) | `id`를 맵 id로 |
| `LeagueOfPhysical-MasterData-{Client,Server}/Runtime/Scripts/LOPMasterData.cs` (수정) | `TableFiles`에 새 표 |
| `LeagueOfPhysical-MasterData-Server/Tests/EditMode/ArcheryTargetSeparationTests.cs` (수정) | 배포 데이터 검사 추가 |

### Server / Client

| 파일 | 책임 |
|---|---|
| 서버 `Assets/Scripts/Game/ArcheryConfigProvider.cs` (수정) | 맵 id로 설정 조회, 모양·띠 채우기 |
| 클라 `Assets/Scripts/Game/ArcheryConfigProvider.cs` (수정) | **서버 쌍둥이와 10행 말고 동일** |
| 서버 `Assets/Scripts/Game/TickSystems/ArcheryHitSystem.cs` (수정) | 맞은 자리를 채점에 넘긴다 |
| 클라 `Assets/Scripts/Game/TickSystems/ArcheryArrowStickSystem.cs` (수정) | 모양별 판정 진입점으로 |

---

## Task 1: 띠와 모양 타입을 만든다 (LOP-Shared)

**Files:**
- Create: `Runtime/Scripts/Game/ArcheryRingBand.cs`
- Create: `Runtime/Scripts/Game/ArcheryTargetShape.cs`
- Modify: `Runtime/Scripts/Game/ArcheryTargetKind.cs`
- Test: `Tests/EditMode/ArcheryRingScoringTests.cs` (신규)

**Interfaces:**
- Consumes: (없음 — 첫 태스크)
- Produces:
  - `readonly struct ArcheryRingBand { float OuterRatio; int Points; ArcheryRingBand(float outerRatio, int points); }`
  - `enum ArcheryTargetShape { Sphere = 0, Face = 1 }`
  - `ArcheryTargetKind(float radius, int points, int weight, bool isTrap, ArcheryTargetShape shape, IReadOnlyList<ArcheryRingBand> bands)`
  - `ArcheryTargetKind.Shape` · `ArcheryTargetKind.Bands`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Tests/EditMode/ArcheryRingScoringTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.Tests
{
    public class ArcheryRingScoringTests
    {
        //  띠는 "바깥 경계까지"를 뜻한다 — 0.2면 중심에서 반지름의 20%까지가 그 띠다.
        [Test]
        public void 띠는_바깥_경계와_점수를_들고_있다()
        {
            var band = new ArcheryRingBand(0.2f, 10);

            Assert.AreEqual(0.2f, band.OuterRatio, 1e-6f);
            Assert.AreEqual(10, band.Points);
        }

        //  지금 과녁은 "어디를 맞히든 같은 점수"다 — 그게 띠 하나짜리다.
        [Test]
        public void 종류는_모양과_띠를_들고_있다()
        {
            var bands = new List<ArcheryRingBand> { new ArcheryRingBand(1f, 2) };
            var kind = new ArcheryTargetKind(0.3f, 2, 40, false, ArcheryTargetShape.Sphere, bands);

            Assert.AreEqual(ArcheryTargetShape.Sphere, kind.Shape);
            Assert.AreEqual(1, kind.Bands.Count);
            Assert.AreEqual(2, kind.Bands[0].Points);
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
```

기대: `failed:true`. `ArcheryRingBand`·`ArcheryTargetShape`를 찾을 수 없음(CS0246) + `ArcheryTargetKind` 생성자 인자 개수(CS1729).

- [ ] **Step 3: 최소 구현**

`Runtime/Scripts/Game/ArcheryRingBand.cs`:

```csharp
namespace LOP
{
    /// <summary>
    /// 과녁 한 장의 띠 하나. 맞은 자리가 중심에서 얼마나 벗어났는지를 <b>반지름으로 나눈 값</b>(0~1)으로
    /// 보고, 그 값이 이 띠의 바깥 경계 안이면 이 점수를 준다.
    ///
    /// <para>지금의 "과녁당 고정 점수"는 바깥 경계가 1인 띠 하나다 — 어디를 맞히든 같은 점수라는 뜻이
    /// 그대로 표현된다.</para>
    /// </summary>
    public readonly struct ArcheryRingBand
    {
        /// <summary>이 띠가 어디까지인가(0~1). 중심이 0, 과녁 가장자리가 1이다.</summary>
        public readonly float OuterRatio;

        /// <summary>이 띠에 맞으면 점수가 이만큼 움직인다. 함정은 음수다.</summary>
        public readonly int Points;

        public ArcheryRingBand(float outerRatio, int points)
        {
            OuterRatio = outerRatio;
            Points = points;
        }
    }
}
```

`Runtime/Scripts/Game/ArcheryTargetShape.cs`:

```csharp
namespace LOP
{
    /// <summary>
    /// 과녁의 생김새. 판정하는 법과 "맞은 자리"의 뜻이 여기서 갈린다.
    /// <b>데이터가 고르는 값이지 인터페이스가 아니다</b> — 두 모양 모두 클·서가 같은 코드를 돌린다.
    /// </summary>
    public enum ArcheryTargetShape
    {
        /// <summary>공. 어느 쪽에서 와도 맞는다. 띠를 나눌 면이 없으므로 띠는 하나여야 한다.</summary>
        Sphere = 0,

        /// <summary>사수를 향해 선 원판. 뒤에서 온 화살은 안 맞고, 맞은 자리로 띠를 가른다.</summary>
        Face = 1,
    }
}
```

`Runtime/Scripts/Game/ArcheryTargetKind.cs`를 아래로 바꾼다:

```csharp
using System.Collections.Generic;

namespace LOP
{
    /// <summary>과녁 한 종류. 작을수록 맞히기 어렵고 그만큼 비싸다.</summary>
    public readonly struct ArcheryTargetKind
    {
        /// <summary>맞았다고 칠 반경(m).</summary>
        public readonly float Radius;

        /// <summary>맞히면 점수가 이만큼 움직인다. 함정은 음수다.</summary>
        public readonly int Points;

        /// <summary>뽑힐 상대 비율. 합이 100일 필요는 없다 — 서로의 크기만 의미가 있다.</summary>
        public readonly int Weight;

        /// <summary>맞히면 안 되는 과녁인가. 함정끼리, 성한 것끼리 따로 뽑는다.</summary>
        public readonly bool IsTrap;

        /// <summary>공인가 판인가.</summary>
        public readonly ArcheryTargetShape Shape;

        /// <summary>
        /// 중심에서 바깥으로 가는 띠 목록. 비어 있으면 <see cref="Points"/>짜리 띠 하나로 친다 —
        /// 띠 데이터가 없던 시절의 과녁이 그대로 동작하게 하려는 것이다.
        /// </summary>
        public readonly IReadOnlyList<ArcheryRingBand> Bands;

        public ArcheryTargetKind(float radius, int points, int weight, bool isTrap,
                                 ArcheryTargetShape shape, IReadOnlyList<ArcheryRingBand> bands)
        {
            Radius = radius;
            Points = points;
            Weight = weight;
            IsTrap = isTrap;
            Shape = shape;
            Bands = bands;
        }
    }
}
```

- [ ] **Step 4: 호출부를 맞춰 컴파일을 초록으로 둔다**

`new ArcheryTargetKind(` 를 부르는 곳을 전부 찾아 `ArcheryTargetShape.Sphere, null`을 뒤에 더한다:

```bash
grep -rn "new ArcheryTargetKind(" \
  /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared \
  /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets \
  /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets
```

각 호출을 이렇게 바꾼다(예):

```csharp
new ArcheryTargetKind(0.45f, 1, 30, false, ArcheryTargetShape.Sphere, null)
```

> `null`은 "띠 데이터가 아직 없다"는 뜻이고, 그러면 `Points`짜리 띠 하나로 친다(Task 2에서 그렇게 구현한다). 실제 띠 데이터는 Task 5에서 들어온다.

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server run_tests --mode EditMode --filter ArcheryRingScoringTests
```

기대: 컴파일 초록, 새 테스트 둘이 **이름으로** 보이고 통과.

- [ ] **Step 5: 커밋 (레포 셋)**

바꾼 파일만 경로로 스테이지하고 `git status --short`로 확인한 뒤:

```
feat(archery): 과녁에 모양과 띠라는 개념을 낸다

동심원 채점을 하려면 "맞은 자리"가 필요하고, 그러려면 과녁이 공이 아니라
사수를 향해 선 판이어야 한다. 그 둘을 데이터가 고르는 값으로 낸다.

지금의 "과녁당 고정 점수"는 바깥 경계가 1인 띠 하나로 표현된다 — 개념을
더하지만 기존 과녁의 뜻은 그대로다.
```

서버·클라는 `chore(archery): 과녁 종류 생성자 변경에 호출부를 맞춘다`

---

## Task 2: 맞은 자리로 채점한다 (LOP-Shared)

**Files:**
- Modify: `Runtime/Scripts/Game/ArcheryTarget.cs`
- Modify: `Runtime/Scripts/Game/ArcheryHitRules.cs`
- Modify: `Runtime/Scripts/Game/ArcheryWaveGenerator.cs`
- Test: `Tests/EditMode/ArcheryRingScoringTests.cs`

**Interfaces:**
- Consumes: `ArcheryRingBand`, `ArcheryTargetShape`, `ArcheryTargetKind.Shape/Bands` (Task 1)
- Produces:
  - `ArcheryTarget(int waveIndex, int slotIndex, Vector3 origin, float riseSpeed, long spawnTick, float radius, int points, bool isTrap, ArcheryTargetShape shape, IReadOnlyList<ArcheryRingBand> bands, Vector3 facing)`
  - `ArcheryTarget.Shape` · `ArcheryTarget.Bands` · `ArcheryTarget.Facing`
  - `ArcheryHitRules.Resolve(in ArcheryTarget target, float normalizedOffset)` — **인자가 하나 늘어난다**

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Tests/EditMode/ArcheryRingScoringTests.cs`의 마지막 `[Test]` 뒤에 더한다:

```csharp
        static ArcheryTarget TargetWith(bool isTrap, int points, params ArcheryRingBand[] bands)
        {
            return new ArcheryTarget(
                waveIndex: 0, slotIndex: 0,
                origin: UnityEngine.Vector3.zero, riseSpeed: 0f, spawnTick: 0L,
                radius: 0.4f, points: points, isTrap: isTrap,
                shape: ArcheryTargetShape.Face,
                bands: bands.Length == 0 ? null : new List<ArcheryRingBand>(bands),
                facing: new UnityEngine.Vector3(0f, 0f, -1f));
        }

        //  띠가 없으면 예전처럼 "어디를 맞히든 같은 점수"다. 띠 데이터가 아직 없는 과녁이
        //  조용히 0점이 되면 안 된다.
        [Test]
        public void 띠가_없으면_과녁_점수를_그대로_준다()
        {
            var target = TargetWith(isTrap: false, points: 2);

            Assert.AreEqual(2, ArcheryHitRules.Resolve(target, 0f).Gained);
            Assert.AreEqual(2, ArcheryHitRules.Resolve(target, 0.99f).Gained);
        }

        [Test]
        public void 중심에_가까울수록_높은_띠를_받는다()
        {
            var target = TargetWith(isTrap: false, points: 0,
                new ArcheryRingBand(0.2f, 10),
                new ArcheryRingBand(0.5f, 8),
                new ArcheryRingBand(1.0f, 5));

            Assert.AreEqual(10, ArcheryHitRules.Resolve(target, 0.0f).Gained, "정중앙");
            Assert.AreEqual(10, ArcheryHitRules.Resolve(target, 0.1f).Gained);
            Assert.AreEqual(8, ArcheryHitRules.Resolve(target, 0.35f).Gained);
            Assert.AreEqual(5, ArcheryHitRules.Resolve(target, 0.9f).Gained);
        }

        //  경계선 위는 "그 띠까지"로 친다 — 안 그러면 경계에 맞을 때마다 값이 흔들린다.
        [Test]
        public void 띠_경계선_위는_그_띠에_속한다()
        {
            var target = TargetWith(isTrap: false, points: 0,
                new ArcheryRingBand(0.2f, 10),
                new ArcheryRingBand(1.0f, 5));

            Assert.AreEqual(10, ArcheryHitRules.Resolve(target, 0.2f).Gained);
        }

        //  가장자리 밖으로 조금 새는 값이 와도 마지막 띠로 받는다 — 부동소수 오차로 1.0000001이
        //  들어와 점수가 0이 되는 일을 막는다.
        [Test]
        public void 가장자리를_아주_조금_넘어도_마지막_띠를_준다()
        {
            var target = TargetWith(isTrap: false, points: 0,
                new ArcheryRingBand(1.0f, 5));

            Assert.AreEqual(5, ArcheryHitRules.Resolve(target, 1.0001f).Gained);
        }

        //  함정은 어느 띠든 벌점이다. 데이터에 -5로 적든 5로 적든 같은 벌점이 되어야 한다.
        [Test]
        public void 함정은_띠_점수의_크기만큼_깎는다()
        {
            var trap = TargetWith(isTrap: true, points: 0,
                new ArcheryRingBand(0.3f, -10),
                new ArcheryRingBand(1.0f, 5));

            Assert.AreEqual(0, ArcheryHitRules.Resolve(trap, 0.1f).Gained);
            Assert.AreEqual(10, ArcheryHitRules.Resolve(trap, 0.1f).Lost);
            Assert.AreEqual(5, ArcheryHitRules.Resolve(trap, 0.9f).Lost);
        }

        //  성한 과녁에 음수가 적혀 있으면 몰래 깎는 대신 아무것도 주지 않는다(기존 규칙 유지).
        [Test]
        public void 성한_과녁의_음수_띠는_0점이_된다()
        {
            var target = TargetWith(isTrap: false, points: 0, new ArcheryRingBand(1.0f, -7));

            Assert.AreEqual(0, ArcheryHitRules.Resolve(target, 0.5f).Gained);
            Assert.AreEqual(0, ArcheryHitRules.Resolve(target, 0.5f).Lost);
        }
```

파일 맨 위 `using`에 `using System.Collections.Generic;`이 이미 있는지 확인하고 없으면 더한다.

- [ ] **Step 2: 실패를 확인한다**

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
```

기대: `failed:true`. `ArcheryTarget` 생성자 인자 개수(CS1729)와 `Resolve`가 인자 둘을 안 받음(CS1501).

- [ ] **Step 3: `ArcheryTarget`에 모양·띠·방향을 더한다**

`Runtime/Scripts/Game/ArcheryTarget.cs`의 필드 목록 끝(`IsTrap` 뒤)에 더한다:

```csharp
        /// <summary>공인가 판인가. 판정하는 법이 여기서 갈린다.</summary>
        public readonly ArcheryTargetShape Shape;

        /// <summary>
        /// 중심에서 바깥으로 가는 띠 목록. 비어 있으면 <see cref="Points"/>짜리 띠 하나로 친다.
        /// </summary>
        public readonly IReadOnlyList<ArcheryRingBand> Bands;

        /// <summary>
        /// 판이 바라보는 쪽(단위 벡터). 공은 이 값을 안 쓴다.
        /// 이쪽에서 오는 화살만 맞는다 — 뒤에서 온 것은 통과한다.
        /// </summary>
        public readonly Vector3 Facing;
```

생성자를 바꾼다:

```csharp
        public ArcheryTarget(int waveIndex, int slotIndex, Vector3 origin, float riseSpeed, long spawnTick,
                             float radius, int points, bool isTrap,
                             ArcheryTargetShape shape, IReadOnlyList<ArcheryRingBand> bands, Vector3 facing)
        {
            WaveIndex = waveIndex;
            SlotIndex = slotIndex;
            Origin = origin;
            RiseSpeed = riseSpeed;
            SpawnTick = spawnTick;
            Radius = radius;
            Points = points;
            IsTrap = isTrap;
            Shape = shape;
            Bands = bands;
            Facing = facing;
        }
```

파일 맨 위에 `using System.Collections.Generic;`을 더한다.

- [ ] **Step 4: 채점 규칙을 띠 기반으로 바꾼다**

`Runtime/Scripts/Game/ArcheryHitRules.cs`의 `Resolve`를 아래로 바꾼다(위의 `ArcheryHitOutcome` 정의는 그대로 둔다):

```csharp
        /// <summary>
        /// 맞은 결과를 정한다. <paramref name="normalizedOffset"/>은 맞은 자리가 중심에서 얼마나
        /// 벗어났는지를 과녁 반지름으로 나눈 값이다(0이 정중앙, 1이 가장자리).
        /// </summary>
        public static ArcheryHitOutcome Resolve(in ArcheryTarget target, float normalizedOffset)
        {
            int points = PointsAt(target, normalizedOffset);

            if (target.IsTrap)
            {
                //  데이터에 -3으로 적든 3으로 적든 같은 벌점이 되게 한다. 적는 사람이 부호를
                //  어느 쪽으로 쓸지 헷갈려도 값이 두 배로 틀리지 않는다.
                return new ArcheryHitOutcome(0, Mathf.Abs(points));
            }

            //  성한 과녁에 음수가 적혀 있으면 점수를 몰래 깎는 대신 아무것도 주지 않는다.
            return new ArcheryHitOutcome(Mathf.Max(points, 0), 0);
        }

        private static int PointsAt(in ArcheryTarget target, float normalizedOffset)
        {
            var bands = target.Bands;
            if (bands == null || bands.Count == 0)
            {
                //  띠 데이터가 없는 과녁 — 어디를 맞히든 같은 점수다.
                return target.Points;
            }

            for (int i = 0; i < bands.Count; i++)
            {
                if (normalizedOffset <= bands[i].OuterRatio)
                {
                    return bands[i].Points;
                }
            }

            //  가장자리를 아주 조금 넘은 값(부동소수 오차)이 들어와도 점수가 0이 되면 안 된다.
            return bands[bands.Count - 1].Points;
        }
```

- [ ] **Step 5: 호출부를 맞춘다**

`new ArcheryTarget(` 과 `ArcheryHitRules.Resolve(` 를 부르는 곳을 전부 찾는다:

```bash
grep -rn "new ArcheryTarget(\|ArcheryHitRules.Resolve(" \
  /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared \
  /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets \
  /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets
```

- `ArcheryWaveGenerator.Fill`의 `into.Add(new ArcheryTarget(...))`는 **종류가 들고 있는 값을 그대로 넘긴다**(난수를 새로 쓰지 않는다):

```csharp
                into.Add(new ArcheryTarget(waveIndex, slot, center, riseSpeed, spawnTick,
                                           kind.Radius, kind.Points, kind.IsTrap,
                                           kind.Shape, kind.Bands, Vector3.zero));
```

> `Vector3.zero`는 "공이라 바라보는 쪽이 없다"는 뜻이다. 판 과녁을 만드는 생성기는 다음 계획에서 짓는다.

- 서버 `ArcheryHitSystem`의 `ApplyCandidates` 안에 있는 호출은 **임시로 `0f`를 넘겨** 컴파일만 맞춘다. 맞은 자리를 실제로 넘기는 것은 Task 4다. 실물은 이렇게 생겼다:

```csharp
                //  바꾸기 전
                var outcome = ArcheryHitRules.Resolve(TargetOfSlot(candidate.Slot));
                //  바꾼 뒤 (임시)
                var outcome = ArcheryHitRules.Resolve(TargetOfSlot(candidate.Slot), 0f);
```

- 테스트 픽스처에서 `new ArcheryTarget(`을 쓰는 곳도 같이 고친다(`ArcheryTargetMotionTests`, `ArcheryHitRulesTests` 등).

> ⚠️ `ArcheryHitRulesTests`의 기존 테스트가 `Resolve(target)` 한 인자 형태라면 `Resolve(target, 0f)`로 고친다. **기대값은 바꾸지 마라** — 띠가 없으면 예전과 같은 점수가 나오는 것이 이 태스크의 합격 기준이다.

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server run_tests --mode EditMode --filter ArcheryRingScoringTests
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server run_tests --mode EditMode --filter ArcheryHitRulesTests
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server run_tests --mode EditMode --filter ArcheryWaveGeneratorTests
```

기대: 전부 통과. **특히 `ArcheryHitRulesTests`의 기존 기대값이 하나도 안 바뀌고 통과해야 한다.**

- [ ] **Step 6: 난수 순서가 안 바뀌었는지 확인한다**

```bash
grep -n "rng\.\|PickKind\|PickCenter" /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryWaveGenerator.cs
```

위에서 아래로 세어 **개수 → (함정비율) → 슬롯마다(함정인가 → 종류 → 각도/반지름/높이 → 솟는 높이)** 순서가 그대로인지 확인하고, 그 목록을 보고서에 적는다. 하나라도 늘거나 자리가 바뀌었으면 되돌린다.

- [ ] **Step 7: 커밋 (레포 셋)**

```
feat(archery): 맞은 자리로 띠를 찾아 점수를 낸다

동심원 과녁은 어디에 꽂혔느냐로 점수가 갈린다. 그 규칙을 한 곳
(ArcheryHitRules)에 넣되, 지금의 "과녁당 고정 점수"가 띠 하나짜리가 되도록
일반화했다 — 띠 데이터가 없는 과녁은 예전과 똑같은 점수를 받는다.

기존 채점 테스트의 기대값을 한 자리도 안 고쳤다는 것이 그 증거다.
```

---

## Task 3: 판(Face) 판정과 맞은 자리 (LOP-Shared)

**Files:**
- Modify: `Runtime/Scripts/Game/ArcheryHitTest.cs`
- Test: `Tests/EditMode/ArcheryFaceHitTests.cs` (신규)

**Interfaces:**
- Consumes: `ArcheryTargetShape` (Task 1), `ArcheryTarget.Shape/Facing` (Task 2)
- Produces:
  - `ArcheryHitTest.SegmentHitsFace(Vector3 from, Vector3 to, Vector3 center, Vector3 facing, float radius, out float t)`
  - `ArcheryHitTest.SegmentHitsTarget(Vector3 from, Vector3 to, Vector3 center, in ArcheryTarget target, out float t, out float normalizedOffset)` — **모양별 진입점. 소비처는 이것만 부른다**

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Tests/EditMode/ArcheryFaceHitTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryFaceHitTests
    {
        //  판은 원점에 서서 −z 쪽(사수 쪽)을 본다. 사수는 z가 음수인 자리에 있다.
        static readonly Vector3 Center = Vector3.zero;
        static readonly Vector3 Facing = new Vector3(0f, 0f, -1f);
        const float Radius = 0.4f;

        static ArcheryTarget Face(float radius = Radius)
        {
            return new ArcheryTarget(
                waveIndex: 0, slotIndex: 0,
                origin: Center, riseSpeed: 0f, spawnTick: 0L,
                radius: radius, points: 1, isTrap: false,
                shape: ArcheryTargetShape.Face, bands: null, facing: Facing);
        }

        [Test]
        public void 정면에서_한가운데로_오면_맞는다()
        {
            bool hit = ArcheryHitTest.SegmentHitsFace(
                new Vector3(0f, 0f, -1f), new Vector3(0f, 0f, 1f), Center, Facing, Radius, out float t);

            Assert.IsTrue(hit);
            Assert.AreEqual(0.5f, t, 1e-4f, "가운데 지점에서 평면을 지난다");
        }

        //  뒤에서 온 화살은 통과한다 — 안 그러면 과녁 뒤로 넘어간 화살이 되돌아 맞는 꼴이 된다.
        [Test]
        public void 뒤에서_온_화살은_안_맞는다()
        {
            bool hit = ArcheryHitTest.SegmentHitsFace(
                new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, -1f), Center, Facing, Radius, out _);

            Assert.IsFalse(hit);
        }

        [Test]
        public void 판_반지름_밖으로_지나면_안_맞는다()
        {
            bool hit = ArcheryHitTest.SegmentHitsFace(
                new Vector3(0.5f, 0f, -1f), new Vector3(0.5f, 0f, 1f), Center, Facing, Radius, out _);

            Assert.IsFalse(hit);
        }

        //  평면에 못 미치고 멈춘 화살은 아직 안 맞은 것이다(다음 틱에 판정된다).
        [Test]
        public void 평면에_못_닿으면_안_맞는다()
        {
            bool hit = ArcheryHitTest.SegmentHitsFace(
                new Vector3(0f, 0f, -2f), new Vector3(0f, 0f, -1f), Center, Facing, Radius, out _);

            Assert.IsFalse(hit);
        }

        //  판과 나란히 가는 화살은 평면을 지나지 않는다.
        [Test]
        public void 판과_나란히_가면_안_맞는다()
        {
            bool hit = ArcheryHitTest.SegmentHitsFace(
                new Vector3(-1f, 0f, -0.5f), new Vector3(1f, 0f, -0.5f), Center, Facing, Radius, out _);

            Assert.IsFalse(hit);
        }

        //  맞은 자리는 중심에서 얼마나 벗어났는지를 반지름으로 나눈 값이다.
        [Test]
        public void 맞은_자리가_중심에서_얼마나_벗어났는지_준다()
        {
            var target = Face();

            ArcheryHitTest.SegmentHitsTarget(
                new Vector3(0.2f, 0f, -1f), new Vector3(0.2f, 0f, 1f), Center, target,
                out _, out float offset);

            //  0.2m 벗어났고 반지름이 0.4m이므로 절반이다.
            Assert.AreEqual(0.5f, offset, 1e-4f);
        }

        [Test]
        public void 정중앙은_맞은_자리가_0이다()
        {
            var target = Face();

            ArcheryHitTest.SegmentHitsTarget(
                new Vector3(0f, 0f, -1f), new Vector3(0f, 0f, 1f), Center, target,
                out _, out float offset);

            Assert.AreEqual(0f, offset, 1e-4f);
        }

        //  공은 예전 판정 그대로 돈다 — 옆에서 와도 맞는다.
        [Test]
        public void 공은_옆에서_와도_맞는다()
        {
            var sphere = new ArcheryTarget(
                waveIndex: 0, slotIndex: 0,
                origin: Center, riseSpeed: 0f, spawnTick: 0L,
                radius: Radius, points: 1, isTrap: false,
                shape: ArcheryTargetShape.Sphere, bands: null, facing: Vector3.zero);

            bool hit = ArcheryHitTest.SegmentHitsTarget(
                new Vector3(-1f, 0f, 0f), new Vector3(1f, 0f, 0f), Center, sphere, out _, out _);

            Assert.IsTrue(hit);
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
```

기대: `failed:true`. `SegmentHitsFace`·`SegmentHitsTarget`를 찾을 수 없음(CS0117).

- [ ] **Step 3: 최소 구현**

`Runtime/Scripts/Game/ArcheryHitTest.cs`의 `SegmentHitsSphere` 뒤에 더한다:

```csharp
        /// <summary>
        /// 화살 선분이 <b>사수를 향해 선 원판</b>을 맞혔는지 본다.
        /// <paramref name="facing"/> 쪽에서 오는 화살만 맞는다 — 뒤에서 온 것은 통과한다.
        /// 맞았으면 <paramref name="t"/>에 선분 위 어디서 평면을 지났는지(0~1)를 담는다.
        /// </summary>
        public static bool SegmentHitsFace(Vector3 from, Vector3 to, Vector3 center, Vector3 facing,
                                           float radius, out float t)
        {
            t = 0f;

            Vector3 segment = to - from;
            float approach = Vector3.Dot(segment, facing);
            //  화살이 판이 보는 쪽으로 다가가고 있어야 한다. 0이면 판과 나란히 가는 것이고,
            //  양수면 뒤에서 오는 것이다.
            if (approach >= -1e-9f)
            {
                return false;
            }

            //  판이 보는 쪽에서 출발했는지 본다. 이미 판을 지나쳐 있었다면 뒤에서 오는 것이다.
            float startSide = Vector3.Dot(from - center, facing);
            if (startSide <= 0f)
            {
                return false;
            }

            //  선분이 판의 평면을 지나는 지점을 푼다.
            float cross = startSide / -approach;
            if (cross < 0f || cross > 1f)
            {
                return false;   // 이번 틱에는 평면까지 못 간다
            }

            Vector3 impact = from + segment * cross;
            if ((impact - center).sqrMagnitude > radius * radius)
            {
                return false;   // 평면은 지났지만 판 밖이다
            }

            t = cross;
            return true;
        }

        /// <summary>
        /// 과녁 모양에 맞는 판정을 고른다. <b>소비처는 이 함수만 부른다</b> — 모양이 늘어도
        /// 부르는 쪽은 안 바뀐다.
        /// <paramref name="normalizedOffset"/>은 맞은 자리가 중심에서 얼마나 벗어났는지를
        /// 반지름으로 나눈 값이다(0이 정중앙, 1이 가장자리). 채점이 이 값으로 띠를 찾는다.
        /// </summary>
        public static bool SegmentHitsTarget(Vector3 from, Vector3 to, Vector3 center,
                                             in ArcheryTarget target,
                                             out float t, out float normalizedOffset)
        {
            normalizedOffset = 0f;

            bool hit = target.Shape == ArcheryTargetShape.Face
                ? SegmentHitsFace(from, to, center, target.Facing, target.Radius, out t)
                : SegmentHitsSphere(from, to, center, target.Radius, out t);

            if (hit == false)
            {
                return false;
            }

            if (target.Radius > 1e-6f)
            {
                Vector3 impact = from + (to - from) * t;
                normalizedOffset = Mathf.Clamp01((impact - center).magnitude / target.Radius);
            }
            return true;
        }
```

> 공은 겉면에 맞으므로 벗어난 값이 늘 1에 가깝다. 그래서 **공은 띠가 하나여야 한다** — 배포 데이터 검사가 그것을 지킨다(Task 5).

- [ ] **Step 4: 통과를 확인한다**

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server run_tests --mode EditMode --filter ArcheryFaceHitTests
```

기대: 여덟 테스트가 **이름으로** 보이고 전부 통과.

- [ ] **Step 5: 커밋**

```
feat(archery): 사수를 향해 선 판 과녁을 판정한다

동심원 채점을 하려면 "맞은 자리"가 있어야 하는데, 공은 옆에서 스쳐도 맞아서
그 자리에 뜻이 없다. 사수를 향해 선 원판이어야 중심에서 얼마나 벗어났는지가
말이 된다.

소비처가 모양을 알 필요가 없도록 진입점을 하나로 뒀다. 공은 예전 판정이
그대로 돈다.
```

---

## Task 4: 소비처가 맞은 자리를 채점에 넘긴다 (Server + Client)

**Files:**
- Modify: 서버 `Assets/Scripts/Game/TickSystems/ArcheryHitSystem.cs`
- Modify: 클라 `Assets/Scripts/Game/TickSystems/ArcheryArrowStickSystem.cs`
- Test: 서버 `Assets/Tests/Editor/ArcheryHitSystemTests.cs`

**Interfaces:**
- Consumes: `ArcheryHitTest.SegmentHitsTarget(...)` (Task 3), `ArcheryHitRules.Resolve(in ArcheryTarget, float)` (Task 2)
- Produces: (없음 — 배선)

- [ ] **Step 1: 현재 통과 수를 적어 둔다**

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server run_tests --mode EditMode --filter ArcheryHitSystemTests
```

통과 수를 보고서에 적는다. **이 태스크 뒤에 줄면 안 된다.**

- [ ] **Step 2: 실패하는 테스트를 쓴다**

서버 `Assets/Tests/Editor/ArcheryHitSystemTests.cs`의 마지막 `[Test]` 뒤에 더한다:

```csharp
        //  판정이 "어디에 맞았나"를 채점까지 흘려보내야 동심원 과녁의 띠가 갈린다.
        //  지금 배포 과녁은 띠가 하나라 점수가 안 바뀌므로, 여기서만 판 과녁을 만들어 확인한다.
        [Test]
        public void 판_과녁은_맞은_자리에_따라_점수가_갈린다()
        {
            var bands = new System.Collections.Generic.List<ArcheryRingBand>
            {
                new ArcheryRingBand(0.25f, 10),
                new ArcheryRingBand(1.0f, 3),
            };

            var target = new ArcheryTarget(
                waveIndex: 0, slotIndex: 0,
                origin: Vector3.zero, riseSpeed: 0f, spawnTick: 0L,
                radius: 0.4f, points: 0, isTrap: false,
                shape: ArcheryTargetShape.Face, bands: bands,
                facing: new Vector3(0f, 0f, -1f));

            //  정중앙을 지나는 선분과, 가장자리 쪽을 지나는 선분.
            ArcheryHitTest.SegmentHitsTarget(
                new Vector3(0f, 0f, -1f), new Vector3(0f, 0f, 1f),
                Vector3.zero, target, out _, out float centerOffset);
            ArcheryHitTest.SegmentHitsTarget(
                new Vector3(0.3f, 0f, -1f), new Vector3(0.3f, 0f, 1f),
                Vector3.zero, target, out _, out float edgeOffset);

            Assert.AreEqual(10, ArcheryHitRules.Resolve(target, centerOffset).Gained);
            Assert.AreEqual(3, ArcheryHitRules.Resolve(target, edgeOffset).Gained);
        }
```

> 이 테스트는 Task 2·3이 끝났으면 **바로 통과한다.** 이 태스크가 고치는 것은 *시스템이 그 값을 실제로 흘려보내는가*이고, 그 증거는 Step 5에서 기존 테스트가 줄지 않는 것이다.

- [ ] **Step 3: 서버 판정을 새 진입점으로 바꾼다**

`ArcheryHitSystem.cs`의 `CollectCandidates` 안, 과녁을 검사하는 줄을 바꾼다. 기존:

```csharp
                    if (ArcheryHitTest.SegmentHitsSphere(from, to, targetAt, targets[i].Radius, out float t))
                    {
                        candidates.Add(new Candidate(t, shot.ShooterId, shot.FireTick, targets[i].SlotIndex));
                    }
```

바꾼 뒤:

```csharp
                    if (ArcheryHitTest.SegmentHitsTarget(from, to, targetAt, targets[i],
                                                         out float t, out float offset))
                    {
                        candidates.Add(new Candidate(t, offset, shot.ShooterId, shot.FireTick,
                                                     targets[i].SlotIndex));
                    }
```

같은 파일 안의 `Candidate` 구조체를 아래로 바꾼다(`Offset`을 `T` 바로 뒤에 둬 순서가 헷갈리지 않게 한다):

```csharp
        private readonly struct Candidate
        {
            public readonly float T;

            /// <summary>맞은 자리가 중심에서 얼마나 벗어났나(0~1). 채점이 띠를 찾는 데 쓴다.</summary>
            public readonly float Offset;

            public readonly string ShooterId;
            public readonly long FireTick;
            public readonly int Slot;

            public Candidate(float t, float offset, string shooterId, long fireTick, int slot)
            {
                T = t; Offset = offset; ShooterId = shooterId; FireTick = fireTick; Slot = slot;
            }
        }
```

그리고 `ApplyCandidates`에서 채점할 때 그 값을 넘긴다:

```csharp
                //  Task 2에서 임시로 넣은 0f를 여기서 진짜 값으로 바꾼다.
                var outcome = ArcheryHitRules.Resolve(TargetOfSlot(candidate.Slot), candidate.Offset);
```

> Task 2에서 임시로 넣은 `0f`를 여기서 진짜 값으로 바꾸는 것이다. `0f`가 남아 있으면 안 된다.

- [ ] **Step 4: 클라 꽂힘 판정도 같은 진입점으로 바꾼다**

클라 `ArcheryArrowStickSystem.cs`의 `CrossesLiveTarget` 안:

```csharp
                if (ArcheryHitTest.SegmentHitsTarget(from, to, targetAt, targets[i], out float t, out _))
```

> 클라는 점수를 계산하지 않으므로 맞은 자리는 버린다(`out _`). **서버와 같은 판정 함수를 쓰는 것**이 중요하다 — 다르면 화면에선 꽂혔는데 점수는 안 나거나 그 반대가 된다.

- [ ] **Step 5: 통과를 확인한다**

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server run_tests --mode EditMode --filter ArcheryHitSystemTests
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client recompile_status
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client get_console_logs --types error --count 20
```

기대: 서버 테스트가 Step 1에서 적어 둔 수보다 **줄지 않고**(새 테스트만큼 늘고), 새 테스트가 이름으로 보인다. 클라 컴파일 초록 + 콘솔 CS 에러 0.

- [ ] **Step 6: 임시값이 안 남았는지 확인한다**

```bash
grep -rn "ArcheryHitRules.Resolve(" \
  /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets \
  /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets
```

런타임 코드(`Assets/Scripts/` 아래)에 `Resolve(..., 0f)`가 남아 있으면 안 된다. 테스트 픽스처는 무방하다.

- [ ] **Step 7: 커밋 (레포 둘)**

서버:
```
feat(archery): 맞은 자리를 채점까지 흘려보낸다

판정은 이미 화살이 과녁의 어디를 지났는지 알고 있었는데, 채점에 넘기지
않아 늘 같은 띠가 나왔다. 동심원 과녁은 그 값이 곧 점수다.
```

클라:
```
chore(archery): 꽂힘 판정을 모양별 진입점으로 바꾼다

서버와 같은 판정 함수를 써야 화면과 점수가 안 갈린다.
```

---

## Task 5: 마스터데이터에 모양과 띠를 넣는다 (infrastructure + MasterData 둘 + Server/Client)

**Files:**
- Modify: `infrastructure/table/Datas/#ArcheryTarget.xlsx`
- Create: `infrastructure/table/Datas/#ArcheryRing.xlsx`
- Modify: `LeagueOfPhysical-MasterData-{Client,Server}/Runtime/Scripts/LOPMasterData.cs`
- Modify: `LeagueOfPhysical-MasterData-Server/Tests/EditMode/ArcheryTargetSeparationTests.cs`
- Modify: 서버·클라 `Assets/Scripts/Game/ArcheryConfigProvider.cs`

**Interfaces:**
- Consumes: `ArcheryTargetKind(..., shape, bands)` (Task 1), `ArcheryRingBand` (Task 1)
- Produces: Luban 생성 `TbArcheryTarget.Shape`, 새 표 `TbArcheryRing(Id, TargetId, OuterRatio, Points)`

- [ ] **Step 1: 엑셀을 고친다**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table
python - <<'PY'
import openpyxl

#  ① #ArcheryTarget에 shape 컬럼을 맨 뒤에 붙인다.
wb = openpyxl.load_workbook('Datas/#ArcheryTarget.xlsx')
ws = wb.worksheets[0]
c = ws.max_column + 1
ws.cell(row=1, column=c, value='shape')
ws.cell(row=2, column=c, value='int')
ws.cell(row=4, column=c, value='shape')
#  0 = Sphere. 지금 과녁은 전부 공이다.
for r in range(5, ws.max_row + 1):
    if ws.cell(row=r, column=2).value is not None:
        ws.cell(row=r, column=c, value=0)
wb.save('Datas/#ArcheryTarget.xlsx')

#  ② #ArcheryRing을 새로 만든다. 과녁 종류마다 띠 하나씩 —
#     지금 과녁은 "어디를 맞히든 같은 점수"라 바깥 경계 1인 띠 하나면 된다.
#     points는 #ArcheryTarget의 points와 같아야 한다(배포 데이터 검사가 지킨다).
rows = [(1, 1, 1.0, 1), (2, 2, 1.0, 2), (3, 3, 1.0, 4),
        (4, 4, 1.0, -3), (5, 5, 1.0, -5), (6, 6, 1.0, -10)]
wb2 = openpyxl.Workbook()
ws2 = wb2.active
ws2.append(['##var', 'id', 'target_id', 'outer_ratio', 'points'])
ws2.append(['##type', 'int', 'int', 'float', 'int'])
ws2.append(['##group', None, None, None, None])
ws2.append(['##', 'id', 'target_id', 'outer_ratio', 'points'])
for r in rows:
    ws2.append([None, *r])
wb2.save('Datas/#ArcheryRing.xlsx')
print('done')
PY
```

- [ ] **Step 2: 엑셀을 눈으로 확인한다**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table
python -c "
import openpyxl
for f in ['#ArcheryTarget','#ArcheryRing']:
    print('===', f)
    ws = openpyxl.load_workbook('Datas/%s.xlsx' % f, data_only=True).worksheets[0]
    for row in ws.iter_rows(values_only=True):
        if any(c is not None for c in row): print(row)
"
```

기대: `#ArcheryTarget`의 기존 여섯 줄에서 `radius/points/weight/is_trap`이 **하나도 안 바뀌고** 끝에 `shape=0`이 붙었다. `#ArcheryRing`은 종류 여섯에 띠 하나씩이고 `points`가 `#ArcheryTarget`의 `points`와 같다.

**하나라도 다르면 멈추고 보고한다.**

- [ ] **Step 3: 굽는다**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table && ./gen.sh
git -C /c/Users/re5na/workspace/LOP/infrastructure status --short
git -C /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client status --short
git -C /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server status --short
```

기대: 셋 다 변경 있음. `lop-backend`에는 변경이 없어야 한다(있으면 보고).

생성물에 새 표가 들어갔는지 확인한다:

```bash
ls /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server/Runtime.Generated/StreamingAssets/MasterData/ | grep archery
```

- [ ] **Step 4: `TableFiles`에 새 표를 더한다**

**이걸 빠뜨리면 로딩이 `KeyNotFoundException`으로 죽는다.** 양쪽 패키지(`-Client`, `-Server`)의 `Runtime/Scripts/LOPMasterData.cs`에서 `TableFiles` 배열 끝을 바꾼다:

```csharp
            "tbarcheryconfig", "tbarcherytarget", "tbarcheryring"
```

- [ ] **Step 5: provider가 모양과 띠를 채우게 한다**

서버·클라 `ArcheryConfigProvider.cs`의 종류를 만드는 루프를 바꾼다. 기존:

```csharp
                kinds.Add(new ArcheryTargetKind(row.Radius, row.Points, row.Weight, row.IsTrap));
```

바꾼 뒤:

```csharp
                kinds.Add(new ArcheryTargetKind(row.Radius, row.Points, row.Weight, row.IsTrap,
                                                (ArcheryTargetShape)row.Shape, BandsOf(md, row.Id)));
```

같은 클래스 안에 헬퍼를 더한다:

```csharp
        //  그 과녁 종류의 띠를 중심에서 바깥 순서로 모은다. 순서가 뒤집히면 바깥 띠가 먼저
        //  걸려서 한가운데를 맞혀도 낮은 점수가 나온다.
        private static List<ArcheryRingBand> BandsOf(LOP.MasterData.LOPMasterData md, int targetId)
        {
            var bands = new List<ArcheryRingBand>();
            foreach (var row in System.Linq.Enumerable.OrderBy(
                         System.Linq.Enumerable.Where(md.Tables.TbArcheryRing.DataList,
                                                      x => x.TargetId == targetId),
                         x => x.OuterRatio))
            {
                bands.Add(new ArcheryRingBand(row.OuterRatio, row.Points));
            }
            return bands;
        }
```

**두 파일을 대조한다:**

```bash
diff /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Game/ArcheryConfigProvider.cs \
     /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Game/ArcheryConfigProvider.cs
```

**10번째 줄(서로 상대편을 가리키는 주석) 하나만 나와야 한다.** 그 밖의 차이가 있으면 멈추고 보고한다.

- [ ] **Step 6: 배포 데이터 검사를 더한다**

`LeagueOfPhysical-MasterData-Server/Tests/EditMode/ArcheryTargetSeparationTests.cs`에 더한다(기존 `LoadTables()` 헬퍼를 쓴다):

```csharp
        //  띠가 중심부터 가장자리까지 빈틈없이 덮어야 한다. 마지막 띠가 1에 못 미치면
        //  가장자리에 맞은 화살이 어느 띠에도 안 걸려 점수가 0이 된다 — 에러는 안 난다.
        [Test]
        public void 모든_과녁의_띠가_가장자리까지_덮는다()
        {
            var tables = LoadTables();

            foreach (var target in tables.TbArcheryTarget.DataList)
            {
                var edges = new List<float>();
                foreach (var ring in tables.TbArcheryRing.DataList)
                {
                    if (ring.TargetId == target.Id) { edges.Add(ring.OuterRatio); }
                }

                Assert.IsNotEmpty(edges, $"과녁 {target.Code}(id={target.Id})에 띠가 하나도 없다");
                edges.Sort();
                Assert.AreEqual(1f, edges[edges.Count - 1], 1e-4f,
                    $"과녁 {target.Code}의 마지막 띠가 가장자리(1.0)까지 안 간다");
                Assert.Greater(edges[0], 0f, $"과녁 {target.Code}의 첫 띠 경계가 0 이하다");
            }
        }

        //  공은 겉면에 맞으므로 "중심에서 얼마나 벗어났나"가 늘 1에 가깝다 — 띠를 여러 개 줘도
        //  바깥 띠만 걸린다. 그런 데이터는 적은 사람의 뜻과 다르게 동작하므로 막는다.
        [Test]
        public void 공_과녁은_띠가_하나뿐이다()
        {
            var tables = LoadTables();

            foreach (var target in tables.TbArcheryTarget.DataList)
            {
                if (target.Shape != 0) { continue; }   // 0 = Sphere

                int count = 0;
                foreach (var ring in tables.TbArcheryRing.DataList)
                {
                    if (ring.TargetId == target.Id) { count++; }
                }

                Assert.AreEqual(1, count,
                    $"공 과녁 {target.Code}에 띠가 {count}개다 — 공은 맞은 자리를 가릴 수 없다");
            }
        }

        //  띠가 하나인 과녁은 그 점수가 대표 점수와 같아야 한다. 다르면 "띠 데이터가 없을 때"와
        //  "있을 때"의 점수가 갈리는데, 코드는 둘 다 정상으로 받아들여 조용히 다르게 동작한다.
        [Test]
        public void 띠가_하나인_과녁은_그_점수가_대표_점수와_같다()
        {
            var tables = LoadTables();

            foreach (var target in tables.TbArcheryTarget.DataList)
            {
                var points = new List<int>();
                foreach (var ring in tables.TbArcheryRing.DataList)
                {
                    if (ring.TargetId == target.Id) { points.Add(ring.Points); }
                }
                if (points.Count != 1) { continue; }

                Assert.AreEqual(target.Points, points[0],
                    $"과녁 {target.Code}의 띠 점수({points[0]})가 대표 점수({target.Points})와 다르다");
            }
        }
```

- [ ] **Step 7: 검사에 이빨이 있는지 직접 깨서 확인한다**

`#ArcheryRing.xlsx`의 `id=1` 행 `outer_ratio`를 잠깐 `0.5`로 바꾸고 다시 굽는다:

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table
python -c "
import openpyxl
wb = openpyxl.load_workbook('Datas/#ArcheryRing.xlsx'); ws = wb.worksheets[0]
ws.cell(row=5, column=4, value=0.5); wb.save('Datas/#ArcheryRing.xlsx')
"
./gen.sh
```

`모든_과녁의_띠가_가장자리까지_덮는다`가 **빨간불이 되는지** 확인한 뒤 되돌린다:

```bash
python -c "
import openpyxl
wb = openpyxl.load_workbook('Datas/#ArcheryRing.xlsx'); ws = wb.worksheets[0]
ws.cell(row=5, column=4, value=1.0); wb.save('Datas/#ArcheryRing.xlsx')
"
./gen.sh
git -C /c/Users/re5na/workspace/LOP/infrastructure status --short
```

되돌린 뒤 초록을 다시 확인하고, 엑셀·생성물이 원래대로인지 `git status`로 본다. **확인 결과를 보고서에 적는다.**

- [ ] **Step 8: 컴파일·테스트**

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server run_tests --mode EditMode --filter ArcheryTargetSeparationTests
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client recompile_status
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client get_console_logs --types error --count 20
```

- [ ] **Step 9: 커밋 (레포 넷)**

infrastructure:
```
feat(archery): 과녁에 모양 컬럼과 띠 표를 낸다

동심원 채점에 쓸 띠를 데이터로 낸다. 지금 과녁은 전부 공이고 "어디를
맞히든 같은 점수"라, 바깥 경계 1인 띠 하나씩을 넣어 뜻이 그대로 유지된다.

컬럼은 맨 뒤에 붙였다 — Luban은 열 위치로 읽으므로 중간에 끼우면 기존 값이
조용히 뒤바뀐다.
```

MasterData 둘: `chore(masterdata): 과녁 모양·띠를 굽는다`
서버·클라: `feat(archery): provider가 과녁 모양과 띠를 채운다`

---

## Task 6: 설정을 맵별로 가른다 (infrastructure + MasterData + Server/Client)

**Files:**
- Modify: `infrastructure/table/Datas/#ArcheryConfig.xlsx`
- Modify: 서버·클라 `Assets/Scripts/Game/ArcheryConfigProvider.cs`
- Modify: `LeagueOfPhysical-MasterData-Server/Tests/EditMode/ArcheryTargetSeparationTests.cs`

**Interfaces:**
- Consumes: `MatchSceneResolver.CurrentRoundIndex(int roundCount)` (기존, LOP-Shared)
- Produces: `ArcheryConfigProvider(LOP.MasterData.LOPMasterData md, IRoomDataStore roomDataStore)` — **생성자 인자가 하나 늘어난다**

- [ ] **Step 1: 엑셀의 id를 맵 id로 바꾼다**

원형 맵의 맵 id는 **5**다(`#Map.xlsx`의 `archery_circle` 행).

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table
python - <<'PY'
import openpyxl
wb = openpyxl.load_workbook('Datas/#ArcheryConfig.xlsx')
ws = wb.worksheets[0]
#  이 표의 id는 이제 "어느 맵의 설정인가"를 뜻한다.
assert ws.cell(row=5, column=2).value == 1, '예상과 다른 id다 — 멈추고 확인할 것'
ws.cell(row=5, column=2, value=5)
wb.save('Datas/#ArcheryConfig.xlsx')
print('done')
PY
```

- [ ] **Step 2: 확인하고 굽는다**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table
python -c "
import openpyxl
ws = openpyxl.load_workbook('Datas/#ArcheryConfig.xlsx', data_only=True).worksheets[0]
for row in ws.iter_rows(values_only=True):
    if any(c is not None for c in row): print(row)
"
./gen.sh
```

기대: 데이터 줄의 `id`만 5로 바뀌고 **그 밖의 값은 하나도 안 바뀐다**(`wave_period_ticks` 120, `min_targets` 3, `max_targets` 5, `spawn_radius` 3.5, `spawn_min_y` 0.3, `spawn_max_y` 0.6, `min_separation` 1.2, `rise_height_min` 1.2, `rise_height_max` 2.4, `stagger_ticks` 12, `rest_ticks` 20).

- [ ] **Step 3: provider가 맵 id로 찾게 한다**

서버·클라 `ArcheryConfigProvider.cs`를 바꾼다. 필드와 생성자:

```csharp
        private readonly LOP.MasterData.LOPMasterData md;
        private readonly IRoomDataStore roomDataStore;

        public ArcheryConfigProvider(LOP.MasterData.LOPMasterData md, IRoomDataStore roomDataStore)
        {
            this.md = md;
            this.roomDataStore = roomDataStore;
        }
```

`Get()`의 첫 부분을 바꾼다:

```csharp
        public ArcheryConfig Get()
        {
            //  설정은 맵마다 다르다 — 같은 활쏘기라도 사거리 맵과 원형 맵은 과녁이 다르게 뜬다.
            //  이번 라운드가 가리키는 맵을 그대로 쓴다(씬을 고를 때와 같은 출처).
            int mapId = CurrentMapId();
            var r = md.Tables.TbArcheryConfig.GetOrDefault(mapId);
            if (r == null)
            {
                throw new System.InvalidOperationException(
                    $"TbArcheryConfig에 맵 {mapId}의 행이 없음 — 활쏘기 맵을 추가했으면 설정 행도 같이 넣어야 한다");
            }
```

(그 아래 `kinds` 조립과 `return new ArcheryConfig(...)`는 그대로 둔다.)

같은 클래스에 헬퍼를 더한다:

```csharp
        //  씬을 고를 때와 같은 출처를 쓴다 — 두 곳이 다른 라운드를 보면 맵과 설정이 어긋난다.
        private int CurrentMapId()
        {
            var rounds = roomDataStore.match?.rounds;
            int index = MatchSceneResolver.CurrentRoundIndex(rounds?.Length ?? 0);
            return rounds[index].mapId;
        }
```

**두 파일을 대조한다 — 10행 말고는 차이가 없어야 한다:**

```bash
diff /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Game/ArcheryConfigProvider.cs \
     /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Game/ArcheryConfigProvider.cs
```

- [ ] **Step 4: DI 등록을 맞춘다**

`ArcheryConfigProvider`의 생성자 인자가 늘었다. **등록처와 소비처는 다른 파일이라 컴파일은 통과하고 방에 들어가야 터진다** — 반드시 둘을 같이 본다.

```bash
grep -rn "ArcheryConfigProvider\|IRoomDataStore" \
  /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts \
  /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts | grep -i "register\|scope"
```

등록이 `builder.Register<ArcheryConfigProvider>(Lifetime.Singleton)` 형태면 VContainer가 생성자 주입을 해 주므로, **`IRoomDataStore`가 같은 스코프나 부모 스코프에 등록돼 있는지**만 확인한다. 없으면 그 스코프에 등록을 더한다. 등록이 `c => new ArcheryConfigProvider(...)` 형태면 인자를 직접 더한다.

- [ ] **Step 5: 배포 데이터 검사를 더한다**

`ArcheryTargetSeparationTests.cs`에 더한다:

```csharp
        //  활쏘기 맵을 새로 추가하면서 설정 행을 안 넣으면, 방에 들어가야 예외를 본다.
        //  여기서 먼저 막는다.
        [Test]
        public void 활쏘기_맵마다_설정_행이_있다()
        {
            var tables = LoadTables();

            foreach (var map in tables.TbMap.DataList)
            {
                var mode = tables.TbGameMode.GetOrDefault(map.GameModeId);
                if (mode == null || mode.Code != "Archery") { continue; }

                Assert.IsNotNull(tables.TbArcheryConfig.GetOrDefault(map.Id),
                    $"활쏘기 맵 {map.Code}(id={map.Id})에 TbArcheryConfig 행이 없다");
            }
        }
```

- [ ] **Step 6: 이빨을 확인한다**

`#ArcheryConfig.xlsx`의 id를 잠깐 `99`로 바꿔 다시 굽고 `활쏘기_맵마다_설정_행이_있다`가 빨간불이 되는지 본다. 확인했으면 `5`로 되돌리고 다시 구워 초록을 확인한다. `git status`로 원래대로인지 본다. **확인 결과를 보고서에 적는다.**

- [ ] **Step 7: 컴파일·전체 테스트**

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server run_tests --mode EditMode
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client recompile_status
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client get_console_logs --types error --count 20
```

기대: 서버 EditMode **전부 초록, 실패 0**. 직전 기준선은 1125 통과였고 이 계획에서 테스트가 늘었으므로 수는 늘어난다. 클라 컴파일 초록 + 콘솔 CS 에러 0.

- [ ] **Step 8: 커밋 (레포 넷)**

infrastructure:
```
feat(archery): 활쏘기 설정을 맵별로 가른다

전역 한 줄이라 모든 활쏘기 맵이 같은 과녁을 썼다. 사거리 맵은 과녁이 전혀
다르게 떠야 하므로, 설정 행의 id를 맵 id로 바꿔 맵마다 한 줄을 갖게 한다.

원형 맵은 지금 값을 그대로 옮긴 행(id=5)을 받는다.
```

MasterData 둘: `chore(masterdata): 맵별 활쏘기 설정을 굽는다`
서버·클라: `feat(archery): provider가 이번 라운드의 맵 설정을 읽는다`

---

## Task 7: 머지 · 배포 · 실물 확인

**Files:**
- Modify: `docs/ROADMAP.md` (클라)

**Interfaces:**
- Consumes: Task 1~6 전부

- [ ] **Step 1: 마지막으로 전부 초록인지 확인한다**

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server run_tests --mode EditMode
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client recompile_status
```

- [ ] **Step 2: 배포 데이터가 클·서 같은지 대조한다**

```bash
for f in tbarcheryconfig tbarcherytarget tbarcheryring; do
  c=$(md5sum /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client/Runtime.Generated/StreamingAssets/MasterData/$f.bytes | cut -d' ' -f1)
  s=$(md5sum /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server/Runtime.Generated/StreamingAssets/MasterData/$f.bytes | cut -d' ' -f1)
  printf "%-20s %s\n" $f "$([ "$c" = "$s" ] && echo 동일 || echo 다름)"
done
```

**하나라도 다르면 멈춘다.** 클·서가 다른 과녁을 보게 된다.

- [ ] **Step 3: ROADMAP을 갱신한다**

`docs/ROADMAP.md`의 활쏘기 절 위에 이 토대 절을 쓴다. 담을 것:

- 왜 이 토대인가 — 양궁 사거리 맵(스펙 `2026-09-15-archery-range-map-design.md`)이 설 자리
- 더한 개념 셋과 **각각의 기존 동작이 그 특수형**이라는 것: 지금의 고정 점수 = 띠 하나, 지금의 구 = `Sphere`, 전역 설정 한 줄 = 원형 맵의 행
- **공 과녁은 띠를 여러 개 못 쓴다**는 제약과 그것을 지키는 배포 데이터 검사
- 원형 맵이 그대로 도는 것이 합격 기준이었다는 것과 그 확인 결과

- [ ] **Step 4: 여섯 레포를 규약대로 머지한다**

레포: `LeagueOfPhysical-Shared` · `LeagueOfPhysical-MasterData-Client` · `LeagueOfPhysical-MasterData-Server` · `LeagueOfPhysical-Server` · `LeagueOfPhysical-Client` · `infrastructure`

레포마다 **한 줄씩 결과를 확인하며**:

```bash
git fetch origin
git rebase --autostash origin/main
git checkout main
git merge --ff-only origin/main
git merge --no-ff <feature>
git push origin main
```

**`&&`로 잇지 말 것.** 실패한 단계를 지나쳐도 뒤 단계가 성공해 버려 *푸시는 됐는데 절차는 안 밟은* 상태가 된다.

⚠️ **원격이 그사이 움직였을 수 있다**(기계가 둘이다). `git fetch` 뒤 `git rev-list --left-right --count origin/main...HEAD`로 확인하고, 원격이 앞서 있으면 **반드시 그 위로 리베이스한 뒤** 머지한다.

Unity 레포는 `--autostash`가 dirty한 Art 서브모듈과 얽혀 막히는 일이 있다 — 그때는 픽스처를 `git stash push -u -m ... -- <경로>`로 직접 빼고 리베이스한 뒤 `pop`한다. 머지에 막히면 `git -c merge.autoStash=false merge --no-ff ...`를 쓴다.

클라는 머지 뒤 Art 포인터가 원격과 같은지 확인한다:

```bash
[ "$(git ls-tree HEAD Assets/Art | awk '{print $3}')" = "$(git ls-tree origin/main Assets/Art | awk '{print $3}')" ] && echo 같다 || echo 다르다
```

- [ ] **Step 5: 배포한다**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
gh workflow run gameserver-deploy -f environment=local -f package_ref=main
gh run list --workflow gameserver-deploy --limit 1
```

끝나면 **클러스터의 실제 값**을 읽어 서버 main과 대조한다(워크플로 성공만 보지 않는다):

```bash
git -C /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server rev-parse --short=7 origin/main
kubectl -n default get cm -o name | grep game | xargs -I{} kubectl -n default get {} -o jsonpath='{.data.GAME_SERVER_IMAGE}'
```

> ArgoCD 동기화에 1분쯤 걸린다. 바로 읽으면 옛 값이 보이므로, 태그가 안 바뀌었으면 **잠시 뒤 다시 읽는다.** 한 번 읽고 "안 움직였다"고 단정하지 않는다.

- [ ] **Step 6: 원형 맵이 그대로인지 실물로 확인한다 (⭐ 이 계획의 합격 기준)**

두 클라를 띄워 원형 맵 한 판을 돌리고 확인한다:

- **클·서가 계산한 과녁 목록이 글자 단위로 같다** — 좌표·솟는 속도·솟는 틱·반지름·점수·함정 여부
- **점수가 예전과 같다** — 큰 과녁 +1, 중간 +2, 작은 +4, 함정 −3/−5/−10
- 화살이 과녁에 제대로 꽂힌다
- 콘솔에 새 예외가 없다

- [ ] **Step 7: 결과를 기록한다**

원형 맵이 한 군데라도 달라졌으면 **그 사실을 크게 적는다.** 이 계획은 "아무것도 안 바뀌어야 한다"가 목표였으므로, 달라졌다면 다음 계획(사거리 맵)을 시작하기 전에 고쳐야 한다.

---

## 확인 목록 (머지 전에 한 번 더)

- [ ] **`ArcheryWaveGenerator.Fill`의 난수 호출이 하나도 안 늘고 안 옮겨졌다**
- [ ] 클·서 `ArcheryConfigProvider`가 **10행 말고 완전히 같다**
- [ ] `tbarcheryconfig`/`tbarcherytarget`/`tbarcheryring`의 `.bytes`가 **클·서 md5 동일**
- [ ] `LOPMasterData.TableFiles`에 `tbarcheryring`이 **양쪽 패키지 다** 들어갔다
- [ ] `#ArcheryTarget`의 기존 값(`radius/points/weight/is_trap`)이 **하나도 안 바뀌었다**
- [ ] `#ArcheryConfig`에서 **`id`만** 바뀌고 나머지 값은 그대로다
- [ ] 런타임 코드에 `ArcheryHitRules.Resolve(..., 0f)` 임시값이 **안 남았다**
- [ ] 새로 만든 `.cs` 넷(`ArcheryRingBand`, `ArcheryTargetShape`, 테스트 둘)에 `.meta`가 함께 스테이지됐다
- [ ] 두 Unity 레포의 커밋에 로컬 픽스처가 **안 섞였다**
- [ ] 서버 EditMode 전부 초록, 새 테스트가 **이름으로** 보인다(개수만 보지 않는다)
- [ ] **기존 채점 테스트의 기대값을 한 자리도 안 고쳤다**

---

## 자기 점검 (계획을 쓴 뒤)

**spec 대응:**

| spec | 태스크 |
|---|---|
| §2.3 설정을 맵별로 가른다 | Task 6 |
| §3.1 과녁은 구가 아니라 판이다 | Task 1(모양 값) + Task 3(판 판정) |
| §3.2 링 점수는 지금 점수 방식의 일반형 | Task 1(띠 타입) + Task 2(채점) + Task 5(데이터) |
| §7 기존 원형 맵을 안 깬다 | 모든 태스크의 합격 기준 + Task 7 Step 6 |
| §8.1 순수 로직 테스트 | Task 1·2·3의 테스트 |
| §8.2 배포 데이터 검사 | Task 5 Step 6, Task 6 Step 5 |
| §8.3 회귀 | Task 2 Step 5, Task 4 Step 5, Task 6 Step 7 |

**이 계획이 다루지 않는 spec 절**(다음 계획 — 사거리 맵):
§3.3 거리별 난이도 · §4 진행(순서·노출·화살) · §5 예약 · §6.1 판당 난수 1회 · §6.2 시작 시점 명단 ·
§6.3 사수별 먹힌 과녁 · 레인 컴포넌트와 맵 씬 · §8.2의 사거리·노출 시간 검사.

**타입 일관성** — Task 1~6에서 쓰인 이름이 전부 일치한다:

- `ArcheryRingBand(float outerRatio, int points)` · `.OuterRatio` · `.Points`
- `ArcheryTargetShape.{Sphere, Face}` (Sphere = 0)
- `ArcheryTargetKind(float radius, int points, int weight, bool isTrap, ArcheryTargetShape shape, IReadOnlyList<ArcheryRingBand> bands)`
- `ArcheryTarget(int waveIndex, int slotIndex, Vector3 origin, float riseSpeed, long spawnTick, float radius, int points, bool isTrap, ArcheryTargetShape shape, IReadOnlyList<ArcheryRingBand> bands, Vector3 facing)`
- `ArcheryHitRules.Resolve(in ArcheryTarget target, float normalizedOffset)`
- `ArcheryHitTest.SegmentHitsFace(Vector3 from, Vector3 to, Vector3 center, Vector3 facing, float radius, out float t)`
- `ArcheryHitTest.SegmentHitsTarget(Vector3 from, Vector3 to, Vector3 center, in ArcheryTarget target, out float t, out float normalizedOffset)`
- `ArcheryConfigProvider(LOP.MasterData.LOPMasterData md, IRoomDataStore roomDataStore)`
- Luban: `TbArcheryTarget.Shape` (int) · `TbArcheryRing.{Id, TargetId, OuterRatio, Points}` · `TbArcheryConfig` 키 = 맵 id
