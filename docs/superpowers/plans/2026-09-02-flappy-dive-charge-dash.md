# 다이브 충전 대시 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 떨어질수록 차는 게이지가 가득 차면 대시 버튼이 활성화되고, 누르면 0.2초 동안 전진 2배로 수평 직선을 그린다.

**Architecture:** 시뮬은 LOP-Shared의 공유 구체 클래스(`FlappyDash` 컴포넌트 + `FlappyDashSystem`)라 클·서가 같은 코드를 돌린다 — 예측을 따로 짜지 않는다. 입력은 기존 `InputCommand`에 필드 하나, 권위는 기존 스냅에 필드 둘을 얹는다. 되감기는 `FlappySavedState`에 두 값을 더한다.

**Tech Stack:** C# / Unity 6 / Mirror / Protobuf / Luban(MasterData) / VContainer / UI Toolkit / NUnit(EditMode)

**Spec:** `docs/superpowers/specs/2026-09-02-flappy-dive-charge-dash-design.md`

## Global Constraints

- **푸시 규약**: `fetch` → `rebase --autostash origin/main` → `checkout main` → `merge --ff-only origin/main` → `merge --no-ff <feature>` → `push`. **한 줄씩 결과를 확인한다. `&&`로 이어 붙이지 않는다.**
- **`git push --force` / `--force-with-lease` 금지.** 거절되면 다시 fetch → 리베이스 → 재시도.
- **`git add -A` / `git commit -a` 금지.** 바꾼 파일만 경로로 지정하고, 커밋 전에 `git status --short`로 스테이지된 것이 의도한 파일뿐인지 확인한다.
- **main에 직접 커밋 금지.** 피처 브랜치에서 작업한다.
- **유니티 레포에 git worktree 금지.** 일반 브랜치로 전환한다.
- **`.meta` 파일은 유니티가 만든 것을 반드시 함께 커밋한다.** 직접 만들지 않는다. (`docs/` 아래는 `Assets/` 밖이라 `.meta`가 없다.)
- **테스트를 위한 어셈블리 이동은 하지 않는다.**
- **모든 새 테스트는 일부러 깨뜨려 빨강을 확인한다.** 통과만으로 검증됐다고 하지 않는다.
- **여러 레포가 걸린 변경은 레포마다 각각** 푸시 규약을 밟는다. 한 레포만 올라가면 계약이 어긋난다.
- **MasterData 생성물을 커밋하기 전에 `git fetch`한다.** 다른 기계가 이미 구웠으면 `.meta` GUID만 달라 중복 작업이 된다.
- 워킹트리의 **의도적으로 커밋하지 않는 로컬 픽스처**를 절대 스테이지하지 않는다 — 클라: `Assets/Art` 포인터, `Room.unity`, `Jua-Regular SDF.asset`, `PackageManagerSettings.asset`, `ProjectSettings.asset` / 서버: `ConfigureRoomComponent.cs`, 볼륨 프로파일, URP 에셋, `ProjectSettings.asset`, 빌드 디렉터리, `test-results.xml`.
- **확정값**: 지속 0.2초 · 전진 2배 · 기본 충전 0.13/초 · 다이브 충전 최대 +1.2/초 · 시작 게이지 0.6 · 대시 중 세로속도 0 + 중력 없음 · 대시 중 플랩 무시 · 대시 중 충돌은 평소와 같은 스턴.

## File Structure

| 파일 | 책임 |
|---|---|
| `Shared/Game/FlappyTickDuration.cs` [신규] | 남은시간 ↔ 끝나는 절대 틱 변환. **이 변환의 유일한 자리** |
| `Shared/Game/FlappyDash.cs` [신규] | 게이지와 남은시간을 담는 데이터만 |
| `Shared/Game/FlappyDashSystem.cs` [신규] | 충전·발동·소진·취소 |
| `Shared/Game/FlappyMoveSystem.cs` | 대시 중이면 x 2배 · vy 0 · 중력 없음 |
| `Shared/Game/FlappyWorld.cs` | 틱 순서에 대시를 끼우고, 스턴 진입 때 취소 |
| `Shared/Game/FlappySavedState.cs` | 되감기에 게이지·남은시간 추가 |
| `Shared/Game/FlappyConfig.cs` | 튜닝값 4개 추가 |
| `Shared/Protos/InputCommand.proto` | `bool dash = 10` |
| `Shared/Protos/EntitySnap.proto` | `dash_end_tick = 20`, `dash_charge = 21` |
| `Server/EntitySnapshotBroadcastSystem.cs` | 두 필드 채우기 |
| `Client/FlappyServerCorrectionHandler.cs` | 대시 불일치도 되돌리기 사유로 |
| `Client/PlayerInputManager.cs` | `SetDash` |
| `Client/UI/FlapPad/*` | 대시 버튼 = 게이지 |
| `infrastructure/table/Datas/#FlappyConfig.xlsx` | 4열 추가 |

---

### Task 1: 시간 변환을 한 곳으로 모은다

스턴에만 있던 "남은시간 ↔ 끝나는 절대 틱" 변환을 대시도 쓴다. 두 벌이 되면 **올림 때문에 한 틱을 더 세는 실수**가 재발하므로(스턴에서 실제로 겪었다) 먼저 한 곳으로 옮긴다.

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyTickDuration.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyStunSystem.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/EntitySnapshotBroadcastSystem.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Netcode/FlappyServerCorrectionHandler.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/FlappyTickDurationTests.cs`

**Interfaces:**
- Produces: `LOP.FlappyTickDuration.Epsilon` (const float), `.EndTick(float remaining, long tick, float deltaTime) → long`, `.RemainingSeconds(long endTick, long tick, float deltaTime) → float`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

```csharp
using NUnit.Framework;

namespace LOP.Tests
{
    public class FlappyTickDurationTests
    {
        private const float Dt = 0.02f;   // 50Hz

        [Test]
        public void 남은시간이_틱에_딱_떨어지면_그만큼만_센다()
        {
            //  0.8초 = 40틱. 41틱이 되면 받는 쪽만 한 틱 더 얼어 있게 된다(라이브에서 겪은 버그).
            Assert.AreEqual(1040L, FlappyTickDuration.EndTick(0.8f, 1000L, Dt));
        }

        [Test]
        public void 시뮬이_이미_0으로_본_잔여는_남은_틱이_없다()
        {
            //  매 틱 float를 빼면 정확히 0을 못 찍고 아주 조금 남는다. 그 조각은 끝난 것이다.
            Assert.AreEqual(0L, FlappyTickDuration.EndTick(1e-6f, 1000L, Dt));
        }

        [Test]
        public void 틱_사이의_시간은_올려서_센다()
        {
            //  0.05초는 2.5틱 — 3틱을 세야 그 시간이 다 지난다.
            Assert.AreEqual(1003L, FlappyTickDuration.EndTick(0.05f, 1000L, Dt));
        }

        [Test]
        public void 끝나는_틱에서_남은시간을_되계산한다()
        {
            Assert.AreEqual(0.8f, FlappyTickDuration.RemainingSeconds(1040L, 1000L, Dt), 1e-5f);
        }

        [Test]
        public void 이미_지난_끝틱은_남은시간이_0이다()
        {
            Assert.AreEqual(0f, FlappyTickDuration.RemainingSeconds(1000L, 1000L, Dt));
            Assert.AreEqual(0f, FlappyTickDuration.RemainingSeconds(990L, 1000L, Dt));
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

`unity cmd run_tests --project-path <서버 프로젝트> --mode EditMode --async_tests true` (클라 Test Runner가 막혀 있으면 서버 에디터에서 Shared 테스트를 돌린다.)
기대: `FlappyTickDuration` 타입이 없어 컴파일 실패.

- [ ] **Step 3: `FlappyTickDuration`을 만든다**

```csharp
namespace LOP
{
    /// <summary>
    /// 남은 시간(초)과 "끝나는 절대 틱" 사이의 변환. 와이어로는 남은 시간이 아니라 끝나는 틱을
    /// 보내므로(스냅이 늦게 도착해도 값이 낡지 않는다) 양쪽이 이 한 곳을 쓴다.
    ///
    /// <para>같은 식을 두 벌 적으면 <b>올림 때문에 한 틱을 더 세는 실수</b>가 재발한다 — 스턴에서
    /// 실제로 겪었고, 받는 쪽만 한 틱 더 얼어 있게 만들었다. 그래서 이 변환의 자리는 여기 하나다.</para>
    /// </summary>
    public static class FlappyTickDuration
    {
        /// <summary>
        /// 매 틱 float를 빼 나가면 정확히 0을 못 찍고 아주 조금(예: 2e-7) 남는다. 시뮬은 그 잔여를
        /// 끝으로 보는데 올림은 한 틱으로 세어 버리므로, 세기 전에 이만큼을 먼저 뺀다.
        /// </summary>
        public const float Epsilon = 1e-5f;

        public static long EndTick(float remaining, long tick, float deltaTime)
        {
            if (remaining <= Epsilon || deltaTime <= 0f)
            {
                return 0;
            }
            return tick + (long)System.Math.Ceiling((remaining - Epsilon) / deltaTime);
        }

        public static float RemainingSeconds(long endTick, long tick, float deltaTime)
        {
            long remainingTicks = endTick - tick;
            return remainingTicks > 0 ? remainingTicks * deltaTime : 0f;
        }
    }
}
```

- [ ] **Step 4: `FlappyStunSystem`의 두 static을 지우고 호출부를 옮긴다**

`FlappyStunSystem`에서 `EndTick`/`RemainingSeconds`와 그 XML 주석을 삭제하고, `Epsilon`은 `FlappyTickDuration.Epsilon`을 쓴다. 호출부 두 곳을 바꾼다:

- 서버 `EntitySnapshotBroadcastSystem`: `FlappyStunSystem.EndTick(...)` → `FlappyTickDuration.EndTick(...)` (2줄)
- 클라 `FlappyServerCorrectionHandler`: `FlappyStunSystem.RemainingSeconds(...)` → `FlappyTickDuration.RemainingSeconds(...)` (2줄)

이름이 하나만 남게 하는 것이 목적이다 — 위임 래퍼를 남기면 같은 개념에 이름이 둘이 된다.

- [ ] **Step 5: 테스트가 통과하는지, 기존 스턴 테스트도 통과하는지 확인한다**

기대: 새 테스트 5개 통과 + 기존 Flappy 스턴 테스트 전부 통과.

- [ ] **Step 6: 테스트가 실제로 실패할 수 있는지 확인한다**

`EndTick`에서 `- Epsilon`을 빼고 돌린다 → `시뮬이_이미_0으로_본_잔여는_남은_틱이_없다`가 빨강이어야 한다. 확인 후 되돌린다.

- [ ] **Step 7: 커밋 (레포 3개 각각)**

세 레포가 한 슬라이스이므로 **Shared → Server → Client 순서로 각각** 푸시 규약을 밟는다. Shared가 먼저 올라가야 나머지 둘이 컴파일된다.

---

### Task 2: 튜닝값 4개를 시뮬까지 도달시킨다

시스템을 쓰기 전에 값이 먼저 있어야 한다. Excel → Luban → 양쪽 MasterData 패키지 → `FlappyConfig`까지 한 번에 뚫는다.

**Files:**
- Modify: `infrastructure/table/Datas/#FlappyConfig.xlsx`
- Regenerate: `LeagueOfPhysical-MasterData-{Client,Server}` (Luban 생성물)
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyConfig.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/FlappyConfigProvider.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/FlappyConfigProvider.cs`

**Interfaces:**
- Consumes: 없음
- Produces: `FlappyConfig.DashMult`, `.DashDuration`, `.DashChargeBase`, `.DashChargeDive` (전부 `float`, 읽기 전용). 생성자 인자 순서는 기존 9개 **뒤에** 4개를 붙인다.

- [ ] **Step 1: Excel에 4열을 더한다**

`infrastructure/table/Datas/#FlappyConfig.xlsx`의 기존 9열(`forward_speed`…`invuln_time`) 뒤에:

| 열 | 값 |
|---|---|
| `dash_mult` | 2 |
| `dash_duration` | 0.2 |
| `dash_charge_base` | 0.13 |
| `dash_charge_dive` | 1.2 |

타입은 기존 열과 같은 `float`, 그룹은 클·서 공용(기존 열과 동일하게 둔다 — 시뮬이 양쪽에서 같은 값을 봐야 한다).

- [ ] **Step 2: 굽기 전에 fetch한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-MasterData-Client && git fetch origin && git status
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-MasterData-Server && git fetch origin && git status
```

다른 기계가 이미 구웠으면 여기서 멈추고 그것을 쓴다 — 재생성하면 `.meta` GUID만 달라진 중복 커밋이 된다.

- [ ] **Step 3: Luban으로 굽는다**

```bash
cd /Users/insoobae/workspace/LOP/infrastructure/table && bash gen.sh
```

기대: 양쪽 MasterData 패키지의 `Tables.cs`/`FlappyConfig.cs`에 새 프로퍼티 4개, `tbflappyconfig.bytes` 갱신.

- [ ] **Step 4: `FlappyConfig`에 4값을 더한다**

```csharp
        /// <summary>대시 중 전진 배수. 이 게임에서 전진 속도가 바뀌는 유일한 경우다.</summary>
        public readonly float DashMult;

        /// <summary>대시가 지속되는 시간(초).</summary>
        public readonly float DashDuration;

        /// <summary>가만히 있어도 차는 초당 충전량.</summary>
        public readonly float DashChargeBase;

        /// <summary>최고 속도로 떨어질 때 여기에 더해지는 초당 충전량. 낙하 속도에 비례한다.</summary>
        public readonly float DashChargeDive;
```

생성자에 네 인자를 **기존 인자 뒤에** 붙이고 대입한다.

- [ ] **Step 5: 양쪽 provider가 새 열을 넘기게 한다**

클라·서버 `FlappyConfigProvider.Get()`의 `new FlappyConfig(...)`에 네 인자를 더한다. **양쪽이 같은 열을 읽어야** 클·서 시뮬이 같은 값을 본다.

```csharp
            return new FlappyConfig(
                r.ForwardSpeed, r.FlapImpulse, r.Gravity, r.MaxFallSpeed,
                r.BodyRadius, r.BodyHeight, r.Restitution,
                stunTime: r.StunTime,
                invulnTime: r.InvulnTime,
                dashMult: r.DashMult,
                dashDuration: r.DashDuration,
                dashChargeBase: r.DashChargeBase,
                dashChargeDive: r.DashChargeDive);
```

- [ ] **Step 6: 양쪽 프로젝트가 컴파일되는지 확인한다**

```bash
unity cmd recompile --project-path <클라>   # 그다음 recompile_status
unity cmd recompile --project-path <서버>
```

- [ ] **Step 7: 커밋 (레포 5개 각각)**

`infrastructure` → `MasterData-Client` → `MasterData-Server` → `Shared` → `Client`/`Server` 순서. MasterData 생성물은 `.bytes`와 `.cs`, 그리고 유니티가 만든 `.meta`를 함께 커밋한다.

---

### Task 3: 게이지와 대시 상태

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyDash.cs`
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyDashSystem.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/FlappyDashSystemTests.cs`

**Interfaces:**
- Consumes: `FlappyConfig.DashDuration/.DashMult/.DashChargeBase/.DashChargeDive`, `FlappyTickDuration.Epsilon`
- Produces:
  - `LOP.FlappyDash : GameFramework.World.Component` — `public float Charge`, `public float DashRemaining`, `public const float InitialCharge = 0.6f`
  - `LOP.FlappyDashSystem` — 생성자 `(FlappyConfig config)`, `bool IsDashing(Entity)`, `bool TryActivate(Entity)`, `void Cancel(Entity)`, `void Tick(Entity, float deltaTime)`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

```csharp
using NUnit.Framework;

namespace LOP.Tests
{
    public class FlappyDashSystemTests
    {
        private const float Dt = 0.02f;

        //  대시에 관계된 값만 실제 값으로 두고 나머지는 이 테스트에 무의미한 자리채움이다.
        private static FlappyConfig Config() => new FlappyConfig(
            forwardSpeed: 11f, flapImpulse: 23f, gravity: 70f, maxFallSpeed: 30f,
            bodyRadius: 0.45f, bodyHeight: 0.9f, restitution: 0.35f,
            stunTime: 0.8f, invulnTime: 0.6f,
            dashMult: 2f, dashDuration: 0.2f, dashChargeBase: 0.13f, dashChargeDive: 1.2f);

        private static GameFramework.World.Entity Bird(float verticalSpeed = 0f)
        {
            var entity = new GameFramework.World.Entity("bird");
            entity.Add(new GameFramework.World.Velocity
            {
                Linear = new System.Numerics.Vector3(11f, verticalSpeed, 0f),
            });
            entity.Add(new FlappyDash());
            return entity;
        }

        [Test]
        public void 안_떨어지면_기본_속도로만_찬다()
        {
            var bird = Bird(verticalSpeed: 0f);
            bird.Get<FlappyDash>().Charge = 0f;

            new FlappyDashSystem(Config()).Tick(bird, Dt);

            Assert.AreEqual(0.13f * Dt, bird.Get<FlappyDash>().Charge, 1e-6f);
        }

        [Test]
        public void 최고_속도로_떨어지면_기본에_다이브가_다_더해진다()
        {
            var bird = Bird(verticalSpeed: -30f);   // 최대낙하
            bird.Get<FlappyDash>().Charge = 0f;

            new FlappyDashSystem(Config()).Tick(bird, Dt);

            Assert.AreEqual((0.13f + 1.2f) * Dt, bird.Get<FlappyDash>().Charge, 1e-6f);
        }

        [Test]
        public void 절반_속도로_떨어지면_다이브도_절반만_더해진다()
        {
            var bird = Bird(verticalSpeed: -15f);
            bird.Get<FlappyDash>().Charge = 0f;

            new FlappyDashSystem(Config()).Tick(bird, Dt);

            Assert.AreEqual((0.13f + 0.6f) * Dt, bird.Get<FlappyDash>().Charge, 1e-6f);
        }

        [Test]
        public void 올라가는_중에는_다이브가_안_붙는다()
        {
            var bird = Bird(verticalSpeed: 23f);   // 막 날갯짓한 직후
            bird.Get<FlappyDash>().Charge = 0f;

            new FlappyDashSystem(Config()).Tick(bird, Dt);

            Assert.AreEqual(0.13f * Dt, bird.Get<FlappyDash>().Charge, 1e-6f);
        }

        [Test]
        public void 게이지는_1을_넘지_않는다()
        {
            var bird = Bird(verticalSpeed: -30f);
            bird.Get<FlappyDash>().Charge = 0.999f;

            new FlappyDashSystem(Config()).Tick(bird, Dt);

            Assert.AreEqual(1f, bird.Get<FlappyDash>().Charge);
        }

        [Test]
        public void 가득_차야만_발동한다()
        {
            var system = new FlappyDashSystem(Config());
            var bird = Bird();
            bird.Get<FlappyDash>().Charge = 0.99f;

            Assert.IsFalse(system.TryActivate(bird));
            Assert.IsFalse(system.IsDashing(bird));
        }

        [Test]
        public void 발동하면_게이지를_전부_쓰고_지속이_찬다()
        {
            var system = new FlappyDashSystem(Config());
            var bird = Bird();
            bird.Get<FlappyDash>().Charge = 1f;

            Assert.IsTrue(system.TryActivate(bird));
            Assert.AreEqual(0f, bird.Get<FlappyDash>().Charge);
            Assert.AreEqual(0.2f, bird.Get<FlappyDash>().DashRemaining, 1e-6f);
            Assert.IsTrue(system.IsDashing(bird));
        }

        [Test]
        public void 대시_중에는_다시_발동되지_않는다()
        {
            var system = new FlappyDashSystem(Config());
            var bird = Bird();
            bird.Get<FlappyDash>().Charge = 1f;
            system.TryActivate(bird);
            bird.Get<FlappyDash>().Charge = 1f;   // 어떻게든 다시 찼다고 쳐도

            Assert.IsFalse(system.TryActivate(bird));
        }

        [Test]
        public void 지속만큼의_틱_동안만_대시다()
        {
            var system = new FlappyDashSystem(Config());
            var bird = Bird();
            bird.Get<FlappyDash>().Charge = 1f;
            system.TryActivate(bird);

            //  0.2초 / 0.02초 = 10틱. 월드는 Tick(감소)을 먼저 부르고 그다음에 이동하므로,
            //  발동한 틱을 포함해 정확히 10틱이 대시여야 한다 — 한 틱이라도 더 가면 안 된다.
            for (int i = 0; i < 10; i++)
            {
                Assert.IsTrue(system.IsDashing(bird), $"{i}번째 틱은 아직 대시여야 한다");
                system.Tick(bird, Dt);
            }
            Assert.IsFalse(system.IsDashing(bird), "10틱이 지나면 대시가 끝나야 한다");
        }

        [Test]
        public void 취소하면_그_자리에서_끝난다()
        {
            var system = new FlappyDashSystem(Config());
            var bird = Bird();
            bird.Get<FlappyDash>().Charge = 1f;
            system.TryActivate(bird);

            system.Cancel(bird);

            Assert.IsFalse(system.IsDashing(bird));
        }

        [Test]
        public void 대시_컴포넌트가_없는_엔티티에는_아무_일도_없다()
        {
            var system = new FlappyDashSystem(Config());
            var plain = new GameFramework.World.Entity("no-dash");

            Assert.IsFalse(system.IsDashing(plain));
            Assert.IsFalse(system.TryActivate(plain));
            Assert.DoesNotThrow(() => system.Tick(plain, Dt));
            Assert.DoesNotThrow(() => system.Cancel(plain));
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다** — `FlappyDash`/`FlappyDashSystem`이 없어 컴파일 실패.

- [ ] **Step 3: `FlappyDash`를 만든다**

```csharp
namespace LOP
{
    /// <summary>
    /// 대시 게이지와 대시가 끝나기까지 남은 시간. 데이터만 갖는다 — 충전·발동·소진은
    /// <see cref="FlappyDashSystem"/>이 한다(<see cref="FlappyStun"/>과 같은 짝).
    /// </summary>
    public class FlappyDash : GameFramework.World.Component
    {
        /// <summary>첫 대시를 오래 기다리지 않게 하는 출발값. 튜닝 대상이 아니라 컨피그로 빼지 않았다.</summary>
        public const float InitialCharge = 0.6f;

        /// <summary>0~1. 1이면 발동할 수 있다.</summary>
        public float Charge = InitialCharge;

        /// <summary>대시가 끝나기까지 남은 시간(초). 0이면 대시 중이 아니다.</summary>
        public float DashRemaining;
    }
}
```

- [ ] **Step 4: `FlappyDashSystem`을 만든다**

```csharp
namespace LOP
{
    /// <summary>
    /// 대시의 충전·발동·소진. 클·서가 같은 구체 클래스를 돌려 결과가 갈리지 않는다.
    /// </summary>
    public class FlappyDashSystem
    {
        private readonly FlappyConfig config;

        public FlappyDashSystem(FlappyConfig config)
        {
            this.config = config;
        }

        public bool IsDashing(GameFramework.World.Entity entity)
        {
            var dash = entity.Get<FlappyDash>();
            return dash != null && dash.DashRemaining > 0f;
        }

        /// <summary>게이지가 가득이고 대시 중이 아닐 때만 발동한다. 게이지는 전부 쓴다.</summary>
        public bool TryActivate(GameFramework.World.Entity entity)
        {
            var dash = entity.Get<FlappyDash>();
            if (dash == null || dash.DashRemaining > 0f || dash.Charge < 1f)
            {
                return false;
            }
            dash.Charge = 0f;
            dash.DashRemaining = config.DashDuration;
            return true;
        }

        /// <summary>대시를 그 자리에서 끝낸다. 스턴에 들어갈 때 부른다.</summary>
        public void Cancel(GameFramework.World.Entity entity)
        {
            var dash = entity.Get<FlappyDash>();
            if (dash != null)
            {
                dash.DashRemaining = 0f;
            }
        }

        public void Tick(GameFramework.World.Entity entity, float deltaTime)
        {
            var dash = entity.Get<FlappyDash>();
            if (dash == null)
            {
                return;
            }

            if (dash.DashRemaining > 0f)
            {
                dash.DashRemaining -= deltaTime;
                if (dash.DashRemaining <= FlappyTickDuration.Epsilon)
                {
                    dash.DashRemaining = 0f;
                }
            }

            if (dash.Charge >= 1f)
            {
                return;
            }

            //  떨어지는 중일 때만 다이브 몫이 붙고, 그 크기는 낙하 속도에 비례한다.
            //  최대낙하로 나눠 정규화하는 것이 핵심이다 — 물리값을 튜닝해도 "최고 속도로
            //  떨어지면 최대 충전"이라는 감각이 그대로 유지된다.
            float fallSpeed = -(entity.Get<GameFramework.World.Velocity>()?.Linear.Y ?? 0f);
            float dive = fallSpeed > 0f && config.MaxFallSpeed > 0f
                ? config.DashChargeDive * System.Math.Min(fallSpeed, config.MaxFallSpeed) / config.MaxFallSpeed
                : 0f;

            dash.Charge = System.Math.Min(1f, dash.Charge + (config.DashChargeBase + dive) * deltaTime);
        }
    }
}
```

- [ ] **Step 5: 테스트가 통과하는지 확인한다** (11개)

- [ ] **Step 6: 테스트가 실제로 실패할 수 있는지 확인한다**

`Tick`의 `System.Math.Min(fallSpeed, config.MaxFallSpeed)`에서 `Min`을 지워 본다 → `최고_속도로_떨어지면...`은 통과하고 **정규화가 깨지는 다른 테스트가 빨강**이어야 한다. 그다음 `TryActivate`의 `dash.Charge < 1f`를 `<= 0f`로 바꿔 본다 → `가득_차야만_발동한다`가 빨강이어야 한다. 확인 후 되돌린다.

- [ ] **Step 7: 커밋** (Shared 한 레포)

---

### Task 4: 이동이 대시를 반영한다

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyMoveSystem.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/FlappyMoveSystemDashTests.cs`

**Interfaces:**
- Consumes: `FlappyConfig.DashMult`
- Produces: `FlappyMoveSystem.Tick(Entity entity, float deltaTime, bool dashing)` — **인자가 하나 늘어난다.** 호출부는 `FlappyWorld` 한 곳뿐이며 Task 5에서 고친다.

> **왜 `bool`을 받나:** 이동 시스템이 `FlappyDashSystem`을 알게 하면 시스템끼리 고리가 생긴다. "대시 중인가"는 월드가 이미 아는 사실이므로 넘겨받는다 — 스턴을 월드가 판정해 이동을 건너뛰는 것과 같은 모양이다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

```csharp
using NUnit.Framework;

namespace LOP.Tests
{
    public class FlappyMoveSystemDashTests
    {
        private const float Dt = 0.02f;

        private static FlappyConfig Config() => new FlappyConfig(
            forwardSpeed: 11f, flapImpulse: 23f, gravity: 70f, maxFallSpeed: 30f,
            bodyRadius: 0.45f, bodyHeight: 0.9f, restitution: 0.35f,
            stunTime: 0.8f, invulnTime: 0.6f,
            dashMult: 2f, dashDuration: 0.2f, dashChargeBase: 0.13f, dashChargeDive: 1.2f);

        private static GameFramework.World.Entity Bird(float verticalSpeed, bool jump = false)
        {
            var entity = new GameFramework.World.Entity("bird");
            entity.Add(new GameFramework.World.Velocity
            {
                Linear = new System.Numerics.Vector3(11f, verticalSpeed, 0f),
            });
            entity.Add(new InputBuffer { Current = new InputCommand { Jump = jump } });
            return entity;
        }

        [Test]
        public void 대시_중에는_전진이_두_배다()
        {
            var bird = Bird(verticalSpeed: -5f);

            new FlappyMoveSystem(Config()).Tick(bird, Dt, dashing: true);

            Assert.AreEqual(22f, bird.Get<GameFramework.World.Velocity>().Linear.X, 1e-4f);
        }

        [Test]
        public void 대시_중에는_세로_속도가_0이고_중력이_안_먹는다()
        {
            var bird = Bird(verticalSpeed: -5f);

            new FlappyMoveSystem(Config()).Tick(bird, Dt, dashing: true);

            Assert.AreEqual(0f, bird.Get<GameFramework.World.Velocity>().Linear.Y, 1e-4f);
        }

        [Test]
        public void 대시_중_날갯짓은_무시된다()
        {
            //  수평 직선이 깨지면 대시가 아니다.
            var bird = Bird(verticalSpeed: -5f, jump: true);

            new FlappyMoveSystem(Config()).Tick(bird, Dt, dashing: true);

            Assert.AreEqual(0f, bird.Get<GameFramework.World.Velocity>().Linear.Y, 1e-4f);
        }

        [Test]
        public void 대시가_아니면_예전과_똑같다()
        {
            var bird = Bird(verticalSpeed: 0f);

            new FlappyMoveSystem(Config()).Tick(bird, Dt, dashing: false);

            var velocity = bird.Get<GameFramework.World.Velocity>().Linear;
            Assert.AreEqual(11f, velocity.X, 1e-4f);
            Assert.AreEqual(-70f * Dt, velocity.Y, 1e-4f);
        }

        [Test]
        public void 대시가_아니면_날갯짓이_그대로_먹는다()
        {
            var bird = Bird(verticalSpeed: -5f, jump: true);

            new FlappyMoveSystem(Config()).Tick(bird, Dt, dashing: false);

            Assert.AreEqual(23f, bird.Get<GameFramework.World.Velocity>().Linear.Y, 1e-4f);
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다** — 인자 3개짜리 `Tick`이 없어 컴파일 실패.

- [ ] **Step 3: `FlappyMoveSystem.Tick`에 대시를 넣는다**

```csharp
        public void Tick(GameFramework.World.Entity entity, float deltaTime, bool dashing)
        {
            var worldVelocity = entity.Get<GameFramework.World.Velocity>();
            if (worldVelocity == null)
            {
                return;   // 이동 없는 엔티티
            }

            Vector3 velocity = worldVelocity.Linear.ToUnity();

            if (dashing)
            {
                // 대시는 완전한 수평 직선이다 — 중력도 날갯짓도 이번 틱엔 없다.
                velocity.y = 0f;
            }
            else
            {
                velocity.y -= config.Gravity * deltaTime;
                if (velocity.y < -config.MaxFallSpeed)
                {
                    velocity.y = -config.MaxFallSpeed;
                }

                // 플랩은 지금까지의 세로 속도를 지우고 새로 준다 — 낙하 중에 눌러도 늘 같은 높이로 뜬다.
                // 중력 다음에 오는 것이 중요하다. 앞에 두면 누른 틱의 중력만큼 손해를 봐서 높이가 흔들린다.
                var input = entity.Get<InputBuffer>()?.Current;
                if (input != null && input.Jump)
                {
                    velocity.y = config.FlapImpulse;
                }
            }

            // 전진은 플레이어가 바꿀 수 없는 상수이고, 대시만 그것을 배수로 늘린다.
            // z를 0으로 붙잡아 코스 밖으로 새지 않게 한다.
            velocity.x = dashing ? config.ForwardSpeed * config.DashMult : config.ForwardSpeed;
            velocity.z = 0f;

            worldVelocity.Linear = velocity.ToNumerics();
        }
```

- [ ] **Step 4: 테스트가 통과하는지 확인한다** (5개 + 기존 이동 테스트)

- [ ] **Step 5: 테스트가 실제로 실패할 수 있는지 확인한다**

`dashing`일 때도 중력을 먹이도록 분기를 지워 본다 → `대시_중에는_세로_속도가_0이고...`가 빨강이어야 한다. 되돌린다.

- [ ] **Step 6: 커밋** (Shared 한 레포 — 이 시점에 `FlappyWorld`가 아직 안 고쳐져 컴파일이 깨지므로 **Task 5와 한 커밋으로 묶는다.** 여기서는 커밋하지 않고 Task 5로 이어간다.)

---

### Task 5: 월드 틱 순서와 되감기

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyWorld.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappySavedState.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Netcode/FlappyServerCorrectionHandler.cs` (이름 변경 반영)
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Entity/FlappyBirdCreator.cs` (컴포넌트 부착)
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Entity/FlappyBirdCreator.cs` (컴포넌트 부착)
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/FlappySavedStateTests.cs` (있으면 확장, 없으면 신규)

**Interfaces:**
- Consumes: `FlappyDashSystem`, `FlappyMoveSystem.Tick(…, bool dashing)`
- Produces: `FlappyWorld.TryGetSavedState(long tick, string entityId, out FlappySavedState)` — **`TryGetSavedStun`에서 이름만 바뀐다.** 시그니처는 그대로다(이미 상태를 통째로 돌려주고 있었다).

- [ ] **Step 1: 되감기 왕복 테스트를 쓴다**

```csharp
        [Test]
        public void 저장과_복원이_게이지와_남은시간을_왕복시킨다()
        {
            var bird = new GameFramework.World.Entity("bird");
            bird.Add(new FlappyStun { StunRemaining = 0.4f, InvulnRemaining = 0.1f });
            bird.Add(new FlappyDash { Charge = 0.73f, DashRemaining = 0.12f });

            var saved = FlappySavedState.Capture(bird);

            bird.Get<FlappyDash>().Charge = 0f;
            bird.Get<FlappyDash>().DashRemaining = 0f;
            saved.RestoreTo(bird);

            Assert.AreEqual(0.73f, bird.Get<FlappyDash>().Charge, 1e-6f);
            Assert.AreEqual(0.12f, bird.Get<FlappyDash>().DashRemaining, 1e-6f);
        }
```

- [ ] **Step 2: 실패를 확인한다**

- [ ] **Step 3: `FlappySavedState`에 두 값을 더한다**

`Charge`/`DashRemaining` 필드를 추가하고, `Capture`에서 `FlappyDash`를 읽어(없으면 0) 담고, `RestoreTo`에서 되돌린다. **스턴과 대시를 각각 null 검사한다** — 한쪽만 있는 엔티티가 있을 수 있다.

- [ ] **Step 4: `FlappyWorld.Mutation`에 대시를 끼운다**

기존 "스턴 틱 → 스턴이면 건너뜀 → 이동" 사이에 넣는다:

```csharp
            // 시간 감소가 먼저다. 이번 틱에 풀릴 새는 이번 틱부터 움직인다.
            for (int i = 0; i < _birds.Count; i++)
            {
                _stunSystem.Tick(_birds[i], deltaTime);
                _dashSystem.Tick(_birds[i], deltaTime);   // 남은시간 감소 + 게이지 충전
            }

            for (int i = 0; i < _birds.Count; i++)
            {
                if (_stunSystem.IsStunned(_birds[i]))
                {
                    // 스턴 중인 새는 전진도 하지 않는다 — 시간 손실이 이 게임의 페널티다.
                    _birds[i].Get<GameFramework.World.Velocity>().Linear = System.Numerics.Vector3.Zero;
                    continue;
                }

                //  발동은 시간 감소 뒤, 이동 앞이다. 뒤에 두면 누른 틱의 중력을 한 번 먹고
                //  대시가 시작돼 수평 직선이 살짝 처진 선이 된다.
                var input = _birds[i].Get<InputBuffer>()?.Current;
                if (input != null && input.Dash)
                {
                    _dashSystem.TryActivate(_birds[i]);
                }

                _moveSystem.Tick(_birds[i], deltaTime, _dashSystem.IsDashing(_birds[i]));
            }
```

`MoveBlockedByMap`에서 스턴에 진입하는 자리(`_stunSystem.Enter(entity)`) 바로 뒤에 `_dashSystem.Cancel(entity)`를 더한다.

생성자에 `FlappyDashSystem`을 주입받아 `_dashSystem`에 담는다. DI 등록은 시뮬이므로 `Register<FlappyDashSystem>`(구체)이다 — 클·서 양쪽 LifetimeScope에 더한다.

- [ ] **Step 5: `TryGetSavedStun` → `TryGetSavedState`로 이름을 바꾼다**

`FlappyWorld`의 메서드명과 클라 `FlappyServerCorrectionHandler`의 호출부(1곳). `IWorld` 쪽 인터페이스에 선언이 있으면 함께 바꾼다.

- [ ] **Step 6: 새를 만들 때 `FlappyDash`를 붙인다**

**양쪽 레포에 같은 이름의 `FlappyBirdCreator`가 따로 있다.** 둘 다 `worldEntity.Add(new FlappyStun());` 바로 옆에 한 줄을 더한다:

```csharp
            worldEntity.Add(new FlappyDash());
```

- 서버: `LeagueOfPhysical-Server/Assets/Scripts/Entity/FlappyBirdCreator.cs`
- 클라: `LeagueOfPhysical-Client/Assets/Scripts/Entity/FlappyBirdCreator.cs` (48번째 줄 근처)

**한쪽만 고치면 안 된다** — 클라에 안 붙이면 내 새만 대시가 없어 예측이 서버와 갈리고, 서버에 안 붙이면 권위 쪽이 대시를 모른다.

- [ ] **Step 7: 테스트 + 컴파일 확인**

Shared EditMode 전부 + 클라·서버 `recompile`.

- [ ] **Step 8: 테스트가 실제로 실패할 수 있는지 확인한다**

`RestoreTo`에서 `DashRemaining` 복원 줄을 지워 본다 → 왕복 테스트가 빨강이어야 한다. 되돌린다.

- [ ] **Step 9: 커밋 (Task 4와 함께, 레포 3개 각각)**

Shared → Server → Client 순서.

---

### Task 6: 입력이 서버까지 간다

**Files:**
- Modify: `LeagueOfPhysical-Shared/Protos/InputCommand.proto`
- Regenerate: `LeagueOfPhysical-Shared/Runtime.Generated/Scripts/Protobuf/InputCommand.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/PlayerInputManager.cs`

**Interfaces:**
- Produces: `InputCommand.Dash` (bool), `PlayerInputManager.SetDash(bool)`

- [ ] **Step 1: proto에 필드를 더한다**

```proto
  // Flappy: 대시 버튼. 이산 액션이라 누른 틱에만 참이다(jump와 같은 짝).
  bool dash = 10;
```

- [ ] **Step 2: proto를 다시 굽는다** — LOP-Shared의 protoc 스크립트로. 생성물과 `.meta`를 함께 커밋한다.

- [ ] **Step 3: `PlayerInputManager`에 대시를 더한다**

`pendingJump` 옆에 `pendingDash`를 두고 같은 수명으로 다룬다 — 커맨드에 실은 뒤 `pendingJump = false` 자리에서 함께 리셋한다.

```csharp
        private bool pendingDash;        // 이산 액션 — 소비 후 리셋

        public void SetDash(bool dash)
        {
            pendingDash = dash;
        }
```

`new InputCommand { … }`에 `Dash = pendingDash,`를 더하고, 리셋 자리에 `pendingDash = false;`를 더한다.

> **주의:** 같은 파일에 `AbilitySystem.HasActiveMotionEffect`를 "대시 등 조작 불가 상태"라고 부르는 주석이 있다. 그것은 **어빌리티의 이동 효과**이지 이 대시가 아니다. 헷갈리지 않게 그 주석의 "대시"를 "밀려나는 중" 같은 말로 바꿔 둔다.

- [ ] **Step 4: 서버가 그 값을 보는지 확인한다**

서버는 proto → `InputCommand` → `InputBuffer`로 이미 흘리고 있다(자세·활공이 그 경로로 온다). 별도 변경이 없어야 정상이며, **없다는 것을 확인한다** — 있으면 그 자리도 고친다.

- [ ] **Step 5: 컴파일 확인 + 커밋** (Shared → Client)

---

### Task 7: 서버 권위와 보정

**Files:**
- Modify: `LeagueOfPhysical-Shared/Protos/EntitySnap.proto`
- Regenerate: `LeagueOfPhysical-Shared/Runtime.Generated/Scripts/Protobuf/EntitySnap.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/EntitySnapshotBroadcastSystem.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Netcode/FlappyServerCorrectionHandler.cs`
- Test: `LeagueOfPhysical-Client` EditMode — `FlappyServerCorrectionHandler`의 순수 비교 함수

**Interfaces:**
- Consumes: `FlappyTickDuration`, `FlappySavedState.DashRemaining/.Charge`
- Produces: `EntitySnap.DashEndTick` (int64), `EntitySnap.DashCharge` (float)

- [ ] **Step 1: proto에 두 필드를 더한다**

```proto
	int64 dash_end_tick = 20;       // Flappy: 대시가 끝나는 절대 틱. 0 = 대시 중 아님
	float dash_charge   = 21;       // Flappy: 대시 게이지 0~1
```

> **`EntitySnap`이 두 개다.** 서버가 채우는 것은 **proto 생성 타입**(프로퍼티가 `DashEndTick`, 대문자)이고, 클라 보정이 읽는 것은 **클라 도메인 클래스** `Client/Assets/Scripts/Netcode/EntitySnap.cs`(필드가 `dashEndTick`, 소문자)다. 수신 어댑터가 앞의 것을 뒤의 것으로 옮기므로 **그 변환 자리에도 두 필드를 더해야 한다** — 빠뜨리면 값이 조용히 0으로 남는다.

- [ ] **Step 2: 서버가 채우고, 수신 어댑터가 클라 타입으로 옮긴다**

```csharp
                var dash = worldEntity.Get<FlappyDash>();
                snap.DashEndTick = FlappyTickDuration.EndTick(dash?.DashRemaining ?? 0f, tick, deltaTime);
                snap.DashCharge = dash?.Charge ?? 0f;
```

- [ ] **Step 3: 보정 비교에 대시를 더하는 실패 테스트를 쓴다**

`StunMatches`와 같은 모양의 순수 static을 하나 더 만들고 그것을 테스트한다.

```csharp
        [Test]
        public void 예측은_대시_중인데_서버는_아니면_어긋난_것이다()
        {
            Assert.IsFalse(FlappyServerCorrectionHandler.DashMatches(
                predictedDashRemaining: 0.1f, snapDashEndTick: 0L, tick: 1000L));
        }

        [Test]
        public void 둘_다_대시_중이면_남은_시간이_달라도_맞는_것이다()
        {
            //  미세한 차이는 다음 틱 시뮬이 좁힌다 — 스턴과 같은 관례다.
            Assert.IsTrue(FlappyServerCorrectionHandler.DashMatches(
                predictedDashRemaining: 0.02f, snapDashEndTick: 1008L, tick: 1000L));
        }

        [Test]
        public void 둘_다_대시가_아니면_맞는_것이다()
        {
            Assert.IsTrue(FlappyServerCorrectionHandler.DashMatches(0f, 0L, 1000L));
        }
```

- [ ] **Step 4: 실패 확인 → 구현**

```csharp
        /// <summary>켜짐/꺼짐만 본다. 남은 시간의 미세한 차이는 다음 틱 시뮬이 좁힌다.</summary>
        public static bool DashMatches(float predictedDashRemaining, long snapDashEndTick, long tick)
        {
            return (predictedDashRemaining > 0f) == (snapDashEndTick > tick);
        }
```

`Matches`에서 `TryGetSavedState`로 꺼낸 상태의 `DashRemaining`으로 이 함수를 함께 호출해, **스턴과 대시 둘 다 맞아야** 참을 돌려준다.

`ApplyAuthoritative`에서는 스턴처럼 대시도 되돌린다:

```csharp
            var dash = entity.Get<FlappyDash>();
            if (dash != null)
            {
                dash.DashRemaining = FlappyTickDuration.RemainingSeconds(snap.dashEndTick, snap.tick, deltaTime);
                dash.Charge = snap.dashCharge;
            }
```

- [ ] **Step 5: 테스트 통과 확인 + 일부러 깨뜨려 빨강 확인**

`DashMatches`를 `return true;`로 바꿔 본다 → 첫 테스트가 빨강이어야 한다.

- [ ] **Step 6: 커밋** (Shared → Server → Client)

---

### Task 8: 대시 버튼

**Files:**
- Modify: `LeagueOfPhysical-Client/Assets/UI/FlapPad/FlapPad.uxml`
- Modify: `LeagueOfPhysical-Client/Assets/UI/FlapPad/FlapPad.uss`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/UI/FlapPad/FlapPadView.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/UI/FlapPad/FlapPadViewModel.cs`

**Interfaces:**
- Consumes: `PlayerInputManager.SetDash`, `FlappyDash.Charge` (월드에서 pull)
- Produces: 없음(화면 끝단)

- [ ] **Step 1: UXML에 버튼을 더한다**

`flap-surface` **뒤에** 두어 위에 얹힌다. 채움을 표현할 자식을 하나 둔다.

```xml
        <ui:VisualElement name="dash-button" class="dash-button">
            <ui:VisualElement name="dash-fill" class="dash-fill" picking-mode="Ignore" />
            <ui:Label name="dash-label" class="dash-label" text="대시" picking-mode="Ignore" />
        </ui:VisualElement>
```

- [ ] **Step 2: ViewModel이 게이지를 읽고 대시를 보낸다**

```csharp
        /// <summary>대시 게이지 0~1. 매 틱 변하는 연속값이라 R3가 아니라 그때그때 읽는다
        /// (아키텍처 가이드라인: 월드의 연속 상태는 pull, 이산 사건만 event).</summary>
        public float DashCharge
        {
            get
            {
                var entity = _entityRegistry.Get(_gameDataStore.userEntityId);
                return entity?.Get<FlappyDash>()?.Charge ?? 0f;
            }
        }

        public bool CanDash => DashCharge >= 1f;

        public void Dash() => _playerInputManager.SetDash(true);
```

`EntityRegistry`와 `IGameDataStore`를 생성자로 받는다. 클래스 XML 주석의 "표시할 라이브 상태가 없다"는 문장을 실제에 맞게 고친다.

- [ ] **Step 3: View가 버튼을 잇는다**

- `dash-button`에 `PointerDownEvent` 등록 → `if (_viewModel.CanDash) _viewModel.Dash();`
- **같은 콜백에서 `evt.StopPropagation()`** — 안 하면 대시할 때마다 날갯짓도 같이 나간다
- 매 프레임 갱신에서 `dash-fill`의 높이(또는 너비)를 `DashCharge * 100%`로 두고, `CanDash`에 따라 `dash-button--ready` 클래스를 붙였다 뗀다
- 키보드 폴링에 Shift/D를 더한다(`PollKeyboard`가 이미 매 프레임 돈다)

- [ ] **Step 4: 눈으로 확인한다**

에디터에서 한 판 띄워: ① 게이지가 떨어질 때 눈에 띄게 빨리 찬다 ② 가득 차기 전엔 눌러도 아무 일 없다 ③ 가득 차면 밝아지고, 누르면 앞으로 쭉 나가며 게이지가 0이 된다 ④ **버튼을 눌렀을 때 새가 위로 뜨지 않는다**(이벤트가 안 샌다).

- [ ] **Step 5: 커밋** (Client 한 레포)

---

### Task 9: 두 클라로 확인한다

코드가 아니라 **확인**이 산출물이다. 로컬 2인 리그로 한 판 돌린다.

- [ ] **Step 1: 8레포 최신·컴파일 확인**

`git status`가 로컬 픽스처만 남았는지, 양쪽 프로젝트가 깨끗이 컴파일되는지.

- [ ] **Step 2: 한 판 돌린다** (서버 환경 local, 두 클라)

- [ ] **Step 3: 아래를 확인한다**

- 내 대시가 **즉시** 나간다(예측이 도는가)
- 대시 뒤 **되돌아가지 않는다**(보정이 안 싸우는가). `DebugHud`의 되돌리기 횟수가 평소 수준인가
- 남의 새도 대시하는 것이 보인다
- 대시로 파이프에 박으면 **평소와 같이 멈춘다**
- 결승 로그의 **깊이가 서로 달라진다** — 대시가 실제로 등수를 가르기 시작했다는 신호다

- [ ] **Step 4: 결과를 로드맵에 적는다**

`docs/ROADMAP.md`의 닫힌 항목에 한 줄. 특히 **대시가 등수를 실제로 갈랐는지**를 수치와 함께 남긴다 — 안 갈렸다면 spec의 열린 항목대로 **충전 속도**부터 본다.

- [ ] **Step 5: 커밋** (Client 한 레포 — 로드맵)
