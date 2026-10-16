using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 적중 사건(무리·표적 번호, 맞은 틱) → 맞은 표적과 자리. 엉뚱한 표적을 고르면 "OO +3"과 깨지는 연출이
    /// 빈 하늘에 뜬다. 옛 서버가 보내는 0 번호(hitTick 0)는 찾지 않아야 한다.
    /// </summary>
    public class ArcheryCatapultHitsTests
    {
        private const float Dt = 0.02f;
        private const long StartTick = 1000;

        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned) { Object.DestroyImmediate(go); }
            spawned.Clear();
        }

        //  레인 0 하나: 원점에서 +z를 본다(Shared의 투석기 코스 시험과 같은 모양).
        private ArcheryRangeLayout OneLane()
        {
            var root = new GameObject("lane0");
            spawned.Add(root);
            var lane = root.AddComponent<ArcheryLane>();
            lane.Order = 0;
            var stand = new GameObject("stand0");
            stand.transform.SetParent(root.transform);
            stand.transform.localPosition = new Vector3(0f, 1.3f, 12f);
            lane.Stands = new[] { stand.transform };
            return ArcheryRangeLayout.From(new[] { lane });
        }

        //  spec §2.3 숫자(가중치 ×10). 함정까지 넣어 무리마다 표적 수가 2~3개로 갈리게 한다.
        private static ArcheryConfig Config(ArcheryCourseKind courseKind)
        {
            var kinds = new[]
            {
                Kind(8, 1.2f, 1, 50, false, 30f, 38f, 2.0f, 3.0f, 0),
                Kind(9, 0.9f, 2, 40, false, 38f, 46f, 3.5f, 5.0f, 0),
                Kind(10, 0.6f, 3, 30, false, 46f, 55f, 5.5f, 7.0f, 0),
                Kind(11, 0.5f, 5, 6, false, 48f, 55f, 8.0f, 10.0f, 0),
                Kind(12, 0.85f, -3, 1, true, 32f, 50f, 3.0f, 5.0f, 75),
                Kind(13, 0.85f, -3, 1, true, 32f, 50f, 3.0f, 5.0f, 75),
            };
            return new ArcheryConfig(
                wavePeriodTicks: 0, minTargets: 0, maxTargets: 0,
                spawnRadius: 16f, spawnMinY: 3.5f, spawnMaxY: 13f, minSeparation: 0f,
                trapRatioMin: 0f, trapRatioMax: 0f,
                shakeFreeSeconds: 0f, shakeRampSeconds: 2.5f, shakeMaxDegrees: 0f,
                riseHeightMin: 0.5f, riseHeightMax: 1.5f, staggerTicks: 0, restTicks: 0,
                kinds: kinds, courseKind: courseKind,
                arrowMinSpeedMps: 50f, arrowMaxSpeedMps: 100f,
                catapult: new ArcheryCatapultSettings(3, 80, 1250, 110, 160, 35, 150, 130, 0.35f, 16f));
        }

        private static ArcheryTargetKind Kind(int id, float radius, int points, int weight, bool trap,
                                              float dMin, float dMax, float vMin, float vMax, int stun)
            => new ArcheryTargetKind(radius, points, weight, trap, ArcheryTargetShape.Sphere,
                                     new[] { new ArcheryRingBand(1f, points) },
                                     id: id, distanceMinM: dMin, distanceMaxM: dMax,
                                     driftMinMps: vMin, driftMaxMps: vMax, stunTicks: stun);

        private ArcheryCourse Course(ArcheryCourseKind courseKind = ArcheryCourseKind.Catapult)
        {
            var layout = OneLane();
            var seed = new MatchSeed();
            seed.Set(42UL);
            return new ArcheryCourse(Config(courseKind), seed, new[] { "u1", "u2" }, Dt, () => layout);
        }

        [Test]
        public void 맞은_무리와_표적_번호로_그_표적과_맞은_틱의_자리를_찾는다()
        {
            var course = Course();
            const int volley = 4;
            var expected = new List<ArcheryTarget>();
            course.Fill(expected, volley, StartTick);
            Assert.GreaterOrEqual(expected.Count, 2, "무리에 표적이 없으면 아무것도 못 본다");

            var scratch = new List<ArcheryTarget>();
            //  각 표적을 차례로 찾는다 — 첫 표적만 보면 "늘 0번을 준다"는 실수가 통과한다.
            foreach (var want in expected)
            {
                long hitTick = want.SpawnTick + 60;
                var hit = new ArcheryTargetHitEvent("u1", hitTick - 30, want.Points,
                                                    waveIndex: volley, slotIndex: want.SlotIndex, hitTick: hitTick);

                Assert.IsTrue(ArcheryCatapultHits.TryLocate(course, StartTick, Dt, hit, scratch,
                                                            out var got, out var position));
                Assert.AreEqual(want.SlotIndex, got.SlotIndex);
                Assert.AreEqual(want.Origin, got.Origin);
                Assert.AreEqual(want.Drift, got.Drift);
                Vector3 expectedPos = ArcheryTargetMotion.PositionAt(want, hitTick, Dt);
                Assert.Less(Vector3.Distance(expectedPos, position), 1e-4f);
                //  나는 표적이라 맞은 틱의 자리는 뜬 자리와 달라야 한다(뜬 틱으로 계산하면 여기서 걸린다).
                Assert.Greater(Vector3.Distance(ArcheryTargetMotion.PositionAt(want, want.SpawnTick, Dt), position), 0.5f);
            }
        }

        [Test]
        public void 투석기_코스가_아니면_찾지_않는다()
        {
            var course = Course(ArcheryCourseKind.Wave);
            var hit = new ArcheryTargetHitEvent("u1", 1100, 1, waveIndex: 0, slotIndex: 0, hitTick: 1200);
            Assert.IsFalse(ArcheryCatapultHits.TryLocate(course, StartTick, Dt, hit, new List<ArcheryTarget>(),
                                                         out _, out _));
        }

        [Test]
        public void 맞은_틱이_없으면_찾지_않는다()
        {
            var course = Course();
            //  옛 서버: 번호 칸을 몰라 0으로 보낸다. 0번 무리 0번 표적은 진짜 있으므로 hitTick으로 가려야 한다.
            var hit = new ArcheryTargetHitEvent("u1", 1100, 1, waveIndex: 0, slotIndex: 0, hitTick: 0);
            Assert.IsFalse(ArcheryCatapultHits.TryLocate(course, StartTick, Dt, hit, new List<ArcheryTarget>(),
                                                         out _, out _));
        }

        [Test]
        public void 무리_번호가_범위_밖이면_찾지_않는다()
        {
            var course = Course();
            var scratch = new List<ArcheryTarget>();
            var below = new ArcheryTargetHitEvent("u1", 1100, 1, waveIndex: -1, slotIndex: 0, hitTick: 1200);
            var above = new ArcheryTargetHitEvent("u1", 1100, 1, waveIndex: course.StepCount, slotIndex: 0, hitTick: 1200);
            Assert.IsFalse(ArcheryCatapultHits.TryLocate(course, StartTick, Dt, below, scratch, out _, out _));
            Assert.IsFalse(ArcheryCatapultHits.TryLocate(course, StartTick, Dt, above, scratch, out _, out _));
        }

        [Test]
        public void 무리에_없는_표적_번호면_찾지_않는다()
        {
            var course = Course();
            var hit = new ArcheryTargetHitEvent("u1", 1100, 1, waveIndex: 0, slotIndex: 99, hitTick: 1200);
            Assert.IsFalse(ArcheryCatapultHits.TryLocate(course, StartTick, Dt, hit, new List<ArcheryTarget>(),
                                                         out _, out _));
        }
    }
}
