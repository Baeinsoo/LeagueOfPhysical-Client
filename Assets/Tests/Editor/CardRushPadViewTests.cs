using System;
using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 카드 조작판 뷰의 배선 — 넘김(찌그러짐)은 뒷면 → 앞면에서만(M2, spec §7.3). 카드를 다음 입력 틱으로 칠하는 시험은
    /// 머리 위 카드(<see cref="CardRushHeadCard"/>)로 옮겼다 — 그 시험의 가짜 러너·세션은 이 파일의 git 기록(2d19e5dd)에 있다.
    /// </summary>
    public class CardRushPadViewTests
    {
        //  WindowManager.Open은 카탈로그에서 UXML을 찾는다 — 없으면 조작판이 안 열리고 모든 탭이 무시된다(Task 14 실제 판).
        [Test]
        public void 창_목록에_조작판이_있다()
        {
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<UIViewCatalog>("Assets/UI/UIViewCatalog.asset");
            Assert.IsTrue(catalog.TryGet(nameof(CardRushPadView), out var entry), "카탈로그에 CardRushPadView 매핑");
            Assert.IsNotNull(entry.uxml);
            Assert.IsNotNull(entry.uss);
        }

        //  가로(1920×1080 패널)·세로(1080×1920 화면 → 1920×3413 패널, Expand 축척)·길쭉한 폰.
        [TestCase(1920f, 1080f)]
        [TestCase(2400f, 1080f)]
        [TestCase(1920f, 3413f)]
        [TestCase(1920f, 4267f)]
        public void 조작판은_화면_아래_띠에_모이고_화면_밖으로_안_나간다(float w, float h)
        {
            var l = CardRushPadLayout.Compute(w, h);

            //  띠: 화면 아래 4분의 1 안(실제 판에서 0.48까지 덮어 내 몸이 숨었다).
            Assert.LessOrEqual(l.BandTop01, 0.25f, "띠 위 끝");
            Assert.AreEqual((l.Card.yMax) / h, l.BandTop01, 1e-4f, "띠 위 끝 = 카드 위 끝");
            Assert.LessOrEqual(l.Chips.yMax, l.Card.yMax + 1e-3f, "칩이 카드보다 높이 솟지 않는다");
            Assert.LessOrEqual(l.Streak.yMax, l.Card.yMax + 1e-3f, "연속 수도");

            //  카드는 여전히 크다 — 색과 모양을 한눈에(핵심 입력 되먹임).
            Assert.GreaterOrEqual(l.Card.height / h, 0.13f, "카드 높이");
            Assert.GreaterOrEqual(l.Card.width / w, 0.13f, "카드 폭");
            Assert.AreEqual(w * 0.5f, l.Card.center.x, 1e-3f, "가운데");

            //  칩은 카드 오른쪽, 연속 수는 왼쪽 — 겹치지 않고 화면 안.
            Assert.Greater(l.Chips.xMin, l.Card.xMax);
            Assert.LessOrEqual(l.Chips.xMax, w);
            Assert.Less(l.Streak.xMax, l.Card.xMin);
            Assert.GreaterOrEqual(l.Streak.xMin, 0f);
            Assert.Greater(l.NowChip, l.NextChip);
            Assert.AreEqual(l.NowChip + 2f * l.NextChip + 2f * l.ChipGap, l.Chips.width, 1e-3f, "칩 줄 폭 = 칩 셋 + 사이 둘");
        }

        //  뒷면에 인라인 색을 남기면 USS 중립 바탕을 덮는다 — 틀린 직후 카드가 "뒷면 무늬 + 그 틀린 색"으로 남았다(실제 판).
        [Test]
        public void 뒷면은_인라인_색을_비워_중립_바탕을_보인다()
        {
            var back = new CardRushCardFace(false, CardColor.Red, Array.Empty<CardColor>(), 0);
            var red = new CardRushCardFace(true, CardColor.Red, Array.Empty<CardColor>(), 0);

            Assert.AreEqual(UnityEngine.UIElements.StyleKeyword.Null, CardRushPadView.CardBackground(back).keyword);
            Assert.AreEqual(UnityEngine.UIElements.StyleKeyword.Undefined, CardRushPadView.CardBackground(red).keyword);
            Assert.Greater(CardRushPadView.CardBackground(red).value.r, 0.5f);
        }

        [Test]
        public void 뒷면에서_앞면이_될_때만_넘긴다()
        {
            var red = new CardRushCardFace(true, CardColor.Red, Array.Empty<CardColor>(), 0);
            var yellow = new CardRushCardFace(true, CardColor.Yellow, Array.Empty<CardColor>(), 0);
            var back = new CardRushCardFace(false, default, Array.Empty<CardColor>(), 0);

            Assert.IsTrue(CardRushPadView.ShouldFlip(lastFaceUp: false, red), "뒷면 → 앞면");
            Assert.IsFalse(CardRushPadView.ShouldFlip(lastFaceUp: true, yellow),
                "앞면인 채 색만 바뀌면 접지 않는다 — 새 색이 보이는 첫 순간을 가리면 안 된다");
            Assert.IsFalse(CardRushPadView.ShouldFlip(lastFaceUp: true, red));
            Assert.IsFalse(CardRushPadView.ShouldFlip(lastFaceUp: true, back), "앞면 → 뒷면");
        }
    }
}
