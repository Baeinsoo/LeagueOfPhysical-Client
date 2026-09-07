# 스카이다이브 착지 충격 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 아래로 멈춘 순간의 낙하 속도가 문턱을 넘으면 죽고, 완주는 "결승선 아래에서 살아서 접지"가 되게 한다.

**Architecture:** 충돌 직전 속도는 이동이 끝나면 사라지므로, 공유 이동 단계가 얇은 컴포넌트(`LandingImpact`)에 그 값을 남기고 두 소비자가 읽는다 — 공유 월드는 *완주를 줄지*, 서버는 *되돌릴지*. 치명 여부 판정은 순수 함수 한 벌(`SkydiveLanding`)을 양쪽이 공유해 클·서가 갈리지 않는다.

**Tech Stack:** Unity 6000.3.16f1, C#, VContainer, Luban 마스터데이터, NUnit EditMode

**Spec:** `docs/superpowers/specs/2026-09-07-skydive-landing-impact-design.md`

## Global Constraints

- **브랜치**: 네 레포 모두 `feature/skydive-landing-impact`. 클라는 이미 `3da8b15`(스펙)에 있다.
- **문턱 기본값 15 m/s** — `JumpPower = 11`보다 커야 한다(스펙 §3). 이 관계를 Task 2가 테스트로 못 박는다.
- **`FallBrake`(150)·`FallApproach`(29)·`JumpPower`(11)·자세별 낙하 속도(60/90/6)를 바꾸지 않는다**(스펙 §4, §9).
- **죽음 판정은 서버만.** 공유 코드는 "치명인가"를 계산할 뿐 되돌리지 않는다(스펙 §7).
- **`LandingImpact`를 `SkydiveSavedState`에 넣지 않는다** — 매 틱 다시 계산되므로 되감기 재생이 같은 값을 낸다(스펙 §7.1).
- **`git add -A` / `git commit -a` 금지.** 바꾼 파일만 경로로 지정하고 커밋 전 `git diff --cached --name-only`로 확인한다. 네 레포 모두 의도적으로 커밋하지 않는 로컬 픽스처가 있다.
- **Play 모드 금지. 코스 굽기 금지** (다시 구우면 `SkydiveWind*.mat` 6개가 망가진다 — 알려진 미해결 버그).
- 유니티 에디터가 떠 있다: 클라 7800 / 서버 7801. `unity cmd <명령> --project-path "<레포 경로>"`.
  `recompile_status`가 `up_to_date`면 재컴파일을 **안 한 것**이고, `failed:false`만으로는 증거가 안 된다 — 콘솔도 함께 본다.

---

## 파일 구조

| 파일 | 책임 | Task |
|---|---|---|
| `infrastructure/table/Datas/#SkydiveConfig.xlsx` | 문턱 값의 진실원본 | 1 |
| `LOP-MasterData-{Client,Server}/Runtime.Generated/.../SkydiveConfig.cs` + `.bytes` | Luban 생성물 | 1 |
| `LOP-Shared/Runtime/Scripts/Game/SkydiveConfig.cs` | 시뮬이 읽는 설정 구조체 | 1 |
| `LOP-{Client,Server}/Assets/Scripts/Game/SkydiveConfigProvider.cs` | 마스터데이터 → 설정 | 1 |
| `LOP-Shared/Runtime/Scripts/Game/LandingImpact.cs` | **이 틱의 착지 충격 속도** (데이터만) | 2 |
| `LOP-Shared/Runtime/Scripts/Game/SkydiveLanding.cs` | **치명 여부 판정** (순수 함수) | 2 |
| `LOP-Shared/Runtime/Scripts/Game/SkydiveWorld.cs` | 충격 기록(이동) + 완주 게이트(Detection) | 3, 4 |
| `LOP-Server/Assets/Scripts/Game/TickSystems/SkydiveLandingSystem.cs` | 치명 착지를 체크포인트로 되돌린다 | 5 |
| `LOP-Server/Assets/Scripts/Game/SkydiveLifetimeScope.cs` | 시스템 등록(결승 감시보다 먼저) | 5 |
| `LOP-Server/Assets/Scripts/Entity/SkydivePlayerCreator.cs` | 다이버에 `LandingImpact` 부착 | 2 |

---

### Task 1: 마스터데이터에 `landing_lethal_speed` 열을 더한다

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/infrastructure/table/Datas/#SkydiveConfig.xlsx`
- Generated (커밋 대상): `LeagueOfPhysical-MasterData-Client/Runtime.Generated/Scripts/MasterData/SkydiveConfig.cs` + `.bytes`, 서버 패키지의 대응 파일
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveConfig.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/SkydiveConfigProvider.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/SkydiveConfigProvider.cs`

**Interfaces:**
- Produces: `LOP.MasterData.SkydiveConfig.LandingLethalSpeed` (float, 생성물) 과 `LOP.SkydiveConfig.LandingLethalSpeed` (float, 시뮬용). Task 2·3·5가 후자를 읽는다.

- [ ] **Step 1: 엑셀에 열을 더한다**

Luban Excel-embedded 형식은 헤더가 네 줄(`##var` / `##type` / `##group` / `##`)이고 5행부터 데이터다.
28열까지 차 있으므로 29열에 넣는다. `##group`을 비우면 클·서 양쪽에 생성된다.

파이썬 스크립트를 임시 파일로 저장해 실행한다(셸 heredoc 중첩을 피한다).

```python
# add_column.py
import openpyxl
wb = openpyxl.load_workbook("#SkydiveConfig.xlsx")
ws = wb["Sheet"]
assert ws.cell(1, 1).value == "##var", "헤더 모양이 달라졌다 — 멈춰라"
assert ws.cell(5, 2).value == 1, "데이터 행 위치가 달라졌다 — 멈춰라"
col = ws.max_column + 1
ws.cell(1, col, "landing_lethal_speed")
ws.cell(2, col, "float")
ws.cell(4, col, "landing_lethal_speed")
ws.cell(5, col, 15)
wb.save("#SkydiveConfig.xlsx")
print("추가한 열:", col)
```

- [ ] **Step 2: 생성기를 돌린다**

```bash
cd "C:/Users/re5na/workspace/LOP/infrastructure/table"
cmd.exe /c gen.bat
```

기대: 오류 없이 끝나고 네 곳이 갱신된다(클·서 각각 `.cs` + `.bytes`).

- [ ] **Step 3: 생성물에 필드가 생겼는지 확인한다**

```bash
grep -n "LandingLethalSpeed" \
  "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client/Runtime.Generated/Scripts/MasterData/SkydiveConfig.cs" \
  "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server/Runtime.Generated/Scripts/MasterData/SkydiveConfig.cs"
```

기대: 양쪽에 `public readonly float LandingLethalSpeed;`가 있다.
**한쪽에만 있으면 멈추고 보고하라** — `##group`을 잘못 넣은 것이다.

- [ ] **Step 4: 시뮬 설정 구조체에 필드를 더한다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveConfig.cs` — `DiveWindLag` 필드 아래에 넣고,
생성자 매개변수는 **맨 끝에** 더한다(가운데 끼우면 호출부가 조용히 밀린다).

```csharp
        /// <summary>이 속도보다 빠르게 아래로 부딪히면 죽는다. <c>JumpPower</c>보다 커야 한다 —
        /// 작으면 점프 착지가 죽는다.</summary>
        public readonly float LandingLethalSpeed;
```

생성자 끝:

```csharp
                                float glideWindLag, float spreadWindLag, float diveWindLag,
                                float landingLethalSpeed)
        {
            // ... 기존 대입들 ...
            DiveWindLag = diveWindLag;
            LandingLethalSpeed = landingLethalSpeed;
        }
```

- [ ] **Step 5: 양쪽 provider가 새 값을 넘기게 한다**

클라·서버 두 `SkydiveConfigProvider.cs` 모두 마지막 인자를 더한다.

```csharp
                r.GlideWindLag, r.SpreadWindLag, r.DiveWindLag,
                r.LandingLethalSpeed);
```

- [ ] **Step 6: 양쪽 컴파일 확인**

```bash
unity cmd recompile --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
unity cmd recompile --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
```

각각 `recompile_status`가 `completed / failed:false`가 될 때까지 폴링하고, `get_console_logs`로 에러 0을 확인한다.

- [ ] **Step 7: 전체 테스트가 그대로인지 확인**

기준선: **클라 1148, 서버 941.** 설정 필드만 늘었으므로 숫자가 그대로여야 한다.

- [ ] **Step 8: 커밋 (레포 여섯 곳)**

각 레포에서 `git diff --cached --name-only`로 **의도한 파일만** 담겼는지 확인한 뒤 커밋한다.

```bash
cd "C:/Users/re5na/workspace/LOP/infrastructure"                    # table/Datas/#SkydiveConfig.xlsx
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client" # Runtime.Generated 아래 생성물
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server" # 〃
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared"            # Runtime/Scripts/Game/SkydiveConfig.cs
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"            # Assets/Scripts/Game/SkydiveConfigProvider.cs
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"            # Assets/Scripts/Game/SkydiveConfigProvider.cs
```

메시지 예: `feat(skydive): 착지 치명 속도를 마스터데이터에 더한다`

---

### Task 2: `LandingImpact` 컴포넌트와 치명 판정

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/LandingImpact.cs`
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveLanding.cs`
- Create: `LeagueOfPhysical-Shared/Tests/EditMode/SkydiveLandingTests.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Entity/SkydivePlayerCreator.cs`

**Interfaces:**
- Consumes: `LOP.SkydiveConfig.LandingLethalSpeed` (Task 1)
- Produces:
  - `LOP.LandingImpact` — `public float DownwardSpeed;` (아래로 갈 때 양수, 착지 틱이 아니면 0)
  - `static bool LOP.SkydiveLanding.IsLethal(float downwardSpeed, in SkydiveConfig config)`
  - `static bool LOP.SkydiveLanding.IsLethal(GameFramework.World.Entity diver, in SkydiveConfig config)` — 컴포넌트가 없으면 false

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/SkydiveLandingTests.cs`

```csharp
using NUnit.Framework;

public class SkydiveLandingTests
{
    private const float JumpPower = 11f;
    private const float Lethal = 15f;

    //  생성자 인자가 길어 읽기 어려우므로, 이 테스트가 실제로 쓰는 값만 의미 있게 채운다.
    //  나머지는 실제 표의 값을 그대로 옮겨 두어 "이상한 조합에서만 맞는 테스트"가 되지 않게 한다.
    private static LOP.SkydiveConfig Config(float lethal = Lethal, float jump = JumpPower)
    {
        return new LOP.SkydiveConfig(
            spreadFallSpeed: 60f, diveFallSpeed: 90f, glideFallSpeed: 6f,
            spreadMoveSpeed: 12f, diveMoveSpeed: 9f, glideMoveSpeed: 14f,
            spreadTurnAccel: 22f, diveTurnAccel: 6f, glideTurnAccel: 18f,
            fallApproach: 29f, postureRate: 4f,
            bodyRadius: 0.4f, bodyHeight: 1.8f, groundY: 0f,
            staminaMax: 300f, glideDrain: 20f, groundRecover: 40f, emergencyGlideTime: 1f,
            groundMoveSpeed: 4f, groundAccel: 100f, jumpPower: jump, poseClearance: 5f,
            fallBrake: 150f,
            glideWindLag: 0.2f, spreadWindLag: 2.06f, diveWindLag: 3.1f,
            landingLethalSpeed: lethal);
    }

    [Test]
    public void 활공_하강_속도로_닿으면_안_죽는다()
    {
        Assert.IsFalse(LOP.SkydiveLanding.IsLethal(6f, Config()));
    }

    /// <summary>
    /// 하드 제약이다 — 점프해서 착지하는 것이 죽으면 게임이 성립하지 않는다.
    /// 문턱과 JumpPower 중 어느 쪽을 움직여도 이 테스트가 그 사실을 알려 준다.
    /// </summary>
    [Test]
    public void 점프_착지는_절대_안_죽는다()
    {
        Assert.IsFalse(LOP.SkydiveLanding.IsLethal(JumpPower, Config()),
                       "점프 착지 속도가 문턱을 넘었다 — 문턱과 JumpPower를 같이 봐야 한다");
    }

    [Test]
    public void 대자와_다이브_낙하_속도로_닿으면_죽는다()
    {
        Assert.IsTrue(LOP.SkydiveLanding.IsLethal(60f, Config()));
        Assert.IsTrue(LOP.SkydiveLanding.IsLethal(90f, Config()));
    }

    /// <summary>문턱이 실제로 그 자리에 있는지. 0.1 차이로 갈려야 한다.</summary>
    [Test]
    public void 문턱_바로_아래위가_갈린다()
    {
        Assert.IsFalse(LOP.SkydiveLanding.IsLethal(Lethal - 0.1f, Config()));
        Assert.IsTrue(LOP.SkydiveLanding.IsLethal(Lethal + 0.1f, Config()));
    }

    /// <summary>위로 가는 속도(점프 직후)는 착지가 아니다.</summary>
    [Test]
    public void 위로_가는_속도는_치명이_아니다()
    {
        Assert.IsFalse(LOP.SkydiveLanding.IsLethal(-60f, Config()));
    }

    [Test]
    public void 컴포넌트가_없으면_치명이_아니다()
    {
        var entity = new GameFramework.World.Entity("diver");
        Assert.IsFalse(LOP.SkydiveLanding.IsLethal(entity, Config()));
    }

    [Test]
    public void 컴포넌트의_값으로_판정한다()
    {
        var entity = new GameFramework.World.Entity("diver");
        entity.Add(new LOP.LandingImpact { DownwardSpeed = 60f });
        Assert.IsTrue(LOP.SkydiveLanding.IsLethal(entity, Config()));

        entity.Get<LOP.LandingImpact>().DownwardSpeed = 6f;
        Assert.IsFalse(LOP.SkydiveLanding.IsLethal(entity, Config()));
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

서버 에디터에서 EditMode 실행. 기대: 컴파일 에러(`LandingImpact` / `SkydiveLanding` 없음).

- [ ] **Step 3: 컴포넌트를 쓴다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/LandingImpact.cs`

```csharp
namespace LOP
{
    /// <summary>
    /// 이 틱에 아래로 멈추면서 받은 충격 속도. 착지한 틱에만 값이 있고 나머지 틱은 0이다.
    ///
    /// <para>왜 컴포넌트가 필요한가: 충돌 직전 속도는 <b>이동이 끝나면 사라진다</b>(지면에 막혀
    /// 0이 된다). 문 크러시는 틱과 위치만으로 다시 계산할 수 있어 나를 것이 없었지만, 착지는
    /// 그럴 수 없다.</para>
    ///
    /// <para><see cref="GameFramework.World.GroundState"/>와 같은 성질이라 스냅샷·저장 상태에
    /// 넣지 않는다 — 매 틱 이동이 다시 계산하므로 되감기 재생이 같은 값을 낸다.</para>
    /// </summary>
    public class LandingImpact : GameFramework.World.Component
    {
        /// <summary>아래로 갈 때 양수.</summary>
        public float DownwardSpeed;
    }
}
```

- [ ] **Step 4: 판정 함수를 쓴다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveLanding.cs`

```csharp
namespace LOP
{
    /// <summary>
    /// 착지가 치명적인지. <b>공유 구체 코드</b>라 클·서가 같은 답을 낸다 — 죽음을 되돌리는 것은
    /// 서버만 하지만, "치명인가"는 완주를 줄지 정할 때 클라도 물어야 한다.
    /// </summary>
    public static class SkydiveLanding
    {
        public static bool IsLethal(float downwardSpeed, in SkydiveConfig config)
        {
            return downwardSpeed > config.LandingLethalSpeed;
        }

        public static bool IsLethal(GameFramework.World.Entity diver, in SkydiveConfig config)
        {
            var impact = diver?.Get<LandingImpact>();
            //  착지한 틱이 아니면 0이라 자연히 거짓이다 — "착지했나"를 따로 묻지 않는다.
            return impact != null && IsLethal(impact.DownwardSpeed, config);
        }
    }
}
```

- [ ] **Step 5: 다이버에 컴포넌트를 붙인다**

`LeagueOfPhysical-Server/Assets/Scripts/Entity/SkydivePlayerCreator.cs` — `new GroundState()` 줄 바로 뒤:

```csharp
            worldEntity.Add(new LandingImpact());
```

- [ ] **Step 6: 통과 확인**

서버 EditMode 전체 실행. 신규 7개가 통과하고 나머지가 그대로여야 한다.

- [ ] **Step 7: 절제 확인**

`SkydiveLanding.IsLethal(float, in SkydiveConfig)`의 본문을 잠깐 `return false;`로 바꾸고 전체를 다시 돌린다.
기대 빨강: `대자와_다이브_낙하_속도로_닿으면_죽는다`, `문턱_바로_아래위가_갈린다`, `컴포넌트의_값으로_판정한다`.
되돌리고 전부 초록으로 복귀하는 것까지 확인한다.

- [ ] **Step 8: 커밋**

Shared(새 파일 2 + `.meta` 2 + 테스트 1 + `.meta` 1)와 Server(creator 1)를 각 레포에서 따로 커밋한다.

---

### Task 3: 이동이 착지 충격을 기록한다

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveWorld.cs` (`MoveBlockedByMap`)
- Modify: `LeagueOfPhysical-Shared/Tests/EditMode/SkydiveWorldTests.cs`

**Interfaces:**
- Consumes: `LOP.LandingImpact` (Task 2)
- Produces: 매 틱 `LandingImpact.DownwardSpeed`가 채워진다 — 접지가 거짓→참으로 뒤집힌 틱에만 이동 직전의 하강 속도, 그 밖엔 0.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`SkydiveWorldTests.cs`에 더한다. 이 파일이 이미 쓰는 `Diver(...)` / `World(registry, map)` /
`HalfSpaceQuery.AddGround(0f)` 방식을 그대로 쓴다.

**먼저 두 헬퍼를 고친다** (Task 1이 생성자를 늘렸으므로 `Config()`는 어차피 손봐야 한다):

```csharp
        // Config() 마지막 인자에 더한다
                glideWindLag: 0.2f, spreadWindLag: 2.06f, diveWindLag: 3.1f,
                landingLethalSpeed: 15f);
```

```csharp
        // Diver(...) 안, GroundState 바로 뒤에 더한다
            e.Add(new LandingImpact());   // 이동이 매 틱 착지 충격을 여기 적는다
```

```csharp
    static float ImpactOf(EntityRegistry r, string id)
        => r.Get(id).Get<LandingImpact>().DownwardSpeed;

    /// <summary>
    /// 대자로 떨어지다 바닥에 닿는 틱에만 충격이 남는다. 다음 틱에도 남아 있으면 서 있는 동안
    /// 매 틱 죽으므로, 둘째 단언이 이 테스트의 핵심이다.
    /// </summary>
    [Test]
    public void 접지로_뒤집힌_틱에만_충격_속도가_남는다()
    {
        var registry = new EntityRegistry();
        var diver = Diver("a");
        //  한 틱(0.02초)에 대자 속도로 1.2m를 가므로, 2m 위에서 시작하면 두 틱째에 닿는다.
        diver.Get<GameFramework.World.Transform>().Position = new Vector3(0f, 2f, 0f).ToNumerics();
        diver.Get<Velocity>().Linear = new Vector3(0f, -Config().SpreadFallSpeed, 0f).ToNumerics();
        registry.Add(diver);

        var map = new HalfSpaceQuery();
        map.AddGround(0f);
        var world = World(registry, map);
        world.GameplayStartTick = 0;

        //  닿을 때까지 돌린다. 닿은 그 틱의 값을 잡아 둔다.
        float atLanding = 0f;
        int landedAt = -1;
        for (int t = 0; t < 10 && landedAt < 0; t++)
        {
            world.Tick(t, 0.02f);
            if (diver.Get<GroundState>().IsGrounded)
            {
                landedAt = t;
                atLanding = ImpactOf(registry, "a");
            }
        }

        Assert.GreaterOrEqual(landedAt, 0, "열 틱 안에 닿지 않았다 — 이 테스트가 아무것도 못 쟀다");
        Assert.That(atLanding, Is.EqualTo(Config().SpreadFallSpeed).Within(1f),
                    "닿은 틱의 충격이 대자 낙하 속도와 달랐다");

        world.Tick(landedAt + 1, 0.02f);
        Assert.IsTrue(diver.Get<GroundState>().IsGrounded, "여전히 서 있어야 한다");
        Assert.That(ImpactOf(registry, "a"), Is.EqualTo(0f).Within(Tolerance),
                    "서 있는 동안에도 값이 남으면 매 틱 죽는다");
    }

    /// <summary>공중을 떨어지는 동안에는 0이다 — "빠르다"가 아니라 "부딪혔다"를 재기 때문.</summary>
    [Test]
    public void 공중에서는_아무리_빨라도_0이다()
    {
        var registry = new EntityRegistry();
        var diver = Diver("a");
        diver.Get<Velocity>().Linear = new Vector3(0f, -Config().DiveFallSpeed, 0f).ToNumerics();
        registry.Add(diver);

        var world = World(registry);   // 면이 없는 하늘
        world.GameplayStartTick = 0;

        for (int t = 0; t < 5; t++)
        {
            world.Tick(t, 0.02f);
            Assert.IsFalse(diver.Get<GroundState>().IsGrounded);
            Assert.That(ImpactOf(registry, "a"), Is.EqualTo(0f).Within(Tolerance), $"t={t}");
        }
    }

    /// <summary>
    /// 되감기 재생이 라이브와 같은 값을 낸다 — LandingImpact를 저장 상태에 안 넣기로 한 선택의 근거다.
    /// (스펙 §7.1) 넣지 않아도 매 틱 이동이 다시 계산하므로 같은 답이 나와야 한다.
    /// </summary>
    [Test]
    public void 되감아_다시_돌려도_같은_충격이_나온다()
    {
        var registry = new EntityRegistry();
        var diver = Diver("a");
        diver.Get<GameFramework.World.Transform>().Position = new Vector3(0f, 2f, 0f).ToNumerics();
        diver.Get<Velocity>().Linear = new Vector3(0f, -Config().SpreadFallSpeed, 0f).ToNumerics();
        registry.Add(diver);

        var map = new HalfSpaceQuery();
        map.AddGround(0f);
        var world = World(registry, map);
        world.GameplayStartTick = 0;

        world.Tick(0, 0.02f);
        world.SaveState(0);
        world.Tick(1, 0.02f);
        float live = ImpactOf(registry, "a");

        world.LoadState(0);
        world.Tick(1, 0.02f);
        Assert.That(ImpactOf(registry, "a"), Is.EqualTo(live).Within(Tolerance),
                    "재생이 라이브와 다른 충격을 냈다 — 저장 상태에서 빠뜨린 것이 있다");
    }
```

> **구현자에게**: `되감아_다시_돌려도_같은_충격이_나온다`가 "둘 다 0"으로 통과하면 아무것도 못 잰
> 것이다. `live`가 0이 아님을 먼저 단언하고 싶으면 더해도 좋다 — 다만 **닿는 틱을 1로 맞추는 것**이
> 위 `접지로_뒤집힌_틱에만_…`과 같은 배치라 보통 그대로 맞는다.

- [ ] **Step 2: 실패를 확인한다**

- [ ] **Step 3: 기록을 넣는다**

`SkydiveWorld.MoveBlockedByMap` — `groundState` 선언을 **`Move` 호출 위로 옮기고**, 이동 직전 값을 읽어 둔다.

```csharp
            var groundState = entity.Get<GameFramework.World.GroundState>();
            //  이동이 속도를 지우기 전에 읽어 둔다. 아래로 갈 때 양수가 되게 부호를 뒤집는다.
            float downwardBeforeMove = -velocity.Linear.Y;
            bool wasGrounded = groundState != null && groundState.IsGrounded;

            //  떨어지는 몸은 턱을 오를 일이 없다. 0을 주면 막혔을 때의 추가 sweep 3발도 안 쏜다.
            var result = KinematicMover.Move(new KinematicMoveInput(
                transform.Position.ToUnity(), velocity.Linear.ToUnity(),
                _config.BodyRadius, _config.BodyHeight, deltaTime,
                _layerMask, stepOffset: 0f), _collisionQuery);

            transform.Position = result.position.ToNumerics();
            // 막힌 축의 속도도 같이 지운다 — 안 지우면 다음 틱 수렴이 "막힌 적 없다는 듯"
            // 옛 속도 위에 계속 쌓인다(KinematicMoveSystem과 같은 관례).
            velocity.Linear = result.velocity.ToNumerics();

            if (groundState != null)
            {
                groundState.IsGrounded = result.grounded;
            }

            var impact = entity.Get<LandingImpact>();
            if (impact != null)
            {
                //  "닿은 순간"만 남긴다. 서 있는 동안 값이 남아 있으면 다음 틱에 또 죽는다.
                impact.DownwardSpeed = (wasGrounded == false && result.grounded && downwardBeforeMove > 0f)
                    ? downwardBeforeMove
                    : 0f;
            }
```

- [ ] **Step 4: 통과 확인** — Shared EditMode 전체.

- [ ] **Step 5: 절제 확인**

`wasGrounded == false &&` 를 지우고 돌린다.
기대: `접지로_뒤집힌_틱에만_충격_속도가_남는다`의 단언 ②가 빨강. 되돌리고 복귀 확인.

- [ ] **Step 6: 커밋**

---

### Task 4: 치명 착지면 완주를 주지 않는다

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveWorld.cs` (`Detection`)
- Modify: `LeagueOfPhysical-Shared/Tests/EditMode/SkydiveWorldTests.cs`

**Interfaces:**
- Consumes: `LOP.SkydiveLanding.IsLethal(Entity, in SkydiveConfig)` (Task 2), `LandingImpact` (Task 3)
- Produces: 완주는 **접지 + 비치명**일 때만 성립한다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

```csharp
    /// <summary>
    /// 스펙 §2.0. 결승선은 몸 바운드가 선을 지나면 성립하고 착지는 발이 멈춰야 성립하는데,
    /// 대자 60m/s면 한 틱에 1.2m를 가고 몸은 1.8m라 <b>선을 먼저 넘고 한두 틱 뒤에 부딪히는</b>
    /// 순간이 실재한다. 순서만으로는 같은 틱의 죽음밖에 못 막으므로 완주 조건에 접지를 넣는다.
    /// </summary>
기존 `World(...)` 헬퍼는 결승선을 **일부러 등록하지 않는다**(주석이 그렇게 적혀 있다).
이 테스트들은 결승선이 필요하므로 **헬퍼에 선택 인자를 더한다**:

```csharp
        static SkydiveWorld World(EntityRegistry registry,
                                  GameFramework.Physics.ICollisionQuery query = null,
                                  WindField wind = null,
                                  DoorField doors = null,
                                  FinishLineBounds finish = null)
            => new SkydiveWorld(registry, new WorldEventBuffer(),
                                new SkydiveMoveSystem(), new StaminaSystem(),
                                new WindDriftSystem(),
                                //  결승선을 안 주면 아무도 통과하지 않는다 — 대부분의 테스트가 그걸 원한다.
                                new FinishSystem(finish ?? new FinishLineBounds(FinishAxis.Y),
                                                 FinishAxis.Y, increasing: false),
                                wind ?? new WindField(), doors ?? new DoorField(), Config(),
                                query ?? new HalfSpaceQuery(),
                                new FlappyWorldFixture.NoopMotionBridge(), layerMask: ~0);

        //  실제 맵은 마커가 y=1.0, 바닥이 y=0이라 서 있으면 Past ≈ 1.48로 통과가 성립한다.
        //  테스트도 같은 모양으로 둔다 — 선을 너무 낮게 놓으면 세 테스트가 전부 "완주 아님"으로
        //  초록이 되어 아무것도 못 잰다.
        static FinishLineBounds GroundFinishLine()
        {
            var line = new FinishLineBounds(FinishAxis.Y);
            line.Register(new Bounds(new Vector3(0f, 1f, 0f), new Vector3(200f, 1f, 200f)));
            return line;
        }

        static bool Finished(EntityRegistry r, string id)
            => r.Get(id).Get<FinishState>()?.Finished ?? false;
```

> **구현자에게**: `FinishState`의 실제 필드 이름을 열어 확인하고 `Finished(...)`를 그에 맞춘다.
> (`FinishedTick`이 `SkydiveSavedState`에 있으므로 그 근처에 있다.)

```csharp
    /// <summary>
    /// 스펙 §2.0. 결승선은 몸 바운드가 선을 지나면 성립하고 착지는 발이 멈춰야 성립하는데,
    /// 대자 60m/s면 한 틱에 1.2m를 가고 몸은 1.8m라 <b>선을 먼저 넘고 한두 틱 뒤에 부딪히는</b>
    /// 순간이 실재한다. 순서만으로는 같은 틱의 죽음밖에 못 막으므로 완주 조건에 접지를 넣는다.
    /// </summary>
    [Test]
    public void 선을_넘어도_접지_전에는_완주가_아니다()
    {
        var registry = new EntityRegistry();
        var diver = Diver("a");
        //  선(윗면 y=1.5) 아래로 이미 들어와 있지만 아직 바닥(y=0)에 닿지는 않은 자리.
        diver.Get<GameFramework.World.Transform>().Position = new Vector3(0f, 1.2f, 0f).ToNumerics();
        diver.Get<Velocity>().Linear = new Vector3(0f, -Config().SpreadFallSpeed, 0f).ToNumerics();
        registry.Add(diver);

        //  바닥을 훨씬 아래에 둬서 이 틱엔 접지가 안 되게 한다.
        var map = new HalfSpaceQuery();
        map.AddGround(-100f);
        var world = World(registry, map, finish: GroundFinishLine());
        world.GameplayStartTick = 0;

        world.Tick(0, 0.02f);

        Assert.IsFalse(diver.Get<GroundState>().IsGrounded, "이 테스트는 공중 상태를 재야 한다");
        Assert.IsFalse(Finished(registry, "a"), "접지 전인데 완주로 잡혔다");
    }

    [Test]
    public void 치명_속도로_접지하면_완주가_아니다()
    {
        var registry = new EntityRegistry();
        var diver = Diver("a");
        diver.Get<GameFramework.World.Transform>().Position = new Vector3(0f, 2f, 0f).ToNumerics();
        diver.Get<Velocity>().Linear = new Vector3(0f, -Config().SpreadFallSpeed, 0f).ToNumerics();
        registry.Add(diver);

        var map = new HalfSpaceQuery();
        map.AddGround(0f);
        var world = World(registry, map, finish: GroundFinishLine());
        world.GameplayStartTick = 0;

        for (int t = 0; t < 10; t++) { world.Tick(t, 0.02f); }

        Assert.IsTrue(diver.Get<GroundState>().IsGrounded, "이 테스트는 접지한 상태를 재야 한다");
        Assert.IsFalse(Finished(registry, "a"), "치명 착지인데 완주로 잡혔다");
    }

    /// <summary>
    /// 방지턱이다 — 결승선을 잘못 놓으면 위 두 테스트가 "완주 아님"으로 둘 다 초록이 되어
    /// 아무것도 못 잰다.
    /// </summary>
    [Test]
    public void 안전_속도로_접지하면_완주다()
    {
        var registry = new EntityRegistry();
        var diver = Diver("a");
        //  바닥 바로 위에서 활공 속도로 내려온다 — 충격이 문턱 아래다.
        diver.Get<GameFramework.World.Transform>().Position = new Vector3(0f, 0.3f, 0f).ToNumerics();
        diver.Get<Velocity>().Linear = new Vector3(0f, -Config().GlideFallSpeed, 0f).ToNumerics();
        diver.Get<Posture>().Gliding = true;
        registry.Add(diver);

        var map = new HalfSpaceQuery();
        map.AddGround(0f);
        var world = World(registry, map, finish: GroundFinishLine());
        world.GameplayStartTick = 0;

        for (int t = 0; t < 10; t++) { world.Tick(t, 0.02f); }

        Assert.IsTrue(diver.Get<GroundState>().IsGrounded);
        Assert.IsTrue(Finished(registry, "a"), "안전하게 내려섰는데 완주가 안 됐다");
    }
```

- [ ] **Step 2: 실패를 확인한다**

- [ ] **Step 3: 게이트를 넣는다**

```csharp
        protected override void Detection(long tick, float deltaTime)
        {
            for (int i = 0; i < _divers.Count; i++)
            {
                GameFramework.World.Entity diver = _divers[i];

                //  완주는 "선을 넘는 것"이 아니라 "선 아래에서 살아서 접지"다(스펙 §2.0).
                //  선을 먼저 넘고 한두 틱 뒤에 부딪히는 순간이 있어, 순서만으로는 완주한 뒤에
                //  죽는 것을 못 막는다.
                bool grounded = diver.Get<GameFramework.World.GroundState>()?.IsGrounded ?? false;
                if (grounded == false || SkydiveLanding.IsLethal(diver, _config))
                {
                    continue;
                }

                _finishSystem.Tick(diver, tick);
            }
        }
```

- [ ] **Step 4: 통과 확인**

- [ ] **Step 5: 절제 확인 — 두 조건을 따로 끈다**

1. `grounded == false ||` 를 지운다 → `선을_넘어도_접지_전에는_완주가_아니다`만 빨강
2. `|| SkydiveLanding.IsLethal(diver, _config)` 를 지운다 → `치명_속도로_접지하면_완주가_아니다`만 빨강

각각 되돌리고 복귀를 확인한다. **한 번에 둘을 끄지 말 것** — 그러면 어느 조건이 무엇을 재는지 모른다.

- [ ] **Step 6: 등수 의미 변화를 확인한다 (읽기만)**

`LeagueOfPhysical-Server/Assets/Scripts/Domain/FinishPlacements.cs`와 `FinishTrackingSystem`을 읽고,
완주 시각이 "선을 넘은 틱"에서 "접지한 틱"으로 바뀌어도 등수 산출이 깨지지 않는지 확인한다.
**코드를 고치지는 말고** 발견한 것을 보고서에 적는다(스펙 §10-3).

- [ ] **Step 7: 커밋**

---

### Task 5: 서버가 치명 착지를 되돌린다

**Files:**
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/SkydiveLandingSystem.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/SkydiveLifetimeScope.cs`
- Create: `LeagueOfPhysical-Server/Assets/Tests/Editor/SkydiveLandingSystemTests.cs`

**Interfaces:**
- Consumes: `LOP.SkydiveLanding.IsLethal(Entity, in SkydiveConfig)` (Task 2), `LOP.SkydiveRespawn.To(...)`, `LOP.SkydiveCheckpoints.LastPassedShelfY(...)`
- Produces: 없음(마지막 소비자)

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`SkydiveDoorSystemTests.cs`를 본떠 만든다 — **같은 조립·같은 단언 방식**을 쓴다.

```csharp
```csharp
    //  SkydiveDoorSystemTests의 조립을 그대로 따른다 — 판정 대상 집합이 어긋나면 안 되므로
    //  다이버를 만드는 방식도 같아야 한다.
    static GameFramework.World.Entity Diver(string id, float y)
    {
        var e = new GameFramework.World.Entity(id);
        e.Add(new GameFramework.World.Transform { Position = new Vector3(0f, y, 0f).ToNumerics() });
        e.Add(new GameFramework.World.Velocity());
        e.Add(new EntityKind(EntityType.Character));
        e.Add(new GameFramework.World.Simulated());
        e.Add(new LandingImpact());
        return e;
    }

    static SkydiveLandingSystem System(GameFramework.World.EntityRegistry registry)
        => new SkydiveLandingSystem(registry, Config(),
                                    SkydiveCourseLayout.ShelfYs,
                                    SkydiveCourseLayout.SpawnY,
                                    SkydiveCourseLayout.RespawnPoints);

    [Test]
    public void 치명_속도로_착지하면_마지막_선반으로_되돌린다()
    {
        var registry = new GameFramework.World.EntityRegistry();
        //  마지막 선반(200)보다 아래에서 죽으면 그 선반으로 돌아간다.
        var diver = Diver("a", 10f);
        diver.Get<LandingImpact>().DownwardSpeed = 60f;
        registry.Add(diver);

        System(registry).Tick(0, 0.02f);

        float y = GameFramework.World.EntityMotionExtensions.GetPosition(diver).y;
        Assert.Greater(y, 10f, "되돌리지 않았다 — 죽은 자리에 그대로 있다");
        Assert.That(y, Is.EqualTo(SkydiveCourseLayout.ShelfYs[SkydiveCourseLayout.ShelfYs.Count - 1])
                         .Within(50f),
                    "마지막으로 지난 선반이 아니라 엉뚱한 자리로 갔다");
    }

    [Test]
    public void 안전_속도로_착지하면_그대로_둔다()
    {
        var registry = new GameFramework.World.EntityRegistry();
        var diver = Diver("a", 10f);
        diver.Get<LandingImpact>().DownwardSpeed = 6f;
        registry.Add(diver);

        System(registry).Tick(0, 0.02f);

        Assert.That(GameFramework.World.EntityMotionExtensions.GetPosition(diver).y,
                    Is.EqualTo(10f).Within(1e-3f), "안전한 착지인데 되돌렸다");
    }

    /// <summary>부활은 속도를 0으로 지우므로(SkydiveRespawn) 그 다음 틱에 또 죽지 않는다.</summary>
    [Test]
    public void 되돌린_뒤_즉시_또_죽지_않는다()
    {
        var registry = new GameFramework.World.EntityRegistry();
        var diver = Diver("a", 10f);
        diver.Get<LandingImpact>().DownwardSpeed = 60f;
        registry.Add(diver);

        var system = System(registry);
        system.Tick(0, 0.02f);
        Vector3 afterRespawn = GameFramework.World.EntityMotionExtensions.GetPosition(diver);

        //  다음 틱: 이동이 아직 안 돌아 충격 값은 그대로다. 그래도 두 번 되돌리면 안 된다는
        //  뜻이 아니라, 부활이 속도를 지웠는지를 본다 — 그것이 죽음 반복 루프를 막는 유일한 장치다.
        Assert.That(GameFramework.World.EntityMotionExtensions.GetVelocity(diver).magnitude,
                    Is.EqualTo(0f).Within(1e-3f), "부활이 속도를 안 지웠다 — 다음 틱에 또 죽는다");
        Assert.That(afterRespawn.y, Is.GreaterThan(10f));
    }
```

> **구현자에게**: `Config()`는 `SkydiveDoorSystemTests`가 쓰는 것을 그대로 복사하고 마지막 인자만
> 더한다. `EntityMotionExtensions.GetVelocity`의 실제 이름을 확인해 맞춘다 —
> 없으면 `diver.Get<GameFramework.World.Velocity>().Linear`를 직접 읽는다.

- [ ] **Step 2: 실패를 확인한다**

- [ ] **Step 3: 시스템을 쓴다**

`SkydiveDoorSystem`과 **같은 모양**이다 — 생성자 인자·`CollectDivers`·`Respawn` 헬퍼를 그대로 따른다.

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 세게 착지한 사람을 마지막으로 지난 선반으로 되돌린다.
    ///
    /// <para>충격 속도는 <see cref="LandingImpact"/>가 이미 들고 있다 — 이동이 끝나면 그 값을
    /// 다시 구할 수 없어서 공유 이동 단계가 남겨 둔 것이다(<see cref="SkydiveDoorSystem"/>은
    /// 틱과 위치로 다시 계산할 수 있어 나를 것이 없었다).</para>
    /// </summary>
    public class SkydiveLandingSystem : GameFramework.Runner.ITickSystem
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly SkydiveConfig config;
        private readonly IReadOnlyList<float> shelfYs;
        private readonly float spawnY;
        private readonly IReadOnlyDictionary<float, Vector3> respawnPoints;

        //  선반별 부활 순번 — 레이저·문과 공유하지 않는다(SkydiveRespawn 주석 참고).
        private readonly Dictionary<float, int> respawnCounts = new Dictionary<float, int>();
        private readonly List<GameFramework.World.Entity> divers = new List<GameFramework.World.Entity>();

        public SkydiveLandingSystem(GameFramework.World.EntityRegistry entityRegistry,
                                    SkydiveConfig config,
                                    IReadOnlyList<float> shelfYs,
                                    float spawnY,
                                    IReadOnlyDictionary<float, Vector3> respawnPoints)
        {
            this.entityRegistry = entityRegistry;
            this.config = config;
            this.shelfYs = shelfYs;
            this.spawnY = spawnY;
            this.respawnPoints = respawnPoints;
        }

        public void Tick(long tick, float deltaTime)
        {
            CollectDivers();
            for (int i = 0; i < divers.Count; i++)
            {
                GameFramework.World.Entity diver = divers[i];
                if (SkydiveLanding.IsLethal(diver, config) == false)
                {
                    continue;
                }
                Respawn(diver, GameFramework.World.EntityMotionExtensions.GetPosition(diver).y);
            }
        }

        private void Respawn(GameFramework.World.Entity diver, float deathY)
        {
            float shelfY = SkydiveCheckpoints.LastPassedShelfY(deathY, shelfYs, spawnY);
            respawnCounts.TryGetValue(shelfY, out int order);
            SkydiveRespawn.To(diver, deathY, config, shelfYs, spawnY, respawnPoints, ref order);
            respawnCounts[shelfY] = order;
        }

        //  걸러내는 기준은 SkydiveDoorSystem.CollectDivers와 같아야 한다 — 판정 대상 집합이
        //  어긋나면 안 된다.
        private void CollectDivers()
        {
            divers.Clear();
            foreach (GameFramework.World.Entity entity in entityRegistry.All)
            {
                if (entity.Get<EntityKind>()?.Kind != EntityType.Character)
                {
                    continue;
                }
                if (entity.Has<GameFramework.World.Simulated>() == false)
                {
                    continue;
                }
                divers.Add(entity);
            }
        }
    }
}
```

- [ ] **Step 4: 스코프에 등록한다**

`SkydiveLifetimeScope.cs` — `SkydiveDoorSystem` 등록 바로 아래에 만들고,
`RegisterBuildCallback`에서 **`FinishTrackingSystem`보다 먼저** 문다(레이저·문과 같은 이유).

```csharp
            builder.Register(c => new SkydiveLandingSystem(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<SkydiveConfig>(),
                SkydiveCourseLayout.ShelfYs,
                SkydiveCourseLayout.SpawnY,
                SkydiveCourseLayout.RespawnPoints), Lifetime.Singleton);
```

```csharp
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(
                    container.Resolve<SkydiveLandingSystem>());
```

- [ ] **Step 5: 통과 확인** — 서버 EditMode 전체.

- [ ] **Step 6: 절제 확인**

`if (SkydiveLanding.IsLethal(diver, config) == false) { continue; }` 를 `if (false) { continue; }`로 바꾸면
`안전_속도로_착지하면_그대로_둔다`가 빨강이 되어야 한다. 되돌리고 복귀 확인.

- [ ] **Step 7: "문짝 위 착지도 같은 규칙"을 확인한다 (읽기만)**

스펙 §11의 테스트 항목 5는 **단위 테스트로 쓰면 동어반복이 된다** — 이 코드는 무엇에 닿았는지를
아예 안 보기 때문에 "문짝이어도 같다"를 재는 분기가 없다. 대신 성질이 서는 **전제**를 확인한다:

1. `SkydiveWorld`가 이동에 넘기는 `_layerMask`가 무엇인지 (서버 스코프에서 주입되는 값)
2. `SkydiveCourseBuilder`가 문 패널을 굽는 레이어 (`box.layer = LayerMask.NameToLayer("Default")`)

**둘이 같은 레이어를 가리키는지** 확인하고 보고서에 적는다. 갈라져 있으면 문짝 위 착지가 접지로
안 잡히므로 규칙 ②가 조용히 성립하지 않는다 — **그 경우 멈추고 보고하라.**

- [ ] **Step 8: 양쪽 전체 스위트 확인**

클라·서버 각각 컴파일 + EditMode 전체. Task 1의 기준선(1148 / 941)에 이번에 더한 테스트 수만큼 늘어야 한다.

- [ ] **Step 9: 커밋**

---

## 마무리 (계획 밖, 사람이 판단)

- 여섯 레포 머지·푸시(푸시 규약: `fetch` → `rebase --autostash origin/main` → `main` `--ff-only` → `merge --no-ff` → `push`, **한 줄씩 결과 확인**)
- **마스터데이터만 바뀐 것이 아니므로** 게임서버 배포가 필요하다(이미지 태그 = 서버 레포 SHA)
- 로컬 배포 후 **플레이로 확인할 것**:
  1. 대자로 내리꽂으면 죽고 체크포인트로 간다
  2. 패러세일을 펴면 산다 — 대자 기준 **11 m 위에서** 펴면 충분한가
  3. 점프 착지가 안 죽는다
  4. **선반이 너무 무서워서 회복하러 가지 않게 되는가** (스펙 §10-1 — 이 슬라이스의 가장 큰 위험)
  5. 문짝 위 착지도 같은 규칙인가
