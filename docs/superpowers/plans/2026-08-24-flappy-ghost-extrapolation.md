# Flappy 유령정지 + 원격 외삽 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 실플레이에서 나온 세 증상(원격 순간이동 / 끼이면 카메라 진동 / 스폰 갇힘)을 없앤다.

**Architecture:** 맵 충돌을 "막기"에서 "통과 + 유령정지 페널티"로 바꾸고(끼임 소멸), 원격 새를 시뮬에서 빼 마지막 스냅에서 외삽으로 그린다(순간이동 소멸). 몸싸움은 내 새만 원격의 외삽 위치에 부딪히는 한쪽 로컬 판정으로 남긴다.

**Tech Stack:** Unity 6.3, C#, VContainer, MessagePipe, Mirror, Luban(마스터데이터), Protobuf(wire), NUnit EditMode

**Spec:** `docs/superpowers/specs/2026-08-24-flappy-ghost-and-remote-extrapolation-design.md`

## Global Constraints

- **시뮬 코드는 구체 클래스를 공유한다.** 클·서가 LOP-Shared의 *같은 구체 코드*를 돌린다. 시뮬에 인터페이스 seam을 만들지 않는다. 인터페이스는 사이드가 달라야 하는 I/O 어댑터에만.
- **World 타입은 풀 네임스페이스로 한정한다** — LOP 측 파일에서 `using GameFramework.World;`를 추가하지 않는다 (`UnityEngine.Component`와 충돌). 예: `GameFramework.World.Entity`.
- **Anemic**: 컴포넌트는 데이터만. 상태 변경 로직은 System에.
- **맵 콜라이더는 솔리드 그대로 둔다.** `ICollisionQuery.CapsuleCast`가 트리거를 걸러내므로 트리거로 되돌리면 감지가 안 된다.
- **캡슐 규약**: `Transform.Position`은 **발밑 기준**. 캡슐 끝점은 `pos + up*radius` / `pos + up*(height - radius)` (`KinematicMover.Cast`와 동일).
- **유령 튜닝값 초기치**: `GhostTime = 0.8`, `InvulnTime = 0.6` (프로토타입 `FlappyAutoPilot`과 동일).
- **외삽 상한**: `0.25`초.
- **FlapWang을 건드리지 않는다.** 정책은 `OwnerPredictedSyncPolicy` 그대로, 유령정지는 `FlappyWorld` 안에서만 산다.
- **`.meta` 파일을 함께 커밋한다.** 새 스크립트/폴더는 Unity가 만든 `.meta`를 반드시 포함.
- **테스트 실행**: `unity cmd recompile` → `recompile_status` 폴링 → `unity cmd run_tests --mode EditMode`. `refresh_unity`라는 명령은 없다. 배치모드 `unity test`는 그 프로젝트 에디터가 떠 있으면 잠금 때문에 못 쓴다.

---

## 파일 구조

| 저장소 | 파일 | 책임 |
|---|---|---|
| infrastructure | `table/Datas/#FlappyConfig.xlsx` | `ghost_time`/`invuln_time` 추가 |
| LOP-Shared | `Runtime/Scripts/Game/FlappyGhost.cs` (신규) | 유령 타이머 데이터 |
| LOP-Shared | `Runtime/Scripts/Game/FlappyGhostSystem.cs` (신규) | 진입·감소·조회 로직 |
| LOP-Shared | `Runtime/Scripts/Game/FlappyConfig.cs` | 필드 2개 추가 |
| LOP-Shared | `Runtime/Scripts/Game/FlappyWorld.cs` | 충돌 규칙 교체 + 저장/복원 |
| LOP-Shared | `Runtime/Scripts/Game/FlappySavedState.cs` (신규) | 롤백용 유령 타이머 스냅 |
| LOP-Shared | `Protos/EntitySnap.proto` | `ghost` 필드 |
| GameFramework | `Runtime/Scripts/Netcode/SnapshotExtrapolation.cs` (신규) | 외삽 순수 커널 |
| Client | `Assets/Scripts/EntitySync/EntitySyncMode.cs` | `Extrapolated` 추가 |
| Client | `Assets/Scripts/EntitySync/OwnerPredictedRemotesExtrapolatedSyncPolicy.cs` (신규) | Flappy 정책 |
| Client | `Assets/Scripts/Netcode/ExtrapolatedEntityInterpolator.cs` (신규) | 외삽 뷰 |
| Client | `Assets/Scripts/Entity/EntityBinder.cs` | 3-모드 분기 + 유령 렌더 부착 |
| Client | `Assets/Scripts/Entity/GhostAppearance.cs` (신규) | 유령 반투명 표시 |
| Server | `Assets/Scripts/Game/TickSystems/EntitySnapshotBroadcastSystem.cs` | 스냅에 유령 싣기 |
| 양쪽 | `Assets/Scripts/Game/FlappyConfigProvider.cs` | 새 두 값 전달 |

---

## Task 1: 유령 튜닝값을 마스터데이터에 넣는다

**Files:**
- Modify: `infrastructure/table/Datas/#FlappyConfig.xlsx`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyConfig.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/FlappyConfigProvider.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/FlappyConfigProvider.cs`

**Interfaces:**
- Produces: `FlappyConfig.GhostTime`(float), `FlappyConfig.InvulnTime`(float) — Task 2·3이 읽는다.

- [ ] **Step 1: 마스터데이터 시트에 컬럼 두 개 추가**

`infrastructure/table/Datas/`에서 FlappyConfig 시트를 열어 컬럼을 추가한다. 기존 컬럼(`forward_speed` 등)과 같은 형식으로:

| 컬럼 | 타입 | 값 | 설명 |
|---|---|---|---|
| `ghost_time` | float | `0.8` | 맵에 부딪혔을 때 멈춰 있는 시간(초) |
| `invuln_time` | float | `0.6` | 유령이 풀린 뒤 다시 안 걸리는 시간(초) |

- [ ] **Step 2: Luban 재생성**

```bash
cd infrastructure/table && ./gen.sh
```
`LeagueOfPhysical-MasterData-Client` / `-Server` 두 패키지에 생성물이 갱신되는지 확인한다.

- [ ] **Step 3: `FlappyConfig`에 필드 추가**

`FlappyConfig.cs`의 필드·생성자에 두 값을 더한다(기존 필드 뒤에 붙인다):

```csharp
        /// <summary>맵에 부딪혔을 때 그 자리에 멈춰 있는 시간(초). 이 시간 손실이 페널티다.</summary>
        public readonly float GhostTime;

        /// <summary>유령이 풀린 뒤 다시 걸리지 않는 시간(초). 같은 벽에 연달아 걸리는 것을 막는다.</summary>
        public readonly float InvulnTime;

        public FlappyConfig(float forwardSpeed, float flapImpulse, float gravity, float maxFallSpeed,
                            float bodyRadius, float bodyHeight, float restitution,
                            float ghostTime, float invulnTime)
        {
            ForwardSpeed = forwardSpeed;
            FlapImpulse = flapImpulse;
            Gravity = gravity;
            MaxFallSpeed = maxFallSpeed;
            BodyRadius = bodyRadius;
            BodyHeight = bodyHeight;
            Restitution = restitution;
            GhostTime = ghostTime;
            InvulnTime = invulnTime;
        }
```

- [ ] **Step 4: 양쪽 provider가 새 값을 넘기게 한다**

클·서 `FlappyConfigProvider.cs`에서 `new FlappyConfig(...)` 호출에 `row.GhostTime, row.InvulnTime`(Luban 생성 프로퍼티명에 맞춘다)을 더한다.

- [ ] **Step 5: 컴파일 확인**

```bash
export PATH="$HOME/.unity/bin:$PATH"
unity cmd recompile
# recompile_status가 completed/failed:false 가 될 때까지 폴링
```
Expected: 에러 0

- [ ] **Step 6: 커밋 (레포별로)**

```bash
git add table/Datas && git commit -m "feat(masterdata): Flappy 유령정지 시간 두 값을 추가한다"
```
Shared·클·서도 각각 바뀐 파일만 지정해 커밋한다. `git add -A` 금지.

---

## Task 2: 유령 상태 컴포넌트와 시스템

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyGhost.cs`
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyGhostSystem.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/FlappyGhostSystemTests.cs`

**Interfaces:**
- Consumes: `FlappyConfig.GhostTime`/`InvulnTime` (Task 1)
- Produces: `FlappyGhostSystem.Enter(entity)` / `Tick(entity, dt)` / `bool IsStopped(entity)` — Task 3이 부른다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

```csharp
using GameFramework.World;
using LOP;
using NUnit.Framework;

public class FlappyGhostSystemTests
{
    private static FlappyConfig Config()
        => new FlappyConfig(5f, 6f, 20f, 30f, 0.35f, 1.5f, 0.5f, ghostTime: 0.8f, invulnTime: 0.6f);

    private static Entity Bird()
    {
        var e = new Entity("bird");
        e.Add(new FlappyGhost());
        return e;
    }

    [Test]
    public void 부딪히면_정지_시간이_찬다()
    {
        var system = new FlappyGhostSystem(Config());
        var bird = Bird();

        system.Enter(bird);

        Assert.That(bird.Get<FlappyGhost>().Remaining, Is.EqualTo(0.8f).Within(0.0001f));
        Assert.That(system.IsStopped(bird), Is.True);
    }

    [Test]
    public void 정지가_끝나면_무적으로_넘어간다()
    {
        var system = new FlappyGhostSystem(Config());
        var bird = Bird();
        system.Enter(bird);

        for (int i = 0; i < 40; i++) system.Tick(bird, 0.02f);   // 0.8초

        var ghost = bird.Get<FlappyGhost>();
        Assert.That(ghost.Remaining, Is.EqualTo(0f));
        Assert.That(ghost.InvulnRemaining, Is.EqualTo(0.6f).Within(0.0001f));
        Assert.That(system.IsStopped(bird), Is.False);
    }

    [Test]
    public void 무적_중에는_다시_걸리지_않는다()
    {
        var system = new FlappyGhostSystem(Config());
        var bird = Bird();
        system.Enter(bird);
        for (int i = 0; i < 40; i++) system.Tick(bird, 0.02f);

        system.Enter(bird);   // 무적 중 재충돌

        Assert.That(system.IsStopped(bird), Is.False);
    }

    [Test]
    public void 무적이_끝나면_다시_걸린다()
    {
        var system = new FlappyGhostSystem(Config());
        var bird = Bird();
        system.Enter(bird);
        for (int i = 0; i < 70; i++) system.Tick(bird, 0.02f);   // 0.8 + 0.6 초과

        system.Enter(bird);

        Assert.That(system.IsStopped(bird), Is.True);
    }

    [Test]
    public void 정지_중_재충돌은_시간을_늘리지_않는다()
    {
        var system = new FlappyGhostSystem(Config());
        var bird = Bird();
        system.Enter(bird);
        system.Tick(bird, 0.4f);

        system.Enter(bird);

        Assert.That(bird.Get<FlappyGhost>().Remaining, Is.EqualTo(0.4f).Within(0.0001f));
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

```bash
unity cmd recompile   # 컴파일 에러(타입 없음)로 실패해야 정상
```
Expected: `FlappyGhost` / `FlappyGhostSystem` 타입 없음 에러

- [ ] **Step 3: 컴포넌트를 만든다**

```csharp
namespace LOP
{
    /// <summary>
    /// 맵에 부딪힌 새가 잠깐 멈춰 있는 상태. 멈춤이 끝나면 잠깐 무적이 되어 같은 벽에 연달아 걸리지 않는다.
    /// 데이터만 갖는다 — 진입·감소는 <see cref="FlappyGhostSystem"/>이 한다.
    /// </summary>
    public class FlappyGhost : GameFramework.World.Component
    {
        /// <summary>멈춤이 끝나기까지 남은 시간(초). 0이면 정상 상태다.</summary>
        public float Remaining;

        /// <summary>다시 걸리지 않는 시간이 끝나기까지 남은 시간(초).</summary>
        public float InvulnRemaining;
    }
}
```

- [ ] **Step 4: 시스템을 만든다**

```csharp
namespace LOP
{
    /// <summary>
    /// 유령 상태의 진입과 시간 감소. 클·서가 같은 구체 클래스를 돌려 결과가 갈리지 않는다.
    /// </summary>
    public class FlappyGhostSystem
    {
        private readonly FlappyConfig config;

        public FlappyGhostSystem(FlappyConfig config)
        {
            this.config = config;
        }

        /// <summary>지금 멈춰 있는가. 멈춰 있으면 이번 틱에 속도를 주지 않는다.</summary>
        public bool IsStopped(GameFramework.World.Entity entity)
        {
            var ghost = entity.Get<FlappyGhost>();
            return ghost != null && ghost.Remaining > 0f;
        }

        /// <summary>맵에 닿았을 때. 이미 멈춰 있거나 무적이면 아무 일도 없다.</summary>
        public void Enter(GameFramework.World.Entity entity)
        {
            var ghost = entity.Get<FlappyGhost>();
            if (ghost == null || ghost.Remaining > 0f || ghost.InvulnRemaining > 0f)
            {
                return;
            }
            ghost.Remaining = config.GhostTime;
        }

        public void Tick(GameFramework.World.Entity entity, float deltaTime)
        {
            var ghost = entity.Get<FlappyGhost>();
            if (ghost == null)
            {
                return;
            }

            if (ghost.Remaining > 0f)
            {
                ghost.Remaining -= deltaTime;
                if (ghost.Remaining <= Epsilon)   // ← 0f가 아니다. 아래 주석 참고
                {
                    // 멈춤이 끝나는 그 틱에 무적을 채운다 — 빠져나오는 동안 다시 걸리지 않게.
                    ghost.Remaining = 0f;
                    ghost.InvulnRemaining = config.InvulnTime;
                }
                return;
            }

            if (ghost.InvulnRemaining > 0f)
            {
                ghost.InvulnRemaining -= deltaTime;
                if (ghost.InvulnRemaining <= Epsilon)
                {
                    ghost.InvulnRemaining = 0f;
                }
            }
        }
    }
}
```

> **⚠️ 정정 (2026-08-24, Task 2 구현 중 발견).** 위 코드의 교차 판정은 `<= 0f`가 아니라
> `<= Epsilon`(`private const float Epsilon = 1e-5f;`)이어야 한다. float32는 40×0.02f로 0.8f를
> 정확히 빼지 못해 `2e-7` 정도가 남고, 그러면 전환이 한 틱 늦는다. 클·서가 같은 잔여를 갖게 되므로
> 결정론은 깨지지 않는다.

- [ ] **Step 5: 새를 만들 때 컴포넌트를 붙인다**

클·서 `FlappyBirdCreator.cs`에서 `CapsuleShape`를 붙이는 곳 옆에 `entity.Add(new FlappyGhost());`를 더한다. **양쪽 다** 고친다.

- [ ] **Step 6: 테스트 통과 확인**

```bash
unity cmd recompile   # 폴링 후
unity cmd run_tests --mode EditMode --filter FlappyGhostSystemTests
```
Expected: 5개 통과

- [ ] **Step 7: 커밋**

```bash
git add Runtime/Scripts/Game/FlappyGhost.cs Runtime/Scripts/Game/FlappyGhost.cs.meta \
        Runtime/Scripts/Game/FlappyGhostSystem.cs Runtime/Scripts/Game/FlappyGhostSystem.cs.meta \
        Tests/EditMode/FlappyGhostSystemTests.cs Tests/EditMode/FlappyGhostSystemTests.cs.meta
git commit -m "feat(flappy): 유령 상태 컴포넌트와 시스템을 만든다"
```

---

## Task 3: 맵 충돌을 막기에서 유령정지로 바꾼다

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyWorld.cs`
- Modify: 클·서 `FlappyRaceLifetimeScope.cs` (`FlappyGhostSystem` 등록 + `FlappyWorld` 생성자 인자)
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/FlappyWorldGhostTests.cs`

**Interfaces:**
- Consumes: `FlappyGhostSystem` (Task 2)
- Produces: 유령 중 속도 0 / 맵에 닿으면 `Enter` — Task 4가 이 상태를 저장한다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`ICollisionQuery` 가짜 구현으로 "항상 맞는다"를 만들어 확인한다:

```csharp
using GameFramework.Physics;
using NUnit.Framework;
using UnityEngine;

public class FlappyWorldGhostTests
{
    private class AlwaysHit : ICollisionQuery
    {
        public CollisionHit CapsuleCast(Vector3 p1, Vector3 p2, float radius,
            Vector3 direction, float distance, int layerMask)
            => new CollisionHit(true, 0f, Vector3.up, p1);
    }

    private class NeverHit : ICollisionQuery
    {
        public CollisionHit CapsuleCast(Vector3 p1, Vector3 p2, float radius,
            Vector3 direction, float distance, int layerMask)
            => CollisionHit.None;
    }

    [Test]
    public void 맵에_닿으면_멈춘다_그러나_막히지는_않는다()
    {
        var world = FlappyWorldFixture.Create(new AlwaysHit(), out var bird);
        Vector3 before = bird.Get<GameFramework.World.Transform>().Position.ToUnity();

        world.Tick(1, 0.02f);

        // 통과한다 — 위치는 그대로가 아니라 앞으로 나가 있다.
        Vector3 after = bird.Get<GameFramework.World.Transform>().Position.ToUnity();
        Assert.That(after.x, Is.GreaterThan(before.x));
        // 그리고 유령에 걸렸다.
        Assert.That(bird.Get<LOP.FlappyGhost>().Remaining, Is.GreaterThan(0f));
    }

    [Test]
    public void 유령_중에는_속도가_0이다()
    {
        var world = FlappyWorldFixture.Create(new AlwaysHit(), out var bird);
        world.Tick(1, 0.02f);   // 유령 진입

        world.Tick(2, 0.02f);

        Vector3 velocity = bird.Get<GameFramework.World.Velocity>().Linear.ToUnity();
        Assert.That(velocity, Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void 맵에_안_닿으면_평소대로_전진한다()
    {
        var world = FlappyWorldFixture.Create(new NeverHit(), out var bird);

        world.Tick(1, 0.02f);

        Assert.That(bird.Get<LOP.FlappyGhost>().Remaining, Is.EqualTo(0f));
        Assert.That(bird.Get<GameFramework.World.Velocity>().Linear.X, Is.GreaterThan(0f));
    }
}
```

> `FlappyWorldFixture`는 이 테스트 파일 안에 private static 헬퍼로 둔다 — `EntityRegistry` + `WorldEventBuffer` + 새 한 마리(`Transform`/`Velocity`/`CapsuleShape`/`FlappyGhost`/`Simulated`/`InputBuffer`) + 가짜 `IMotionBridge`(빈 구현)를 조립해 `FlappyWorld`를 만든다.

- [ ] **Step 2: 실패를 확인한다**

```bash
unity cmd recompile && unity cmd run_tests --mode EditMode --filter FlappyWorldGhostTests
```
Expected: 실패 (아직 막는 구현)

- [ ] **Step 3: `Mutation`을 바꾼다**

```csharp
        protected override void Mutation(long tick, float deltaTime)
        {
            CollectBirds();

            // 시간 감소가 먼저다. 이번 틱에 풀릴 새는 이번 틱부터 움직인다.
            for (int i = 0; i < _birds.Count; i++)
            {
                _ghostSystem.Tick(_birds[i], deltaTime);
            }

            for (int i = 0; i < _birds.Count; i++)
            {
                if (_ghostSystem.IsStopped(_birds[i]))
                {
                    // 멈춰 있는 새는 전진도 하지 않는다 — 시간 손실이 이 게임의 페널티다.
                    _birds[i].Get<GameFramework.World.Velocity>().Linear = System.Numerics.Vector3.Zero;
                    continue;
                }
                _moveSystem.Tick(_birds[i], deltaTime);
            }

            _bodyCollisionSystem.Resolve(_birds);

            _motionBridge.SyncTransforms();
            for (int i = 0; i < _birds.Count; i++)
            {
                MoveThroughMap(_birds[i], deltaTime);
            }
        }
```

- [ ] **Step 4: `MoveThroughMap`을 감지형으로 바꾼다**

```csharp
        // 맵은 더는 막지 않는다. 부딪혔는지만 보고 유령으로 넘긴다 —
        // 전진 속도가 고정이라 "막기"로는 벽에 박힌 새가 수평으로 영영 빠져나오지 못한다.
        private void MoveThroughMap(GameFramework.World.Entity entity, float deltaTime)
        {
            var transform = entity.Get<GameFramework.World.Transform>();
            var velocity = entity.Get<GameFramework.World.Velocity>();
            var body = entity.Get<GameFramework.World.CapsuleShape>();
            if (transform == null || velocity == null || body == null)
            {
                return;
            }

            Vector3 start = transform.Position.ToUnity();
            Vector3 delta = velocity.Linear.ToUnity() * deltaTime;

            if (delta.sqrMagnitude > 0f)
            {
                // 캡슐 끝점 규약은 KinematicMover.Cast와 같다 — position은 발밑 기준.
                Vector3 p1 = start + Vector3.up * body.Radius;
                Vector3 p2 = start + Vector3.up * (body.Height - body.Radius);
                var hit = _collisionQuery.CapsuleCast(
                    p1, p2, body.Radius, delta.normalized, delta.magnitude, _layerMask);
                if (hit.HasHit)
                {
                    _ghostSystem.Enter(entity);
                }
            }

            transform.Position = (start + delta).ToNumerics();
            _motionBridge.PushMotion(entity);
        }
```

**지운다**: `_motionBridge.Depenetrate(entity)` 호출과 `KinematicMover.Move(...)` 호출. `KinematicMover` using이 남으면 제거.

- [ ] **Step 5: 생성자에 `FlappyGhostSystem`을 받는다**

`FlappyWorld` 필드에 `private readonly FlappyGhostSystem _ghostSystem;`를 더하고 생성자 인자·대입을 추가한다. 클·서 `FlappyRaceLifetimeScope.cs` 두 곳에서 `builder.Register<FlappyGhostSystem>(Lifetime.Singleton);`를 등록하고 `FlappyWorld` 생성 시 넘긴다.

- [ ] **Step 6: 테스트 통과 확인**

```bash
unity cmd recompile && unity cmd run_tests --mode EditMode --filter FlappyWorldGhostTests
```
Expected: 3개 통과. 기존 Flappy 테스트도 전부 통과해야 한다 — `--filter` 없이 한 번 더 돌린다.

- [ ] **Step 7: 커밋**

```bash
git commit -m "feat(flappy): 맵 충돌을 막기에서 유령정지로 바꾼다"
```

---

## Task 4: 유령 타이머를 롤백에 태운다

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappySavedState.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyWorld.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/FlappyWorldSaveStateTests.cs`

**Interfaces:**
- Consumes: `WorldBase.SaveGameState(long)` / `LoadGameState(long)` 훅 (`protected virtual`)
- Produces: 없음 (내부 완결)

> **왜 필요한가**: 클라 되감기는 `IWorld.LoadState(tick)`으로 그 틱의 상태로 돌아간 뒤 다시 굴린다. `WorldBase`는 위치·속도만 되돌리므로, 유령 타이머가 안 되돌아가면 재생 때 "이미 풀린 줄 알고" 움직여 예측이 어긋난다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

```csharp
[Test]
public void 되감으면_유령_타이머도_그_틱으로_돌아간다()
{
    var world = FlappyWorldFixture.Create(new AlwaysHit(), out var bird);

    world.Tick(1, 0.02f);        // 유령 진입
    world.SaveState(1);
    float atSave = bird.Get<LOP.FlappyGhost>().Remaining;

    for (long t = 2; t <= 10; t++) { world.Tick(t, 0.02f); world.SaveState(t); }
    Assert.That(bird.Get<LOP.FlappyGhost>().Remaining, Is.LessThan(atSave));   // 줄었다

    Assert.That(world.LoadState(1), Is.True);

    Assert.That(bird.Get<LOP.FlappyGhost>().Remaining, Is.EqualTo(atSave).Within(0.0001f));
}
```

- [ ] **Step 2: 실패 확인**

```bash
unity cmd run_tests --mode EditMode --filter FlappyWorldSaveStateTests
```
Expected: 실패 (타이머가 안 돌아옴)

- [ ] **Step 3: 저장 값 타입을 만든다**

```csharp
namespace LOP
{
    /// <summary>
    /// 되감기용 Flappy 고유 상태. 위치·속도는 <see cref="GameFramework.World.WorldBase"/>가 이미
    /// 다루므로 여기엔 그 밖의 것만 담는다.
    /// </summary>
    public readonly struct FlappySavedState
    {
        public readonly float GhostRemaining;
        public readonly float InvulnRemaining;

        private FlappySavedState(float ghostRemaining, float invulnRemaining)
        {
            GhostRemaining = ghostRemaining;
            InvulnRemaining = invulnRemaining;
        }

        public static FlappySavedState Capture(GameFramework.World.Entity entity)
        {
            var ghost = entity.Get<FlappyGhost>();
            return ghost == null
                ? new FlappySavedState(0f, 0f)
                : new FlappySavedState(ghost.Remaining, ghost.InvulnRemaining);
        }

        public void RestoreTo(GameFramework.World.Entity entity)
        {
            var ghost = entity.Get<FlappyGhost>();
            if (ghost == null)
            {
                return;
            }
            ghost.Remaining = GhostRemaining;
            ghost.InvulnRemaining = InvulnRemaining;
        }
    }
}
```

- [ ] **Step 4: `FlappyWorld`에 훅을 구현한다**

`LOPWorld`와 같은 모양으로. 필드에 프레임 버퍼를 둔다:

```csharp
        private readonly GameFramework.Netcode.SequenceBuffer<
            System.Collections.Generic.Dictionary<string, FlappySavedState>> _gameFrames
            = new GameFramework.Netcode.SequenceBuffer<
                System.Collections.Generic.Dictionary<string, FlappySavedState>>(128);

        protected override void SaveGameState(long tick)
        {
            var frame = new System.Collections.Generic.Dictionary<string, FlappySavedState>();
            foreach (var entity in EntityRegistry.All)
            {
                if (entity.Has<GameFramework.World.Simulated>())
                {
                    frame[entity.Id] = FlappySavedState.Capture(entity);
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
```

- [ ] **Step 5: 테스트 통과 확인 + 커밋**

```bash
unity cmd recompile && unity cmd run_tests --mode EditMode
git commit -m "feat(flappy): 유령 타이머를 되감기에 태운다"
```

> **여기까지가 G1이다.** 다음으로 넘어가기 전에 런타임 확인을 한 번 한다 — §검증 G1 참고.

---

## Task 5: 유령 상태를 스냅에 실어 보낸다

**Files:**
- Modify: `LeagueOfPhysical-Shared/Protos/EntitySnap.proto`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/EntitySnapshotBroadcastSystem.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Netcode/EntitySnap.cs`

**Interfaces:**
- Produces: 클라 `LOP.EntitySnap.ghost`(bool) — Task 6·9가 읽는다.

> **⚠️ 스냅 타입이 둘이다.** 와이어의 proto `EntitySnap`(대문자 프로퍼티)과, 클라가 쓰는 별개
> 클래스 `LOP.EntitySnap`(`Assets/Scripts/Netcode/EntitySnap.cs`, 소문자 프로퍼티 + `timestamp`)이
> 있고 AutoMapper가 이름으로 잇는다. **양쪽 다 고쳐야** 클라까지 값이 온다.

- [ ] **Step 1: proto에 필드를 더한다**

```proto
	repeated ProtoActiveEffect status_effects = 11;
	bool ghost = 12;                // Flappy: 맵에 부딪혀 멈춰 있는 중
```

- [ ] **Step 2: proto 재생성**

LOP-Shared의 proto 생성 절차를 따른다(`Tools/Protobuf`). 생성물 `Runtime.Generated/Scripts/Protobuf/EntitySnap*.cs`가 갱신되는지 확인.

- [ ] **Step 3: 서버가 값을 채운다**

`EntitySnapshotBroadcastSystem.cs`의 `new EntitySnap { ... }`에 한 줄 더한다:

```csharp
                    Ghost = entity.Get<FlappyGhost>()?.Remaining > 0f,
```

> FlapWang 엔티티엔 `FlappyGhost`가 없어 `null` → `false`가 된다. 사이드 분기가 필요 없다.

- [ ] **Step 4: 클라 스냅 클래스에도 필드를 더한다**

`Assets/Scripts/Netcode/EntitySnap.cs`:

```csharp
        public bool grounded { get; set; }
        public bool ghost { get; set; }          // Flappy: 맵에 부딪혀 멈춰 있는 중
```

AutoMapper가 proto `Ghost` → 클라 `ghost`를 이름으로 잇는다(`grounded`와 같은 방식). 별도 매핑
설정이 필요 없는지 `MapperConfig`에서 확인하고, 수동 매핑이 필요하면 `grounded` 옆에 한 줄 더한다.

- [ ] **Step 5: 컴파일 확인 + 커밋**

```bash
unity cmd recompile   # 서버 프로젝트에서
git commit -m "feat(flappy): 유령 상태를 엔티티 스냅에 싣는다"
```

---

## Task 6: 유령을 반투명으로 그린다

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Entity/GhostAppearance.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Entity/EntityBinder.cs`

**Interfaces:**
- Consumes: `EntitySnap.Ghost` (Task 5)

- [ ] **Step 1: 표시 컴포넌트를 만든다**

```csharp
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 유령(맵에 부딪혀 멈춘) 상태를 반투명으로 보여 준다. 남이 이유 없이 멈춘 것처럼 보이지 않게 하는
    /// 최소한의 연출이라, 상태 판단은 하지 않고 받은 값을 그대로 그린다.
    /// </summary>
    public class GhostAppearance : MonoBehaviour, ICleanup
    {
        private static readonly Color GhostColor = new Color(0.6f, 0.6f, 0.7f, 0.7f);

        private Renderer[] renderers;
        private Color[] originalColors;
        private bool applied;

        public void SetEntity(LOPActor actor)
        {
            renderers = actor.GetComponentsInChildren<Renderer>(true);
            originalColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                originalColors[i] = renderers[i].material.color;
            }
        }

        public void SetGhost(bool ghost)
        {
            if (renderers == null || ghost == applied)
            {
                return;
            }
            applied = ghost;
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].material.color = ghost ? GhostColor : originalColors[i];
            }
        }

        public void Cleanup()
        {
            SetGhost(false);
        }
    }
}
```

- [ ] **Step 2: 스냅을 받는 쪽에서 호출한다**

`SnapshotEntityInterpolator`/`ExtrapolatedEntityInterpolator`가 스냅을 받을 때(`AddServerEntitySnap`) `GhostAppearance.SetGhost(snap.ghost)`를 부른다. 내 새(Predicted)는 시뮬의 `FlappyGhost`를 직접 읽어 매 프레임 반영한다.

- [ ] **Step 3: `EntityBinder`에서 캐릭터에 부착**

캐릭터 분기(`kind.Kind == EntityType.Character`)의 장식 뷰 옆에:

```csharp
                GhostAppearance ghostAppearance = root.AddComponent<GhostAppearance>();
                objectResolver.Inject(ghostAppearance);
                ghostAppearance.SetEntity(actor);
```

- [ ] **Step 4: 컴파일 + 커밋**

```bash
unity cmd recompile
git commit -m "feat(flappy): 유령 상태를 반투명으로 보여 준다"
```

---

## Task 7: 외삽 커널 (GameFramework)

**Files:**
- Create: `GameFramework/Runtime/Scripts/Netcode/SnapshotExtrapolation.cs`
- Test: `GameFramework/Tests/Runtime/Netcode/SnapshotExtrapolationTests.cs`

**Interfaces:**
- Produces: `SnapshotExtrapolation.Position(Vector3 position, Vector3 velocity, Vector3 acceleration, float elapsed, float maxElapsed)` → `Vector3`. Task 9가 부른다.

> 기존 짝: 같은 폴더의 `SnapshotInterpolation.Solve`(브래킷 탐색), `Hermite.Position/Velocity`. 외삽도 게임 무관 산수이므로 여기 둔다. 중력은 인자로 받아 커널이 도메인을 모르게 한다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

```csharp
using GameFramework.Netcode;
using NUnit.Framework;
using UnityEngine;

namespace GameFramework.Tests.Netcode
{
    public class SnapshotExtrapolationTests
    {
        [Test]
        public void 경과가_0이면_그_자리다()
        {
            Vector3 result = SnapshotExtrapolation.Position(
                new Vector3(1f, 2f, 3f), Vector3.one, Vector3.down * 10f, 0f, 0.25f);

            Assert.That(result, Is.EqualTo(new Vector3(1f, 2f, 3f)));
        }

        [Test]
        public void 가속도가_없으면_등속으로_나간다()
        {
            Vector3 result = SnapshotExtrapolation.Position(
                Vector3.zero, new Vector3(2f, 0f, 0f), Vector3.zero, 0.1f, 0.25f);

            Assert.That(result.x, Is.EqualTo(0.2f).Within(0.0001f));
        }

        [Test]
        public void 중력을_포함해_포물선을_그린다()
        {
            // y = v*t + 0.5*a*t^2 = 5*0.1 + 0.5*(-20)*0.01 = 0.5 - 0.1 = 0.4
            Vector3 result = SnapshotExtrapolation.Position(
                Vector3.zero, new Vector3(0f, 5f, 0f), new Vector3(0f, -20f, 0f), 0.1f, 0.25f);

            Assert.That(result.y, Is.EqualTo(0.4f).Within(0.0001f));
        }

        [Test]
        public void 상한을_넘으면_상한에서_멈춘다()
        {
            Vector3 atCap = SnapshotExtrapolation.Position(
                Vector3.zero, new Vector3(2f, 0f, 0f), Vector3.zero, 0.25f, 0.25f);
            Vector3 beyond = SnapshotExtrapolation.Position(
                Vector3.zero, new Vector3(2f, 0f, 0f), Vector3.zero, 5f, 0.25f);

            Assert.That(beyond, Is.EqualTo(atCap));
        }

        [Test]
        public void 경과가_음수면_그_자리다()
        {
            Vector3 result = SnapshotExtrapolation.Position(
                new Vector3(1f, 0f, 0f), Vector3.one, Vector3.zero, -1f, 0.25f);

            Assert.That(result, Is.EqualTo(new Vector3(1f, 0f, 0f)));
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

```bash
unity cmd recompile && unity cmd run_tests --mode EditMode --filter SnapshotExtrapolationTests
```
Expected: 타입 없음

- [ ] **Step 3: 커널을 만든다**

```csharp
using UnityEngine;

namespace GameFramework.Netcode
{
    /// <summary>
    /// 마지막으로 받은 상태에서 앞을 내다보는 순수 산수(dead reckoning 2차). 보간
    /// (<see cref="SnapshotInterpolation"/>)이 "받은 두 점 사이"를 그리는 것과 달리, 이쪽은
    /// "받은 마지막 점 너머"를 그린다 — 원격을 내 시각에 맞춰 놓을 때 쓴다.
    /// <para>가속도는 인자로 받는다. 중력 같은 값은 게임이 알고 커널은 모른다.</para>
    /// </summary>
    public static class SnapshotExtrapolation
    {
        /// <summary>
        /// <paramref name="elapsed"/>초 뒤 위치. <paramref name="maxElapsed"/>를 넘는 경과는
        /// 상한에서 잘린다 — 오래 못 받을수록 틀리므로, 계속 내달리는 대신 그 자리에 세운다.
        /// </summary>
        public static Vector3 Position(Vector3 position, Vector3 velocity, Vector3 acceleration,
            float elapsed, float maxElapsed)
        {
            if (elapsed <= 0f)
            {
                return position;
            }
            float t = elapsed > maxElapsed ? maxElapsed : elapsed;
            return position + velocity * t + 0.5f * acceleration * (t * t);
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 + 커밋**

```bash
unity cmd run_tests --mode EditMode --filter SnapshotExtrapolationTests
git commit -m "feat(netcode): 스냅샷 외삽 커널을 더한다"
```

---

## Task 8: `Extrapolated` 모드와 정책

**Files:**
- Modify: `Client/Assets/Scripts/EntitySync/EntitySyncMode.cs`
- Create: `Client/Assets/Scripts/EntitySync/OwnerPredictedRemotesExtrapolatedSyncPolicy.cs`
- Delete: `Client/Assets/Scripts/EntitySync/CharactersPredictedSyncPolicy.cs` (+ `.meta`)
- Modify: `Client/Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`
- Test: `Client/Assets/Tests/EditMode/EntitySync/EntitySyncPolicyTests.cs`

**Interfaces:**
- Produces: `EntitySyncMode.Extrapolated` — Task 9의 `EntityBinder` 분기가 쓴다.

- [ ] **Step 1: 모드를 더한다**

```csharp
        /// <summary>내 시간선에서 같이 굴린다. 스냅이 오면 그 틱으로 맞추고 지금까지 다시 굴린다.</summary>
        Predicted,

        /// <summary>
        /// 굴리지는 않고, 마지막 스냅에서 내 시각까지 이어 그린다. 남의 입력을 모르는 채로 굴리면
        /// 틀린 궤적을 확신 있게 그려 보정이 크게 튀는데, 외삽은 틀려도 방향이 대체로 맞아 오차가 작다.
        /// </summary>
        Extrapolated,
```

- [ ] **Step 2: 정책을 갈아끼운다**

```csharp
namespace LOP
{
    /// <summary>
    /// 내 새는 예측하고 남은 외삽한다(Flappy Race). 남의 플랩 입력이 클라로 오지 않으므로 남을
    /// 시뮬로 굴리면 "계속 추락"이라는 틀린 궤적이 나온다 — 굴리는 대신 마지막 속도로 이어 그린다.
    /// </summary>
    public class OwnerPredictedRemotesExtrapolatedSyncPolicy : IEntitySyncPolicy
    {
        private readonly System.Func<string> localEntityId;

        public OwnerPredictedRemotesExtrapolatedSyncPolicy(System.Func<string> localEntityId)
        {
            this.localEntityId = localEntityId;
        }

        public EntitySyncMode For(GameFramework.World.Entity entity)
        {
            if (entity.Get<EntityKind>()?.Kind != EntityType.Character)
            {
                return EntitySyncMode.Interpolated;
            }
            string id = localEntityId();
            return string.IsNullOrEmpty(id) == false && entity.Id == id
                ? EntitySyncMode.Predicted
                : EntitySyncMode.Extrapolated;
        }
    }
}
```

- [ ] **Step 3: 등록을 바꾼다**

`FlappyRaceLifetimeScope.cs`:

```csharp
            builder.Register<IEntitySyncPolicy>(c =>
                new OwnerPredictedRemotesExtrapolatedSyncPolicy(
                    () => c.Resolve<IGameDataStore>().userEntityId), Lifetime.Singleton);
```

- [ ] **Step 4: 테스트를 갱신한다**

`EntitySyncPolicyTests.cs`의 `CharactersPredictedSyncPolicy` 케이스를 새 정책으로 바꾼다:

```csharp
    [Test]
    public void 내_캐릭터는_예측하고_남은_외삽한다()
    {
        var policy = new OwnerPredictedRemotesExtrapolatedSyncPolicy(() => "me");

        var mine = new GameFramework.World.Entity("me");
        mine.Add(new EntityKind { Kind = EntityType.Character });
        var other = new GameFramework.World.Entity("other");
        other.Add(new EntityKind { Kind = EntityType.Character });

        Assert.That(policy.For(mine), Is.EqualTo(EntitySyncMode.Predicted));
        Assert.That(policy.For(other), Is.EqualTo(EntitySyncMode.Extrapolated));
    }

    [Test]
    public void 아이템은_보간한다()
    {
        var policy = new OwnerPredictedRemotesExtrapolatedSyncPolicy(() => "me");
        var item = new GameFramework.World.Entity("item");
        item.Add(new EntityKind { Kind = EntityType.Item });

        Assert.That(policy.For(item), Is.EqualTo(EntitySyncMode.Interpolated));
    }

    [Test]
    public void 내_id를_아직_모르면_전부_외삽이다()
    {
        var policy = new OwnerPredictedRemotesExtrapolatedSyncPolicy(() => "");
        var bird = new GameFramework.World.Entity("bird");
        bird.Add(new EntityKind { Kind = EntityType.Character });

        Assert.That(policy.For(bird), Is.EqualTo(EntitySyncMode.Extrapolated));
    }
```

- [ ] **Step 5: 테스트 통과 + 커밋**

```bash
unity cmd run_tests --mode EditMode --filter EntitySyncPolicyTests
git commit -m "feat(client): 원격을 외삽하는 동기화 모드를 더한다"
```

---

## Task 9: 외삽 뷰 컴포넌트

**Files:**
- Create: `Client/Assets/Scripts/Netcode/ExtrapolatedEntityInterpolator.cs`
- Modify: `Client/Assets/Scripts/Entity/EntityBinder.cs`
- Modify: `Client/Assets/Scripts/Game/MessageHandler/GameEntityMessageHandler.cs`

**Interfaces:**
- Consumes: `SnapshotExtrapolation.Position` (Task 7), `EntitySyncMode.Extrapolated` (Task 8)

- [ ] **Step 1: 뷰를 만든다**

```csharp
using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 외삽 모드 엔티티. 마지막 스냅의 위치·속도에서 지금 시각까지 이어 그린다(중력 포함 포물선).
    /// 새 스냅이 오면 끊지 않고 짧게 섞는다. 시뮬은 이 엔티티를 굴리지 않는다.
    /// </summary>
    public class ExtrapolatedEntityInterpolator : MonoBehaviour, ICleanup
    {
        private const float MaxExtrapolation = 0.25f;   // Source cl_extrapolate_amount 기본값과 같은 값
        private const float BlendDuration = 0.1f;       // 새 스냅으로 옮겨 타는 시간

        [Inject] private GameFramework.Netcode.INetworkTime networkTime;

        public GameFramework.World.Entity worldEntity { get; set; }
        public LOPActor actor { get; set; }

        /// <summary>중력(아래 방향 가속). 게임 스코프가 주입한다 — 커널은 도메인을 모른다.</summary>
        public Vector3 acceleration { get; set; }

        private EntitySnap latest;
        private bool hasSnap;
        private Vector3 blendFrom;
        private float blendRemaining;

        public void AddServerEntitySnap(EntitySnap snap)
        {
            if (hasSnap && snap.timestamp <= latest.timestamp)
            {
                return;   // unreliable 순서역전
            }
            if (hasSnap)
            {
                blendFrom = CurrentPosition();
                blendRemaining = BlendDuration;
            }
            latest = snap;
            hasSnap = true;
        }

        private Vector3 CurrentPosition()
        {
            float elapsed = (float)(networkTime.time - latest.timestamp);
            return GameFramework.Netcode.SnapshotExtrapolation.Position(
                latest.position, latest.velocity, acceleration, elapsed, MaxExtrapolation);
        }

        private void LateUpdate()
        {
            if (hasSnap == false)
            {
                return;
            }

            Vector3 target = CurrentPosition();
            if (blendRemaining > 0f)
            {
                blendRemaining -= Time.deltaTime;
                float u = Mathf.Clamp01(1f - blendRemaining / BlendDuration);
                target = Vector3.Lerp(blendFrom, target, u);
            }

            // 월드 엔티티에도 쓴다 — 몸싸움(Task 10)이 이 위치를 본다.
            if (worldEntity != null)
            {
                worldEntity.Get<GameFramework.World.Transform>().Position = target.ToNumerics();
            }
            if (actor?.visualGameObject != null)
            {
                actor.visualGameObject.transform.position = target;
                actor.visualGameObject.transform.rotation = Quaternion.Euler(latest.rotation);
            }
        }

        public void Cleanup()
        {
            hasSnap = false;
        }
    }
}
```

- [ ] **Step 2: `EntityBinder`를 3-모드로 바꾼다**

지금의 `if (Predicted) ... else ...` 두 갈래를 `switch`로:

```csharp
            switch (syncMode)
            {
                case EntitySyncMode.Predicted:
                {
                    PredictedEntityInterpolator interpolator = root.AddComponent<PredictedEntityInterpolator>();
                    objectResolver.Inject(interpolator);
                    interpolator.actor = actor;
                    break;
                }
                case EntitySyncMode.Extrapolated:
                {
                    ExtrapolatedEntityInterpolator interpolator = root.AddComponent<ExtrapolatedEntityInterpolator>();
                    objectResolver.Inject(interpolator);
                    interpolator.worldEntity = worldEntity;
                    interpolator.actor = actor;
                    interpolator.acceleration = new Vector3(0f, -flappyConfig.Gravity, 0f);
                    break;
                }
                default:
                {
                    SnapshotEntityInterpolator interpolator = root.AddComponent<SnapshotEntityInterpolator>();
                    objectResolver.Inject(interpolator);
                    interpolator.worldEntity = worldEntity;
                    interpolator.actor = actor;
                    break;
                }
            }
```

> `flappyConfig`는 게임 스코프에만 있으므로 `EntityBinder`에 직접 주입하지 않는다. 대신 **중력을 넘겨줄 얇은 공급자**(`IExtrapolationAcceleration`, `Vector3 Value { get; }`)를 만들어 게임 스코프가 등록하고 FlapWang은 `Vector3.zero`를 등록한다. `EntityBinder`는 그 인터페이스만 안다.

- [ ] **Step 3: 스냅 라우팅에 새 모드를 태운다**

`GameEntityMessageHandler`가 `Simulated` 유무로 갈랐던 부분을, 부착된 컴포넌트로 고쳐 라우팅한다:

```csharp
            // 외삽·보간 둘 다 스냅을 받아야 한다. 예측만 Reconciler로 간다.
            if (actor.TryGetComponent(out ExtrapolatedEntityInterpolator extrapolated))
            {
                extrapolated.AddServerEntitySnap(snap);
            }
            else if (actor.TryGetComponent(out SnapshotEntityInterpolator interpolated))
            {
                interpolated.AddServerEntitySnap(snap);
            }
            else
            {
                reconciler.OnServerSnap(snap);
            }
```

- [ ] **Step 4: 컴파일 + 커밋**

```bash
unity cmd recompile
git commit -m "feat(client): 원격을 외삽으로 그리는 뷰를 더한다"
```

> **여기까지가 G3이다.** 런타임 확인 — §검증 G3.

---

## Task 10: 내 새만 원격에 부딪히게 한다

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyWorld.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/FlappyWorldLocalBodyTests.cs`

**Interfaces:**
- Consumes: 원격의 `Transform.Position` (Task 9가 매 프레임 갱신)

> **왜 되나**: Task 9의 뷰가 외삽 결과를 `World.Transform.Position`에 쓴다. 그래서 시뮬이 원격을 *굴리지는* 않아도 그 위치는 최신이다. 몸싸움은 그 위치를 읽기만 한다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

```csharp
[Test]
public void 시뮬_대상이_아닌_새도_밀어내기_상대가_된다()
{
    // 내 새(Simulated) 하나 + 원격(Simulated 없음) 하나를 겹쳐 둔다.
    var world = FlappyWorldFixture.CreateWithRemote(out var mine, out var remote);
    SetPosition(remote, GetPosition(mine));   // 완전히 겹침

    world.Tick(1, 0.02f);

    // 내 새는 밀려났고, 원격은 그대로다(반작용 없음 — 서버가 정한다).
    Assert.That(GetPosition(mine), Is.Not.EqualTo(GetPosition(remote)));
    Assert.That(GetPosition(remote), Is.EqualTo(RemoteStart));
}
```

- [ ] **Step 2: 실패 확인**

Expected: 지금은 `CollectBirds`가 `Simulated`만 모으므로 원격이 아예 상대에 없다.

- [ ] **Step 3: 상대 목록을 나눈다**

`CollectBirds`를 둘로 나눈다 — **굴릴 대상**(`_birds`, `Simulated`)과 **부딪힐 상대**(`_bodies`, `FlappyGhost`를 가진 모든 새):

```csharp
        // 굴리는 대상과 부딪히는 상대는 다르다. 클라에서 원격은 굴리지 않지만(외삽으로 그린다)
        // 내 새가 그 자리에 부딪히기는 해야 한다 — 부딪힘이 서버 왕복 뒤에 보이면 반응이 굼뜨다.
        private void CollectBirds()
        {
            _birds.Clear();
            _bodies.Clear();
            foreach (var entity in EntityRegistry.All)
            {
                if (entity.Has<FlappyGhost>() == false)
                {
                    continue;   // 새가 아니다
                }
                _bodies.Add(entity);
                if (entity.Has<GameFramework.World.Simulated>())
                {
                    _birds.Add(entity);
                }
            }
            _birds.Sort((l, r) => string.CompareOrdinal(l.Id, r.Id));
            _bodies.Sort((l, r) => string.CompareOrdinal(l.Id, r.Id));
        }
```

- [ ] **Step 4: 몸싸움을 한쪽만 적용하게 한다**

`FlappyBodyCollisionSystem`에 오버로드를 더한다 — `Resolve(movers, bodies)`: `movers`만 위치·속도가 바뀌고 `bodies`는 읽기 전용. 서버는 `Resolve(birds, birds)`로 지금과 같은 양방향, 클라는 `Resolve(birds, bodies)`.

`FlappyWorld.Mutation`의 호출을 `_bodyCollisionSystem.Resolve(_birds, _bodies)`로 바꾼다. **서버에서는 `_birds == _bodies`가 되므로 동작이 지금과 같다**(서버는 모든 새가 `Simulated`).

- [ ] **Step 5: 테스트 통과 + 커밋**

```bash
unity cmd run_tests --mode EditMode
git commit -m "feat(flappy): 내 새가 원격의 외삽 위치에 부딪히게 한다"
```

- [ ] **Step 6: 스펙의 열린 결정 두 개를 여기서 닫는다**

2인 플레이로 실제로 겹쳐 보고 판단해 스펙 §9에 결과를 적는다.

| 열린 결정 | 어떻게 판단하나 |
|---|---|
| 유령인 새를 남이 밀 수 있나 | 지금 구현은 유령도 `_bodies`에 있어 **밀리는 상대가 된다**(위치는 서버가 정하므로 로컬에선 안 밀림). 겹친 채 굳어 보이면 유령을 `_bodies`에서 빼는 쪽으로 바꾼다 |
| 내 새의 유령 진입을 클라가 예측하나 | 지금은 서버 권위(스냅으로만 받음). 부딪힘 반응이 굼떠 보이면 로컬 진입을 붙이되, **틀리면 0.8초 정지를 잘못 보여 준다**는 비용을 먼저 적어 둔다 |

---

## Task 11: 보정 스무딩 임계를 실측으로 맞춘다

**Files:**
- Modify: `Client/Assets/Scripts/Game/GameplayInstaller.cs`

> 지금 값 `RenderCorrectionSmoother(0.1f, 0.025f, 3f)`의 텔레포트 임계 3m는 *원격을 시뮬로 굴리던* 시절에 정해졌다. 외삽으로 바뀌면 보정이 작아지므로 임계를 내려야 한다. **값을 지어내지 않고 측정해서 정한다.**

- [ ] **Step 1: 보정 크기를 측정한다**

2인 플레이로 한 판 돌리며 Debug HUD의 `Recon last/avg/max`를 기록한다. 부딪힘을 여러 번 만든다.

```bash
export PATH="$HOME/.unity/bin:$PATH"
unity cmd console --tail 200   # [ReconSpike] 로그의 err 분포를 본다
```

- [ ] **Step 2: 임계를 정한다**

측정된 `max`의 2~3배를 텔레포트 임계로 잡는다(정상 보정은 절대 스냅되지 않고, 진짜 이상치만 스냅되게). 기록한 수치와 고른 값을 커밋 메시지에 남긴다.

- [ ] **Step 3: 적용 + 커밋**

세 번째 인자(텔레포트 임계)만 바꾼다. Step 2에서 고른 수치를 그대로 쓴다 — 예를 들어 측정
`max`가 0.35m였다면 `1.0f`로 둔다.

```csharp
            builder.Register(_ => new GameFramework.Netcode.RenderCorrectionSmoother(0.1f, 0.025f, 1.0f), Lifetime.Transient);
```

> 위 `1.0f`는 **예시가 아니라 자리**다. Step 1의 실측 없이 그대로 쓰지 말 것 — 측정값의 2~3배로
> 정하고, 측정 수치와 고른 근거를 커밋 메시지에 남긴다.

```bash
git commit -m "tune(client): 보정 스무딩 임계를 외삽 기준으로 낮춘다"
```

---

## 검증

### 단위

각 Task의 마지막 Step. 전체는 `unity cmd run_tests --mode EditMode`로 클·서 양쪽에서 한 번씩.

### 런타임 (슬라이스 경계마다)

두 클라(메인 + MPPM 클론)를 `unity` CLI로 몰아 확인한다. 클론이 검은 화면으로 멈추면 `editor_stop` → `editor_play`로 되살린다.

| 경계 | 확인 | 어떻게 |
|---|---|---|
| **G1** (Task 4 후) | 파이프에 부딪혀도 **끼이지 않는다** / 카메라가 **떨리지 않는다** / 스폰에서 **갇히지 않는다** | 일부러 파이프에 박아 본다. 갇히면 실패 |
| **G2** (Task 6 후) | 남이 멈출 때 **반투명으로 보인다** | 2인 플레이, 상대가 부딪히게 유도 |
| **G3** (Task 9 후) | 남의 새가 **순간이동하지 않는다** | **육안으로 20초 이상 지켜본다** |
| **G4** (Task 11 후) | 부딪히면 **내 새가 즉시 밀린다** / 밀린 뒤 크게 튀지 않는다 | 2인으로 서로 들이받는다 |

> **⚠️ 어제의 실패를 반복하지 않는다.** 2026-08-23 슬라이스는 *접촉 순간의 좌표 일치*만 확인하고 통과시켰고, 그래서 "원격이 순간이동한다"를 놓쳤다. **G3·G4는 수치가 아니라 육안이 기준이다.** 좌표가 맞아도 눈에 튀면 실패로 친다.

### 회귀

- **FlapWang 한 판** — 정책·유령정지 모두 Flappy 전용이지만, `EntityBinder`·`EntitySnap`·`GameEntityMessageHandler`는 공용이다. 내 캐릭터가 인식되고 조작되며 남이 부드러운지 본다.

---

## 머지 순서

패키지가 먼저다 — 서버 CI가 형제 패키지를 `origin/main`으로 맞추므로, 패키지가 main에 없으면 서버 빌드가 깨진다.

```
infrastructure → MasterData-Client/Server → GameFramework → LOP-Shared → Server → Client
```

각 저장소에서 `CLAUDE.md`의 푸시 규약(fetch → rebase --autostash → checkout main → merge --ff-only → merge --no-ff → push)을 **한 줄씩 확인하며** 밟는다. Unity 레포는 로컬 픽스처를 `git stash push -u`로 빼 두고 끝나면 `pop`한다.
