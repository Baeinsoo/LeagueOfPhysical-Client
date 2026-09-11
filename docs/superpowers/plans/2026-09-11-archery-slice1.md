# Archery 슬라이스 1 — 모드가 존재하고, 활을 쏠 수 있다

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 로비에서 `Archery`를 고르면 방에 들어가고, **원형맵의 자기 자리에 서서** 왼손으로 조준하고
오른손으로 당겼다 놓으면 **화살이 포물선으로 날아간다.** 남이 쏜 화살도 보인다.

**Architecture:** 새 게임 모드는 기존 배관에 칸 하나를 채우는 일이다 — `TbGameMode` 행이 게임 씬을
가리키고, 그 씬의 `ArcheryLifetimeScope`가 자기 월드·생성기·룰을 등록한다. 시뮬(`ArcheryWorld`,
`ArcheryAimSystem`, `ArcheryTrajectory`)은 LOP-Shared에 두어 클·서가 **같은 구체 클래스**를 컴파일한다.
**화살은 엔티티가 아니다** — *누가·언제·어디서·어느 방향으로·얼마나 빠르게* 쐈는지만 있으면 어느
시각의 위치든 계산되므로, 발사 사실만 남기고 궤적은 양쪽이 각자 계산한다.

**Tech Stack:** Unity 6 / VContainer / Mirror / Protobuf(wire) / Luban(MasterData) / NUnit(EditMode)

**Spec:** `docs/superpowers/specs/2026-09-11-archery-game-mode-design.md`

---

## 이 슬라이스가 전체 어디쯤인가

| 슬라이스 | 내용 |
|---|---|
| **1 (이 문서)** | 모드가 존재한다. 제자리에 서서 조준·당김·발사. 화살이 난다. **과녁 없음** |
| 2 | 결정론 웨이브로 과녁이 뜬다. 맞히면 점수. 60초 뒤 순위. `#ArcheryConfig`/`#ArcheryTarget` |
| 3 | 함정 과녁 + 점수 차감 + **미리 당기기의 대가**(줌인 대가·흔들림). ← **재미 확인 지점** |
| 4 | 직선맵 추가 + 원형맵의 사람 오발 |

**슬라이스 1이 의도적으로 안 하는 것:** 과녁, 점수, 함정, 당김 흔들림, 마스터데이터 튜닝 테이블,
직선맵, 화살이 무언가에 맞는 판정. 튜닝 숫자는 **시스템의 `const`** 로 둔다 — 슬라이스 2에서
`#ArcheryConfig`로 옮긴다(Skydive가 밟은 순서와 같다).

---

## Global Constraints

- **게임 모드 내부명은 `Archery`.** 파일·클래스·`TbGameMode.Code` 전부 이 접두어.
  `#GameMode.xlsx`의 **비어 있는 `TargetShooting` 행을 `Archery`로 고쳐 쓴다** — 코드 어디서도
  그 코드 문자열을 참조하지 않는 것을 확인했다(`grep -rn "TargetShooting" --include=*.cs`가 0건).
- **시뮬 코드는 LOP-Shared에 구체 클래스로 둔다.** 인터페이스 seam 금지(결정론은 *공유 구체 코드*가
  보장한다).
- **`*System`은 무상태 DI 인스턴스**, **`static`은 컨텍스트 없는 순수 커널에만**(`ArcheryTrajectory`).
  순수 커널에 `*System` 이름을 붙이지 않는다.
- **World 타입 이름이 `UnityEngine`과 겹치면 풀 네임스페이스로 한정한다** — `GameFramework.World.Transform`,
  `GameFramework.World.Component` 등. 규칙의 목적은 **모호성 회피**이지 `using` 금지가 아니다:
  LOP-Shared는 `using GameFramework.World;`와 `using UnityEngine;`을 함께 쓰는 파일이 이미
  **테스트 11개·런타임 다수** 있고, 겹치는 이름만 한정해서 안전하게 쓴다. **bare `Component`를
  쓰는 파일에서만** 그 using이 문제가 된다.
- **`git add -A` / `git commit -a` 금지.** 워킹트리에 의도적으로 커밋하지 않는 로컬 픽스처가
  상시 있다(`Assets/Art` 서브모듈 포인터, `Jua-Regular SDF.asset`, `ProjectSettings/*`).
  **바꾼 파일만 경로로 지정**하고 커밋 전에 `git status --short`로 스테이지된 것을 확인한다.
- **`.cs`를 새로 만들면 Unity가 만든 `.meta`를 반드시 함께 커밋한다.** `.meta`를 손으로 만들지 않는다.
- **main에 직접 커밋 금지.** 브랜치: 각 레포에서 `feature/archery-slice1`.
- **틱 레이트는 50Hz** — 60초 = 3000틱.

### 프로토 변경 주의 (필수)

이 슬라이스는 **기존 메시지 `InputCommand`에 필드를 더할 뿐 새 메시지를 만들지 않는다.**
그래서 **MessageId를 다시 생성하지 않는다** — 부모 스크립트 `generate_protos.sh`는
`MessageIds.cs`를 지우고 다시 만드는데, 그때 id가 밀리면 **와이어가 조용히 깨진다.**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
bash Scripts/compile_protos.sh          # ← 이것만 돌린다
git diff --stat Runtime.Generated/Scripts/MessageIds.cs   # ← 반드시 0줄이어야 한다
```

`MessageIds.cs`에 변경이 생기면 **거기서 멈추고** 원인을 확인한다.

### 컴파일·테스트 게이트 (매 태스크 공통)

에디터가 떠 있으면 `unity` CLI가 붙는다. **클·서 에디터가 동시에 붙어 있으므로 `--project-path`를
매번 명시**한다.

```bash
CLIENT=C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
SERVER=C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server

unity status                                                  # 붙어 있는 에디터 확인
unity command recompile        --project-path "$CLIENT"
unity command recompile_status --project-path "$CLIENT"
unity command get_console_logs --severity error --limit 40 --project-path "$CLIENT"
```

> ⚠️ **`recompile_status`의 `failed:false`만 보면 안 된다.** `status`가 `up_to_date`면 **재컴파일을
> 아예 안 한 것**이다. 결정타는 `get_console_logs`의 CS 에러를 **시각과 대조**하는 것.
>
> ⚠️ CLI 응답은 30초 상한이고 `--timeout`이 안 먹는다. `recompile`/`run_tests`가 타임아웃으로
> 죽어도 에디터에선 계속 돈다 — `*_status`로 폴링한다.
>
> ⚠️ **컴파일 에러가 있는 상태에서 `run_tests`를 부르면 에디터가 물린다.** 반드시 컴파일이
> 깨끗한 것을 확인한 뒤에 테스트를 돌린다.

패키지 EditMode 테스트:

```bash
unity command run_tests  --mode EditMode --async_tests true --project-path "$CLIENT"
unity command test_status --project-path "$CLIENT"
```

---

## File Structure

| 파일 | 책임 |
|---|---|
| **LOP-Shared** | |
| `Runtime/Scripts/Game/ArcheryAim.cs` | 조준·당김 상태(데이터만). 각도, 당기는 중인가, 언제부터 |
| `Runtime/Scripts/Game/ArcheryShot.cs` | 발사 사실 하나(불변 struct). 이것만 있으면 궤적이 계산된다 |
| `Runtime/Scripts/Game/ArcheryTrajectory.cs` | 순수 커널 — 각도→방향, 발사 후 t초의 위치·속도 |
| `Runtime/Scripts/Game/ArcheryAimSystem.cs` | 입력을 읽어 조준 상태를 갱신하고, 떼는 틱에 발사를 만든다 |
| `Runtime/Scripts/Game/ArcheryWorld.cs` | 시뮬 코어. 매 틱 조준 갱신 + 날아가는 화살 목록 관리 |
| `Runtime/Scripts/Game/InputCommand.cs` | (수정) 조준·당김 필드 추가 |
| `Protos/InputCommand.proto` | (수정) 같은 필드를 와이어에 |
| `Tests/EditMode/ArcheryTrajectoryTests.cs` | 각도→방향, 포물선 |
| `Tests/EditMode/ArcheryAimSystemTests.cs` | 당김 시간→속도, 떼는 틱에만 발사 |
| `Tests/EditMode/ArcheryWorldTests.cs` | 화살 수명, 롤백 저장·복원 |
| **LOP-Client** | |
| `Assets/Scripts/Game/PlayerInputManager.cs` | (수정) 조준·당김 setter + 와이어 변환 |
| `Assets/Scripts/Game/ArcheryLifetimeScope.cs` | 클라 덩어리 등록 |
| `Assets/Scripts/Entity/ArcheryPlayerCreator.cs` | 플레이어 몸(클라) |
| `Assets/Scripts/Game/ArcheryArrowView.cs` | 날아가는 화살을 그린다 |
| `Assets/Scripts/Game/ArcheryAimView.cs` | 매 프레임 카메라 각도를 조준으로 넘기고, 당김에 맞춰 시야를 좁힌다 |
| `Assets/Scripts/UI/ArcheryPad/ArcheryPadViewModel.cs` | 좌/우 영역 터치 → 시점·당김 (넘기기만 한다) |
| `Assets/Scripts/UI/ArcheryPad/ArcheryPadView.cs` | 패드 UXML 트리 + 바인딩 |
| `Assets/Scenes/Archery.unity` | 게임 씬 |
| `Assets/AddressableAssetsData/AssetGroups/Scene.asset` | (수정) 맵 씬 등록 — **원격 그룹** |
| **LOP-Server** | |
| `Assets/Scripts/Game/ArcheryLifetimeScope.cs` | 서버 덩어리 등록 |
| `Assets/Scripts/Game/ArcheryRuleSystem.cs` | 원형 배치 스폰·60초·등수(임시) |
| `Assets/Scripts/Entity/ArcheryPlayerCreator.cs` | 플레이어 몸(서버) |
| `Assets/Scripts/Game/MessageHandler/GameInputMessageHandler.cs` | (수정) 새 필드 수신 변환 |
| `Assets/Scenes/Archery.unity` | 게임 씬 |
| **LOP-Art** | |
| `Scenes/ArcheryCircleMap.unity` | 원형맵 — 사대 마커 8개 + 가운데 무대 + 바닥 |
| **infrastructure** | |
| `table/Datas/#GameMode.xlsx` `#Map.xlsx` `#Queue.xlsx` | 데이터 행 |

---

## Task 1: 순수 커널 — 각도와 포물선

화살이 네트워크를 타지 않는 근거가 되는 계산이다. **상태가 없다** — 발사 정보만 주면 어느 시각의
위치든 나온다.

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryShot.cs`
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryTrajectory.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/ArcheryTrajectoryTests.cs`

**Interfaces:**
- Consumes: `UnityEngine.Vector3`, `UnityEngine.Mathf`
- Produces:
  - `LOP.ArcheryShot` — `readonly struct`, ctor `(string shooterId, long fireTick, Vector3 origin, Vector3 velocity)`,
    필드 `ShooterId`/`FireTick`/`Origin`/`Velocity`
  - `LOP.ArcheryTrajectory` — `static class`.
    `const float Gravity = 20f`, `const float LifetimeSeconds = 3f`,
    `static Vector3 DirectionFrom(float yawDegrees, float pitchDegrees)`,
    `static Vector3 PositionAt(in ArcheryShot shot, float secondsSinceFire)`,
    `static Vector3 VelocityAt(in ArcheryShot shot, float secondsSinceFire)`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/ArcheryTrajectoryTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryTrajectoryTests
    {
        const float Tolerance = 1e-3f;

        [Test]
        public void 정면을_보면_앞으로_향한다()
        {
            var direction = ArcheryTrajectory.DirectionFrom(0f, 0f);

            Assert.AreEqual(0f, direction.x, Tolerance);
            Assert.AreEqual(0f, direction.y, Tolerance);
            Assert.AreEqual(1f, direction.z, Tolerance);
        }

        [Test]
        public void 좌우_각도는_y축_회전이다()
        {
            var direction = ArcheryTrajectory.DirectionFrom(90f, 0f);

            Assert.AreEqual(1f, direction.x, Tolerance);
            Assert.AreEqual(0f, direction.y, Tolerance);
            Assert.AreEqual(0f, direction.z, Tolerance);
        }

        [Test]
        public void 위아래_각도가_양수면_위를_본다()
        {
            var direction = ArcheryTrajectory.DirectionFrom(0f, 90f);

            Assert.AreEqual(0f, direction.x, Tolerance);
            Assert.AreEqual(1f, direction.y, Tolerance);
            Assert.AreEqual(0f, direction.z, Tolerance);
        }

        [Test]
        public void 방향은_길이가_1이다()
        {
            var direction = ArcheryTrajectory.DirectionFrom(37f, 21f);

            Assert.AreEqual(1f, direction.magnitude, Tolerance);
        }

        [Test]
        public void 수평으로_쏘면_중력만큼_처진다()
        {
            var shot = new ArcheryShot("a", 0, Vector3.zero, new Vector3(0f, 0f, 40f));

            var position = ArcheryTrajectory.PositionAt(shot, 1f);

            Assert.AreEqual(40f, position.z, Tolerance);                          // 40 × 1
            Assert.AreEqual(-0.5f * ArcheryTrajectory.Gravity, position.y, Tolerance); // -½gt²
        }

        [Test]
        public void 발사_직후에는_출발점_그대로다()
        {
            var origin = new Vector3(3f, 2f, 1f);
            var shot = new ArcheryShot("a", 0, origin, new Vector3(0f, 0f, 40f));

            var position = ArcheryTrajectory.PositionAt(shot, 0f);

            Assert.AreEqual(origin.x, position.x, Tolerance);
            Assert.AreEqual(origin.y, position.y, Tolerance);
            Assert.AreEqual(origin.z, position.z, Tolerance);
        }

        [Test]
        public void 세로_속도가_중력만큼_줄어든다()
        {
            var shot = new ArcheryShot("a", 0, Vector3.zero, new Vector3(0f, 10f, 40f));

            var velocity = ArcheryTrajectory.VelocityAt(shot, 1f);

            Assert.AreEqual(40f, velocity.z, Tolerance);
            Assert.AreEqual(10f - ArcheryTrajectory.Gravity, velocity.y, Tolerance);
        }
    }
}
```

- [ ] **Step 2: 테스트가 실패하는 것을 확인한다**

```bash
unity command recompile        --project-path "$CLIENT"
unity command recompile_status --project-path "$CLIENT"
unity command get_console_logs --severity error --limit 20 --project-path "$CLIENT"
```

Expected: `ArcheryShot` / `ArcheryTrajectory`가 없다는 CS0246 컴파일 에러.

- [ ] **Step 3: 최소 구현을 쓴다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryShot.cs`:

```csharp
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 화살 한 발이 떠난 사실. <b>화살은 엔티티가 아니다</b> — 누가·언제·어디서·어느 속도로
    /// 떠났는지만 있으면 어느 시각의 위치든 계산되므로, 이 다섯 값만 오가고 궤적은 양쪽이 각자 낸다.
    /// </summary>
    public readonly struct ArcheryShot
    {
        public readonly string ShooterId;
        public readonly long FireTick;
        public readonly Vector3 Origin;
        public readonly Vector3 Velocity;

        public ArcheryShot(string shooterId, long fireTick, Vector3 origin, Vector3 velocity)
        {
            ShooterId = shooterId;
            FireTick = fireTick;
            Origin = origin;
            Velocity = velocity;
        }
    }
}
```

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryTrajectory.cs`:

```csharp
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 화살의 궤적. 상태가 없는 순수 계산이라 클·서·뷰가 같은 식에 같은 시각을 넣으면 같은 답을 얻는다.
    /// </summary>
    public static class ArcheryTrajectory
    {
        /// <summary>
        /// 화살에 걸리는 중력. 캐릭터 중력(약 19.6)과 비슷한 크기로 잡아 무게감이 따로 놀지 않게 한
        /// 튜닝 값이다 — 같은 값으로 묶어 둔 것이 아니다.
        /// </summary>
        public const float Gravity = 20f;

        /// <summary>이 시간이 지난 화살은 목록에서 지운다. 화면 밖으로 나간 뒤에도 들고 있을 이유가 없다.</summary>
        public const float LifetimeSeconds = 3f;

        /// <summary>좌우(y축 회전)·위아래 각도를 방향 벡터로. 위아래 각도가 양수면 위를 본다.</summary>
        public static Vector3 DirectionFrom(float yawDegrees, float pitchDegrees)
        {
            float yaw = yawDegrees * Mathf.Deg2Rad;
            float pitch = pitchDegrees * Mathf.Deg2Rad;
            float horizontal = Mathf.Cos(pitch);
            return new Vector3(
                Mathf.Sin(yaw) * horizontal,
                Mathf.Sin(pitch),
                Mathf.Cos(yaw) * horizontal);
        }

        public static Vector3 PositionAt(in ArcheryShot shot, float secondsSinceFire)
        {
            float t = secondsSinceFire;
            return shot.Origin
                 + shot.Velocity * t
                 + new Vector3(0f, -0.5f * Gravity * t * t, 0f);
        }

        public static Vector3 VelocityAt(in ArcheryShot shot, float secondsSinceFire)
        {
            return shot.Velocity + new Vector3(0f, -Gravity * secondsSinceFire, 0f);
        }
    }
}
```

- [ ] **Step 4: 테스트가 통과하는 것을 확인한다**

```bash
unity command recompile_status --project-path "$CLIENT"       # 에러 0 확인 후에
unity command run_tests  --mode EditMode --async_tests true --project-path "$CLIENT"
unity command test_status --project-path "$CLIENT"
```

Expected: `ArcheryTrajectoryTests` 7개 PASS.

- [ ] **Step 5: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git status --short
git add Runtime/Scripts/Game/ArcheryShot.cs Runtime/Scripts/Game/ArcheryShot.cs.meta \
        Runtime/Scripts/Game/ArcheryTrajectory.cs Runtime/Scripts/Game/ArcheryTrajectory.cs.meta \
        Tests/EditMode/ArcheryTrajectoryTests.cs Tests/EditMode/ArcheryTrajectoryTests.cs.meta
git commit -m "feat(archery): 화살 궤적을 상태 없는 계산으로 둔다"
```

---

## Task 2: 조준 상태와 발사 — `ArcheryAim` + `ArcheryAimSystem`

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryAim.cs`
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryAimSystem.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/ArcheryAimSystemTests.cs`

**Interfaces:**
- Consumes: `GameFramework.World.{Entity, Component, Transform}`, `LOP.{InputCommand, InputBuffer, ArcheryShot, ArcheryTrajectory}`,
  확장 메서드 `ToNumerics()`/`ToUnity()` (namespace `GameFramework`)
- Produces:
  - `LOP.ArcheryAim : GameFramework.World.Component` — 필드 `float Yaw`, `float Pitch`, `bool Drawing`, `long DrawStartTick`
  - `LOP.ArcheryAimSystem` —
    `const float MinSpeed = 25f`, `const float MaxSpeed = 65f`, `const float FullDrawSeconds = 0.8f`, `const float EyeHeight = 1.4f`,
    `static float DrawRatio(long drawStartTick, long currentTick, float tickInterval)`,
    `static float SpeedFor(float drawRatio)`,
    `ArcheryShot? Tick(GameFramework.World.Entity entity, long tick, float tickInterval)`

> **왜 `Tick`이 발사를 돌려주나.** 조준 상태를 바꾸는 것과 화살을 만드는 것은 같은 커맨드를 읽는
> 한 번의 판단이다. 두 메서드로 쪼개면 호출자가 순서를 지켜야 하고, 그 순서가 깨지면 클·서가
> 갈린다. 떼는 틱에만 값이 나오고 나머지 틱은 `null`이다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/ArcheryAimSystemTests.cs`:

```csharp
using GameFramework;
using GameFramework.World;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryAimSystemTests
    {
        const float Tolerance = 1e-3f;
        const float TickInterval = 0.02f;   // 50Hz

        static Entity Archer(Vector3 position)
        {
            var entity = new Entity("archer-1");
            entity.Add(new GameFramework.World.Transform { Position = position.ToNumerics() });
            entity.Add(new ArcheryAim());
            entity.Add(new InputBuffer());
            return entity;
        }

        static void Feed(Entity entity, float yaw, float pitch, bool drawing, bool release)
        {
            entity.Get<InputBuffer>().Current = new InputCommand
            {
                AimYaw = yaw,
                AimPitch = pitch,
                Drawing = drawing,
                Release = release,
            };
        }

        [Test]
        public void 당기기_시작한_틱을_기억한다()
        {
            var archer = Archer(Vector3.zero);
            var system = new ArcheryAimSystem();

            Feed(archer, 0f, 0f, drawing: true, release: false);
            system.Tick(archer, 100, TickInterval);

            var aim = archer.Get<ArcheryAim>();
            Assert.IsTrue(aim.Drawing);
            Assert.AreEqual(100, aim.DrawStartTick);
        }

        [Test]
        public void 계속_당기고_있으면_시작_틱이_바뀌지_않는다()
        {
            var archer = Archer(Vector3.zero);
            var system = new ArcheryAimSystem();

            Feed(archer, 0f, 0f, drawing: true, release: false);
            system.Tick(archer, 100, TickInterval);
            system.Tick(archer, 110, TickInterval);

            Assert.AreEqual(100, archer.Get<ArcheryAim>().DrawStartTick);
        }

        [Test]
        public void 조준_각도가_상태에_들어온다()
        {
            var archer = Archer(Vector3.zero);
            var system = new ArcheryAimSystem();

            Feed(archer, 30f, -15f, drawing: false, release: false);
            system.Tick(archer, 1, TickInterval);

            var aim = archer.Get<ArcheryAim>();
            Assert.AreEqual(30f, aim.Yaw, Tolerance);
            Assert.AreEqual(-15f, aim.Pitch, Tolerance);
        }

        [Test]
        public void 오래_당길수록_화살이_빠르다()
        {
            float shortDraw = ArcheryAimSystem.SpeedFor(
                ArcheryAimSystem.DrawRatio(100, 105, TickInterval));      // 0.1초
            float longDraw = ArcheryAimSystem.SpeedFor(
                ArcheryAimSystem.DrawRatio(100, 130, TickInterval));      // 0.6초

            Assert.Greater(longDraw, shortDraw);
        }

        [Test]
        public void 끝까지_당긴_뒤_더_당겨도_같은_속도다()
        {
            float full = ArcheryAimSystem.SpeedFor(
                ArcheryAimSystem.DrawRatio(0, (long)(ArcheryAimSystem.FullDrawSeconds / TickInterval), TickInterval));
            float longer = ArcheryAimSystem.SpeedFor(ArcheryAimSystem.DrawRatio(0, 10000, TickInterval));

            Assert.AreEqual(ArcheryAimSystem.MaxSpeed, full, Tolerance);
            Assert.AreEqual(ArcheryAimSystem.MaxSpeed, longer, Tolerance);
        }

        [Test]
        public void 떼는_틱에만_화살이_나온다()
        {
            var archer = Archer(Vector3.zero);
            var system = new ArcheryAimSystem();

            Feed(archer, 0f, 0f, drawing: true, release: false);
            Assert.IsNull(system.Tick(archer, 100, TickInterval));

            Feed(archer, 0f, 0f, drawing: false, release: true);
            Assert.IsNotNull(system.Tick(archer, 120, TickInterval));

            Feed(archer, 0f, 0f, drawing: false, release: false);
            Assert.IsNull(system.Tick(archer, 121, TickInterval));
        }

        [Test]
        public void 당기지_않고_떼면_화살이_없다()
        {
            var archer = Archer(Vector3.zero);
            var system = new ArcheryAimSystem();

            Feed(archer, 0f, 0f, drawing: false, release: true);

            Assert.IsNull(system.Tick(archer, 100, TickInterval));
        }

        [Test]
        public void 화살은_눈높이에서_조준_방향으로_떠난다()
        {
            var archer = Archer(new Vector3(5f, 0f, -3f));
            var system = new ArcheryAimSystem();

            Feed(archer, 90f, 0f, drawing: true, release: false);
            system.Tick(archer, 100, TickInterval);
            Feed(archer, 90f, 0f, drawing: false, release: true);
            var shot = system.Tick(archer, 140, TickInterval);

            Assert.IsTrue(shot.HasValue);
            Assert.AreEqual("archer-1", shot.Value.ShooterId);
            Assert.AreEqual(140, shot.Value.FireTick);
            Assert.AreEqual(5f, shot.Value.Origin.x, Tolerance);
            Assert.AreEqual(ArcheryAimSystem.EyeHeight, shot.Value.Origin.y, Tolerance);
            Assert.AreEqual(-3f, shot.Value.Origin.z, Tolerance);
            // 좌우 90도 = +x 방향. 세로 성분은 없다.
            Assert.Greater(shot.Value.Velocity.x, 0f);
            Assert.AreEqual(0f, shot.Value.Velocity.y, Tolerance);
            Assert.AreEqual(0f, shot.Value.Velocity.z, Tolerance);
        }

        [Test]
        public void 쏘고_나면_당김이_풀린다()
        {
            var archer = Archer(Vector3.zero);
            var system = new ArcheryAimSystem();

            Feed(archer, 0f, 0f, drawing: true, release: false);
            system.Tick(archer, 100, TickInterval);
            Feed(archer, 0f, 0f, drawing: false, release: true);
            system.Tick(archer, 140, TickInterval);

            Assert.IsFalse(archer.Get<ArcheryAim>().Drawing);
        }
    }
}
```

- [ ] **Step 2: 테스트가 실패하는 것을 확인한다**

```bash
unity command recompile        --project-path "$CLIENT"
unity command recompile_status --project-path "$CLIENT"
unity command get_console_logs --severity error --limit 20 --project-path "$CLIENT"
```

Expected: `ArcheryAim`·`ArcheryAimSystem`이 없다는 CS0246, 그리고 `InputCommand`에
`AimYaw`/`AimPitch`/`Drawing`/`Release`가 없다는 CS0117.

- [ ] **Step 3: 입력 커맨드에 조준·당김을 더한다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/InputCommand.cs` — 클래스 안에 추가:

```csharp
        /// <summary>좌우 조준 각도(도). 카메라가 보는 방향을 그대로 싣는다.</summary>
        public float AimYaw { get; set; }

        /// <summary>위아래 조준 각도(도). 양수면 위를 본다.</summary>
        public float AimPitch { get; set; }

        /// <summary>활을 당기고 있나. 손가락을 대고 있는 동안 계속 참인 연속 값이다.</summary>
        public bool Drawing { get; set; }

        /// <summary>손을 뗀 틱에만 참인 이산 액션이다(<see cref="Jump"/>와 같은 짝).</summary>
        public bool Release { get; set; }
```

같은 파일의 `ToString()`도 함께 늘린다 — 진단 로그가 조준을 못 보면 발사 어긋남을 못 쫓는다:

```csharp
        public override string ToString()
            => $"h={Horizontal:F2} v={Vertical:F2} jump={Jump} ability={AbilityId} posture={Posture:F2} glide={Glide} posing={Posing} dash={Dash} aim=({AimYaw:F1},{AimPitch:F1}) draw={Drawing} release={Release}";
```

- [ ] **Step 4: 조준 상태와 시스템을 쓴다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryAim.cs`:

```csharp
namespace LOP
{
    /// <summary>
    /// 활을 겨누고 당기는 상태(데이터만). 처리는 <see cref="ArcheryAimSystem"/>이 한다.
    /// </summary>
    public class ArcheryAim : GameFramework.World.Component
    {
        /// <summary>좌우 조준 각도(도).</summary>
        public float Yaw;

        /// <summary>위아래 조준 각도(도). 양수면 위를 본다.</summary>
        public float Pitch;

        /// <summary>지금 당기고 있나.</summary>
        public bool Drawing;

        /// <summary>당기기 시작한 절대 틱. <see cref="Drawing"/>이 거짓이면 의미 없다.</summary>
        public long DrawStartTick;
    }
}
```

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryAimSystem.cs`:

```csharp
using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 입력을 읽어 조준 상태를 갱신하고, 손을 뗀 틱에 화살 한 발을 만든다.
    /// 무상태다 — 상태는 <see cref="ArcheryAim"/>에 있다.
    /// </summary>
    public class ArcheryAimSystem
    {
        /// <summary>살짝 당겼을 때의 화살 속도(m/s). 느리고 크게 휜다.</summary>
        public const float MinSpeed = 25f;

        /// <summary>끝까지 당겼을 때의 화살 속도(m/s). 빠르고 곧게 간다.</summary>
        public const float MaxSpeed = 65f;

        /// <summary>이만큼 당기면 최대다. 더 당겨도 세지지 않는다.</summary>
        public const float FullDrawSeconds = 0.8f;

        /// <summary>화살이 떠나는 높이 — 발밑이 아니라 눈높이에서 나가야 겨눈 대로 간다.</summary>
        public const float EyeHeight = 1.4f;

        /// <summary>당긴 정도 0~1. 오래 당겨도 1을 넘지 않는다.</summary>
        public static float DrawRatio(long drawStartTick, long currentTick, float tickInterval)
        {
            float seconds = (currentTick - drawStartTick) * tickInterval;
            return Mathf.Clamp01(seconds / FullDrawSeconds);
        }

        public static float SpeedFor(float drawRatio)
        {
            return Mathf.Lerp(MinSpeed, MaxSpeed, Mathf.Clamp01(drawRatio));
        }

        /// <summary>떼는 틱에만 화살을 돌려준다. 나머지 틱은 null이다.</summary>
        public ArcheryShot? Tick(GameFramework.World.Entity entity, long tick, float tickInterval)
        {
            var aim = entity.Get<ArcheryAim>();
            var command = entity.Get<InputBuffer>()?.Current;
            if (aim == null || command == null)
            {
                return null;
            }

            aim.Yaw = command.AimYaw;
            aim.Pitch = command.AimPitch;

            // 당기기 시작한 틱은 "안 당기다가 당기기 시작한" 그 틱에만 새로 찍는다.
            if (command.Drawing && aim.Drawing == false)
            {
                aim.DrawStartTick = tick;
            }

            if (command.Release == false)
            {
                aim.Drawing = command.Drawing;
                return null;
            }

            // 당긴 적 없이 뗀 것은 발사가 아니다(손가락이 스친 경우).
            if (aim.Drawing == false)
            {
                return null;
            }

            float speed = SpeedFor(DrawRatio(aim.DrawStartTick, tick, tickInterval));
            Vector3 origin = entity.Get<GameFramework.World.Transform>().Position.ToUnity()
                           + new Vector3(0f, EyeHeight, 0f);
            Vector3 velocity = ArcheryTrajectory.DirectionFrom(aim.Yaw, aim.Pitch) * speed;

            aim.Drawing = false;
            return new ArcheryShot(entity.Id, tick, origin, velocity);
        }
    }
}
```

- [ ] **Step 5: 테스트가 통과하는 것을 확인한다**

```bash
unity command recompile_status --project-path "$CLIENT"
unity command run_tests  --mode EditMode --async_tests true --project-path "$CLIENT"
unity command test_status --project-path "$CLIENT"
```

Expected: `ArcheryAimSystemTests` 9개 + `ArcheryTrajectoryTests` 7개 PASS.

- [ ] **Step 6: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git status --short
git add Runtime/Scripts/Game/ArcheryAim.cs Runtime/Scripts/Game/ArcheryAim.cs.meta \
        Runtime/Scripts/Game/ArcheryAimSystem.cs Runtime/Scripts/Game/ArcheryAimSystem.cs.meta \
        Runtime/Scripts/Game/InputCommand.cs \
        Tests/EditMode/ArcheryAimSystemTests.cs Tests/EditMode/ArcheryAimSystemTests.cs.meta
git commit -m "feat(archery): 당긴 만큼 세게 나가는 발사를 만든다"
```

---

## Task 3: 시뮬 코어 — `ArcheryWorld`

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryWorld.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/ArcheryWorldTests.cs`

**Interfaces:**
- Consumes: `GameFramework.World.{WorldBase, EntityRegistry, WorldEventBuffer, Simulated, Entity}`, `LOP.{ArcheryAimSystem, ArcheryShot, ArcheryTrajectory, InputBuffer, InputCommand, ArcheryAim}`
- Produces:
  - `LOP.ArcheryWorld : GameFramework.World.WorldBase` — ctor `(EntityRegistry, WorldEventBuffer, ArcheryAimSystem, float tickInterval)`,
    `IReadOnlyList<ArcheryShot> Shots { get; }`

> **롤백에 대비해 저장/복원을 넣는다.** 이 슬라이스에서는 아무도 움직이지 않아 되감기가 거의
> 일어나지 않지만, **당김 중에 되감기면 시위가 풀리는** 종류의 버그는 나중에 원인을 찾기 매우
> 어렵다. `WorldBase`가 내주는 훅에 조준 상태와 화살 목록을 얹는 것은 열 줄 남짓이다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/ArcheryWorldTests.cs`:

```csharp
using GameFramework.World;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryWorldTests
    {
        const float TickInterval = 0.02f;

        static (ArcheryWorld world, EntityRegistry registry, Entity archer) Make()
        {
            var registry = new EntityRegistry();
            var archer = new Entity("archer-1");
            archer.Add(new GameFramework.World.Transform());
            // Velocity가 없으면 WorldBase.SaveState가 이 몸의 프레임을 기록하지 않고,
            // 그러면 LoadState가 프레임을 못 찾아 LoadGameState까지 가지도 못한다.
            archer.Add(new Velocity());
            archer.Add(new ArcheryAim());
            archer.Add(new InputBuffer());
            archer.Add(new Simulated());
            registry.Add(archer);

            var world = new ArcheryWorld(registry, new WorldEventBuffer(), new ArcheryAimSystem(), TickInterval);
            return (world, registry, archer);
        }

        static void Feed(Entity entity, bool drawing, bool release)
        {
            entity.Get<InputBuffer>().Current = new InputCommand { Drawing = drawing, Release = release };
        }

        [Test]
        public void 쏘면_화살_목록에_들어간다()
        {
            var (world, _, archer) = Make();

            Feed(archer, drawing: true, release: false);
            world.Tick(1, TickInterval);
            Feed(archer, drawing: false, release: true);
            world.Tick(2, TickInterval);

            Assert.AreEqual(1, world.Shots.Count);
            Assert.AreEqual("archer-1", world.Shots[0].ShooterId);
        }

        [Test]
        public void 수명이_지난_화살은_사라진다()
        {
            var (world, _, archer) = Make();

            Feed(archer, drawing: true, release: false);
            world.Tick(1, TickInterval);
            Feed(archer, drawing: false, release: true);
            world.Tick(2, TickInterval);
            Assert.AreEqual(1, world.Shots.Count);

            Feed(archer, drawing: false, release: false);
            long expiryTick = 2 + (long)(ArcheryTrajectory.LifetimeSeconds / TickInterval) + 1;
            world.Tick(expiryTick, TickInterval);

            Assert.AreEqual(0, world.Shots.Count);
        }

        [Test]
        public void 되감으면_당김이_되살아난다()
        {
            var (world, _, archer) = Make();

            Feed(archer, drawing: true, release: false);
            world.Tick(10, TickInterval);
            world.SaveState(10);
            Assert.IsTrue(archer.Get<ArcheryAim>().Drawing);

            Feed(archer, drawing: false, release: true);
            world.Tick(11, TickInterval);
            Assert.IsFalse(archer.Get<ArcheryAim>().Drawing);

            world.LoadState(10);

            Assert.IsTrue(archer.Get<ArcheryAim>().Drawing);
            Assert.AreEqual(10, archer.Get<ArcheryAim>().DrawStartTick);
        }

        [Test]
        public void 되감으면_없던_화살도_사라진다()
        {
            var (world, _, archer) = Make();

            Feed(archer, drawing: true, release: false);
            world.Tick(10, TickInterval);
            world.SaveState(10);

            Feed(archer, drawing: false, release: true);
            world.Tick(11, TickInterval);
            Assert.AreEqual(1, world.Shots.Count);

            world.LoadState(10);

            Assert.AreEqual(0, world.Shots.Count);
        }
    }
}
```

- [ ] **Step 2: 테스트가 실패하는 것을 확인한다**

```bash
unity command recompile        --project-path "$CLIENT"
unity command recompile_status --project-path "$CLIENT"
unity command get_console_logs --severity error --limit 20 --project-path "$CLIENT"
```

Expected: `ArcheryWorld`가 없다는 CS0246.

> 베이스 계약은 확인해 두었다(아래 Step 3의 주석 참고). 그래도 한 번 눈으로 대조하면 싸다:
> ```bash
> grep -n "protected virtual\|public bool LoadState\|public void SaveState" \
>   C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/World/WorldBase.cs
> ```

- [ ] **Step 3: 월드를 쓴다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/ArcheryWorld.cs`:

```csharp
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// Archery의 시뮬 코어. 매 틱 조준을 갱신하고, 떠난 화살을 목록에 담아 수명이 다하면 지운다.
    /// <b>화살은 엔티티가 아니다</b> — 발사 정보만 있으면 위치가 계산되므로 레지스트리에 넣지 않는다.
    /// </summary>
    public class ArcheryWorld : GameFramework.World.WorldBase
    {
        private readonly ArcheryAimSystem aimSystem;
        private readonly float tickInterval;
        private readonly List<ArcheryShot> shots = new List<ArcheryShot>();

        // 되감기용 보관. 틱마다 그 시점의 조준 상태와 화살 목록을 통째로 둔다.
        private readonly Dictionary<long, SavedState> saved = new Dictionary<long, SavedState>();

        private readonly struct SavedState
        {
            public readonly List<ArcheryShot> Shots;
            public readonly Dictionary<string, ArcheryAim> Aims;

            public SavedState(List<ArcheryShot> shots, Dictionary<string, ArcheryAim> aims)
            {
                Shots = shots;
                Aims = aims;
            }
        }

        public IReadOnlyList<ArcheryShot> Shots => shots;

        public ArcheryWorld(GameFramework.World.EntityRegistry entityRegistry,
                            GameFramework.World.WorldEventBuffer eventBuffer,
                            ArcheryAimSystem aimSystem,
                            float tickInterval)
            : base(entityRegistry, eventBuffer)
        {
            this.aimSystem = aimSystem;
            this.tickInterval = tickInterval;
        }

        protected override void Mutation(long tick, float deltaTime)
        {
            foreach (var entity in EntityRegistry.All)
            {
                if (entity.Has<GameFramework.World.Simulated>() == false)
                {
                    continue;
                }

                var shot = aimSystem.Tick(entity, tick, tickInterval);
                if (shot.HasValue)
                {
                    shots.Add(shot.Value);
                }
            }

            RemoveExpired(tick);
        }

        // 화면 밖으로 나간 화살을 계속 들고 있으면 목록이 한 판 내내 자란다.
        private void RemoveExpired(long tick)
        {
            float lifetimeTicks = ArcheryTrajectory.LifetimeSeconds / tickInterval;
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                if (tick - shots[i].FireTick > lifetimeTicks)
                {
                    shots.RemoveAt(i);
                }
            }
        }

        protected override void SaveGameState(long tick)
        {
            var aims = new Dictionary<string, ArcheryAim>();
            foreach (var entity in EntityRegistry.All)
            {
                var aim = entity.Get<ArcheryAim>();
                if (aim != null)
                {
                    aims[entity.Id] = new ArcheryAim
                    {
                        Yaw = aim.Yaw, Pitch = aim.Pitch,
                        Drawing = aim.Drawing, DrawStartTick = aim.DrawStartTick,
                    };
                }
            }
            saved[tick] = new SavedState(new List<ArcheryShot>(shots), aims);
        }

        // 베이스가 bool을 요구한다 — 그 틱 기록이 없으면 false다.
        protected override bool LoadGameState(long tick)
        {
            if (saved.TryGetValue(tick, out var state) == false)
            {
                return false;
            }

            shots.Clear();
            shots.AddRange(state.Shots);

            foreach (var pair in state.Aims)
            {
                var aim = EntityRegistry.Get(pair.Key)?.Get<ArcheryAim>();
                if (aim == null)
                {
                    continue;
                }
                aim.Yaw = pair.Value.Yaw;
                aim.Pitch = pair.Value.Pitch;
                aim.Drawing = pair.Value.Drawing;
                aim.DrawStartTick = pair.Value.DrawStartTick;
            }

            return true;
        }
    }
}
```

> 확인된 베이스 계약(`GameFramework/Runtime/Scripts/World/WorldBase.cs`):
> `public EntityRegistry EntityRegistry { get; }` · `protected WorldBase(EntityRegistry, WorldEventBuffer)` ·
> `protected virtual void Mutation(long, float)` · `protected virtual void SaveGameState(long)` ·
> **`protected virtual bool LoadGameState(long)`**. 그리고 `LoadState`는 **베이스 프레임을 못 찾으면
> `LoadGameState`를 아예 부르지 않는다** — 그래서 사수 몸에 `Velocity`가 반드시 있어야 한다.

- [ ] **Step 4: 테스트가 통과하는 것을 확인한다**

```bash
unity command recompile_status --project-path "$CLIENT"
unity command run_tests  --mode EditMode --async_tests true --project-path "$CLIENT"
unity command test_status --project-path "$CLIENT"
```

Expected: `ArcheryWorldTests` 4개 PASS + 앞선 16개 PASS.

- [ ] **Step 5: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git status --short
git add Runtime/Scripts/Game/ArcheryWorld.cs Runtime/Scripts/Game/ArcheryWorld.cs.meta \
        Tests/EditMode/ArcheryWorldTests.cs Tests/EditMode/ArcheryWorldTests.cs.meta
git commit -m "feat(archery): 화살을 엔티티 없이 들고 있는 시뮬 코어를 만든다"
```

---

## Task 4: 와이어 — 조준·당김이 서버까지 간다

**Files:**
- Modify: `LeagueOfPhysical-Shared/Protos/InputCommand.proto`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/PlayerInputManager.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/MessageHandler/GameInputMessageHandler.cs`

**Interfaces:**
- Consumes: `LOP.InputCommand`(Task 2에서 늘린 것), proto `global::InputCommand`
- Produces:
  - `LOP.PlayerInputManager` — `void SetAim(float yaw, float pitch)`, `void SetDrawing(bool drawing)`, `void SetRelease()`

- [ ] **Step 1: proto에 필드를 더한다**

`LeagueOfPhysical-Shared/Protos/InputCommand.proto` — 마지막 필드 뒤에 이어서(번호는 **비어 있는
다음 수**를 쓴다. 기존 최대가 10이므로 11부터):

```proto
  // Archery: 좌우 조준 각도(도). 카메라가 보는 방향을 그대로 싣는다.
  float aim_yaw = 11;
  // Archery: 위아래 조준 각도(도). 양수면 위를 본다.
  float aim_pitch = 12;
  // Archery: 활을 당기고 있나. 손가락을 대고 있는 동안 참인 연속 값.
  bool drawing = 13;
  // Archery: 손을 뗀 틱에만 참인 이산 액션(jump와 같은 짝).
  bool release = 14;
```

- [ ] **Step 2: 생성물을 다시 만들고 MessageId가 안 움직였는지 확인한다**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
bash Scripts/compile_protos.sh
git diff --stat Runtime.Generated/Scripts/MessageIds.cs
```

Expected: `MessageIds.cs` diff **0줄**. `Runtime.Generated/Scripts/Protobuf/InputCommand.cs`만 바뀐다.
**MessageIds.cs가 바뀌었으면 거기서 멈추고 보고한다** — 와이어가 깨진다.

- [ ] **Step 3: 클라 송신에 필드를 잇는다**

`Assets/Scripts/Game/PlayerInputManager.cs`:

필드 선언부에 추가:

```csharp
        private float heldAimYaw;     // 연속 — 카메라가 매 프레임 갱신, 틱마다 샘플
        private float heldAimPitch;
        private bool heldDrawing;     // 연속 — 손가락을 대고 있는 동안 참
        private bool pendingRelease;  // 이산 — 소비 후 리셋
```

`Tick`의 `new InputCommand { ... }` 안에 추가:

```csharp
                AimYaw = heldAimYaw,
                AimPitch = heldAimPitch,
                Drawing = heldDrawing,
                Release = pendingRelease,
```

`Tick` 끝의 이산 액션 소비에 한 줄 추가:

```csharp
            pendingRelease = false;
```

`ToProto`의 반환 객체에 추가:

```csharp
                AimYaw = command.AimYaw,
                AimPitch = command.AimPitch,
                Drawing = command.Drawing,
                Release = command.Release,
```

setter 추가:

```csharp
        /// <summary>조준 각도 — 카메라가 보는 방향을 매 프레임 넣는다. 틱마다 샘플된다.</summary>
        public void SetAim(float yaw, float pitch)
        {
            heldAimYaw = yaw;
            heldAimPitch = pitch;
        }

        /// <summary>활을 당기고 있나. 손가락을 대고 있는 동안 참.</summary>
        public void SetDrawing(bool drawing)
        {
            heldDrawing = drawing;
        }

        /// <summary>손을 뗀 순간 한 번 부른다. 다음 틱 커맨드에 실려 나간다.</summary>
        public void SetRelease()
        {
            pendingRelease = true;
        }
```

- [ ] **Step 4: 서버 수신 변환에 필드를 잇는다**

`LeagueOfPhysical-Server/Assets/Scripts/Game/MessageHandler/GameInputMessageHandler.cs`를 열어
proto → 도메인 `InputCommand` 변환부를 찾고(기존 `Posture`/`Glide`/`Dash`가 옮겨지는 자리) 같은
자리에 네 줄을 더한다:

```csharp
                AimYaw = proto.AimYaw,
                AimPitch = proto.AimPitch,
                Drawing = proto.Drawing,
                Release = proto.Release,
```

같은 변환이 **클라의 원격 입력 수신**에도 있다. 빠뜨리면 *남의 화살만 안 보이는* 증상이 된다:

```bash
grep -rn "Posture = " C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts \
                      C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts
```

나온 **모든** 변환 지점에 같은 네 줄을 더한다.

- [ ] **Step 5: 양쪽 컴파일을 확인한다**

```bash
unity command recompile        --project-path "$CLIENT"
unity command recompile_status --project-path "$CLIENT"
unity command get_console_logs --severity error --limit 40 --project-path "$CLIENT"
unity command recompile        --project-path "$SERVER"
unity command recompile_status --project-path "$SERVER"
unity command get_console_logs --severity error --limit 40 --project-path "$SERVER"
```

Expected: 양쪽 다 CS 에러 0.

- [ ] **Step 6: 커밋 (레포 세 곳)**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git status --short
git add Protos/InputCommand.proto Runtime.Generated/Scripts/Protobuf/InputCommand.cs
git commit -m "feat(archery): 조준과 당김을 와이어에 싣는다"

cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git status --short
git add Assets/Scripts/Game/PlayerInputManager.cs
git commit -m "feat(archery): 조준·당김 입력을 보낸다"

cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git status --short
git add Assets/Scripts/Game/MessageHandler/GameInputMessageHandler.cs
git commit -m "feat(archery): 조준·당김 입력을 받는다"
```

---

## Task 5: 서버 덩어리 — 원형으로 세우고 60초 뒤 끝낸다

**Files:**
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Entity/ArcheryPlayerCreator.cs`
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Game/ArcheryRuleSystem.cs`
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Game/ArcheryLifetimeScope.cs`
- Create: `LeagueOfPhysical-Server/Assets/Scenes/Archery.unity`

**Interfaces:**
- Consumes: `LOP.{ICharacterCreator, IGameRuleSystem, IRoomDataStore, EntitySpawner, CharacterCreationData, SpawnPoint, SpawnPlacement, MatchOutcome, FinishPlacements, GameLifetimeScope, ArcheryWorld, ArcheryAimSystem}`,
  `GameFramework.World.{Entity, EntityRegistry, WorldEventBuffer, Transform, Velocity, IWorld}`
- Produces:
  - `LOP.ArcheryPlayerCreator : ICharacterCreator`
  - `LOP.ArcheryRuleSystem : IGameRuleSystem` — `MatchDurationTicks => 3000`
  - `LOP.ArcheryLifetimeScope : GameLifetimeScope`

- [ ] **Step 1: 플레이어 몸(서버)을 쓴다**

`LeagueOfPhysical-Server/Assets/Scripts/Entity/ArcheryPlayerCreator.cs`:

```csharp
using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// Archery의 플레이어 몸(서버). 걷지 않으므로 이동·접지·충돌 부품이 없다 —
    /// 이 게임에는 그런 개념이 없다. 대신 활을 겨누는 상태를 갖는다.
    /// </summary>
    public class ArcheryPlayerCreator : ICharacterCreator
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public ArcheryPlayerCreator(GameFramework.World.EntityRegistry entityRegistry)
        {
            this.entityRegistry = entityRegistry;
        }

        public void Create(CharacterCreationData creationData)
        {
            var worldEntity = new GameFramework.World.Entity(creationData.entityId);
            worldEntity.Add(new GameFramework.World.Transform
            {
                Position = creationData.position.ToNumerics(),
                Rotation = Quaternion.Euler(creationData.rotation).ToNumerics(),
            });
            worldEntity.Add(new GameFramework.World.Velocity());
            worldEntity.Add(new EntityKind(EntityType.Character));
            worldEntity.Add(new Appearance(creationData.visualId));
            worldEntity.Add(new ArcheryAim());
            worldEntity.Add(new InputBuffer());
            entityRegistry.Add(worldEntity);

            Debug.Log($"[World] Registered archer {worldEntity.Id}");
        }
    }
}
```

- [ ] **Step 2: 룰을 쓴다**

`LeagueOfPhysical-Server/Assets/Scripts/Game/ArcheryRuleSystem.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// Archery 룰(서버). 참가자를 사대에 세우고 60초 뒤 판을 끝낸다.
    /// <b>점수는 슬라이스 2가 여기에 붙는다</b> — 지금은 순위를 가릴 근거가 없어 전원 동순위다.
    /// </summary>
    public class ArcheryRuleSystem : IGameRuleSystem
    {
        // 맵에 사대 마커가 없을 때만 쓰는 폴백. 원 둘레에 등간격으로 세운다.
        private const float FallbackRingRadius = 12f;

        private const string BodyVisualId = "Assets/Art/Characters/Knight/Knight.prefab";

        private readonly IRoomDataStore roomDataStore;
        private readonly EntitySpawner entitySpawner;
        private readonly Dictionary<string, string> entityIdToUserId = new Dictionary<string, string>();

        public ArcheryRuleSystem(IRoomDataStore roomDataStore, EntitySpawner entitySpawner)
        {
            this.roomDataStore = roomDataStore;
            this.entitySpawner = entitySpawner;
        }

        public void Initialize()
        {
            entityIdToUserId.Clear();

            // 사대 위치는 맵이 정한다 — 룰이 좌표를 들고 있으면 맵을 새로 만들 때마다 룰을 고쳐야 한다.
            var slots = SpawnPlacement.Arrange(
                UnityEngine.Object.FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None));

            var playerList = roomDataStore.match.playerList;
            for (int i = 0; i < playerList.Length; i++)
            {
                Vector3 position = slots.Count > 0 ? slots[i % slots.Count] : RingSlot(i, playerList.Length);

                // 가운데를 바라보게 세운다 — 사대는 원의 안쪽을 본다.
                Vector3 lookAtCenter = new Vector3(0f, Mathf.Atan2(-position.x, -position.z) * Mathf.Rad2Deg, 0f);

                string entityId = entitySpawner.GenerateEntityId();
                entityIdToUserId[entityId] = playerList[i];

                entitySpawner.Spawn(new CharacterCreationData
                {
                    userId = playerList[i],
                    entityId = entityId,
                    visualId = BodyVisualId,
                    characterCode = "",
                    position = position,
                    rotation = lookAtCenter,
                    velocity = Vector3.zero,
                });
            }
        }

        private static Vector3 RingSlot(int index, int total)
        {
            float angle = (index / (float)Mathf.Max(total, 1)) * Mathf.PI * 2f;
            return new Vector3(Mathf.Sin(angle) * FallbackRingRadius, 0f, Mathf.Cos(angle) * FallbackRingRadius);
        }

        public void Deinitialize()
        {
            entityIdToUserId.Clear();
        }

        /// <summary>이 슬라이스에는 끝낼 조건이 없다 — 시간만으로 끝난다.</summary>
        public bool IsMatchOver => false;

        /// <summary>50Hz × 60초.</summary>
        public long MatchDurationTicks => 3000;

        /// <summary>
        /// 점수가 없으므로 전원 동순위다. 슬라이스 2가 점수를 진행도로 넘긴다.
        /// </summary>
        public MatchOutcome ResolveOutcome()
        {
            var unfinished = new List<(string userId, float progress)>();
            foreach (var pair in entityIdToUserId)
            {
                unfinished.Add((pair.Value, 0f));
            }

            // 첫 인자는 도착 기록 목록(FinishRecord)이다 — 문자열이 아니다. 이 게임엔 결승선이 없어 빈 목록.
            return FinishPlacements.Resolve(
                System.Array.Empty<FinishRecord>(), entityIdToUserId,
                unfinished, System.Array.Empty<string>(), new List<string>());
        }
    }
}
```

> `FinishPlacements.Resolve`의 실제 시그니처를 먼저 확인한다:
> ```bash
> grep -n "public static MatchOutcome Resolve" -A 8 \
>   C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Domain/FinishPlacements.cs
> ```
> 위 grep은 **`Assets/Scripts/Domain/FinishPlacements.cs`** 를 본다(`Scripts/Game/`이 아니다).
> 확인된 시그니처:
>
> ```csharp
> public static MatchOutcome Resolve(
>     IReadOnlyList<FinishRecord> finished,
>     IReadOnlyDictionary<string, string> entityIdToUserId,
>     IReadOnlyList<(string userId, float progress)> unfinished,
>     IReadOnlyList<string> eliminated,
>     IReadOnlyList<string> left)
> ```
>
> 공동 순위는 다음 등수를 그만큼 건너뛴다(1,1,3 — 스포츠 표준). 전원 진행도가 0이면 전원 1위가 된다.

- [ ] **Step 3: 서버 스코프를 쓴다**

`LeagueOfPhysical-Server/Assets/Scripts/Game/ArcheryLifetimeScope.cs`:

```csharp
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>Archery 덩어리(서버) — 제자리에 선 사수들, 날아가는 화살.</summary>
    public class ArcheryLifetimeScope : GameLifetimeScope
    {
        // 50Hz. 시뮬이 당긴 시간을 초로 환산할 때 쓴다.
        private const float TickInterval = 0.02f;

        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.Register<ArcheryAimSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.World.IWorld>(c => new ArcheryWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<ArcheryAimSystem>(),
                TickInterval), Lifetime.Singleton);

            builder.Register<ICharacterCreator, ArcheryPlayerCreator>(Lifetime.Singleton);
            builder.Register<IGameRuleSystem, ArcheryRuleSystem>(Lifetime.Singleton);
        }
    }
}
```

- [ ] **Step 4: 서버 게임 씬을 만든다**

기존 `Skydive.unity`를 복제해 만든다. **`LOPGameSceneCoordinator`를 지우지 않는다** — 카메라를 내
캐릭터에 물리는 코드가 공용 DI가 아니라 씬 컴포넌트다.

```bash
unity command copy_asset --from Assets/Scenes/Skydive.unity --to Assets/Scenes/Archery.unity --project-path "$SERVER"
```

그다음 에디터에서(또는 `unity command`로):
1. `SkydiveLifetimeScope` 컴포넌트를 제거하고 `ArcheryLifetimeScope`를 붙인다.
   **규칙은 "타입 이름이 `Skydive`로 시작하는 컴포넌트만 제거"** 다.
2. 갈아끼우면 **상속된 `runner` 직렬화 참조가 끊긴다.** 다시 물리고 확인한다:
   ```bash
   unity command get_serialized_fields --object <ArcheryLifetimeScope> --project-path "$SERVER"
   ```
   `{fileID: 0}`이 없어야 한다.
3. 빌드 세팅에 추가:
   ```bash
   unity command add_scene_to_build --path Assets/Scenes/Archery.unity --project-path "$SERVER"
   ```

- [ ] **Step 5: 컴파일을 확인한다**

```bash
unity command recompile        --project-path "$SERVER"
unity command recompile_status --project-path "$SERVER"
unity command get_console_logs --severity error --limit 40 --project-path "$SERVER"
```

Expected: CS 에러 0.

- [ ] **Step 6: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git status --short
git add Assets/Scripts/Entity/ArcheryPlayerCreator.cs Assets/Scripts/Entity/ArcheryPlayerCreator.cs.meta \
        Assets/Scripts/Game/ArcheryRuleSystem.cs Assets/Scripts/Game/ArcheryRuleSystem.cs.meta \
        Assets/Scripts/Game/ArcheryLifetimeScope.cs Assets/Scripts/Game/ArcheryLifetimeScope.cs.meta \
        Assets/Scenes/Archery.unity Assets/Scenes/Archery.unity.meta \
        ProjectSettings/EditorBuildSettings.asset
git commit -m "feat(archery): 사대에 세우고 60초를 재는 서버 룰을 붙인다"
```

---

## Task 6: 클라 덩어리 — 겨누고, 당기고, 화살이 난다

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Entity/ArcheryPlayerCreator.cs`
- Create: `LeagueOfPhysical-Client/Assets/Scripts/UI/ArcheryPad/ArcheryPadViewModel.cs`
- Create: `LeagueOfPhysical-Client/Assets/Scripts/UI/ArcheryPad/ArcheryPadView.cs`
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Game/ArcheryArrowView.cs`
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Game/ArcheryAimView.cs`
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Game/ArcheryLifetimeScope.cs`
- Create: `LeagueOfPhysical-Client/Assets/Scenes/Archery.unity`

**Interfaces:**
- Consumes: `LOP.{PlayerInputManager, CameraController, IPlayerContext, GameLifetimeScope, ArcheryWorld, ArcheryAimSystem, ArcheryTrajectory, ICharacterCreator, IEntitySyncPolicy, CharactersPredictedSyncPolicy, IGameDataStore}`,
  `GameFramework.World.EntityRegistry`, `GameFramework.Runner.{IRunner, ITickSystem}`, R3, VContainer
- Produces:
  - `LOP.ArcheryPlayerCreator : ICharacterCreator`
  - `LOP.UI.ArcheryPadViewModel` — `void LookBy(Vector2 deltaPixels)`, `void BeginDraw()`, `void EndDraw()`
  - `LOP.UI.ArcheryPadView`
  - `LOP.ArcheryAimView : VContainer.Unity.ILateTickable`
  - `LOP.ArcheryArrowView : VContainer.Unity.ILateTickable`
  - `LOP.ArcheryLifetimeScope : GameLifetimeScope`

> **화면 시각은 `renderTick`으로 통일한다.** 시뮬은 정수 틱으로 돌지만 화면은 더 자주 그려지므로,
> 뷰는 `renderTick = (runner.tickUpdater.elapsedTime - interval) / interval`을 쓴다.
> **⛔ `tickUpdater.tick`을 뷰에서 쓰지 않는다** — 틱 루프가 `onTick` 뒤에 `tick++` 하므로 그 값은
> *아직 일어나지 않은 틱*이고, 화살이 몸보다 앞서 그려진다. `SkydiveLaserView.cs`가 정답지다.

- [ ] **Step 1: 플레이어 몸(클라)을 쓴다**

`Assets/Scripts/Entity/ArcheryPlayerCreator.cs` — 서버 것과 같되 내 캐릭터를 기억한다:

```csharp
using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>Archery의 플레이어 몸(클라). 걷지 않으므로 이동 부품이 없다.</summary>
    public class ArcheryPlayerCreator : ICharacterCreator
    {
        private readonly IGameDataStore gameDataStore;
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public ArcheryPlayerCreator(IGameDataStore gameDataStore, IPlayerContext playerContext,
                                    GameFramework.World.EntityRegistry entityRegistry)
        {
            this.gameDataStore = gameDataStore;
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
        }

        public void Create(CharacterCreationData creationData)
        {
            var worldEntity = new GameFramework.World.Entity(creationData.entityId);
            worldEntity.Add(new GameFramework.World.Transform
            {
                Position = creationData.position.ToNumerics(),
                Rotation = Quaternion.Euler(creationData.rotation).ToNumerics(),
            });
            worldEntity.Add(new GameFramework.World.Velocity());
            worldEntity.Add(new EntityKind(EntityType.Character));
            worldEntity.Add(new Appearance(creationData.visualId));
            worldEntity.Add(new ArcheryAim());

            // 남의 몸도 입력 버퍼를 갖는다 — 서버가 남의 입력을 되뿌려 주고(EntityInputBroadcastSystem)
            // RemoteInputSystem이 여기에 채운다. 이 게임은 남도 굴리므로(CharactersPredictedSyncPolicy)
            // 그 입력이 실제로 읽혀서 남의 화살도 내 화면에서 같은 규칙으로 날아간다.
            worldEntity.Add(new InputBuffer());
            entityRegistry.Add(worldEntity);

            if (gameDataStore.userEntityId == creationData.entityId)
            {
                playerContext.entityId = creationData.entityId;
            }

            Debug.Log($"[World] Registered archer {worldEntity.Id}");
        }
    }
}
```

- [ ] **Step 2: 조작 패드를 쓴다**

`Assets/Scripts/UI/ArcheryPad/ArcheryPadViewModel.cs` — **넘기기만 한다.** 조준 각도도, 당긴 정도도
여기서 따로 세지 않는다. 그 값들은 시뮬(`ArcheryAim`)이 이미 들고 있고, 화면이 같은 값을 두 번 세면
언젠가 갈라진다.

```csharp
using UnityEngine;

namespace LOP.UI
{
    /// <summary>
    /// 조작 패드의 커맨드. 터치 좌표 해석은 View가 하고, 여기서는 그 결과를 시점과 입력으로 넘긴다.
    /// <b>조준은 카메라가 보는 방향</b>이라 각도를 여기서 들고 있지 않다 — 매 프레임 카메라에서
    /// 읽어 입력에 싣는 일은 <see cref="LOP.ArcheryAimView"/>가 한다.
    /// </summary>
    public class ArcheryPadViewModel
    {
        private readonly PlayerInputManager input;
        private readonly CameraController cameraController;

        private bool drawing;

        public ArcheryPadViewModel(PlayerInputManager input, CameraController cameraController)
        {
            this.input = input;
            this.cameraController = cameraController;
        }

        /// <summary>왼쪽 영역 드래그 — 시점을 돌린다. 조준은 이 시점을 그대로 따른다.</summary>
        public void LookBy(Vector2 deltaPixels)
        {
            cameraController.ProcessTouchInput(deltaPixels);
        }

        public void BeginDraw()
        {
            drawing = true;
            input.SetDrawing(true);
        }

        /// <summary>두 번 불려도 한 번만 쏜다 — 아래 View가 뗌을 두 경로로 받기 때문이다.</summary>
        public void EndDraw()
        {
            if (drawing == false)
            {
                return;
            }
            drawing = false;
            input.SetDrawing(false);
            input.SetRelease();
        }
    }
}
```

`Assets/Scripts/UI/ArcheryPad/ArcheryPadView.cs` — **UXML 로드·윈도우 매니저 등록·구독 해제 관례는
`SkydivePadView.cs`를 그대로 따른다**(같은 폴더 구조·같은 등록 방식). 이 게임에서 새로 쓰는 것은
아래 이벤트 배선뿐이다. 화면을 좌/우 절반으로 나눈 두 `VisualElement`에 붙인다:

```csharp
        // 왼쪽 절반 — 끌면 시점이 돈다. 손가락을 대고 있는 동안만 받는다.
        left.RegisterCallback<PointerDownEvent>(evt => left.CapturePointer(evt.pointerId));
        left.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (left.HasPointerCapture(evt.pointerId))
            {
                viewModel.LookBy(evt.deltaPosition);
            }
        });
        left.RegisterCallback<PointerUpEvent>(evt => left.ReleasePointer(evt.pointerId));

        // 오른쪽 절반 — 누르면 당기고 떼면 쏜다.
        right.RegisterCallback<PointerDownEvent>(evt =>
        {
            right.CapturePointer(evt.pointerId);
            viewModel.BeginDraw();
        });
        right.RegisterCallback<PointerUpEvent>(evt =>
        {
            right.ReleasePointer(evt.pointerId);
            viewModel.EndDraw();
        });
        // 손가락이 화면 밖으로 나가면 위의 Up이 안 온다 — 그대로 두면 시위를 당긴 채 영영 멈춘다.
        right.RegisterCallback<PointerCaptureOutEvent>(_ => viewModel.EndDraw());
```

> **포인터를 잡는(capture) 이유**: 두 손가락이 동시에 올라가는 조작이라, 잡아 두지 않으면 왼쪽에서
> 시작한 드래그가 오른쪽 영역으로 넘어갔을 때 이벤트가 엉뚱한 쪽으로 간다.

- [ ] **Step 3: 조준을 넘기고 시야를 좁힌다**

`Assets/Scripts/Game/ArcheryAimView.cs`:

```csharp
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 매 프레임 카메라가 보는 방향을 조준으로 넘기고, <b>당기는 동안 시야를 좁힌다.</b>
    /// 좁아지는 만큼 어디에 무엇이 뜨는지 못 보게 되는 것이 당김의 대가다.
    /// 당긴 정도는 여기서 따로 세지 않고 시뮬 상태(<see cref="ArcheryAim"/>)를 읽는다 —
    /// 화면이 같은 값을 두 번 세면 언젠가 갈라진다.
    /// </summary>
    public class ArcheryAimView : ILateTickable
    {
        private const float WideFov = 60f;
        private const float DrawnFov = 32f;
        private const float FovLerpPerSecond = 8f;

        private readonly GameFramework.Runner.IRunner runner;
        private readonly PlayerInputManager input;
        private readonly CameraController cameraController;
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public ArcheryAimView(GameFramework.Runner.IRunner runner, PlayerInputManager input,
                              CameraController cameraController, IPlayerContext playerContext,
                              GameFramework.World.EntityRegistry entityRegistry)
        {
            this.runner = runner;
            this.input = input;
            this.cameraController = cameraController;
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
        }

        public void LateTick()
        {
            var camera = cameraController.MainCamera;
            if (camera == null)
            {
                return;
            }

            // 유니티의 x 회전은 양수가 아래를 본다. 조준 각도는 양수가 위이므로 부호를 뒤집는다.
            float yaw = camera.transform.eulerAngles.y;
            float pitch = -Mathf.DeltaAngle(0f, camera.transform.eulerAngles.x);
            input.SetAim(yaw, pitch);

            camera.fieldOfView = Mathf.Lerp(
                camera.fieldOfView,
                Mathf.Lerp(WideFov, DrawnFov, MyDrawRatio()),
                Time.deltaTime * FovLerpPerSecond);
        }

        private float MyDrawRatio()
        {
            if (playerContext.entityId == null)
            {
                return 0f;
            }
            var aim = entityRegistry.Get(playerContext.entityId)?.Get<ArcheryAim>();
            if (aim == null || aim.Drawing == false)
            {
                return 0f;
            }

            double interval = runner.tickUpdater.interval;
            if (interval <= 0d)
            {
                return 0f;
            }
            // 화면 시각은 정수 틱이 아니라 renderTick이다 — 시뮬과 같은 식에 같은 시각을 넣는다.
            double renderTick = (runner.tickUpdater.elapsedTime - interval) / interval;
            return ArcheryAimSystem.DrawRatio(aim.DrawStartTick, (long)System.Math.Floor(renderTick), (float)interval);
        }
    }
}
```

- [ ] **Step 4: 화살을 그린다**

`Assets/Scripts/Game/ArcheryArrowView.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 날아가는 화살을 그린다. <b>시뮬과 같은 식에 같은 시각을 넣으므로</b> 그림과 계산이 어긋나지 않는다.
    /// 화살은 엔티티가 아니라서 뷰가 직접 월드의 발사 목록을 읽는다.
    /// </summary>
    public class ArcheryArrowView : ILateTickable
    {
        private readonly GameFramework.Runner.IRunner runner;
        private readonly ArcheryWorld world;

        // 목록의 자리(index)로 화살을 알아보면 안 된다 — 수명이 다한 화살이 빠지면 뒤 화살들의
        // 자리가 앞으로 당겨져서, 남아 있는 화살이 남의 궤적으로 순간이동한다.
        // 쏜 사람과 쏜 틱은 절대 안 바뀌므로 그 둘을 이름으로 쓴다.
        private readonly Dictionary<(string shooterId, long fireTick), GameObject> drawn =
            new Dictionary<(string, long), GameObject>();
        private readonly List<(string, long)> stale = new List<(string, long)>();

        public ArcheryArrowView(GameFramework.Runner.IRunner runner, ArcheryWorld world)
        {
            this.runner = runner;
            this.world = world;
        }

        public void LateTick()
        {
            double interval = runner.tickUpdater.interval;
            if (interval <= 0d)
            {
                return;
            }
            double renderTick = (runner.tickUpdater.elapsedTime - interval) / interval;

            var shots = world.Shots;
            var alive = new HashSet<(string, long)>();

            for (int i = 0; i < shots.Count; i++)
            {
                var key = (shots[i].ShooterId, shots[i].FireTick);
                alive.Add(key);

                if (drawn.TryGetValue(key, out var arrow) == false || arrow == null)
                {
                    arrow = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    arrow.transform.localScale = new Vector3(0.05f, 0.05f, 0.6f);
                    Object.Destroy(arrow.GetComponent<Collider>());   // 그림일 뿐이다 — 판정은 시뮬이 한다
                    drawn[key] = arrow;
                }

                float seconds = (float)((renderTick - shots[i].FireTick) * interval);
                if (seconds < 0f)
                {
                    seconds = 0f;   // 아직 떠나기 전 프레임 — 출발점에 둔다
                }
                arrow.transform.position = ArcheryTrajectory.PositionAt(shots[i], seconds);

                var velocity = ArcheryTrajectory.VelocityAt(shots[i], seconds);
                if (velocity.sqrMagnitude > 1e-6f)
                {
                    arrow.transform.rotation = Quaternion.LookRotation(velocity);
                }
            }

            stale.Clear();
            foreach (var pair in drawn)
            {
                if (alive.Contains(pair.Key) == false)
                {
                    stale.Add(pair.Key);
                }
            }
            foreach (var key in stale)
            {
                Object.Destroy(drawn[key]);
                drawn.Remove(key);
            }
        }
    }
}
```

> **임시 그림이다.** 큐브 프리미티브는 화살 모델이 나오면 교체한다. 지금 필요한 것은
> *포물선이 눈에 보이는가* 하나다.
>
> `IRunner.tickUpdater`의 `interval`/`elapsedTime` 이름은 `SkydiveLaserView.cs`에서 확인한 것과
> 같다. 다르면 그 파일에 맞춘다.

- [ ] **Step 5: 클라 스코프를 쓴다**

`Assets/Scripts/Game/ArcheryLifetimeScope.cs`:

```csharp
using System;
using System.Collections.Generic;
using LOP.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>Archery 덩어리(클라) — 제자리에 선 사수들, 남도 굴려서 남의 화살도 보인다.</summary>
    public class ArcheryLifetimeScope : GameLifetimeScope
    {
        private const float TickInterval = 0.02f;

        [SerializeField] private CameraController cameraController;

        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.RegisterComponent(cameraController);

            builder.Register<ArcheryAimSystem>(Lifetime.Singleton);
            builder.Register<ArcheryWorld>(c => new ArcheryWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<ArcheryAimSystem>(),
                TickInterval), Lifetime.Singleton)
                .As<GameFramework.World.IWorld>().AsSelf();

            builder.Register<ICharacterCreator, ArcheryPlayerCreator>(Lifetime.Singleton);

            // 남도 굴린다 — 서버가 되뿌린 남의 입력이 남의 활을 같은 규칙으로 당기게 한다.
            builder.Register<IEntitySyncPolicy, CharactersPredictedSyncPolicy>(Lifetime.Singleton);

            builder.RegisterEntryPoint<ArcheryAimView>().AsSelf();
            builder.RegisterEntryPoint<ArcheryArrowView>().AsSelf();

            builder.Register<ArcheryPadViewModel>(Lifetime.Transient);
            builder.Register<ArcheryPadView>(Lifetime.Transient);
        }

        protected override void RegisterViewFactories(
            IObjectResolver container, IWindowManager windowManager, List<IDisposable> sink)
        {
            sink.Add(windowManager.RegisterViewFactory<ArcheryPadView>(
                () => container.Resolve<ArcheryPadView>()));
        }
    }
}
```

> `SkydiveLifetimeScope`가 등록하는 것 중 **이 게임에 없는 개념**(WindField·LaserField·DoorField·
> BodyCollisionSystem·FinishSystem·IServerCorrectionHandler·IExtrapolationAcceleration)은 넣지 않는다.
> 단 **씬 마커나 `EntityBinder`의 생성자가 요구하는 등록**이 빠지면 주입이 그 자리에서 끊긴다.
> 컴파일 통과 후 **실제로 방에 들어가 보고** 콘솔에 주입 실패가 없는지 확인한다(Task 7 Step 5).

- [ ] **Step 6: 클라 게임 씬을 만든다**

```bash
unity command copy_asset --from Assets/Scenes/Skydive.unity --to Assets/Scenes/Archery.unity --project-path "$CLIENT"
```

Task 5 Step 4와 같은 규칙으로 컴포넌트를 갈아끼운다. **클라 씬은 `cameraController` 직렬화 참조도
다시 물려야 한다**(스코프의 `[SerializeField]`).

```bash
unity command get_serialized_fields --object <ArcheryLifetimeScope> --project-path "$CLIENT"
unity command add_scene_to_build --path Assets/Scenes/Archery.unity --project-path "$CLIENT"
```

Expected: `runner`·`cameraController` 모두 `{fileID: 0}`이 아니다.

- [ ] **Step 7: 컴파일을 확인한다**

```bash
unity command recompile        --project-path "$CLIENT"
unity command recompile_status --project-path "$CLIENT"
unity command get_console_logs --severity error --limit 40 --project-path "$CLIENT"
```

Expected: CS 에러 0.

- [ ] **Step 8: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git status --short
git add Assets/Scripts/Entity/ArcheryPlayerCreator.cs Assets/Scripts/Entity/ArcheryPlayerCreator.cs.meta \
        Assets/Scripts/UI/ArcheryPad Assets/Scripts/UI/ArcheryPad.meta \
        Assets/Scripts/Game/ArcheryArrowView.cs Assets/Scripts/Game/ArcheryArrowView.cs.meta \n        Assets/Scripts/Game/ArcheryAimView.cs Assets/Scripts/Game/ArcheryAimView.cs.meta \
        Assets/Scripts/Game/ArcheryLifetimeScope.cs Assets/Scripts/Game/ArcheryLifetimeScope.cs.meta \
        Assets/Scenes/Archery.unity Assets/Scenes/Archery.unity.meta \
        ProjectSettings/EditorBuildSettings.asset
git commit -m "feat(archery): 겨누고 당겨서 쏘는 클라 덩어리를 붙인다"
```

---

## Task 7: 맵과 데이터 — 실제로 들어가진다

**Files:**
- Create: `LeagueOfPhysical-Art/Scenes/ArcheryCircleMap.unity` (= 클라 서브모듈의 `Assets/Art/Scenes/ArcheryCircleMap.unity`)
- Modify: `LeagueOfPhysical-Client/Assets/AddressableAssetsData/AssetGroups/Scene.asset`
- Modify: `infrastructure/table/Datas/#GameMode.xlsx` `#Map.xlsx` `#Queue.xlsx`
- Commit: 클라의 아트 포인터(`chore(art)`)

- [ ] **Step 1: 원형맵을 만든다 (아트 서브모듈에서)**

⚠️ **아트 체크아웃이 둘이다.** 에디터가 여는 것은 **클라의 서브모듈**
(`LeagueOfPhysical-Client/Assets/Art`)이다. 독립 클론(`LOP/LeagueOfPhysical-Art`)에 만들면 에디터가
영영 못 본다. Art 레포 내부 경로는 `Scenes/…`다.

맵에 넣을 것:
- 바닥 평면 (원 반지름 12m를 넉넉히 덮게)
- **사대 마커 `SpawnPoint` 8개** — 반지름 12m 원 둘레에 45도 간격
- 가운데 무대 — 슬라이스 2에서 과녁이 뜰 자리. 지금은 지름 4m 원기둥 하나면 충분하다
- 조명

```bash
unity command create_scene --path Assets/Art/Scenes/ArcheryCircleMap.unity --project-path "$CLIENT"
# 마커 8개를 원 둘레에 배치 (create_gameobjects의 위치 배열 사용)
# 각 마커에 SpawnPoint 컴포넌트를 붙인다
unity command save_scene --project-path "$CLIENT"
```

- [ ] **Step 2: 어드레서블에 등록한다 — 반드시 원격 그룹**

맵 씬은 Art에 있지만 **등록 파일은 클라 저장소**(`Assets/AddressableAssetsData/AssetGroups/Scene.asset`)다.
**로컬 그룹에 넣으면 서버가 영영 못 받는다.**

YAML 손편집 대신:

```bash
unity command eval --project-path "$CLIENT" --code '
  var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
  var group = settings.FindGroup("Scene");
  var guid = UnityEditor.AssetDatabase.AssetPathToGUID("Assets/Art/Scenes/ArcheryCircleMap.unity");
  var entry = settings.CreateOrMoveEntry(guid, group);
  return entry.address + " -> " + group.Name;
'
```

Expected: 반환 문자열이 `Scene` 그룹을 가리킨다.

- [ ] **Step 3: 마스터데이터 행을 넣는다**

⚠️ **`#Map.xlsx`는 5열**(`id | game_mode_id | code | name | scene_path`)이다. 생성된 json에 `name`이
없는 것은 그것이 클라 전용 컬럼이라 그렇다 — **4열로 착각해 옮기면 씬 경로가 `name` 칸에 들어가고
맵 로드가 조용히 실패한다.** 반드시 원본 xlsx를 openpyxl로 열어 헤더를 확인한다.

- `#GameMode.xlsx` — 빈 `TargetShooting` 행을 고친다:
  `code = Archery`, `name = 활쏘기`, `description = 함정을 가려내며 적은 과녁을 다투는 활쏘기`,
  `min_players = 1`, `max_players = 8`, `scene_path = Assets/Scenes/Archery.unity`
- `#Map.xlsx` — 한 줄: `game_mode_id` = 위 행의 id, `code = archery_circle`, `name = 원형 사대`,
  `scene_path = Assets/Art/Scenes/ArcheryCircleMap.unity`
- `#Queue.xlsx` — `AllowedGameModeIds`에 이 모드 id를 넣어 매칭이 잡히게 한다

그다음 생성:

```bash
cd C:/Users/re5na/workspace/LOP/infrastructure/table
bash gen.sh
```

Expected: MasterData-Client / MasterData-Server 양쪽에 `.cs`·`.bytes` 변경.
**`.meta`는 유니티가 만들고 나서 add한다**(재스캔 대기).

- [ ] **Step 4: 아트 포인터를 커밋한다**

**순서를 지킨다.** 포인터가 안 올라가면 CI 체크아웃에 맵이 **존재하지 않아** 번들이 비어서 나간다.

```bash
# ① 아트 main 머지·푸시 (아트 레포의 자체 절차)
# ② 클라 피처 브랜치에서 서브모듈 포인터만 커밋
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git status --short           # ' m Assets/Art' 확인
git add Assets/Art           # ← 경로 지정. -A 금지
git commit -m "chore(art): 원형 사대 맵을 가리킨다"
```

- [ ] **Step 5: 끝-끝으로 확인한다**

클라 두 대(메인 에디터 + MPPM 클론)와 서버를 띄우고 `Archery`로 매칭한다.

확인할 것:

1. 방에 들어가고 **사대 원 둘레에 서로 다른 자리로** 선다
2. 왼쪽을 끌면 **시점이 돌고**, 오른쪽을 누르면 **화면이 좁아진다**
3. 떼면 **화살이 포물선으로 날아간다**
4. **다른 클라의 화살도 보인다** ← 원격 입력 중계가 살아 있다는 증거
5. 60초 뒤 **결과 화면이 뜬다**(순위는 전원 동순위 — 정상)
6. 콘솔에 주입 실패·NRE가 없다

> ⚠️ **환경을 바꿨으면 Play를 다시 시작한다** — 환경 설정은 부팅 때만 읽힌다.
> ⚠️ **게임서버 이미지 태그가 낡으면 접속 몇 초 뒤 예외 없이 튕긴다.** 코드를 파기 전에
> `GAME_SERVER_IMAGE` 태그와 서버 레포 main의 커밋 차를 먼저 본다.

- [ ] **Step 6: 커밋 (레포 세 곳)**

```bash
cd C:/Users/re5na/workspace/LOP/infrastructure
git status --short
git add table/Datas/#GameMode.xlsx table/Datas/#Map.xlsx table/Datas/#Queue.xlsx
git commit -m "feat(archery): 게임 모드와 원형맵을 데이터에 올린다"

cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client
git status --short && git add -u && git commit -m "chore: regenerate for archery"

cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server
git status --short && git add -u && git commit -m "chore: regenerate for archery"

cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git status --short
git add Assets/AddressableAssetsData/AssetGroups/Scene.asset
git commit -m "feat(archery): 원형맵을 원격 어드레서블 그룹에 등록한다"
```

---

## 완료 기준

- [ ] LOP-Shared EditMode 테스트 20개가 통과한다(궤적 7 · 조준 9 · 월드 4)
- [ ] 클·서 양쪽 컴파일 에러 0
- [ ] 로비에서 `Archery`를 골라 방에 들어가진다
- [ ] 사대 원 둘레에 서로 다른 자리로 선다
- [ ] 왼손 드래그로 시점이 돌고, 오른손을 누르면 화면이 좁아지고, 떼면 화살이 포물선으로 난다
- [ ] **다른 클라가 쏜 화살이 내 화면에도 보인다**
- [ ] 60초 뒤 결과 화면이 뜬다
- [ ] 8개 레포 중 손댄 곳(Shared·Client·Server·Art·infrastructure·MasterData×2)이 각각 푸시 규약대로 올라갔다
