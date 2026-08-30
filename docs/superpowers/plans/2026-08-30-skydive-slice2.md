# Skydive 슬라이스 2 — 자세와 스태미나

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 떨어지는 것을 **조종할 수 있게** 만든다 — 가로 슬라이더로 다이브/대자/패러세일을 고르고,
패러세일은 스태미나를 먹는다.

**Architecture:** 자세는 `Posture`(축 0~1 + 패러세일 bool), 자원은 `Stamina` — 둘 다 World Core
컴포넌트라 롤백에 저절로 실린다. 이동은 **"자세가 목표 속도를 정하고 실제 속도가 그리로 수렴"** 하는
모델이라 자세가 바뀌어도 속도가 튀지 않는다(넷코드 이득). 튜닝값은 `TbSkydiveConfig` 한 행에서 온다.

**Tech Stack:** Unity 6 / VContainer / R3 / UI Toolkit / Mirror + protobuf / Luban(MasterData) / NUnit

**Spec:** `docs/superpowers/specs/2026-08-30-skydive-game-mode-design.md`

## Global Constraints

- **게임 모드 내부명은 `Skydive`.** 자세 값 이름에 `Skydive`를 쓰지 않는다 — `Posture.Axis`(0=대자,
  1=다이브)와 `Posture.Gliding`(패러세일)만 쓴다.
- **시뮬 코드는 LOP-Shared에 구체 클래스로.** 인터페이스 seam 금지(결정론).
- **World 타입은 항상 풀 네임스페이스 한정** — `GameFramework.World.Transform` 등. 런타임 코드에
  `using GameFramework.World;` 금지(`UnityEngine.Component`와 충돌). 테스트 파일은 예외.
- **클·서가 같은 값을 써야 하는 상수는 양쪽에 복제하지 않는다** — `TbSkydiveConfig` 한 곳에서 온다.
- **`git add -A` / `git commit -a` 금지.** 각 레포에 의도적으로 커밋하지 않는 로컬 픽스처가 상시 있다.
  커밋 전 `git status --short`로 스테이지된 것이 의도한 파일뿐인지 확인한다.
- **`.cs`/`.uxml`/`.uss` 신규 시 유니티가 만든 `.meta`를 함께 커밋.** 직접 만들지 않는다.
- **main 직접 커밋 금지.** 각 레포 `feature/skydive-slice2`.

### 슬라이스 2가 하지 않는 것

레이저 · 경계 · 결승선 · 맵 충돌 · 체크포인트. 그리고 **스태미나 회복은 "발판에 서면"이 조건인데
발판(강체)은 슬라이스 3**이다 — 이 슬라이스에서는 회복 경로를 만들되 임시 바닥(`GroundY`) 접촉에
물린다(슬라이스 3이 그 자리를 진짜 지면 접촉으로 바꾼다).

**스펙 §2.4("다이브 선회 반경보다 좁은 틈이 코스에 있어야 한다")도 슬라이스 3으로 이월한다** — 검사할 코스가 아직 없다. 대신 그 검사가 쓸 재료(자세별 수평 가속·최고 속도)를 이 슬라이스가 config로 노출하므로, 슬라이스 3은 값을 새로 만들지 않고 읽어 쓰면 된다.

### 튜닝 시작값과 지켜야 할 부등식

| | 하강(m/s) | 수평 최대(m/s) | 수평 가속(m/s²) | 높이당 수평(활공비) |
|---|---|---|---|---|
| 대자 Spread | 25 | 12 | 22 | 0.48 |
| 다이브 Dive | 45 | 18 | 6 | 0.40 |
| 패러세일 Glide | 6 | 14 | 18 | **2.33** |

**세 부등식이 스펙 §2.1이다 — 테스트로 박는다:**
- 하강 속도: `Dive > Spread > Glide`
- **높이당 수평 거리: `Glide > Spread > Dive`**
- 수평 가속(선회): `Spread > Glide > Dive` (대자가 1등)

값은 시작점이고 손맛으로 바꾼다. **바꾼 뒤에도 부등식은 테스트가 지킨다.**

### 컴파일·테스트 게이트 (매 태스크 공통)

```bash
CLIENT=C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
SERVER=C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server

unity command recompile        --project-path "$CLIENT"
unity command recompile_status --project-path "$CLIENT"
unity command get_console_logs --severity error --limit 40 --project-path "$CLIENT"
unity command run_tests  --mode EditMode --async_tests true --project-path "$CLIENT"
unity command test_status --project-path "$CLIENT"
```

> ⚠️ **`recompile_status`의 `failed:false`만 보면 안 된다.** `status`가 `up_to_date`면 **재컴파일을
> 아예 안 한 것**이다. 다시 부르고, 최종 판정은 콘솔의 CS 에러 유무로 한다.
> ⚠️ CLI 응답은 30초 상한이고 `--timeout`이 안 먹는다. 타임아웃이 떠도 에디터에선 계속 돈다 —
> `*_status`로 폴링한다.
> ⚠️ 서버 코드를 고쳤으면 `--project-path "$SERVER"`로도 같은 확인을 한다.

---

## File Structure

| 파일 | 책임 |
|---|---|
| **LOP-Shared** | |
| `Runtime/Scripts/Game/Posture.cs` | 자세 상태(축 + 패러세일). 데이터만 |
| `Runtime/Scripts/Game/Stamina.cs` | 자원 상태(현재량 + 비상 펼침 사용 여부). 데이터만 |
| `Runtime/Scripts/Game/SkydiveConfig.cs` | 튜닝값 struct (마스터데이터에서 채워 주입) |
| `Runtime/Scripts/Game/StaminaSystem.cs` | 소모·회복·0 처리·비상 펼침 |
| `Runtime/Scripts/Game/SkydiveMoveSystem.cs` | **수정** — 자세가 목표 속도를 정하고 그리로 수렴 |
| `Runtime/Scripts/Game/SkydiveWorld.cs` | **수정** — 입력→자세 반영, 스태미나 틱, 롤백 훅 |
| `Runtime/Scripts/Game/SkydiveSavedState.cs` | 롤백 스냅샷(자세·스태미나) |
| `Runtime/Scripts/Game/InputCommand.cs` | **수정** — `Posture`(float), `Glide`(bool) |
| `Protos/InputCommand.proto` | **수정** — 필드 7, 8 |
| **LOP-Client** | |
| `Assets/Scripts/Game/SkydiveConfigProvider.cs` | `TbSkydiveConfig` → `SkydiveConfig` |
| `Assets/Scripts/Game/PlayerInputManager.cs` | **수정** — `SetPosture`/`SetGlide` + 커맨드·proto에 싣기 |
| `Assets/Scripts/UI/SkydivePad/SkydivePadViewModel.cs` | 슬라이더 값 → `PlayerInputManager` |
| `Assets/Scripts/UI/SkydivePad/SkydivePadView.cs` | UXML 트리 소유 + 터치 처리(얇은 바인더) |
| `Assets/UI/SkydivePad/SkydivePad.uxml` / `.uss` | 방향 스틱 + 가로 슬라이더 + 스태미나 막대 |
| `Assets/Scripts/Game/SkydiveHudCoordinator.cs` | 패드 화면을 열고 닫는다 |
| `Assets/Scripts/Game/SkydiveLifetimeScope.cs` | **수정** — config·시스템·UI 등록 |
| `Assets/Scripts/Entity/SkydivePlayerCreator.cs` | **수정** — `Posture`/`Stamina` 부착, 몸 크기를 config에서 |
| **LOP-Server** | |
| `Assets/Scripts/Game/SkydiveConfigProvider.cs` | 서버 쪽 같은 어댑터 |
| `Assets/Scripts/Game/SkydiveLifetimeScope.cs` | **수정** |
| `Assets/Scripts/Entity/SkydivePlayerCreator.cs` | **수정** |
| `Assets/Scripts/Game/MessageHandler/Game.Input.MessageHandler.cs` | **수정** — proto→도메인에 새 필드 |
| **infrastructure** | |
| `table/Datas/#SkydiveConfig.xlsx` | 신규 테이블 |
| `table/Datas/__tables__.xlsx` | **신규 테이블 등록(빠뜨리면 로더가 안 읽는다)** |

---

## Task 1: 자세·스태미나 데이터와 이동 커널

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/Posture.cs`
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/Stamina.cs`
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveConfig.cs`
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/StaminaSystem.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveMoveSystem.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/SkydiveMoveSystemTests.cs` (기존 파일 교체)
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/StaminaSystemTests.cs`

**Interfaces:**
- Consumes: `GameFramework.World.{Entity, Component, Transform, Velocity}`, `LOP.InputBuffer`, `LOP.InputCommand`
- Produces:
  - `LOP.Posture : GameFramework.World.Component` — `float Axis` (0=대자, 1=다이브), `bool Gliding`
  - `LOP.Stamina : GameFramework.World.Component` — `float Current`, `bool EmergencyUsed`, `float EmergencyRemaining`
  - `LOP.SkydiveConfig` — readonly struct, 아래 ctor
  - `LOP.StaminaSystem` — `void Tick(Entity, float dt, in SkydiveConfig, bool grounded)`, `bool TryStartGlide(Entity, in SkydiveConfig)`
  - `LOP.SkydiveMoveSystem` — `void Tick(Entity, float dt, in SkydiveConfig)` (시그니처 변경)

- [ ] **Step 1: 실패하는 테스트를 쓴다 — 이동**

`Tests/EditMode/SkydiveMoveSystemTests.cs` **전체를 이 내용으로 교체**한다(슬라이스 1의 5개는
`GroundY` 상수 기반이라 시그니처가 바뀌면 컴파일이 안 된다):

```csharp
using GameFramework;
using GameFramework.World;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class SkydiveMoveSystemTests
    {
        const float Tolerance = 1e-3f;

        static SkydiveConfig Config()
            => new SkydiveConfig(
                spreadFallSpeed: 25f, diveFallSpeed: 45f, glideFallSpeed: 6f,
                spreadMoveSpeed: 12f, diveMoveSpeed: 18f, glideMoveSpeed: 14f,
                spreadTurnAccel: 22f, diveTurnAccel: 6f, glideTurnAccel: 18f,
                fallApproach: 30f, postureRate: 4f,
                bodyRadius: 0.4f, bodyHeight: 1.8f, groundY: 0f,
                staminaMax: 100f, glideDrain: 20f, groundRecover: 40f, emergencyGlideTime: 1f);

        static Entity Diver(float axis, bool gliding, Vector3 velocity, Vector3 position,
                            float h = 0f, float v = 0f)
        {
            var entity = new Entity("diver-1");
            entity.Add(new GameFramework.World.Transform { Position = position.ToNumerics() });
            entity.Add(new Velocity { Linear = velocity.ToNumerics() });
            entity.Add(new Posture { Axis = axis, Gliding = gliding });
            var buffer = new InputBuffer();
            buffer.Current = new InputCommand { Horizontal = h, Vertical = v };
            entity.Add(buffer);
            return entity;
        }

        static Vector3 VelocityOf(Entity e) => e.Get<Velocity>().Linear.ToUnity();
        static Vector3 PositionOf(Entity e) => e.Get<GameFramework.World.Transform>().Position.ToUnity();

        // 한 자세로 오래 굴려 정상 상태(목표 속도에 수렴한 뒤)의 하강·수평 속도를 잰다.
        static (float fall, float side) Settle(float axis, bool gliding)
        {
            var e = Diver(axis, gliding, Vector3.zero, new Vector3(0f, 1000f, 0f), h: 1f);
            var sys = new SkydiveMoveSystem();
            for (int i = 0; i < 600; i++) { sys.Tick(e, 0.02f, Config()); }
            var vel = VelocityOf(e);
            return (-vel.y, new Vector2(vel.x, vel.z).magnitude);
        }

        [Test]
        public void 하강_속도는_다이브가_가장_빠르고_패러세일이_가장_느리다()
        {
            float dive = Settle(1f, false).fall;
            float spread = Settle(0f, false).fall;
            float glide = Settle(0f, true).fall;

            Assert.Greater(dive, spread, "다이브가 대자보다 빨라야 한다");
            Assert.Greater(spread, glide, "대자가 패러세일보다 빨라야 한다");
        }

        [Test]
        public void 높이당_수평거리는_패러세일_대자_다이브_순이다()
        {
            var dive = Settle(1f, false);
            var spread = Settle(0f, false);
            var glide = Settle(0f, true);

            float diveRatio = dive.side / dive.fall;
            float spreadRatio = spread.side / spread.fall;
            float glideRatio = glide.side / glide.fall;

            Assert.Greater(glideRatio, spreadRatio, "패러세일이 대자보다 멀리 가야 한다");
            Assert.Greater(spreadRatio, diveRatio, "대자가 다이브보다 멀리 가야 한다");
        }

        [Test]
        public void 선회는_대자가_가장_민첩하다()
        {
            // 정지 상태에서 한 틱만 굴려 수평 가속의 크기를 비교한다.
            float Accel(float axis, bool gliding)
            {
                var e = Diver(axis, gliding, Vector3.zero, new Vector3(0f, 1000f, 0f), h: 1f);
                new SkydiveMoveSystem().Tick(e, 0.02f, Config());
                var vel = VelocityOf(e);
                return new Vector2(vel.x, vel.z).magnitude;
            }

            Assert.Greater(Accel(0f, false), Accel(0f, true), "대자가 패러세일보다 민첩해야 한다");
            Assert.Greater(Accel(0f, true), Accel(1f, false), "패러세일이 다이브보다 민첩해야 한다");
        }

        [Test]
        public void 임시_바닥에_닿으면_멈춘다()
        {
            var e = Diver(1f, false, new Vector3(0f, -40f, 0f), new Vector3(0f, 0.3f, 0f));

            new SkydiveMoveSystem().Tick(e, 0.1f, Config());

            Assert.AreEqual(0f, PositionOf(e).y, Tolerance);
            Assert.AreEqual(0f, VelocityOf(e).y, Tolerance);
        }

        [Test]
        public void 입력이_없으면_수평_속도가_줄어든다()
        {
            var e = Diver(0f, false, new Vector3(10f, 0f, 0f), new Vector3(0f, 1000f, 0f), h: 0f);

            new SkydiveMoveSystem().Tick(e, 0.1f, Config());

            Assert.Less(VelocityOf(e).x, 10f, "입력이 없으면 목표가 0이라 감속해야 한다");
        }

        [Test]
        public void 자세가_바뀌어도_속도가_한_틱에_튀지_않는다()
        {
            // 대자로 수렴시킨 뒤 다이브로 바꿔 한 틱 — 하강 속도 변화가 목표 차이보다 훨씬 작아야 한다.
            var e = Diver(0f, false, Vector3.zero, new Vector3(0f, 1000f, 0f));
            var sys = new SkydiveMoveSystem();
            for (int i = 0; i < 600; i++) { sys.Tick(e, 0.02f, Config()); }

            float before = -VelocityOf(e).y;
            e.Get<Posture>().Axis = 1f;
            sys.Tick(e, 0.02f, Config());
            float after = -VelocityOf(e).y;

            Assert.Less(after - before, 5f, "한 틱에 목표까지 점프하면 안 된다 (수렴이어야 한다)");
            Assert.Greater(after, before, "그래도 빨라지는 방향이어야 한다");
        }
    }
}
```

- [ ] **Step 2: 실패하는 테스트를 쓴다 — 스태미나**

`Tests/EditMode/StaminaSystemTests.cs`:

```csharp
using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    public class StaminaSystemTests
    {
        const float Tolerance = 1e-3f;

        static SkydiveConfig Config()
            => new SkydiveConfig(
                spreadFallSpeed: 25f, diveFallSpeed: 45f, glideFallSpeed: 6f,
                spreadMoveSpeed: 12f, diveMoveSpeed: 18f, glideMoveSpeed: 14f,
                spreadTurnAccel: 22f, diveTurnAccel: 6f, glideTurnAccel: 18f,
                fallApproach: 30f, postureRate: 4f,
                bodyRadius: 0.4f, bodyHeight: 1.8f, groundY: 0f,
                staminaMax: 100f, glideDrain: 20f, groundRecover: 40f, emergencyGlideTime: 1f);

        static Entity Diver(float stamina, bool gliding)
        {
            var e = new Entity("diver-1");
            e.Add(new Posture { Axis = 0f, Gliding = gliding });
            e.Add(new Stamina { Current = stamina });
            return e;
        }

        [Test]
        public void 패러세일을_켜면_줄어든다()
        {
            var e = Diver(100f, gliding: true);

            new StaminaSystem().Tick(e, 0.5f, Config(), grounded: false);

            Assert.AreEqual(90f, e.Get<Stamina>().Current, Tolerance);   // 20/s × 0.5s
        }

        [Test]
        public void 자유낙하는_공짜다()
        {
            var e = Diver(100f, gliding: false);

            new StaminaSystem().Tick(e, 1f, Config(), grounded: false);

            Assert.AreEqual(100f, e.Get<Stamina>().Current, Tolerance);
        }

        [Test]
        public void 공중에서는_회복되지_않는다()
        {
            var e = Diver(10f, gliding: false);

            new StaminaSystem().Tick(e, 1f, Config(), grounded: false);

            Assert.AreEqual(10f, e.Get<Stamina>().Current, Tolerance);
        }

        [Test]
        public void 발_딛고_있으면_회복된다()
        {
            var e = Diver(10f, gliding: false);

            new StaminaSystem().Tick(e, 0.5f, Config(), grounded: true);

            Assert.AreEqual(30f, e.Get<Stamina>().Current, Tolerance);   // 40/s × 0.5s
        }

        [Test]
        public void 다_떨어지면_패러세일이_저절로_접힌다()
        {
            var e = Diver(5f, gliding: true);

            new StaminaSystem().Tick(e, 1f, Config(), grounded: false);

            Assert.AreEqual(0f, e.Get<Stamina>().Current, Tolerance);
            Assert.IsFalse(e.Get<Posture>().Gliding, "잔고가 0이면 접혀야 한다");
        }

        [Test]
        public void 잔고가_0이어도_마지막_펼침이_한_번_허용된다()
        {
            var e = Diver(0f, gliding: false);
            var sys = new StaminaSystem();

            Assert.IsTrue(sys.TryStartGlide(e, Config()), "첫 비상 펼침은 허용된다");
            Assert.IsTrue(e.Get<Posture>().Gliding);

            // 비상 시간이 끝나면 접힌다
            sys.Tick(e, 1.1f, Config(), grounded: false);
            Assert.IsFalse(e.Get<Posture>().Gliding);

            Assert.IsFalse(sys.TryStartGlide(e, Config()), "두 번째는 허용되지 않는다");
        }

        [Test]
        public void 잔고가_있으면_비상_횟수를_쓰지_않는다()
        {
            var e = Diver(50f, gliding: false);
            var sys = new StaminaSystem();

            Assert.IsTrue(sys.TryStartGlide(e, Config()));

            Assert.IsFalse(e.Get<Stamina>().EmergencyUsed, "잔고로 폈으면 비상 횟수는 그대로다");
        }
    }
}
```

- [ ] **Step 3: 테스트가 실패하는지 확인한다**

```bash
unity command run_tests  --mode EditMode --async_tests true --project-path "$CLIENT"
unity command test_status --project-path "$CLIENT"
```

기대: 컴파일 에러 — `Posture`/`Stamina`/`SkydiveConfig`/`StaminaSystem`이 없고
`SkydiveMoveSystem.Tick`이 3인자가 아니다.

- [ ] **Step 4: 컴포넌트 둘을 만든다**

`Runtime/Scripts/Game/Posture.cs`:

```csharp
namespace LOP
{
    /// <summary>
    /// 지금 어떤 자세로 떨어지고 있나. 데이터만 — 바꾸는 것은 <see cref="SkydiveWorld"/>와
    /// <see cref="StaminaSystem"/>이다.
    /// </summary>
    public class Posture : GameFramework.World.Component
    {
        /// <summary>0이면 대자(팔다리 벌림), 1이면 완전한 다이브(머리부터). 사이는 연속이다.</summary>
        public float Axis;

        /// <summary>패러세일을 펼쳤나. 자세 축과 무관한 별개 도구라 bool이다.</summary>
        public bool Gliding;
    }
}
```

`Runtime/Scripts/Game/Stamina.cs`:

```csharp
namespace LOP
{
    /// <summary>
    /// 한 판 동안 쓸 수 있는 활공 총량. 패러세일만 이걸 먹고, 자유낙하는 공짜다.
    /// </summary>
    public class Stamina : GameFramework.World.Component
    {
        public float Current;

        /// <summary>잔고 0에서의 "마지막 한 번" 펼침을 이미 썼나.</summary>
        public bool EmergencyUsed;

        /// <summary>그 마지막 펼침이 끝나기까지 남은 시간(초). 0이면 비상 상태가 아니다.</summary>
        public float EmergencyRemaining;
    }
}
```

- [ ] **Step 5: 튜닝 struct를 만든다**

`Runtime/Scripts/Game/SkydiveConfig.cs`:

```csharp
namespace LOP
{
    /// <summary>
    /// Skydive 튜닝값. MasterData <c>TbSkydiveConfig</c>에서 사이드 provider가 채워 시뮬에 주입한다.
    /// Shared는 MasterData 패키지를 참조하지 않으므로 순수 struct로 건네받는다(<see cref="FlappyConfig"/>와 같은 짝).
    /// </summary>
    public readonly struct SkydiveConfig
    {
        /// <summary>대자로 안정됐을 때의 하강 속도(양수).</summary>
        public readonly float SpreadFallSpeed;
        /// <summary>완전한 다이브의 하강 속도(양수). 가장 크다.</summary>
        public readonly float DiveFallSpeed;
        /// <summary>패러세일의 하강 속도(양수). 가장 작다.</summary>
        public readonly float GlideFallSpeed;

        /// <summary>대자의 수평 최고 속도.</summary>
        public readonly float SpreadMoveSpeed;
        /// <summary>다이브의 수평 최고 속도.</summary>
        public readonly float DiveMoveSpeed;
        /// <summary>패러세일의 수평 최고 속도.</summary>
        public readonly float GlideMoveSpeed;

        /// <summary>대자의 수평 가속 — 방향을 얼마나 빨리 바꾸나. 셋 중 가장 크다(대자가 제일 민첩).</summary>
        public readonly float SpreadTurnAccel;
        /// <summary>다이브의 수평 가속. 가장 작다 — 빠른 대신 못 꺾는다.</summary>
        public readonly float DiveTurnAccel;
        /// <summary>패러세일의 수평 가속.</summary>
        public readonly float GlideTurnAccel;

        /// <summary>실제 하강 속도가 자세의 목표 속도로 다가가는 가속(m/s²). 자세를 바꿔도 속도가 튀지 않게 한다.</summary>
        public readonly float FallApproach;
        /// <summary>자세 축이 1초에 바뀔 수 있는 양. 4면 0↔1 전환에 0.25초가 걸린다.</summary>
        public readonly float PostureRate;

        /// <summary>몸 캡슐 반지름. 클·서가 같은 값을 써야 한다.</summary>
        public readonly float BodyRadius;
        /// <summary>몸 캡슐 전체 높이.</summary>
        public readonly float BodyHeight;
        /// <summary>임시 바닥 높이. 슬라이스 3의 맵 충돌이 이 자리를 대체한다.</summary>
        public readonly float GroundY;

        /// <summary>스태미나 최대치.</summary>
        public readonly float StaminaMax;
        /// <summary>패러세일을 켜 둔 동안 초당 줄어드는 양.</summary>
        public readonly float GlideDrain;
        /// <summary>발 딛고 있을 때 초당 차는 양. 공중에서는 차지 않는다.</summary>
        public readonly float GroundRecover;
        /// <summary>잔고 0에서 허용되는 마지막 펼침의 지속 시간(초).</summary>
        public readonly float EmergencyGlideTime;

        public SkydiveConfig(
            float spreadFallSpeed, float diveFallSpeed, float glideFallSpeed,
            float spreadMoveSpeed, float diveMoveSpeed, float glideMoveSpeed,
            float spreadTurnAccel, float diveTurnAccel, float glideTurnAccel,
            float fallApproach, float postureRate,
            float bodyRadius, float bodyHeight, float groundY,
            float staminaMax, float glideDrain, float groundRecover, float emergencyGlideTime)
        {
            SpreadFallSpeed = spreadFallSpeed;
            DiveFallSpeed = diveFallSpeed;
            GlideFallSpeed = glideFallSpeed;
            SpreadMoveSpeed = spreadMoveSpeed;
            DiveMoveSpeed = diveMoveSpeed;
            GlideMoveSpeed = glideMoveSpeed;
            SpreadTurnAccel = spreadTurnAccel;
            DiveTurnAccel = diveTurnAccel;
            GlideTurnAccel = glideTurnAccel;
            FallApproach = fallApproach;
            PostureRate = postureRate;
            BodyRadius = bodyRadius;
            BodyHeight = bodyHeight;
            GroundY = groundY;
            StaminaMax = staminaMax;
            GlideDrain = glideDrain;
            GroundRecover = groundRecover;
            EmergencyGlideTime = emergencyGlideTime;
        }
    }
}
```

- [ ] **Step 6: `StaminaSystem`을 만든다**

`Runtime/Scripts/Game/StaminaSystem.cs`:

```csharp
namespace LOP
{
    /// <summary>
    /// 활공 자원의 소모·회복. 젤다 규칙 그대로 — 자유낙하는 공짜, 패러세일만 먹고,
    /// 회복은 발 딛고 있을 때만. 잔고가 0이어도 "마지막 한 번"은 펼 수 있다(착지 직전 구제).
    /// </summary>
    public class StaminaSystem
    {
        public void Tick(GameFramework.World.Entity entity, float deltaTime,
                         in SkydiveConfig config, bool grounded)
        {
            var stamina = entity.Get<Stamina>();
            var posture = entity.Get<Posture>();
            if (stamina == null || posture == null)
            {
                return;
            }

            // 비상 펼침 중이면 잔고가 아니라 남은 시간이 줄고, 다 되면 접힌다.
            if (stamina.EmergencyRemaining > 0f)
            {
                stamina.EmergencyRemaining -= deltaTime;
                if (stamina.EmergencyRemaining <= 0f)
                {
                    stamina.EmergencyRemaining = 0f;
                    posture.Gliding = false;
                }
                return;
            }

            if (posture.Gliding)
            {
                stamina.Current -= config.GlideDrain * deltaTime;
                if (stamina.Current <= 0f)
                {
                    stamina.Current = 0f;
                    posture.Gliding = false;   // 손에서 놓아진다
                }
                return;
            }

            if (grounded)
            {
                stamina.Current += config.GroundRecover * deltaTime;
                if (stamina.Current > config.StaminaMax)
                {
                    stamina.Current = config.StaminaMax;
                }
            }
            // 공중에서 안 펴고 있으면 아무 일도 없다 — 젤다도 공중에선 안 찬다.
        }

        /// <summary>
        /// 패러세일을 펴려는 시도. 잔고가 있으면 그냥 펴고, 0이면 "마지막 한 번"을 쓴다.
        /// 이미 그 한 번을 썼으면 거절한다.
        /// </summary>
        public bool TryStartGlide(GameFramework.World.Entity entity, in SkydiveConfig config)
        {
            var stamina = entity.Get<Stamina>();
            var posture = entity.Get<Posture>();
            if (stamina == null || posture == null)
            {
                return false;
            }

            if (stamina.Current > 0f)
            {
                posture.Gliding = true;
                return true;
            }

            if (stamina.EmergencyUsed)
            {
                return false;
            }

            stamina.EmergencyUsed = true;
            stamina.EmergencyRemaining = config.EmergencyGlideTime;
            posture.Gliding = true;
            return true;
        }
    }
}
```

- [ ] **Step 7: `SkydiveMoveSystem`을 자세 기반으로 바꾼다**

`Runtime/Scripts/Game/SkydiveMoveSystem.cs` **전체를 교체**한다:

```csharp
namespace LOP
{
    /// <summary>
    /// Skydive의 이동. 자세가 <b>목표</b> 하강·수평 속도를 정하고, 실제 속도는 그 목표로 수렴한다 —
    /// 자세를 바꿔도 속도가 한 틱에 튀지 않아 남을 예측하는 쪽의 오차가 완만해진다.
    /// 지형 충돌은 슬라이스 3이 얹는다(지금은 임시 바닥만).
    /// </summary>
    public class SkydiveMoveSystem
    {
        public void Tick(GameFramework.World.Entity entity, float deltaTime, in SkydiveConfig config)
        {
            var velocity = entity.Get<GameFramework.World.Velocity>();
            var transform = entity.Get<GameFramework.World.Transform>();
            var posture = entity.Get<Posture>();
            if (velocity == null || transform == null || posture == null)
            {
                return;
            }

            float axis = posture.Axis < 0f ? 0f : (posture.Axis > 1f ? 1f : posture.Axis);

            // 패러세일은 자세 축과 무관한 도구라 축을 덮어쓴다.
            float targetFall = posture.Gliding
                ? config.GlideFallSpeed
                : Lerp(config.SpreadFallSpeed, config.DiveFallSpeed, axis);
            float maxSide = posture.Gliding
                ? config.GlideMoveSpeed
                : Lerp(config.SpreadMoveSpeed, config.DiveMoveSpeed, axis);
            float turnAccel = posture.Gliding
                ? config.GlideTurnAccel
                : Lerp(config.SpreadTurnAccel, config.DiveTurnAccel, axis);

            var linear = velocity.Linear;

            // 세로 — 목표 하강 속도로 수렴한다(중력을 직접 적분하지 않는다).
            linear.Y = Approach(linear.Y, -targetFall, config.FallApproach * deltaTime);

            // 가로 — 입력 방향 × 최고 속도가 목표. 입력이 없으면 목표가 0이라 저절로 감속한다.
            var command = entity.Get<InputBuffer>()?.Current;
            float inputX = command == null ? 0f : command.Horizontal;
            float inputZ = command == null ? 0f : command.Vertical;
            float inputLen = (float)System.Math.Sqrt(inputX * inputX + inputZ * inputZ);
            if (inputLen > 1f)
            {
                inputX /= inputLen;
                inputZ /= inputLen;
            }
            linear.X = Approach(linear.X, inputX * maxSide, turnAccel * deltaTime);
            linear.Z = Approach(linear.Z, inputZ * maxSide, turnAccel * deltaTime);

            var position = transform.Position + linear * deltaTime;
            if (position.Y <= config.GroundY)
            {
                position.Y = config.GroundY;
                linear.Y = 0f;
            }

            velocity.Linear = linear;
            transform.Position = position;
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        // 현재값을 목표로 step만큼 당긴다. 넘어가면 목표에 딱 맞춘다(진동 방지).
        private static float Approach(float current, float target, float step)
        {
            float diff = target - current;
            if (diff > step) return current + step;
            if (diff < -step) return current - step;
            return target;
        }
    }
}
```

- [ ] **Step 8: 테스트가 통과하는지 확인한다**

```bash
unity command recompile        --project-path "$CLIENT"
unity command recompile_status --project-path "$CLIENT"
unity command get_console_logs --severity error --limit 40 --project-path "$CLIENT"
unity command run_tests  --mode EditMode --async_tests true --project-path "$CLIENT"
unity command test_status --project-path "$CLIENT"
```

기대: `SkydiveMoveSystemTests` 6개 + `StaminaSystemTests` 7개 PASS.

> ⚠️ 이 시점에 **`SkydiveWorld`가 컴파일되지 않는다** — `SkydiveMoveSystem.Tick`이 3인자가 됐기
> 때문이다. Task 2가 고친다. 컴파일 에러가 `SkydiveWorld.cs`의 그 한 줄뿐인지 확인하고 넘어가라
> (다른 에러가 있으면 이 태스크의 문제다).

- [ ] **Step 9: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git checkout -b feature/skydive-slice2
git status --short
git add Runtime/Scripts/Game/Posture.cs Runtime/Scripts/Game/Posture.cs.meta \
        Runtime/Scripts/Game/Stamina.cs Runtime/Scripts/Game/Stamina.cs.meta \
        Runtime/Scripts/Game/SkydiveConfig.cs Runtime/Scripts/Game/SkydiveConfig.cs.meta \
        Runtime/Scripts/Game/StaminaSystem.cs Runtime/Scripts/Game/StaminaSystem.cs.meta \
        Runtime/Scripts/Game/SkydiveMoveSystem.cs \
        Tests/EditMode/SkydiveMoveSystemTests.cs \
        Tests/EditMode/StaminaSystemTests.cs Tests/EditMode/StaminaSystemTests.cs.meta
git status --short
git commit -m "feat(skydive): 자세가 속도를 정하고 패러세일이 스태미나를 먹는다"
```

---

## Task 2: 월드 통합과 롤백

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveSavedState.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveWorld.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/SkydiveWorldTests.cs`

**Interfaces:**
- Consumes: Task 1의 `Posture`, `Stamina`, `SkydiveConfig`, `StaminaSystem`, `SkydiveMoveSystem.Tick(Entity, float, in SkydiveConfig)`
- Produces:
  - `LOP.SkydiveSavedState` — `static SkydiveSavedState Capture(Entity)`, `void RestoreTo(Entity)`
  - `LOP.SkydiveWorld` — ctor `(EntityRegistry, WorldEventBuffer, SkydiveMoveSystem, StaminaSystem, SkydiveConfig)` **(인자 2개 추가)**

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Tests/EditMode/SkydiveWorldTests.cs`:

```csharp
using GameFramework;
using GameFramework.World;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class SkydiveWorldTests
    {
        const float Tolerance = 1e-3f;

        static SkydiveConfig Config()
            => new SkydiveConfig(
                spreadFallSpeed: 25f, diveFallSpeed: 45f, glideFallSpeed: 6f,
                spreadMoveSpeed: 12f, diveMoveSpeed: 18f, glideMoveSpeed: 14f,
                spreadTurnAccel: 22f, diveTurnAccel: 6f, glideTurnAccel: 18f,
                fallApproach: 30f, postureRate: 4f,
                bodyRadius: 0.4f, bodyHeight: 1.8f, groundY: 0f,
                staminaMax: 100f, glideDrain: 20f, groundRecover: 40f, emergencyGlideTime: 1f);

        static SkydiveWorld World(EntityRegistry registry)
            => new SkydiveWorld(registry, new WorldEventBuffer(),
                                new SkydiveMoveSystem(), new StaminaSystem(), Config());

        static Entity Diver(string id, bool simulated = true, EntityType kind = EntityType.Character)
        {
            var e = new Entity(id);
            e.Add(new GameFramework.World.Transform { Position = new Vector3(0f, 1000f, 0f).ToNumerics() });
            e.Add(new Velocity());
            e.Add(new EntityKind(kind));
            e.Add(new Posture());
            e.Add(new Stamina { Current = 100f });
            e.Add(new InputBuffer());
            if (simulated) { e.Add(new Simulated()); }
            return e;
        }

        static float HeightOf(EntityRegistry r, string id)
            => r.Get(id).Get<GameFramework.World.Transform>().Position.Y;

        [Test]
        public void 출발_전에는_아무도_움직이지_않는다()
        {
            var registry = new EntityRegistry();
            registry.Add(Diver("a"));
            var world = World(registry);
            world.GameplayStartTick = 100;

            world.Tick(10, 0.02f);

            Assert.AreEqual(1000f, HeightOf(registry, "a"), Tolerance);
        }

        [Test]
        public void Simulated가_없으면_굴리지_않는다()
        {
            var registry = new EntityRegistry();
            registry.Add(Diver("a", simulated: false));
            var world = World(registry);
            world.GameplayStartTick = 0;

            world.Tick(1, 0.02f);

            Assert.AreEqual(1000f, HeightOf(registry, "a"), Tolerance);
        }

        [Test]
        public void 캐릭터가_아니면_굴리지_않는다()
        {
            var registry = new EntityRegistry();
            registry.Add(Diver("a", kind: EntityType.Item));
            var world = World(registry);
            world.GameplayStartTick = 0;

            world.Tick(1, 0.02f);

            Assert.AreEqual(1000f, HeightOf(registry, "a"), Tolerance);
        }

        [Test]
        public void 등록_순서를_뒤집어도_결과가_같다()
        {
            // 이 월드가 존재하는 이유가 결정론이다 — 레지스트리 순회 순서는 정해져 있지 않으므로
            // 처리 순서를 id로 고정한다. 그 고정이 살아 있는지 재는 테스트다.
            float RunWith(string[] order)
            {
                var registry = new EntityRegistry();
                foreach (var id in order) { registry.Add(Diver(id)); }
                var world = World(registry);
                world.GameplayStartTick = 0;
                for (int i = 0; i < 10; i++) { world.Tick(i, 0.02f); }
                return HeightOf(registry, "b");
            }

            Assert.AreEqual(RunWith(new[] { "a", "b", "c" }), RunWith(new[] { "c", "b", "a" }), Tolerance);
        }

        [Test]
        public void 입력이_자세로_반영된다()
        {
            var registry = new EntityRegistry();
            var diver = Diver("a");
            diver.Get<InputBuffer>().Current = new InputCommand { Posture = 1f };
            registry.Add(diver);
            var world = World(registry);
            world.GameplayStartTick = 0;

            for (int i = 0; i < 30; i++) { world.Tick(i, 0.02f); }   // 0.6초 > 전환 0.25초

            Assert.AreEqual(1f, diver.Get<Posture>().Axis, 1e-2f);
        }

        [Test]
        public void 자세_축은_한_틱에_끝까지_가지_않는다()
        {
            var registry = new EntityRegistry();
            var diver = Diver("a");
            diver.Get<InputBuffer>().Current = new InputCommand { Posture = 1f };
            registry.Add(diver);
            var world = World(registry);
            world.GameplayStartTick = 0;

            world.Tick(0, 0.02f);

            Assert.Less(diver.Get<Posture>().Axis, 0.5f, "0.02초에 4×0.02=0.08만 움직여야 한다");
            Assert.Greater(diver.Get<Posture>().Axis, 0f);
        }

        [Test]
        public void 되감으면_자세와_스태미나가_그때로_돌아간다()
        {
            var registry = new EntityRegistry();
            var diver = Diver("a");
            diver.Get<InputBuffer>().Current = new InputCommand { Posture = 1f, Glide = true };
            registry.Add(diver);
            var world = World(registry);
            world.GameplayStartTick = 0;

            world.Tick(0, 0.02f);
            world.SaveState(0);
            float axisAt0 = diver.Get<Posture>().Axis;
            float staminaAt0 = diver.Get<Stamina>().Current;

            for (int i = 1; i <= 20; i++) { world.Tick(i, 0.02f); world.SaveState(i); }
            Assert.AreNotEqual(axisAt0, diver.Get<Posture>().Axis, "20틱 뒤엔 달라져 있어야 한다");

            Assert.IsTrue(world.LoadState(0));
            Assert.AreEqual(axisAt0, diver.Get<Posture>().Axis, Tolerance);
            Assert.AreEqual(staminaAt0, diver.Get<Stamina>().Current, Tolerance);
        }

        [Test]
        public void 컴포넌트가_없어도_예외가_나지_않는다()
        {
            var registry = new EntityRegistry();
            var broken = new Entity("broken");
            broken.Add(new EntityKind(EntityType.Character));
            broken.Add(new Simulated());   // Transform/Velocity/Posture 없음
            registry.Add(broken);
            registry.Add(Diver("ok"));
            var world = World(registry);
            world.GameplayStartTick = 0;

            Assert.DoesNotThrow(() => world.Tick(1, 0.02f));
        }
    }
}
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```bash
unity command run_tests  --mode EditMode --async_tests true --project-path "$CLIENT"
unity command test_status --project-path "$CLIENT"
```

기대: 컴파일 에러 — `SkydiveWorld` 생성자가 3인자이고 `InputCommand`에 `Posture`/`Glide`가 없다.

> `InputCommand.Posture`/`Glide`는 Task 3이 proto와 함께 넣지만, **도메인 필드 두 개는 이 태스크에서
> 먼저 넣는다**(테스트가 필요하다). Task 3은 proto와 어댑터만 맡는다.

- [ ] **Step 3: 도메인 `InputCommand`에 두 필드를 더한다**

`Runtime/Scripts/Game/InputCommand.cs`를 수정한다 — `AbilityId` 아래에 추가:

```csharp
        public bool Jump { get; set; }
        public int AbilityId { get; set; }

        /// <summary>자세 축. 0이면 대자, 1이면 다이브. 사이는 연속이다.</summary>
        public float Posture { get; set; }

        /// <summary>패러세일을 펴고 있나. 자세 축과 무관한 별개 도구다.</summary>
        public bool Glide { get; set; }
```

그리고 `ToString()`도 같이 고친다(진단 로그가 새 값을 못 보면 넷코드 디버깅에서 눈이 먼다):

```csharp
        public override string ToString()
            => $"h={Horizontal:F2} v={Vertical:F2} jump={Jump} ability={AbilityId} posture={Posture:F2} glide={Glide}";
```

- [ ] **Step 4: `SkydiveSavedState`를 만든다**

`Runtime/Scripts/Game/SkydiveSavedState.cs`:

```csharp
namespace LOP
{
    /// <summary>
    /// 되감기용 Skydive 고유 상태의 한 틱 사진 — 자세와 스태미나. 위치·속도는
    /// <see cref="GameFramework.World.WorldBase"/>가 이미 담으므로 여기엔 그 밖의 것만 담는다.
    /// </summary>
    public readonly struct SkydiveSavedState
    {
        public readonly float Axis;
        public readonly bool Gliding;
        public readonly float Stamina;
        public readonly bool EmergencyUsed;
        public readonly float EmergencyRemaining;

        private SkydiveSavedState(float axis, bool gliding, float stamina,
                                  bool emergencyUsed, float emergencyRemaining)
        {
            Axis = axis;
            Gliding = gliding;
            Stamina = stamina;
            EmergencyUsed = emergencyUsed;
            EmergencyRemaining = emergencyRemaining;
        }

        public static SkydiveSavedState Capture(GameFramework.World.Entity entity)
        {
            var posture = entity.Get<Posture>();
            var stamina = entity.Get<Stamina>();
            return new SkydiveSavedState(
                posture == null ? 0f : posture.Axis,
                posture != null && posture.Gliding,
                stamina == null ? 0f : stamina.Current,
                stamina != null && stamina.EmergencyUsed,
                stamina == null ? 0f : stamina.EmergencyRemaining);
        }

        public void RestoreTo(GameFramework.World.Entity entity)
        {
            var posture = entity.Get<Posture>();
            if (posture != null)
            {
                posture.Axis = Axis;
                posture.Gliding = Gliding;
            }

            var stamina = entity.Get<Stamina>();
            if (stamina != null)
            {
                stamina.Current = Stamina;
                stamina.EmergencyUsed = EmergencyUsed;
                stamina.EmergencyRemaining = EmergencyRemaining;
            }
        }
    }
}
```

- [ ] **Step 5: `SkydiveWorld`를 고친다**

`Runtime/Scripts/Game/SkydiveWorld.cs` **전체를 교체**한다:

```csharp
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// Skydive의 시뮬 코어. 클·서가 같은 구체 클래스를 돌려 결과가 갈리지 않게 한다.
    /// 한 틱: ① 입력을 자세로 반영(축은 정해진 속도로만 움직인다) → ② 스태미나 소모·회복
    /// → ③ 자세가 정한 목표 속도로 이동.
    /// 레이저 판정은 Detection에 들어오지만(슬라이스 4) 지금은 비어 있다.
    /// </summary>
    public class SkydiveWorld : GameFramework.World.WorldBase
    {
        private readonly SkydiveMoveSystem _moveSystem;
        private readonly StaminaSystem _staminaSystem;
        private readonly SkydiveConfig _config;

        // 매 틱 도는 코드라 목록을 새로 만들지 않고 비워서 다시 쓴다.
        private readonly List<GameFramework.World.Entity> _divers = new List<GameFramework.World.Entity>();

        // 자세·스태미나의 틱별 사진. 위치·속도는 WorldBase가 담는다.
        private readonly GameFramework.Netcode.SequenceBuffer<Dictionary<string, SkydiveSavedState>> _gameFrames
            = new GameFramework.Netcode.SequenceBuffer<Dictionary<string, SkydiveSavedState>>(SaveCapacity);

        public SkydiveWorld(
            GameFramework.World.EntityRegistry entityRegistry,
            GameFramework.World.WorldEventBuffer eventBuffer,
            SkydiveMoveSystem moveSystem,
            StaminaSystem staminaSystem,
            SkydiveConfig config)
            : base(entityRegistry, eventBuffer)
        {
            _moveSystem = moveSystem;
            _staminaSystem = staminaSystem;
            _config = config;
        }

        protected override void Mutation(long tick, float deltaTime)
        {
            CollectDivers();

            if (HasStarted(tick) == false)
            {
                // 출발 전. 속도를 명시적으로 0으로 둔다 — 스냅샷과 물리 팔로워가 이 값을 읽는다.
                for (int i = 0; i < _divers.Count; i++)
                {
                    var velocity = _divers[i].Get<GameFramework.World.Velocity>();
                    if (velocity != null)
                    {
                        velocity.Linear = System.Numerics.Vector3.Zero;
                    }
                }
                return;
            }

            for (int i = 0; i < _divers.Count; i++)
            {
                ApplyPostureInput(_divers[i], deltaTime);
            }

            for (int i = 0; i < _divers.Count; i++)
            {
                // 임시 바닥에 닿아 있으면 "발 딛고 있다"로 본다 — 슬라이스 3이 이 판정을
                // 진짜 지면 접촉으로 바꾼다.
                var transform = _divers[i].Get<GameFramework.World.Transform>();
                bool grounded = transform != null && transform.Position.Y <= _config.GroundY + 0.01f;
                _staminaSystem.Tick(_divers[i], deltaTime, _config, grounded);
            }

            for (int i = 0; i < _divers.Count; i++)
            {
                _moveSystem.Tick(_divers[i], deltaTime, _config);
            }
        }

        // 입력이 자세를 바로 덮어쓰지 않는다 — 정해진 속도로만 움직인다. 그래야 자세가
        // 튀지 않고, 남을 예측하는 쪽의 오차도 완만해진다.
        private void ApplyPostureInput(GameFramework.World.Entity entity, float deltaTime)
        {
            var posture = entity.Get<Posture>();
            var command = entity.Get<InputBuffer>()?.Current;
            if (posture == null || command == null)
            {
                return;
            }

            float target = command.Posture < 0f ? 0f : (command.Posture > 1f ? 1f : command.Posture);
            float step = _config.PostureRate * deltaTime;
            float diff = target - posture.Axis;
            if (diff > step) { posture.Axis += step; }
            else if (diff < -step) { posture.Axis -= step; }
            else { posture.Axis = target; }

            if (command.Glide)
            {
                if (posture.Gliding == false)
                {
                    _staminaSystem.TryStartGlide(entity, _config);
                }
            }
            else
            {
                posture.Gliding = false;
            }
        }

        // id 순으로 세운다. 레지스트리 순회 순서는 정해져 있지 않은데, 처리 순서가 클·서에서
        // 같아야 두 쪽이 같은 결과에 이른다.
        private void CollectDivers()
        {
            _divers.Clear();
            foreach (var entity in EntityRegistry.All)
            {
                if (entity.Get<EntityKind>()?.Kind != EntityType.Character)
                {
                    continue;
                }
                if (entity.Has<GameFramework.World.Simulated>() == false)
                {
                    continue;   // 클라에서 남은 보간으로 그린다
                }
                _divers.Add(entity);
            }
            _divers.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
        }

        protected override void SaveGameState(long tick)
        {
            var frame = new Dictionary<string, SkydiveSavedState>();
            foreach (var entity in EntityRegistry.All)
            {
                if (entity.Has<GameFramework.World.Simulated>())
                {
                    frame[entity.Id] = SkydiveSavedState.Capture(entity);
                }
            }
            _gameFrames.Record(tick, frame);
        }

        protected override bool LoadGameState(long tick)
        {
            if (!_gameFrames.TryGet(tick, out var frame))
            {
                return false;
            }
            foreach (var pair in frame)
            {
                var entity = EntityRegistry.Get(pair.Key);
                if (entity != null)
                {
                    pair.Value.RestoreTo(entity);
                }
            }
            return true;
        }
    }
}
```

- [ ] **Step 6: 테스트가 통과하는지 확인한다**

```bash
unity command recompile        --project-path "$CLIENT"
unity command recompile_status --project-path "$CLIENT"
unity command get_console_logs --severity error --limit 40 --project-path "$CLIENT"
unity command run_tests  --mode EditMode --async_tests true --project-path "$CLIENT"
unity command test_status --project-path "$CLIENT"
```

기대: `SkydiveWorldTests` 8개 PASS. 전체 실패 0.

> ⚠️ 클·서 앱 코드는 아직 안 고쳤으므로 **`SkydiveLifetimeScope`가 컴파일되지 않는다**(생성자 인자
> 개수가 바뀌었다). Task 5·6이 고친다. 이 태스크에서는 **패키지 테스트가 통과하는 것**까지 본다.

- [ ] **Step 7: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git status --short
git add Runtime/Scripts/Game/SkydiveSavedState.cs Runtime/Scripts/Game/SkydiveSavedState.cs.meta \
        Runtime/Scripts/Game/SkydiveWorld.cs \
        Runtime/Scripts/Game/InputCommand.cs \
        Tests/EditMode/SkydiveWorldTests.cs Tests/EditMode/SkydiveWorldTests.cs.meta
git status --short
git commit -m "feat(skydive): 자세와 스태미나를 월드가 굴리고 되감기에 싣는다"
```

---

## Task 3: wire — 입력에 자세와 패러세일을 싣는다

**Files:**
- Modify: `LeagueOfPhysical-Shared/Protos/InputCommand.proto`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/PlayerInputManager.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/MessageHandler/Game.Input.MessageHandler.cs`

**Interfaces:**
- Consumes: Task 2가 넣은 `LOP.InputCommand.Posture` (float), `LOP.InputCommand.Glide` (bool)
- Produces:
  - proto `InputCommand`에 `float posture = 7;`, `bool glide = 8;`
  - `LOP.PlayerInputManager` — `void SetPosture(float axis)`, `void SetGlide(bool glide)`

> ⚠️ **이 태스크는 와이어를 바꾼다.** 클·서가 같이 배포되지 않으면 입력이 어긋난다. 슬라이스 2가
> 통째로 한 번에 배포되므로 문제는 없지만, **부분 배포를 하지 마라.**

- [ ] **Step 1: proto에 필드 둘을 더한다**

`LeagueOfPhysical-Shared/Protos/InputCommand.proto`:

```proto
syntax = "proto3";

message InputCommand {
  int64 sequence_number = 1;
  float horizontal = 2;
  float vertical = 3;
  bool jump = 4;
  int32 ability_id = 6;
  // 자세 축 — 0이면 대자, 1이면 다이브. 사이는 연속이다.
  float posture = 7;
  // 패러세일을 펴고 있나. 자세 축과 무관한 별개 도구다.
  bool glide = 8;
}
```

> 번호 5는 원래 비어 있다 — 건드리지 마라. 7·8이 다음 빈 번호다.

- [ ] **Step 2: proto를 재생성한다**

이 레포의 생성 절차를 따른다. **부모 스크립트를 통째로 돌리지 말고 서브스크립트를 개별 실행하라** —
과거에 부모 스크립트가 `MessageIds.cs`를 지워 **메시지 ID가 통째로 밀리고 와이어가 조용히 깨진
사고**가 있었다.

생성 전에 현재 ID를 떠 둔다:

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
cp Runtime.Generated/Scripts/MessageIds.cs /tmp/MessageIds.before.cs
```

- [ ] **Step 3: 메시지 ID가 안 밀렸는지 확인한다 (⭐ 이 태스크의 핵심 게이트)**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
diff /tmp/MessageIds.before.cs Runtime.Generated/Scripts/MessageIds.cs && echo "ID 변화 없음 — 정상"
```

기대: **차이 없음.** `InputCommand`는 `@auto_generate` 마커가 없는 필드 타입이라 MessageId를 갖지
않으므로, 필드를 더해도 ID 목록은 그대로여야 한다.

**차이가 있으면 멈추고 보고하라.** 그 상태로 진행하면 와이어가 깨진다.

- [ ] **Step 4: 클라 송신 어댑터에 새 값을 싣는다**

`LeagueOfPhysical-Client/Assets/Scripts/Game/PlayerInputManager.cs`를 세 군데 고친다.

(1) 보유 필드 — `pendingAbilityId` 아래에 추가:

```csharp
        private float heldPosture;   // 연속 — 슬라이더가 매 프레임 갱신(떼면 0=대자), 틱마다 샘플
        private bool heldGlide;      // 연속 — 슬라이더가 문턱을 넘고 있는 동안 참
```

(2) `Tick`의 `var command = new InputCommand { ... }` 초기화에 두 줄 추가:

```csharp
                Posture = heldPosture,
                Glide = heldGlide,
```

(3) `ToProto`에 두 줄 추가하고, 파일 끝의 `SetJump` 아래에 setter 둘을 만든다:

```csharp
        private static global::InputCommand ToProto(InputCommand command)
        {
            return new global::InputCommand
            {
                // ... 기존 필드들은 그대로 두고 아래 둘을 더한다
                Posture = command.Posture,
                Glide = command.Glide,
            };
        }

        /// <summary>자세 축(0=대자, 1=다이브). 슬라이더가 매 프레임 갱신한다.</summary>
        public void SetPosture(float axis)
        {
            heldPosture = axis < 0f ? 0f : (axis > 1f ? 1f : axis);
        }

        /// <summary>패러세일을 펴고 있나. 슬라이더가 문턱을 넘고 있는 동안 참.</summary>
        public void SetGlide(bool glide)
        {
            heldGlide = glide;
        }
```

> **`heldPosture`/`heldGlide`를 `Tick`에서 리셋하지 마라.** `pendingJump`와 달리 이건 *지속되는*
> 값이다 — 손을 떼면 UI가 0/false로 되돌린다. 매 틱 리셋하면 자세가 한 틱만 살고 사라진다.

- [ ] **Step 5: 서버 수신 어댑터에 새 값을 싣는다**

`LeagueOfPhysical-Server/Assets/Scripts/Game/MessageHandler/Game.Input.MessageHandler.cs`에서
proto → 도메인 `InputCommand`로 변환하는 자리를 찾아(파일 안에서 `Horizontal`을 대입하는 곳)
같은 블록에 두 줄을 더한다:

```csharp
                Posture = proto.Posture,
                Glide = proto.Glide,
```

**변환 자리가 여러 곳이면 전부 고쳐라** — `recent_inputs`(redundancy 윈도우)를 푸는 경로가 따로
있을 수 있다. 한 곳만 고치면 재전송으로 도착한 입력에서만 자세가 사라져, **패킷 손실이 있을 때만
자세가 튀는** 재현 어려운 버그가 된다.

- [ ] **Step 6: 클·서 컴파일을 확인한다**

```bash
unity command recompile        --project-path "$CLIENT"
unity command recompile_status --project-path "$CLIENT"
unity command get_console_logs --severity error --limit 40 --project-path "$CLIENT"
unity command recompile        --project-path "$SERVER"
unity command recompile_status --project-path "$SERVER"
unity command get_console_logs --severity error --limit 40 --project-path "$SERVER"
```

기대: `SkydiveLifetimeScope` 생성자 인자 관련 에러만 남는다(Task 5·6이 고친다). 그 밖의 CS 에러는 0.

- [ ] **Step 7: 커밋 (레포 셋)**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git status --short
git add Protos/InputCommand.proto Runtime.Generated
git status --short
git commit -m "feat(skydive): 입력에 자세 축과 패러세일을 싣는다"

cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git checkout -b feature/skydive-slice2
git status --short
git add Assets/Scripts/Game/PlayerInputManager.cs
git status --short
git commit -m "feat(skydive): 자세·패러세일 입력을 서버로 보낸다"

cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git checkout -b feature/skydive-slice2
git status --short
git add Assets/Scripts/Game/MessageHandler/Game.Input.MessageHandler.cs
git status --short
git commit -m "feat(skydive): 받은 입력에서 자세·패러세일을 읽는다"
```

---

## Task 4: 마스터데이터 — `TbSkydiveConfig` 신설

**Files:**
- Create: `infrastructure/table/Datas/#SkydiveConfig.xlsx`
- Modify: `infrastructure/table/Datas/__tables__.xlsx`
- Generated: MasterData-Client / MasterData-Server / lop-backend

**Interfaces:**
- Produces: `LOP.MasterData.TbSkydiveConfig` — id=1 한 행, Task 1의 `SkydiveConfig` 필드와 1:1

- [ ] **Step 1: 기존 테이블의 구조를 먼저 읽는다**

`TbFlappyConfig`가 **같은 모양의 선례**(전역 단일 행, id=1)다. 스키마와 등록 방식을 그대로 따른다:

```bash
cd C:/Users/re5na/workspace/LOP/infrastructure/table/Datas
python -c "
import openpyxl
for n in ['#FlappyConfig.xlsx','__tables__.xlsx']:
    wb=openpyxl.load_workbook(n); ws=wb.active
    print('===',n)
    for r in ws.iter_rows(values_only=True): print([('' if c is None else c) for c in r])
"
```

- [ ] **Step 2: `#SkydiveConfig.xlsx`를 만든다**

`#FlappyConfig.xlsx`와 **같은 헤더 4행 구조**(`##var` / `##type` / `##group` / `##`)로 만들고,
데이터 한 행을 넣는다. 열과 값:

| 열 | 타입 | 값 |
|---|---|---|
| `id` | int | 1 |
| `spread_fall_speed` | float | 25 |
| `dive_fall_speed` | float | 45 |
| `glide_fall_speed` | float | 6 |
| `spread_move_speed` | float | 12 |
| `dive_move_speed` | float | 18 |
| `glide_move_speed` | float | 14 |
| `spread_turn_accel` | float | 22 |
| `dive_turn_accel` | float | 6 |
| `glide_turn_accel` | float | 18 |
| `fall_approach` | float | 30 |
| `posture_rate` | float | 4 |
| `body_radius` | float | 0.4 |
| `body_height` | float | 1.8 |
| `ground_y` | float | 0 |
| `stamina_max` | float | 100 |
| `glide_drain` | float | 20 |
| `ground_recover` | float | 40 |
| `emergency_glide_time` | float | 1 |

`##group`은 **`#FlappyConfig.xlsx`가 쓰는 값을 그대로** 쓴다(시뮬 값이라 클·서 둘 다 필요하다).
Step 1에서 읽은 것을 따르라 — 지어내지 마라.

- [ ] **Step 3: `__tables__.xlsx`에 등록한다 (⭐ 빠뜨리면 로더가 안 읽는다)**

새 테이블은 `__tables__.xlsx`에 행을 넣어야 Luban이 만들고 런타임 로더가 읽는다.
`FlappyConfig` 행을 그대로 본떠 `SkydiveConfig` 행을 넣는다(full name, value type, input 파일 등
그 행이 채운 열을 전부 같은 방식으로).

- [ ] **Step 4: 생성한다**

```bash
cd C:/Users/re5na/workspace/LOP/infrastructure/table
./gen.sh
```

기대: `[gen] target=client` / `target=server` / `target=matchmaking` / `[done]`.

- [ ] **Step 5: 생성 결과를 확인한다**

```bash
ls C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client/Runtime.Generated/Scripts/MasterData/ | grep -i skydive
ls C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server/Runtime.Generated/Scripts/MasterData/ | grep -i skydive
```

기대: 양쪽에 `SkydiveConfig.cs`와 `TbSkydiveConfig.cs`.

**로더가 이 테이블을 읽는지도 확인하라** — 로더가 파일 목록을 들고 있다면 새 테이블이 그 목록에
들어갔는지 본다(들어가지 않으면 런타임에 테이블이 비어 있다):

```bash
grep -rn "FlappyConfig" C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client/Runtime/Scripts/ | head -5
```

`FlappyConfig`가 어딘가 수기 목록에 있으면 **같은 자리에 `SkydiveConfig`도 있어야 한다.**

`.meta` 삭제도 확인한다:

```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client status --short | grep '^ D' || echo "삭제 없음"
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server status --short | grep '^ D' || echo "삭제 없음"
```

- [ ] **Step 6: 네 레포에 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/infrastructure
git checkout -b feature/skydive-slice2
git status --short
git add "table/Datas/#SkydiveConfig.xlsx" "table/Datas/__tables__.xlsx"
git status --short
git commit -m "feat(skydive): 자세·스태미나 튜닝 테이블을 더한다"

for R in LeagueOfPhysical-MasterData-Client LeagueOfPhysical-MasterData-Server; do
  cd "C:/Users/re5na/workspace/LOP/$R"
  git checkout -b feature/skydive-slice2
  git status --short
  git add Runtime.Generated
  git status --short
  git commit -m "chore(masterdata): TbSkydiveConfig 추가 반영 (생성물)"
done

cd C:/Users/re5na/workspace/LOP/lop-backend
git checkout -b feature/skydive-slice2
git status --short
git add apps/matchmaking-server/src/masterdata apps/matchmaking-server/master_data
git status --short
git commit -m "chore(masterdata): TbSkydiveConfig 추가 반영 (생성물)"
```

---

## Task 5: 서버 배선

**Files:**
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Game/SkydiveConfigProvider.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/SkydiveLifetimeScope.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Entity/SkydivePlayerCreator.cs`

**Interfaces:**
- Consumes: Task 1·2의 `SkydiveConfig`, `StaminaSystem`, `SkydiveMoveSystem`,
  `SkydiveWorld(EntityRegistry, WorldEventBuffer, SkydiveMoveSystem, StaminaSystem, SkydiveConfig)`,
  `Posture`, `Stamina`; Task 4의 `LOP.MasterData.TbSkydiveConfig`
- Produces: `LOP.SkydiveConfigProvider` — `SkydiveConfig Get()`

- [ ] **Step 1: config provider를 만든다**

`Assets/Scripts/Game/SkydiveConfigProvider.cs`:

```csharp
namespace LOP
{
    /// <summary>
    /// Luban <c>TbSkydiveConfig</c>(전역 단일 행, id=1)을 LOP-Shared <see cref="SkydiveConfig"/>로 옮기는
    /// 사이드 로컬 어댑터. (Shared는 MasterData 패키지 비참조 → 여기서 변환. <see cref="FlappyConfigProvider"/> 대칭.)
    /// </summary>
    public class SkydiveConfigProvider
    {
        private readonly LOP.MasterData.LOPMasterData md;

        public SkydiveConfigProvider(LOP.MasterData.LOPMasterData md)
        {
            this.md = md;
        }

        public SkydiveConfig Get()
        {
            // 없으면 Luban의 애매한 KeyNotFoundException 대신 원인을 짚어 크게 실패
            var r = md.Tables.TbSkydiveConfig.GetOrDefault(1);
            if (r == null)
            {
                throw new System.InvalidOperationException(
                    "TbSkydiveConfig id=1 행을 찾을 수 없음 — MasterData 미로드 또는 SkydiveConfig 데이터 누락");
            }
            return new SkydiveConfig(
                r.SpreadFallSpeed, r.DiveFallSpeed, r.GlideFallSpeed,
                r.SpreadMoveSpeed, r.DiveMoveSpeed, r.GlideMoveSpeed,
                r.SpreadTurnAccel, r.DiveTurnAccel, r.GlideTurnAccel,
                r.FallApproach, r.PostureRate,
                r.BodyRadius, r.BodyHeight, r.GroundY,
                r.StaminaMax, r.GlideDrain, r.GroundRecover, r.EmergencyGlideTime);
        }
    }
}
```

- [ ] **Step 2: 스코프를 고친다**

`Assets/Scripts/Game/SkydiveLifetimeScope.cs`의 `ConfigureGame` **전체를 교체**한다:

```csharp
        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.Register<SkydiveConfigProvider>(Lifetime.Singleton);
            builder.Register<SkydiveConfig>(c => c.Resolve<SkydiveConfigProvider>().Get(), Lifetime.Singleton);

            builder.Register<SkydiveMoveSystem>(Lifetime.Singleton);
            builder.Register<StaminaSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.World.IWorld>(c => new SkydiveWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<SkydiveMoveSystem>(),
                c.Resolve<StaminaSystem>(),
                c.Resolve<SkydiveConfig>()), Lifetime.Singleton);

            builder.Register<ICharacterCreator, SkydivePlayerCreator>(Lifetime.Singleton);
            builder.Register<IGameRuleSystem, SkydiveRuleSystem>(Lifetime.Singleton);
        }
```

- [ ] **Step 3: 생성기가 자세·스태미나를 붙이고 몸 크기를 config에서 받게 한다**

`Assets/Scripts/Entity/SkydivePlayerCreator.cs`를 고친다:

- 파일 위쪽의 `BodyRadius`/`BodyHeight` **상수 두 개를 지운다**(이제 config에서 온다).
- 생성자에 `SkydiveConfig config`를 더하고 필드에 보관한다.
- `CapsuleShape` 줄을 `new GameFramework.World.CapsuleShape(config.BodyRadius, config.BodyHeight)`로.
- `entityRegistry.Add(worldEntity)` **앞에** 두 줄을 더한다:

```csharp
            worldEntity.Add(new Posture());
            worldEntity.Add(new Stamina { Current = config.StaminaMax });
```

> 몸 크기 상수를 지우는 것이 이 단계의 요점이다 — 슬라이스 1의 주석이 "클·서가 같은 값이어야 하니
> 함께 옮길 것"이라고 경고한 그 이동이다. **클라(Task 6)도 반드시 같이 옮겨야 한다.**

- [ ] **Step 4: 서버 컴파일을 확인한다**

```bash
unity command recompile        --project-path "$SERVER"
unity command recompile_status --project-path "$SERVER"
unity command get_console_logs --severity error --limit 40 --project-path "$SERVER"
```

기대: CS 에러 0.

- [ ] **Step 5: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git status --short
git add Assets/Scripts/Game/SkydiveConfigProvider.cs Assets/Scripts/Game/SkydiveConfigProvider.cs.meta \
        Assets/Scripts/Game/SkydiveLifetimeScope.cs \
        Assets/Scripts/Entity/SkydivePlayerCreator.cs
git status --short
git commit -m "feat(skydive): 서버가 튜닝값을 읽고 자세·스태미나를 붙인다"
```

---

## Task 6: 클라 배선

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Game/SkydiveConfigProvider.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/SkydiveLifetimeScope.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Entity/SkydivePlayerCreator.cs`

**Interfaces:**
- Consumes: Task 1·2의 타입들, Task 4의 `TbSkydiveConfig`
- Produces: `LOP.SkydiveConfigProvider` (클라 쪽 동명 클래스 — 서버와 별개 레포다)

- [ ] **Step 1: config provider를 만든다**

`Assets/Scripts/Game/SkydiveConfigProvider.cs` — **Task 5 Step 1의 코드와 완전히 같다.** 서버 레포와
클라 레포는 별개 어셈블리라 각자 갖는다(`FlappyConfigProvider`도 양쪽에 있다). 위 코드를 그대로 쓴다:

```csharp
namespace LOP
{
    /// <summary>
    /// Luban <c>TbSkydiveConfig</c>(전역 단일 행, id=1)을 LOP-Shared <see cref="SkydiveConfig"/>로 옮기는
    /// 사이드 로컬 어댑터. (Shared는 MasterData 패키지 비참조 → 여기서 변환. <see cref="FlappyConfigProvider"/> 대칭.)
    /// </summary>
    public class SkydiveConfigProvider
    {
        private readonly LOP.MasterData.LOPMasterData md;

        public SkydiveConfigProvider(LOP.MasterData.LOPMasterData md)
        {
            this.md = md;
        }

        public SkydiveConfig Get()
        {
            var r = md.Tables.TbSkydiveConfig.GetOrDefault(1);
            if (r == null)
            {
                throw new System.InvalidOperationException(
                    "TbSkydiveConfig id=1 행을 찾을 수 없음 — MasterData 미로드 또는 SkydiveConfig 데이터 누락");
            }
            return new SkydiveConfig(
                r.SpreadFallSpeed, r.DiveFallSpeed, r.GlideFallSpeed,
                r.SpreadMoveSpeed, r.DiveMoveSpeed, r.GlideMoveSpeed,
                r.SpreadTurnAccel, r.DiveTurnAccel, r.GlideTurnAccel,
                r.FallApproach, r.PostureRate,
                r.BodyRadius, r.BodyHeight, r.GroundY,
                r.StaminaMax, r.GlideDrain, r.GroundRecover, r.EmergencyGlideTime);
        }
    }
}
```

- [ ] **Step 2: 스코프를 고친다**

`Assets/Scripts/Game/SkydiveLifetimeScope.cs`의 `ConfigureGame`에서 **월드 등록 부분을 교체**하고
config·스태미나 시스템을 더한다(카메라·동기화 정책 등 기존 등록은 그대로 둔다):

```csharp
            builder.Register<SkydiveConfigProvider>(Lifetime.Singleton);
            builder.Register<SkydiveConfig>(c => c.Resolve<SkydiveConfigProvider>().Get(), Lifetime.Singleton);

            builder.Register<SkydiveMoveSystem>(Lifetime.Singleton);
            builder.Register<StaminaSystem>(Lifetime.Singleton);
            builder.Register<SkydiveWorld>(c => new SkydiveWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<SkydiveMoveSystem>(),
                c.Resolve<StaminaSystem>(),
                c.Resolve<SkydiveConfig>()), Lifetime.Singleton)
                .As<GameFramework.World.IWorld>().AsSelf();
```

- [ ] **Step 3: 생성기를 고친다**

`Assets/Scripts/Entity/SkydivePlayerCreator.cs` — **Task 5 Step 3과 같은 변경**을 클라 쪽에 한다:
`BodyRadius`/`BodyHeight` 상수 삭제, 생성자에 `SkydiveConfig config` 추가, `CapsuleShape`를
config 값으로, `entityRegistry.Add` 앞에 두 줄:

```csharp
            worldEntity.Add(new Posture());
            worldEntity.Add(new Stamina { Current = config.StaminaMax });
```

- [ ] **Step 4: 클라 컴파일을 확인한다**

```bash
unity command recompile        --project-path "$CLIENT"
unity command recompile_status --project-path "$CLIENT"
unity command get_console_logs --severity error --limit 40 --project-path "$CLIENT"
```

기대: CS 에러 0.

- [ ] **Step 5: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git status --short
git add Assets/Scripts/Game/SkydiveConfigProvider.cs Assets/Scripts/Game/SkydiveConfigProvider.cs.meta \
        Assets/Scripts/Game/SkydiveLifetimeScope.cs \
        Assets/Scripts/Entity/SkydivePlayerCreator.cs
git status --short
git commit -m "feat(skydive): 클라가 튜닝값을 읽고 자세·스태미나를 붙인다"
```

---

## Task 7: 조작 UI — 가로 슬라이더와 스태미나 막대

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/UI/SkydivePad/SkydivePad.uxml`
- Create: `LeagueOfPhysical-Client/Assets/UI/SkydivePad/SkydivePad.uss`
- Create: `LeagueOfPhysical-Client/Assets/Scripts/UI/SkydivePad/SkydivePadViewModel.cs`
- Create: `LeagueOfPhysical-Client/Assets/Scripts/UI/SkydivePad/SkydivePadView.cs`
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Game/SkydiveHudCoordinator.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/SkydiveLifetimeScope.cs`
- Modify: `LeagueOfPhysical-Client/Assets/UI/UIViewCatalog.asset` (뷰 → UXML 매핑)

**Interfaces:**
- Consumes: Task 3의 `PlayerInputManager.SetPosture(float)` / `SetGlide(bool)`,
  기존 `PlayerInputManager.SetMovement(float, float)`, `CameraController`, `IWindowManager`,
  `IPlayerContext`, `GameFramework.World.EntityRegistry`, Task 1의 `Stamina`·`Posture`
- Produces: `LOP.UI.SkydivePadViewModel`, `LOP.UI.SkydivePadView`, `LOP.SkydiveHudCoordinator`

> **본보기가 있다.** `Assets/Scripts/UI/FlapPad/{FlapPadView,FlapPadViewModel}.cs` +
> `Assets/UI/FlapPad/{FlapPad.uxml,FlapPad.uss}` + `Assets/Scripts/Game/FlappyHudCoordinator.cs`가
> 정확히 같은 구조다. **먼저 그 셋을 읽고 같은 모양으로 만들어라** — 등록·해제·수명 처리가 거기 있다.
> 방향 스틱 부분은 `Assets/UI/GamePad/GamePad.uxml`의 `joystick-area`/`joystick-bg`/`joystick-handle`
> 구조와 `Assets/Scripts/UI/GamePad/GamePadViewModel.cs`의 처리를 참고하라.

- [ ] **Step 1: UXML을 만든다**

`Assets/UI/SkydivePad/SkydivePad.uxml`:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <Style src="SkydivePad.uss" />
    <ui:VisualElement name="skydive-pad-root" class="skydive-pad-root" picking-mode="Ignore">
        <!-- 지금 자세와 스태미나. 엄지가 슬라이더를 덮으므로 상태는 다른 곳에 보여야 한다. -->
        <ui:VisualElement name="status" class="status" picking-mode="Ignore">
            <ui:Label name="posture-label" class="posture-label" text="대자" />
            <ui:VisualElement name="stamina-track" class="stamina-track" picking-mode="Ignore">
                <ui:VisualElement name="stamina-fill" class="stamina-fill" picking-mode="Ignore" />
            </ui:VisualElement>
        </ui:VisualElement>

        <!-- 왼손: 방향 -->
        <ui:VisualElement name="joystick-area" class="joystick-area">
            <ui:VisualElement name="joystick-bg" class="joystick-bg" picking-mode="Ignore">
                <ui:VisualElement name="joystick-handle" class="joystick-handle" picking-mode="Ignore" />
            </ui:VisualElement>
        </ui:VisualElement>

        <!-- 오른손: 자세. 누른 자리가 중립(대자)이 되는 떠 있는 슬라이더다. -->
        <ui:VisualElement name="posture-area" class="posture-area">
            <ui:VisualElement name="posture-track" class="posture-track" picking-mode="Ignore">
                <ui:VisualElement name="posture-handle" class="posture-handle" picking-mode="Ignore" />
            </ui:VisualElement>
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 2: USS를 만든다**

`Assets/UI/SkydivePad/SkydivePad.uss`:

```css
.skydive-pad-root {
    position: absolute;
    left: 0; right: 0; top: 0; bottom: 0;
}

/* 장애물이 화면 아래에서 올라오므로 하단 중앙은 비운다. */
.joystick-area { position: absolute; left: 0; bottom: 0; width: 35%; height: 45%; }
.posture-area  { position: absolute; right: 0; bottom: 0; width: 35%; height: 45%; }

.joystick-bg {
    position: absolute; width: 160px; height: 160px;
    border-radius: 80px; background-color: rgba(255, 255, 255, 0.12);
}
.joystick-handle {
    position: absolute; width: 64px; height: 64px;
    border-radius: 32px; background-color: rgba(255, 255, 255, 0.45);
}

.posture-track {
    position: absolute; width: 220px; height: 44px;
    border-radius: 22px; background-color: rgba(0, 0, 0, 0.35);
}
.posture-handle {
    position: absolute; width: 44px; height: 44px;
    border-radius: 22px; background-color: rgba(255, 255, 255, 0.85);
}

.status { position: absolute; left: 16px; top: 16px; width: 240px; }
.posture-label { color: rgb(230, 236, 245); font-size: 18px; margin-bottom: 6px; }
.stamina-track {
    height: 14px; border-radius: 7px; background-color: rgba(0, 0, 0, 0.4);
}
.stamina-fill {
    height: 14px; border-radius: 7px; width: 100%;
    background-color: rgb(52, 211, 153);
}
```

- [ ] **Step 3: ViewModel을 만든다**

`Assets/Scripts/UI/SkydivePad/SkydivePadViewModel.cs`:

```csharp
using R3;

namespace LOP.UI
{
    /// <summary>
    /// 조작 패드의 상태와 커맨드. 터치 좌표 해석은 View가 하고, 여기서는 그 결과를
    /// 입력 매니저로 넘기고 화면에 보일 값을 노출한다.
    /// </summary>
    public class SkydivePadViewModel : System.IDisposable
    {
        // 슬라이더를 이만큼 왼쪽으로 밀면 패러세일이 펴진다. 도구라 반쯤 펼칠 수 없다.
        private const float GlideThreshold = 0.45f;

        private readonly PlayerInputManager input;
        private readonly CameraController cameraController;
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly SkydiveConfig config;

        private readonly ReactiveProperty<float> staminaRatio = new ReactiveProperty<float>(1f);
        private readonly ReactiveProperty<string> postureName = new ReactiveProperty<string>("대자");

        public ReadOnlyReactiveProperty<float> StaminaRatio => staminaRatio;
        public ReadOnlyReactiveProperty<string> PostureName => postureName;

        public SkydivePadViewModel(PlayerInputManager input, CameraController cameraController,
                                   IPlayerContext playerContext,
                                   GameFramework.World.EntityRegistry entityRegistry,
                                   SkydiveConfig config)
        {
            this.input = input;
            this.cameraController = cameraController;
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
            this.config = config;
        }

        /// <summary>방향 스틱. 값은 −1~1로 정규화된 것이 들어온다.</summary>
        public void Move(UnityEngine.Vector2 stick)
        {
            // 카메라가 보는 방향 기준으로 돌린다 — 화면에서 위로 밀면 화면 위쪽으로 간다.
            float yaw = cameraController.MainCamera.transform.eulerAngles.y * UnityEngine.Mathf.Deg2Rad;
            float cos = UnityEngine.Mathf.Cos(yaw);
            float sin = UnityEngine.Mathf.Sin(yaw);
            input.SetMovement(stick.x * cos + stick.y * sin, -stick.x * sin + stick.y * cos);
        }

        /// <summary>
        /// 자세 슬라이더. −1(완전히 왼쪽)~+1(완전히 오른쪽). 오른쪽이 다이브, 왼쪽이 패러세일이다.
        /// </summary>
        public void Posture(float slider)
        {
            input.SetGlide(slider <= -GlideThreshold);
            input.SetPosture(slider > 0f ? slider : 0f);
        }

        /// <summary>손을 떼면 대자로 돌아온다.</summary>
        public void ReleasePosture()
        {
            input.SetGlide(false);
            input.SetPosture(0f);
        }

        public void CameraLook(UnityEngine.Vector2 delta) => cameraController.ProcessTouchInput(delta);

        /// <summary>매 프레임 월드에서 읽어 화면 값을 갱신한다(연속 상태는 pull).</summary>
        public void Refresh()
        {
            var entity = string.IsNullOrEmpty(playerContext.entityId)
                ? null
                : entityRegistry.Get(playerContext.entityId);
            if (entity == null)
            {
                return;
            }

            var stamina = entity.Get<Stamina>();
            if (stamina != null && config.StaminaMax > 0f)
            {
                staminaRatio.Value = stamina.Current / config.StaminaMax;
            }

            var posture = entity.Get<LOP.Posture>();
            if (posture != null)
            {
                postureName.Value = posture.Gliding ? "패러세일" : (posture.Axis > 0.5f ? "다이브" : "대자");
            }
        }

        public void Dispose()
        {
            staminaRatio.Dispose();
            postureName.Dispose();
        }
    }
}
```

- [ ] **Step 4: View를 만든다**

이 프로젝트의 View 계약은 `Assets/Scripts/UI/Core/UIView.cs`다 — **UXML은 View가 로드하지 않고
`UIManager`가 `Initialize(VisualElement root)`로 주입한다.** `Root`를 통해 트리에 접근하고,
`Layer`를 선언하고, 정리는 `Dispose(bool)` 오버라이드로 한다.

`Assets/Scripts/UI/SkydivePad/SkydivePadView.cs`:

```csharp
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// Skydive 조작 화면. 왼쪽은 방향 스틱, 오른쪽은 자세 슬라이더 — 둘 다 <b>누른 자리가 중립</b>인
    /// 떠 있는 컨트롤이다(화면 가장자리에서 밀 여유가 없는 문제와 엄지를 눈으로 맞추는 문제를 함께 없앤다).
    /// ViewModel 커맨드로 넘기기만 하는 얇은 바인더다.
    /// </summary>
    public class SkydivePadView : UIView
    {
        // 스틱을 끝까지 민 것으로 치는 거리(px). 화면 크기와 무관한 고정값이라 손 크기 기준이다.
        private const float StickRadius = 80f;
        private const float SliderRadius = 110f;

        private readonly SkydivePadViewModel _viewModel;

        private VisualElement _joystickBg;
        private VisualElement _joystickHandle;
        private VisualElement _postureTrack;
        private VisualElement _postureHandle;
        private VisualElement _staminaFill;
        private Label _postureLabel;

        private int _stickPointer = -1;
        private Vector2 _stickOrigin;
        private int _sliderPointer = -1;
        private Vector2 _sliderOrigin;

        private IVisualElementScheduledItem _tick;

        public SkydivePadView(SkydivePadViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();

            _joystickBg = Root.Q<VisualElement>("joystick-bg");
            _joystickHandle = Root.Q<VisualElement>("joystick-handle");
            _postureTrack = Root.Q<VisualElement>("posture-track");
            _postureHandle = Root.Q<VisualElement>("posture-handle");
            _staminaFill = Root.Q<VisualElement>("stamina-fill");
            _postureLabel = Root.Q<Label>("posture-label");

            var stickArea = Root.Q<VisualElement>("joystick-area");
            stickArea.RegisterCallback<PointerDownEvent>(OnStickDown);
            stickArea.RegisterCallback<PointerMoveEvent>(OnStickMove);
            stickArea.RegisterCallback<PointerUpEvent>(OnStickUp);
            stickArea.RegisterCallback<PointerCaptureOutEvent>(_ => ResetStick());

            var postureArea = Root.Q<VisualElement>("posture-area");
            postureArea.RegisterCallback<PointerDownEvent>(OnSliderDown);
            postureArea.RegisterCallback<PointerMoveEvent>(OnSliderMove);
            postureArea.RegisterCallback<PointerUpEvent>(OnSliderUp);
            postureArea.RegisterCallback<PointerCaptureOutEvent>(_ => ResetSlider());

            _viewModel.StaminaRatio
                .Subscribe(ratio => _staminaFill.style.width = Length.Percent(Mathf.Clamp01(ratio) * 100f))
                .AddTo(Disposables);

            _viewModel.PostureName
                .Subscribe(name => _postureLabel.text = name)
                .AddTo(Disposables);

            // UIView는 MonoBehaviour가 아니라 Update가 없다 — 패널 스케줄러로 매 프레임 월드를 읽는다.
            _tick = Root.schedule.Execute(_ => _viewModel.Refresh()).Every(0);
        }

        private void OnStickDown(PointerDownEvent evt)
        {
            _stickPointer = evt.pointerId;
            _stickOrigin = evt.localPosition;
            Place(_joystickBg, _stickOrigin);
            Place(_joystickHandle, _stickOrigin);
            ((VisualElement)evt.currentTarget).CapturePointer(evt.pointerId);
        }

        private void OnStickMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != _stickPointer)
            {
                return;
            }

            Vector2 delta = (Vector2)evt.localPosition - _stickOrigin;
            Vector2 clamped = Vector2.ClampMagnitude(delta, StickRadius);
            Place(_joystickHandle, _stickOrigin + clamped);

            // UI의 y는 아래가 양수라 뒤집어야 "위로 밀면 앞으로"가 된다.
            _viewModel.Move(new Vector2(clamped.x / StickRadius, -clamped.y / StickRadius));
        }

        private void OnStickUp(PointerUpEvent evt)
        {
            if (evt.pointerId == _stickPointer)
            {
                ((VisualElement)evt.currentTarget).ReleasePointer(evt.pointerId);
                ResetStick();
            }
        }

        private void ResetStick()
        {
            _stickPointer = -1;
            _viewModel.Move(Vector2.zero);
        }

        private void OnSliderDown(PointerDownEvent evt)
        {
            _sliderPointer = evt.pointerId;
            _sliderOrigin = evt.localPosition;
            Place(_postureTrack, _sliderOrigin);
            Place(_postureHandle, _sliderOrigin);
            ((VisualElement)evt.currentTarget).CapturePointer(evt.pointerId);
        }

        private void OnSliderMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != _sliderPointer)
            {
                return;
            }

            float dx = Mathf.Clamp(((Vector2)evt.localPosition).x - _sliderOrigin.x, -SliderRadius, SliderRadius);
            Place(_postureHandle, new Vector2(_sliderOrigin.x + dx, _sliderOrigin.y));
            _viewModel.Posture(dx / SliderRadius);
        }

        private void OnSliderUp(PointerUpEvent evt)
        {
            if (evt.pointerId == _sliderPointer)
            {
                ((VisualElement)evt.currentTarget).ReleasePointer(evt.pointerId);
                ResetSlider();
            }
        }

        private void ResetSlider()
        {
            _sliderPointer = -1;
            Place(_postureHandle, _sliderOrigin);
            _viewModel.ReleasePosture();
        }

        // 요소의 중심을 그 자리에 둔다. UXML에서 position:absolute라 left/top으로 옮긴다.
        private static void Place(VisualElement element, Vector2 center)
        {
            element.style.left = center.x - element.resolvedStyle.width * 0.5f;
            element.style.top = center.y - element.resolvedStyle.height * 0.5f;
        }

        private bool _disposed;

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;

                if (disposing)
                {
                    _tick?.Pause();
                    _viewModel.Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }
}
```

- [ ] **Step 5: HUD 코디네이터를 만든다**

`Assets/Scripts/Game/SkydiveHudCoordinator.cs` — `FlappyHudCoordinator.cs`와 같은 짝이다
(`MessageHandlerBase`를 상속해 `EntityCreated`를 구독하고, 내 몸이 생기면 화면을 연다):

```csharp
using GameFramework;
using LOP.Event.Entity;
using LOP.UI;
using MessagePipe;

namespace LOP
{
    /// <summary>
    /// 내 몸이 생기면 Skydive 인게임 화면(조작 패드 + 디버그 HUD)을 연다.
    /// 엔티티 생성과 화면 띄우기를 분리한다 — 화면 교체는 "큰 흐름"이라 코디네이터 책임
    /// (아키텍처 가이드라인 "흐름의 경계"). <see cref="FlappyHudCoordinator"/>와 같은 짝이다.
    /// </summary>
    public class SkydiveHudCoordinator : MessageHandlerBase
    {
        private readonly IGameDataStore gameDataStore;
        private readonly IWindowManager windowManager;
        private readonly ISubscriber<EntityCreated> entityCreatedSubscriber;

        private bool _opened;

        public SkydiveHudCoordinator(IGameDataStore gameDataStore, IWindowManager windowManager,
            ISubscriber<EntityCreated> entityCreatedSubscriber)
        {
            this.gameDataStore = gameDataStore;
            this.windowManager = windowManager;
            this.entityCreatedSubscriber = entityCreatedSubscriber;
        }

        protected override void Subscribe() => Track(entityCreatedSubscriber.Subscribe(OnEntityCreated));

        private void OnEntityCreated(EntityCreated entityCreated)
        {
            if (_opened || entityCreated.entityId != gameDataStore.userEntityId)
            {
                return;
            }

            // 조작면을 먼저 열어 Window 밴드 최하단에 깐다(전체화면이라 위 위젯 입력을 막지 않도록).
            windowManager.Open<SkydivePadView>();
            windowManager.Open<DebugHudView>();
            _opened = true;
        }
    }
}
```

- [ ] **Step 5b: UXML을 카탈로그에 등록한다 (⭐ 빠뜨리면 화면이 안 열린다)**

`WindowManager.Open<T>()`는 **`Assets/UI/UIViewCatalog.asset`** 에서 `typeof(T).Name`으로 UXML을
찾는다. 등록이 없으면 열리지 않고 콘솔에 이렇게만 남는다:

```
[WindowManager] UIViewCatalog에 'SkydivePadView' UXML 매핑이 없습니다.
```

에디터에서 `Assets/UI/UIViewCatalog.asset`을 선택해 항목을 하나 더하고,
**이름 `SkydivePadView` ↔ `Assets/UI/SkydivePad/SkydivePad.uxml`** 을 물린다.
(`FlapPadView` 항목이 어떻게 채워져 있는지 보고 그대로 따라라.)

등록됐는지 확인:

```bash
grep -n "SkydivePadView" "$CLIENT/Assets/UI/UIViewCatalog.asset"
```

- [ ] **Step 6: 스코프에 등록한다**

`Assets/Scripts/Game/SkydiveLifetimeScope.cs`의 `ConfigureGame` 끝에 추가:

```csharp
            builder.RegisterEntryPoint<SkydiveHudCoordinator>();
            builder.Register<LOP.UI.SkydivePadViewModel>(Lifetime.Transient);
            builder.Register<LOP.UI.SkydivePadView>(Lifetime.Transient);
```

그리고 `RegisterViewFactories`를 오버라이드한다(`FlappyRaceLifetimeScope`와 같은 모양):

```csharp
        protected override void RegisterViewFactories(
            IObjectResolver container, IWindowManager windowManager, List<IDisposable> sink)
        {
            sink.Add(windowManager.RegisterViewFactory<LOP.UI.SkydivePadView>(
                () => container.Resolve<LOP.UI.SkydivePadView>()));
        }
```

필요한 `using System;`, `using System.Collections.Generic;`, `using LOP.UI;`를 파일 위에 더한다.

- [ ] **Step 7: 클라 컴파일을 확인한다**

```bash
unity command recompile        --project-path "$CLIENT"
unity command recompile_status --project-path "$CLIENT"
unity command get_console_logs --severity error --limit 40 --project-path "$CLIENT"
```

기대: CS 에러 0.

- [ ] **Step 8: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git status --short
git add Assets/UI/SkydivePad Assets/Scripts/UI/SkydivePad \
        Assets/Scripts/Game/SkydiveHudCoordinator.cs Assets/Scripts/Game/SkydiveHudCoordinator.cs.meta \
        Assets/Scripts/Game/SkydiveLifetimeScope.cs
git status --short
git commit -m "feat(skydive): 방향 스틱과 자세 슬라이더로 떨어지는 것을 조종한다"
```

---

## Task 8: 머지·배포·실플레이

**Files:** (코드 변경 없음)

- [ ] **Step 1: 여덟 레포를 머지한다**

레포마다 `CLAUDE.md`의 **푸시 규약**을 그대로, **한 줄씩 결과를 확인하며** 따른다.
순서: Shared → Client → Server → infrastructure → MasterData-Client → MasterData-Server → lop-backend.
(이번 슬라이스는 **Art를 건드리지 않으므로** 아트 머지와 서브모듈 포인터 커밋이 없다.)

```bash
git fetch origin
git rebase --autostash origin/main
git checkout main
git merge --ff-only origin/main
git merge --no-ff feature/skydive-slice2
git push origin main
```

> ⚠️ `--force` / `--force-with-lease` 금지. 거절되면 다시 `fetch` → 리베이스 → 재시도.
> ⚠️ Unity 레포는 리베이스 전에 로컬 픽스처를 `git stash push -u`로 빼두고 끝나면 `pop` 한다.

- [ ] **Step 2: 배포**

```bash
gh workflow run gameserver-deploy --repo Baeinsoo/LeagueOfPhysical-Server -f environment=local
gh workflow run backend-deploy    --repo Baeinsoo/lop-backend -f app=all -f environment=local
```

**어드레서블(`content-deploy`)은 이번엔 필요 없다** — 아트 에셋이 안 바뀌었다. (맵을 건드렸다면
필요하다.)

- [ ] **Step 3: 배포가 실제로 반영됐는지 확인한다 (⭐ 워크플로 성공 ≠ 롤아웃 완료)**

```bash
kubectl get application backend -n argocd -o jsonpath='{.status.sync.revision}{"\n"}'
cd C:/Users/re5na/workspace/LOP/infrastructure && git fetch origin -q && git rev-parse origin/main
```

**둘이 다르면** ArgoCD가 아직 새 커밋을 안 집어간 것이다(`Synced Healthy`여도 그렇다). 밀어 넣는다:

```bash
kubectl annotate application backend -n argocd argocd.argoproj.io/refresh=hard --overwrite
kubectl annotate application root    -n argocd argocd.argoproj.io/refresh=hard --overwrite
```

리비전이 따라온 뒤 실제 이미지까지 본다:

```bash
kubectl get configmap -n default -o yaml | grep -i "GAME_SERVER_IMAGE"
kubectl get pods -A | grep -iE "room|matchmaking|lobby"
```

- [ ] **Step 4: 두 클라로 확인한다**

메인 에디터 + MPPM 클론으로 스카이다이브 매칭. 확인할 것:

1. 오른쪽 아래를 누르면 **그 자리에 자세 슬라이더가 뜬다**
2. **오른쪽으로 밀면 빨라지고** 왼쪽으로 밀면 패러세일이 펴진다
3. 패러세일을 켜면 **스태미나 막대가 줄고**, 다 되면 저절로 접힌다
4. **손을 떼면 대자로 돌아온다**
5. 왼쪽 스틱으로 **옆으로 이동**이 되고, 패러세일일 때 가장 멀리 간다
6. 상대의 자세 변화도 화면에 반영된다(남도 예측으로 굴러간다)
7. 자세를 급히 바꿔도 **캐릭터가 순간이동하지 않는다**(수렴 모델)

> ⚠️ 출발 직후 ~5초 정지는 여전히 정상이다(출발 게이트). 카운트다운 화면은 아직 없다.

- [ ] **Step 5: 손맛을 보고 숫자를 조정한다**

이 슬라이스의 진짜 목적은 **"떨어지는 게 재밌나"** 다. 값을 바꿀 때는 `#SkydiveConfig.xlsx`만
고치고 `gen.sh` → 배포하면 된다(코드 변경 없음). **바꾼 뒤에도 세 부등식은 Task 1의 테스트가
지킨다** — 테스트가 깨지면 그 조합은 스펙 §2.1을 위반한 것이다.

- [ ] **Step 6: ROADMAP에 기록한다**

`docs/ROADMAP.md`의 "이번 세션에 닫힌 것"에 한 줄 더하고, 별도 브랜치
`docs/roadmap-skydive-slice2`에서 커밋해 같은 푸시 규약으로 머지한다.

---

## 다음 슬라이스

슬라이스 3(코스와 결승)은 **이 슬라이스가 실제로 만든 것을 보고** 계획을 쓴다. 특히 이 슬라이스가
남기는 두 자리를 그때 채운다:

- `SkydiveConfig.GroundY`(임시 바닥) → 진짜 맵 충돌
- `SkydiveWorld`의 `grounded` 판정(임시 바닥 접촉) → 발판 위에 서 있는지
