using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 카드 러시 카메라 식(spec 2026-11-05 §11.5) — 피벗은 몸 앞 3 m, 각 35°·거리 그대로. 굴러오는 것이 풀리는 7 m 앞이 화면 안,
    /// 내 몸은 아래 "무사 N" 글 위, 머리 위 카드는 위 HUD 밑. (옛 CardRushCourseLayoutTests의 카메라 시험을 옮겼다.)
    /// </summary>
    public class CardRushCameraTests
    {
        private const float Eps = 1e-4f;

        //  CardRush.unity 메인 카메라의 field of view(세로). 씬 값이 바뀌면 여기도 같이 바꾼다.
        private const float SceneFov = 50f;

        //  화면 높이(아래 0) — 내 몸 자리에서 앞으로 forward, 위로 up.
        private static float ScreenY(float forward, float up, int screenW, int screenH)
            => CardRushCamera.ViewportY(-CardRushCamera.LookAheadMeters + forward, up,
                                        CardRushCamera.PitchDegrees, CardRushCamera.DistanceFor(screenW, screenH), SceneFov);

        [Test]
        public void 피벗은_몸_앞_3m이고_10초당_비율로_따라간다()
        {
            var pivot = CardRushCamera.PivotTarget(new Vector3(1f, 0f, 2f), Vector3.forward);
            Assert.That(Vector3.Distance(new Vector3(1f, 0f, 5f), pivot), Is.LessThan(Eps));
            var next = CardRushCamera.Follow(Vector3.zero, new Vector3(10f, 0f, 0f), 0.02f);
            Assert.AreEqual(10f * (1f - Mathf.Exp(-10f * 0.02f)), next.x, Eps);
        }

        [Test]
        public void 카메라_각과_거리()
        {
            Assert.AreEqual(0f, CardRushCamera.YawDegrees(Vector3.forward), Eps);
            Assert.AreEqual(90f, CardRushCamera.YawDegrees(Vector3.right), Eps);
            Assert.AreEqual(9f, CardRushCamera.DistanceFor(1920, 1080), Eps);
            Assert.AreEqual(9f * 1.35f, CardRushCamera.DistanceFor(1080, 1920), Eps, "세로 화면은 멀리");
        }

        //  굴러오는 것은 내 앞 7 m에서 풀린다 — 그 자리(높이 1 m 통 꼭대기까지)가 위 HUD(진행 막대) 밑에 보여야 미리 읽는다.
        [TestCase(1920, 1080)]
        [TestCase(2400, 1080)]
        [TestCase(1080, 1920)]
        [TestCase(1080, 2400)]
        public void 굴러오는_것이_풀리는_7m_앞이_화면_안(int w, int h)
        {
            float reveal = CardRushRules.RollerRevealMm / 1000f;
            Assert.Less(ScreenY(reveal, 1f, w, h), 0.88f, "7 m 앞 통 꼭대기");
            Assert.Greater(ScreenY(reveal, 0f, w, h), 0.5f, "화면 위쪽 절반");
        }

        //  아래 "무사 N" 글(아래 3% + 글 높이 ≈ 0.1)과 손가락 자리보다 위에 내 발이 와야 꽈당·으악이 보인다.
        [TestCase(1920, 1080)]
        [TestCase(2400, 1080)]
        [TestCase(1080, 1920)]
        [TestCase(1080, 2400)]
        public void 내_몸과_몸개그는_아래_글_위에_보인다(int w, int h)
        {
            const float Bottom = 0.15f;
            Assert.Greater(ScreenY(0f, 0f, w, h), Bottom, "서 있는 발");
            //  꽈당은 몸 가운데를 축으로 구르며 떠오른다 — 발이 바닥 밑으로 안 간다. 으악 납작은 발 그대로.
            Assert.Greater(ScreenY(0f, -0.05f, w, h), Bottom, "엉덩방아(살짝 눌림)");
            Assert.Less(ScreenY(0f, BodySizes.CharacterHeight, w, h), 0.9f, "머리가 위 HUD(진행 막대) 밑에 남는다");
        }

        [TestCase(1920, 1080)]
        [TestCase(2400, 1080)]
        [TestCase(1080, 1920)]
        [TestCase(1080, 2400)]
        public void 내_머리_위_카드는_위_HUD_밑에_보인다(int w, int h)
        {
            //  정수리를 몸 키로 넉넉히 잡는다(실제 정수리는 그보다 낮다).
            float cardTop = BodySizes.CharacterHeight + CardRushHeadCardView.HeadCardLift + CardRushHeadCardView.MineHeight;
            Assert.Less(ScreenY(0f, cardTop, w, h), 0.88f, "카드 위 끝");
        }

        [Test]
        public void 화면_높이_식은_피벗이_가운데다()
        {
            Assert.AreEqual(0.5f, CardRushCamera.ViewportY(0f, 0f, 35f, 9f, 50f), Eps);
            //  피벗 바로 앞(진행 방향)은 화면 위로, 아래(땅 밑)는 화면 아래로.
            Assert.Greater(CardRushCamera.ViewportY(2f, 0f, 35f, 9f, 50f), 0.5f);
            Assert.Less(CardRushCamera.ViewportY(0f, -1f, 35f, 9f, 50f), 0.5f);
            //  시야각 끝 — 카메라 위쪽 축으로 거리×tan(25°)만큼 올린 점은 화면 맨 위(1).
            float up = 9f * Mathf.Tan(25f * Mathf.Deg2Rad);
            float p = 35f * Mathf.Deg2Rad;
            Assert.AreEqual(1f, CardRushCamera.ViewportY(up * Mathf.Sin(p), up * Mathf.Cos(p), 35f, 9f, 50f), 1e-3f);
        }
    }
}
