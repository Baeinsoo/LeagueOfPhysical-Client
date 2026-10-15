using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryCatapultLooksTests
    {
        static readonly (string code, float radius)[] Festival =
        {
            ("Shield", 1.2f), ("Helmet", 0.9f), ("Apple", 0.6f), ("Grail", 0.5f), ("KingFlag", 0.85f), ("FlowerBasket", 0.85f),
        };

        [Test]
        public void 축제_표적_여섯은_각자_회색_도형이_있다()
        {
            foreach (var (code, _) in Festival)
            {
                Assert.IsTrue(ArcheryCatapultLooks.IsFestival(code), code);
            }
            Assert.IsFalse(ArcheryCatapultLooks.IsFestival("RangeFace"));
        }

        [Test]
        public void 보이는_크기가_맞는_크기를_넘지_않는다()
        {
            //  활쏘기 원칙: 보이는 크기가 곧 맞는 크기 — 그림이 판정 구 밖으로 5% 넘게 나가면 "맞았는데 안 맞음"이 된다.
            foreach (var (code, radius) in Festival)
            {
                var go = ArcheryCatapultLooks.BuildPlaceholder(code, radius);
                try
                {
                    var bounds = new Bounds(go.transform.position, Vector3.zero);
                    foreach (var r in go.GetComponentsInChildren<Renderer>()) { bounds.Encapsulate(r.bounds); }
                    Assert.LessOrEqual(bounds.extents.magnitude, radius * 1.05f * 1.733f, code);   // 상자 대각 ≤ 구를 감싼 상자
                    Assert.LessOrEqual(Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z), radius * 1.05f, code);
                    Assert.Greater(bounds.extents.magnitude, radius * 0.5f, $"{code}: 너무 작다");
                    Assert.IsEmpty(go.GetComponentsInChildren<Collider>(), $"{code}: 그림일 뿐이다 — 판정은 서버가 한다");
                }
                finally { Object.DestroyImmediate(go); }
            }
        }

        [Test]
        public void 쏘면_안_되는_것은_빨간_금지_표시를_단다()
        {
            foreach (var code in new[] { "KingFlag", "FlowerBasket" })
            {
                var go = ArcheryCatapultLooks.BuildPlaceholder(code, 0.85f);
                try { Assert.IsNotNull(go.transform.Find("forbid"), code); }
                finally { Object.DestroyImmediate(go); }
            }
        }
    }
}
