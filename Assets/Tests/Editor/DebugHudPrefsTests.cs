using LOP.UI;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 디버그 HUD는 기본 숨김(spec §1 정리, intent "화면에 디버그 글자가 안 보이나"). 옛 키는 창을 열 때마다 0(보임)이
    /// 적혀 왔으므로 그 값을 읽으면 사용자 폰에선 영영 안 숨는다 — 새 키만 본다.
    /// </summary>
    public class DebugHudPrefsTests
    {
        private const string OldKey = "lop.debugHud.hidden";
        private bool hadNew, hadOld;
        private int savedNew, savedOld;

        //  에디터의 진짜 PlayerPrefs를 쓰므로 원래 값을 지켜 둔다(개발자의 토글 상태를 시험이 바꾸면 안 된다).
        [SetUp]
        public void SetUp()
        {
            hadNew = PlayerPrefs.HasKey(DebugHudPrefs.ShownKey);
            savedNew = PlayerPrefs.GetInt(DebugHudPrefs.ShownKey, 0);
            hadOld = PlayerPrefs.HasKey(OldKey);
            savedOld = PlayerPrefs.GetInt(OldKey, 0);
            PlayerPrefs.DeleteKey(DebugHudPrefs.ShownKey);
        }

        [TearDown]
        public void TearDown()
        {
            if (hadNew) { PlayerPrefs.SetInt(DebugHudPrefs.ShownKey, savedNew); } else { PlayerPrefs.DeleteKey(DebugHudPrefs.ShownKey); }
            if (hadOld) { PlayerPrefs.SetInt(OldKey, savedOld); } else { PlayerPrefs.DeleteKey(OldKey); }
        }

        [Test]
        public void 한_번도_안_건드렸으면_숨긴다()
        {
            Assert.IsTrue(DebugHudPrefs.LoadHidden());
        }

        [Test]
        public void 옛_키에_보임이_적혀_있어도_새_키가_없으면_숨긴다()
        {
            PlayerPrefs.SetInt(OldKey, 0);

            Assert.IsTrue(DebugHudPrefs.LoadHidden());
        }

        [Test]
        public void 켜_두면_다음에도_보인다()
        {
            DebugHudPrefs.SaveHidden(false);
            Assert.IsFalse(DebugHudPrefs.LoadHidden());

            DebugHudPrefs.SaveHidden(true);
            Assert.IsTrue(DebugHudPrefs.LoadHidden());
        }

        [Test]
        public void 손잡이는_에디터와_개발_빌드에서만_보인다()
        {
            Assert.IsTrue(DebugHudPrefs.HandleVisible(isEditor: true, isDebugBuild: false));
            Assert.IsTrue(DebugHudPrefs.HandleVisible(isEditor: false, isDebugBuild: true));
            Assert.IsTrue(DebugHudPrefs.HandleVisible(isEditor: true, isDebugBuild: true));
        }

        [Test]
        public void 릴리스_빌드에선_손잡이가_없다()
        {
            //  intent 성공 판정 "화면에 디버그 글자가 안 보이나" — 숨긴 패널뿐 아니라 "디버그" 손잡이 글자도 없어야 한다.
            Assert.IsFalse(DebugHudPrefs.HandleVisible(isEditor: false, isDebugBuild: false));
        }
    }
}
