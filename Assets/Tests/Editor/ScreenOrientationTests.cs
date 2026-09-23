using NUnit.Framework;
using UnityEditor;

namespace LOP.Tests
{
    /// <summary>
    /// 이 게임은 언제나 가로로만 돈다. 폰을 세워도 세로로 넘어가지 않아야 한다.
    /// <para>가로 두 방향(왼쪽·오른쪽)은 서로 오갈 수 있게 둔다 — 폰을 뒤집어 쥐는 사람이 있고,
    /// 그건 화면 구성이 달라지는 변화가 아니다.</para>
    /// </summary>
    public class ScreenOrientationTests
    {
        [Test]
        public void Orientation_IsLandscapeOnly()
        {
            Assert.That(PlayerSettings.defaultInterfaceOrientation, Is.EqualTo(UIOrientation.AutoRotation),
                "가로 두 방향을 오가려면 자동 회전이어야 한다. 한 방향으로 고정하면 뒤집어 쥔 사람이 거꾸로 본다.");

            Assert.That(PlayerSettings.allowedAutorotateToPortrait, Is.False, "세로로 넘어가면 안 된다.");
            Assert.That(PlayerSettings.allowedAutorotateToPortraitUpsideDown, Is.False, "거꾸로 세로도 안 된다.");

            Assert.That(PlayerSettings.allowedAutorotateToLandscapeLeft, Is.True);
            Assert.That(PlayerSettings.allowedAutorotateToLandscapeRight, Is.True);
        }
    }
}
