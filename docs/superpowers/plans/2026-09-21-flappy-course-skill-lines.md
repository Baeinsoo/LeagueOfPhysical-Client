# Flappy 코스 — 실력이 거리로 바뀌는 구조 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 대시 충전을 "과감함"에만 주도록 고치고, 코스에 고저차·갈림길·부스트 패드를 넣어 **실력이 거리로 바뀌는 통로**를 만든다.

**Architecture:** 배치 규칙은 순수 C#(`LOP.MapTools`)에 두고 EditMode로 잰다. 게임 규칙(충전 곡선·부스트)은 **LOP-Shared**에 두어 클·서가 같은 코드를 돈다. 부스트 패드는 `FlappyWindmill`이 이미 쓰는 **씬 마커 + 필드 주입** 패턴을 그대로 따른다.

**Tech Stack:** Unity 6000.3 / URP · VContainer · NUnit · Luban MasterData · `GameFramework.Rng`

**Spec:** [docs/superpowers/specs/2026-09-20-flappy-course-skill-lines-design.md](../specs/2026-09-20-flappy-course-skill-lines-design.md)

## Global Constraints

- **창 4.37m · 관문 간격 11.4m는 불변.** 움직이는 것은 회랑 중심뿐이다.
- **시뮬 로직은 LOP-Shared에 둔다.** 클·서가 같은 구체 코드를 돌아야 갈리지 않는다. 인터페이스 seam을 만들지 않는다.
- **씬 마커 MonoBehaviour는 LOP-Shared에 둔다.** 맵은 클라가 만들고 서버가 읽는다 — 한쪽에만 있으면 반대쪽에서 missing script가 되고 **그 빈 컴포넌트가 씬 주입을 끊는다**(`FlappyWindmill` 주석이 박아 둔 사실).
- **물리 질의로 부스트를 판정하지 않는다.** 사각형 포함 여부를 산술로만 계산한다.
- **`LOP.MapTools`는 에디터 전용 어셈블리다.** 런타임도 쓰는 순수 로직은 `FlappyRaceSlice.Logic`(전 플랫폼·`noEngineReferences`)에 둔다 — 에디터에선 컴파일되고 **플레이어 빌드에서만 깨진다**.
- **굽기 → 곧바로 `save_scene`.** 빌더가 저장해도 Undo 등록이 씬을 다시 dirty로 만든다. 그 상태로 도메인 리로드가 걸리면 모달이 에디터를 통째로 막는다.
- **git**: main 직접 커밋 금지 · `git add -A` 금지 · force push 금지. 로컬 픽스처(`Assets/Art`, Jua 폰트, `PackageManagerSettings`)를 스테이지하지 않는다.
- **`.meta`는 유니티가 만든 것을 함께 커밋**하고, 파일을 옮길 땐 `.cs`와 `.meta`를 함께 `git mv`.
- **MasterData를 고치면 형제 레포부터 당긴다.** infrastructure·양쪽 MasterData가 뒤처진 채로 `gen.sh`를 돌리면 다른 기계의 작업이 되돌아간다.

---

## File Structure

**LOP-Shared (게임 규칙 — 클·서 공통)**

| 파일 | 책임 |
|---|---|
| `Runtime/Scripts/Game/FlappyDashSystem.cs` *(수정)* | 충전 곡선을 세제곱으로 |
| `Runtime/Scripts/Game/FlappyBoostPad.cs` *(신규)* | 씬 마커 — 스스로 필드에 등록 |
| `Runtime/Scripts/Game/FlappyBoostPadField.cs` *(신규)* | 이 판의 패드 전부 + 포함 판정 |
| `Runtime/Scripts/Game/FlappyWorld.cs` *(수정)* | 매 틱 패드 적용 |
| `Tests/EditMode/FlappyDashSystemTests.cs` *(수정)* · `FlappyBoostPadFieldTests.cs` *(신규)* | |

**클라 (배치 규칙 · 빌더 · 검사)**

| 파일 | 책임 |
|---|---|
| `Assets/Scripts/FlappyRaceSlice/Logic/FlappyElevation.cs` *(이동)* | 고저차 곡선 — 엔진 비의존으로 고쳐 옮긴다 |
| `Assets/Scripts/MapTools/CourseElevation.cs` *(신규)* | 진행률 → 진폭·뾰족함 (순수) |
| `Assets/Scripts/MapTools/BranchLayout.cs` *(신규)* | 갈림길 배치 (순수) |
| `Assets/Scripts/MapTools/ClassicCourse.cs` *(수정)* | 회랑 중심에 고저차 |
| `Assets/Scripts/Editor/FlappyClassicCourseBuilder.cs` *(수정)* | 고저차·갈림길·패드 굽기 |
| `Assets/Scripts/Editor/FlappyMapPlayabilityCheck.cs` *(수정)* | 두 길 각각 검증 |
| `Assets/Scripts/MapTools/PlayabilityReport.cs` *(수정)* | 두 길 절 + 카메라 주석 정정 |

**infrastructure / MasterData**: `table/Datas/#FlappyConfig.xlsx` → `gen.sh` → 양쪽 패키지 `.bytes`

---

### Task 1: 충전 곡선을 세제곱으로

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyDashSystem.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/FlappyDashSystemTests.cs`

**Interfaces:**
- Consumes: `FlappyConfig.DashChargeBase` · `DashChargeDive` · `MaxFallSpeed` (기존)
- Produces: 동작 변경만. 새 API 없음

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`FlappyDashSystemTests.cs`에 더한다(기존 테스트는 손대지 않는다 — 값이 바뀌어 깨지는 것만 Step 5에서 고친다):

```csharp
        //  아래 셋이 이 게임의 리스크·리워드를 숫자로 못박는다. 곡선을 건드리면 여기가 먼저 빨개진다.

        [Test]
        public void 평범한_날갯짓_속도에서는_거의_안_찬다()
        {
            //  날갯짓은 튀었다 떨어지는 반복이라 평범하게 날아도 시간의 절반이 낙하다.
            //  그 평균 낙하(9.3 m/s = 최대의 31%)가 충전의 31%를 받아가면 "과감함"이 값을 잃는다.
            //  세제곱이면 31% → 3%다.
            var system = new FlappyDashSystem(Config());
            var bird = Bird(verticalSpeed: -9.3f);

            system.Tick(bird, 1f);   // 1초

            float max = Config().DashChargeDive;   // 최대 낙하에서의 1초치
            Assert.Less(bird.Get<FlappyDash>().Charge, max * 0.05f, "평범한 비행이 최대의 5%를 넘으면 안 된다");
        }

        [Test]
        public void 최대_낙하에서는_계수를_그대로_받는다()
        {
            var system = new FlappyDashSystem(Config());
            var bird = Bird(verticalSpeed: -30f);   // MaxFallSpeed

            system.Tick(bird, 0.1f);

            Assert.AreEqual(Config().DashChargeDive * 0.1f, bird.Get<FlappyDash>().Charge, 1e-5f);
        }

        [Test]
        public void 회랑_전체를_다이브하면_반_칸이다()
        {
            //  14.56m를 중력 59로 떨어지는 동안 실제로 얼마나 차는지 — 이 값이 "한 칸 = 깊은
            //  다이브 두 번"이라는 설계를 지킨다. 회랑 높이나 중력을 바꾸면 여기가 먼저 말해 준다.
            var config = new FlappyConfig(forwardSpeed: 6.8f, flapImpulse: 18.6f, gravity: 59f,
                                          maxFallSpeed: 30f, bodyRadius: 0.45f, bodyHeight: 0.9f,
                                          restitution: 0.35f, stunTime: 0.8f, invulnTime: 0.6f,
                                          dashMult: 2f, dashDuration: 0.2f,
                                          dashChargeBase: 0f, dashChargeDive: 1.4f);
            var system = new FlappyDashSystem(config);
            var bird = Bird();
            var velocity = bird.Get<Velocity>();

            float fallen = 0f;
            while (fallen < 14.56f)
            {
                float vy = velocity.Linear.Y - config.Gravity * Dt;
                if (vy < -config.MaxFallSpeed) { vy = -config.MaxFallSpeed; }
                velocity.Linear = new System.Numerics.Vector3(velocity.Linear.X, vy, 0f);
                fallen += -vy * Dt;
                system.Tick(bird, Dt);
            }

            Assert.AreEqual(0.50f, bird.Get<FlappyDash>().Charge, 0.06f);
        }

        [Test]
        public void 기본_충전이_0이면_올라갈_때는_안_찬다()
        {
            var config = new FlappyConfig(forwardSpeed: 6.8f, flapImpulse: 18.6f, gravity: 59f,
                                          maxFallSpeed: 30f, bodyRadius: 0.45f, bodyHeight: 0.9f,
                                          restitution: 0.35f, stunTime: 0.8f, invulnTime: 0.6f,
                                          dashMult: 2f, dashDuration: 0.2f,
                                          dashChargeBase: 0f, dashChargeDive: 1.4f);
            var system = new FlappyDashSystem(config);
            var bird = Bird(verticalSpeed: +18.6f);

            system.Tick(bird, 1f);

            Assert.AreEqual(0f, bird.Get<FlappyDash>().Charge, Tolerance);
        }
```

- [ ] **Step 2: 돌려서 실패를 본다**

Unity(서버 또는 클라 프로젝트) EditMode에서 `FlappyDashSystemTests` 실행.
기대: `평범한_날갯짓_속도에서는_거의_안_찬다`가 실패(지금은 31%를 받는다).

- [ ] **Step 3: 곡선을 바꾼다**

`FlappyDashSystem.Tick`의 dive 계산을 고친다:

```csharp
            //  떨어지는 중일 때만 다이브 몫이 붙는다. <b>세제곱</b>인 것이 핵심이다 — 날갯짓은
            //  튀었다 떨어지는 반복이라 평범하게 날아도 시간의 절반이 낙하다. 속도에 단순
            //  비례하면 평범한 비행(평균 9.3 = 최대의 31%)이 31%를 그대로 받아가서, "과감하게
            //  내려간다"와 "그냥 난다"를 구분하지 못한다. 세제곱이면 31% → 3%로 33배 벌어진다.
            //  지수는 컨피그로 빼지 않는다 — 튜닝 손잡이가 아니라 곡선의 모양이고, 바꾸면
            //  "한 칸 = 깊은 다이브 두 번"이라는 경제가 통째로 다시 계산돼야 한다.
            float fallSpeed = -(entity.Get<GameFramework.World.Velocity>()?.Linear.Y ?? 0f);
            float normalized = fallSpeed > 0f && config.MaxFallSpeed > 0f
                ? System.Math.Min(fallSpeed, config.MaxFallSpeed) / config.MaxFallSpeed
                : 0f;
            float dive = config.DashChargeDive * normalized * normalized * normalized;
```

- [ ] **Step 4: 통과를 본다** — 새 테스트 4개 PASS.

- [ ] **Step 5: 옛 테스트가 깨졌으면 고친다**

기존 테스트 중 **선형을 전제한 기대값**이 있으면 새 곡선 기준으로 고친다. *값만* 고치고 **테스트의 뜻을 바꾸지 않는다** — 뜻까지 바꿔야 한다면 그 테스트는 원래 무엇을 지키고 있었는지 커밋 메시지에 적는다.

- [ ] **Step 6: 일부러 깨뜨려 빨강을 확인한다**

`normalized * normalized * normalized`를 `normalized`로 되돌려 돌린다. **`평범한_날갯짓_속도에서는_거의_안_찬다`와 `회랑_전체를_다이브하면_반_칸이다`가 실패해야 한다.** 확인 후 되돌린다.

- [ ] **Step 7: 커밋 (LOP-Shared)**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git checkout -b feature/dash-charge-curve
git add Runtime/Scripts/Game/FlappyDashSystem.cs Tests/EditMode/FlappyDashSystemTests.cs
git status --short
git commit -m "feat(flappy): 대시 충전을 세제곱 곡선으로 — 과감함에만 준다"
```

---

### Task 2: MasterData 값 — 공짜 충전을 없앤다

**Files:**
- Modify: `infrastructure/table/Datas/#FlappyConfig.xlsx`
- Regenerate: 양쪽 MasterData 패키지 `.bytes`
- Modify: 양쪽 `Tests/EditMode/FlappyConfigColumnOrderTests.cs` (박아 둔 값)

- [ ] **Step 1: 형제 레포를 먼저 당긴다**

```bash
cd /Users/insoobae/workspace/LOP/infrastructure && git fetch origin && git status --short && git pull --ff-only
cd ../LeagueOfPhysical-MasterData-Client && git fetch origin && git pull --ff-only
cd ../LeagueOfPhysical-MasterData-Server && git fetch origin && git pull --ff-only
```

**뒤처진 채로 `gen.sh`를 돌리면 다른 기계의 데이터가 되돌아간다.** 한 줄씩 결과를 보고 넘어간다.

- [ ] **Step 2: 열 위치를 확인한다**

`#FlappyConfig.xlsx`의 헤더 행을 읽어 `dash_charge_base`·`dash_charge_dive`가 **몇 번째 열인지 직접 확인**한다. 열 위치를 기억에 의존해 추측하지 않는다 — 이전에 `forward_speed`가 C열이었다는 것은 다른 열의 근거가 못 된다.

- [ ] **Step 3: 값을 바꾼다**

| 필드 | 전 | 후 |
|---|---|---|
| `dash_charge_base` | 0.13 | **0** |
| `dash_charge_dive` | 1.2 | **1.4** |

- [ ] **Step 4: 생성한다**

```bash
cd /Users/insoobae/workspace/LOP/infrastructure/table && bash gen.sh
cd .. && git status --short
```

기대: `tbflappyconfig.bytes` **둘만** 바뀐다. 다른 테이블이 바뀌었으면 Step 1을 빠뜨린 것이다 — 되돌리고 다시 한다.

- [ ] **Step 5: 박아 둔 테스트를 고친다**

양쪽 MasterData의 `FlappyConfigColumnOrderTests.cs`에서 `0.13f` → `0f`, `1.2f` → `1.4f`.
이 테스트는 **바이트가 실제로 그 값인지**를 지키는 것이므로 반드시 같이 고친다(안 고치면 main이 빨개진다 — 전에 겪었다).

- [ ] **Step 6: 세 레포를 각각 푸시한다**

infrastructure · MasterData-Client · MasterData-Server 각각 피처 브랜치 → 리베이스 → `--no-ff` 머지 → 푸시. **한 줄씩.**

- [ ] **Step 7: 클라에서 값이 들어왔는지 확인한다**

```
unity cmd eval_file --file <cfg.cs>   # tbflappyconfig.bytes를 읽어 출력
```
기대: `충전기본 0 · 다이브충전 1.4`

---

### Task 3: 고저차 규칙 (순수)

**Files:**
- Move: `Assets/Scripts/FlappyRaceSlice/FlappyElevation.cs` → `Assets/Scripts/FlappyRaceSlice/Logic/FlappyElevation.cs` (`.meta`와 함께)
- Create: `Assets/Scripts/MapTools/CourseElevation.cs`
- Test: `Assets/Tests/EditMode/MapTools/CourseElevationTests.cs`

**Interfaces:**
- Consumes: `FlappyRace.CourseSectionRule.Progress`
- Produces: `CourseElevation.CenterY(float x, float startX, float length)` → 회랑 중심의 y 오프셋

> **Ruling — spec §6 표에서 한 가지를 바꾼다.** spec은 구간마다 *파장*을 다르게(120/90/60m) 적었지만, 파장이 x에 따라 변하면 **위상이 튀어** 회랑이 구간 경계에서 끊긴다. 그래서 **파장은 90m로 고정**하고, **진폭(2→7m)과 뾰족함(사인→삼각파)만 진행률을 따라 매끄럽게** 바꾼다. 의도(구간 1 완만 → 구간 3 뾰족)는 그대로 얻으면서 연속성이 깨지지 않는다.

- [ ] **Step 1: FlappyElevation을 엔진 비의존으로 옮긴다**

`FlappyRaceSlice.Logic`은 `noEngineReferences: true`라 `Mathf`를 못 쓴다. `System.Math`로 바꾼다:

```csharp
namespace FlappyRace
{
    /// <summary>
    /// 코스 고도 프로파일 — 생성기·플레이어 바닥/천장·봇이 모두 같은 공식을 쓰게 공유.
    /// sharp=false: 사인(완만한 언덕). sharp=true: 삼각파(뾰족한 V·W, 경사는 선형이라 따라갈 수 있음).
    ///
    /// <para><b>Logic 어셈블리에 산다</b>: 맵 빌더(에디터)와 런타임이 둘 다 쓸 수 있어야 하고,
    /// 이 어셈블리는 엔진을 참조하지 않으므로 <c>Mathf</c> 대신 <c>System.Math</c>를 쓴다.</para>
    /// </summary>
    public static class FlappyElevation
    {
        public static float Value(float x, float amp, float startX, float wavelength, bool sharp)
        {
            if (amp == 0f || wavelength <= 0f) { return 0f; }
            double ph = (x - startX) / wavelength;
            double w = sharp
                ? (2.0 / System.Math.PI) * System.Math.Asin(System.Math.Sin(2.0 * System.Math.PI * ph))
                : System.Math.Sin(2.0 * System.Math.PI * ph);
            return amp * (float)w;
        }

        /// <summary>사인과 삼각파를 <paramref name="sharpness"/>(0~1)로 섞는다 — 구간이 바뀌어도 끊기지 않는다.</summary>
        public static float Blend(float x, float amp, float startX, float wavelength, float sharpness)
        {
            float soft = Value(x, amp, startX, wavelength, sharp: false);
            float hard = Value(x, amp, startX, wavelength, sharp: true);
            float t = sharpness < 0f ? 0f : (sharpness > 1f ? 1f : sharpness);
            return soft + (hard - soft) * t;
        }
    }
}
```

옛 위치를 참조하던 곳이 있으면 함께 고친다(`grep -rn "FlappyElevation" Assets/`).

- [ ] **Step 2: 실패하는 테스트를 쓴다**

`Assets/Tests/EditMode/MapTools/CourseElevationTests.cs`:

```csharp
using FlappyRace;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// 회랑 중심의 높낮이. <b>끊기지 않는가</b>와 <b>뒤로 갈수록 커지는가</b>를 못박는다 —
    /// 구간 경계에서 튀면 통과 불가능한 자리가 생긴다.
    /// </summary>
    public class CourseElevationTests
    {
        const float StartX = 0f;
        const float Length = 612f;

        [Test]
        public void 시작점의_높이는_0이다()
        {
            //  스폰 자리가 흔들리면 안 된다.
            Assert.AreEqual(0f, CourseElevation.CenterY(StartX, StartX, Length), 1e-4f);
        }

        [Test]
        public void 어디서도_끊기지_않는다()
        {
            //  1m 간격으로 훑어 이웃 값의 차가 항상 작아야 한다. 파장이 구간마다 다르면
            //  경계에서 위상이 튀어 여기가 빨개진다(그래서 파장을 고정했다).
            float previous = CourseElevation.CenterY(StartX, StartX, Length);
            for (float x = StartX + 1f; x <= StartX + Length; x += 1f)
            {
                float current = CourseElevation.CenterY(x, StartX, Length);
                Assert.Less(System.Math.Abs(current - previous), 1.2f, $"x={x}에서 튀었다");
                previous = current;
            }
        }

        [Test]
        public void 뒤로_갈수록_높낮이가_커진다()
        {
            float front = Swing(StartX, StartX + Length / 3f);
            float back = Swing(StartX + Length * 2f / 3f, StartX + Length);

            Assert.Greater(back, front * 1.5f, "구간 3이 구간 1보다 확실히 커야 한다");
        }

        [Test]
        public void 회랑이_탐색_대역을_벗어나지_않는다()
        {
            //  진폭이 너무 크면 바닥·천장이 봇의 탐색 대역(±27.3m) 밖으로 나가 검사가 거짓말한다.
            for (float x = StartX; x <= StartX + Length; x += 2f)
            {
                Assert.Less(System.Math.Abs(CourseElevation.CenterY(x, StartX, Length)), 10f);
            }
        }

        [Test]
        public void 길이가_0이면_평평하다()
        {
            Assert.AreEqual(0f, CourseElevation.CenterY(100f, StartX, 0f), 1e-4f);
        }

        static float Swing(float from, float to)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            for (float x = from; x <= to; x += 1f)
            {
                float v = CourseElevation.CenterY(x, StartX, Length);
                if (v < lo) { lo = v; }
                if (v > hi) { hi = v; }
            }
            return hi - lo;
        }
    }
}
```

- [ ] **Step 3: 돌려서 실패를 본다** — `CourseElevation`이 없어 컴파일 실패.

- [ ] **Step 4: 구현한다**

`Assets/Scripts/MapTools/CourseElevation.cs`:

```csharp
using FlappyRace;

namespace LOP.MapTools
{
    /// <summary>
    /// 회랑 중심을 진행률에 따라 흔든다. 구간 1은 완만하고 구간 3은 뾰족하다.
    ///
    /// <para><b>파장을 고정한 이유</b>: 구간마다 파장을 다르게 하면 <b>위상이 튀어</b> 회랑이
    /// 경계에서 끊긴다(통과 불가능한 자리가 생긴다). 그래서 파장은 한 값으로 두고
    /// <b>진폭과 뾰족함만</b> 진행률을 따라 매끄럽게 바꾼다 — 의도한 감각은 그대로 나온다.</para>
    ///
    /// <para><b>고저차만으로는 다이브가 깊어지지 않는다.</b> 이 파도는 평균 2 m/s 남짓으로
    /// 내려가므로 낙하 시간을 0.2초도 못 늘린다. 깊은 다이브는 <see cref="BranchLayout"/>의
    /// 아래 길이 준다. 이 곡선이 하는 일은 <i>리듬</i>이지 경제가 아니다.</para>
    /// </summary>
    public static class CourseElevation
    {
        public const float Wavelength = 90f;
        public const float AmpStart = 2f;
        public const float AmpEnd = 7f;

        public static float CenterY(float x, float startX, float courseLength)
        {
            if (courseLength <= 0f) { return 0f; }
            float t = CourseSectionRule.Progress(x, startX, courseLength);
            float amp = AmpStart + (AmpEnd - AmpStart) * t;
            return FlappyElevation.Blend(x, amp, startX, Wavelength, sharpness: t);
        }
    }
}
```

- [ ] **Step 5: 통과를 본다** — 5개 PASS.

- [ ] **Step 6: 일부러 깨뜨린다** — `Wavelength`를 `90f - 40f * t`처럼 x에 따라 변하게 만들어 **`어디서도_끊기지_않는다`가 실패하는지** 확인하고 되돌린다.

- [ ] **Step 7: 커밋**

```bash
git add Assets/Scripts/FlappyRaceSlice/Logic/FlappyElevation.cs Assets/Scripts/FlappyRaceSlice/Logic/FlappyElevation.cs.meta \
        Assets/Scripts/MapTools/CourseElevation.cs Assets/Scripts/MapTools/CourseElevation.cs.meta \
        Assets/Tests/EditMode/MapTools/CourseElevationTests.cs Assets/Tests/EditMode/MapTools/CourseElevationTests.cs.meta
git status --short
git commit -m "feat(maptools): 회랑 중심에 높낮이를 준다 — 파장은 고정, 진폭과 뾰족함만 변한다"
```

---

### Task 4: 고저차를 굽는다 — 여기서 한 번 플레이한다

**Files:**
- Modify: `Assets/Scripts/MapTools/ClassicCourse.cs`
- Modify: `Assets/Scripts/Editor/FlappyClassicCourseBuilder.cs`
- Test: `Assets/Tests/EditMode/MapTools/ClassicCourseTests.cs` (기존)

- [ ] **Step 1: 배치 규칙이 중심 오프셋을 받게 한다**

`ClassicCourseRule.Layout`에 `System.Func<float, float> centerAt = null` 인자를 더한다(기본 null = 평평, 기존 테스트 무변경). 창 중심을 `centerAt(x) + 랜덤워크`로 만들고, **회랑 범위도 같이 움직인다**:

```csharp
                float baseline = centerAt != null ? centerAt(x) : 0f;
                float low = floorY + baseline + window * 0.5f;
                float high = ceilingY + baseline - window * 0.5f;
```

- [ ] **Step 2: 기존 테스트가 여전히 초록인지 본다**

`ClassicCourseTests` 14개. `centerAt`을 안 넘기면 동작이 같아야 한다 — 하나라도 깨지면 기본값 경로를 건드린 것이다.

- [ ] **Step 3: 고저차 테스트를 하나 더한다**

```csharp
        [Test]
        public void 중심_오프셋을_주면_창이_따라_올라간다()
        {
            var flat = ClassicCourseRule.Layout(0f, 100f, 11.4f, -7.28f, 7.28f, 4.37f, 6f, 1UL);
            var lifted = ClassicCourseRule.Layout(0f, 100f, 11.4f, -7.28f, 7.28f, 4.37f, 6f, 1UL,
                                                  centerAt: _ => 3f);

            Assert.AreEqual(flat.Count, lifted.Count);
            for (int i = 0; i < flat.Count; i++)
            {
                Assert.AreEqual(flat[i].GapCenter + 3f, lifted[i].GapCenter, 1e-3f);
            }
        }
```

- [ ] **Step 4: 빌더가 바닥·천장·파이프를 고저차에 맞춰 놓는다**

`Build()`에서:

```csharp
            System.Func<float, float> centerAt =
                x => LOP.MapTools.CourseElevation.CenterY(x, StartX, length);
            var pipes = LOP.MapTools.ClassicCourseRule.Layout(
                StartX, length, spacing, floorY, ceilingY, window, MaxGapStep, Seed, centerAt);
```

바닥·천장은 이제 **한 덩어리 슬래브로는 안 된다** — 고저차를 따라가야 하므로 관문 간격마다 조각으로 나눠 놓는다. 조각 하나의 y는 그 구간 중앙의 `centerAt` 값을 쓴다. 조각 길이는 `spacing`(11.4m)로 두고, 이웃 조각과 **겹치게** 만들어(폭 = spacing × 1.2) 틈이 생기지 않게 한다.

> 구간별 재질은 그대로 유지한다 — 조각마다 `SectionMaterial(x, length, fallback)`을 묻는다.

- [ ] **Step 5: 굽고 곧바로 저장한다**

```
unity cmd menu --path "LOP/Debug/Flappy 전통 코스 굽기"
unity cmd save_scene            # ← 반드시 붙여서. 안 하면 다음 리로드에 모달이 뜬다
```

- [ ] **Step 6: 검사한다**

`LOP/Debug/Flappy 맵 검사`. **① 클린런 네 자리가 전부 ✅여야 한다.** 깨지면 진폭이 과하다 — `CourseElevation.AmpEnd`를 낮춰 다시 굽는다(7 → 6 → 5 순으로).

- [ ] **Step 7: 플레이한다**

로컬 리그에서 한 판. 확인:
- 내리막에서 **떨어지는 시간이 길어지는가**
- 구간 3이 구간 1보다 **뾰족한가**
- 바닥·천장 조각 사이에 **틈이 보이지 않는가**

- [ ] **Step 8: 커밋**

```bash
git add Assets/Scripts/MapTools/ClassicCourse.cs Assets/Scripts/Editor/FlappyClassicCourseBuilder.cs \
        Assets/Tests/EditMode/MapTools/ClassicCourseTests.cs
git commit -m "feat(flappy): 회랑에 높낮이를 넣는다"
```

---

### Task 5: 부스트 패드 — 공유 판정

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyBoostPad.cs`
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyBoostPadField.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/FlappyBoostPadFieldTests.cs`

**Interfaces:**
- Produces: `FlappyBoostPadField.Add/Remove(FlappyBoostPad)` · `bool TryDuration(float x, float y, out float duration)`

> **명명**: `Zone`이 아니라 `Pad`다. 클라에 옛 프로토타입 `FlappyBoostZone`(전역 네임스페이스)이 남아 있어 읽는 사람이 헷갈린다. 업계에서도 boost pad / speed pad가 통용어다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

```csharp
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 부스트 패드는 <b>산술 포함 판정</b>만 한다 — 물리 질의를 쓰면 클·서가 갈린다.
    /// 겹친 패드에서 어느 쪽이 이기는지도 못박는다(순서가 결과를 바꾸면 결정론이 깨진다).
    /// </summary>
    public class FlappyBoostPadFieldTests
    {
        static FlappyBoostPadField Field(params (float x0, float x1, float y0, float y1, float dur)[] pads)
        {
            var field = new FlappyBoostPadField();
            foreach (var p in pads)
            {
                field.AddRect(p.x0, p.x1, p.y0, p.y1, p.dur);
            }
            return field;
        }

        [Test]
        public void 안에_있으면_지속시간을_준다()
        {
            var field = Field((10f, 20f, -5f, 5f, 0.6f));
            Assert.IsTrue(field.TryDuration(15f, 0f, out float duration));
            Assert.AreEqual(0.6f, duration, 1e-5f);
        }

        [Test]
        public void 밖이면_주지_않는다()
        {
            var field = Field((10f, 20f, -5f, 5f, 0.6f));
            Assert.IsFalse(field.TryDuration(9.9f, 0f, out _));
            Assert.IsFalse(field.TryDuration(15f, 5.1f, out _));
        }

        [Test]
        public void 경계는_안쪽이다()
        {
            //  틱 경계에서 스칠 때 클·서가 다르게 판정하면 안 된다 — 포함 규칙을 한쪽으로 고정한다.
            var field = Field((10f, 20f, -5f, 5f, 0.6f));
            Assert.IsTrue(field.TryDuration(10f, -5f, out _));
            Assert.IsTrue(field.TryDuration(20f, 5f, out _));
        }

        [Test]
        public void 겹치면_긴_쪽이_이긴다()
        {
            //  등록 순서가 결과를 바꾸면 결정론이 깨진다. 둘 다 넣어 보고 같은 답인지 본다.
            var a = Field((0f, 10f, -5f, 5f, 0.4f), (5f, 15f, -5f, 5f, 0.9f));
            var b = Field((5f, 15f, -5f, 5f, 0.9f), (0f, 10f, -5f, 5f, 0.4f));

            Assert.IsTrue(a.TryDuration(7f, 0f, out float da));
            Assert.IsTrue(b.TryDuration(7f, 0f, out float db));
            Assert.AreEqual(0.9f, da, 1e-5f);
            Assert.AreEqual(da, db, 1e-5f);
        }

        [Test]
        public void 패드가_없으면_조용하다()
        {
            Assert.IsFalse(new FlappyBoostPadField().TryDuration(0f, 0f, out _));
        }
    }
}
```

- [ ] **Step 2: 돌려서 실패를 본다.**

- [ ] **Step 3: 구현한다**

`FlappyBoostPadField.cs` — 사각형 목록 + 포함 판정. `AddRect`는 테스트용 문이고, 씬 마커는 `Add(FlappyBoostPad)`로 들어온다(`FlappyWindmillField`와 같은 모양).

```csharp
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 이 판에 놓인 부스트 패드 전부. 맵 씬의 <see cref="FlappyBoostPad"/> 마커가 스스로 들어온다
    /// (<see cref="FlappyWindmillField"/>와 같은 모양).
    ///
    /// <para><b>물리 질의를 쓰지 않는다.</b> 사각형 포함 여부를 산술로만 계산한다 — 트리거
    /// 콜라이더로 판정하면 클·서가 다른 틱에 다른 답을 내고, 롤백 재생에서는 물리를 안 돌려
    /// 아예 답이 없다.</para>
    ///
    /// <para><b>겹치면 긴 쪽이 이긴다.</b> 등록 순서가 결과를 바꾸면 결정론이 깨진다.</para>
    /// </summary>
    public class FlappyBoostPadField
    {
        private readonly struct Rect
        {
            public readonly float X0, X1, Y0, Y1, Duration;
            public Rect(float x0, float x1, float y0, float y1, float duration)
            {
                X0 = x0; X1 = x1; Y0 = y0; Y1 = y1; Duration = duration;
            }
            public bool Contains(float x, float y) => x >= X0 && x <= X1 && y >= Y0 && y <= Y1;
        }

        private readonly List<Rect> _rects = new List<Rect>();

        public int Count => _rects.Count;

        public void AddRect(float x0, float x1, float y0, float y1, float duration)
        {
            _rects.Add(new Rect(System.Math.Min(x0, x1), System.Math.Max(x0, x1),
                                System.Math.Min(y0, y1), System.Math.Max(y0, y1), duration));
        }

        public void Clear() => _rects.Clear();

        public bool TryDuration(float x, float y, out float duration)
        {
            duration = 0f;
            bool found = false;
            for (int i = 0; i < _rects.Count; i++)
            {
                Rect r = _rects[i];
                if (r.Contains(x, y) && (found == false || r.Duration > duration))
                {
                    duration = r.Duration;
                    found = true;
                }
            }
            return found;
        }
    }
}
```

`FlappyBoostPad.cs` — 씬 마커:

```csharp
using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 맵 씬에 놓는 부스트 패드 표시. 맵이 올라올 때 <see cref="FlappyBoostPadField"/>를 주입받아
    /// 스스로 등록한다.
    ///
    /// <para><see cref="FlappyWindmill"/>과 같은 이유로 <b>공용 패키지</b>에 있다: 맵 씬은 클라에서
    /// 만들고 서버가 읽는데, 스크립트가 한쪽에만 있으면 반대쪽에서 missing script가 되고 그 빈
    /// 컴포넌트가 씬 주입을 끊는다. 이 패드는 특히 그래야 한다 — 부스트가 전진 속도를 바꾸므로
    /// 한쪽만 밟으면 곧장 갈린다.</para>
    ///
    /// <para><b>콜라이더를 쓰지 않는다.</b> 판정은 필드가 사각형 포함 여부를 산술로 한다 —
    /// 트리거로 하면 롤백 재생에서 물리를 안 돌려 아예 답이 없다.</para>
    /// </summary>
    [ExecuteAlways]
    [SceneInjectMonoBehaviour]
    public class FlappyBoostPad : MonoBehaviour
    {
        /// <summary>밟으면 몇 초 동안 대시 상태가 되나.</summary>
        public float Duration = 0.6f;

        private FlappyBoostPadField field;

        [Inject]
        public void Construct(FlappyBoostPadField field)
        {
            this.field = field;
            Register();
        }

        private void OnEnable() => Register();

        private void OnDisable() => field?.Remove(this);

        private void Register()
        {
            if (field == null) { return; }
            Vector3 c = transform.position;
            Vector3 s = transform.lossyScale;
            field.Add(this, c.x - s.x * 0.5f, c.x + s.x * 0.5f,
                            c.y - s.y * 0.5f, c.y + s.y * 0.5f, Duration);
        }

        //  씬에서 눈으로 보이게. 게임에는 영향이 없다.
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.35f);
            Gizmos.DrawCube(transform.position, transform.lossyScale);
        }
    }
}
```

`FlappyBoostPadField`에는 `Add(FlappyBoostPad, x0, x1, y0, y1, duration)`과 `Remove(FlappyBoostPad)`를 더한다 — 마커를 키로 들고 있어야 씬 편집 중 지웠을 때 목록에서 빠진다(`FlappyWindmillField`와 같은 이유).

- [ ] **Step 4: 통과를 본다** — 5개 PASS.

- [ ] **Step 5: 일부러 깨뜨린다** — `r.Duration > duration`을 `found == false`만으로 바꿔 **`겹치면_긴_쪽이_이긴다`가 실패하는지** 확인하고 되돌린다.

- [ ] **Step 6: 커밋 (LOP-Shared)**

---

### Task 6: 부스트 패드 — 월드에 붙인다

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyWorld.cs`
- Modify: 클라 `Assets/Scripts/Game/FlappyRaceLifetimeScope.cs` · 서버의 대응 스코프

- [ ] **Step 1: 월드가 필드를 받는다**

생성자에 `FlappyBoostPadField boostPads`를 더한다(`FlappyWindmillField` 바로 옆).

- [ ] **Step 2: 매 틱 적용한다**

대시 발동(`TryActivate`) **뒤**, 이동 **앞**에 둔다 — 순서는 기존 주석이 설명하는 이유와 같다:

```csharp
                //  부스트 패드는 <b>공짜 대시</b>다. 게이지를 쓰지 않고 남은 시간만 채운다 —
                //  새 가속 상태를 만들면 와이어·예측·롤백·스택 규칙이 전부 새로 생기는데,
                //  대시를 재사용하면 그게 전부 0이고 감각도 같다(2배 수평 직선).
                var position = _birds[i].Get<GameFramework.World.Transform>()?.Position;
                if (position != null && _boostPads.TryDuration(position.Value.X, position.Value.Y,
                                                               out float boost))
                {
                    _dashSystem.Boost(_birds[i], boost);
                }
```

`FlappyDashSystem.Boost(entity, duration)`를 더한다 — `DashRemaining = Math.Max(DashRemaining, duration)`. **게이지는 건드리지 않는다.**

- [ ] **Step 3: 테스트**

`FlappyDashSystemTests`에 더한다: 부스트는 게이지를 안 쓴다 · 더 긴 쪽으로만 갱신된다 · 스턴 중 `Cancel` 뒤에도 다시 받을 수 있다.

- [ ] **Step 4: DI 등록**

클라·서버 스코프에 `builder.Register<FlappyBoostPadField>(Lifetime.Singleton)`. **양쪽 다** 해야 한다 — 한쪽만 하면 그쪽만 부스트가 돈다.

- [ ] **Step 5: 커밋 (Shared + 클 + 서, 각각)**

---

### Task 7: 갈림길 규칙 (순수)

**Files:**
- Create: `Assets/Scripts/MapTools/BranchLayout.cs`
- Test: `Assets/Tests/EditMode/MapTools/BranchLayoutTests.cs`

**Interfaces:**
- Produces: `readonly struct Branch` (xStart, xEnd, entranceX, entranceGapLow/High, tunnelFloorY, tunnelCeilY, padX0, padX1, padY0, padY1, padDuration) · `BranchLayout.Plan(...)` · `BranchLayout.Validate(...)` · `BranchLayout.LowerRouteGainMeters(...)`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Tests/EditMode/MapTools/BranchLayoutTests.cs`:

```csharp
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// 갈림길 배치. <b>지름길이 함정도 정답도 아니어야 한다</b>는 것이 이 테스트의 전부다 —
    /// 입구가 너무 좁으면 아무도 못 들어가고, 이득이 너무 크면 위 길이 의미를 잃는다.
    /// </summary>
    public class BranchLayoutTests
    {
        //  회랑은 화면 세로와 같다(카메라 20m · FOV 40). 창은 물리에서 나온 값.
        const float FloorY = -7.28f;
        const float CeilY = 7.28f;
        const float Window = 4.37f;
        const float Spacing = 11.4f;
        const float ForwardSpeed = 6.8f;

        static Branch Plan(float xStart = 200f)
            => BranchLayout.Plan(xStart, lengthX: 5 * Spacing, floorY: FloorY, ceilingY: CeilY,
                                 window: Window, spacing: Spacing, forwardSpeed: ForwardSpeed,
                                 boostDuration: 0.6f);

        [Test]
        public void 입구는_회랑_바닥보다_아래다()
        {
            //  같은 높이에 두면 다이브가 안 된다 — 회랑(14.56m)만으로는 반 칸도 못 번다.
            Branch b = Plan();
            Assert.Less(b.EntranceGapHigh, FloorY + 0.01f, "입구가 회랑 안에 있으면 지름길이 아니다");
        }

        [Test]
        public void 터널이_한_칸을_줄_만큼_깊다()
        {
            //  천장에서 터널 바닥까지의 총 낙하가 20m는 돼야 한 칸 근처가 나온다.
            Branch b = Plan();
            Assert.GreaterOrEqual(CeilY - b.TunnelFloorY, 20f);
        }

        [Test]
        public void 터널_안에는_관문이_없다()
        {
            //  지름길의 값어치는 "충돌 0"이다. 관문을 넣으면 그냥 다른 길이 된다.
            Branch b = Plan();
            Assert.AreEqual(0, b.TunnelGateCount);
        }

        [Test]
        public void 패드_뒤에_부스트_길이만큼_비어_있다()
        {
            //  부스트 중엔 중력도 날갯짓도 없다(대시와 같다). 출구가 막혀 있으면 피할 수 없이 박는다.
            Branch b = Plan();
            float boostRun = 0.6f * ForwardSpeed * 2f;   // 지속 × 전진 × 배수
            Assert.GreaterOrEqual(b.ClearAfterPad, boostRun, "부스트가 끝나기 전에 벽이 온다");
        }

        [Test]
        public void 아래_길의_이득이_충돌_한_번_언저리다()
        {
            //  너무 크면 위 길이 의미를 잃고, 너무 작으면 아무도 위험을 지지 않는다.
            float gain = BranchLayout.LowerRouteGainMeters(Plan(), ForwardSpeed,
                                                           dashMult: 2f, dashDuration: 0.2f);
            float oneCrash = 0.8f * ForwardSpeed;   // stunTime × 전진 = 5.44m

            Assert.Greater(gain, oneCrash * 0.7f);
            Assert.Less(gain, oneCrash * 1.5f);
        }

        [Test]
        public void 갈림_구간이_너무_짧으면_계획하지_않는다()
        {
            //  들어갔다 나오는 데 최소 몇 관문은 필요하다 — 짧으면 입구와 출구가 붙어 버린다.
            Assert.IsFalse(BranchLayout.TryPlan(200f, lengthX: Spacing, FloorY, CeilY, Window,
                                                Spacing, ForwardSpeed, 0.6f, out _));
        }

        [Test]
        public void 같은_입력이면_같은_배치다()
        {
            Branch a = Plan(200f), b = Plan(200f);
            Assert.AreEqual(a.EntranceX, b.EntranceX, 1e-4f);
            Assert.AreEqual(a.TunnelFloorY, b.TunnelFloorY, 1e-4f);
        }

        [Test]
        public void 어긋난_배치는_말로_돌려준다()
        {
            //  빌더가 씬을 건드리기 <b>전에</b> 멈출 수 있어야 한다(ClassicCourseRule.Validate와 같은 모양).
            var broken = new Branch(xStart: 200f, xEnd: 210f, entranceX: 205f,
                                    entranceGapLow: -20f, entranceGapHigh: -19.9f,
                                    tunnelFloorY: -10f, tunnelCeilY: FloorY,
                                    padX0: 208f, padX1: 209f, padY0: -9f, padY1: -8f,
                                    padDuration: 0.6f, tunnelGateCount: 0, clearAfterPad: 1f);
            Assert.IsNotNull(BranchLayout.Validate(broken, FloorY, ForwardSpeed));
        }
    }
}
```

- [ ] **Step 2: 돌려서 실패를 본다** — `BranchLayout`이 없어 컴파일 실패.

- [ ] **Step 3: 구현한다**

`Assets/Scripts/MapTools/BranchLayout.cs`. 구조:

```csharp
namespace LOP.MapTools
{
    /// <summary>갈림길 하나의 설계도. 씬 타입을 안 들고 와야 순수 계층에서 잴 수 있다.</summary>
    public readonly struct Branch
    {
        public readonly float XStart, XEnd;
        public readonly float EntranceX, EntranceGapLow, EntranceGapHigh;
        public readonly float TunnelFloorY, TunnelCeilY;
        public readonly float PadX0, PadX1, PadY0, PadY1, PadDuration;
        public readonly int TunnelGateCount;
        /// <summary>패드 끝에서 터널 출구까지의 빈 거리. 부스트가 끝나기 전에 벽이 오면 안 된다.</summary>
        public readonly float ClearAfterPad;
        // ... 생성자
    }

    /// <summary>
    /// 손가락 맵 — <b>난이도를 입구 한 곳에 몰고 보상을 셋으로 준다</b>:
    /// ① 들어가는 낙하가 곧 다이브(≈1칸) ② 안은 관문 0(충돌 손실 0) ③ 출구 부스트.
    ///
    /// <para><b>아래 길이 회랑보다 깊어야 한다.</b> 같은 높이에 두면 회랑(14.56m)만으로는
    /// 반 칸도 못 벌어 지름길이 아무것도 주지 못한다.</para>
    /// </summary>
    public static class BranchLayout
    {
        /// <summary>터널 바닥이 회랑 바닥보다 얼마나 아래인가. 총 낙하 20m를 만드는 값.</summary>
        public const float TunnelDepth = 13f;
        /// <summary>입구 구멍의 폭(세로). 창보다 좁게 — 여기가 유일한 난관이다.</summary>
        public const float EntranceGap = 3.2f;

        public static Branch Plan(float xStart, float lengthX, float floorY, float ceilingY,
                                  float window, float spacing, float forwardSpeed, float boostDuration);

        public static bool TryPlan(..., out Branch branch);   // 짧으면 false

        /// <summary>어긋난 곳을 말로 돌려준다. 통과면 null.</summary>
        public static string Validate(Branch branch, float floorY, float forwardSpeed);

        /// <summary>
        /// 아래 길이 위 길보다 몇 미터 앞서나. <b>부스트 이득 + 벌어들인 칸의 대시 이득</b>이다.
        /// 충돌 회피 이득은 넣지 않는다 — 그건 플레이어 실력에 달린 값이라 모델 없이 못 센다.
        /// </summary>
        public static float LowerRouteGainMeters(Branch branch, float forwardSpeed,
                                                 float dashMult, float dashDuration);
    }
}
```

`LowerRouteGainMeters` = `boostDuration × forwardSpeed × (dashMult − 1)` + `벌어들인 칸 × dashDuration × forwardSpeed × (dashMult − 1)`.
벌어들인 칸은 **입구 낙하 높이에서 계산**한다(§3의 세제곱 곡선을 그대로 적분) — 값이 테스트 한 줄로 고정되므로, 나중에 곡선이나 깊이를 바꾸면 여기가 먼저 말해 준다.

- [ ] **Step 4: 통과를 본다** — 8개 PASS.

- [ ] **Step 5: 일부러 깨뜨린다**

`TunnelDepth`를 0으로 바꿔 돌린다. **`터널이_한_칸을_줄_만큼_깊다`·`입구는_회랑_바닥보다_아래다`·`아래_길의_이득이_충돌_한_번_언저리다`가 실패해야 한다.** 되돌린다.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/MapTools/BranchLayout.cs Assets/Scripts/MapTools/BranchLayout.cs.meta         Assets/Tests/EditMode/MapTools/BranchLayoutTests.cs Assets/Tests/EditMode/MapTools/BranchLayoutTests.cs.meta
git status --short
git commit -m "feat(maptools): 손가락 맵 — 난이도를 입구에 몰고 보상을 셋으로"
```

---

### Task 8: 갈림길을 굽고, 두 길을 각각 검증한다

**Files:**
- Modify: `Assets/Scripts/Editor/FlappyClassicCourseBuilder.cs`
- Modify: `Assets/Scripts/Editor/FlappyMapPlayabilityCheck.cs`
- Modify: `Assets/Scripts/MapTools/PlayabilityReport.cs`

- [ ] **Step 1: 빌더가 갈림길을 굽는다**

구간 2·3에 하나씩. 분리대 슬래브 · 위 길 관문(기존 배치) · 바닥의 입구 구멍 · 터널 벽 · 출구 `FlappyBoostPad` 마커.

- [ ] **Step 2: 검사기가 두 길을 각각 증명한다**

기존 클린런 탐색에 **세로 제한**을 더한다(갈림 구간에서 분리대 위/아래로 가둔다). 세 가지를 따로 돌린다:

| 증명 | 실패하면 |
|---|---|
| 위 길 클린런 | 안전한 길이 없다 — 맵이 불가능하다 |
| 아래 길 **진입** | 지름길이 아니라 함정이다 |
| 아래 길 클린런 | 들어가면 죽는다 |

- [ ] **Step 3: 리포트에 절을 더한다**

```
── 🔀 갈림길 ──────────────────────────
  갈림길 2개
  x=250  위 길 ✅ · 아래 길 ✅ (진입 ✅) · 아래 이득 +5.2m (충돌 1회 = 5.4m의 0.96배)
```

- [ ] **Step 4: 굽고 → 저장 → 검사 → 플레이**

- [ ] **Step 5: 커밋**

---

### Task 9: 종합 검증 · 정정 · 배포

- [ ] **Step 1: 카메라 주석을 정정한다**

`PlayabilityReport.cs`의 `const float CameraDistance = 20f; // FlappyCameraFollow.fixedZ = -20` 은 **죽은 파일을 출처로 적고 있다.** 실제 출처로 바꾼다:

```csharp
        //  실제 카메라는 CameraController다. 거리는 SetTarget이 씬의 카메라 위치에서 읽어
        //  maxDistance로 자르며, FlappyRace.unity에서 위치 (0,2,-20) · maxDistance 20이라
        //  결과가 20m다. (FlappyCameraFollow는 옛 프로토타입 전용이고 이 값과 무관하다.)
        const float CameraDistance = 20f;
```

- [ ] **Step 2: EditMode 전체** — 클라와 Shared 양쪽. `--detach`로.

- [ ] **Step 3: 맵 검사 전 절 확인** — 클린런 · 관문 박자 · 시각 정직성 · 층 규약 · 갈림길.

- [ ] **Step 4: 배포 사슬** — Shared → MasterData 양쪽 → Art → 클라 포인터 → `content-deploy`. **순서를 지킨다**(어기면 마커가 missing script로 구워진다).

- [ ] **Step 5: 플레이로 확인한다**

| 확인 | 기대 |
|---|---|
| 게이지 | 평범하게 날면 **거의 안 찬다** |
| 다이브 | 깊게 내려가면 눈에 띄게 찬다 |
| 갈림길 | 아래 길이 **들어갈 만하고**, 들어가면 이득이 느껴진다 |
| 부스트 | 출구에서 밀려 나가고, 그 앞이 비어 있다 |
| 구간 | 1·2·3이 **다르게 느껴진다** |

- [ ] **Step 6: ROADMAP에 적는다** — 무엇을 왜 했고, 열린 결정(터널 깊이·부스트 길이·구간 1 갈림길)의 답이 나왔으면 그것도.
