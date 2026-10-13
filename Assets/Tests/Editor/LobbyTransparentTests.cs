using System.IO;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>로비 가운데는 3D 터널이 그린다 — 로비 UI는 2D 무대와 배경을 갖지 않는다.</summary>
    public class LobbyTransparentTests
    {
        [Test]
        public void 로비_UI엔_2D_무대가_없다()
        {
            StringAssert.DoesNotContain("kit-stage", File.ReadAllText("Assets/UI/LobbyHome/LobbyHomeView.uxml"));
        }

        [Test]
        public void 로비_배경은_투명하고_랭크_배경색_규칙이_없다()
        {
            var uss = File.ReadAllText("Assets/UI/LobbyHome/LobbyHomeView.uss");
            StringAssert.Contains(".lobbyhome-root { flex-grow: 1; background-color: rgba(0, 0, 0, 0); }", uss);
            StringAssert.DoesNotContain(".ranked.lobbyhome-root", uss);
        }
    }
}
