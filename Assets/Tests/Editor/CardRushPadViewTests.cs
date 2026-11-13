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
            Assert.IsNotNull(root.Q("card-rush-surface"), "탭 면");
            Assert.IsNotNull(root.Q<Label>("card-rush-streak"), "무사 글");
        }

        //  3차(spec 2026-11-05 §11.5): 아래 글은 지금 연속 무사 통과 — 2차 "연속 N"(정타) 자리.
        [Test]
        public void 아래_글은_무사_N()
        {
            Assert.AreEqual("무사 7", CardRushPadView.StreakText(7));
            var uxml = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/CardRushPad/CardRushPad.uxml");
            Assert.AreEqual("탭 = 카드 잡기 · 다시 탭 = 쓰기", CardRushPadView.HintText);
            Assert.AreEqual(CardRushPadView.HintText, uxml.Instantiate().Q<Label>("card-rush-hint").text, "조작 안내");
            Assert.AreEqual("무사 0", uxml.Instantiate().Q<Label>("card-rush-streak").text, "판 시작 전 글");
        }
    }
}
