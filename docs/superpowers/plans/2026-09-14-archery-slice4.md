# Archery 슬라이스 4 — 솟아오르는 과녁 · 연달아 솟는 묶음

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 과녁이 무대 아래에서 **포물선으로 솟았다 떨어지게** 하고, 한 웨이브가 동시에 뜨는 대신 **하나씩 연달아 솟는 묶음**이 되게 한다.

**Architecture:** 과녁은 여전히 통신하지 않는다 — `(matchSeed, waveIndex)`로 클·서가 각자 계산한다. 바뀌는 것은 **과녁 하나가 "자리"에서 "궤적"이 된다**는 것뿐이다: `Center` 하나 대신 `출발점 + 솟는 속도 + 솟기 시작한 틱`을 들고, 어느 시각의 위치든 순수 계산(`ArcheryTargetMotion.PositionAt`)으로 낸다. 화살과 완전히 같은 취급이다.

**Tech Stack:** Unity 6000.3.16f1 · C# · Luban(MasterData) · VContainer · NUnit(EditMode)

**Spec:** `docs/superpowers/specs/2026-09-11-archery-game-mode-design.md` (§3.1 솟아오르는 과녁, §3.2 연달아 솟는 묶음, §12.1 판정 결과)

앞 슬라이스: `docs/superpowers/plans/2026-09-13-archery-slice3.md`

---

## Global Constraints

- **결정론이 이 모드의 생명줄이다.** 과녁은 통신하지 않고 `(matchSeed, waveIndex)`로 양쪽이 계산한다. **`ArcheryWaveGenerator.Fill`의 난수 소비 순서가 곧 계약**이며, 이 계획은 그 순서를 바꾸므로 **클·서가 반드시 함께 배포**돼야 한다.
- **난수 소비 횟수가 데이터에 의존한다**(기존 성질). 클·서 마스터데이터가 다르면 뽑는 횟수가 달라져 이후 모든 값이 어긋난다 — 배포 전 양 패키지 `.bytes` 해시를 대조한다.
- 시뮬에 들어가는 코드는 **구체 클래스를 클·서가 공유**한다 — 인터페이스 seam 금지.
- **`using GameFramework.World;`를 쓰지 않는다.** World 타입은 항상 풀 네임스페이스로 한정한다(`Component`가 `UnityEngine.Component`와 겹친다).
- **Anemic Domain Model** — 데이터 타입은 데이터와 읽기 전용 파생 속성만.
- 주석은 최소로, 일상어로. 비자명한 *의도(왜)* 만. 전문용어를 설명 없이 던지지 않는다.
- 새 `.cs`마다 Unity가 만든 `.meta`를 함께 커밋한다. `.meta`를 직접 만들지 않는다.
- **`git add -A` / `git commit -a` 금지.** 바꾼 파일만 경로로 지정하고 `git status --short`로 확인한다. 양쪽 Unity 레포에 커밋하면 안 되는 로컬 픽스처가 늘 떠 있다(`Assets/Art`, `Jua-Regular SDF.asset`, `ProjectSettings/*`, `URPDefaultResources/*`, `ConfigureRoomComponent.cs`, `AddressableAssetSettings.asset`, `DefaultVolumeProfile.asset`, `output/`, `.superpowers/`).
- 커밋 트레일러(모든 커밋):
  ```
  Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01J3Xe7rZoLKi3dJFGKrLsGL
  ```
- **마스터데이터 컬럼은 반드시 맨 뒤에 추가한다.** Luban은 컬럼 *이름*이 아니라 *몇 번째 열*로 읽으므로, 중간에 끼우면 기존 값이 조용히 뒤바뀐다.

---

## 왜 이 슬라이스인가 (spec §12.1 요약)

슬라이스 3 실측 판정이 **"참는 것이 고민되지 않는다"** 였다. 원인은 둘:

- 미리 당기기의 대가 셋 중 **둘이 없었다**(손떨림은 뺐고, 줌인은 과녁이 뜨는 원이 화각에 다 들어와 무력).
- 함정이 무섭지 않았다 — 다만 이건 **2인 테스트**라 §1의 전제("과녁이 사람 수보다 훨씬 적다")가 성립하지 않은 탓일 수 있다.

살아 있던 축은 하나였다: *"작은 걸 노릴까 큰 걸 노릴까"*. 이 슬라이스는 **그 축을 죽이지 않으면서** 조준에 "언제"를 더한다.

---

## 착수 전에 박아둘 결정 다섯

### 결정 1 — 과녁은 "자리"가 아니라 "궤적"이 된다

`ArcheryTarget`이 `Center`(고정 위치) 대신 **`Origin`(솟기 시작하는 자리) + `RiseSpeed`(초기 상승 속도) + `SpawnTick`(솟기 시작한 틱)** 을 든다. 어느 시각의 위치든:

```
y(t) = Origin.y + RiseSpeed·t − ½·g·t²   (g는 화살과 같은 중력)
```

**화살과 완전히 같은 모양이다**(`ArcheryShot` + `ArcheryTrajectory.PositionAt`). 그 선례를 그대로 따르는 것이 이 설계의 핵심이다 — 새 개념을 만들지 않는다.

### 결정 2 — 중력은 **화살과 같은 값 하나**, 솟는 높이가 과녁마다 다르다

처음에는 과녁에 자기 중력을 주려 했다(수명·높이에서 역산). **틀렸다.** 화살과 과녁은 같은 화면에
동시에 있고, 플레이어는 화살로 과녁을 따라간다 — 중력이 다르면 **0.6초에 화살은 3.6m 떨어지는데
과녁은 1.5m** 떨어져 세상에 중력이 둘 있는 것처럼 보인다.

**`ArcheryTrajectory.Gravity = 20`을 과녁도 그대로 쓴다.** 중력은 세계의 성질이지 물체의 설정이
아니다.

그러면 높이와 수명이 **묶인다** — 중력이 고정이므로 하나를 정하면 다른 하나가 따라온다:

```
v₀ = √(2gH)      수명 T = 2v₀/g = √(8H/g)
```

**솟는 높이를 과녁마다 다르게 뽑는다.** 고정이면 몇 번 보고 나면 "언제쯤 정점"이 몸에 배어
리듬만으로 쏘게 된다. 높이가 다르면 **과녁마다 정점 시각이 달라져** 매번 봐야 한다.

| 높이 | 수명 | 초기속도 | 한 틱 이동 |
|---|---|---|---|
| 1.2m | 0.69초 | 6.93 m/s | 0.139m |
| 1.8m | 0.85초 | 8.49 m/s | 0.170m |
| 2.4m | 0.98초 | 9.80 m/s | 0.196m |

**범위를 1.2~2.4m로 잡는다.** 상한은 물리가 정한다 — 2.5m를 넘으면 한 틱 이동이 가장 작은 과녁
반지름(0.2m)을 넘어 결정 4의 근사가 깨진다. 하한은 너무 낮으면 움직임이 안 읽혀서다.

> **수명이 과녁마다 달라진다.** `ArcheryConfig.LifetimeSeconds` 같은 전역 값이 아니라
> **과녁 자신이 자기 수명을 안다**(`ArcheryTarget.LifetimeSeconds`). 판정·그리기가 "이 과녁이
> 아직 살아 있나"를 물을 때 그 과녁의 값을 쓴다.

### 결정 3 — 판정도 뷰도 **시각을 받는 형태**로 바뀐다

지금 소비처 셋이 전부 `Fill` → `.Center`를 쓴다:

| 어디 | 무엇 |
|---|---|
| 서버 `ArcheryHitSystem.CollectCandidates` | 적중 판정 (권위) |
| 클라 `ArcheryArrowStickSystem.CrossesLiveTarget` | 화살 꽂힘 (그림) |
| 클라 `ArcheryTargetView.LateTick` | 과녁 그리기 |

셋 다 `ArcheryTargetMotion.PositionAt(target, tick, tickInterval)`로 바뀐다.

⚠️ **`ArcheryTargetView`가 지금 정수 틱을 쓴다.** 그 자리 주석이 근거를 이렇게 적어 두었다:

> *"과녁이 떠 있나 없나는 틱 단위 사실이라 소수 틱으로 물을 것이 없다 — (화살의 자세는 소수 틱이 필요하지만 과녁은 가만히 있다.)"*

**그 전제가 이 슬라이스에서 깨진다.** 그대로 두면 과녁이 20ms 계단으로 튄다. 소수 틱으로 바꾸고 주석도 고친다.

### 결정 4 — 판정은 "움직이는 구"를 상대한다

지금 `ArcheryHitTest.SegmentHitsSphere(from, to, center, radius, t)`는 **정지한 구**를 전제한다. 과녁이 움직이면 화살과 과녁이 **둘 다** 움직인다.

정식 해법은 상대속도 이차방정식이지만, **이 게임은 그게 필요 없다.** 틱 간격이 20ms이고 과녁 최대 속도가 11.4m/s라 한 틱에 0.23m — 과녁 반경(0.2~0.45m)보다 작다. 그래서 **그 틱의 과녁 위치를 한 번 계산해 정지한 것으로 보고 재면** 충분하다.

**다만 어느 시각의 과녁인가가 중요하다.** 화살 선분은 `[t-1, t]` 구간인데 과녁을 `t`에서만 재면 반 틱만큼 어긋난다. **구간 가운데(`t - 0.5`)** 의 과녁 위치를 쓴다 — 선분의 평균 위치와 짝이 맞는다.

> 이 근사가 깨지는 조건을 테스트로 못박는다: 과녁이 한 틱에 자기 반지름보다 많이 움직이면 뚫고 지나갈 수 있다. 지금 값으로는 안 닿지만, 나중에 속도를 올리면 그 테스트가 걸린다.

### 결정 5 — 묶음 안에서 솟는 시각은 **슬롯마다 다르다**

지금은 한 웨이브의 과녁이 전부 같은 순간에 뜬다. 바뀐 뒤에는 슬롯마다 `SpawnTick`이 다르다:

```
SpawnTick(slot) = 웨이브 시작 틱 + slot · StaggerTicks
```

**난수를 쓰지 않는다** — 간격이 일정해야 리듬이 생기고, 리듬이 있어야 손이 맞춰졌다가 함정에 걸린다(spec §3.2). 난수로 흩뜨리면 그냥 산만해진다.

**웨이브 주기는 마지막 과녁이 떨어질 때까지를 덮어야 한다:**

```
WavePeriodTicks ≥ (MaxTargets-1)·StaggerTicks + LifetimeTicks + RestTicks
```

이 관계를 **배포 데이터 검사로 못박는다**(Task 6) — 어긋나면 마지막 과녁이 공중에서 잘려 사라지는데 에러는 안 난다.

---

## 파일 구조

### LOP-Shared (`C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared`)

| 파일 | 책임 |
|---|---|
| `Runtime/Scripts/Game/ArcheryTarget.cs` (수정) | `Center` → `Origin`+`RiseSpeed`+`SpawnTick` |
| `Runtime/Scripts/Game/ArcheryTargetMotion.cs` (신규) | 과녁 위치·수명 계산 (순수 커널) |
| `Runtime/Scripts/Game/ArcheryConfig.cs` (수정) | 솟는 높이·수명·간격·쉼 |
| `Runtime/Scripts/Game/ArcheryWaveGenerator.cs` (수정) | 궤적을 만든다 + 슬롯마다 다른 솟는 시각 |
| `Tests/EditMode/ArcheryTargetMotionTests.cs` (신규) | 궤적 커널 |
| `Tests/EditMode/ArcheryWaveGeneratorTests.cs` (수정) | 기존 테스트를 새 계약에 |

### MasterData (`LeagueOfPhysical-MasterData-Client` / `-Server`)

생성물(`Runtime.Generated/`)만 바뀐다. 손으로 고치지 않는다.

| 파일 | 책임 |
|---|---|
| `LeagueOfPhysical-MasterData-Server/Tests/EditMode/ArcheryTargetSeparationTests.cs` (수정) | 웨이브 주기가 묶음을 덮는지 검사 추가 |

### infrastructure

| 파일 | 책임 |
|---|---|
| `table/Datas/#ArcheryConfig.xlsx` (수정) | 새 컬럼 넷 + 묶음 크기 조정 |

### Server / Client

| 파일 | 책임 |
|---|---|
| 서버 `Assets/Scripts/Game/ArcheryConfigProvider.cs` (수정) | 새 컬럼을 `ArcheryConfig`로 |
| 서버 `Assets/Scripts/Game/TickSystems/ArcheryHitSystem.cs` (수정) | 움직이는 과녁 판정 |
| 서버 `Assets/Tests/Editor/ArcheryHitSystemTests.cs` (수정) | 픽스처를 새 계약에 |
| 클라 `Assets/Scripts/Game/ArcheryConfigProvider.cs` (수정) | **서버 쌍둥이와 한 글자도 다르면 안 된다** |
| 클라 `Assets/Scripts/Game/ArcheryTargetView.cs` (수정) | 소수 틱으로 움직이는 과녁 그리기 |
| 클라 `Assets/Scripts/Game/TickSystems/ArcheryArrowStickSystem.cs` (수정) | 움직이는 과녁에 꽂히기 |

---

## Task 1: 과녁이 "자리"에서 "궤적"이 된다 (LOP-Shared)

**Files:**
- Modify: `Runtime/Scripts/Game/ArcheryTarget.cs`
- Create: `Runtime/Scripts/Game/ArcheryTargetMotion.cs`
- Test: `Tests/EditMode/ArcheryTargetMotionTests.cs` (신규)

**Interfaces:**
- Consumes: (없음 — 첫 태스크)
- Produces:
  - `ArcheryTarget(int waveIndex, int slotIndex, Vector3 origin, float riseSpeed, long spawnTick, float radius, int points, bool isTrap)`
  - 필드: `WaveIndex` `SlotIndex` `Origin`(Vector3) `RiseSpeed`(float) `SpawnTick`(long) `Radius` `Points` `IsTrap`
  - 파생: `float LifetimeSeconds => 2f * RiseSpeed / ArcheryTargetMotion.Gravity`
  - `static class ArcheryTargetMotion`:
    - `const float Gravity` — **`ArcheryTrajectory.Gravity`와 같은 값을 가리킨다**
    - `static Vector3 PositionAt(in ArcheryTarget target, double tick, float tickInterval)`
    - `static float RiseSpeedFor(float riseHeight)` — 높이만 받는다(중력이 고정이므로)
    - `static bool IsAlive(in ArcheryTarget target, double tick, float tickInterval)` — 과녁이 자기 수명을 안다

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Tests/EditMode/ArcheryTargetMotionTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryTargetMotionTests
    {
        const float TickInterval = 0.02f;

        static ArcheryTarget Target(long spawnTick, float riseSpeed)
        {
            return new ArcheryTarget(
                waveIndex: 0, slotIndex: 0,
                origin: new Vector3(1f, 0f, 2f), riseSpeed: riseSpeed, spawnTick: spawnTick,
                radius: 0.3f, points: 2, isTrap: false);
        }

        [Test]
        public void 솟기_전에는_출발점에_있다()
        {
            var target = Target(spawnTick: 100, riseSpeed: 10f);

            var before = ArcheryTargetMotion.PositionAt(target, 90, TickInterval);

            Assert.AreEqual(target.Origin, before);
        }

        [Test]
        public void 솟는_순간에도_출발점이다()
        {
            var target = Target(spawnTick: 100, riseSpeed: 10f);

            Assert.AreEqual(target.Origin, ArcheryTargetMotion.PositionAt(target, 100, TickInterval));
        }

        //  좌우로는 안 움직인다 — 솟았다 떨어지는 것뿐이라 x·z는 출발점 그대로다.
        [Test]
        public void 좌우로는_움직이지_않는다()
        {
            var target = Target(spawnTick: 100, riseSpeed: 10f);

            var mid = ArcheryTargetMotion.PositionAt(target, 130, TickInterval);

            Assert.AreEqual(target.Origin.x, mid.x, 1e-4f);
            Assert.AreEqual(target.Origin.z, mid.z, 1e-4f);
            Assert.Greater(mid.y, target.Origin.y, "솟는 중이면 출발점보다 높아야 한다");
        }

        //  정점 높이가 설계값(솟는 높이)과 맞아야 한다 — 이게 틀리면 과녁이 뜨는 공간을 벗어난다.
        [Test]
        public void 정점에서_정해진_높이만큼_솟는다()
        {
            float riseHeight = 2f;
            float riseSpeed = ArcheryTargetMotion.RiseSpeedFor(riseHeight);
            var target = Target(spawnTick: 100, riseSpeed: riseSpeed);

            //  정점은 수명의 절반 시점이다(올라간 만큼 내려온다).
            double apexTick = 100 + (target.LifetimeSeconds / 2f) / TickInterval;
            var apex = ArcheryTargetMotion.PositionAt(target, apexTick, TickInterval);

            Assert.AreEqual(target.Origin.y + riseHeight, apex.y, 0.01f);
        }

        //  수명이 끝나는 순간 출발 높이로 돌아온다 — 올라간 만큼 내려온다는 뜻이다.
        [Test]
        public void 수명이_끝나면_출발_높이로_돌아온다()
        {
            float riseSpeed = ArcheryTargetMotion.RiseSpeedFor(2f);
            var target = Target(spawnTick: 100, riseSpeed: riseSpeed);

            double endTick = 100 + target.LifetimeSeconds / TickInterval;
            var end = ArcheryTargetMotion.PositionAt(target, endTick, TickInterval);

            Assert.AreEqual(target.Origin.y, end.y, 0.01f);
        }

        //  화면은 틱 사이도 물어본다 — 소수 틱이 정수 틱 둘 사이에 있어야 한다.
        [Test]
        public void 소수_틱도_받는다()
        {
            var target = Target(spawnTick: 100, riseSpeed: 10f);

            float at110 = ArcheryTargetMotion.PositionAt(target, 110, TickInterval).y;
            float at110half = ArcheryTargetMotion.PositionAt(target, 110.5, TickInterval).y;
            float at111 = ArcheryTargetMotion.PositionAt(target, 111, TickInterval).y;

            Assert.Greater(at110half, at110);
            Assert.Less(at110half, at111);
        }

        [Test]
        public void 수명_안에서만_살아_있다()
        {
            var target = Target(spawnTick: 100, riseSpeed: ArcheryTargetMotion.RiseSpeedFor(2f));

            Assert.IsFalse(ArcheryTargetMotion.IsAlive(target, 99, TickInterval), "솟기 전");
            Assert.IsTrue(ArcheryTargetMotion.IsAlive(target, 100, TickInterval), "솟는 순간");
            Assert.IsTrue(ArcheryTargetMotion.IsAlive(target, 120, TickInterval), "공중");

            double endTick = 100 + target.LifetimeSeconds / TickInterval;
            Assert.IsFalse(ArcheryTargetMotion.IsAlive(target, endTick + 1, TickInterval), "떨어진 뒤");
        }

        //  중력이 고정이라 높이 하나가 속도도 수명도 정한다 — 손으로 적어 넣는 값이 아니다.
        [Test]
        public void 솟는_속도는_높이에서_나온다()
        {
            float riseSpeed = ArcheryTargetMotion.RiseSpeedFor(riseHeight: 2f);

            //  v0 = sqrt(2gH)
            Assert.AreEqual(Mathf.Sqrt(2f * ArcheryTargetMotion.Gravity * 2f), riseSpeed, 1e-3f);
        }

        //  화살과 과녁이 같은 화면에 있다 — 중력이 다르면 같은 시간에 다르게 떨어져 눈에 띈다.
        [Test]
        public void 과녁_중력은_화살_중력과_같다()
        {
            Assert.AreEqual(ArcheryTrajectory.Gravity, ArcheryTargetMotion.Gravity);
        }

        //  높이가 다르면 수명도 다르다 — 그래서 "언제쯤 정점"이 과녁마다 달라진다.
        [Test]
        public void 높이가_다르면_수명도_다르다()
        {
            var low = Target(spawnTick: 100, riseSpeed: ArcheryTargetMotion.RiseSpeedFor(1.2f));
            var high = Target(spawnTick: 100, riseSpeed: ArcheryTargetMotion.RiseSpeedFor(2.4f));

            Assert.Less(low.LifetimeSeconds, high.LifetimeSeconds);
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
```

기대: `failed:true`, `ArcheryTargetMotion`을 찾을 수 없음(CS0103) + `ArcheryTarget` 생성자 불일치.

> **LOP-Shared의 EditMode 테스트는 서버 에디터로 돌린다** — `file:` 패키지라 서버 프로젝트가 컴파일한다.

- [ ] **Step 3: 최소 구현**

`Runtime/Scripts/Game/ArcheryTarget.cs`의 struct 본문을 아래로 바꾼다(클래스 XML 주석은 그대로 두되 "떠 있는" → "솟아오르는"으로):

```csharp
    public readonly struct ArcheryTarget
    {
        public readonly int WaveIndex;
        public readonly int SlotIndex;

        /// <summary>솟기 시작하는 자리(무대 아래). 좌우로는 안 움직이므로 x·z는 내내 이 값이다.</summary>
        public readonly Vector3 Origin;

        /// <summary>솟기 시작하는 속도(m/s). 높이와 수명에서 역산한다 — <see cref="ArcheryTargetMotion.RiseSpeedFor"/>.</summary>
        public readonly float RiseSpeed;

        /// <summary>솟기 시작하는 절대 틱. 묶음 안에서 슬롯마다 다르다(연달아 솟는다).</summary>
        public readonly long SpawnTick;

        public readonly float Radius;
        public readonly int Points;

        /// <summary>맞히면 안 되는 과녁인가.</summary>
        public readonly bool IsTrap;

        /// <summary>
        /// 솟았다 떨어지기까지 걸리는 시간(초). 과녁마다 솟는 높이가 달라 <b>수명도 제각각</b>이라,
        /// 전역 설정이 아니라 과녁 자신이 안다. 중력이 고정이므로 초기속도 하나로 정해진다.
        /// </summary>
        public float LifetimeSeconds => 2f * RiseSpeed / ArcheryTargetMotion.Gravity;

        public ArcheryTarget(int waveIndex, int slotIndex, Vector3 origin, float riseSpeed, long spawnTick,
                             float radius, int points, bool isTrap)
        {
            WaveIndex = waveIndex;
            SlotIndex = slotIndex;
            Origin = origin;
            RiseSpeed = riseSpeed;
            SpawnTick = spawnTick;
            Radius = radius;
            Points = points;
            IsTrap = isTrap;
        }
    }
```

`Runtime/Scripts/Game/ArcheryTargetMotion.cs`를 만든다:

```csharp
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 과녁이 솟았다 떨어지는 길. 상태가 없는 순수 계산이라 클·서·뷰가 같은 시각을 넣으면 같은 답을
    /// 얻는다 — <b>그래서 과녁을 통신으로 보낼 필요가 없다</b>.
    ///
    /// <para>화살(<see cref="ArcheryTrajectory"/>)과 같은 모양이다: 출발점·초기속도·출발시각만 있으면
    /// 어느 시각의 위치든 나온다.</para>
    /// </summary>
    public static class ArcheryTargetMotion
    {
        /// <summary>
        /// 과녁에 걸리는 중력. <b>화살과 같은 값이다</b> — 중력은 세계의 성질이지 물체의 설정이
        /// 아니다. 다르게 두면 같은 화면에서 화살과 과녁이 서로 다른 속도로 떨어져,
        /// 화살로 과녁을 따라가는 이 게임에서는 바로 눈에 띈다.
        /// </summary>
        public const float Gravity = ArcheryTrajectory.Gravity;

        /// <summary>
        /// 그 높이까지 솟으려면 얼마로 출발해야 하나(m/s). 중력이 고정이라 높이 하나가 속도도
        /// 수명도 정한다 — 따로 적을 값이 없다.
        /// </summary>
        public static float RiseSpeedFor(float riseHeight)
        {
            if (riseHeight <= 0f)
            {
                return 0f;
            }
            //  정점 높이 H = v0²/(2g)를 v0에 대해 풀면 v0 = sqrt(2gH)다.
            return Mathf.Sqrt(2f * Gravity * riseHeight);
        }

        /// <summary>그 시각의 과녁 자리. 솟기 전에는 출발점에 가만히 있다.</summary>
        public static Vector3 PositionAt(in ArcheryTarget target, double tick, float tickInterval)
        {
            float t = (float)((tick - target.SpawnTick) * tickInterval);
            if (t <= 0f)
            {
                return target.Origin;
            }
            float y = target.RiseSpeed * t - 0.5f * Gravity * t * t;
            return new Vector3(target.Origin.x, target.Origin.y + y, target.Origin.z);
        }

        /// <summary>아직 공중에 있나. 솟기 전과 떨어진 뒤에는 거짓이다.</summary>
        public static bool IsAlive(in ArcheryTarget target, double tick, float tickInterval)
        {
            double elapsed = (tick - target.SpawnTick) * tickInterval;
            return elapsed >= 0d && elapsed <= target.LifetimeSeconds;
        }
    }
}
```

`ArcheryWaveGenerator.Fill`의 마지막 줄이 옛 생성자를 부르므로 **임시로** 컴파일만 되게 고친다(제대로 된 궤적은 Task 3):

```csharp
                into.Add(new ArcheryTarget(waveIndex, slot, center, 0f, 0L,
                                           kind.Radius, kind.Points, kind.IsTrap));
```

그리고 `FarEnough`가 `placed[i].Center`를 쓰므로 `placed[i].Origin`으로 바꾼다.

- [ ] **Step 4: 통과를 확인한다**

이 시점에 **소비처 셋이 `.Center`를 불러 컴파일이 깨진다**(서버 판정, 클라 뷰, 클라 꽂힘). 임시로 `.Origin`으로 바꿔 컴파일만 맞춘다 — 제대로 된 시각 기반 조회는 Task 4/5에서 한다. 세 파일 각각:

- 서버 `ArcheryHitSystem.cs`: `targets[i].Center` → `targets[i].Origin`
- 클라 `ArcheryArrowStickSystem.cs`: `targets[i].Center` → `targets[i].Origin`
- 클라 `ArcheryTargetView.cs`: `targets[i].Center` → `targets[i].Origin`

또한 서버 `ArcheryHitSystemTests.cs`와 Shared `ArcheryWaveGeneratorTests.cs`가 `.Center`를 쓰면 같이 고친다.

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server run_tests --mode EditMode
```

기대: 컴파일 초록, `ArcheryTargetMotionTests`의 **열** 개 테스트가 **이름으로** 결과에 보이고 전부 통과한다.

- [ ] **Step 5: 커밋 (레포 셋)**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git checkout -b feature/archery-slice4
git add Runtime/Scripts/Game/ArcheryTarget.cs Runtime/Scripts/Game/ArcheryTargetMotion.cs Runtime/Scripts/Game/ArcheryTargetMotion.cs.meta Runtime/Scripts/Game/ArcheryWaveGenerator.cs Tests/EditMode/ArcheryTargetMotionTests.cs Tests/EditMode/ArcheryTargetMotionTests.cs.meta Tests/EditMode/ArcheryWaveGeneratorTests.cs
git status --short
git commit -F - <<'EOF'
feat(archery): 과녁을 "자리"에서 "궤적"으로 바꾼다

고정 과녁에서는 조준이 "어디를 노리느냐" 하나였다. 솟았다 떨어지게 하면
"언제 쏘느냐"가 붙는다 — 화살이 12m를 0.2~0.5초에 가므로 지금 보이는
자리가 아니라 닿을 때 있을 자리를 노려야 한다.

화살과 같은 모양으로 둔다: 출발점·초기속도·출발시각만 있으면 어느 시각의
위치든 나온다. 그래서 과녁도 여전히 통신하지 않는다.

과녁 중력은 화살 중력(20)과 **같은 값**이다. 중력은 세계의 성질이지 물체의
설정이 아니다 — 다르게 두면 같은 화면에서 화살과 과녁이 서로 다른 속도로
떨어져, 화살로 과녁을 따라가는 이 게임에서는 바로 눈에 띈다.

그래서 솟는 높이 하나가 속도도 수명도 정한다(v0=sqrt(2gH), 수명=2v0/g).

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01J3Xe7rZoLKi3dJFGKrLsGL
EOF
```

서버·클라도 각각 `git checkout -b feature/archery-slice4` 후 임시 수정을 커밋:

```bash
git add <바꾼 파일들>
git status --short
git commit -F - <<'EOF'
chore(archery): 과녁 궤적 전환에 호출부를 맞춘다

Center가 Origin으로 바뀌었다. 시각을 받아 위치를 내는 제대로 된 조회는
다음 슬라이스 단계에서 한다 — 여기서는 컴파일만 맞춘다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01J3Xe7rZoLKi3dJFGKrLsGL
EOF
```

---

## Task 2: 설정에 솟는 높이 범위·간격·쉼을 낸다 (LOP-Shared)

**Files:**
- Modify: `Runtime/Scripts/Game/ArcheryConfig.cs`
- Test: `Tests/EditMode/ArcheryWaveGeneratorTests.cs`

**Interfaces:**
- Consumes: (없음)
- Produces:
  - `ArcheryConfig(int wavePeriodTicks, int minTargets, int maxTargets, float spawnRadius, float spawnMinY, float spawnMaxY, float minSeparation, float trapRatioMin, float trapRatioMax, float shakeFreeSeconds, float shakeRampSeconds, float shakeMaxDegrees, float riseHeightMin, float riseHeightMax, int staggerTicks, int restTicks, IReadOnlyList<ArcheryTargetKind> kinds)`
  - `float RiseHeightMin` · `float RiseHeightMax` · `int StaggerTicks` · `int RestTicks`
  - `int BurstTicks { get; }` — 파생. **가장 높이 솟는** 과녁이 떨어질 때까지 걸리는 틱 수

> 인자가 열일곱으로 길어진다. 부르는 곳이 넷뿐이고(클·서 provider, 테스트 둘) 전부 이름 붙인 인자를 쓰므로 읽기에 문제가 없다. 빌더를 만들지 않는다(YAGNI).
>
> **`shake*` 셋은 남긴다.** 손떨림은 뺐지만(spec §2.2) 데이터 컬럼이 아직 있고, 이 슬라이스에서 컬럼을 지우면 Luban 열 위치가 밀려 위험하다. 별도 정리 슬라이스에서 지운다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Tests/EditMode/ArcheryWaveGeneratorTests.cs`의 `ConfigWith`를 아래로 바꾼다:

```csharp
        //  실측 기본값과 같은 모양으로 둔다 — 테스트가 배포 데이터와 다른 조건을 시험하면
        //  통과해도 아무것도 보장하지 못한다.
        private const float TestRiseHeightMin = 1.2f;
        private const float TestRiseHeightMax = 2.4f;
        private const int TestStaggerTicks = 12;
        private const int TestRestTicks = 20;

        private static ArcheryConfig ConfigWith(ArcheryTargetKind[] kinds, float minSeparation,
                                                float trapRatioMin = 0f, float trapRatioMax = 0f)
        {
            return new ArcheryConfig(
                wavePeriodTicks: 120, minTargets: 3, maxTargets: 5,
                spawnRadius: 3.5f, spawnMinY: 1.5f, spawnMaxY: 8f, minSeparation: minSeparation,
                trapRatioMin: trapRatioMin, trapRatioMax: trapRatioMax,
                shakeFreeSeconds: 1.2f, shakeRampSeconds: 2.5f, shakeMaxDegrees: 0f,
                riseHeightMin: TestRiseHeightMin, riseHeightMax: TestRiseHeightMax,
                staggerTicks: TestStaggerTicks, restTicks: TestRestTicks,
                kinds: kinds);
        }
```

> 기존 `ConfigWith(kinds, minSeparation, trapRatioMin, trapRatioMax)` 4인자 호출이 그대로 살도록 기본값을 둔다 — 기존 테스트 본문을 안 고쳐도 된다.

그리고 새 테스트 둘을 파일 끝의 마지막 `[Test]` 뒤에 넣는다:

```csharp
        //  마지막 과녁이 떨어질 때까지 걸리는 시간이다 — 웨이브 주기가 이보다 짧으면
        //  마지막 과녁이 공중에서 잘려 사라진다(에러는 안 난다).
        //  높이가 과녁마다 다르므로 **가장 높이 솟는 경우**로 잡아야 안전하다.
        [Test]
        public void 묶음_길이는_가장_높이_솟는_과녁이_떨어질_때까지다()
        {
            var config = Config();

            float longest = 2f * ArcheryTargetMotion.RiseSpeedFor(TestRiseHeightMax)
                          / ArcheryTargetMotion.Gravity;
            int lifetimeTicks = Mathf.CeilToInt(longest / 0.02f);
            int expected = (config.MaxTargets - 1) * TestStaggerTicks + lifetimeTicks;

            Assert.AreEqual(expected, config.BurstTicks);
        }
```

- [ ] **Step 2: 실패를 확인한다**

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
```

기대: `failed:true`, `ArcheryConfig` 생성자 인자 개수(CS7036/CS1739) 또는 `RiseHeightMin`/`BurstTicks` 없음(CS1061).

- [ ] **Step 3: 최소 구현**

`Runtime/Scripts/Game/ArcheryConfig.cs`에서 `MaxTargetRadius` 속성 아래에 더한다:

```csharp
        /// <summary>과녁이 솟아오르는 높이의 하한(m).</summary>
        public float RiseHeightMin { get; }

        /// <summary>
        /// 과녁이 솟아오르는 높이의 상한(m). <b>과녁마다 이 사이에서 뽑는다</b> — 고정이면 몇 번
        /// 보고 나서 "언제쯤 정점"이 몸에 배어 리듬만으로 쏘게 된다.
        ///
        /// <para>⚠️ 너무 높이 잡으면 과녁이 한 틱에 자기 반지름보다 많이 움직여 판정이 뚫린다.
        /// 중력 20·가장 작은 과녁 반지름 0.2m에서 상한은 약 2.5m다 — 배포 데이터 검사가 지킨다.</para>
        /// </summary>
        public float RiseHeightMax { get; }

        /// <summary>묶음 안에서 다음 과녁이 솟기까지의 간격(틱). 일정해야 리듬이 생긴다.</summary>
        public int StaggerTicks { get; }

        /// <summary>묶음이 끝나고 다음 묶음까지의 쉼(틱). 끊겼다 시작해야 매 묶음이 새로 긴장된다.</summary>
        public int RestTicks { get; }

        /// <summary>
        /// 묶음이 다 끝나기까지 걸리는 틱 수 — <b>가장 높이 솟는 과녁</b> 기준이다.
        /// <see cref="WavePeriodTicks"/>가 이보다 짧으면 마지막 과녁이 공중에서 잘려 사라진다 —
        /// 에러는 안 나므로 배포 데이터 검사가 지킨다.
        /// </summary>
        public int BurstTicks { get; }
```

생성자 시그니처를 바꾸고 본문 끝(`TrapKinds = traps;` 뒤)에 더한다:

```csharp
        public ArcheryConfig(int wavePeriodTicks, int minTargets, int maxTargets,
                             float spawnRadius, float spawnMinY, float spawnMaxY, float minSeparation,
                             float trapRatioMin, float trapRatioMax,
                             float shakeFreeSeconds, float shakeRampSeconds, float shakeMaxDegrees,
                             float riseHeightMin, float riseHeightMax, int staggerTicks, int restTicks,
                             IReadOnlyList<ArcheryTargetKind> kinds)
```

(기존 대입은 그대로 두고 아래를 이어 붙인다)

```csharp
            RiseHeightMin = riseHeightMin;
            RiseHeightMax = riseHeightMax;
            StaggerTicks = staggerTicks;
            RestTicks = restTicks;

            //  가장 높이 솟는 과녁이 제일 오래 떠 있다 — 묶음 길이는 그 기준으로 잡아야 안전하다.
            float longestLifetime = 2f * ArcheryTargetMotion.RiseSpeedFor(riseHeightMax)
                                  / ArcheryTargetMotion.Gravity;
            //  틱은 정수라 올림한다 — 내림하면 마지막 한 틱이 모자라 과녁이 땅에 닿기 전에 잘린다.
            int lifetimeTicks = Mathf.CeilToInt(longestLifetime / 0.02f);
            BurstTicks = (maxTargets - 1) * staggerTicks + lifetimeTicks;
```

`using UnityEngine;`이 파일에 없으면 더한다(`Mathf` 때문).

- [ ] **Step 4: 호출부 넷을 맞춰 컴파일을 초록으로 둔다**

서버·클라 `ArcheryConfigProvider.cs`의 `new ArcheryConfig(...)`에 새 인자 넷을 **임시값**으로 더한다(실제 컬럼은 Task 6 뒤 Task 7/8에서):

```csharp
            return new ArcheryConfig(
                r.WavePeriodTicks, r.MinTargets, r.MaxTargets,
                r.SpawnRadius, r.SpawnMinY, r.SpawnMaxY, r.MinSeparation,
                r.TrapRatioMin, r.TrapRatioMax,
                r.ShakeFreeSeconds, r.ShakeRampSeconds, r.ShakeMaxDegrees,
                //  아직 데이터에 칸이 없다 — 마스터데이터를 구운 뒤 실제 컬럼으로 바꾼다.
                riseHeightMin: 1.2f, riseHeightMax: 2.4f, staggerTicks: 12, restTicks: 20,
                kinds);
```

**두 파일을 나란히 놓고 한 글자도 다르지 않은지 확인한다** — 다르면 클·서가 다른 과녁을 본다.

서버 `ArcheryHitSystemTests.cs`의 `Build(kinds, minSeparation)`에도 같은 넷을 더한다.

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server run_tests --mode EditMode
```

기대: 컴파일 초록, Failed 0, 새 테스트 둘이 이름으로 보인다.

- [ ] **Step 5: 커밋 (레포 셋)**

각 레포에서 바꾼 파일만 스테이지하고 커밋한다. LOP-Shared:

```
feat(archery): 설정에 솟는 높이 범위·간격·쉼을 낸다

높이는 하나가 아니라 범위다 — 과녁마다 뽑는다. 고정이면 몇 번 보고 나서
"언제쯤 정점"이 몸에 배어 리듬만으로 쏘게 된다.

속도와 수명은 설정에 없다. 중력이 고정이므로 높이 하나가 둘 다 정한다.
묶음 길이(BurstTicks)는 가장 높이 솟는 과녁 기준이다 — 웨이브 주기가
이보다 짧으면 마지막 과녁이 공중에서 잘리는데 에러가 안 난다.
```

서버·클라: `chore(archery): 설정 생성자 변경에 호출부를 맞춘다`

---

## Task 3: 웨이브 생성기가 궤적과 솟는 시각을 만든다 (LOP-Shared)

**Files:**
- Modify: `Runtime/Scripts/Game/ArcheryWaveGenerator.cs`
- Test: `Tests/EditMode/ArcheryWaveGeneratorTests.cs`

**Interfaces:**
- Consumes: `ArcheryTarget` 새 생성자 (Task 1), `ArcheryConfig.RiseHeightMin/Max/StaggerTicks` (Task 2), `ArcheryTargetMotion.RiseSpeedFor` (Task 1)
- Produces:
  - `Fill(List<ArcheryTarget> into, ulong matchSeed, int waveIndex, ArcheryConfig config, long gameplayStartTick)` — **인자가 하나 늘어난다**(절대 틱을 알아야 `SpawnTick`을 낼 수 있다)
  - **난수 소비 순서가 바뀐다**: 개수 → (함정 종류 있으면) 함정비율 → 슬롯마다 (함정인가 → 종류 → 각도 → 반지름 → 높이 → **솟는 높이**)

> ⚠️ **솟는 높이를 슬롯마다 뽑으므로 난수를 하나씩 더 쓴다.** 이 순서가 곧 클·서 계약이라
> 양쪽이 반드시 함께 배포돼야 한다. 뽑는 자리는 **슬롯 루프의 맨 끝**이다 — 앞에 끼우면 기존
> 값들이 전부 밀린다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Tests/EditMode/ArcheryWaveGeneratorTests.cs`에 넣는다. 기존 `Fill` 호출이 전부 인자 하나 부족해지므로, **헬퍼를 하나 만들어 그것만 고친다**:

```csharp
        //  모든 테스트가 같은 출발 틱을 쓴다 — 절대 틱이 필요한 것은 SpawnTick 계산뿐이라
        //  값 자체에는 의미가 없다.
        private const long TestStartTick = 1000;

        private static void Fill(List<ArcheryTarget> into, ulong seed, int wave, ArcheryConfig config)
        {
            ArcheryWaveGenerator.Fill(into, seed, wave, config, TestStartTick);
        }
```

그리고 기존 테스트 본문의 `ArcheryWaveGenerator.Fill(a, 0xC0FFEEUL, wave, config)` 같은 호출을 전부 `Fill(a, 0xC0FFEEUL, wave, config)`로 바꾼다(클래스 안의 헬퍼가 잡는다).

새 테스트 셋을 파일 끝에 더한다:

```csharp
        //  spec 3.2: 동시에 뜨지 않고 하나씩 연달아 솟는다. 이 간격이 리듬을 만들고,
        //  리듬이 있어야 손이 맞춰졌다가 함정에 걸린다.
        [Test]
        public void 묶음_안에서_슬롯마다_솟는_시각이_다르다()
        {
            var config = Config();
            var targets = new List<ArcheryTarget>();
            Fill(targets, 5UL, 0, config);

            Assert.Greater(targets.Count, 1, "간격을 재려면 둘 이상이어야 한다");
            for (int i = 1; i < targets.Count; i++)
            {
                Assert.AreEqual(config.StaggerTicks, targets[i].SpawnTick - targets[i - 1].SpawnTick,
                                $"슬롯 {i - 1}과 {i} 사이 간격");
            }
        }

        //  난수로 흩뜨리지 않는다 — 간격이 들쭉날쭉하면 리듬이 아니라 그냥 산만한 것이 된다.
        [Test]
        public void 솟는_간격은_웨이브가_달라도_일정하다()
        {
            var config = Config();
            var targets = new List<ArcheryTarget>();

            for (int wave = 0; wave < 50; wave++)
            {
                Fill(targets, 77UL, wave, config);
                for (int i = 1; i < targets.Count; i++)
                {
                    Assert.AreEqual(config.StaggerTicks, targets[i].SpawnTick - targets[i - 1].SpawnTick,
                                    $"wave {wave} slot {i}");
                }
            }
        }

        [Test]
        public void 첫_과녁은_웨이브_시작에_솟는다()
        {
            var config = Config();
            var targets = new List<ArcheryTarget>();

            for (int wave = 0; wave < 10; wave++)
            {
                Fill(targets, 3UL, wave, config);
                long waveStart = ArcheryWaveGenerator.WaveStartTick(wave, TestStartTick, config);
                Assert.AreEqual(waveStart, targets[0].SpawnTick, $"wave {wave}");
            }
        }

        //  높이가 설정 범위 안에 들어가야 한다 — 벗어나면 과녁이 공간 밖으로 나가거나
        //  한 틱에 자기 반지름보다 많이 움직여 판정이 뚫린다.
        [Test]
        public void 솟는_높이가_설정_범위_안이다()
        {
            var config = Config();
            var targets = new List<ArcheryTarget>();

            float minSpeed = ArcheryTargetMotion.RiseSpeedFor(config.RiseHeightMin);
            float maxSpeed = ArcheryTargetMotion.RiseSpeedFor(config.RiseHeightMax);

            for (int wave = 0; wave < 100; wave++)
            {
                Fill(targets, 9UL, wave, config);
                for (int i = 0; i < targets.Count; i++)
                {
                    Assert.That(targets[i].RiseSpeed, Is.InRange(minSpeed - 1e-3f, maxSpeed + 1e-3f),
                                $"wave {wave} slot {i}");
                }
            }
        }

        //  고정이면 "언제쯤 정점"이 몸에 배어 리듬만으로 쏘게 된다 — 실제로 갈리는지 본다.
        [Test]
        public void 과녁마다_솟는_높이가_다르다()
        {
            var config = Config();
            var targets = new List<ArcheryTarget>();

            var seen = new HashSet<float>();
            for (int wave = 0; wave < 50; wave++)
            {
                Fill(targets, 21UL, wave, config);
                for (int i = 0; i < targets.Count; i++)
                {
                    seen.Add(targets[i].RiseSpeed);
                }
            }

            Assert.Greater(seen.Count, 10, "높이가 사실상 고정이면 정점 시각도 매번 같아진다");
        }
```

- [ ] **Step 2: 실패를 확인한다**

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
```

기대: `failed:true` — `Fill`이 인자 다섯을 안 받는다(CS1501).

- [ ] **Step 3: 최소 구현**

`ArcheryWaveGenerator.Fill`의 시그니처와 마지막 `into.Add`를 고친다:

```csharp
        public static void Fill(List<ArcheryTarget> into, ulong matchSeed, int waveIndex,
                                ArcheryConfig config, long gameplayStartTick)
```

XML 주석의 난수 순서 설명은 그대로 두고, 그 아래에 한 줄 더한다:

```csharp
        /// <para>솟는 시각은 난수가 아니다 — 슬롯 번호에 간격을 곱한 값이라 리듬이 일정하다.</para>
```

`into.Add(...)` 줄을 아래로:

```csharp
                //  묶음 안에서 하나씩 연달아 솟는다 — 간격이 일정해야 리듬이 생기고, 리듬이
                //  있어야 손이 맞춰졌다가 그 속의 함정에 걸린다(spec 3.2).
                long spawnTick = ArcheryWaveGenerator.WaveStartTick(waveIndex, gameplayStartTick, config)
                               + (long)slot * config.StaggerTicks;

                //  솟는 높이는 과녁마다 다르다 — 고정이면 "언제쯤 정점"이 몸에 배어 리듬만으로
                //  쏘게 된다. 높이가 다르면 정점 시각도 달라져 매번 봐야 한다.
                //  (난수를 여기서 한 번 더 쓴다 — 순서가 계약이므로 반드시 슬롯 루프 맨 끝이다.)
                float riseHeight = rng.Range(config.RiseHeightMin, config.RiseHeightMax);
                float riseSpeed = ArcheryTargetMotion.RiseSpeedFor(riseHeight);

                into.Add(new ArcheryTarget(waveIndex, slot, center, riseSpeed, spawnTick,
                                           kind.Radius, kind.Points, kind.IsTrap));
```

- [ ] **Step 4: 소비처 셋의 호출을 맞춘다**

`Fill`을 부르는 곳이 셋이다. 각각 `world.GameplayStartTick`을 넘긴다:

- 서버 `ArcheryHitSystem.cs`: `ArcheryWaveGenerator.Fill(targets, matchSeed.Value, wave, config, world.GameplayStartTick);`
- 클라 `ArcheryArrowStickSystem.cs`: 같은 모양
- 클라 `ArcheryTargetView.cs`: 같은 모양

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server run_tests --mode EditMode
```

기대: 컴파일 초록, Failed 0. **기존 결정론 테스트(`같은_씨앗과_웨이브는_언제_몇_번_물어도_같은_과녁을_준다`)가 여전히 통과해야 한다** — 이름으로 확인한다. 단 그 테스트가 `.Center`를 비교하고 있으면 `.Origin`+`.SpawnTick`+`.RiseSpeed` 비교로 바꾼다.

- [ ] **Step 5: 커밋 (레포 셋)**

```
feat(archery): 묶음 안에서 과녁이 하나씩 연달아 솟는다

동시에 뜨면 전부 보고 나서 고를 수 있다. 연달아 솟으면 리듬에 손이 맞고,
그 리듬 속에 함정이 섞여 있으면 보고 판단하기 전에 이미 놓은 뒤다 —
참기가 머리로 고르는 판단이 아니라 손이 멈춰야 하는 순간이 된다(spec 3.2).

솟는 **시각**에는 난수를 쓰지 않는다. 간격이 들쭉날쭉하면 리듬이 아니라 그냥
산만한 것이 되기 때문이다. 대신 솟는 **높이**를 과녁마다 뽑는다 — 고정이면
"언제쯤 정점"이 몸에 배어 리듬만으로 쏘게 된다.

⚠️ 난수를 슬롯마다 하나씩 더 쓰므로 소비 순서가 바뀐다 = 클·서 계약 변경이다.
반드시 함께 배포돼야 한다.
```

---

## Task 4: 서버 판정이 움직이는 과녁을 상대한다 (Server)

**Files:**
- Modify: `Assets/Scripts/Game/TickSystems/ArcheryHitSystem.cs`
- Test: `Assets/Tests/Editor/ArcheryHitSystemTests.cs`

**Interfaces:**
- Consumes: `ArcheryTargetMotion.PositionAt(in ArcheryTarget, double tick, float tickInterval)` · `IsAlive(in ArcheryTarget, double, float)` (Task 1)
- Produces: (없음 — 서버 내부)

**결정 4를 여기서 구현한다.** 화살 선분은 `[tick-1, tick]` 구간이고, 과녁은 **구간 가운데(`tick - 0.5`)** 의 위치로 잰다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Tests/Editor/ArcheryHitSystemTests.cs`에 더한다. 기존 헬퍼(`Build`, `f.Archer`, `f.TargetsOfWave`, `f.ScoreOf`, `ShotThrough`)를 그대로 쓴다.

먼저 **움직이는 과녁을 맞히는 화살**을 만드는 헬퍼가 필요하다. 기존 `ShotThrough`는 정지 과녁의 중심을 지나가게 만드는 것이라 그대로는 못 쓴다 — 아래를 `ShotThrough` 옆에 더한다:

```csharp
        /// <summary>
        /// 그 틱에 과녁이 있을 자리를 정확히 지나가는 화살. 과녁이 움직이므로 "어디로 쏘나"가
        /// 아니라 "언제 어디에 있을 것인가"를 먼저 풀어야 한다.
        /// </summary>
        static ArcheryShot ShotThroughMoving(string shooterId, long fireTick, in ArcheryTarget target,
                                             long hitTick, float distance)
        {
            //  판정이 재는 것과 같은 시각(구간 가운데)의 자리를 노린다.
            Vector3 at = ArcheryTargetMotion.PositionAt(target, hitTick - 0.5, TickInterval);
            Vector3 origin = at + new Vector3(0f, 0f, -distance);
            float seconds = (hitTick - fireTick) * TickInterval;
            //  한 틱 만에 닿게 잡으면 위 선분이 정확히 그 자리에서 끝난다.
            return new ArcheryShot(shooterId, fireTick, origin, new Vector3(0f, 0f, distance / seconds));
        }
```

테스트 셋:

```csharp
        //  과녁이 움직이므로 "지금 있는 자리"로 쏘면 빗나간다 — 닿을 때 있을 자리를 노려야 한다.
        [Test]
        public void 움직이는_과녁도_맞힐_수_있다()
        {
            var f = Build(StartTick);
            f.Archer("a");
            var target = f.TargetsOfWave(0)[0];

            //  솟는 도중의 한 시점을 노린다.
            long hitTick = target.SpawnTick + 20;
            f.World.IngestRemoteShot(ShotThroughMoving("a", hitTick - 1, target, hitTick, 1.0f));
            f.System.Tick(hitTick, TickInterval);

            Assert.AreEqual(target.Points, f.ScoreOf("a"));
        }

        //  솟기 전 과녁은 무대 아래에 있다 — 그 자리를 쏴도 맞으면 안 된다.
        [Test]
        public void 솟기_전_과녁은_못_맞힌다()
        {
            var f = Build(StartTick);
            f.Archer("a");
            var target = f.TargetsOfWave(0)[0];

            //  아직 안 솟은 시점. 출발점을 정확히 지나가게 쏜다.
            long earlyTick = target.SpawnTick - 5;
            if (earlyTick <= StartTick)
            {
                Assert.Ignore("첫 슬롯은 웨이브 시작과 동시에 솟아 '솟기 전'이 없다");
            }

            f.World.IngestRemoteShot(ShotThrough("a", earlyTick - 1, target, 1.0f));
            f.System.Tick(earlyTick, TickInterval);

            Assert.AreEqual(0, f.ScoreOf("a"));
        }

        //  떨어진 과녁도 마찬가지다. 수명이 지나면 무대 아래로 사라진 것이다.
        [Test]
        public void 떨어진_과녁은_못_맞힌다()
        {
            var f = Build(StartTick);
            f.Archer("a");
            var target = f.TargetsOfWave(0)[0];

            long lateTick = target.SpawnTick + Mathf.CeilToInt(target.LifetimeSeconds / TickInterval) + 5;
            f.World.IngestRemoteShot(ShotThroughMoving("a", lateTick - 1, target, lateTick, 1.0f));
            f.System.Tick(lateTick, TickInterval);

            Assert.AreEqual(0, f.ScoreOf("a"));
        }
```

> `ShotThrough`(정지용)는 과녁의 `Origin`을 쓰도록 Task 1에서 이미 고쳤다 — `솟기_전_과녁은_못_맞힌다`가 그걸 그대로 쓴다.

- [ ] **Step 2: 실패를 확인한다**

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server run_tests --mode EditMode
```

기대: `움직이는_과녁도_맞힐_수_있다`가 실패(판정이 `Origin`을 보고 있어 못 맞힌다). 나머지 둘은 통과할 수도 있다 — **실제로 관측한 것을 보고한다**(통과했으면 그 사실을 적고, 왜 통과했는지 한 줄 쓴다).

- [ ] **Step 3: 최소 구현**

`ArcheryHitSystem.CollectCandidates`의 과녁 순회를 고친다:

```csharp
                for (int i = 0; i < targets.Count; i++)
                {
                    if (waveState.IsConsumed(targets[i].SlotIndex))
                    {
                        continue;
                    }

                    //  아직 안 솟았거나 이미 떨어진 과녁은 없는 것이다.
                    if (ArcheryTargetMotion.IsAlive(targets[i], tick, tickInterval) == false)
                    {
                        continue;
                    }

                    //  화살 선분은 [tick-1, tick] 구간인데 과녁을 tick에서만 재면 반 틱 어긋난다 —
                    //  구간 가운데의 자리를 쓴다. 한 틱에 과녁이 자기 반지름보다 적게 움직이므로
                    //  그 사이 정지한 것으로 봐도 된다(테스트가 그 조건을 지킨다).
                    Vector3 targetAt = ArcheryTargetMotion.PositionAt(targets[i], tick - 0.5, tickInterval);

                    if (ArcheryHitTest.SegmentHitsSphere(from, to, targetAt, targets[i].Radius, out float t))
                    {
                        candidates.Add(new Candidate(t, shot.ShooterId, shot.FireTick, targets[i].SlotIndex));
                    }
                }
```

`CollectCandidates(long tick)`가 이미 `tick`을 받으므로 시그니처 변경은 없다.

- [ ] **Step 4: 통과를 확인한다**

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server run_tests --mode EditMode
```

기대: Failed 0. **기존 적중 테스트가 전부 그대로 통과해야 한다** — 이름으로 확인한다. 깨지는 것이 있으면 그 테스트가 정지 과녁을 전제하고 있다는 뜻이니, 움직이는 과녁에 맞게 고치되 **무엇을 재던 테스트였는지를 보존한다**(단순히 기대값을 바꿔 통과시키지 않는다).

- [ ] **Step 5: 근사가 깨지는 조건을 못박는다**

`Assets/Tests/Editor/ArcheryHitSystemTests.cs`에 더한다:

```csharp
        //  "한 틱 동안 과녁이 정지한 것으로 봐도 된다"는 근사에 기대고 있다. 그 전제는
        //  과녁이 한 틱에 자기 반지름보다 적게 움직인다는 것이다 — 높이를 올리면 깨진다.
        //  가장 높이 솟는 경우로 재야 한다(그게 제일 빠르다).
        [Test]
        public void 과녁은_한_틱에_자기_반지름보다_적게_움직인다()
        {
            var config = Config();
            float perTick = ArcheryTargetMotion.RiseSpeedFor(config.RiseHeightMax) * TickInterval;

            float smallest = float.MaxValue;
            for (int i = 0; i < config.Kinds.Count; i++)
            {
                smallest = Mathf.Min(smallest, config.Kinds[i].Radius);
            }

            Assert.Less(perTick, smallest,
                $"가장 높이 솟는 과녁이 한 틱에 {perTick:F3}m 움직이는데 가장 작은 과녁 반지름이 "
                + $"{smallest:F3}m다 — 판정이 과녁을 뚫고 지나갈 수 있다. rise_height_max를 낮추거나 "
                + "가장 작은 과녁을 키워야 한다");
        }
```

테스트를 다시 돌려 통과를 확인한다.

- [ ] **Step 6: 커밋**

```
feat(archery): 서버 판정이 움직이는 과녁을 상대한다

화살 선분은 [tick-1, tick] 구간인데 과녁을 tick에서만 재면 반 틱 어긋난다 —
구간 가운데의 자리를 쓴다.

움직이는 구 판정(상대속도 이차방정식)을 짓지 않는다. 가장 높이 솟는 과녁도
한 틱 20ms에 0.196m 움직이는데 가장 작은 과녁 반지름이 0.2m라, 그 사이
정지한 것으로 봐도 결과가 같다. 그 전제가 깨지는 조건을 테스트로 못박아
뒀다 — 높이를 올리면 거기서 걸린다.
```

---

## Task 5: 클라 화면이 움직이는 과녁을 그린다 (Client)

**Files:**
- Modify: `Assets/Scripts/Game/ArcheryTargetView.cs`
- Modify: `Assets/Scripts/Game/TickSystems/ArcheryArrowStickSystem.cs`

**Interfaces:**
- Consumes: `ArcheryTargetMotion.PositionAt/IsAlive` (Task 1)
- Produces: (없음 — 화면)

**결정 3을 여기서 구현한다.** 뷰가 정수 틱을 쓰는 근거가 깨졌다.

- [ ] **Step 1: 뷰를 소수 틱으로 바꾼다**

`Assets/Scripts/Game/ArcheryTargetView.cs`의 틱 계산과 주석을 고친다. 기존:

```csharp
            //  과녁이 떠 있나 없나는 틱 단위 사실이라 소수 틱으로 물을 것이 없다 — 정수 틱으로 묻는다.
            //  (화살의 *자세*는 소수 틱이 필요하지만 과녁은 가만히 있다.)
            long renderTick = (long)System.Math.Floor((runner.tickUpdater.elapsedTime - interval) / interval);
            int wave = ArcheryWaveGenerator.WaveIndexAt(renderTick, world.GameplayStartTick, config);
```

바뀐 뒤:

```csharp
            //  과녁이 이제 솟았다 떨어지므로 소수 틱이 필요하다 — 정수 틱으로 물으면 20ms 계단으로
            //  튄다. (예전에는 "과녁은 가만히 있다"가 근거였는데 그 전제가 깨졌다.)
            double renderTick = (runner.tickUpdater.elapsedTime - interval) / interval;

            //  어느 웨이브인지는 틱 단위 사실이라 여기는 정수로 묻는다.
            int wave = ArcheryWaveGenerator.WaveIndexAt((long)System.Math.Floor(renderTick),
                                                        world.GameplayStartTick, config);
```

과녁 위치를 정하는 줄을 고친다:

```csharp
                sphere.transform.position = ArcheryTargetMotion.PositionAt(targets[i], renderTick, interval);
```

> `interval`은 `double`이므로 `(float)interval`로 넘긴다.

그리고 **아직 안 솟았거나 떨어진 과녁은 그리지 않는다** — `consumed.IsTargetGone` 검사 바로 뒤에 더한다:

```csharp
                if (ArcheryTargetMotion.IsAlive(targets[i], renderTick, (float)interval) == false)
                {
                    continue;   // 아직 안 솟았거나 이미 떨어졌다
                }
```

- [ ] **Step 2: 화살 꽂힘도 움직이는 과녁을 본다**

`Assets/Scripts/Game/TickSystems/ArcheryArrowStickSystem.cs`의 `CrossesLiveTarget`을 고친다. 서버 판정과 **같은 시각 규칙**(구간 가운데)을 쓴다:

```csharp
        private bool CrossesLiveTarget(in ArcheryShot shot, int wave, long tick,
                                       float fromSeconds, float toSeconds,
                                       out int slot, out float atSeconds)
        {
            Vector3 from = ArcheryTrajectory.PositionAt(shot, fromSeconds);
            Vector3 to = ArcheryTrajectory.PositionAt(shot, toSeconds);
            for (int i = 0; i < targets.Count; i++)
            {
                //  이미 먹힌 과녁에는 안 꽂힌다 — 서버가 사라졌다고 한 자리다.
                if (consumed.IsTargetGone(wave, targets[i].SlotIndex))
                {
                    continue;
                }
                //  서버 판정과 **같은 시각**을 쓴다 — 다르면 화면에선 꽂혔는데 점수는 안 나거나
                //  그 반대다. 있는 자리와 살아 있는지를 둘 다 이 한 시각으로 묻는 것까지 같아야 한다.
                double at = tick - 0.5;

                if (ArcheryTargetMotion.IsAlive(targets[i], at, tickInterval) == false)
                {
                    continue;
                }

                Vector3 targetAt = ArcheryTargetMotion.PositionAt(targets[i], at, tickInterval);

                if (ArcheryHitTest.SegmentHitsSphere(from, to, targetAt, targets[i].Radius, out float t))
                {
                    slot = targets[i].SlotIndex;
                    atSeconds = Mathf.Lerp(fromSeconds, toSeconds, t);
                    return true;
                }
            }
            slot = -1;
            atSeconds = 0f;
            return false;
        }
```

`Advance`의 호출부도 틱을 넘기게 고친다. 기존 루프가 `for (long t = from; t < tick; t++)`로 한 틱씩 밟으므로 **그 `t`를 넘긴다**(`tick`이 아니라):

```csharp
                if (CrossesLiveTarget(shot, wave, t + 1, fromSeconds, toSeconds, out int slot, out float at))
```

> `t`는 구간 시작이고 판정은 구간 끝 틱을 기준으로 하므로 `t + 1`이다. 서버의 `CollectCandidates`가 `tick`(구간 끝)을 쓰는 것과 짝이 맞는다.

`ArcheryArrowStickSystem`이 `config`를 이미 들고 있는지 확인하고, 없으면 생성자에 더한다(스코프 등록도 같이).

- [ ] **Step 3: 컴파일을 확인한다**

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client recompile_status
```

기대: `failed:false`, `errors:[]`.

> ⚠️ **클라에서 `run_tests`를 돌리지 않는다** — 러너가 이 환경에서 고착돼 매번 30초 타임아웃이다. 컴파일 초록이 클라의 게이트다.

- [ ] **Step 4: 커밋**

```
feat(archery): 화면이 솟아오르는 과녁을 그린다

뷰가 정수 틱을 쓰던 근거("과녁은 가만히 있다")가 깨졌다 — 그대로 두면
과녁이 20ms 계단으로 튄다. 소수 틱으로 바꾸고 주석도 고쳤다.

화살 꽂힘 판정은 서버와 **같은 시각 규칙**(구간 가운데)을 쓴다. 다르면
화면에선 꽂혔는데 점수는 안 나거나 그 반대가 된다.
```

---

## Task 6: 데이터에 솟는 높이 범위·간격·쉼을 넣는다 (infrastructure + MasterData 둘)

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/infrastructure/table/Datas/#ArcheryConfig.xlsx`
- Generated: `LeagueOfPhysical-MasterData-{Client,Server}/Runtime.Generated/**`
- Modify: `LeagueOfPhysical-MasterData-Server/Tests/EditMode/ArcheryTargetSeparationTests.cs`

**Interfaces:**
- Consumes: (없음 — 데이터)
- Produces: Luban 생성 `ArcheryConfig`에 `RiseHeightMin`/`RiseHeightMax`/`StaggerTicks`/`RestTicks`

> **컬럼은 반드시 맨 뒤에 붙인다.** Luban은 컬럼 이름이 아니라 몇 번째 열인지로 읽는다.

- [ ] **Step 1: 엑셀에 컬럼 넷을 더하고 묶음 값을 조정한다**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table
python - <<'PY'
import openpyxl

wb = openpyxl.load_workbook('Datas/#ArcheryConfig.xlsx')
ws = wb.worksheets[0]
header = [c.value for c in ws[1]]

def set_by_name(name, value):
    ws.cell(row=5, column=header.index(name) + 1, value=value)

#  묶음: 3~5개가 다다닥 솟는다(spec 3.2).
set_by_name('min_targets', 3)
set_by_name('max_targets', 5)

#  과녁이 무대에서 솟아나오게 한다. 이 값들은 과녁이 *떠 있던* 시절 것이라
#  그대로 두면 공중 1.5~8m에 나타나 깡충 뛰었다 사라진다 — 솟아오르는 게 아니라
#  떠서 까딱거리는 그림이다. 맵의 가운데 무대 윗면이 y=0.3이므로 거기서 출발시킨다.
#  (솟는 높이 상한이 2.4m로 묶여 있어 "바닥에서 5m까지 솟구치게"는 불가능하다 —
#   고칠 곳은 솟는 높이가 아니라 출발 높이였다. 정점은 1.5~3.0m가 된다.)
set_by_name('spawn_min_y', 0.3)
set_by_name('spawn_max_y', 0.6)

#  웨이브 주기는 묶음 전체 + 쉼을 덮어야 한다.
#  가장 높이(2.4m) 솟는 과녁의 수명이 0.98초 = 49틱이므로
#  (5-1)*12 + 49 + 20 = 117 이고, 여유를 둬 120으로 한다.
set_by_name('wave_period_ticks', 120)

#  높이는 과녁마다 이 사이에서 뽑는다 — 고정이면 "언제쯤 정점"이 몸에 밴다.
#  상한 2.4m: 2.5m를 넘으면 과녁이 한 틱에 가장 작은 과녁 반지름(0.2m)보다
#  많이 움직여 판정이 뚫린다. 수명·속도는 데이터에 없다 — 중력이 고정이라
#  높이 하나가 둘 다 정한다.
added = [
    ('rise_height_min',   'float', 1.2),    # 수명 0.69초
    ('rise_height_max',   'float', 2.4),    # 수명 0.98초, 한 틱 0.196m < 0.2m
    ('stagger_ticks',     'int',   12),     # 0.24초 간격으로 하나씩
    ('rest_ticks',        'int',   20),     # 묶음 사이 0.4초 쉼
]
start = ws.max_column + 1
for i, (name, typ, value) in enumerate(added):
    c = start + i
    ws.cell(row=1, column=c, value=name)
    ws.cell(row=2, column=c, value=typ)
    ws.cell(row=4, column=c, value=name)
    ws.cell(row=5, column=c, value=value)
wb.save('Datas/#ArcheryConfig.xlsx')
print('done')
PY
```

- [ ] **Step 2: 엑셀을 눈으로 확인한다**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table
python -c "
import openpyxl
ws = openpyxl.load_workbook('Datas/#ArcheryConfig.xlsx', data_only=True).worksheets[0]
for row in ws.iter_rows(values_only=True):
    if any(c is not None for c in row): print(row)
"
```

기대: `##var` 줄 끝에 `rise_height_min, rise_height_max, stagger_ticks, rest_ticks`, `##type`에 `float, float, int, int`, 데이터 줄 끝에 `1.2, 2.4, 12, 20`. **`min_targets`=3, `max_targets`=5, `wave_period_ticks`=120, `spawn_min_y`=0.3, `spawn_max_y`=0.6**. 그 밖의 기존 값(`spawn_radius` 3.5, `min_separation` 1.2 등)은 **하나도 안 바뀌어야 한다** — 하나라도 움직였으면 멈추고 되돌린다.

- [ ] **Step 3: 굽고 네 출력처를 확인한다**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table && ./gen.sh
git -C /c/Users/re5na/workspace/LOP/infrastructure status --short
git -C /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client status --short
git -C /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server status --short
git -C /c/Users/re5na/workspace/LOP/lop-backend status --short
```

기대: 앞의 셋에 변경, `lop-backend`는 무변경. 생성물에 새 필드가 들어갔는지:

```bash
grep -n "RiseHeightMin\|RiseHeightMax\|StaggerTicks\|RestTicks" \
  /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server/Runtime.Generated/Scripts/MasterData/ArcheryConfig.cs
```

- [ ] **Step 4: 배포 데이터 검사를 더한다**

`LeagueOfPhysical-MasterData-Server/Tests/EditMode/ArcheryTargetSeparationTests.cs`에 더한다. 파일의 기존 `LoadTables()` 헬퍼를 그대로 쓴다:

```csharp
        //  웨이브 주기가 묶음 전체를 못 덮으면 마지막 과녁이 공중에서 잘려 사라진다 —
        //  에러는 안 나고 "가끔 과녁이 덜 뜨는" 것으로만 보인다.
        [Test]
        public void 웨이브_주기가_묶음_전체와_쉼을_덮는다()
        {
            var tables = LoadTables();
            var config = tables.TbArcheryConfig.GetOrDefault(1);
            Assert.IsNotNull(config, "TbArcheryConfig id=1 행이 없다");

            //  가장 높이 솟는 과녁이 제일 오래 떠 있다 — 그 기준으로 재야 안전하다.
            //  중력이 화살과 같으므로 v0 = sqrt(2gH), 수명 = 2v0/g다.
            float g = 20f;   // ArcheryTrajectory.Gravity — MasterData 패키지는 Shared를 참조하지 않는다
            float longestLifetime = 2f * Mathf.Sqrt(2f * g * config.RiseHeightMax) / g;
            //  틱은 정수라 올림한다 — 내림하면 마지막 한 틱이 모자라 과녁이 땅에 닿기 전에 잘린다.
            int lifetimeTicks = Mathf.CeilToInt(longestLifetime / 0.02f);
            int needed = (config.MaxTargets - 1) * config.StaggerTicks + lifetimeTicks + config.RestTicks;

            Assert.GreaterOrEqual(
                config.WavePeriodTicks, needed,
                $"wave_period_ticks({config.WavePeriodTicks})가 묶음 전체({needed}틱: 마지막 과녁이 "
                + $"{(config.MaxTargets - 1) * config.StaggerTicks}틱 뒤에 솟아 {lifetimeTicks}틱을 살고, "
                + $"쉼 {config.RestTicks}틱)보다 짧다 — 마지막 과녁이 공중에서 잘린다");
        }

        //  과녁이 솟는 공간을 벗어나면 사대 위로 넘어오거나 화면 밖으로 나간다.
        [Test]
        public void 솟는_높이가_과녁_공간_안에_들어간다()
        {
            var tables = LoadTables();
            var config = tables.TbArcheryConfig.GetOrDefault(1);
            Assert.IsNotNull(config, "TbArcheryConfig id=1 행이 없다");

            //  가장 높은 자리에서 가장 높이 솟는 경우가 천장에 제일 가깝다.
            float apex = config.SpawnMaxY + config.RiseHeightMax;
            //  솟아오르는 만큼 천장 위로 올라가도 되는 여유(m). 화면 밖으로 나가지만 않으면 된다.
            const float Headroom = 4f;
            Assert.LessOrEqual(apex, config.SpawnMaxY + Headroom,
                $"가장 높은 자리({config.SpawnMaxY})에서 {config.RiseHeightMax}m 솟으면 {apex}m다 — "
                + "화면 밖으로 나갈 수 있다");
        }

        //  이 검사가 이 슬라이스의 생명줄이다 — 높이를 올리면 과녁이 한 틱에 자기 반지름보다
        //  많이 움직여 화살이 뚫고 지나간다. 에러는 안 나고 "가끔 안 맞는다"로만 보인다.
        [Test]
        public void 가장_높이_솟는_과녁도_한_틱에_가장_작은_반지름보다_적게_움직인다()
        {
            var tables = LoadTables();
            var config = tables.TbArcheryConfig.GetOrDefault(1);
            Assert.IsNotNull(config, "TbArcheryConfig id=1 행이 없다");

            float g = 20f;   // ArcheryTrajectory.Gravity
            float fastest = Mathf.Sqrt(2f * g * config.RiseHeightMax);
            float perTick = fastest * 0.02f;

            float smallest = float.MaxValue;
            foreach (var row in tables.TbArcheryTarget.DataList)
            {
                smallest = Mathf.Min(smallest, row.Radius);
            }

            Assert.Less(perTick, smallest,
                $"rise_height_max({config.RiseHeightMax}m)면 과녁이 한 틱에 {perTick:F3}m 움직이는데 "
                + $"가장 작은 과녁 반지름이 {smallest:F3}m다 — 판정이 뚫린다. 높이를 낮추거나 "
                + "가장 작은 과녁을 키워야 한다");
        }
```

`using UnityEngine;`이 없으면 더한다(`Mathf` 때문).

- [ ] **Step 5: 컴파일·테스트**

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server run_tests --mode EditMode
```

기대: 컴파일 초록, Failed 0, 새 검사 둘이 이름으로 보인다.

> ⚠️ **`솟는_높이가_과녁_공간_안에_들어간다`가 떨어질 수 있다.** 지금 데이터는 `spawn_min_y`=1.5, `spawn_max_y`=8이라 1.5+5=6.5 ≤ 8로 통과한다. 떨어지면 데이터를 고치고, **테스트를 고치지 않는다.**

- [ ] **Step 6: 커밋 (레포 셋)**

infrastructure:

```
feat(archery): 데이터에 솟는 높이 범위·간격·쉼을 넣는다

묶음을 3~5개로 키우고 웨이브 주기를 120틱으로 맞췄다 —
(5-1)*12 + 49 + 20 = 117틱이 필요하다(49 = 가장 높이 솟는 과녁의 수명).

높이는 1.2~2.4m 범위에서 과녁마다 뽑는다. 고정이면 "언제쯤 정점"이 몸에 배어
리듬만으로 쏘게 된다. 수명·속도 컬럼은 없다 — 중력이 화살과 같은 값으로
고정이라 높이 하나가 둘 다 정한다.

상한 2.4m는 물리가 정했다. 2.5m를 넘으면 과녁이 한 틱에 가장 작은 과녁
반지름(0.2m)보다 많이 움직여 판정이 뚫린다 — 그 선을 검사로 못박았다.

컬럼은 전부 맨 뒤에 붙였다. Luban은 컬럼 이름이 아니라 몇 번째 열인지로
읽으므로 중간에 끼우면 기존 값이 조용히 뒤바뀐다.
```

MasterData 둘: `chore(masterdata): 솟아오르는 과녁 설정을 굽는다` (서버는 검사 둘 포함)

---

## Task 7: 서버·클라 provider가 실제 컬럼을 읽는다

**Files:**
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/ArcheryConfigProvider.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/ArcheryConfigProvider.cs`

**Interfaces:**
- Consumes: Luban 생성 필드 (Task 6), `ArcheryConfig` 17인자 생성자 (Task 2)
- Produces: (없음)

**이 태스크의 유일한 위험은 두 파일이 갈리는 것이다.** 갈리면 클·서가 다른 과녁을 보는데 에러가 안 난다.

- [ ] **Step 1: 서버를 고친다**

Task 2에서 넣은 임시값을 실제 컬럼으로 바꾸고, 낡은 주석을 지운다:

```csharp
            return new ArcheryConfig(
                r.WavePeriodTicks, r.MinTargets, r.MaxTargets,
                r.SpawnRadius, r.SpawnMinY, r.SpawnMaxY, r.MinSeparation,
                r.TrapRatioMin, r.TrapRatioMax,
                r.ShakeFreeSeconds, r.ShakeRampSeconds, r.ShakeMaxDegrees,
                r.RiseHeight, r.LifetimeSeconds, r.StaggerTicks, r.RestTicks,
                kinds);
```

- [ ] **Step 2: 클라를 똑같이 고친다**

같은 블록으로 바꾼다. **한 글자도 다르면 안 된다.**

- [ ] **Step 3: 두 파일을 대조한다**

```bash
diff <(sed -n '/return new ArcheryConfig/,/kinds);/p' \
        /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Game/ArcheryConfigProvider.cs) \
     <(sed -n '/return new ArcheryConfig/,/kinds);/p' \
        /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Game/ArcheryConfigProvider.cs) \
  && echo "구성 블록 동일"
```

**출력이 없어야 통과다.** 보고서에 이 출력을 넣는다.

- [ ] **Step 4: 양쪽 컴파일·서버 테스트**

```
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server recompile_status
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server run_tests --mode EditMode
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client recompile
unity command --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client recompile_status
```

기대: 서버 Failed 0, 양쪽 컴파일 `errors:[]`.

- [ ] **Step 5: 커밋 (레포 둘)**

```
feat(archery): provider가 솟는 높이·수명·간격·쉼을 읽는다

클·서 쌍둥이가 한 글자도 다르면 안 된다 — 다르면 두 사이드가 다른 과녁을
보는데 에러는 안 나고 점수만 이상해진다. diff로 대조했다.
```

---

## Task 8: 여섯 레포 머지 · 배포 · 실측

**Files:**
- Modify: `docs/ROADMAP.md` (클라)

**Interfaces:**
- Consumes: Task 1~7 전부

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
for f in tbarcheryconfig tbarcherytarget; do
  c=$(md5sum /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client/Runtime.Generated/StreamingAssets/MasterData/$f.bytes | cut -d' ' -f1)
  s=$(md5sum /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server/Runtime.Generated/StreamingAssets/MasterData/$f.bytes | cut -d' ' -f1)
  printf "%-20s %s\n" $f "$([ "$c" = "$s" ] && echo 동일 || echo ⚠️다름)"
done
```

**다르면 멈춘다.** 난수 소비 횟수가 데이터에 의존하므로, 다르면 두 사이드가 완전히 다른 과녁을 본다.

- [ ] **Step 3: ROADMAP을 갱신한다**

`docs/ROADMAP.md`의 Archery 절 위에 슬라이스 4 절을 쓴다. 담을 것:

- **슬라이스 3 판정이 "아니오"였다**는 사실과 그 원인(대가 셋 중 둘이 없었음, 2인이라 함정 전제 미성립)
- 무엇을 바꿨나 — 솟아오르는 과녁, 연달아 솟는 묶음
- 결정 다섯(위 "착수 전에 박아둘 결정 다섯")을 요약. 특히 **과녁 중력이 화살 중력과 다른 상수**인 이유와 **판정 근사의 전제**
- 실측 항목(Step 5)을 체크박스로

- [ ] **Step 4: 여섯 레포를 규약대로 머지한다**

레포: `LeagueOfPhysical-Shared` · `LeagueOfPhysical-MasterData-Client` · `LeagueOfPhysical-MasterData-Server` · `LeagueOfPhysical-Server` · `LeagueOfPhysical-Client` · `infrastructure`

레포마다 **한 줄씩 결과를 확인하며**:

```bash
git fetch origin
git rebase --autostash origin/main
git checkout main
git merge --ff-only origin/main
git merge --no-ff feature/archery-slice4
git push origin main
```

**`&&`로 잇지 말 것.** Unity 레포는 `--autostash`가 dirty한 Art 서브모듈과 얽혀 막히는 일이 있다 — 그때는 픽스처를 `git stash push -u -m ... -- <경로>`로 직접 빼고 리베이스한 뒤 `pop`한다. 머지에도 막히면 `git -c merge.autoStash=false merge --no-ff ...`를 쓴다.

클라는 머지 뒤 **Art 포인터가 원격과 같은지** 확인한다:

```bash
[ "$(git ls-tree HEAD Assets/Art | awk '{print $3}')" = "$(git ls-tree origin/main Assets/Art | awk '{print $3}')" ] && echo 예 || echo ⚠️아니오
```

- [ ] **Step 5: 배포하고 실물로 대조한다**

**클·서가 반드시 함께 나가야 한다** — 난수 소비 순서가 또 바뀌었다.

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
gh workflow run gameserver-deploy -f environment=local -f package_ref=main
```

끝나면:

```bash
gh run list --workflow gameserver-deploy --limit 1
kubectl get cm -o name | grep game | xargs -I{} kubectl get {} -o jsonpath='{.data.GAME_SERVER_IMAGE}'
git -C /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server rev-parse --short=7 origin/main
```

이미지 태그가 서버 main과 같아야 한다. 서버 코드가 바뀌었으므로 이번엔 태그가 움직인다.

- [ ] **Step 6: 두 클라로 실측한다 (⭐ spec §12.2)**

- [ ] **"언제 쏘느냐"가 생겼나** — 정점에서 쏘게 되나, 아무 때나 쏴도 비슷한가
- [ ] **함정을 실수로 쏘게 되나** — 리듬이 손을 끌고 가는 순간이 실제로 오나
- [ ] 여전히 **미리 당기기가 공짜인가** — 과녁이 움직이니 당겨놓은 조준이 저절로 낡는가
- [ ] 양쪽이 **같은 과녁을 같은 자리에서** 보나 — 난수 순서가 또 바뀌었으니 여기서 드러난다
- [ ] 묶음 리듬(0.24초 간격, 0.4초 쉼)이 **읽히나** — 너무 빠르거나 늘어지지 않나
- [ ] 과녁이 **화면 밖으로 나가거나 사대로 넘어오지** 않나
- [ ] 화살이 움직이는 과녁에 **제대로 꽂히나** — 화면과 점수가 어긋나지 않나

> ⚠️ **2인 판정의 한계**(spec §12.2): "과녁이 부족하다"가 만드는 압박은 사람이 늘어야 나타난다. 2인 결과를 그 축의 결론으로 삼지 않는다.

- [ ] **Step 7: 판정 결과를 spec §12에 적는다**

답이 "아니오"면 그 사실을 크게 적는다. 그 위에 더 얹기 전에 알아야 한다.

---

## 확인 목록 (머지 전에 한 번 더)

- [ ] `MessageIds.cs`와 `Protos/`를 **하나도 안 건드렸다**(이 슬라이스는 와이어를 안 바꾼다)
- [ ] 클·서 `ArcheryConfigProvider`의 `new ArcheryConfig(...)` 블록이 **diff에서 완전히 같다**
- [ ] 양 MasterData 패키지의 `tbarcheryconfig.bytes` / `tbarcherytarget.bytes`가 **md5 동일**
- [ ] `#ArcheryConfig`의 **기존 컬럼 값이 의도한 셋(min/max_targets, wave_period_ticks) 말고는 안 바뀌었다**
- [ ] 새로 만든 `.cs` 둘(`ArcheryTargetMotion`, `ArcheryTargetMotionTests`)에 `.meta`가 함께 스테이지됐다
- [ ] 두 Unity 레포의 `git status --short`에 로컬 픽스처가 **커밋에 섞이지 않았다**
- [ ] 서버 EditMode 전부 초록, 새 테스트가 **이름으로** 결과에 보인다(개수만 보지 않는다)
- [ ] 기존 결정론 테스트(`같은_씨앗과_웨이브는...`)가 통과한다
- [ ] **클·서를 함께 배포했다** — 난수 순서가 바뀌었다

---

## 자기 점검 (계획을 쓴 뒤)

**spec 대응:**

| spec | 태스크 |
|---|---|
| §3.1 과녁이 솟았다 떨어진다 | Task 1(궤적 커널) + Task 3(생성) |
| §3.1 솟는 높이 5m · 정점 0.43초 | Task 2(설정) + Task 6(데이터) |
| §3.1 한가운데는 피한다 | **기존 동작 유지** — `PickCenter`가 이미 원판 위에 고르게 뽑고 `min_separation`이 겹침을 막는다. 추가 작업 없음 |
| §3.2 묶음 3~5개 | Task 6(데이터) |
| §3.2 연달아 솟는다 | Task 3(`SpawnTick`이 슬롯마다 다르다) |
| §3.2 묶음 사이 쉼 | Task 2(`RestTicks`) + Task 6(데이터) + Task 6(검사) |
| §3.2 함정 비율은 묶음마다 | **기존 동작 유지** — 이미 웨이브마다 뽑는다 |
| §3.2 작을수록 비싸다 | **기존 동작 유지** — 데이터에 이미 있다 |
| §12.2 다음 판정에서 물을 것 | Task 8 Step 6 |

**타입 일관성 확인:**
- `ArcheryTarget` 생성자 인자 순서가 Task 1 정의 ↔ Task 3 호출에서 일치
- `ArcheryConfig` 17인자가 Task 2 정의 ↔ Task 2 Step 4 provider ↔ Task 7 실제 컬럼에서 일치
- `ArcheryTargetMotion.PositionAt(in ArcheryTarget, double, float)`가 Task 1 정의 ↔ Task 4(서버) ↔ Task 5(클라 둘)에서 일치
- `Fill(..., long gameplayStartTick)`이 Task 3 정의 ↔ 소비처 셋에서 일치

**계획 단계에서 검산한 것 (그대로 쓰면 된다):**

중력은 화살과 같은 **20**으로 고정이고, 높이 하나가 속도도 수명도 정한다(`v₀=√(2gH)`, `T=2v₀/g`).

| 솟는 높이 | 수명 | 초기속도 | 한 틱 이동 |
|---|---|---|---|
| 1.2m (하한) | 0.69초 | 6.93 m/s | 0.139m ✅ |
| 1.8m | 0.85초 | 8.49 m/s | 0.170m ✅ |
| **2.4m (상한)** | **0.98초** | **9.80 m/s** | **0.196m** < 가장 작은 반지름 0.2m ✅ |
| 2.5m | 1.00초 | 10.0 m/s | 0.200m ❌ 근사가 깨진다 |

- 정점 주변 느린 구간(±0.3m): **0.35초** — 화살 비행시간 0.2~0.5초와 같은 자릿수
- 필요한 웨이브 주기: (5−1)·12 + ⌈0.98/0.02⌉ + 20 = **117틱** → 120으로 둠

**처음 쓴 설계 둘을 계획 단계에서 고쳤다:**

1. **과녁에 자기 중력(8.26)을 주려 했다** — 틀렸다. 화살과 과녁이 같은 화면에 있고 플레이어는
   화살로 과녁을 따라간다. 0.6초에 화살은 3.6m 떨어지는데 과녁은 1.5m면 세상에 중력이 둘 있는
   것처럼 보인다. **중력은 세계의 성질이지 물체의 설정이 아니다.**
2. **솟는 높이를 5m 고정으로 두려 했다** — 중력을 20으로 통일하면 5m는 한 틱 0.283m라 근사가
   깨진다. 그리고 고정이면 몇 번 보고 나서 "언제쯤 정점"이 몸에 배어 리듬만으로 쏘게 된다.
   **1.2~2.4m 범위에서 과녁마다 뽑는다.**

**남은 위험(계획이 못 막는 것):**

- 상한 2.4m는 여유가 **0.004m뿐**이다. 실측에서 "움직임이 약하다"고 판단해 높이를 올리면
  Task 6의 검사가 걸린다 — 그때는 **가장 작은 과녁(0.2m)을 키우거나** 움직이는 구 판정으로
  바꾼다. **검사를 고치지 않는다.**
- 솟는 폭이 1.2~2.4m라 spec §3.1이 적었던 5m보다 작다. 실측에서 "덜 극적이다"가 나올 수 있다 —
  그렇다면 위 선택지 둘 중 하나를 골라야지, 높이만 올리면 판정이 조용히 뚫린다.
- 묶음이 끝난 뒤 빈 시간이 생긴다(수명이 웨이브 주기보다 짧다). `rest_ticks` 0.4초 외의 여백이
  늘어진다고 느껴지면 `stagger_ticks`를 늘리거나 웨이브 주기를 줄인다(Task 6의 하한 아래로는 못 간다).
