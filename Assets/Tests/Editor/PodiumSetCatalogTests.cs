using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>시상대 세트는 종목·맵으로 고른다 — (종목,맵) 일치 > 종목만 일치 > 공용. 전용 세트는 등록만으로 끼운다.</summary>
    public class PodiumSetCatalogTests
    {
        private sealed class Named : IPodiumSet
        {
            public readonly string Name;
            public Named(string name) => Name = name;
            public PodiumStage Build(Transform root, PodiumContext context) => new PodiumStage();
        }

        [Test]
        public void 아무것도_없으면_공용()
        {
            var common = new Named("common");
            Assert.AreSame(common, new PodiumSetCatalog(common).Resolve(5, 12));
        }

        [Test]
        public void 종목_맵_일치가_종목만_일치보다_먼저()
        {
            var common = new Named("common");
            var mode = new Named("mode");
            var map = new Named("map");
            var c = new PodiumSetCatalog(common);
            c.Register(5, null, mode);
            c.Register(5, 12, map);
            Assert.AreSame(map, c.Resolve(5, 12));
            Assert.AreSame(mode, c.Resolve(5, 13));
            Assert.AreSame(common, c.Resolve(6, 12));
        }

        [Test]
        public void 기본_카탈로그는_공용만() =>
            Assert.IsInstanceOf<CommonPodiumSet>(PodiumSetCatalog.CreateDefault().Resolve(1, 1));

        [Test]
        public void 등수가_없으면_결과_없음으로_본다()
        {
            Assert.IsFalse(new PodiumContext(false, null, false).HasResult);
            Assert.IsTrue(new PodiumContext(false, new[] { 1 }, false).HasResult);
        }
    }
}
