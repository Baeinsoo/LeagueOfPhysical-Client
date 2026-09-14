using GameFramework.Physics;
using NUnit.Framework;
using UnityEngine;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// 탐색의 "부딪혔나"가 진짜 이동 커널과 <b>같은 자</b>인지를 지킨다. 예전에는 도착점 하나를
    /// 0.1m 눈금에 붙여 정지 캡슐로 찍었는데, 커널은 캡슐을 한 틱 쓸고 벽에서 0.02m를 띄운다 —
    /// 그래서 탐색이 커널보다 관대했고, 탐색이 찾은 경로가 재생에서 벽에 걸렸다.
    /// </summary>
    public class FlappyTickSweepTests
    {
        const float ForwardSpeed = 11f;
        const float FlapImpulse = 23f;
        const float Gravity = 70f;
        const float MaxFallSpeed = 30f;
        const float Radius = 0.45f;
        const float Height = 0.9f;
        const float Tick = 0.02f;
        const int Mask = ~0;

        static FlappyTickSweep SweepOver(ICollisionQuery world)
            => new FlappyTickSweep(world, ForwardSpeed, Radius, Height, Tick, Mask);

        //  커널을 <b>테스트가 직접</b> 한 번 더 부른다. FlappyTickSweep이 커널에 넣는 값(몸 규격·
        //  전진 속도·dt·stepOffset 0·groundProbe 0) 중 하나라도 달라지면 두 답이 갈린다.
        static bool KernelSaysFree(ICollisionQuery world, float x, float y, float vy, out Vector3 end)
        {
            var watcher = new CountingQuery(world);
            var result = KinematicMover.Move(new KinematicMoveInput(
                new Vector3(x, y, 0f), new Vector3(ForwardSpeed, vy, 0f),
                Radius, Height, Tick, Mask, stepOffset: 0f, groundProbe: 0f), watcher);
            end = result.position;
            return watcher.Hits == 0;
        }

        //  몸이 통째로 회랑 안에 들어가나 — 예전 탐색이 도착점에서 하던 정지 점 검사와 같다.
        static bool PointSaysFree(CorridorWorld world, float y)
            => y > world.FloorY && y + Height < world.CeilingY;

        static float[] SampleSpeeds()
            => new[] { FlapImpulse, 10f, 4.012001f, 0f, -1.4f, -5f, -15f, -MaxFallSpeed };

        [Test]
        public void 한_틱_쓸기의_닿음_판정은_진짜_이동_커널과_같다()
        {
            //  <b>이 파일에서 가장 중요한 테스트다.</b> 바닥과 천장 사이를 0.01m 간격으로 훑어
            //  스치는 경계 양쪽을 모두 지난다.
            var world = new CorridorWorld(floorY: -1f, ceilingY: 3f);
            var sweep = SweepOver(world);
            int free = 0, blocked = 0;

            for (float y = -1.2f; y <= 2.4f; y += 0.01f)
            {
                foreach (float vy in SampleSpeeds())
                {
                    bool mine = sweep.IsFree(0f, y, vy);
                    bool kernel = KernelSaysFree(world, 0f, y, vy, out _);
                    Assert.AreEqual(kernel, mine, $"y={y:F3} vy={vy:F3} — 쓸기와 커널이 갈렸다");
                    if (mine) { free++; } else { blocked++; }
                }
            }

            //  둘 다 안 나오면 위 단언이 아무것도 안 지킨다(전부 free거나 전부 blocked면 통과).
            Assert.Greater(free, 0, "자유로운 경우가 하나도 없다 — 비교가 공허하다");
            Assert.Greater(blocked, 0, "막힌 경우가 하나도 없다 — 비교가 공허하다");
        }

        [Test]
        public void 쓸기는_점_검사보다_관대하지_않다()
        {
            //  이 과제가 고친 것의 방향을 못박는다. 예전 모델(도착점 점 검사)이 자유라고 한
            //  자리 중 일부는 커널이 막는다 — 커널은 한 틱을 쓸고 벽에서 0.02m를 띄우기 때문.
            //  반대(쓸기는 자유인데 점 검사는 막힘)는 <b>한 건도 없어야</b> 한다. 있으면 탐색이
            //  이번엔 커널보다 엄해진 것이고, 그것도 똑같이 결함이다.
            var world = new CorridorWorld(floorY: -1f, ceilingY: 3f);
            var sweep = SweepOver(world);
            int stricterThanPoint = 0;

            for (float y = -1.2f; y <= 2.4f; y += 0.005f)
            {
                foreach (float vy in SampleSpeeds())
                {
                    float ny = FlappyTickMath.AdvanceHeight(y, vy, Tick);
                    bool point = PointSaysFree(world, ny);
                    bool swept = sweep.IsFree(0f, y, vy);

                    Assert.IsFalse(swept && point == false,
                        $"y={y:F3} vy={vy:F3} — 쓸기만 자유라고 했다(탐색이 커널보다 엄해졌다)");
                    if (point && swept == false) { stricterThanPoint++; }
                }
            }

            Assert.Greater(stricterThanPoint, 0,
                "점 검사가 자유라 한 자리를 쓸기가 막은 경우가 하나도 없다 — 두 모델이 같다는 뜻이라 "
                + "이 과제가 고친 차이가 사라졌다");
        }

        [Test]
        public void 닿지_않았으면_커널이_낸_높이가_탐색이_믿는_높이와_정확히_같다()
        {
            //  오차 허용 0. 여기가 갈리면 탐색이 찾은 경로를 재생했을 때 다른 자리에 있게 된다.
            var world = new CorridorWorld(floorY: -50f, ceilingY: 50f);
            var sweep = SweepOver(world);
            int count = 0;

            for (float y = -10f; y <= 10f; y += 0.37f)
            {
                foreach (float vy in SampleSpeeds())
                {
                    Assert.IsTrue(sweep.IsFree(0f, y, vy, out Vector3 end), "빈 하늘인데 막혔다");
                    Assert.AreEqual(FlappyTickMath.AdvanceHeight(y, vy, Tick), end.y, 0f,
                                    $"y={y:F3} vy={vy:F3}");
                    Assert.AreEqual(ForwardSpeed * Tick, end.x, 1e-6f);
                    count++;
                }
            }
            Assert.Greater(count, 100);
        }

        [Test]
        public void 쓸기로_찾은_경로는_진짜_커널로_재생해도_한_번도_안_닿는다()
        {
            //  합성 회랑에서 탐색을 돌리고, 나온 날갯짓 순서를 <b>커널로</b> 그대로 굴린다.
            //  높이도 같고 충돌 판정도 같으면 정의상 안 어긋난다 — 어긋나면 세 번째 차이가
            //  있다는 뜻이고, 이 테스트가 그것을 잡는다.
            //  회랑 높이는 임의로 고른 값이 아니다: 한 번 날갯짓하면 23²/(2×70)≈3.78m를 솟고
            //  그 사이엔 멈출 수가 없으므로(날갯짓은 세로 속도를 23으로 덮어쓴다), 그보다
            //  좁으면 애초에 어떤 순서로도 못 난다.
            var world = new CorridorWorld(floorY: -1f, ceilingY: 6f);
            var sweep = SweepOver(world);
            var options = new CleanRunOptions(startX: 0f, startY: 0f, finishX: 20f,
                                              minY: -5f, maxY: 10f,
                                              forwardSpeed: ForwardSpeed, flapImpulse: FlapImpulse,
                                              gravity: Gravity, maxFallSpeed: MaxFallSpeed,
                                              tickSeconds: Tick, heightGrid: 0.1f);

            CleanRunResult result = CleanRunSearch.Run(options, (x, y) => PointSaysFree(world, y), sweep.Probe);

            Assert.IsTrue(result.Reachable, "날갯짓 아치가 들어가는 회랑인데 못 찾았다");
            Assert.Greater(result.Flaps.Count, 80);

            float[] heights = CleanRunSearch.PathHeights(options, result.Flaps);
            var position = new Vector3(options.StartX, options.StartY, 0f);
            float verticalSpeed = 0f;
            for (int i = 0; i < result.Flaps.Count; i++)
            {
                verticalSpeed = FlappyTickMath.NextVerticalSpeed(
                    verticalSpeed, result.Flaps[i], FlapImpulse, Gravity, MaxFallSpeed, Tick);
                Assert.IsTrue(KernelSaysFree(world, position.x, position.y, verticalSpeed, out Vector3 end),
                              $"{i + 1}번째 틱에 커널이 닿았다 (x={position.x:F2} y={position.y:F2})");
                position = end;
                //  재생 높이가 탐색이 믿은 높이와 한 자리도 안 갈려야 한다.
                Assert.AreEqual(heights[i + 1], position.y, 0f, $"{i + 1}번째 틱의 높이가 갈렸다");
            }
        }

        /// <summary>바닥과 천장이 무한히 뻗은 회랑. 캡슐 sweep을 해석적으로 푼다.</summary>
        sealed class CorridorWorld : ICollisionQuery
        {
            public readonly float FloorY;
            public readonly float CeilingY;

            public CorridorWorld(float floorY, float ceilingY)
            {
                FloorY = floorY;
                CeilingY = ceilingY;
            }

            public CollisionHit CapsuleCast(Vector3 point1, Vector3 point2, float radius,
                                            Vector3 direction, float distance, int layerMask)
            {
                float bottom = Mathf.Min(point1.y, point2.y) - radius;
                float top = Mathf.Max(point1.y, point2.y) + radius;

                if (direction.y < -0.5f)
                {
                    float gap = bottom - FloorY;
                    return gap <= distance
                        ? new CollisionHit(true, Mathf.Max(gap, 0f), Vector3.up, Vector3.zero, null)
                        : CollisionHit.None;
                }
                if (direction.y > 0.5f)
                {
                    float gap = CeilingY - top;
                    return gap <= distance
                        ? new CollisionHit(true, Mathf.Max(gap, 0f), Vector3.down, Vector3.zero, null)
                        : CollisionHit.None;
                }
                //  가로로 갈 때는 막는 것이 없다 — 다만 이미 바닥/천장에 파묻혀 있으면
                //  물리엔진이 그러듯 거리 0의 접촉으로 답한다.
                if (bottom < FloorY)
                {
                    return new CollisionHit(true, 0f, Vector3.up, Vector3.zero, null);
                }
                if (top > CeilingY)
                {
                    return new CollisionHit(true, 0f, Vector3.down, Vector3.zero, null);
                }
                return CollisionHit.None;
            }

            public CollisionHit Raycast(Vector3 origin, Vector3 direction, float distance, int layerMask)
                => CollisionHit.None;

            public CollisionHit[] OverlapSphere(Vector3 center, float radius, int layerMask)
                => System.Array.Empty<CollisionHit>();
        }

        /// <summary>몇 번 닿았는지 세는 포트 덮개(테스트가 커널을 직접 부를 때 쓴다).</summary>
        sealed class CountingQuery : ICollisionQuery
        {
            readonly ICollisionQuery inner;
            public int Hits { get; private set; }

            public CountingQuery(ICollisionQuery inner) => this.inner = inner;

            public CollisionHit CapsuleCast(Vector3 point1, Vector3 point2, float radius,
                                            Vector3 direction, float distance, int layerMask)
            {
                CollisionHit hit = inner.CapsuleCast(point1, point2, radius, direction, distance, layerMask);
                if (hit.HasHit) { Hits++; }
                return hit;
            }

            public CollisionHit Raycast(Vector3 origin, Vector3 direction, float distance, int layerMask)
                => inner.Raycast(origin, direction, distance, layerMask);

            public CollisionHit[] OverlapSphere(Vector3 center, float radius, int layerMask)
                => inner.OverlapSphere(center, radius, layerMask);
        }
    }
}
