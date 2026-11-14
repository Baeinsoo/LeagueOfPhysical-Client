using LOP.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace LOP.Tests
{
    /// <summary>
    /// 카드 조작판(탭 면 + "무사 N")의 배선. 카드를 다음 입력 틱으로 칠하는 시험은 머리 위 카드(<see cref="CardRushHeadCardTests"/>)로 옮겼다.
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

        //  2차(spec 2026-11-03 §5.2): 아래 띠의 큰 카드·요구 색 칩을 지웠다 — 카드는 머리 위로. 탭 면과 "연속 N"만 남는다.
        [Test]
        public void 조작판엔_카드와_칩이_없다()
        {
            var uxml = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/CardRushPad/CardRushPad.uxml");
            Assert.IsNotNull(uxml);
            var root = uxml.Instantiate();
            Assert.IsNull(root.Q("card-rush-card"), "큰 카드");
            Assert.IsNull(root.Q("card-rush-next"), "요구 색 칩");
            Assert.IsNull(root.Q("card-rush-surface"), "4.2차: 화면 전체 탭 면은 없다 — 카드 버튼만 받는다");
            Assert.IsNotNull(root.Q<Label>("card-rush-streak"), "무사 글");
        }

        //  3차(spec 2026-11-05 §11.5): 아래 글은 지금 연속 무사 통과 — 2차 "연속 N"(정타) 자리.
        [Test]
        public void 아래_글은_무사_N()
        {
            Assert.AreEqual("무사 7", CardRushPadView.StreakText(7));
            var uxml = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/CardRushPad/CardRushPad.uxml");
            Assert.AreEqual("카드 버튼: 돌리기 → 고르기 → 쓰기", CardRushPadView.HintText);
            Assert.AreEqual(CardRushPadView.HintText, uxml.Instantiate().Q<Label>("card-rush-hint").text, "조작 안내");
            Assert.AreEqual("무사 0", uxml.Instantiate().Q<Label>("card-rush-streak").text, "판 시작 전 글");
        }

        //  4.2차: 오른쪽 아래 카드 버튼이 내 카드 모습을 그린다 — 모습별 바탕·크기·링 굵기.
        [Test]
        public void 카드_버튼은_모습을_그린다()
        {
            var ready = new CardRushHeadCardFace(true, false, default, false, 1f, ready: true, readyBig: true);
            Assert.AreEqual(CardRushHeadCardView.ReadyScale, CardRushCardButton.ScaleOf(ready), "평소 숨쉬기 커진 반");
            Assert.AreEqual(CardRushCardMaterials.Back, CardRushCardButton.FillOf(ready), "평소 = 회색 뒷면");
            Assert.AreEqual(0f, CardRushCardButton.RingWidthOf(ready), "평소엔 링 없음");

            var rolling = new CardRushHeadCardFace(true, true, CardColor.Blue, false, 1f, ringT01: 0.5f, rolling: true);
            Assert.AreEqual(CardRushCardPalette.ColorOf(CardColor.Blue), CardRushCardButton.FillOf(rolling), "도는 색");
            Assert.AreEqual(CardRushCardButton.ThinRingWidthOfHalf, CardRushCardButton.RingWidthOf(rolling), "도는 6초 링은 가늘게");

            var held = new CardRushHeadCardFace(true, true, CardColor.Red, false, 1f, held: true, ringT01: 0.5f);
            Assert.AreEqual(CardRushCardButton.RingWidthOfHalf, CardRushCardButton.RingWidthOf(held), "잡은 5초 링은 굵게");
            var blink = new CardRushHeadCardFace(true, true, CardColor.Red, false, 1f, held: true, ringT01: 0.1f, ringBlink: true);
            Assert.AreEqual(0f, CardRushCardButton.RingWidthOf(blink), "깜빡 꺼짐");

            var dim = new CardRushHeadCardFace(true, false, default, true, 1f);
            Assert.AreEqual(CardRushCardButton.DimAlpha, CardRushCardButton.FillOf(dim).a, "쿨다운·기절은 흐리게");
            var vanish = new CardRushHeadCardFace(true, false, default, true, 1f, vanish01: 0.75f);
            Assert.AreEqual(0.25f, CardRushCardButton.ScaleOf(vanish), 1e-5f, "사라지며 작아진다");

            var button = new CardRushCardButton();
            button.SetFace(default);
            Assert.AreEqual(Visibility.Hidden, button.style.visibility.value, "완주 뒤엔 숨긴다");
            button.SetFace(held);
            Assert.AreEqual(Visibility.Visible, button.style.visibility.value);
            Assert.AreEqual(PickingMode.Position, button.pickingMode, "탭을 받는다");
        }

        //  4.2차: 머리 위엔 돌거나 잡은 동안만 — 평소·쿨다운 뒷면은 버튼에만.
        [Test]
        public void 머리_위엔_돌거나_잡았을_때만_뜬다()
        {
            Assert.IsFalse(CardRushHeadCardView.ShownOverHead(new CardRushHeadCardFace(true, false, default, false, 1f, ready: true)), "평소");
            Assert.IsFalse(CardRushHeadCardView.ShownOverHead(new CardRushHeadCardFace(true, false, default, true, 1f)), "쿨다운");
            Assert.IsFalse(CardRushHeadCardView.ShownOverHead(default), "숨김");
            Assert.IsTrue(CardRushHeadCardView.ShownOverHead(new CardRushHeadCardFace(true, true, CardColor.Red, false, 1f, rolling: true)), "돎");
            Assert.IsTrue(CardRushHeadCardView.ShownOverHead(new CardRushHeadCardFace(true, true, CardColor.Red, false, 1f, held: true)), "잡음");
            Assert.IsTrue(CardRushHeadCardView.ShownOverHead(new CardRushHeadCardFace(true, false, default, true, 1f, vanish01: 0.3f)), "사라지는 중");
        }
    }
}
