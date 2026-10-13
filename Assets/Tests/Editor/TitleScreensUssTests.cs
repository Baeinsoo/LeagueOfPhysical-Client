using System.IO;
using LOP.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace LOP.Tests
{
    /// <summary>로그인·연결 실패 — 키트로 바뀌었고, View가 찾는 이름은 그대로인지.</summary>
    public class TitleScreensUssTests
    {
        [Test]
        public void 옛_그림과_옛_카드를_쓰지_않는다()
        {
            foreach (var p in new[] { "Assets/UI/Login/LoginView.uss", "Assets/UI/ServerUnreachable/ServerUnreachableView.uss" })
                StringAssert.DoesNotContain("title_screen", File.ReadAllText(p));
            foreach (var p in new[] { "Assets/UI/Login/LoginView.uxml", "Assets/UI/ServerUnreachable/ServerUnreachableView.uxml" })
            {
                var x = File.ReadAllText(p);
                StringAssert.Contains("class=\"kit ", x);
                StringAssert.DoesNotContain("\"card ", x);
                StringAssert.DoesNotContain("btn--primary", x);
            }
        }

        [Test]
        public void 로그인_화면이_찾는_이름이_있다()
        {
            var root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Login/LoginView.uxml").Instantiate();
            Assert.IsNotNull(root.Q<Button>("guest-login"));
            Assert.IsNotNull(root.Q<Button>("gpgs-login"));
            Assert.IsNotNull(root.Q<Button>("gamecenter-login"));
            Assert.IsNotNull(root.Q<Label>("login-error"));
        }

        [Test]
        public void 연결_실패_화면이_찾는_이름이_있다()
        {
            var root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/ServerUnreachable/ServerUnreachableView.uxml").Instantiate();
            Assert.IsNotNull(root.Q<Label>("unreachable-message"));
            Assert.IsNotNull(root.Q<Button>("unreachable-retry"));
        }
    }
}
