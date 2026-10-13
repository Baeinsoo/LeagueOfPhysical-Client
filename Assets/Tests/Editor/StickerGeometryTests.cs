using LOP.UI;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>스티커 앞면은 윗변이 오른쪽으로 밀린 평행사변형(시안 clip-path와 같은 모양).</summary>
    public class StickerGeometryTests
    {
        private static readonly Rect Box = new Rect(0, 0, 400, 100);

        [Test]
        public void 기울임은_폭의_비율만큼_윗변을_민다()
        {
            var p = StickerGeometry.Face(Box, 0.025f, 0f);
            Assert.AreEqual(new Vector2(0, 100), p[0]);
            Assert.AreEqual(new Vector2(390, 100), p[1]);
            Assert.AreEqual(new Vector2(400, 0), p[2]);
            Assert.AreEqual(new Vector2(10, 0), p[3]);
        }

        [Test]
        public void 고정_꼬리가_있으면_비율보다_먼저다()
        {
            var p = StickerGeometry.Face(Box, 0.025f, 16f);
            Assert.AreEqual(new Vector2(384, 100), p[1]);
            Assert.AreEqual(new Vector2(16, 0), p[3]);
        }

        [Test]
        public void 기울임_0이면_사각형()
        {
            var p = StickerGeometry.Face(Box, 0f, 0f);
            Assert.AreEqual(new Vector2(400, 100), p[1]);
            Assert.AreEqual(new Vector2(0, 0), p[3]);
        }

        [Test]
        public void 크기_0이면_빈_결과()
        {
            Assert.IsEmpty(StickerGeometry.Face(new Rect(0, 0, 0, 100), 0.025f, 0f));
            Assert.IsEmpty(StickerGeometry.Face(new Rect(0, 0, 100, 0), 0.025f, 0f));
            Assert.IsEmpty(StickerGeometry.Face(new Rect(0, 0, float.NaN, 100), 0.025f, 0f));
        }

        [Test]
        public void 기울임이_폭_절반을_넘으면_자른다()
        {
            var p = StickerGeometry.Face(Box, 0.9f, 0f);
            Assert.AreEqual(new Vector2(200, 100), p[1]);
            Assert.AreEqual(new Vector2(200, 0), p[3]);
        }

        [Test]
        public void 뒷면은_앞면을_어긋남만큼_옮긴_것()
        {
            var face = StickerGeometry.Face(Box, 0.025f, 0f);
            var back = StickerGeometry.Offset(face, 6f, 9f);
            for (int i = 0; i < 4; i++) Assert.AreEqual(face[i] + new Vector2(6, 9), back[i]);
        }
    }
}
