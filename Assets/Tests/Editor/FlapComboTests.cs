using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 연속 날갯짓 연출 "집중형"(spec §7, 사용자 결정 10-26). 앞길과 화면 가운데를 비운다 — 작은 "+N"은 새 뒤쪽 위에서 0.45초,
    /// 깃털은 새 뒤로만, 꼬리는 25부터 가늘게, 고비(10·25·50·100) 한마디·고리는 새 옆에 작게. 단위 u는 시제품 무대(높이 545px)의 1px.
    /// </summary>
    public class FlapComboTests
    {
        private const float U = 2f;       //  1090px 높이 화면
        private const float RPx = 18f;    //  새 반지름(화면 px)
        private static readonly Vector2 Bird = new Vector2(600f, 500f);

        private static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c).ToLowerInvariant();

        [Test]
        public void 단계_색은_10_25_50에서_바뀐다()
        {
            Assert.AreEqual("#fff6e6", Hex(FlapComboLook.TierColor(1)));
            Assert.AreEqual("#fff6e6", Hex(FlapComboLook.TierColor(9)));
            Assert.AreEqual("#ffd257", Hex(FlapComboLook.TierColor(10)));
            Assert.AreEqual("#ff9a3c", Hex(FlapComboLook.TierColor(25)));
            Assert.AreEqual("#ff6a3d", Hex(FlapComboLook.TierColor(50)));
            Assert.AreEqual("#ff6a3d", Hex(FlapComboLook.TierColor(300)));
            Assert.AreEqual("#2a1a0c", Hex(FlapComboLook.Outline));
        }

        [Test]
        public void 고비_한마디는_10_25_50_100에만()
        {
            Assert.AreEqual("좋아!", FlapComboLook.Word(10));
            Assert.AreEqual("멋져!", FlapComboLook.Word(25));
            Assert.AreEqual("미쳤다!", FlapComboLook.Word(50));
            Assert.AreEqual("전설!", FlapComboLook.Word(100));
            foreach (int n in new[] { 1, 9, 11, 24, 26, 49, 75, 99, 101, 200 })
            {
                Assert.IsNull(FlapComboLook.Word(n), n.ToString());
                Assert.IsFalse(FlapComboLook.IsMilestone(n), n.ToString());
            }
        }

        [Test]
        public void 숫자는_0_6에서_1_2로_튀었다가_30퍼센트에_1배()
        {
            Assert.AreEqual(0.6f, FlapComboLook.NumberPop(0f), 1e-4f);
            Assert.AreEqual(1.2f, FlapComboLook.NumberPop(0.15f), 1e-4f);
            Assert.AreEqual(1.1f, FlapComboLook.NumberPop(0.225f), 1e-4f);
            Assert.AreEqual(1f, FlapComboLook.NumberPop(0.3f), 1e-4f);
            Assert.AreEqual(1f, FlapComboLook.NumberPop(0.8f), 1e-4f);
        }

        [Test]
        public void 숫자는_반까지_또렷하다가_0_45초에_사라진다()
        {
            Assert.AreEqual(0.45f, FlapComboLook.NumberLife, 1e-6f);
            Assert.AreEqual(1f, FlapComboLook.NumberAlpha(0.5f), 1e-4f);
            Assert.AreEqual(0.5f, FlapComboLook.NumberAlpha(0.75f), 1e-4f);
            Assert.AreEqual(0f, FlapComboLook.NumberAlpha(1f), 1e-4f);
        }

        [Test]
        public void 숫자_크기는_연속이_길수록_조금씩_60에서_멈춘다()
        {
            Assert.AreEqual(0.62f + 1f / 300f, FlapComboLook.NumberSize(1), 1e-5f);
            Assert.AreEqual(0.62f + 30f / 300f, FlapComboLook.NumberSize(30), 1e-5f);
            Assert.AreEqual(0.82f, FlapComboLook.NumberSize(60), 1e-5f);
            Assert.AreEqual(0.82f, FlapComboLook.NumberSize(500), 1e-5f);
        }

        [Test]
        public void 숫자와_한마디는_새_뒤쪽_위에_앞길을_가리지_않는다()
        {
            //  판의 오른쪽 아래 모서리가 몸 가운데보다 뒤, 몸 꼭대기보다 위(패널 y는 아래로 큰다).
            Vector2 number = FlapComboLook.NumberCorner(Bird, RPx, U);
            Assert.Less(number.x, Bird.x, "몸 가운데보다 뒤");
            Assert.Less(number.y, Bird.y - RPx, "몸 꼭대기보다 위");

            Vector2 word = FlapComboLook.WordCorner(Bird, RPx, U);
            Assert.Less(word.x, Bird.x);
            Assert.Less(word.y, number.y, "한마디는 숫자 위");
        }

        [Test]
        public void 깃털은_날갯짓마다_둘_25부터_하나씩_넷까지()
        {
            Assert.AreEqual(2, FlapComboLook.FeatherCount(1));
            Assert.AreEqual(2, FlapComboLook.FeatherCount(24));
            Assert.AreEqual(3, FlapComboLook.FeatherCount(25));
            Assert.AreEqual(4, FlapComboLook.FeatherCount(50));
            Assert.AreEqual(4, FlapComboLook.FeatherCount(400));
        }

        [Test]
        public void 꼬리_길이는_연속이_길수록_조금_80에서_멈춘다()
        {
            Assert.AreEqual(0.15f + 25f / 160f, FlapComboLook.TrailLife(25), 1e-5f);
            Assert.AreEqual(0.65f, FlapComboLook.TrailLife(80), 1e-5f);
            Assert.AreEqual(0.65f, FlapComboLook.TrailLife(300), 1e-5f);
            Assert.Less(FlapComboLook.TrailWidth(25, 1f, U), 5f * U, "가늘게");
        }

        [Test]
        public void 고리는_몸_밖에서_시작해_퍼지며_사라진다()
        {
            Assert.Greater(FlapComboLook.RingRadius(0f, RPx, U), RPx, "새 그림을 덮지 않는다");
            Assert.Greater(FlapComboLook.RingRadius(1f, RPx, U), FlapComboLook.RingRadius(0f, RPx, U));
            Assert.Greater(FlapComboLook.RingWidth(0f, U), FlapComboLook.RingWidth(1f, U));
        }

        // ── 흐름 ───────────────────────────────────────────

        private static FlapComboFx Fx() => new FlapComboFx(new System.Random(7));

        [Test]
        public void 숫자는_그리는_장의_새_자리에_뜬다()
        {
            //  누른 순간(입력)엔 이번 장 보간 전이다 — 그 장 LateUpdate에서 지금 그려진 자리로 낸다(옛 자리 유령 금지, 10-26 디버그).
            var fx = Fx();
            fx.Queue(3);
            Assert.AreEqual(0, fx.Numbers.Count, "자리를 알기 전엔 안 낸다");

            var drawn = new Vector2(640f, 420f);
            fx.Resolve(drawn, RPx, U);

            Assert.AreEqual(1, fx.Numbers.Count);
            Assert.AreEqual(3, fx.Numbers[0].Count);
            Assert.AreEqual(FlapComboLook.NumberCorner(drawn, RPx, U), fx.Numbers[0].Corner);
        }

        [Test]
        public void 날갯짓마다_새_숫자가_겹쳐_뜨고_먼저_것은_제자리()
        {
            var fx = Fx();
            fx.Queue(1);
            fx.Resolve(Bird, RPx, U);
            fx.Advance(0.1f, U);
            var moved = Bird + new Vector2(0f, -60f);
            fx.Queue(2);
            fx.Resolve(moved, RPx, U);

            Assert.AreEqual(2, fx.Numbers.Count);
            Assert.AreEqual(FlapComboLook.NumberCorner(Bird, RPx, U), fx.Numbers[0].Corner, "먼저 뜬 것은 새를 따라 옮겨지지 않는다");
            Assert.AreEqual(FlapComboLook.NumberCorner(moved, RPx, U), fx.Numbers[1].Corner);
        }

        [Test]
        public void 새가_없는_장엔_밀린_날갯짓을_버린다()
        {
            var fx = Fx();
            fx.Queue(1);
            fx.DropPending();
            fx.Resolve(Bird, RPx, U);

            Assert.AreEqual(0, fx.Numbers.Count);
        }

        [Test]
        public void 숫자가_한꺼번에_많아도_풀을_넘지_않고_제일_오래된_것부터_비운다()
        {
            var fx = Fx();
            for (int n = 1; n <= FlapComboFx.MaxNumbers + 3; n++)
            {
                fx.Queue(n);
            }
            fx.Resolve(Bird, RPx, U);

            Assert.AreEqual(FlapComboFx.MaxNumbers, fx.Numbers.Count);
            Assert.AreEqual(4, fx.Numbers[0].Count);
        }

        [Test]
        public void 수명이_다하면_사라진다()
        {
            var fx = Fx();
            fx.Queue(10);
            fx.Resolve(Bird, RPx, U);
            Assert.IsTrue(fx.WordVisible);
            Assert.AreEqual("좋아!", fx.WordText);
            Assert.AreEqual(1, fx.Rings.Count);

            fx.Advance(FlapComboLook.NumberLife + 0.01f, U);
            Assert.AreEqual(0, fx.Numbers.Count, "0.45초");
            fx.Advance(FlapComboLook.RingLife, U);
            Assert.AreEqual(0, fx.Rings.Count, "0.6초");
            Assert.AreEqual(0, fx.Feathers.Count, "길어야 0.9초");
            fx.Advance(FlapComboLook.WordLife, U);
            Assert.IsFalse(fx.WordVisible, "1.1초");
        }

        [Test]
        public void 고비가_아니면_한마디도_고리도_없다()
        {
            var fx = Fx();
            fx.Queue(11);
            fx.Resolve(Bird, RPx, U);

            Assert.IsFalse(fx.WordVisible);
            Assert.AreEqual(0, fx.Rings.Count);
        }

        [Test]
        public void 깃털은_새_뒤에서_뒤로_흩날리며_떨어진다()
        {
            var fx = Fx();
            fx.Queue(30);
            fx.Resolve(Bird, RPx, U);

            Assert.AreEqual(FlapComboLook.FeatherCount(30), fx.Feathers.Count);
            foreach (var f in fx.Feathers)
            {
                Assert.LessOrEqual(f.Position.x, Bird.x - RPx + 1e-3f, "몸 뒤 끝에서");
                Assert.Less(f.Velocity.x, 0f, "뒤로");
                Assert.That(f.Life, Is.InRange(FlapComboLook.FeatherLifeMin, FlapComboLook.FeatherLifeMax));
            }
            float vy = fx.Feathers[0].Velocity.y;
            fx.Advance(0.2f, U);
            Assert.Greater(fx.Feathers[0].Velocity.y, vy, "중력으로 떨어진다(패널 y 아래로)");
        }

        [Test]
        public void 꼬리는_25부터_달리는_동안만_남고_기절하면_꺼진다()
        {
            var fx = Fx();
            fx.Queue(24);
            fx.Resolve(Bird, RPx, U);
            fx.RecordTrail(Vector3.zero, racing: true);
            Assert.AreEqual(0, fx.Trail.Count, "24는 아직");

            fx.Queue(25);
            fx.Resolve(Bird, RPx, U);
            fx.RecordTrail(Vector3.zero, racing: true);
            fx.RecordTrail(Vector3.right, racing: true);
            Assert.AreEqual(2, fx.Trail.Count);

            fx.RecordTrail(Vector3.right * 2f, racing: false);
            Assert.AreEqual(2, fx.Trail.Count, "결승 뒤엔 더 안 그린다");

            fx.Crash();
            Assert.AreEqual(0, fx.Trail.Count, "기절하면 꼬리를 끈다");
            Assert.AreEqual(0, fx.Count);
            fx.RecordTrail(Vector3.zero, racing: true);
            Assert.AreEqual(0, fx.Trail.Count, "다시 25까지");
        }

        [Test]
        public void 꼬리_점은_길이만큼만_남는다()
        {
            var fx = Fx();
            fx.Queue(25);
            fx.Resolve(Bird, RPx, U);
            fx.RecordTrail(Vector3.zero, racing: true);
            fx.Advance(FlapComboLook.TrailLife(25) + 0.01f, U);

            Assert.AreEqual(0, fx.Trail.Count);
        }

        [Test]
        public void 기절해도_떠_있는_숫자와_깃털은_조용히_제_수명대로()
        {
            var fx = Fx();
            fx.Queue(5);
            fx.Resolve(Bird, RPx, U);
            fx.Queue(6);
            fx.Crash();
            fx.Resolve(Bird, RPx, U);

            Assert.AreEqual(1, fx.Numbers.Count, "이미 뜬 것은 두고, 기절 뒤로 밀린 날갯짓은 버린다");
        }
    }
}
