using LOP.MapTools;
using NUnit.Framework;
using UnityEngine;

namespace LOP.MapTools.Tests
{
    public class TrapShapeRulesTests
    {
        static SurfaceSample Sample(Vector3 normal) => new SurfaceSample(Vector3.zero, normal.normalized);

        [Test]
        public void 앞으로_기울어_덮은_면은_의심한다()
        {
            //  아래를 향하면서(y<0) 뒤쪽 성분이 있는(x<0) 법선 = 앞으로 기운 덮개.
            Assert.IsTrue(new ForwardOverhangRule().IsSuspect(Sample(new Vector3(-0.5f, -0.8f, 0f))));
        }

        [Test]
        public void 뒤로_기운_끝면은_의심하지_않는다()
        {
            //  실측: 같은 크기라도 뒤로 기울면 위로 열려 있어 넘어간다(1.68m 계단은 멀쩡했다).
            Assert.IsFalse(new ForwardOverhangRule().IsSuspect(Sample(new Vector3(0.5f, -0.8f, 0f))));
        }

        [Test]
        public void 평평한_천장은_의심하지_않는다()
        {
            //  똑바로 아래를 보는 면은 앞으로 기운 게 아니다 — 부딪혀도 옆으로 미끄러져 빠진다.
            Assert.IsFalse(new ForwardOverhangRule().IsSuspect(Sample(new Vector3(0f, -1f, 0f))));
        }

        [Test]
        public void 바닥은_의심하지_않는다()
        {
            Assert.IsFalse(new ForwardOverhangRule().IsSuspect(Sample(new Vector3(-0.3f, 1f, 0f))));
        }

        [Test]
        public void 거의_수직인_벽은_의심하지_않는다()
        {
            //  아래 성분이 거의 없으면 덮개가 아니라 벽이다.
            Assert.IsFalse(new ForwardOverhangRule().IsSuspect(Sample(new Vector3(-1f, -0.05f, 0f))));
        }

        [Test]
        public void 기본_규칙_목록에_덮개_규칙이_들어_있다()
        {
            //  규칙을 더할 수 있는 구조라는 것 자체를 못박는다 — 목록이 비면 씨앗이 하나도 안 는다.
            Assert.IsNotEmpty(TrapShapeRules.Default);
            CollectionAssert.Contains(System.Linq.Enumerable.ToList(
                System.Linq.Enumerable.Select(TrapShapeRules.Default, r => r.Name)), "앞으로 기운 덮개");
        }
    }
}
