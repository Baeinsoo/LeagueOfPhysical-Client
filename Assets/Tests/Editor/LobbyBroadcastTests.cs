using System.IO;
using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>랭크에서만 보이는 방송 자막·중계 표시.</summary>
    public class LobbyBroadcastTests
    {
        [Test]
        public void 자막과_중계_표시가_로비에_있다()
        {
            var uxml = File.ReadAllText("Assets/UI/LobbyHome/LobbyHomeView.uxml");
            StringAssert.Contains("name=\"lower-third\"", uxml);
            StringAssert.Contains("name=\"lower-chips\"", uxml);
            StringAssert.Contains("name=\"broadcast\"", uxml);
            StringAssert.Contains("lobbyhome-corner--tl", uxml);
        }

        [Test]
        public void 랭크에서만_보인다()
        {
            var uss = File.ReadAllText("Assets/UI/LobbyHome/LobbyHomeView.uss");
            StringAssert.Contains(".lobbyhome-lower { position: absolute;", uss);
            StringAssert.Contains(".ranked .lobbyhome-lower { translate: 0 0; }", uss);
            //  % 이동은 자막 폭에 따라 달라 짧은 자막이 화면 왼쪽에 남는다 — 고정 거리로 밀어 낸다.
            StringAssert.Contains("translate: -800px 0;", uss);
            StringAssert.Contains(".ranked .lobbyhome-broadcast { opacity: 1; }", uss);
        }

        [Test]
        public void 순위_칩_글자()
        {
            Assert.AreEqual("1위", LobbyHomeView.PlacementChipText(1));
            Assert.AreEqual("4위", LobbyHomeView.PlacementChipText(4));
        }
    }
}
