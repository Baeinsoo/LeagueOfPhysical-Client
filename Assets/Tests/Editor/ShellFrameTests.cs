using System.IO;
using Cysharp.Threading.Tasks;
using LOP.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;
using VContainer;

namespace LOP.Tests
{
    /// <summary>메뉴 공통 셸 틀 — 머리띠(뒤로·제목·코인)와 아래 LIVE 띠. 셸 화면마다 같은 마크업을 쓴다.</summary>
    public class ShellFrameTests
    {
        private const string Uxml = "Assets/UI/Shell/ShellView.uxml";
        private const string Uss = "Assets/UI/Shell/ShellView.uss";

        /// <summary>셸 화면 UXML이 키트 루트와 머리띠·띠 요소를 다 갖췄나. 상점·라커룸·선수 카드 시험도 쓴다.</summary>
        public static void AssertShellFrame(string uxml)
        {
            foreach (var n in new[] { "shell-root", "back-button", "shell-title", "shell-coin", "shell-coin-text", "shell-body", "shell-ticker" })
            {
                StringAssert.Contains($"name=\"{n}\"", uxml);
            }
            StringAssert.Contains("class=\"kit shell-root", uxml);
        }

        [Test]
        public void 셸_틀_요소() => AssertShellFrame(File.ReadAllText(Uxml));

        [Test]
        public void 셸_틀_규칙()
        {
            var s = File.ReadAllText(Uss);
            StringAssert.Contains(".shell-head", s);
            StringAssert.Contains(".shell-ticker", s);
            StringAssert.DoesNotContain("#000", s);
        }

        [Test]
        public void 코인_스토어가_없으면_칩을_숨긴다()
        {
            Assert.IsFalse(ShellView.CoinVisible(null));
            Assert.IsTrue(ShellView.CoinVisible(new EconomyStore(null, null, (u, c) => default)));
        }

        [Test]
        public void 컨테이너로_만든_셸은_코인_칩을_보인다()
        {
            var builder = new ContainerBuilder();
            builder.RegisterInstance(new EconomyStore(null, null, (u, c) => UniTask.FromResult<GetEconomyResponse>(null)));
            builder.Register<SettingsView>(Lifetime.Transient);
            using var container = builder.Build();
            var view = container.Resolve<SettingsView>();

            var root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Uxml).Instantiate();
            view.Initialize(root);
            view.OnOpen();

            Assert.AreEqual(DisplayStyle.Flex, root.Q("shell-coin").style.display.value);
            Assert.AreEqual("🪙 -", root.Q<Label>("shell-coin-text").text, "받기 전엔 로비 칩처럼 '-'");
            Assert.AreEqual("설정", root.Q<Label>("shell-title").text);
            view.Dispose();
        }
    }
}
