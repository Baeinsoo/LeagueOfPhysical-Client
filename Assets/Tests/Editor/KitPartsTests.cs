using System.IO;
using LOP.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace LOP.Tests
{
    /// <summary>3부(타이틀)가 쓰는 키트 부품 — 무대·진행 막대·자막 띠·큰 판.</summary>
    public class KitPartsTests
    {
        private static string KitUss => File.ReadAllText("Assets/UI/Kit/Kit.uss");

        [Test]
        public void 무대는_키트에_있고_로비_USS엔_없다()
        {
            StringAssert.Contains(".kit-stage__light", KitUss);
            StringAssert.Contains(".ranked .kit-stage__glow", KitUss);
            StringAssert.DoesNotContain("lobbyhome-stage", File.ReadAllText("Assets/UI/LobbyHome/LobbyHomeView.uss"));
            StringAssert.DoesNotContain("lobbyhome-stage", File.ReadAllText("Assets/UI/LobbyHome/LobbyHomeView.uxml"));
            StringAssert.Contains("kit-stage__floor", File.ReadAllText("Assets/UI/LobbyHome/LobbyHomeView.uxml"));
        }

        [Test]
        public void 진행_막대_자막_띠_큰_판이_키트에_있다()
        {
            StringAssert.Contains(".kit-progress__fill", KitUss);
            StringAssert.Contains(".kit-lt__tag", KitUss);
            StringAssert.Contains(".kit-panel__face", KitUss);
        }

        [Test]
        public void 진행_비율은_0과_1_사이로_자른다()
        {
            Assert.AreEqual(0f, KitProgress.Clamp01(-0.2f));
            Assert.AreEqual(0.4f, KitProgress.Clamp01(0.4f), 1e-6);
            Assert.AreEqual(1f, KitProgress.Clamp01(1.7f));
            Assert.AreEqual(0f, KitProgress.Clamp01(float.NaN));
        }

        [Test]
        public void 채움_폭은_비율만큼_퍼센트로_준다()
        {
            var fill = new VisualElement();
            KitProgress.SetRatio(fill, 0.6f);
            Assert.AreEqual(LengthUnit.Percent, fill.style.width.value.unit);
            Assert.AreEqual(60f, fill.style.width.value.value, 1e-4);
        }
    }
}
