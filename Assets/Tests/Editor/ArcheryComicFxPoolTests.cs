using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryComicFxPoolTests
    {
        private static Texture2D Atlas() => new Texture2D(1024, 512);

        [Test]
        public void 같은_사람은_한_장만()
        {
            var atlas = Atlas();
            using (var pool = new ArcheryComicFxPool(atlas))
            {
                pool.Show("p1", ArcheryComicFx.For(ArcheryComicEvent.Bull), Vector3.zero, 0f);
                pool.Show("p1", ArcheryComicFx.For(ArcheryComicEvent.RobinHood), Vector3.zero, 0.1f);
                pool.Show("p2", ArcheryComicFx.For(ArcheryComicEvent.Miss), Vector3.one, 0.1f);
                Assert.AreEqual(2, pool.ActiveCount);
            }
            Object.DestroyImmediate(atlas);
        }

        [Test]
        public void 수명이_끝나면_치운다()
        {
            var atlas = Atlas();
            using (var pool = new ArcheryComicFxPool(atlas))
            {
                pool.Show("p1", ArcheryComicFx.For(ArcheryComicEvent.Bull), Vector3.zero, 0f);
                pool.Tick(0.5f, null);
                Assert.AreEqual(1, pool.ActiveCount);
                pool.Tick(ArcheryComicFx.Life + 0.01f, null);
                Assert.AreEqual(0, pool.ActiveCount);
            }
            Object.DestroyImmediate(atlas);
        }

        [Test]
        public void 지우면_남는_오브젝트가_없다()
        {
            var atlas = Atlas();
            int before = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Length;
            var pool = new ArcheryComicFxPool(atlas);
            pool.Show("p1", ArcheryComicFx.For(ArcheryComicEvent.Chicken), Vector3.zero, 0f);
            Assert.Greater(Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Length, before);
            pool.Dispose();
            Assert.AreEqual(before, Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Length);
            Object.DestroyImmediate(atlas);
        }
    }
}
