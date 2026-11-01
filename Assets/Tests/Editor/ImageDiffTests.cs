using LOP.EditorTools.Automation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LOP.Tests
{
    /// <summary>픽셀 비교(spec §15) — 허용치·비율·구역(왼쪽 위 원점)·제외 구역·차이 그림, PNG 왕복.</summary>
    public class ImageDiffTests
    {
        private static readonly Color32 Gray = new Color32(100, 100, 100, 255);
        private static readonly Color32 Red = new Color32(255, 0, 0, 255);

        [Test]
        public void 같은_그림은_차이가_0이다()
        {
            var result = ImageDiff.Compare(TestPng.Solid(4, 4, Gray), TestPng.Solid(4, 4, Gray), 4, 4, 0, null, null, null);

            Assert.AreEqual(0, result.Differing);
            Assert.AreEqual(16, result.Compared);
            Assert.AreEqual(0, result.Percent);
        }

        [Test]
        public void 허용치까지의_차이는_세지_않고_넘으면_센다()
        {
            var actual = TestPng.Solid(2, 1, Gray);
            actual[0] = new Color32(110, 100, 100, 255);
            actual[1] = new Color32(100, 100, 130, 255);

            var result = ImageDiff.Compare(TestPng.Solid(2, 1, Gray), actual, 2, 1, 16, null, null, null);

            Assert.AreEqual(1, result.Differing);
            Assert.AreEqual(50, result.Percent);
        }

        [Test]
        public void 허용치와_정확히_같으면_안_세고_채널_최댓값만_본다()
        {
            //  Review L4: `>`→`>=` 사보타주와 "채널 합" 사보타주를 둘 다 잡는다.
            var expected = TestPng.Solid(3, 1, Gray);
            var actual = TestPng.Solid(3, 1, Gray);
            actual[0] = new Color32(116, 100, 100, 255);   // 차 16 — tolerance와 같다, >가 아니라 세지 않는다
            actual[1] = new Color32(110, 110, 110, 255);   // 채널마다 10, 최댓값 10(안 셈) — 채널 합이면 30이라 셈
            actual[2] = new Color32(117, 100, 100, 255);   // 차 17 — 정상적으로 센다(위 둘이 거짓 통과가 아님을 보증)

            var result = ImageDiff.Compare(expected, actual, 3, 1, 16, null, null, null);

            Assert.AreEqual(1, result.Differing);
            Assert.AreEqual(3, result.Compared);
        }

        [Test]
        public void 구역은_왼쪽_위_원점이다()
        {
            //  텍스처의 줄 1(위쪽)·칸 0 = 화면 왼쪽 위.
            var actual = TestPng.Solid(2, 2, Gray);
            actual[1 * 2 + 0] = Red;
            var expected = TestPng.Solid(2, 2, Gray);

            var topLeft = ImageDiff.Compare(expected, actual, 2, 2, 0, new Rect(0f, 0f, 0.5f, 0.5f), null, null);
            var bottomLeft = ImageDiff.Compare(expected, actual, 2, 2, 0, new Rect(0f, 0.5f, 0.5f, 0.5f), null, null);

            Assert.AreEqual(1, topLeft.Differing);
            Assert.AreEqual(1, topLeft.Compared);
            Assert.AreEqual(0, bottomLeft.Differing);
            Assert.AreEqual(1, bottomLeft.Compared);
        }

        [Test]
        public void 제외_구역은_빼고_세고_차이_그림에_색으로_남긴다()
        {
            var actual = TestPng.Solid(2, 1, Red);
            var diff = new Color32[2];

            var result = ImageDiff.Compare(TestPng.Solid(2, 1, Gray), actual, 2, 1, 0, null, new[] { new Rect(0f, 0f, 0.5f, 1f) }, diff);

            Assert.AreEqual(1, result.Differing);
            Assert.AreEqual(1, result.Compared);
            Assert.AreEqual(ImageDiff.SkipColor, diff[0]);
            Assert.AreEqual(ImageDiff.DiffColor, diff[1]);
        }

        [Test]
        public void 구역은_0에서_1_사이_수_넷이다()
        {
            Assert.IsTrue(NormalizedRect.TryParse(JArray.Parse("[0, 0, 0.5, 0.25]"), out var rect, out _));
            Assert.AreEqual(new Rect(0f, 0f, 0.5f, 0.25f), rect);

            Assert.IsFalse(NormalizedRect.TryParse(JArray.Parse("[0, 0, 1]"), out _, out _));
            Assert.IsFalse(NormalizedRect.TryParse(JArray.Parse("[0.8, 0, 0.5, 0.5]"), out _, out _));
            Assert.IsFalse(NormalizedRect.TryParse(JArray.Parse("[0, 0, 0, 1]"), out _, out _));
            Assert.IsFalse(NormalizedRect.TryParse(new JValue("x"), out _, out var why));
            StringAssert.Contains("[x, y, w, h]", why);
        }

        [Test]
        public void png로_썼다_읽으면_픽셀이_그대로다()
        {
            var pixels = TestPng.Solid(3, 2, Gray);
            pixels[4] = new Color32(1, 2, 3, 255);

            Assert.IsTrue(PngPixels.TryDecode(PngPixels.Encode(3, 2, pixels), out int width, out int height, out var back));
            Assert.AreEqual(3, width);
            Assert.AreEqual(2, height);
            CollectionAssert.AreEqual(pixels, back);

            //  깨진 바이트는 false — 유니티가 오류 로그를 낼 수 있어 그 로그는 이 시험의 실패가 아니다.
            LogAssert.ignoreFailingMessages = true;
            Assert.IsFalse(PngPixels.TryDecode(new byte[] { 1, 2, 3 }, out _, out _, out _));
        }
    }
}
