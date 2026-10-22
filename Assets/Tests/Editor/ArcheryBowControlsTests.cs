using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    using Hit = LOP.UI.ArcheryButtonLayout.Hit;

    //  활 조작판의 조작: 누른 손가락의 역할에 따라 활을 들고 쏘고, 나머지는 겨누기만 한다.
    public class ArcheryBowControlsTests
    {
        sealed class FakeInput : LOP.UI.IBowInput
        {
            public readonly List<string> Calls = new List<string>();
            public Vector2 Movement;
            public void SetDrawing(bool drawing) => Calls.Add("Drawing:" + drawing);
            public void SetDrawRatio(float ratio) => Calls.Add("Ratio:" + ratio);
            public void SetRelease() => Calls.Add("Release");
            public void SetMovement(float horizontal, float vertical) { Movement = new Vector2(horizontal, vertical); }
            public int Count(string call) => Calls.FindAll(c => c == call).Count;
        }

        sealed class FakeCamera : LOP.UI.IAimCamera
        {
            public float Yaw { get; private set; }
            public float Pitch { get; private set; }
            public float CameraYawDegrees { get; set; }
            public Camera MainCamera => null;
            public void AimBy(Vector2 degrees) { Yaw += degrees.x; Pitch -= degrees.y; }
        }

        static LOP.UI.ArcheryBowControls Make(FakeInput input, FakeCamera cam, float[] draw, List<bool> released)
            => new LOP.UI.ArcheryBowControls(input, cam, () => draw[0], armed => released?.Add(armed));

        [Test]
        public void 활_손가락을_시위가_걸린_뒤_떼면_쏜다()
        {
            var input = new FakeInput();
            var released = new List<bool>();
            var draw = new[] { 0f };
            var controls = Make(input, new FakeCamera(), draw, released);

            controls.PointerDown(1, Hit.Bow, 1.78f);
            Assert.AreEqual(1, input.Count("Drawing:True"));
            Assert.AreEqual(1, input.Count("Ratio:1"));

            draw[0] = 0.5f;
            controls.PointerUp(1);

            Assert.AreEqual(1, input.Count("Drawing:False"));
            Assert.AreEqual(1, input.Count("Release"));
            CollectionAssert.AreEqual(new[] { true }, released);
        }

        [Test]
        public void 시위가_안_걸렸으면_떼도_안_쏜다()
        {
            var input = new FakeInput();
            var draw = new[] { 0f };
            var controls = Make(input, new FakeCamera(), draw, null);

            controls.PointerDown(1, Hit.Bow, 1.78f);
            draw[0] = 0.1f;
            controls.PointerUp(1);

            Assert.AreEqual(0, input.Count("Release"));
        }

        [Test]
        public void 취소_버튼_위에서_떼면_내려놓는다()
        {
            var input = new FakeInput();
            var draw = new[] { 0f };
            var controls = Make(input, new FakeCamera(), draw, null);

            controls.PointerDown(1, Hit.Bow, 1.78f);
            draw[0] = 0.5f;
            controls.PointerMove(1, Vector2.zero, true);
            controls.PointerUp(1);

            Assert.AreEqual(0, input.Count("Release"));
            Assert.AreEqual(1, input.Count("Drawing:False"));
        }

        [Test]
        public void 다른_손가락은_조준만_한다()
        {
            var input = new FakeInput();
            var cam = new FakeCamera();
            var controls = Make(input, cam, new[] { 0f }, null);

            controls.PointerDown(2, Hit.None, 1.78f);
            Assert.AreEqual(0, input.Calls.FindAll(c => c.StartsWith("Drawing")).Count);

            controls.PointerMove(2, new Vector2(0.1f, 0f), false);
            Assert.AreNotEqual(0f, cam.Yaw, "겨누는 손가락이 카메라를 못 돌렸다");
        }

        [Test]
        public void 스틱은_카메라_기준_방향으로_넘긴다()
        {
            var input = new FakeInput();
            var cam = new FakeCamera { CameraYawDegrees = 90f };
            var controls = Make(input, cam, new[] { 0f }, null);

            controls.SetMove(new Vector2(0f, 1f));
            controls.FeedMove();

            Assert.AreEqual(1f, input.Movement.x, 1e-4f);
            Assert.AreEqual(0f, input.Movement.y, 1e-4f);
        }
    }
}
