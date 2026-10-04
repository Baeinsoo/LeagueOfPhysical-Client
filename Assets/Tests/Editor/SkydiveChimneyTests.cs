using System.Linq;
using LOP.EditorTools;
using NUnit.Framework;
using UnityEngine;
using C = LOP.EditorTools.SkydiveChimneyLayout;

namespace LOP.Tests
{
    /// <summary>
    /// 굴뚝 시제품 — 젤다 굴뚝처럼 촘촘한 레이저 그물이 자세 선택을 강요하는지(1m 낙하당 옆 이동: 대자 0.2 / 패러세일 2.3).
    /// </summary>
    public class SkydiveChimneyTests
    {
        [Test]
        public void 굽기_검사를_통과한다()
        {
            Assert.IsNull(SkydiveChimneyBuilder.Verify());
        }

        [Test]
        public void 그물은_구멍_창으로만_지나간다()
        {
            var net = C.Nets[0];
            var lasers = C.NetLasers(net);
            //  창 가장자리 안쪽은 지나가고, 창 밖은 굴뚝 벽까지 어디든 막힌다(진짜 판정 함수로).
            float inner = C.WindowHalf - 0.2f;
            Assert.IsFalse(Hits(lasers, net.Y, net.X + inner, net.Z - inner), "창 모서리 안쪽");
            Assert.IsFalse(Hits(lasers, net.Y, net.X, net.Z), "창 가운데");
            for (float x = -C.Half + 0.5f; x <= C.Half - 0.5f; x += 0.5f)
            {
                for (float z = -C.Half + 0.5f; z <= C.Half - 0.5f; z += 0.5f)
                {
                    bool inWindow = Mathf.Abs(x - net.X) <= C.WindowHalf && Mathf.Abs(z - net.Z) <= C.WindowHalf;
                    if (inWindow == false)
                    {
                        Assert.IsTrue(Hits(lasers, net.Y, x, z), $"({x}, {z})는 막혀야 한다");
                    }
                }
            }
        }

        [Test]
        public void 계단은_대자로_못_가고_패러세일로만_간다()
        {
            var steps = C.Transitions().Where(t => t.Kind == C.StepKind.Glide).ToArray();
            Assert.GreaterOrEqual(steps.Length, 8, "계단이 있어야 자세를 고른다");
            foreach (var t in steps)
            {
                //  가장 가까운 가장자리끼리도 대자로는 못 닿고, 가장 먼 가장자리끼리도 패러세일로는 닿는다.
                Assert.Less(C.SpreadReach(t.Drop), t.Distance - C.WindowHalf * 2f, $"{t.FromY:0}→{t.ToY:0} 대자로 닿는다");
                Assert.GreaterOrEqual(C.GlideReach(t.Drop), t.Distance + C.WindowHalf * 2f, $"{t.FromY:0}→{t.ToY:0} 패러세일로도 못 닿는다");
            }
        }

        [Test]
        public void 낙하_구간은_대자로_간다()
        {
            var drops = C.Transitions().Where(t => t.Kind == C.StepKind.Fall).ToArray();
            Assert.GreaterOrEqual(drops.Length, 3);
            foreach (var t in drops)
            {
                Assert.GreaterOrEqual(C.SpreadReach(t.Drop), t.Distance, $"{t.FromY:0}→{t.ToY:0} 대자로 못 닿는다");
            }
        }

        [Test]
        public void 구간마다_꼭_필요한_패러세일_시간은_스태미나_안이지만_빠듯하다()
        {
            var sections = C.GlideSecondsPerSection();
            Assert.AreEqual(2, sections.Length, "스폰→턱, 턱→바닥");
            foreach (var (fromY, seconds) in sections)
            {
                Assert.LessOrEqual(seconds, C.GlideBudgetSeconds, $"{fromY:0} 구간은 스태미나로 못 깬다");
                Assert.Greater(seconds, C.GlideBudgetSeconds * 0.6f, $"{fromY:0} 구간은 너무 넉넉하다 — 아무 때나 펴도 된다");
            }
        }

        [Test]
        public void 체크포인트는_스폰과_중간_턱_둘이다()
        {
            CollectionAssert.AreEquivalent(new[] { C.SpawnY, C.LedgeY }, C.RespawnPoints.Keys.ToArray());
        }

        [Test]
        public void 중간_턱의_구멍은_마지막_계단에서_곧장_빠질_수_없다()
        {
            //  턱에 내려서 걸어가야 구멍에 닿는다 — 그래야 턱이 스태미나를 채우는 자리가 된다.
            var last = C.Nets.Where(n => n.Y > C.LedgeY).OrderBy(n => n.Y).First();
            float d = new Vector2(C.LedgeHole.X - last.X, C.LedgeHole.Z - last.Z).magnitude;
            Assert.Greater(d - C.LedgeHole.Half - C.WindowHalf, C.GlideReach(last.Y - C.LedgeY));
        }

        private static bool Hits(SkydiveCourseBuilder.LaserSpec[] lasers, float netY, float x, float z)
        {
            const float radius = 0.4f, height = 1.8f;   // #SkydiveConfig body_radius·body_height
            var from = new Vector3(x, netY + 6f, z);
            var to = new Vector3(x, netY - 6f, z);
            foreach (var l in lasers)
            {
                if (LaserSweep.Hit(l.ToLaser(), 0,
                        new System.Numerics.Vector3(from.x, from.y + radius, from.z), new System.Numerics.Vector3(from.x, from.y + height - radius, from.z),
                        new System.Numerics.Vector3(to.x, to.y + radius, to.z), new System.Numerics.Vector3(to.x, to.y + height - radius, to.z),
                        radius, out _))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
