using LOP.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace LOP.Tests
{
    /// <summary>정보 HUD 화면: 요소가 다 있고, 아래 엄지 입력면(FlapPad)을 막지 않으며, 창 목록에 올라 있다.</summary>
    public class RaceHudViewTests
    {
        private const string Uxml = "Assets/UI/RaceHud/RaceHud.uxml";

        private static readonly string[] Names =
        {
            "race-hud-root", "race-clock-plate", "race-clock", "race-progress", "race-progress-track",
            "race-progress-bands", "race-progress-fill", "race-progress-goal", "race-progress-dots",
            "race-rank-plate", "race-rank", "race-rank-unit", "race-rank-total", "race-rank-delta",
            "race-banner", "race-banner-act", "race-banner-title", "race-banner-bar",
            "race-stretch", "race-stretch-text",
        };

        [Test]
        public void 요소가_다_있다()
        {
            var root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Uxml).Instantiate();
            foreach (var name in Names)
            {
                Assert.IsNotNull(root.Q(name), name);
            }
        }

        [Test]
        public void 어떤_요소도_입력을_잡지_않는다()
        {
            //  이 창은 FlapPad 위에 열린다 — 하나라도 입력을 잡으면 그 자리를 눌러도 날갯짓이 안 나간다.
            var root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Uxml).Instantiate();
            foreach (var name in Names)
            {
                Assert.AreEqual(PickingMode.Ignore, root.Q(name).pickingMode, name);
            }
        }

        [Test]
        public void 구간_띠는_내_진행_채움_아래에_깔린다()
        {
            //  UI Toolkit은 형제 순서대로 그린다 — 띠가 채움 뒤에 오면 내 진행 호박색을 막 색이 덮는다.
            var root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Uxml).Instantiate();
            var track = root.Q("race-progress-track");

            Assert.Less(track.IndexOf(root.Q("race-progress-bands")), track.IndexOf(root.Q("race-progress-fill")));
        }

        [Test]
        public void 창_목록에_정보_HUD가_있다()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>("Assets/UI/UIViewCatalog.asset");
            Assert.IsTrue(catalog.TryGet(nameof(RaceHudView), out var entry));
            Assert.IsNotNull(entry.uxml);
            Assert.IsNotNull(entry.uss);
        }
    }
}
