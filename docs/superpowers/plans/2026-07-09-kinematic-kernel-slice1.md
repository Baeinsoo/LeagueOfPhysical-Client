# 공유 키네마틱 커널 (슬라이스 1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** collide-and-slide 캡슐 이동 커널과 그 물리 쿼리 포트를 만든다 — 라이브 틱에 물리지 않는 순수 추가(dormant), EditMode로 완전 검증.

**Architecture:** 물리 충돌 쿼리를 `ICollisionQuery` 포트(GameFramework)로 추상화하고 Unity `Physics.CapsuleCast`로 구현. 그 포트만 호출하는 collide-and-slide 커널 `KinematicMover`(LOP-Shared, 클·서 공유 구체 static 커널)를 만든다. 커널은 스크립트된 가짜 쿼리(`FakeCollisionQuery`)로 씬 없이 순수 로직만 EditMode 테스트한다.

**Tech Stack:** Unity 6, C#, NUnit(EditMode), UnityMCP(컴파일·테스트 실행), VContainer(주입은 슬라이스 2), Git.

## Global Constraints

- **작업 브랜치 = `feature/shared-kinematic-controller`** (이미 spec 커밋됨). **main 직접 커밋 금지.**
- **UnityMCP는 매 호출에 `unity_instance` 명시** — 클라 인스턴스(`mcpforunity://instances`에서 `name == "LeagueOfPhysical-Client"`의 full id `Name@hash`)를 해석해 전달. `set_active_instance`는 이 프로젝트에서 신뢰 불가.
- **`.meta` 파일은 Unity가 생성한 것을 함께 커밋** — 직접 만들지 않는다. 새 `.cs` 작성 후 `refresh_unity`로 Unity가 `.meta`를 생성하게 한 뒤 `.cs`+`.meta`를 같이 add.
- **커널은 라이브 틱에 배선하지 않는다(dormant)** — 이번 슬라이스는 순수 추가. `MovementSystem`/`LOPRunner`/`PhysicsComponent` 미변경.
- **커밋 메시지 끝**: `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`
- 캡슐 규약: `position` = 발밑 기준(콜라이더 center=(0,height/2,0)). radius 0.35, height 1.5 (현 `PhysicsComponent` 값).

> **spec 대비 스코프 정정:** spec 슬라이스 1은 "MovementSystem 중력·grounded"를 포함했으나, `MovementSystem.Tick`은 이미 라이브(LOPWorld가 매 틱 호출)라 중력을 넣으면 PhysX 중력과 이중 적용된다. 따라서 **중력·grounded의 MovementSystem 배선은 슬라이스 2로 이관**(키네마틱 전환과 동시에 PhysX 중력 제거). 슬라이스 1은 순수 커널+포트만 — dormant 유지.

---

## 파일 구조

- **Create** `GameFramework/Runtime/Scripts/Game/CollisionHit.cs` — 충돌 쿼리 결과 값 타입(엔진 `RaycastHit` 격리).
- **Create** `GameFramework/Runtime/Scripts/Game/ICollisionQuery.cs` — 캡슐 sweep 쿼리 포트.
- **Create** `GameFramework/Runtime/Scripts/Game/UnityCollisionQuery.cs` — `Physics.CapsuleCast` 어댑터.
- **Create** `LeagueOfPhysical-Shared/Runtime/Scripts/Game/KinematicMover.cs` — collide-and-slide 공유 커널 + 입출력 struct.
- **Create** `LeagueOfPhysical-Shared/Tests/EditMode/KinematicMoverTests.cs` — `FakeCollisionQuery` + 커널 EditMode 테스트.

asmdef 변경 없음: `ICollisionQuery`는 `baegames.GameFramework.Runtime`(LOP.Shared.Runtime가 이미 참조), 커널은 `baegames.LOP.Shared.Runtime`, 테스트는 `baegames.LOP.Shared.Tests.EditMode`(Runtime+GameFramework 이미 참조).

---

## Task 1: 충돌 쿼리 포트 + Unity 어댑터 (GameFramework)

물리 쿼리 seam. Unity `Physics.CapsuleCast`를 얇게 감싸 포트 뒤로 격리한다. 씬이 필요한 어댑터라 EditMode 단위 테스트는 없고 **컴파일 클린**으로 검증(커널 로직은 Task 2에서 가짜 쿼리로 검증, 실제 지오메트리는 슬라이스 2 플레이로 검증).

**Files:**
- Create: `GameFramework/Runtime/Scripts/Game/CollisionHit.cs`
- Create: `GameFramework/Runtime/Scripts/Game/ICollisionQuery.cs`
- Create: `GameFramework/Runtime/Scripts/Game/UnityCollisionQuery.cs`

**Interfaces:**
- Consumes: 없음 (UnityEngine.Physics만).
- Produces: `GameFramework.CollisionHit`(struct: `bool HasHit`, `float Distance`, `Vector3 Normal`, `Vector3 Point`, `static CollisionHit None`), `GameFramework.ICollisionQuery.CapsuleCast(Vector3 point1, Vector3 point2, float radius, Vector3 direction, float distance, int layerMask) → CollisionHit`, `GameFramework.UnityCollisionQuery : ICollisionQuery`.

- [ ] **Step 1: `CollisionHit.cs` 작성**

```csharp
using UnityEngine;

namespace GameFramework
{
    /// <summary>충돌 쿼리 결과(엔진 RaycastHit을 포트 경계에서 격리한 얇은 값 타입).</summary>
    public readonly struct CollisionHit
    {
        public readonly bool HasHit;
        public readonly float Distance;
        public readonly Vector3 Normal;
        public readonly Vector3 Point;

        public CollisionHit(bool hasHit, float distance, Vector3 normal, Vector3 point)
        {
            HasHit = hasHit;
            Distance = distance;
            Normal = normal;
            Point = point;
        }

        public static CollisionHit None => new CollisionHit(false, 0f, Vector3.zero, Vector3.zero);
    }
}
```

- [ ] **Step 2: `ICollisionQuery.cs` 작성**

```csharp
using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 물리 충돌 쿼리 포트. 캡슐을 쓸어(sweep) 첫 충돌을 찾는다. 엔진 물리(PhysX)에 직결되지
    /// 않도록 주입한다. <see cref="IPhysicsSimulator"/>(스텝 구동)와 짝을 이루는 쿼리 추상.
    /// 클·서 양쪽 동일 구체(<see cref="UnityCollisionQuery"/>)를 사용한다.
    /// </summary>
    public interface ICollisionQuery
    {
        /// <summary>
        /// 캡슐(양 끝 구 중심 <paramref name="point1"/>·<paramref name="point2"/>, 반지름
        /// <paramref name="radius"/>)을 <paramref name="direction"/> 방향으로
        /// <paramref name="distance"/>만큼 쓸어 첫 충돌을 반환한다. 없으면 <see cref="CollisionHit.None"/>.
        /// </summary>
        CollisionHit CapsuleCast(Vector3 point1, Vector3 point2, float radius,
            Vector3 direction, float distance, int layerMask);
    }
}
```

- [ ] **Step 3: `UnityCollisionQuery.cs` 작성**

```csharp
using UnityEngine;

namespace GameFramework
{
    /// <summary>Unity 내장 물리(PhysX)로 <see cref="ICollisionQuery"/>를 구현하는 어댑터.</summary>
    public sealed class UnityCollisionQuery : ICollisionQuery
    {
        public CollisionHit CapsuleCast(Vector3 point1, Vector3 point2, float radius,
            Vector3 direction, float distance, int layerMask)
        {
            if (Physics.CapsuleCast(point1, point2, radius, direction, out RaycastHit hit, distance, layerMask))
            {
                return new CollisionHit(true, hit.distance, hit.normal, hit.point);
            }
            return CollisionHit.None;
        }
    }
}
```

- [ ] **Step 4: Unity 리임포트 + 컴파일 확인**

클라 인스턴스 id를 해석(`mcpforunity://instances`)한 뒤:
- `refresh_unity(unity_instance="LeagueOfPhysical-Client@<hash>")` — `.meta` 생성 + 컴파일.
- `read_console(unity_instance="LeagueOfPhysical-Client@<hash>", types=["error"])`.

Expected: 신규 에러 0. 3개 `.cs`에 대응하는 `.meta` 생성됨.

- [ ] **Step 5: 커밋**

```bash
git add GameFramework/Runtime/Scripts/Game/CollisionHit.cs \
        GameFramework/Runtime/Scripts/Game/CollisionHit.cs.meta \
        GameFramework/Runtime/Scripts/Game/ICollisionQuery.cs \
        GameFramework/Runtime/Scripts/Game/ICollisionQuery.cs.meta \
        GameFramework/Runtime/Scripts/Game/UnityCollisionQuery.cs \
        GameFramework/Runtime/Scripts/Game/UnityCollisionQuery.cs.meta
git commit -m "feat(physics): ICollisionQuery 포트 + UnityCollisionQuery 어댑터

캡슐 sweep 충돌 쿼리 추상(GameFramework). CollisionHit로 RaycastHit 격리.
IPhysicsSimulator(스텝)와 짝. 커널(KinematicMover)이 소비. dormant.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

> **주의:** GameFramework는 클·서 공유 `file:` 패키지다. 이 커밋은 `C:/Users/re5na/workspace/LOP/GameFramework` 저장소에서 이뤄진다(클라 프로젝트가 아님). 해당 폴더로 이동해 커밋할 것.

---

## Task 2: collide-and-slide 커널 `KinematicMover` (LOP-Shared) — TDD

속도를 캡슐 sweep으로 "벽까지만 이동 + 미끄러짐" 처리해 최종 위치·속도·grounded를 낸다. `FakeCollisionQuery`로 씬 없이 순수 로직만 검증. 테스트를 하나씩 추가하며 구현을 점진 일반화(TDD).

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/KinematicMover.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/KinematicMoverTests.cs`

**Interfaces:**
- Consumes: `GameFramework.ICollisionQuery`, `GameFramework.CollisionHit` (Task 1).
- Produces: `LOP.KinematicMoveInput`(struct: `Vector3 position, Vector3 velocity, float radius, float height, float deltaTime, int layerMask`), `LOP.KinematicMoveResult`(struct: `Vector3 position, Vector3 velocity, bool grounded`), `LOP.KinematicMover.Move(in KinematicMoveInput input, ICollisionQuery query) → KinematicMoveResult` (정적).

- [ ] **Step 1: 테스트 파일 + 첫 테스트(NoHit) 작성**

`LeagueOfPhysical-Shared/Tests/EditMode/KinematicMoverTests.cs`:

```csharp
using System.Collections.Generic;
using GameFramework;
using LOP;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class KinematicMoverTests
    {
        const float Tolerance = 1e-3f;

        // 스크립트된 충돌 응답을 순서대로 돌려주는 테스트용 쿼리(씬 없이 collide-and-slide 로직만 검증).
        private class FakeCollisionQuery : ICollisionQuery
        {
            public readonly Queue<CollisionHit> Responses = new Queue<CollisionHit>();
            public int CallCount;

            public CollisionHit CapsuleCast(Vector3 point1, Vector3 point2, float radius,
                Vector3 direction, float distance, int layerMask)
            {
                CallCount++;
                return Responses.Count > 0 ? Responses.Dequeue() : CollisionHit.None;
            }
        }

        private static KinematicMoveInput Input(Vector3 pos, Vector3 vel, float dt = 0.1f)
            => new KinematicMoveInput(pos, vel, 0.35f, 1.5f, dt, ~0);

        [Test]
        public void NoHit_MovesFullDelta()
        {
            var query = new FakeCollisionQuery();   // 응답 없음 → 항상 None
            var r = KinematicMover.Move(Input(Vector3.zero, new Vector3(10f, 0f, 0f)), query);

            // delta = velocity*dt = (1,0,0)
            Assert.That(r.position.x, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(r.position.y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(r.position.z, Is.EqualTo(0f).Within(Tolerance));
            Assert.IsFalse(r.grounded);
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인 (커널 없음)**

클라 인스턴스 id 해석 후:
`run_tests(unity_instance="LeagueOfPhysical-Client@<hash>", mode="EditMode", testFilter="LOP.Tests.KinematicMoverTests")`
Expected: 컴파일 실패 — `KinematicMover`/`KinematicMoveInput` 미정의.

- [ ] **Step 3: `KinematicMover.cs` 최소 구현(전진만)**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/KinematicMover.cs`:

```csharp
using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>이동 커널 입력: 시작 위치·속도·캡슐 규격·dt·충돌 레이어.</summary>
    public readonly struct KinematicMoveInput
    {
        public readonly Vector3 position;   // 발밑 기준
        public readonly Vector3 velocity;
        public readonly float radius;
        public readonly float height;
        public readonly float deltaTime;
        public readonly int layerMask;

        public KinematicMoveInput(Vector3 position, Vector3 velocity, float radius,
            float height, float deltaTime, int layerMask)
        {
            this.position = position;
            this.velocity = velocity;
            this.radius = radius;
            this.height = height;
            this.deltaTime = deltaTime;
            this.layerMask = layerMask;
        }
    }

    /// <summary>이동 커널 결과: 최종 위치·(충돌 반영) 속도·바닥 접지 여부.</summary>
    public readonly struct KinematicMoveResult
    {
        public readonly Vector3 position;
        public readonly Vector3 velocity;
        public readonly bool grounded;

        public KinematicMoveResult(Vector3 position, Vector3 velocity, bool grounded)
        {
            this.position = position;
            this.velocity = velocity;
            this.grounded = grounded;
        }
    }

    /// <summary>
    /// 속도를 캡슐 sweep으로 "벽까지만 이동 + 미끄러짐"(collide-and-slide) 처리해 최종 위치를 낸다.
    /// 클·서 공유 구체 커널(같은 코드 = 예측이 권위와 일치). 물리 쿼리는 ICollisionQuery 포트 뒤로 격리.
    /// </summary>
    public static class KinematicMover
    {
        public static KinematicMoveResult Move(in KinematicMoveInput input, ICollisionQuery query)
        {
            Vector3 remaining = input.velocity * input.deltaTime;
            return new KinematicMoveResult(input.position + remaining, input.velocity, false);
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

`run_tests(unity_instance="LeagueOfPhysical-Client@<hash>", mode="EditMode", testFilter="LOP.Tests.KinematicMoverTests")`
Expected: `NoHit_MovesFullDelta` PASS.

- [ ] **Step 5: 커밋**

```bash
# LeagueOfPhysical-Shared 저장소에서:
git add Runtime/Scripts/Game/KinematicMover.cs Runtime/Scripts/Game/KinematicMover.cs.meta \
        Tests/EditMode/KinematicMoverTests.cs Tests/EditMode/KinematicMoverTests.cs.meta
git commit -m "feat(movement): KinematicMover 골격 — 무충돌 전진 + NoHit 테스트

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 6: 두 번째 테스트(정면 벽 정지) 추가**

`KinematicMoverTests` 클래스에 추가:

```csharp
        [Test]
        public void HeadOnWall_StopsAndZeroesVelocityAlongNormal()
        {
            var query = new FakeCollisionQuery();
            // 정면 벽: 거리 0.5에서 법선이 이동 반대(-x)
            query.Responses.Enqueue(new CollisionHit(true, 0.5f, new Vector3(-1f, 0f, 0f), Vector3.zero));
            var r = KinematicMover.Move(Input(Vector3.zero, new Vector3(10f, 0f, 0f)), query);

            // 접촉 전까지만(≈0.48, skin 여유), 목표(1.0)까지 안 감
            Assert.That(r.position.x, Is.GreaterThan(0.4f));
            Assert.That(r.position.x, Is.LessThan(0.6f));
            // 벽 법선 방향 속도 소멸
            Assert.That(r.velocity.x, Is.EqualTo(0f).Within(Tolerance));
        }
```

- [ ] **Step 7: 실패 확인**

`run_tests(...testFilter="LOP.Tests.KinematicMoverTests")`
Expected: `HeadOnWall_...` FAIL (현재 구현은 쿼리 무시 → x=1.0, velocity.x=10).

- [ ] **Step 8: 충돌 정지 구현(임시)**

`KinematicMover.Move`를 교체:

```csharp
        const float SkinWidth = 0.02f;   // 벽에서 살짝 띄우는 여유(끼임 방지)

        public static KinematicMoveResult Move(in KinematicMoveInput input, ICollisionQuery query)
        {
            Vector3 pos = input.position;
            Vector3 remaining = input.velocity * input.deltaTime;
            Vector3 velocity = input.velocity;

            float dist = remaining.magnitude;
            if (dist > 1e-5f)
            {
                Vector3 dir = remaining / dist;
                Vector3 p1 = pos + Vector3.up * input.radius;
                Vector3 p2 = pos + Vector3.up * (input.height - input.radius);
                CollisionHit hit = query.CapsuleCast(p1, p2, input.radius, dir, dist + SkinWidth, input.layerMask);
                if (hit.HasHit)
                {
                    float moveDist = Mathf.Max(hit.Distance - SkinWidth, 0f);
                    pos += dir * moveDist;
                    velocity = Vector3.zero;   // 임시: 충돌 시 정지 (다음 테스트에서 일반화)
                }
                else
                {
                    pos += remaining;
                }
            }
            return new KinematicMoveResult(pos, velocity, false);
        }
```

- [ ] **Step 9: 통과 확인 (1·2 모두)**

`run_tests(...testFilter="LOP.Tests.KinematicMoverTests")`
Expected: `NoHit_...`, `HeadOnWall_...` 둘 다 PASS.

- [ ] **Step 10: 커밋**

```bash
git add Runtime/Scripts/Game/KinematicMover.cs Tests/EditMode/KinematicMoverTests.cs
git commit -m "feat(movement): 충돌 시 접촉 지점 정지 + HeadOnWall 테스트

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 11: 세 번째 테스트(각진 벽 미끄러짐) 추가**

```csharp
        [Test]
        public void AngledWall_SlidesAlong_RedirectsVelocity()
        {
            var query = new FakeCollisionQuery();
            // 45도 벽: 법선=(-0.7071,0,-0.7071). 접촉 후 남은 이동이 벽면을 따라 미끄러짐.
            query.Responses.Enqueue(new CollisionHit(true, 0.3f,
                new Vector3(-0.7071f, 0f, -0.7071f), Vector3.zero));
            // 두 번째 sweep은 열림(None) → 미끄러진 나머지를 그대로 이동
            var r = KinematicMover.Move(Input(Vector3.zero, new Vector3(10f, 0f, 0f)), query);

            // x로 진행하다 벽을 만나 z로도 미끄러짐(수직축 이동 발생)
            Assert.That(r.position.z, Is.LessThan(-0.01f), "벽면을 따라 옆으로 미끄러져야 함");
            Assert.That(r.position.x, Is.GreaterThan(0.01f));
            // 정면 벽처럼 소멸이 아니라 벽면 방향으로 꺾임(속도 크기 유지)
            Assert.That(new Vector3(r.velocity.x, 0f, r.velocity.z).magnitude, Is.GreaterThan(0.1f),
                "각진 벽에서는 속도가 소멸이 아니라 재방향되어야 함");
        }
```

- [ ] **Step 12: 실패 확인**

`run_tests(...testFilter="LOP.Tests.KinematicMoverTests")`
Expected: `AngledWall_...` FAIL (임시 구현은 velocity=0·1회 정지 → z=0, 속도 소멸).

- [ ] **Step 13: 반복 collide-and-slide로 일반화**

`KinematicMover.Move`를 교체(상수 추가):

```csharp
        const int MaxSlides = 4;         // 미끄러짐 반복 상한(과회전·무한루프 방지)
        const float SkinWidth = 0.02f;   // 벽에서 살짝 띄우는 여유(끼임 방지)

        public static KinematicMoveResult Move(in KinematicMoveInput input, ICollisionQuery query)
        {
            Vector3 pos = input.position;
            Vector3 remaining = input.velocity * input.deltaTime;
            Vector3 velocity = input.velocity;

            for (int i = 0; i < MaxSlides; i++)
            {
                float dist = remaining.magnitude;
                if (dist < 1e-5f)
                {
                    break;
                }
                Vector3 dir = remaining / dist;

                Vector3 p1 = pos + Vector3.up * input.radius;
                Vector3 p2 = pos + Vector3.up * (input.height - input.radius);
                CollisionHit hit = query.CapsuleCast(p1, p2, input.radius, dir, dist + SkinWidth, input.layerMask);
                if (hit.HasHit == false)
                {
                    pos += remaining;
                    break;
                }

                float moveDist = Mathf.Max(hit.Distance - SkinWidth, 0f);
                pos += dir * moveDist;

                // 남은 이동과 속도를 충돌면(plane)에 투영 → 벽을 따라 미끄러짐. 정면 벽이면 0으로 소멸.
                Vector3 leftover = remaining - dir * moveDist;
                remaining = Vector3.ProjectOnPlane(leftover, hit.Normal);
                velocity = Vector3.ProjectOnPlane(velocity, hit.Normal);
            }

            return new KinematicMoveResult(pos, velocity, false);
        }
```

- [ ] **Step 14: 통과 확인 (1·2·3)**

`run_tests(...testFilter="LOP.Tests.KinematicMoverTests")`
Expected: 3개 모두 PASS (`HeadOnWall`은 정면이라 투영이 0으로 소멸 → 여전히 정지·속도0).

- [ ] **Step 15: 커밋**

```bash
git add Runtime/Scripts/Game/KinematicMover.cs Tests/EditMode/KinematicMoverTests.cs
git commit -m "feat(movement): 반복 collide-and-slide(벽면 투영 미끄러짐) + AngledWall 테스트

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 16: 네 번째 테스트(바닥 접지·수직속도 소멸) 추가**

```csharp
        [Test]
        public void GroundHit_SetsGrounded_AndZeroesVerticalVelocity()
        {
            var query = new FakeCollisionQuery();
            // 아래로 낙하 중 바닥(법선 위) 접촉
            query.Responses.Enqueue(new CollisionHit(true, 0.1f, new Vector3(0f, 1f, 0f), Vector3.zero));
            var r = KinematicMover.Move(Input(Vector3.zero, new Vector3(0f, -20f, 0f)), query);

            Assert.IsTrue(r.grounded, "바닥 법선(위쪽) 접촉 시 grounded");
            Assert.That(r.velocity.y, Is.EqualTo(0f).Within(Tolerance), "바닥에 닿으면 수직 속도 소멸");
        }
```

- [ ] **Step 17: 실패 확인**

`run_tests(...testFilter="LOP.Tests.KinematicMoverTests")`
Expected: `GroundHit_...` FAIL (`grounded`가 항상 false).

- [ ] **Step 18: 바닥 감지 추가**

`KinematicMover`에 상수 추가하고 `Move` 안에 grounded 처리 삽입:

```csharp
        const float GroundNormalY = 0.7f;  // 면 법선의 위쪽 성분이 이보다 크면 바닥(≈45도)
```

`Move` 시그니처·루프를 아래로 교체(grounded 변수 + 감지 라인 추가):

```csharp
        public static KinematicMoveResult Move(in KinematicMoveInput input, ICollisionQuery query)
        {
            Vector3 pos = input.position;
            Vector3 remaining = input.velocity * input.deltaTime;
            Vector3 velocity = input.velocity;
            bool grounded = false;

            for (int i = 0; i < MaxSlides; i++)
            {
                float dist = remaining.magnitude;
                if (dist < 1e-5f)
                {
                    break;
                }
                Vector3 dir = remaining / dist;

                Vector3 p1 = pos + Vector3.up * input.radius;
                Vector3 p2 = pos + Vector3.up * (input.height - input.radius);
                CollisionHit hit = query.CapsuleCast(p1, p2, input.radius, dir, dist + SkinWidth, input.layerMask);
                if (hit.HasHit == false)
                {
                    pos += remaining;
                    break;
                }

                float moveDist = Mathf.Max(hit.Distance - SkinWidth, 0f);
                pos += dir * moveDist;

                if (hit.Normal.y >= GroundNormalY)
                {
                    grounded = true;
                }

                Vector3 leftover = remaining - dir * moveDist;
                remaining = Vector3.ProjectOnPlane(leftover, hit.Normal);
                velocity = Vector3.ProjectOnPlane(velocity, hit.Normal);
            }

            return new KinematicMoveResult(pos, velocity, grounded);
        }
```

- [ ] **Step 19: 통과 확인 (1~4)**

`run_tests(...testFilter="LOP.Tests.KinematicMoverTests")`
Expected: 4개 모두 PASS.

- [ ] **Step 20: 커밋**

```bash
git add Runtime/Scripts/Game/KinematicMover.cs Tests/EditMode/KinematicMoverTests.cs
git commit -m "feat(movement): 바닥 법선 감지 grounded + 수직속도 소멸 + GroundHit 테스트

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 21: 다섯 번째 테스트(무한루프 방지) 추가**

```csharp
        [Test]
        public void AlwaysBlocked_TerminatesWithinMaxSlides()
        {
            var query = new FakeCollisionQuery();
            // 매 sweep마다 같은 각진 벽 → 잔여가 계속 남아도 상한(MaxSlides) 내에서 종료해야 함
            for (int i = 0; i < 20; i++)
            {
                query.Responses.Enqueue(new CollisionHit(true, 0.1f,
                    new Vector3(-0.7071f, 0f, -0.7071f), Vector3.zero));
            }
            var r = KinematicMover.Move(Input(Vector3.zero, new Vector3(10f, 0f, 0f)), query);

            Assert.That(query.CallCount, Is.LessThanOrEqualTo(4), "MaxSlides 상한 내에서 종료(무한루프 방지)");
        }
```

- [ ] **Step 22: 통과 확인 (특성 테스트 — 이미 통과)**

`run_tests(...testFilter="LOP.Tests.KinematicMoverTests")`
Expected: 5개 모두 PASS (`MaxSlides` 루프 상한이 종료 보장 — 이 테스트는 그 불변식을 고정).

- [ ] **Step 23: 커밋**

```bash
git add Tests/EditMode/KinematicMoverTests.cs
git commit -m "test(movement): MaxSlides 종료 보장(무한루프 방지) 특성 테스트

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Self-Review

**1. Spec coverage (슬라이스 1 범위):**
- `ICollisionQuery` 포트 + `UnityCollisionQuery`(양쪽) → Task 1. ✓ (양쪽 = GameFramework 공유 패키지 단일 구현, 클·서가 각자 등록 — 등록은 슬라이스 2.)
- collide-and-slide 공유 커널 → Task 2. ✓
- EditMode 테스트(sweep/미끄러짐/바닥) → Task 2 (5 테스트). ✓
- MovementSystem 중력·grounded → **슬라이스 2로 이관**(dormant 위배 방지, Global Constraints에 명시). 슬라이스 1 범위 아님. ✓ (spec 대비 의도적 정정, 문서화됨.)
- dormant(라이브 미배선) → Task 1·2 모두 미배선. ✓

**2. Placeholder scan:** 모든 스텝에 실제 코드·명령·기대출력 포함. "TBD"/"적절히"/"등등" 없음. ✓

**3. Type consistency:**
- `CollisionHit(bool, float, Vector3, Vector3)` — Task 1 정의, Task 2 테스트에서 동일 시그니처로 생성. ✓
- `ICollisionQuery.CapsuleCast(Vector3, Vector3, float, Vector3, float, int)` — Task 1 정의, `FakeCollisionQuery`/`UnityCollisionQuery`/커널 호출 모두 동일. ✓
- `KinematicMoveInput(Vector3, Vector3, float, float, float, int)` / `KinematicMoveResult(Vector3, Vector3, bool)` — Task 2 정의, 테스트 `Input(...)` 헬퍼·assert와 일치. ✓
- 상수 `SkinWidth`/`MaxSlides`/`GroundNormalY` — Step 8/13/18에서 점진 도입, 최종본에 모두 존재. ✓

이상 없음.

---

## 다음 (슬라이스 2 예고 — 이 plan 범위 아님)

커널이 검증되면 슬라이스 2에서: 캐릭터 `PhysicsComponent.Initialize(true,false)`(kinematic) + `MovementSystem`에 중력·grounded + `KinematicMoveSystem`(`ICollisionQuery` 주입, 커널 호출)을 서버 틱에 배선 + PhysX 캐릭터 중력 제거. 별도 plan.
