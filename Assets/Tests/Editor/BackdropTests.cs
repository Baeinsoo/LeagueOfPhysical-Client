using LOP.UI;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>모달 백드롭 — 로그인은 타이틀을 어둡게 덮지 않지만 아래 입력은 막는다.</summary>
    public class BackdropTests
    {
        private sealed class Dim : UIPopup { }
        private sealed class Clear : UIPopup { public override bool DimsBackground => false; }

        [Test]
        public void 기본_모달은_반투명_검정()
        {
            Assert.AreEqual(new Color(0f, 0f, 0f, 0.5f), WindowManager.BackdropColorFor(new Dim()));
        }

        [Test]
        public void 투명_백드롭도_입력을_막는다()
        {
            //  색만 투명이고 백드롭은 그대로 깔린다 — 깔리는 조건(IsModal)과 입력 차단(PickingMode.Position)은 그대로.
            var clear = new Clear();
            Assert.AreEqual(0f, WindowManager.BackdropColorFor(clear).a);
            Assert.IsTrue(clear.IsModal);
        }

        [Test]
        public void 로그인은_어둡게_덮지_않고_연결_실패는_덮는다()
        {
            Assert.IsFalse(new LoginView(null).DimsBackground);
            Assert.IsTrue(new ServerUnreachableView(null).DimsBackground);
        }
    }
}
