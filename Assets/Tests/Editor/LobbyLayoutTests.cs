using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 패널은 폭을 1920에 맞춘다 — 20:9 폰이면 높이가 약 864라 16:9 배치로는 오른쪽 줄과 PLAY가 겹친다.
    /// 그때 로비를 낮은 배치(.short)로 바꾼다.
    /// </summary>
    public class LobbyLayoutTests
    {
        [Test] public void 열여섯대구는_보통_배치() => Assert.IsFalse(LobbyLayout.IsShort(1080f));
        [Test] public void 이십대구는_낮은_배치() => Assert.IsTrue(LobbyLayout.IsShort(864f));
        [Test] public void 이십일대구도_낮은_배치() => Assert.IsTrue(LobbyLayout.IsShort(823f));
        [Test] public void 높이를_모르면_보통_배치() => Assert.IsFalse(LobbyLayout.IsShort(float.NaN));
    }
}
