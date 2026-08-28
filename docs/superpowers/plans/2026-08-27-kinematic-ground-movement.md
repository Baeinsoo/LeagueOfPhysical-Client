# 키네마틱 이동 — 지면 따라 이동 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 새가 경사면을 스칠 때 25Hz로 떠는 것을 없앤다 — 수평 sweep이 실제 몸 자리에서 이루어지고, 지면 위에서는 지면 경사를 따라 움직이게 한다.

**Architecture:** `LOP-Shared`의 공유 이동 커널 `KinematicMover.Move` 하나를 고친다. 매 틱 발밑을 짧게 훑어 지면을 찾고(무상태), 찾으면 바닥에서 `SkinWidth`만큼 띄운 뒤 이동을 지면 평면에 투영한다. 통짜 들어올리기(`StepOffset`)를 없애고, 턱 오르기는 입력 파라미터로 받아 **막혔을 때만** 3-sweep으로 시도한다.

**Tech Stack:** C# / Unity 6000.3 / NUnit EditMode. 커널은 `UnityEngine.Vector3`만 쓰는 순수 static 함수 (`ICollisionQuery` 포트 뒤로 물리 격리).

**Spec:** `docs/superpowers/specs/2026-08-27-kinematic-ground-movement-design.md`

## Global Constraints

- **커널은 순수 함수로 유지한다.** 프레임 간 상태를 들지 않는다 — 클라 롤백 재생이 라이브와 같은 답을 내야 한다 (스펙 D3).
- **경사를 올라도 세로 속도를 늘리지 않는다** (스펙 D5). 커널은 지금처럼 `horizVel.y`를 버리고 `input.velocity.y` 계열만 세로로 돌려준다.
- **상수 유지**: `SkinWidth = 0.02f`, `GroundNormalY = 0.7f`, `MaxSlides = 4`. 새로 추가하는 건 `GroundProbe = 0.05f` 하나.
- **`KinematicMover`는 Flappy와 FlapWang이 함께 쓴다.** 두 호출부(`FlappyWorld.MoveBlockedByMap`, `KinematicMoveSystem.Tick`)를 항상 같이 맞춘다.
- **`git add -A` / `git commit -a` 금지.** 바꾼 파일만 경로로 지정하고 커밋 전 `git status --short`로 확인한다. 워킹트리에는 커밋하지 않는 로컬 픽스처가 늘 있다.
- **main에 직접 커밋 금지.** 작업 브랜치는 `feature/flappy-slope-tremble` (Shared·Client 양쪽에 이미 만들어져 있다).
- **테스트 실행**: `. "$HOME/.unity/env"` 후 `unity command run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --mode editor --async_tests true` → `unity command test_status --project-path ...`. **`--async_tests true`를 빼면 `total:0`을 초록처럼 돌려준다** — `failed:0`만 보지 말고 `total`이 기대한 개수인지 반드시 확인할 것.
- **재컴파일 전에 재생 상태를 확인한다**: `unity command eval --project-path ... --code 'UnityEngine.Debug.Log("PLAYING=" + UnityEditor.EditorApplication.isPlaying);'` → `unity command console --tail 1`. 재생 중에 `recompile`을 걸면 사용자의 판이 끊기고 브릿지가 안 돌아올 수 있다.
- LOP-Shared는 패키지이므로 테스트는 **Client 프로젝트의 EditMode에서 함께 돈다**. `--project-path`는 Client를 가리킨다.

---

## File Structure

| 파일 | 책임 |
|---|---|
| `LeagueOfPhysical-Shared/Runtime/Scripts/Game/KinematicMover.cs` | **수정** — 지면 찾기·간격 유지·지면 투영·명시적 턱 오르기 |
| `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyWorld.cs` | **수정** — 호출부에 `stepOffset: 0f` |
| `LeagueOfPhysical-Shared/Runtime/Scripts/Game/KinematicMoveSystem.cs` | **수정** — 호출부에 `stepOffset: 0.1f` (FlapWang 기존 감각 유지) |
| `LeagueOfPhysical-Shared/Tests/EditMode/HalfSpaceQuery.cs` | **신규** — 반평면(바닥·벽·경사)·턱 지오메트리 테스트 쿼리 + 밀어내기 브릿지 |
| `LeagueOfPhysical-Shared/Tests/EditMode/KinematicMoverSlopeTests.cs` | **신규** — 커널 경사 회귀 |
| `LeagueOfPhysical-Shared/Tests/EditMode/FlappyWorldSlopeTremorTests.cs` | **신규** — 월드 레벨 떨림 회귀 |
| `LeagueOfPhysical-Shared/Tests/EditMode/KinematicMoverTests.cs` | **수정** — 스크립트 쿼리를 방향별로, 새 파라미터 반영 |

---

## Task 1: 떨림을 빨간불로 고정한다

지금 코드에서 **실패하는 것을 눈으로 본 뒤에** 고친다. 실측으로 확인된 두 가지만 여기서 다룬다.

**Files:**
- Create: `LeagueOfPhysical-Shared/Tests/EditMode/HalfSpaceQuery.cs`
- Create: `LeagueOfPhysical-Shared/Tests/EditMode/KinematicMoverSlopeTests.cs`
- Create: `LeagueOfPhysical-Shared/Tests/EditMode/FlappyWorldSlopeTremorTests.cs`

**Interfaces:**
- Produces: `LOP.Tests.HalfSpaceQuery` (`AddGround`/`AddWall`/`AddSlope`/`Clearance`/`PushOut`), `LOP.Tests.HalfSpaceMotionBridge`, `LOP.Tests.StepQuery` — Task 2·3이 그대로 쓴다.
- Consumes: `LOP.Tests.FlappyWorldFixture.Create(ICollisionQuery, IMotionBridge, out Entity)` (이미 있음).

- [ ] **Step 1: 테스트용 지오메트리 쿼리를 만든다**

`LeagueOfPhysical-Shared/Tests/EditMode/HalfSpaceQuery.cs`:

```csharp
using System.Collections.Generic;
using GameFramework.Physics;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 반평면(바닥·벽·경사)으로 맵을 흉내내는 테스트용 충돌 쿼리.
    /// 스크립트된 응답 큐(<c>FakeCollisionQuery</c>)와 달리 실제 지오메트리라서
    /// "이동한 결과가 면 안쪽인가" 같은 위치 기반 검증이 가능하다 — 경사 파묻힘 재현이 그것이다.
    /// </summary>
    internal class HalfSpaceQuery : ICollisionQuery
    {
        internal struct Face
        {
            public Vector3 Point;
            public Vector3 Normal;   // 이 방향이 빈 공간, 반대쪽이 solid
        }

        public readonly List<Face> Faces = new List<Face>();

        public void AddGround(float y)
            => Faces.Add(new Face { Point = new Vector3(0f, y, 0f), Normal = Vector3.up });

        public void AddWall(float x)
            => Faces.Add(new Face { Point = new Vector3(x, 0f, 0f), Normal = Vector3.left });

        /// <param name="degrees">+x로 갈수록 높아지는 오르막의 각도.</param>
        public void AddSlope(float degrees, Vector3 through)
        {
            float rad = degrees * Mathf.Deg2Rad;
            Faces.Add(new Face { Point = through, Normal = new Vector3(-Mathf.Sin(rad), Mathf.Cos(rad), 0f) });
        }

        /// <summary>면에서 캡슐까지의 여유. 음수면 그만큼 파묻혔다는 뜻.</summary>
        public float Clearance(Face face, Vector3 p1, Vector3 p2, float radius)
            => Mathf.Min(Vector3.Dot(p1 - face.Point, face.Normal),
                         Vector3.Dot(p2 - face.Point, face.Normal)) - radius;

        public CollisionHit CapsuleCast(Vector3 p1, Vector3 p2, float radius,
            Vector3 direction, float distance, int layerMask)
        {
            float best = float.MaxValue;
            Vector3 normal = Vector3.zero;
            foreach (var face in Faces)
            {
                float clear = Clearance(face, p1, p2, radius);
                //  이미 파묻힌 면은 sweep이 못 본다 — PhysX가 시작 겹침을 무시하는 것과 같다.
                //  이 성질이 없으면 파묻힘 버그 자체가 재현되지 않는다.
                if (clear < 0f) continue;
                float closing = Vector3.Dot(direction, face.Normal);
                if (closing >= -1e-6f) continue;   // 멀어지거나 평행
                float t = clear / -closing;
                if (t <= distance && t < best) { best = t; normal = face.Normal; }
            }
            return best == float.MaxValue
                ? CollisionHit.None
                : new CollisionHit(true, best, normal, p1, null);
        }

        public CollisionHit Raycast(Vector3 origin, Vector3 direction, float distance, int layerMask)
            => CollisionHit.None;

        public CollisionHit[] OverlapSphere(Vector3 center, float radius, int layerMask)
            => System.Array.Empty<CollisionHit>();

        /// <summary>지금 캡슐이 면 안쪽이면 밖으로 밀어낼 벡터(겹침 없으면 zero).</summary>
        public Vector3 PushOut(Vector3 p1, Vector3 p2, float radius)
        {
            Vector3 total = Vector3.zero;
            foreach (var face in Faces)
            {
                float clear = Clearance(face, p1, p2, radius);
                if (clear < 0f) total += face.Normal * -clear;
            }
            return total;
        }
    }

    /// <summary>진짜 <c>MotionBridge</c>처럼 파묻힘을 World.Transform에 반영하고 민 값을 돌려준다.</summary>
    internal class HalfSpaceMotionBridge : GameFramework.World.IMotionBridge
    {
        private readonly HalfSpaceQuery _map;

        public HalfSpaceMotionBridge(HalfSpaceQuery map) { _map = map; }

        public void SyncTransforms() { }
        public void Separate(GameFramework.World.Entity entity) { }
        public void PushMotion(GameFramework.World.Entity entity) { }

        public System.Numerics.Vector3 Depenetrate(GameFramework.World.Entity entity)
        {
            var transform = entity.Get<GameFramework.World.Transform>();
            var shape = entity.Get<GameFramework.World.CapsuleShape>();
            Vector3 feet = new Vector3(transform.Position.X, transform.Position.Y, transform.Position.Z);
            Vector3 p1 = feet + Vector3.up * shape.Radius;
            Vector3 p2 = feet + Vector3.up * (shape.Height - shape.Radius);
            Vector3 push = _map.PushOut(p1, p2, shape.Radius);
            if (push == Vector3.zero)
            {
                return System.Numerics.Vector3.Zero;
            }
            transform.Position += new System.Numerics.Vector3(push.x, push.y, push.z);
            return new System.Numerics.Vector3(push.x, push.y, push.z);
        }
    }

    /// <summary>바닥(y=0)에 <c>StepX</c>부터 <c>StepHeight</c>짜리 한 단이 있는 지형. 턱 오르기 검증용.</summary>
    internal class StepQuery : ICollisionQuery
    {
        public float StepX = 1f;
        public float StepHeight = 0.1f;

        private float SurfaceY(float x) => x >= StepX ? StepHeight : 0f;

        public CollisionHit CapsuleCast(Vector3 p1, Vector3 p2, float radius,
            Vector3 direction, float distance, int layerMask)
        {
            float bottom = Mathf.Min(p1.y, p2.y) - radius;
            if (direction.y < -0.5f)
            {
                float gap = bottom - SurfaceY(p1.x);
                return gap >= 0f && gap <= distance
                    ? new CollisionHit(true, gap, Vector3.up, Vector3.zero, null) : CollisionHit.None;
            }
            if (direction.y > 0.5f)
            {
                return CollisionHit.None;   // 천장 없음
            }
            if (bottom >= StepHeight - 1e-4f)
            {
                return CollisionHit.None;   // 턱보다 높이 있으면 안 막힌다
            }
            float ahead = (StepX - radius) - p1.x;
            return ahead >= 0f && ahead <= distance
                ? new CollisionHit(true, ahead, Vector3.left, Vector3.zero, null) : CollisionHit.None;
        }

        public CollisionHit Raycast(Vector3 origin, Vector3 direction, float distance, int layerMask)
            => CollisionHit.None;

        public CollisionHit[] OverlapSphere(Vector3 center, float radius, int layerMask)
            => System.Array.Empty<CollisionHit>();
    }
}
```

- [ ] **Step 2: 커널 회귀 테스트를 쓴다 (파묻힘)**

`LeagueOfPhysical-Shared/Tests/EditMode/KinematicMoverSlopeTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class KinematicMoverSlopeTests
    {
        const float Radius = 0.45f;
        const float Height = 0.9f;
        const float DeltaTime = 0.02f;
        const float Gravity = 70f;
        const float ForwardSpeed = 11f;

        [Test]
        public void 오르막을_지나도_몸이_경사_안으로_파묻히지_않는다()
        {
            //  수평 sweep이 캡슐을 들어올려 검사하면서 실제 위치는 안 올리면, 오르막에서
            //  그 차이만큼 몸이 언덕에 박힌다(실측 2.7cm). 박힘이 곧 떨림의 씨앗이다.
            var map = new HalfSpaceQuery();
            map.AddSlope(32f, Vector3.zero);

            Vector3 pos = new Vector3(-1f, 0.6f, 0f);
            Vector3 vel = new Vector3(ForwardSpeed, 0f, 0f);

            for (int tick = 0; tick < 60; tick++)
            {
                vel.y -= Gravity * DeltaTime;
                var result = KinematicMover.Move(
                    new KinematicMoveInput(pos, vel, Radius, Height, DeltaTime, ~0), map);
                pos = result.position;
                vel = result.velocity;

                Vector3 p1 = pos + Vector3.up * Radius;
                Vector3 p2 = pos + Vector3.up * (Height - Radius);
                float clear = map.Clearance(map.Faces[0], p1, p2, Radius);
                Assert.That(clear, Is.GreaterThan(-1e-3f),
                    $"t{tick}: 이동 뒤 몸이 경사 안으로 {-clear:F4}m 파묻혔다");
            }
        }
    }
}
```

- [ ] **Step 3: 월드 회귀 테스트를 쓴다 (떨림)**

`LeagueOfPhysical-Shared/Tests/EditMode/FlappyWorldSlopeTremorTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class FlappyWorldSlopeTremorTests
    {
        [Test]
        public void 날갯짓하지_않았는데_세로_속도가_위를_향하면_안_된다()
        {
            //  파묻힌 몸을 밀어내면서 "표면으로 파고드는 속도"를 지우는데, 고정 전진 속도가
            //  경사 법선에 크게 걸려 있어 그 제거가 세로 +4.16m/s짜리 발길질이 된다.
            //  파묻혔다/안 파묻혔다를 매 틱 오가므로 25Hz로 떤다.
            //  입력이 없는 새의 세로 속도는 중력·지면이 소유한다 — 위를 향할 이유가 없다.
            var map = new HalfSpaceQuery();
            map.AddSlope(32f, Vector3.zero);

            var world = FlappyWorldFixture.Create(map, new HalfSpaceMotionBridge(map), out var bird);
            world.GameplayStartTick = 0;
            bird.Get<GameFramework.World.Transform>().Position = new System.Numerics.Vector3(-1f, 0.6f, 0f);

            for (long tick = 0; tick < 120; tick++)
            {
                world.Tick(tick, 0.02f);
                float vy = bird.Get<GameFramework.World.Velocity>().Linear.Y;
                Assert.That(vy, Is.LessThanOrEqualTo(1e-3f),
                    $"t{tick}: 입력이 없는데 세로 속도가 +{vy:F2} — 경사가 새를 밀어 올리고 있다");
            }
        }
    }
}
```

- [ ] **Step 4: 재생 중이 아닌지 확인하고 컴파일한다**

```bash
. "$HOME/.unity/env"
unity command eval --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client \
  --code 'UnityEngine.Debug.Log("PLAYING=" + UnityEditor.EditorApplication.isPlaying);'
unity command console --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --tail 1
```
`PLAYING=False`를 확인한 뒤에만 다음으로 간다. `True`면 멈추고 보고한다.

```bash
unity command recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity command recompile_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
```
Expected: `"errors":[]`

- [ ] **Step 5: 빨간불을 확인한다**

```bash
unity command run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client \
  --mode editor --async_tests true --filter Slope
unity command test_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
```
Expected: **두 테스트 모두 Failed.** 실패 메시지에 파묻힌 깊이(≈0.027)와 세로 속도(+4.x)가 찍혀야 한다.
둘 중 하나라도 통과하면 재현이 안 된 것이므로 멈추고 보고한다 — 통과하는 테스트는 아무것도 지키지 않는다.

- [ ] **Step 6: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git add Tests/EditMode/HalfSpaceQuery.cs Tests/EditMode/HalfSpaceQuery.cs.meta \
        Tests/EditMode/KinematicMoverSlopeTests.cs Tests/EditMode/KinematicMoverSlopeTests.cs.meta \
        Tests/EditMode/FlappyWorldSlopeTremorTests.cs Tests/EditMode/FlappyWorldSlopeTremorTests.cs.meta
git status --short
git commit -m "test(flappy): 경사 떨림을 빨간불로 고정한다

수평 sweep이 캡슐을 0.1m 들어올려 검사하면서 실제 위치는 안 올려서, 오르막에서
매 틱 2.7cm씩 몸이 파묻힌다. 그 복구가 고정 전진 속도를 세로 +4.16으로 꺾어
25Hz 떨림이 된다. 두 사실을 각각 테스트로 박는다(지금은 둘 다 실패)."
```

> `.meta`는 유니티가 만들어 준 것만 커밋한다. 임포트 전이라 없으면 Step 4 재컴파일 뒤에 생긴다.

---

## Task 2: 지면을 매 틱 찾고, 바닥에서 살짝 띄운다

들어올리기를 없애기 **전에** 그 자리를 대신할 것을 먼저 넣는다. 이 태스크만으로는 떨림이 안 고쳐진다.

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/KinematicMover.cs`
- Modify: `LeagueOfPhysical-Shared/Tests/EditMode/KinematicMoverTests.cs`

**Interfaces:**
- Produces: `KinematicMover.Move` 내부에 `onGround`/`groundNormal` — Task 3이 투영에 쓴다.
- Consumes: Task 1의 `HalfSpaceQuery`.

- [ ] **Step 1: 기존 스크립트 쿼리를 방향별로 나눈다 (선행 정리)**

지금 `FakeCollisionQuery`는 **방향과 무관하게 큐 순서대로** 응답한다. 지면 탐침을 앞에 추가하면
그 응답을 가로채 기존 테스트 4개(`HeadOnWall`/`AngledWall`/`GroundHit`/`AlwaysBlocked`)가 깨진다.
탐침을 넣기 전에 쿼리를 방향별로 나눈다.

`KinematicMoverTests.cs`의 `FakeCollisionQuery`를 통째로 교체:

```csharp
        // 스크립트된 충돌 응답을 돌려주는 테스트용 쿼리(씬 없이 collide-and-slide 로직만 검증).
        // 수평/수직 큐를 나눈 이유: 커널이 이동 전에 발밑을 훑는 캐스트를 한 번 하므로,
        // 큐가 하나면 그 탐침이 수평용 응답을 먹어 버린다.
        private class FakeCollisionQuery : ICollisionQuery
        {
            public readonly Queue<CollisionHit> Horizontal = new Queue<CollisionHit>();
            public readonly Queue<CollisionHit> Vertical = new Queue<CollisionHit>();
            public int HorizontalCallCount;

            public CollisionHit CapsuleCast(Vector3 point1, Vector3 point2, float radius,
                Vector3 direction, float distance, int layerMask)
            {
                if (Mathf.Abs(direction.y) > 0.5f)
                {
                    return Take(Vertical, distance);
                }
                HorizontalCallCount++;
                return Take(Horizontal, distance);
            }

            //  실제 sweep은 요청한 거리 밖의 것을 못 본다 — 스크립트 응답도 같게 다룬다.
            //  이게 없으면 이동 전 지면 탐침(짧은 거리)이 수직 스텝용 응답을 먼저 먹어,
            //  GroundHit_SetsGrounded_AndZeroesVerticalVelocity가 엉뚱하게 깨진다.
            private static CollisionHit Take(Queue<CollisionHit> queue, float distance)
            {
                if (queue.Count == 0 || queue.Peek().Distance > distance)
                {
                    return CollisionHit.None;
                }
                return queue.Dequeue();
            }

            public CollisionHit Raycast(Vector3 origin, Vector3 direction, float distance, int layerMask)
                => CollisionHit.None;

            public CollisionHit[] OverlapSphere(Vector3 center, float radius, int layerMask)
                => System.Array.Empty<CollisionHit>();
        }
```

호출부를 맞춘다:
- `HeadOnWall_...`, `AngledWall_...`, `AlwaysBlocked_...` → `query.Responses.Enqueue(...)` 를 `query.Horizontal.Enqueue(...)` 로.
- `GroundHit_...` → `query.Vertical.Enqueue(...)` 로.
- `AlwaysBlocked_...` 의 마지막 단언을 `query.CallCount` → `query.HorizontalCallCount` 로.

> **왜 거리 검사까지 넣는가**: `GroundHit_...`는 거리 0.1짜리 바닥 히트를 하나 넣는다. Task 2에서
> 넣을 지면 탐침은 `SkinWidth + GroundProbe = 0.07`만 요청하는데, 거리 검사가 없으면 그 0.1짜리
> 응답을 탐침이 먼저 가져가 버린다. 그러면 수직 스텝은 빈손이 되고 `velocity.y`가 0으로 안 죽어
> **원래 검증하려던 것과 무관하게** 빨간불이 난다. 스크립트 쿼리를 실제 sweep처럼 만들어 막는다.

- [ ] **Step 2: 여기서 한 번 돌려 초록을 확인한다**

```bash
unity command recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity command recompile_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity command run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client \
  --mode editor --async_tests true --filter KinematicMoverTests
unity command test_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
```
Expected: `KinematicMoverTests` 6개 전부 Passed (아직 커널은 안 바꿨으므로 동작이 같아야 한다).

- [ ] **Step 3: 지면 탐침 테스트를 먼저 쓴다**

`KinematicMoverSlopeTests.cs`에 추가:

```csharp
        [Test]
        public void 바닥에_딱_붙어_있으면_SkinWidth만큼_띄운다()
        {
            //  밀어내기는 바닥에 딱 붙게 민다. 그 상태로 수평 sweep을 쏘면 거리 0으로 맞아
            //  한 발도 못 나간다(예전에 통짜 들어올리기가 가려 주던 경우).
            var map = new HalfSpaceQuery();
            map.AddGround(0f);

            var result = KinematicMover.Move(
                new KinematicMoveInput(Vector3.zero, new Vector3(0f, -1f, 0f), Radius, Height, DeltaTime, ~0), map);

            Assert.That(result.position.y, Is.EqualTo(0.02f).Within(1e-3f), "바닥에서 SkinWidth만큼 떠 있어야 한다");
            Assert.IsTrue(result.grounded);
        }

        [Test]
        public void 위로_오르는_중에는_바닥으로_끌어당기지_않는다()
        {
            //  날갯짓해서 뜨는 새를 지면으로 스냅하면 플랩이 먹히지 않는다.
            var map = new HalfSpaceQuery();
            map.AddGround(0f);

            var result = KinematicMover.Move(
                new KinematicMoveInput(new Vector3(0f, 0.03f, 0f), new Vector3(0f, 5f, 0f), Radius, Height, DeltaTime, ~0), map);

            Assert.That(result.position.y, Is.GreaterThan(0.03f), "오르는 중엔 바닥에 붙이면 안 된다");
            Assert.IsFalse(result.grounded);
        }
```

- [ ] **Step 4: 돌려서 빨간불을 본다**

```bash
unity command recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity command run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client \
  --mode editor --async_tests true --filter KinematicMoverSlopeTests
unity command test_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
```
Expected: `바닥에_딱_붙어...` 는 Failed (지금은 y가 0 그대로). `위로_오르는_중...` 은 Passed일 수 있다
(아직 스냅 코드가 없으니 당연히 안 끌어당긴다) — 그건 회귀 방지용이므로 정상이다.

- [ ] **Step 5: 지면 탐침과 간격 유지를 구현한다**

`KinematicMover.cs`의 상수에 추가:

```csharp
        const float GroundProbe = 0.05f; // 발밑을 이만큼 아래까지 훑어 지면을 찾는다. 한 틱 낙하분(≈0.028)보다 넉넉하되, 떠 있는 몸을 지면으로 오인하지 않을 만큼 짧게.
```

`Move`의 맨 앞(`Vector3 pos = input.position;` 바로 다음)에 삽입:

```csharp
            // (0) 지면 찾기 — 매 틱 다시 잰다(상태를 들지 않아야 롤백 재생이 라이브와 같은 답을 낸다).
            //     찾으면 바닥에서 SkinWidth만큼 띄운다: 딱 붙은 채로 수평 sweep을 쏘면 거리 0으로
            //     맞아 한 발도 못 나간다.
            //     올라가는 중에는 지면으로 치지 않는다 — 그러면 날갯짓해 뜨는 몸을 도로 붙여 버린다.
            bool onGround = false;
            Vector3 groundNormal = Vector3.up;
            if (input.velocity.y <= 0f)
            {
                CollisionHit floor = Cast(pos, SkinWidth, Vector3.down, SkinWidth + GroundProbe, input, query);
                if (floor.HasHit && floor.Normal.y >= GroundNormalY)
                {
                    onGround = true;
                    groundNormal = floor.Normal;
                    //  탐침은 SkinWidth 올린 자리에서 쐈으므로 실제 여유 = Distance - SkinWidth.
                    //  그 여유를 SkinWidth로 맞춘다.
                    pos.y += 2f * SkinWidth - floor.Distance;
                }
            }
```

그리고 수직 스텝의 `bool grounded = false;` 를 `bool grounded = onGround;` 로 바꾼다
(탐침이 이미 지면을 확인했으면 수직 스텝이 안 돌아도 접지다).

- [ ] **Step 6: 돌려서 초록을 확인한다**

```bash
unity command recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity command run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client \
  --mode editor --async_tests true
unity command test_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
```
Expected: `KinematicMoverTests` 6개 + Task 2의 새 2개 Passed. **Task 1의 두 테스트는 아직 Failed** —
아직 들어올리기를 안 없앴으므로 정상이다. `total`이 이전 실행보다 딱 2 늘었는지 확인한다.

- [ ] **Step 7: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/KinematicMover.cs Tests/EditMode/KinematicMoverTests.cs Tests/EditMode/KinematicMoverSlopeTests.cs
git status --short
git commit -m "feat(kinematic): 매 틱 지면을 찾고 바닥에서 살짝 띄운다

이동 전에 발밑을 GroundProbe(0.05m)만큼 훑어 지면과 그 법선을 구한다. 찾으면
바닥에서 SkinWidth만큼 띄워, 딱 붙은 채로 수평 sweep을 쏴 한 발도 못 나가는 경우를
없앤다(지금은 통짜 들어올리기가 이걸 가려 주고 있다). 올라가는 중에는 지면으로 치지
않는다 — 날갯짓해 뜨는 몸을 도로 붙이면 안 된다.

상태를 들지 않는다: 매 틱 다시 재므로 롤백 재생이 라이브와 같은 답을 낸다.

테스트 쿼리를 수평/수직 큐로 나눴다 — 탐침이 수평용 스크립트 응답을 먹어 버리기 때문."
```

---

## Task 3: 지면을 따라 움직이고, 턱 오르기를 명시적으로 만든다

이 슬라이스의 핵심. 통짜 들어올리기 제거 · 지면 투영 · 명시적 턱 오르기를 **한 커밋으로** 한다 —
들어올리기만 먼저 없애면 FlapWang이 턱을 못 올라 중간 상태가 깨진다.

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/KinematicMover.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyWorld.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/KinematicMoveSystem.cs`
- Modify: `LeagueOfPhysical-Shared/Tests/EditMode/KinematicMoverTests.cs`
- Modify: `LeagueOfPhysical-Shared/Tests/EditMode/KinematicMoverSlopeTests.cs`

**Interfaces:**
- Produces: `KinematicMoveInput(Vector3 position, Vector3 velocity, float radius, float height, float deltaTime, int layerMask, float stepOffset)` — 인자 7개로 바뀐다. 호출부 3곳(운영 2 + 테스트 헬퍼 1)을 모두 고쳐야 컴파일된다.
- Consumes: Task 2의 `onGround`/`groundNormal`, Task 1의 `StepQuery`.

- [ ] **Step 1: 턱 오르기 테스트를 먼저 쓴다**

`KinematicMoverSlopeTests.cs`에 추가:

```csharp
        [Test]
        public void 턱_높이를_주면_그_이하의_턱을_오른다()
        {
            var map = new StepQuery { StepX = 1f, StepHeight = 0.1f };
            var result = KinematicMover.Move(
                new KinematicMoveInput(new Vector3(0.5f, 0f, 0f), new Vector3(10f, 0f, 0f),
                    0.35f, 1.5f, 0.1f, ~0, stepOffset: 0.15f), map);

            Assert.That(result.position.x, Is.GreaterThan(1f), "턱을 넘어가야 한다");
            Assert.That(result.position.y, Is.GreaterThan(0.05f), "턱 위로 올라가야 한다");
        }

        [Test]
        public void 턱_높이가_0이면_같은_턱에_막힌다()
        {
            //  나는 새에게 계단 오르기는 의미가 없다 — Flappy는 0을 넘긴다.
            var map = new StepQuery { StepX = 1f, StepHeight = 0.1f };
            var result = KinematicMover.Move(
                new KinematicMoveInput(new Vector3(0.5f, 0f, 0f), new Vector3(10f, 0f, 0f),
                    0.35f, 1.5f, 0.1f, ~0, stepOffset: 0f), map);

            Assert.That(result.position.x, Is.LessThan(1f), "턱 오르기를 끄면 막혀야 한다");
        }
```

이 시점엔 `stepOffset` 파라미터가 없어 **컴파일이 안 된다** — 정상이다. Step 2에서 만든다.

- [ ] **Step 2: 입력에 `stepOffset`을 추가한다**

`KinematicMover.cs`의 `KinematicMoveInput`:

```csharp
    /// <summary>이동 커널 입력: 시작 위치·속도·캡슐 규격·dt·충돌 레이어·턱 높이.</summary>
    public readonly struct KinematicMoveInput
    {
        public readonly Vector3 position;   // 발밑 기준
        public readonly Vector3 velocity;
        public readonly float radius;
        public readonly float height;
        public readonly float deltaTime;
        public readonly int layerMask;
        //  막혔을 때 이 높이까지는 넘어가 본다. 0이면 턱 오르기를 아예 안 한다.
        //  예전엔 커널 상수로 모든 수평 sweep을 이만큼 들어올렸는데, 그게 오르막에서 몸을
        //  파묻히게 만들었다. 이제는 "막혔을 때만" 쓰는 값이라 게임이 정한다.
        public readonly float stepOffset;

        public KinematicMoveInput(Vector3 position, Vector3 velocity, float radius,
            float height, float deltaTime, int layerMask, float stepOffset)
        {
            this.position = position;
            this.velocity = velocity;
            this.radius = radius;
            this.height = height;
            this.deltaTime = deltaTime;
            this.layerMask = layerMask;
            this.stepOffset = stepOffset;
        }
    }
```

`const float StepOffset = 0.1f;` 줄은 **삭제**한다.

- [ ] **Step 3: 수평 스텝을 지면 따라 이동 + 실제 몸 자리 sweep으로 바꾼다**

> ⚠️ **초안 정정**: 이 자리에 원래 `Vector3.ProjectOnPlane(remaining, groundNormal)`이 적혀 있었다.
> 그건 수평 성분을 cos²θ만큼 깎아 언덕을 감속 구간으로 만든다(실측: 내리막 40틱에 x=7.80 → 5.39).
> 아래 코드가 정정된 형태다. 되돌리지 마라.

`Move`의 (1) 블록을 교체:

```csharp
            // (1) 수평 collide-and-slide — 실제 몸 자리에서 검사한다.
            //     지면 위면 이동을 지면 평면에 투영해 경사를 "따라" 간다. 그래야 sweep이 바닥을
            //     정면으로 만나지 않아, 예전처럼 캡슐을 들어올려 속일 필요가 없다.
            //     (들어올리면 검사한 몸과 옮기는 몸이 달라져 오르막에서 실제 몸이 언덕에 파묻혔다.)
            Vector3 horizVel = new Vector3(input.velocity.x, 0f, input.velocity.z);
            Vector3 remaining = horizVel * input.deltaTime;
            if (onGround)
            {
                //  경사를 "따라" 가되 수평 진행은 깎지 않는다. 평면에 그냥 투영하면 수평 성분이
                //  cos²θ만큼 줄어 언덕이 감속 구간이 되고, 내리막이 평지보다 느려진다(32°에서 -28%).
                //  수평 성분은 그대로 두고 세로만 램프에 얹는다 — 언리얼 CMC의 기본값
                //  (bMaintainHorizontalGroundVelocity)이 하는 것과 같다.
                remaining.y = -(remaining.x * groundNormal.x + remaining.z * groundNormal.z) / groundNormal.y;
            }
            for (int i = 0; i < MaxSlides; i++)
            {
                float dist = remaining.magnitude;
                if (dist < 1e-5f)
                {
                    break;
                }
                Vector3 dir = remaining / dist;
                CollisionHit hit = Cast(pos, 0f, dir, dist + SkinWidth, input, query);
                if (hit.HasHit == false)
                {
                    pos += remaining;
                    break;
                }
                float moveDist = Mathf.Max(hit.Distance - SkinWidth, 0f);
                pos += dir * moveDist;
                Vector3 leftover = remaining - dir * moveDist;

                //  걸을 수 없는 면(벽·턱)에 막혔을 때만 넘어가 본다.
                if (input.stepOffset > 0f && hit.Normal.y < GroundNormalY
                    && TryStepUp(ref pos, leftover, input, query))
                {
                    break;
                }

                remaining = Vector3.ProjectOnPlane(leftover, hit.Normal);
                horizVel = Vector3.ProjectOnPlane(horizVel, hit.Normal);
            }
```

- [ ] **Step 4: 명시적 턱 오르기를 구현한다**

`KinematicMover` 클래스 안, `Cast` 옆에 추가:

```csharp
        // 막힌 앞을 넘어가 본다: 위로 들었다 → 앞으로 쓸고 → 다시 내려 착지.
        // 착지면이 걸을 수 있는 면일 때만 채택한다 — 그래야 벽을 기어오르지 않는다.
        // 성공하면 pos를 옮기고 true. 표준 컨트롤러(언리얼 CMC StepUp)의 3-sweep 그대로다.
        private static bool TryStepUp(ref Vector3 pos, Vector3 leftover,
            in KinematicMoveInput input, ICollisionQuery query)
        {
            float dist = leftover.magnitude;
            if (dist < 1e-5f)
            {
                return false;
            }
            Vector3 dir = leftover / dist;

            CollisionHit up = Cast(pos, 0f, Vector3.up, input.stepOffset + SkinWidth, input, query);
            float rise = up.HasHit ? Mathf.Max(up.Distance - SkinWidth, 0f) : input.stepOffset;
            if (rise <= SkinWidth)
            {
                return false;   // 머리 위가 막혀 못 올라간다
            }

            Vector3 lifted = pos + Vector3.up * rise;
            CollisionHit forward = Cast(lifted, 0f, dir, dist + SkinWidth, input, query);
            float advance = forward.HasHit ? Mathf.Max(forward.Distance - SkinWidth, 0f) : dist;
            if (advance <= SkinWidth)
            {
                return false;   // 올려도 못 지나간다 = 진짜 벽
            }

            Vector3 ahead = lifted + dir * advance;
            CollisionHit down = Cast(ahead, 0f, Vector3.down, rise + SkinWidth, input, query);
            if (down.HasHit == false || down.Normal.y < GroundNormalY)
            {
                return false;   // 발 디딜 곳이 아니다
            }

            pos = ahead + Vector3.down * Mathf.Max(down.Distance - SkinWidth, 0f);
            return true;
        }
```

`Move`의 XML 요약 주석도 새 순서에 맞게 고친다 (지면 찾기 → 지면 따라 수평 → 수직).

- [ ] **Step 5: 호출부 세 곳을 맞춘다**

`FlappyWorld.cs` (`MoveBlockedByMap` 안):
```csharp
            var result = KinematicMover.Move(new KinematicMoveInput(
                transform.Position.ToUnity(), velocity.Linear.ToUnity(),
                body.Radius, body.Height, deltaTime, _layerMask, stepOffset: 0f), _hitTracker);
```
> 새는 날아다니므로 턱을 오를 이유가 없다. 0을 준다.

`KinematicMoveSystem.cs` (`Tick` 안):
```csharp
            var result = KinematicMover.Move(new KinematicMoveInput(
                transform.Position.ToUnity(), vel, body.Radius, body.Height, deltaTime,
                _layerMask, stepOffset: StepOffset), _query);
```
그리고 클래스 상수에 추가:
```csharp
        //  캐릭터가 넘어갈 수 있는 턱 높이. 예전엔 커널 상수였다.
        const float StepOffset = 0.1f;
```

`KinematicMoverTests.cs`의 헬퍼:
```csharp
        private static KinematicMoveInput Input(Vector3 pos, Vector3 vel, float dt = 0.1f, float stepOffset = 0f)
            => new KinematicMoveInput(pos, vel, 0.35f, 1.5f, dt, ~0, stepOffset);
```
`KinematicMoverSlopeTests.cs`의 Task 1·2 테스트에도 `stepOffset: 0f`를 넣는다.

- [ ] **Step 6: 전체를 돌린다**

```bash
unity command eval --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client \
  --code 'UnityEngine.Debug.Log("PLAYING=" + UnityEditor.EditorApplication.isPlaying);'
unity command console --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --tail 1
unity command recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity command recompile_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity command run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client \
  --mode editor --async_tests true
unity command test_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
```
Expected: **전부 Passed.** 특히 Task 1의 두 테스트(파묻힘·세로 속도)가 초록으로 바뀌어야 한다.
`total`이 Task 2 실행 대비 2 늘었는지(턱 테스트 2개) 확인한다.

하나라도 빨간불이면 멈추고 보고한다. 특히 `GroundedHorizontalMove_MovesAlongGround_NotBlockedByFloor`가
깨지면 캐칭이 되살아난 것이므로 지면 투영이 제대로 안 들어간 것이다.

- [ ] **Step 7: 내리막을 측정하고, 결과대로 테스트를 추가한다**

스펙에 "재지 않았다"고 남긴 항목이다. 임시 테스트로 내리막 한 구간을 찍어 본다:

```csharp
        [Test]
        public void 내리막_궤적을_찍는다()
        {
            var map = new HalfSpaceQuery();
            map.AddSlope(-32f, Vector3.zero);   // 내리막
            Vector3 pos = new Vector3(-1f, 0.6f, 0f);
            Vector3 vel = new Vector3(ForwardSpeed, 0f, 0f);
            var log = new System.Text.StringBuilder("\n내리막:\n");
            for (int tick = 0; tick < 40; tick++)
            {
                vel.y -= Gravity * DeltaTime;
                var r = KinematicMover.Move(new KinematicMoveInput(pos, vel, Radius, Height, DeltaTime, ~0, 0f), map);
                pos = r.position; vel = r.velocity;
                log.AppendLine($"t{tick} pos({pos.x:F3},{pos.y:F3}) vel({vel.x:F2},{vel.y:F2})");
            }
            Debug.Log(log.ToString());
        }
```

찍어 보고 **면에 붙어 매끄럽게 내려가면** 이 임시 테스트를 지우고 회귀 단언으로 바꾼다
(예: 몸이 경사 안으로 파묻히지 않는다 + 세로 속도가 위를 향하지 않는다).
**계단식으로 튀면** 지우지 말고 그대로 보고한다 — 별도 판단이 필요하다.

- [ ] **Step 8: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/KinematicMover.cs Runtime/Scripts/Game/FlappyWorld.cs \
        Runtime/Scripts/Game/KinematicMoveSystem.cs \
        Tests/EditMode/KinematicMoverTests.cs Tests/EditMode/KinematicMoverSlopeTests.cs
git status --short
git commit -m "fix(kinematic): 지면을 따라 움직이게 하고 통짜 들어올리기를 없앤다

수평 sweep이 캡슐을 0.1m 들어올려 검사하면서 실제 위치는 안 올렸다. 평지에선 티가
안 나지만 오르막에선 그 틈으로 지면이 들어와 매 틱 2.5cm씩 몸이 파묻혔고(실측 2.7cm),
그 복구가 고정 전진 속도 11m/s를 세로 +4.16으로 꺾어 25Hz 떨림이 됐다.

이제 sweep은 실제 몸 자리에서 하고, 지면 위에서는 이동을 지면 평면에 투영해 경사를
따라간다 — 바닥을 정면으로 만나지 않으니 들어올릴 이유가 없다.

턱 오르기는 커널 상수가 아니라 입력 파라미터가 됐고, 막혔을 때만 위-앞-아래 3-sweep으로
시도한다(언리얼 CMC StepUp). Flappy는 0(나는 새에게 계단은 의미 없음), FlapWang은 0.1."
```

---

## Task 4: 라이브 확인 (사람만 할 수 있다)

EditMode로는 덮을 수 없다. `KinematicMover`는 두 게임이 함께 쓰므로 **양쪽 다** 봐야 한다.

- [ ] **Step 1: Flappy Race — 경사에서 떨지 않는지**

`docs/superpowers/specs/2026-08-26-flappy-solid-map-design.md`와 메모리 `local-two-client-test-rig`의 절차로
로컬 리그를 세운다(서버 에디터 환경 `local`, 픽스처 `gameModeId 6 / mapId 2`).

- 경사 구간을 스치며 날 때 새가 떨지 않는지 (이전엔 25Hz, 진폭 5~8cm)
- 클라 콘솔에 `[ReconSpike]`가 70틱 주기로 쏟아지지 않는지
- 지면에 착지·이륙이 예전과 같은지

- [ ] **Step 2: FlapWang — 턱·계단이 예전과 같은지**

`gameModeId 1 / mapId 1`. **이게 이 슬라이스에서 가장 큰 위험이다** — 턱 오르기가
"모든 이동에 0.1m 공짜"에서 "막혔을 때만 시도"로 바뀌었다.

- 계단·턱을 예전처럼 올라가는지
- 경사면에서 걷는 느낌이 달라지지 않았는지
- 벽에 붙어 걸을 때 끼지 않는지

- [ ] **Step 3: 결과를 스펙에 남긴다**

`docs/superpowers/specs/2026-08-27-kinematic-ground-movement-design.md` 끝에 "확인 결과" 절을 붙인다.
열린 항목 O1(`ClearVelocityIntoSurface`의 세로 속도 주입)의 **실제 발동 빈도**를 봤다면 함께 적는다 —
그 값이 O1을 어떻게 정리할지의 근거가 된다.

---

## 이 계획이 다루지 않는 것

- **새끼리 얹혀서 떠는 것** — 원인이 다르다(접촉 임펄스 절반 문제). `ROADMAP.md`의 접촉 반복 해석기 몫.
- **맵의 막다른 쐐기**(x≈132.75) — 아트/레벨 쪽.
- **`ClearVelocityIntoSurface` 통일**(스펙 O1) — 이 슬라이스로 발동 빈도가 확 줄어든 뒤에 판단한다.
