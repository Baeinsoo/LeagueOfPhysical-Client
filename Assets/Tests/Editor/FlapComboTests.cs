using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 연속 날갯짓 연출 "집중형"(spec §7, 사용자 결정 10-26). 앞길과 화면 가운데를 비운다 — 작은 "+N"은 새 뒤쪽 위에서 0.45초,
    /// 깃털은 새 뒤로만, 꼬리는 30부터 가늘게, 게이지를 주는 걸음(10마다) 한마디·고리는 새 옆에 작게(50과 그 뒤 50마다만 조금 크게). 단위 u는 시제품 무대(높이 545px)의 1px.
    /// </summary>
    public class FlapComboTests
    {
        private const float U = 2f;       //  1090px 높이 화면
        private const float RPx = 18f;    //  새 반지름(화면 px)
        private static readonly Vector2 Bird = new Vector2(600f, 500f);

        private static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c).ToLowerInvariant();

        [Test]
        public void 단계_색은_10_30_50에서_바뀐다()
        {
            Assert.AreEqual("#fff6e6", Hex(FlapComboLook.TierColor(1)));
            Assert.AreEqual("#fff6e6", Hex(FlapComboLook.TierColor(9)));
            Assert.AreEqual("#ffd257", Hex(FlapComboLook.TierColor(10)));
            Assert.AreEqual("#ffd257", Hex(FlapComboLook.TierColor(29)), "리뷰2 M3 — 단계는 화면 고비(30)에 맞춘다");
            Assert.AreEqual("#ff9a3c", Hex(FlapComboLook.TierColor(30)));
            Assert.AreEqual("#ff6a3d", Hex(FlapComboLook.TierColor(50)));
            Assert.AreEqual("#ff6a3d", Hex(FlapComboLook.TierColor(300)));
            Assert.AreEqual("#2a1a0c", Hex(FlapComboLook.Outline));
            Assert.AreEqual(0.6f, FlapComboLook.TrailColor(29, 1f).a, 1e-5f);
            Assert.AreEqual(0.7f, FlapComboLook.TrailColor(30, 1f).a, 1e-5f);
            Assert.AreEqual(30, FlapComboLook.TrailFrom);
        }

        [Test]
        public void 기본_규칙은_게이지를_주는_10마다_한마디가_뜨고_단계는_30_50_100에서_오른다()
        {
            //  11-02 — 10만이 아니라 게이지를 주는 모든 걸음(10마다)이 한마디를 받는다. 단계: 10~20·30~40·50~90·100부터.
            string Word(int n) => FlapComboLook.WordAt(FlapComboLook.StepTier(FlappyFlapComboRules.Default, n));
            Assert.AreEqual("좋아!", Word(10));
            Assert.AreEqual("좋아!", Word(20));
            Assert.AreEqual("멋져!", Word(30));
            Assert.AreEqual("멋져!", Word(40));
            Assert.AreEqual("미쳤다!", Word(50));
            Assert.AreEqual("미쳤다!", Word(90));
            Assert.AreEqual("전설!", Word(100));
            Assert.AreEqual("전설!", Word(110));
            Assert.AreEqual("전설!", Word(150));
            Assert.AreEqual("전설!", Word(310));
            foreach (int n in new[] { 0, 1, 9, 11, 19, 21, 25, 49, 75, 99, 101, 149, 175, 201 })
            {
                Assert.IsNull(Word(n), n.ToString());
            }
        }

        [Test]
        public void 크게는_50과_그뒤_50마다만()
        {
            var rules = FlappyFlapComboRules.Default;
            foreach (int n in new[] { 50, 100, 150, 200, 250 })
            {
                Assert.IsTrue(FlapComboLook.StepBig(rules, n), n.ToString());
            }
            foreach (int n in new[] { 10, 20, 30, 40, 60, 90, 110, 140, 160, 49, 51 })
            {
                Assert.IsFalse(FlapComboLook.StepBig(rules, n), n.ToString());
            }
        }

        [Test]
        public void 보상_걸음은_맵_규칙의_간격을_본다()
        {
            //  간격 4인 맵: 4·8·12…가 걸음이고 10은 아니다. 보상 없는 맵은 아무것도 아니다.
            FlappyFlapComboRules.TryCreate(4, new[] { 0.1f }, 0.1f, out var four);
            Assert.AreEqual(0, FlapComboLook.StepTier(four, 4));
            Assert.AreEqual(1, FlapComboLook.StepTier(four, 32));
            Assert.AreEqual(-1, FlapComboLook.StepTier(four, 10));
            Assert.IsFalse(FlapComboLook.StepBig(four, 50), "50은 이 맵의 걸음이 아니다");
            Assert.IsTrue(FlapComboLook.StepBig(four, 100));
            Assert.AreEqual(-1, FlapComboLook.StepTier(FlappyFlapComboRules.Off, 10));
            Assert.IsFalse(FlapComboLook.StepBig(FlappyFlapComboRules.Off, 50));
        }

        [Test]
        public void 한마디는_단계_순서로_고르고_넷째_뒤는_전설()
        {
            //  단계 번호(StepTier)로 고른다.
            Assert.AreEqual("좋아!", FlapComboLook.WordAt(0));
            Assert.AreEqual("멋져!", FlapComboLook.WordAt(1));
            Assert.AreEqual("미쳤다!", FlapComboLook.WordAt(2));
            Assert.AreEqual("전설!", FlapComboLook.WordAt(3));
            Assert.AreEqual("전설!", FlapComboLook.WordAt(5));
            Assert.IsNull(FlapComboLook.WordAt(-1));
        }

        [Test]
        public void 대시_보상이면_한마디_뒤에_작은_대시플러스()
        {
            //  대시 칸 색(FlapPad.uss dash-slot--filled #ffaa28), 한마디의 60% — 새 판·번쩍임 없이 글자 하나(절제).
            Assert.AreEqual("좋아! <size=60%><color=#FFAA28>대시+</color></size>", FlapComboLook.WordWithCue("좋아!", true));
            Assert.AreEqual("좋아!", FlapComboLook.WordWithCue("좋아!", false));
            Assert.IsNull(FlapComboLook.WordWithCue(null, true));
        }

        [Test]
        public void 숫자는_0_6에서_1_3으로_튀었다가_30퍼센트에_1배()
        {
            //  10-27 피드백: 더 묵직하게 — 정점을 1.2에서 1.3으로 올렸다(같은 타이밍).
            Assert.AreEqual(0.6f, FlapComboLook.NumberPop(0f), 1e-4f);
            Assert.AreEqual(1.3f, FlapComboLook.NumberPop(0.15f), 1e-4f);
            Assert.AreEqual(1.15f, FlapComboLook.NumberPop(0.225f), 1e-4f);
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
        public void 숫자_자리는_몸_지름만큼_더_위로_올라갔다()
        {
            //  10-27 피드백: 더 높게 — 몸 꼭대기 위 2u에서, 몸 지름(2r)만큼 더 위로. 가로는 그대로(반지름 반만큼 뒤).
            Vector2 number = FlapComboLook.NumberCorner(Bird, RPx, U);
            Assert.AreEqual(Bird.x - 0.5f * RPx, number.x, 1e-4f);
            Assert.AreEqual(Bird.y - 3f * RPx - 2f * U, number.y, 1e-4f);

            //  한마디는 숫자 자리를 그대로 따라가므로 같이 올라간다 — 둘 사이 간격(WordAbove)은 그대로.
            Vector2 word = FlapComboLook.WordCorner(Bird, RPx, U);
            Assert.AreEqual(number.y - FlapComboLook.WordAbove * U, word.y, 1e-4f);
        }

        [Test]
        public void 숫자는_글자가_크고_테두리가_두껍고_그림자가_있다()
        {
            //  10-27 피드백: 더 묵직하게 — 기존 30u의 1.3배, 테두리는 한마디(0.1)보다 두껍게(0.16), 그림자는 숫자에만.
            Assert.AreEqual(39f, FlapComboLook.NumberFont, 1e-4f);
            Assert.AreEqual(0.16f, FlapComboLook.NumberOutlineRatio, 1e-4f);
            Assert.AreEqual(0.1f, FlapComboLook.WordOutlineRatio, 1e-4f);
            Assert.Greater(FlapComboLook.NumberOutlineRatio, FlapComboLook.WordOutlineRatio);
            Assert.AreEqual(0.06f, FlapComboLook.NumberShadowOffsetRatio, 1e-4f);
            Assert.Greater(FlapComboLook.NumberShadowOffsetRatio, 0f);
            Assert.Greater(FlapComboLook.NumberShadowBlurRatio, 0f);
            Assert.AreEqual(0.6f, FlapComboLook.NumberShadow.a, 1e-4f, "은은하게 — 테두리처럼 진하지 않다");
            Assert.AreEqual(FlapComboLook.Outline.r, FlapComboLook.NumberShadow.r, 1e-4f, "테두리와 같은 어두운 색");
            Assert.AreEqual(FlapComboLook.Outline.g, FlapComboLook.NumberShadow.g, 1e-4f);
            Assert.AreEqual(FlapComboLook.Outline.b, FlapComboLook.NumberShadow.b, 1e-4f);
        }

        [Test]
        public void 깃털은_날갯짓마다_둘_30부터_셋_50부터_넷()
        {
            Assert.AreEqual(2, FlapComboLook.FeatherCount(1));
            Assert.AreEqual(2, FlapComboLook.FeatherCount(29));
            Assert.AreEqual(3, FlapComboLook.FeatherCount(30));
            Assert.AreEqual(3, FlapComboLook.FeatherCount(49));
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

        //  시험용 걸음 — 판은 맵 규칙으로 단계를 정한다(FlappyFlapStreak). 여기선 기본 규칙으로 정한다.
        private static void Beat(FlapComboFx fx, int n)
            => fx.Queue(n, FlapComboLook.StepTier(FlappyFlapComboRules.Default, n), false, FlapComboLook.StepBig(FlappyFlapComboRules.Default, n));

        [Test]
        public void 큰_걸음은_한마디와_고리를_크게_작은_걸음은_10처럼()
        {
            var small = Fx();
            Beat(small, 20);
            small.Resolve(Bird, RPx, U);
            Assert.AreEqual("좋아!", small.WordText, "20도 10처럼 한마디");
            Assert.AreEqual(1, small.Rings.Count);
            Assert.IsFalse(small.WordBig);
            Assert.IsFalse(small.Rings[0].Big);

            var big = Fx();
            Beat(big, 50);
            big.Resolve(Bird, RPx, U);
            Assert.AreEqual("미쳤다!", big.WordText);
            Assert.IsTrue(big.WordBig);
            Assert.IsTrue(big.Rings[0].Big);
            Assert.Greater(FlapComboLook.RingRadius(1f, RPx, U, big: true), FlapComboLook.RingRadius(1f, RPx, U));
            Assert.AreEqual(RPx + 4f * U, FlapComboLook.RingRadius(0f, RPx, U, big: true), 1e-4f, "시작 자리는 같다");

            var notStep = Fx();
            notStep.Queue(55, -1, false, big: true);   //  걸음이 아니면 크게도 없다
            notStep.Resolve(Bird, RPx, U);
            Assert.AreEqual(0, notStep.Rings.Count);
            Assert.IsFalse(notStep.WordBig);
        }

        [Test]
        public void 숫자는_그리는_장의_새_자리에_뜬다()
        {
            //  누른 순간(입력)엔 이번 장 보간 전이다 — 그 장 LateUpdate에서 지금 그려진 자리로 낸다(옛 자리 유령 금지, 10-26 디버그).
            var fx = Fx();
            Beat(fx, 3);
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
            Beat(fx, 1);
            fx.Resolve(Bird, RPx, U);
            fx.Advance(0.1f, U);
            var moved = Bird + new Vector2(0f, -60f);
            Beat(fx, 2);
            fx.Resolve(moved, RPx, U);

            Assert.AreEqual(2, fx.Numbers.Count);
            Assert.AreEqual(FlapComboLook.NumberCorner(Bird, RPx, U), fx.Numbers[0].Corner, "먼저 뜬 것은 새를 따라 옮겨지지 않는다");
            Assert.AreEqual(FlapComboLook.NumberCorner(moved, RPx, U), fx.Numbers[1].Corner);
        }

        [Test]
        public void 새가_없는_장엔_밀린_날갯짓을_버린다()
        {
            var fx = Fx();
            Beat(fx, 1);
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
                Beat(fx, n);
            }
            fx.Resolve(Bird, RPx, U);

            Assert.AreEqual(FlapComboFx.MaxNumbers, fx.Numbers.Count);
            Assert.AreEqual(4, fx.Numbers[0].Count);
        }

        [Test]
        public void 수명이_다하면_사라진다()
        {
            var fx = Fx();
            Beat(fx, 10);
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
        public void 보상_걸음이_아니면_한마디도_고리도_없다()
        {
            var fx = Fx();
            Beat(fx, 11);
            fx.Resolve(Bird, RPx, U);

            Assert.IsFalse(fx.WordVisible);
            Assert.AreEqual(0, fx.Rings.Count);
        }

        [Test]
        public void 한마디는_수가_아니라_넘겨받은_단계로_뜬다()
        {
            var atThree = Fx();
            atThree.Queue(3, 0, true);   //  간격 3인 맵의 첫 걸음
            atThree.Resolve(Bird, RPx, U);
            Assert.IsTrue(atThree.WordVisible);
            Assert.AreEqual("좋아!", atThree.WordText);
            Assert.IsTrue(atThree.WordDashCue);
            Assert.AreEqual(1, atThree.Rings.Count);

            var tenNotMilestone = Fx();
            tenNotMilestone.Queue(10, -1, false);   //  그 맵에선 10이 보상 걸음이 아니다
            tenNotMilestone.Resolve(Bird, RPx, U);
            Assert.IsFalse(tenNotMilestone.WordVisible);
            Assert.AreEqual(0, tenNotMilestone.Rings.Count);
        }

        [Test]
        public void 게이지를_안_준_걸음엔_대시플러스가_없다()
        {
            var fx = Fx();
            fx.Queue(30, 1, false);   //  버튼 없는 맵이거나 게이지가 가득이었다
            fx.Resolve(Bird, RPx, U);

            Assert.AreEqual("멋져!", fx.WordText);
            Assert.IsFalse(fx.WordDashCue);
        }

        [Test]
        public void 깃털은_새_뒤에서_뒤로_흩날리며_떨어진다()
        {
            var fx = Fx();
            Beat(fx, 30);
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
        public void 꼬리는_30부터_달리는_동안만_남고_기절하면_꺼진다()
        {
            var fx = Fx();
            Beat(fx, 29);
            fx.Resolve(Bird, RPx, U);
            fx.RecordTrail(Vector3.zero, racing: true);
            Assert.AreEqual(0, fx.Trail.Count, "29는 아직");

            Beat(fx, 30);
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
            Assert.AreEqual(0, fx.Trail.Count, "다시 30까지");
        }

        [Test]
        public void 꼬리_점은_길이만큼만_남는다()
        {
            var fx = Fx();
            Beat(fx, 30);
            fx.Resolve(Bird, RPx, U);
            fx.RecordTrail(Vector3.zero, racing: true);
            fx.Advance(FlapComboLook.TrailLife(30) + 0.01f, U);

            Assert.AreEqual(0, fx.Trail.Count);
        }

        [Test]
        public void 기절해도_떠_있는_숫자와_깃털은_조용히_제_수명대로()
        {
            var fx = Fx();
            Beat(fx, 5);
            fx.Resolve(Bird, RPx, U);
            Beat(fx, 6);
            fx.Crash();
            fx.Resolve(Bird, RPx, U);

            Assert.AreEqual(1, fx.Numbers.Count, "이미 뜬 것은 두고, 기절 뒤로 밀린 날갯짓은 버린다");
        }
    }
}
