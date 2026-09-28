using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ChibiFaceAtlasTests
    {
        [Test]
        public void 보통은_왼쪽_위_칸()
        {
            //  텍스처 좌표는 아래가 0 — 위 줄의 오프셋 y는 0.5다.
            Assert.AreEqual(new Vector4(1f / 3f, 0.5f, 0f, 0.5f), ChibiFaceAtlas.CellST(ChibiExpression.Normal));
        }

        [Test]
        public void 셋째는_위_줄_오른쪽_넷째는_아래_줄_왼쪽()
        {
            Assert.AreEqual(new Vector4(1f / 3f, 0.5f, 2f / 3f, 0.5f), ChibiFaceAtlas.CellST(ChibiExpression.Cheer));
            Assert.AreEqual(new Vector4(1f / 3f, 0.5f, 0f, 0f), ChibiFaceAtlas.CellST(ChibiExpression.Despair));
            Assert.AreEqual(new Vector4(1f / 3f, 0.5f, 1f / 3f, 0f), ChibiFaceAtlas.CellST(ChibiExpression.Focus));
        }
    }
}
