# Skydive 코스 — 강체 구조물과 발판 보급 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 빈 하늘이던 1000m 낙하로에 **뚫고 지나가야 하는 선반**이 생기고, 그 선반 위에 내려서면 **스태미나가 찬다.**

**Architecture:** 이동을 형제 게임(Flappy Race)과 **같은 모양**으로 맞춘다 — `*MoveSystem`은 속도만 정하고, 맵과의 충돌(collide-and-slide)은 `*World`가 공유 커널 `KinematicMover`로 처리한다. 접지 여부는 그 커널이 이미 돌려주므로(`KinematicMoveResult.grounded`) 스태미나 회복은 **새 판정 없이** 그 값을 쓰면 된다. 코스 지오메트리는 손으로 YAML을 짜지 않고 **에디터 도구가 표 하나에서 굽는다** — 표가 곧 코스 설계이고, 다시 돌릴 수 있으며, 리뷰가 가능하다.

**Tech Stack:** Unity 6.3, VContainer, GameFramework World Core(`ICollisionQuery`), LOP-Shared `KinematicMover`, Mirror

**Spec:** `docs/superpowers/specs/2026-08-30-skydive-game-mode-design.md` (§3.4 강체는 세 가지 일을 한다, §2.2 스태미나, §7.4 코스 검사, §8 슬라이스 3)

## 이 슬라이스가 생긴 이유

스펙 슬라이스 3은 "코스와 결승"인데, 앞선 작업(`2026-08-30-skydive-slice3-visible-posture-and-finish.md`)은 **결승 쪽 절반만** 했다. 코스 쪽 절반이 그대로 남아 있고, 코드가 스스로 그렇게 말하고 있다:

- `SkydiveMoveSystem` — `/// 지형 충돌은 슬라이스 3이 얹는다(지금은 임시 바닥만).`
- `SkydiveWorld.Mutation` — `// 임시 바닥에 닿아 있으면 "발 딛고 있다"로 본다 — 슬라이스 3이 이 판정을 진짜 지면 접촉으로 바꾼다.`

지금 `SkydiveMap.unity`에는 바닥 한 장(200×200)과 스폰 8개, 결승선 마커뿐이다. 1000m가 **완전한 빈 하늘**이라 자세를 바꿀 이유가 없고, 스태미나는 회복 경로가 없어 한 판에 15초짜리 일회용 자원이다. 다음 슬라이스(레이저)가 물어야 할 "지름길을 뚫을까"는 **뚫을 대상이 있어야** 성립하므로, 그 대상을 이 슬라이스가 만든다.

## Global Constraints

- **결정론은 클·서가 같은 구체 클래스를 컴파일하는 데서 온다.** 시뮬 로직은 `LeagueOfPhysical-Shared`에 인터페이스 seam 없이 구체 타입으로 둔다. 인터페이스는 사이드가 달라야 하는 I/O 어댑터(`ICollisionQuery` 등)에만.
- **형제 게임과 짝을 맞춘다.** `FlappyWorld`가 이미 `ICollisionQuery` + `layerMask`를 들고 `KinematicMover.Move`를 부른다. `SkydiveWorld`도 **같은 모양**으로 만든다 — 새 패턴을 발명하지 않는다.
- **한국어 주석, 쉬운 말로.** 코드로 자명한 것은 적지 않고 **비자명한 의도(왜)**만 짧게. 전문용어를 설명 없이 던지지 않는다.
- **`GameFramework.World.Component`는 `UnityEngine.Component`와 이름이 겹친다.** 클라·서버 파일은 `using GameFramework.World;`를 **추가하지 않고** World 타입을 항상 풀 네임스페이스로 한정한다. Shared 패키지 내부 파일은 해당 없음.
- **다른 게임 모드를 건드리지 않는다.** `KinematicMover`·`ICollisionQuery`·`StaminaSystem`은 다른 모드도 쓴다 — **읽기만 하고 시그니처를 바꾸지 않는다.**
- **커밋은 바꾼 파일만 경로로 지정한다.** `git add -A` / `git commit -a` 금지 — 이 Unity 레포들엔 늘 의도적인 로컬 픽스처가 있고, 쓸어 담아 커밋해서 main을 깨뜨린 적이 있다. 커밋 전 `git status --short`로 스테이지된 것이 의도한 파일뿐인지 확인한다.
- **`run_tests` 전에 `recompile_status`의 `errors`가 빈 것을 반드시 확인한다.** 컴파일 에러 상태에서 테스트를 돌리면 에디터 메인 스레드가 물려 재시작 외엔 복구가 안 된다.
- **테스트 판정은 개수가 아니라 이름으로 한다.** 낡은 결과가 "전부 통과"로 보일 수 있다.
- **에디터를 건드리기 전에 `isPlaying`을 별도 호출로 확인한다.** 사용자가 플레이 중일 수 있다 — 확인과 실행을 한 덩어리로 묶지 않는다.
- 브랜치 `feature/skydive-course`. 머지·푸시·배포는 사용자 승인 후에만.

## 이 슬라이스가 의도적으로 남겨 두는 것

미룬 것은 **막으려던 위험과 그 위험이 실제가 되는 조건**을 함께 적는다. 조건 없이 문장만 남으면 나중에 왜 미뤘는지가 사라진다.

| 미루는 것 | 막으려던 위험 | 실제가 되는 조건 |
|---|---|---|
| **코스 밖 우회** — 선반은 바닥과 같은 200×200이라, 원점에서 100m 넘게 벗어나면 선반도 바닥도 없는 허공을 지나 그냥 내려간다 | "구멍을 안 뚫고도 1등을 한다" | **슬라이스 5(경계)가 닫는다.** 스펙 §3.5가 이 일을 맡고 있고, 여기서 벽을 세우면 스펙이 정한 "넓은 하늘"이 아니라 수직 갱도가 된다 |
| **파묻힘 밀어내기(`Depenetrate`)** — Flappy는 매 틱 부른다. Skydive는 안 부른다 | 몸이 선반 안에서 시작하면 sweep이 시작 겹침을 무시해 영영 못 빠져나온다 | Flappy에서 이게 실제로 발동한 경로는 ①스폰 겹침 ②레인 클램프 둘뿐인데 Skydive엔 **둘 다 없다**(허공에서 스폰, 클램프 없음). **플레이 중 몸이 선반에 박혀 멈추는 것이 관측되면** 그때 `KinematicMover`에 공유 헬퍼로 올리고 양쪽에서 부른다 |

---

## Task 1: 맵을 뚫지 않고 미끄러진다

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveMoveSystem.cs` (위치 쓰기·임시 바닥 클램프 제거 → 속도 전담)
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveWorld.cs` (생성자 + 맵 sweep 단계 추가)
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/SkydiveLifetimeScope.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Entity/SkydivePlayerCreator.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/SkydiveLifetimeScope.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Entity/SkydivePlayerCreator.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/SkydiveMoveSystemTests.cs` (수정)
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/SkydiveWorldTests.cs` (수정 + 신규 케이스)

**Interfaces:**
- Consumes: `LOP.KinematicMover.Move(in KinematicMoveInput, ICollisionQuery) → KinematicMoveResult` (기존, 변경 없음), `GameFramework.Physics.ICollisionQuery` (기존), `LOP.Tests.HalfSpaceQuery` (기존 테스트 더블, `AddGround(float y)`)
- Produces:
  - `SkydiveMoveSystem.Tick(Entity, float deltaTime, in SkydiveConfig)` — 시그니처 **그대로**, 이제 `Velocity`만 쓰고 `Transform`은 건드리지 않는다
  - `new SkydiveWorld(EntityRegistry, WorldEventBuffer, SkydiveMoveSystem, StaminaSystem, SkydiveConfig, ICollisionQuery collisionQuery, int layerMask)` — 인자 2개가 **끝에 추가**됨
  - 캐릭터 엔티티가 `GameFramework.World.GroundState`를 갖는다(양쪽 크리에이터가 붙임), 매 틱 `SkydiveWorld`가 갱신

### 왜 이 모양인가

`FlappyWorld`가 이미 이 구조다 — `FlappyMoveSystem`은 속도만 정하고, `FlappyWorld.MoveBlockedByMap`이 `KinematicMover.Move`를 부른다. Skydive만 `MoveSystem` 안에서 위치까지 쓰고 있어 형제 게임과 어긋나 있었다. 맞춰 두면 슬라이스 6(플레이어끼리 충돌)이 **"속도가 다 정해진 뒤 한 번"**이라는 스펙 §5의 페이즈 배리어를 넣을 자리가 그대로 생긴다.

- [ ] **Step 1: `SkydiveMoveSystem`에서 위치 쓰기를 걷어내는 실패 테스트를 쓴다**

`SkydiveMoveSystemTests.cs`의 `임시_바닥에_닿으면_멈춘다` 테스트를 **삭제**하고(그 책임이 `SkydiveWorld`로 옮겨 갔다 — 같은 것을 재는 테스트를 Step 6에서 World 쪽에 새로 만든다), 대신 아래를 추가한다:

```csharp
        [Test]
        public void 위치는_건드리지_않는다()
        {
            //  맵 충돌까지 봐야 최종 위치가 정해지므로, 위치는 SkydiveWorld가 정한다.
            //  MoveSystem이 여기서 위치를 미리 옮기면 그 값이 sweep의 출발점을 오염시킨다.
            var e = Diver(0f, false, new Vector3(0f, -25f, 0f), new Vector3(0f, 500f, 0f), h: 1f);

            new SkydiveMoveSystem().Tick(e, 0.02f, Config());

            Assert.AreEqual(500f, PositionOf(e).y, Tolerance, "MoveSystem은 위치를 쓰지 않는다");
            Assert.AreEqual(0f, PositionOf(e).x, Tolerance);
        }
```

- [ ] **Step 2: 실패를 확인한다**

Unity Test Runner(EditMode) 또는 `unity` CLI로 `SkydiveMoveSystemTests` 실행.
Expected: `위치는_건드리지_않는다` FAIL (현재는 위치를 쓰므로 y가 499.5).

- [ ] **Step 3: `SkydiveMoveSystem`을 속도 전담으로 바꾼다**

`SkydiveMoveSystem.cs` 전체를 아래로 교체한다:

```csharp
namespace LOP
{
    /// <summary>
    /// Skydive의 <b>속도</b>를 정한다. 자세가 목표 하강·수평 속도를 정하고, 실제 속도는 그 목표로
    /// 수렴한다 — 자세를 바꿔도 속도가 한 틱에 튀지 않아 남을 예측하는 쪽의 오차가 완만해진다.
    ///
    /// 위치는 여기서 정하지 않는다: 맵에 부딪히면 벽까지만 가야 하는데 그 판정은 충돌 쿼리가
    /// 필요하고, 그 쿼리를 든 쪽이 <see cref="SkydiveWorld"/>다(<see cref="FlappyMoveSystem"/>과 같은 짝).
    /// </summary>
    public class SkydiveMoveSystem
    {
        public void Tick(GameFramework.World.Entity entity, float deltaTime, in SkydiveConfig config)
        {
            var velocity = entity.Get<GameFramework.World.Velocity>();
            var posture = entity.Get<Posture>();
            if (velocity == null || posture == null)
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

            velocity.Linear = linear;
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

- [ ] **Step 4: 테스트가 통과하는지 확인한다**

Expected: `SkydiveMoveSystemTests` 전부 PASS (`위치는_건드리지_않는다` 포함).
`SkydiveWorldTests`는 아직 컴파일된다(생성자 미변경) — 다만 이제 아무도 위치를 안 써서 `HeightOf`가 1000에 머문다. 그건 Step 5가 고친다.

- [ ] **Step 5: `SkydiveWorld`가 맵 sweep으로 위치를 정하게 한다**

`SkydiveWorld.cs`를 다음과 같이 바꾼다.

(a) 파일 맨 위 `using`에 추가한다(`ToUnity`/`ToNumerics` 확장은 `GameFramework`에, `ICollisionQuery`는 `GameFramework.Physics`에, `Vector3`는 `UnityEngine`에 있다):

```csharp
using System.Collections.Generic;
using GameFramework;
using GameFramework.Physics;
```

`using UnityEngine;`은 **추가하지 않는다** — 아래 코드가 `Vector3` 같은 이름을 직접 쓰지 않아 필요가 없고, 이 레포는 World 타입과의 이름 충돌 때문에 `using`을 함부로 늘리지 않는다.

(b) 클래스 상단 필드에 추가:

```csharp
        private readonly ICollisionQuery _collisionQuery;
        private readonly int _layerMask;
```

(c) 생성자 — 인자 2개를 **끝에** 붙인다:

```csharp
        public SkydiveWorld(
            GameFramework.World.EntityRegistry entityRegistry,
            GameFramework.World.WorldEventBuffer eventBuffer,
            SkydiveMoveSystem moveSystem,
            StaminaSystem staminaSystem,
            SkydiveConfig config,
            ICollisionQuery collisionQuery,
            int layerMask)
            : base(entityRegistry, eventBuffer)
        {
            _moveSystem = moveSystem;
            _staminaSystem = staminaSystem;
            _config = config;
            _collisionQuery = collisionQuery;
            _layerMask = layerMask;
        }
```

(d) `Mutation`의 마지막 루프(`_moveSystem.Tick(...)`) 뒤에 이동 루프를 **추가**한다. 루프를 나누는 이유는 스펙 §5가 정한 배리어다 — 전원의 속도가 먼저 다 정해져야 슬라이스 6의 몸싸움이 순서와 무관해진다.

```csharp
            for (int i = 0; i < _divers.Count; i++)
            {
                _moveSystem.Tick(_divers[i], deltaTime, _config);
            }

            // 속도가 전원 다 정해진 뒤에 옮긴다 — 슬라이스 6의 몸싸움이 이 사이에 들어온다(스펙 §5).
            for (int i = 0; i < _divers.Count; i++)
            {
                MoveBlockedByMap(_divers[i], deltaTime);
            }
```

(e) 클래스에 메서드를 추가한다:

```csharp
        // 맵은 막는다 — KinematicMover가 벽까지만 옮기고 미끄러뜨린다(collide-and-slide).
        // 캡슐 규격은 CapsuleShape가 아니라 config에서 읽는다: 이 게임은 몸 크기도 튜닝값이라
        // 진실원본이 마스터데이터 한 곳이고, 크리에이터가 붙이는 CapsuleShape도 같은 값의 사본이다.
        private void MoveBlockedByMap(GameFramework.World.Entity entity, float deltaTime)
        {
            var transform = entity.Get<GameFramework.World.Transform>();
            var velocity = entity.Get<GameFramework.World.Velocity>();
            if (transform == null || velocity == null)
            {
                return;
            }

            //  떨어지는 몸은 턱을 오를 일이 없다. 0을 주면 막혔을 때의 추가 sweep 3발도 안 쏜다.
            var result = KinematicMover.Move(new KinematicMoveInput(
                transform.Position.ToUnity(), velocity.Linear.ToUnity(),
                _config.BodyRadius, _config.BodyHeight, deltaTime,
                _layerMask, stepOffset: 0f), _collisionQuery);

            transform.Position = result.position.ToNumerics();
            // 막힌 축의 속도도 같이 지운다 — 안 지우면 다음 틱 수렴이 "막힌 적 없다는 듯"
            // 옛 속도 위에 계속 쌓인다(KinematicMoveSystem과 같은 관례).
            velocity.Linear = result.velocity.ToNumerics();

            var groundState = entity.Get<GameFramework.World.GroundState>();
            if (groundState != null)
            {
                groundState.IsGrounded = result.grounded;
            }
        }
```

- [ ] **Step 6: `SkydiveWorldTests`를 새 생성자에 맞추고 착지 케이스를 추가한다**

`World(...)` 헬퍼를 충돌 쿼리를 받게 바꾼다. 기본값은 **아무것도 없는 하늘**(`HalfSpaceQuery`에 면을 안 넣으면 항상 `CollisionHit.None`)이라, 기존 테스트들은 그대로 통과한다.

```csharp
        static SkydiveWorld World(EntityRegistry registry, GameFramework.Physics.ICollisionQuery query = null)
            => new SkydiveWorld(registry, new WorldEventBuffer(),
                                new SkydiveMoveSystem(), new StaminaSystem(), Config(),
                                query ?? new HalfSpaceQuery(), layerMask: ~0);
```

`Diver(...)` 헬퍼에 접지 상태를 붙인다(붙이지 않으면 접지가 어디에도 기록되지 않는다):

```csharp
            e.Add(new GroundState());
```

그리고 케이스 두 개를 추가한다:

```csharp
        [Test]
        public void 바닥에_닿으면_멈추고_접지로_기록된다()
        {
            var registry = new EntityRegistry();
            var diver = Diver("a");
            diver.Get<GameFramework.World.Transform>().Position = new Vector3(0f, 0.3f, 0f).ToNumerics();
            registry.Add(diver);

            var map = new HalfSpaceQuery();
            map.AddGround(0f);
            var world = World(registry, map);
            world.GameplayStartTick = 0;

            for (int t = 0; t < 20; t++) { world.Tick(t, 0.02f); }

            Assert.GreaterOrEqual(HeightOf(registry, "a"), -0.01f, "바닥을 뚫고 내려가면 안 된다");
            Assert.IsTrue(diver.Get<GroundState>().IsGrounded, "바닥에 서 있으면 접지여야 한다");
        }

        [Test]
        public void 허공에서는_접지가_아니다()
        {
            var registry = new EntityRegistry();
            var diver = Diver("a");
            registry.Add(diver);

            var map = new HalfSpaceQuery();
            map.AddGround(0f);   // 1000m 아래 — 이번 틱엔 닿지 않는다
            var world = World(registry, map);
            world.GameplayStartTick = 0;

            world.Tick(0, 0.02f);

            Assert.IsFalse(diver.Get<GroundState>().IsGrounded);
            Assert.Less(HeightOf(registry, "a"), 1000f, "허공에서는 내려가야 한다");
        }
```

- [ ] **Step 7: Shared EditMode 테스트를 돌린다**

Expected: `SkydiveWorldTests` / `SkydiveMoveSystemTests` / `StaminaSystemTests` / `KinematicMover*Tests` / `FlappyWorld*Tests` 전부 PASS.
**개수가 아니라 이름으로 확인한다.**

- [ ] **Step 8: 클라 배선 — 스코프와 크리에이터**

`LeagueOfPhysical-Client/Assets/Scripts/Game/SkydiveLifetimeScope.cs`의 `SkydiveWorld` 등록을 바꾼다:

```csharp
            builder.Register<SkydiveWorld>(c => new SkydiveWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<SkydiveMoveSystem>(),
                c.Resolve<StaminaSystem>(),
                c.Resolve<SkydiveConfig>(),
                c.Resolve<GameFramework.Physics.ICollisionQuery>(),
                // sweep이 볼 것은 맵 지오메트리뿐이다. 몸의 물리 콜라이더는 Character 레이어에
                // 있으므로(PhysicsBodyFactory), 이 마스크에 Character가 없는 한 사람끼리는 안 걸린다.
                // 사람끼리 부딪히는 것은 별도 단계로 들어온다(슬라이스 6, 스펙 §4.1).
                LayerMask.GetMask("Default")), Lifetime.Singleton)
                .As<GameFramework.World.IWorld>().AsSelf();
```

`LeagueOfPhysical-Client/Assets/Scripts/Entity/SkydivePlayerCreator.cs`의 `Create` 안, `CapsuleShape` 추가 줄 바로 뒤에 넣는다:

```csharp
            // 발 딛고 있는지는 이동 커널이 매 틱 다시 계산해 여기 적는다 — 스태미나 회복이 이 값을 읽는다.
            worldEntity.Add(new GameFramework.World.GroundState());
```

- [ ] **Step 9: 서버 배선 — 스코프와 크리에이터**

`LeagueOfPhysical-Server/Assets/Scripts/Game/SkydiveLifetimeScope.cs`:

```csharp
            builder.Register<GameFramework.World.IWorld>(c => new SkydiveWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<SkydiveMoveSystem>(),
                c.Resolve<StaminaSystem>(),
                c.Resolve<SkydiveConfig>(),
                c.Resolve<GameFramework.Physics.ICollisionQuery>(),
                // 클라와 같은 마스크여야 예측이 권위와 갈리지 않는다.
                UnityEngine.LayerMask.GetMask("Default")), Lifetime.Singleton);
```

이 파일에는 `using UnityEngine;`이 없으므로 위처럼 **풀 한정**으로 쓴다(`using`을 새로 추가하지 않는다 — 이 레포 규약이 World 타입 충돌을 피하려고 클·서 파일에서 `using`을 늘리지 않는 쪽이다).

`LeagueOfPhysical-Server/Assets/Scripts/Entity/SkydivePlayerCreator.cs`의 `CapsuleShape` 줄 뒤에 클라와 **같은 줄**을 넣는다:

```csharp
            worldEntity.Add(new GameFramework.World.GroundState());
```

- [ ] **Step 10: 클·서 컴파일을 확인한다**

`unity` CLI로 클라·서버 각각 `recompile` → `recompile_status`의 `errors`가 비었는지 확인한다.
`status`가 `up_to_date`면 **재컴파일을 안 한 것**이므로 통과로 읽지 않는다 — 콘솔 CS 에러를 시각과 대조한다.

- [ ] **Step 11: 커밋**

세 레포 각각, 바꾼 파일만 경로로 지정한다.

```bash
# LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/SkydiveMoveSystem.cs Runtime/Scripts/Game/SkydiveWorld.cs \
        Tests/EditMode/SkydiveMoveSystemTests.cs Tests/EditMode/SkydiveWorldTests.cs
git status --short
git commit -m "feat(skydive): 맵에 부딪히면 벽까지만 가고 미끄러진다"

# LeagueOfPhysical-Client
git add Assets/Scripts/Game/SkydiveLifetimeScope.cs Assets/Scripts/Entity/SkydivePlayerCreator.cs
git status --short
git commit -m "feat(skydive): 클라 월드에 맵 충돌 쿼리를 물린다"

# LeagueOfPhysical-Server
git add Assets/Scripts/Game/SkydiveLifetimeScope.cs Assets/Scripts/Entity/SkydivePlayerCreator.cs
git status --short
git commit -m "feat(skydive): 서버 월드에 맵 충돌 쿼리를 물린다"
```

---

## Task 2: 발판 위에 서면 스태미나가 찬다

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveWorld.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/SkydiveWorldTests.cs`

**Interfaces:**
- Consumes: Task 1이 만든 `GroundState.IsGrounded`(매 틱 `MoveBlockedByMap`이 갱신), `StaminaSystem.Tick(Entity, float, in SkydiveConfig, bool grounded)` (기존, **변경 없음**)
- Produces: 없음(월드 내부 순서 변경)

### 왜 순서를 바꾸나

지금 `Mutation`은 ①자세 → ②스태미나 → ③이동 순인데, "발 딛고 있나"는 ③이 계산해 낸다. ②에서 읽으면 **한 틱 전 값**을 쓰게 된다. 스태미나를 이동 뒤로 옮기면 "움직인 결과 발을 디뎠다 → 그래서 찬다"가 되어 시점이 어긋나지 않는다.

한 틱 늦어도 20ms라 눈에 안 보이지만, 순서를 바꿔 두면 **"어느 틱의 접지인가"를 나중에 아무도 다시 묻지 않아도 된다.**

- [ ] **Step 1: 실패 테스트를 쓴다**

`SkydiveWorldTests.cs`에 추가:

```csharp
        [Test]
        public void 발판_위에_서면_스태미나가_찬다()
        {
            var registry = new EntityRegistry();
            var diver = Diver("a");
            diver.Get<GameFramework.World.Transform>().Position = new Vector3(0f, 0.3f, 0f).ToNumerics();
            diver.Get<Stamina>().Current = 0f;
            registry.Add(diver);

            var map = new HalfSpaceQuery();
            map.AddGround(0f);
            var world = World(registry, map);
            world.GameplayStartTick = 0;

            // y=0.3에서 떨어져 바닥에 앉기까지 일곱 틱쯤 걸린다(수렴 가속이라 처음엔 느리다).
            // 1초를 굴리면 그중 40틱 이상이 접지이고, 회복 40/s이므로 30 넘게 차 있어야 한다.
            for (int t = 0; t < 50; t++) { world.Tick(t, 0.02f); }

            Assert.Greater(diver.Get<Stamina>().Current, 20f, "발판 위에서는 스태미나가 차야 한다");
        }

        [Test]
        public void 허공에서는_스태미나가_차지_않는다()
        {
            var registry = new EntityRegistry();
            var diver = Diver("a");
            diver.Get<Stamina>().Current = 0f;
            registry.Add(diver);

            var world = World(registry);   // 면이 없는 하늘
            world.GameplayStartTick = 0;

            for (int t = 0; t < 50; t++) { world.Tick(t, 0.02f); }

            Assert.AreEqual(0f, diver.Get<Stamina>().Current, Tolerance, "공중에서는 안 찬다(젤다 규칙)");
        }
```

- [ ] **Step 2: 실패를 확인한다**

Expected: `발판_위에_서면_스태미나가_찬다` FAIL — 현재는 `Position.Y <= GroundY + 0.01` 임시 판정이라 y=0.3에서는 접지로 안 본다.

- [ ] **Step 3: `Mutation`의 스태미나 단계를 이동 뒤로 옮기고 접지 원천을 바꾼다**

`SkydiveWorld.Mutation`에서 기존 스태미나 루프를 **삭제**한다:

```csharp
            for (int i = 0; i < _divers.Count; i++)
            {
                // 임시 바닥에 닿아 있으면 "발 딛고 있다"로 본다 — 슬라이스 3이 이 판정을
                // 진짜 지면 접촉으로 바꾼다.
                var transform = _divers[i].Get<GameFramework.World.Transform>();
                bool grounded = transform != null && transform.Position.Y <= _config.GroundY + 0.01f;
                _staminaSystem.Tick(_divers[i], deltaTime, _config, grounded);
            }
```

그리고 `MoveBlockedByMap` 루프 **뒤에** 새 루프를 놓는다:

```csharp
            // 이동 뒤에 온다 — "발 딛고 있나"를 이동 커널이 방금 계산했기 때문이다.
            // 앞에 두면 한 틱 전 접지로 회복 여부를 정하게 된다.
            for (int i = 0; i < _divers.Count; i++)
            {
                bool grounded = _divers[i].Get<GameFramework.World.GroundState>()?.IsGrounded ?? false;
                _staminaSystem.Tick(_divers[i], deltaTime, _config, grounded);
            }
```

`Mutation`의 최종 순서: 자세 → 속도 → 이동 → 스태미나.

- [ ] **Step 4: 클래스 주석을 실제 순서에 맞춘다**

`SkydiveWorld` 클래스의 `<summary>`에서 "한 틱: ① … → ② 스태미나 소모·회복 → ③ 자세가 정한 목표 속도로 이동." 부분을 바꾼다:

```csharp
    /// 한 틱: ① 입력을 자세로 반영(축은 정해진 속도로만 움직인다) → ② 자세가 목표 속도를 정한다
    /// → ③ 맵에 막히면 벽까지만 옮긴다(미끄러짐·접지 판정) → ④ 방금 나온 접지로 스태미나 소모·회복.
    /// 레이저 판정은 Detection에 들어오지만(슬라이스 4) 지금은 비어 있다.
```

- [ ] **Step 5: 테스트를 돌린다**

Expected: 새 케이스 2개 PASS, `SkydiveWorldTests`·`StaminaSystemTests` 기존 케이스 전부 PASS.

- [ ] **Step 6: 커밋**

```bash
# LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/SkydiveWorld.cs Tests/EditMode/SkydiveWorldTests.cs
git status --short
git commit -m "feat(skydive): 발판 위에 내려서면 스태미나가 다시 찬다"
```

---

## Task 3: 코스가 실제로 통과 가능한지 재는 계산기

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveReach.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/SkydiveReachTests.cs`

**Interfaces:**
- Consumes: 없음(순수 산수)
- Produces: `static float SkydiveReach.MaxHorizontal(float fallDistance, float fallSpeed, float moveSpeed, float turnAccel)`

### 무엇을 재는가

두 선반 사이를 떨어지는 동안 **옆으로 얼마나 갈 수 있나**를 낸다. 이 값보다 구멍 사이 거리가 멀면 그 코스는 **통과 불가능**이다. 스펙 §7.4가 요구하는 코스 검사가 이것이고, Task 4의 에디터 도구가 이 함수로 자기가 만든 코스를 검사한다.

모델은 보수적으로 잡는다 — **수평 속도 0에서 출발**해 `turnAccel`로 가속하다 `moveSpeed`에서 멈춘다. 실제로는 이전 구간의 속도를 물고 오므로 진짜 도달 거리는 이보다 크다. 즉 이 함수가 "간다"고 하면 진짜로 간다.

- [ ] **Step 1: 실패 테스트를 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/SkydiveReachTests.cs`:

```csharp
using NUnit.Framework;

namespace LOP.Tests
{
    public class SkydiveReachTests
    {
        const float Tolerance = 0.01f;

        [Test]
        public void 최고속에_닿기_전이면_가속_구간만_적분한다()
        {
            // 낙하 1m를 1m/s로 = 1초. 가속 6이면 최고속 18에 3초 걸리므로 아직 가속 중.
            // 거리 = ½·6·1² = 3
            Assert.AreEqual(3f, SkydiveReach.MaxHorizontal(1f, 1f, moveSpeed: 18f, turnAccel: 6f), Tolerance);
        }

        [Test]
        public void 최고속에_닿은_뒤는_등속으로_이어진다()
        {
            // 낙하 5m를 1m/s로 = 5초. 최고속 18까지 3초(거리 27), 남은 2초는 등속 36.
            Assert.AreEqual(63f, SkydiveReach.MaxHorizontal(5f, 1f, moveSpeed: 18f, turnAccel: 6f), Tolerance);
        }

        [Test]
        public void 대자가_다이브보다_멀리_간다()
        {
            // 선반 간격 150m. 실제 튜닝값으로 — 대자(25 하강/12 최고속/22 가속) vs 다이브(45/18/6).
            float spread = SkydiveReach.MaxHorizontal(150f, 25f, 12f, 22f);
            float dive = SkydiveReach.MaxHorizontal(150f, 45f, 18f, 6f);

            Assert.Greater(spread, dive,
                "천천히 내려가면 옆으로 더 갈 시간이 있다 — 이 관계가 자세 선택의 이유다");
        }

        [Test]
        public void 하강_속도가_0이면_0을_돌려준다()
        {
            // 0으로 나누지 않는다. 호출자가 잘못 넣어도 코스 검사가 죽으면 안 된다.
            Assert.AreEqual(0f, SkydiveReach.MaxHorizontal(150f, 0f, 12f, 22f), Tolerance);
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

Expected: 컴파일 실패 — `SkydiveReach`가 없다.

- [ ] **Step 3: 계산기를 만든다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveReach.cs`:

```csharp
namespace LOP
{
    /// <summary>
    /// 한 자세로 주어진 높이만큼 떨어지는 동안 <b>옆으로 갈 수 있는 최대 거리</b>를 낸다.
    /// 코스의 구멍과 구멍 사이가 이 값보다 멀면 그 코스는 통과할 수 없다(스펙 §7.4 코스 검사).
    ///
    /// 수평 속도 0에서 출발한다고 보므로 실제보다 짧게 나온다 — 앞 구간의 속도를 물고 오기 때문이다.
    /// 일부러 그렇게 뒀다: 이 함수가 "간다"고 하면 진짜로 간다.
    /// </summary>
    public static class SkydiveReach
    {
        public static float MaxHorizontal(float fallDistance, float fallSpeed,
                                          float moveSpeed, float turnAccel)
        {
            if (fallSpeed <= 0f || turnAccel <= 0f || moveSpeed <= 0f || fallDistance <= 0f)
            {
                return 0f;
            }

            float fallTime = fallDistance / fallSpeed;
            float timeToTopSpeed = moveSpeed / turnAccel;

            if (fallTime <= timeToTopSpeed)
            {
                return 0.5f * turnAccel * fallTime * fallTime;   // 아직 가속 중
            }

            float accelDistance = 0.5f * moveSpeed * timeToTopSpeed;
            return accelDistance + moveSpeed * (fallTime - timeToTopSpeed);
        }
    }
}
```

- [ ] **Step 4: 테스트가 통과하는지 확인한다**

Expected: `SkydiveReachTests` 4개 전부 PASS.

- [ ] **Step 5: 커밋**

```bash
# LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/SkydiveReach.cs Runtime/Scripts/Game/SkydiveReach.cs.meta \
        Tests/EditMode/SkydiveReachTests.cs Tests/EditMode/SkydiveReachTests.cs.meta
git status --short
git commit -m "feat(skydive): 구멍 사이를 실제로 갈 수 있는지 재는 계산기"
```

> `.meta`는 Unity가 만든 것만 커밋한다. 에디터가 아직 스캔하지 않았으면 스캔을 기다렸다가 추가한다 — 직접 쓰지 않는다.

---

## Task 4: 코스를 굽는 에디터 도구

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Editor/SkydiveCourseBuilder.cs`

**Interfaces:**
- Consumes: `SkydiveReach.MaxHorizontal` (Task 3)
- Produces: 메뉴 `LOP/Skydive/코스 굽기` — 열려 있는 씬에 `Course` 루트를 만들고 선반·기둥을 채운다. 메뉴 `LOP/Skydive/코스 검사` — 굽지 않고 표만 검사한다

### 왜 도구인가

1000m 코스를 `.unity` YAML로 손으로 쓰면 오브젝트 하나에 다섯 블록씩 붙어 40개면 200블록이다. 읽을 수도, 고칠 수도, 리뷰할 수도 없다. **표 하나가 곧 코스 설계**가 되게 하고 도구가 그 표를 씬으로 굽는다 — 숫자를 바꿔 다시 구우면 되고, 리뷰어는 표만 보면 된다.

튜닝값을 마스터데이터에서 읽지 않고 상수로 두는 이유: 이 도구는 **코스가 그 튜닝값에서 통과 가능한가**를 검사하는데, 검사 기준이 데이터를 따라 조용히 움직이면 검사가 아니다. 값이 바뀌면 여기도 같이 고치도록 일부러 박아 둔다.

### 코스 표

| 선반 | 고도 y | 구멍 중심 (x, z) | 구멍 한 변 | 앞 구멍에서 수평거리 |
|---|---|---|---|---|
| 1 | 850 | (0, 0) | 30 | 0 — 스폰 바로 아래(가르치는 구간) |
| 2 | 700 | (25, 0) | 24 | 25 |
| 3 | 550 | (25, 25) | 20 | 25 |
| 4 | 400 | (−20, 25) | 20 | 45.0 |
| 5 | 250 | (−20, −25) | 16 | 50.0 |
| 6 | 100 | (25, −20) | 16 | 45.3 |

- 선반은 **바닥과 같은 200×200**(x, z ∈ [−100, 100]), 두께 3m, 중심이 표의 y.
- 간격 150m에서 대자로 갈 수 있는 거리는 약 **68m**, 다이브는 약 **33m**다(구멍 반쪽만큼은 덤 —
  중심까지 안 가도 가장자리로 들어가면 통과다). 표의 거리는 전부 대자 사거리 안이지만, **4·5·6번은
  다이브 사거리 밖**이다 — 여섯 구간 중 셋에서 자세를 고르게 만드는 것이 이 표의 목적이다.
  (처음 잡았던 표는 그 구간이 하나뿐이라 검산에서 걸러 다시 잡았다.)
- 기둥은 선반 사이마다 네 모서리(x, z = ±60)에 4×4 굵기로 세운다. 구멍 가장자리의 최대 |좌표|가 37이라 길을 막지 않는다. **속도감을 눈에 보이게 하는 것**이 유일한 목적이다.

- [ ] **Step 1: 도구를 만든다**

`LeagueOfPhysical-Client/Assets/Scripts/Editor/SkydiveCourseBuilder.cs`:

```csharp
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// 열려 있는 <c>SkydiveMap</c> 씬에 낙하 코스를 굽는다 — 구멍 하나씩 뚫린 선반과, 속도감을
    /// 보여 주는 모서리 기둥.
    ///
    /// 표(<c>Shelves</c>)가 곧 코스 설계다. 숫자를 고치고 다시 구우면 되고, 리뷰어는 표만
    /// 보면 된다. 굽기 전에 <see cref="SkydiveReach"/>로 구멍 사이가 실제로 닿는 거리인지 검사한다.
    /// </summary>
    public static class SkydiveCourseBuilder
    {
        // 굽는 결과를 전부 담는 루트. 다시 구우면 통째로 지우고 새로 만든다.
        private const string CourseRootName = "Course";

        // 선반은 바닥과 같은 넓이다(x, z ∈ [-100, 100]). 이보다 크게 만들면 바닥 밖 허공에
        // 선반만 떠 있게 된다. 옆으로 크게 벗어나면 코스를 우회할 수 있는데, 그것을 막는 것은
        // 슬라이스 5(경계)의 일이다.
        private const float SlabHalf = 100f;
        private const float SlabThickness = 3f;

        private const float PillarSide = 4f;
        private const float PillarOffset = 60f;   // 구멍 가장자리 최대 37보다 멀어 길을 막지 않는다

        // 검사 기준값. 마스터데이터에서 읽지 않는다 — 기준이 데이터를 따라 조용히 움직이면
        // 검사가 아니게 된다. TbSkydiveConfig를 바꿨다면 여기도 같이 고친다.
        private const float SpreadFallSpeed = 25f;
        private const float SpreadMoveSpeed = 12f;
        private const float SpreadTurnAccel = 22f;
        private const float DiveFallSpeed = 45f;
        private const float DiveMoveSpeed = 18f;
        private const float DiveTurnAccel = 6f;

        // 스폰 고도. 첫 선반까지의 거리를 검사할 때 쓴다.
        private const float SpawnY = 1000f;

        private readonly struct Shelf
        {
            public readonly float Y;
            public readonly float HoleX;
            public readonly float HoleZ;
            public readonly float HoleHalf;

            public Shelf(float y, float holeX, float holeZ, float holeSide)
            {
                Y = y;
                HoleX = holeX;
                HoleZ = holeZ;
                HoleHalf = holeSide * 0.5f;
            }
        }

        // 코스 설계 그 자체. 위에서 아래 순서로 적는다.
        private static readonly Shelf[] Shelves =
        {
            new Shelf(850f, 0f, 0f, 30f),      // 스폰 바로 아래 — 아무것도 안 해도 지나간다
            new Shelf(700f, 25f, 0f, 24f),     // 옆으로 가는 걸 가르치는 구간(25m, 다이브로도 닿는다)
            new Shelf(550f, 25f, 25f, 20f),
            new Shelf(400f, -20f, 25f, 20f),   // 여기부터 셋은 다이브로 곧장 가면 못 닿는다
            new Shelf(250f, -20f, -25f, 16f),
            new Shelf(100f, 25f, -20f, 16f),
        };

        [MenuItem("LOP/Skydive/코스 굽기")]
        public static void Build()
        {
            if (Verify(out string report) == false)
            {
                EditorUtility.DisplayDialog("코스 굽기", report, "취소");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Scenes/floor.mat");

            GameObject root = GameObject.Find(CourseRootName);
            if (root != null)
            {
                Object.DestroyImmediate(root);   // 다시 구울 때 옛 코스가 겹쳐 남지 않게
            }
            root = new GameObject(CourseRootName);

            for (int i = 0; i < Shelves.Length; i++)
            {
                BuildShelf(root.transform, Shelves[i], material);
                float upperY = i == 0 ? SpawnY : Shelves[i - 1].Y;
                BuildPillars(root.transform, Shelves[i].Y, upperY, material);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Skydive] 코스를 구웠다 — 선반 {Shelves.Length}개\n{report}");
            EditorUtility.DisplayDialog("코스 굽기",
                $"선반 {Shelves.Length}개를 만들었다. 씬을 저장해라.\n\n{report}", "확인");
        }

        // 선반 = 구멍을 둘러싼 판 네 장. 하나의 큰 판에 구멍을 뚫을 수는 없어서(상자 콜라이더는
        // 볼록한 덩어리뿐) 테두리로 만든다.
        private static void BuildShelf(Transform parent, in Shelf shelf, Material material)
        {
            float northStart = shelf.HoleZ + shelf.HoleHalf;
            float southEnd = shelf.HoleZ - shelf.HoleHalf;
            float eastStart = shelf.HoleX + shelf.HoleHalf;
            float westEnd = shelf.HoleX - shelf.HoleHalf;

            string prefix = $"Shelf_{shelf.Y:0}";
            AddBox(parent, $"{prefix}_N", material,
                new Vector3(0f, shelf.Y, (northStart + SlabHalf) * 0.5f),
                new Vector3(SlabHalf * 2f, SlabThickness, SlabHalf - northStart));
            AddBox(parent, $"{prefix}_S", material,
                new Vector3(0f, shelf.Y, (southEnd - SlabHalf) * 0.5f),
                new Vector3(SlabHalf * 2f, SlabThickness, southEnd + SlabHalf));
            AddBox(parent, $"{prefix}_E", material,
                new Vector3((eastStart + SlabHalf) * 0.5f, shelf.Y, shelf.HoleZ),
                new Vector3(SlabHalf - eastStart, SlabThickness, shelf.HoleHalf * 2f));
            AddBox(parent, $"{prefix}_W", material,
                new Vector3((westEnd - SlabHalf) * 0.5f, shelf.Y, shelf.HoleZ),
                new Vector3(westEnd + SlabHalf, SlabThickness, shelf.HoleHalf * 2f));
        }

        // 떨어지는 동안 옆에 뭔가 지나가야 속도가 보인다. 길에서 멀리 떨어뜨려 놓는다.
        private static void BuildPillars(Transform parent, float lowerY, float upperY, Material material)
        {
            float height = upperY - lowerY;
            if (height <= 0f)
            {
                return;
            }
            float centerY = lowerY + height * 0.5f;

            foreach (float sx in new[] { -PillarOffset, PillarOffset })
            {
                foreach (float sz in new[] { -PillarOffset, PillarOffset })
                {
                    AddBox(parent, $"Pillar_{lowerY:0}_{sx:0}_{sz:0}", material,
                        new Vector3(sx, centerY, sz),
                        new Vector3(PillarSide, height, PillarSide));
                }
            }
        }

        private static void AddBox(Transform parent, string name, Material material,
                                   Vector3 center, Vector3 size)
        {
            //  두께가 0 이하인 판은 만들지 않는다 — 구멍이 판 끝에 붙으면 생길 수 있다.
            if (size.x <= 0.01f || size.y <= 0.01f || size.z <= 0.01f)
            {
                return;
            }

            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, worldPositionStays: false);
            box.transform.localPosition = center;
            box.transform.localScale = size;
            box.layer = LayerMask.NameToLayer("Default");   // sweep 마스크가 보는 레이어
            if (material != null)
            {
                box.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
        }

        /// <summary>구멍과 구멍 사이가 실제로 닿는 거리인지 검사한다(스펙 §7.4).</summary>
        [MenuItem("LOP/Skydive/코스 검사")]
        public static void VerifyMenu()
        {
            Verify(out string report);
            EditorUtility.DisplayDialog("코스 검사", report, "확인");
            Debug.Log($"[Skydive] 코스 검사\n{report}");
        }

        private static bool Verify(out string report)
        {
            var lines = new List<string>();
            bool ok = true;

            float previousX = 0f;
            float previousZ = 0f;
            float previousY = SpawnY;

            foreach (var shelf in Shelves)
            {
                float fall = previousY - shelf.Y;
                float gap = new Vector2(shelf.HoleX - previousX, shelf.HoleZ - previousZ).magnitude;
                float spread = SkydiveReach.MaxHorizontal(fall, SpreadFallSpeed, SpreadMoveSpeed, SpreadTurnAccel);
                float dive = SkydiveReach.MaxHorizontal(fall, DiveFallSpeed, DiveMoveSpeed, DiveTurnAccel);

                //  구멍 반쪽만큼은 덤이다 — 중심까지 안 가도 가장자리로 들어가면 통과다.
                bool reachable = gap <= spread + shelf.HoleHalf;
                ok &= reachable;

                string note = reachable
                    ? (gap > dive + shelf.HoleHalf ? "  (다이브로는 못 닿음 — 의도)" : "")
                    : "  [X] 대자로도 못 닿는다";
                lines.Add($"y={shelf.Y:0}: 이동 {gap:0.0}m / 대자 {spread:0.0}m / 다이브 {dive:0.0}m{note}");

                previousX = shelf.HoleX;
                previousZ = shelf.HoleZ;
                previousY = shelf.Y;
            }

            var sb = new StringBuilder();
            sb.AppendLine(ok ? "통과 가능한 코스다." : "통과 불가능한 구간이 있다.");
            foreach (string line in lines)
            {
                sb.AppendLine(line);
            }
            report = sb.ToString();
            return ok;
        }
    }
}
```

- [ ] **Step 2: 컴파일을 확인한다**

`unity` CLI로 클라 `recompile` → `recompile_status`의 `errors`가 비었는지 확인.
`SkydiveReach`는 LOP-Shared 런타임이라 Editor 어셈블리에서 보인다(`FlappyMapTrapScanner`가 이미 `GameFramework.Physics`와 `FlappyRace`를 참조하는 것과 같다). 안 보이면 `Assets/Scripts/Editor/`의 asmdef 유무를 먼저 확인한다.

- [ ] **Step 3: 코스 검사만 먼저 돌려 표가 맞는지 본다**

에디터 메뉴 `LOP/Skydive/코스 검사`.
Expected: "통과 가능한 코스다." + 여섯 줄. **4·5·6번 선반 줄에** `(다이브로는 못 닿음 — 의도)`가 붙어 있어야 한다 — 그게 이 표의 존재 이유다.

- [ ] **Step 4: 커밋(도구만)**

씬은 아직 건드리지 않는다 — 굽기는 컨트롤러가 에디터에서 실행한다.

```bash
# LeagueOfPhysical-Client
git add Assets/Scripts/Editor/SkydiveCourseBuilder.cs Assets/Scripts/Editor/SkydiveCourseBuilder.cs.meta
git status --short
git commit -m "feat(skydive): 낙하 코스를 표에서 굽는 에디터 도구"
```

---

## 코드 뒤 — 컨트롤러가 손으로 하는 절차

에디터를 띄우고 씬을 저장하는 일, 서브모듈 포인터를 옮기는 일, 배포는 자동화하지 않는다. 순서를 틀리면 **에러 없이 기능만 죽는다.**

- [ ] **A. 사용자가 플레이 중이 아닌지 별도 호출로 확인한다.**
- [ ] **B. 클라 에디터에서 `Assets/Art/Scenes/SkydiveMap.unity`를 열고 `LOP/Skydive/코스 굽기` 실행 → 씬 저장.**
- [ ] **C. Art 레포에서 씬만 커밋·푸시한다.** `LeagueOfPhysical-Art`는 클라·서버 양쪽에 서브모듈로 붙어 있다.
- [ ] **D. 클라·서버 양쪽의 서브모듈 포인터를 새 Art 커밋으로 옮겨 커밋한다.** 한쪽만 옮기면 서버가 옛 맵을 본다 — 클라에는 선반이 보이는데 서버는 통과시킨다.
- [ ] **E. 클라 레포에서 `content-deploy`를 돌린다.** 맵은 어드레서블로 배달되므로 S3에 올라가야 서버가 새 씬을 받는다.
- [ ] **F. 게임서버 이미지를 빌드·배포한다.** 서버 코드(`SkydiveLifetimeScope`, `SkydivePlayerCreator`)가 바뀌었으므로 이미지 태그가 움직여야 롤아웃이 난다.
- [ ] **G. 배포된 파드에서 실제 태그를 확인한다.** `kubectl exec <파드이름> -- printenv GAME_SERVER_IMAGE`. `--field-selector`는 종료 중인 옛 파드를 잡으므로 **이름으로 지정한다.**

머지·푸시·배포는 **사용자 승인 후에만** 한다.

## 플레이로 확인할 것

1. 첫 선반(y=850)은 아무것도 안 해도 지나간다 — 구멍이 스폰 바로 아래 30m다.
2. 4·5·6번 선반은 **다이브로 곧장 가면 못 닿는다.** 대자로 펴서 옆으로 가야 한다. 이게 안 느껴지면 표의 거리를 늘린다.
3. 선반 위에 내려서면 **스태미나 막대가 다시 찬다**(초당 40, 0에서 만땅까지 7.5초).
4. 선반에 몸이 박혀 멈추는 일이 있나 — 있으면 위 "의도적으로 남겨 두는 것" 표의 `Depenetrate`를 되살릴 때다.
5. 원점에서 100m 넘게 벗어나면 선반을 다 우회한다 — **알고 있는 구멍이고 슬라이스 5가 닫는다.**
