using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 기절 동안 머리 위에 별 셋이 돈다(spec §3 — 내 새·남의 새 모두). 별은 새 그림의 자식이라 보간기가 그림을 옮겨도 같이 가고,
    /// 기절색 주인(StunAppearance)이 별을 자기 렌더러로 모으지 않게 표시를 단다.
    /// </summary>
    public class FlappyStunStarsTests
    {
        private const float R = 0.45f;
        private FakeFlappyBirdLooks looks;
        private FlappyStunStars stars;
        private GameObject visual;

        private static FlappyConfig Config()
            => new FlappyConfig(forwardSpeed: 4.5f, flapImpulse: 10.125f, gravity: 33.75f, maxFallSpeed: 11.25f,
                                bodyRadius: R, bodyHeight: 0.9f, restitution: 0.35f, stunTime: 1.2f, invulnTime: 0.6f,
                                dashMult: 2.2f, dashDuration: 0.48f, dashChargeBase: 0f, dashChargeDive: 1.3f);

        [SetUp]
        public void SetUp()
        {
            looks = new FakeFlappyBirdLooks();
            stars = new FlappyStunStars(looks, Config());
            visual = new GameObject("Visual_a");
            visual.transform.position = new Vector3(10f, 2f, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            stars.Dispose();
            UnityEngine.Object.DestroyImmediate(visual);
        }

        private void Set(bool stunned)
        {
            looks.Birds.Clear();
            looks.Birds.Add(new FlappyBirdLook("a", false, stunned, visual.transform));
        }

        [Test]
        public void 기절한_새_머리_위에_별_셋()
        {
            Set(true);
            stars.LateTick(0f);

            Assert.IsTrue(stars.Shows("a"));
            float centerY = 2f + R;
            for (int i = 0; i < FlappyStunStars.StarCount; i++)
            {
                Vector3 p = stars.StarOf("a", i).position;
                Assert.That(p.y, Is.InRange(centerY + 1.25f * R - 0.35f * R - 1e-4f, centerY + 1.25f * R + 0.35f * R + 1e-4f), $"별 {i} 높이");
                Assert.That(Mathf.Abs(p.x - 10f), Is.LessThanOrEqualTo(1.3f * R + 1e-4f), $"별 {i} 가로");
            }
        }

        [Test]
        public void 별은_머리_위를_돈다()
        {
            Set(true);
            stars.LateTick(0f);
            Vector3 before = stars.StarOf("a", 0).position;

            stars.LateTick(0.1f);

            Assert.AreNotEqual(before, stars.StarOf("a", 0).position);
        }

        [Test]
        public void 그림을_따라가고_풀리면_숨는다()
        {
            Set(true);
            stars.LateTick(0f);
            float x = stars.StarOf("a", 0).position.x;

            visual.transform.position += new Vector3(10f, 0f, 0f);   //  보간기가 하는 일
            Assert.AreEqual(x + 10f, stars.StarOf("a", 0).position.x, 1e-4f, "그림의 자식 — 같은 프레임에 같이 간다");

            Set(false);
            stars.LateTick(0.02f);
            Assert.IsFalse(stars.Shows("a"));
        }

        [Test]
        public void 별은_기절색_주인에게서_빠진다()
        {
            Set(true);
            stars.LateTick(0f);

            Assert.IsNotNull(stars.StarOf("a", 0).GetComponentInParent<StunAppearanceIgnore>(true),
                "그림 자식이라 StunAppearance가 렌더러를 다시 모을 때 걸린다 — 기절색이 별에 묻고 별이 먼저 지워지면 죽은 렌더러에 쓴다");
        }
    }
}
