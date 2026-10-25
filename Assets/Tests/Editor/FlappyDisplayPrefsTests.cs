using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 플래피는 상대 이름 기본 숨김(spec §2 — 폴가이즈 문법), 설정 "상대 이름 보기"를 켜면 이름표만 다시 켠다.
    /// 새 몸엔 체력이 없어 늘 가득 찬 HP 막대는 언제나 끈다.
    /// </summary>
    public class FlappyDisplayPrefsTests
    {
        private bool had;
        private int saved;

        [SetUp]
        public void SetUp()
        {
            had = PlayerPrefs.HasKey(FlappyDisplayPrefs.ShowOpponentNamesKey);
            saved = PlayerPrefs.GetInt(FlappyDisplayPrefs.ShowOpponentNamesKey, 0);
            PlayerPrefs.DeleteKey(FlappyDisplayPrefs.ShowOpponentNamesKey);
        }

        [TearDown]
        public void TearDown()
        {
            if (had) { PlayerPrefs.SetInt(FlappyDisplayPrefs.ShowOpponentNamesKey, saved); }
            else { PlayerPrefs.DeleteKey(FlappyDisplayPrefs.ShowOpponentNamesKey); }
        }

        [Test]
        public void 상대_이름은_기본_숨김()
        {
            Assert.IsFalse(FlappyDisplayPrefs.ShowOpponentNames);
        }

        [Test]
        public void 설정을_켜면_이름이_보인다()
        {
            FlappyDisplayPrefs.SetShowOpponentNames(true);

            Assert.IsTrue(FlappyDisplayPrefs.ShowOpponentNames);
        }

        [Test]
        public void 플래피_스코프가_이름표와_HP_막대를_직접_정한다()
        {
            //  override가 빠지면 GameLifetimeScope의 기본값(둘 다 켬)이 조용히 쓰인다.
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            Assert.IsNotNull(typeof(FlappyRaceLifetimeScope).GetProperty("ShowHealthBars", flags));
            Assert.IsNotNull(typeof(FlappyRaceLifetimeScope).GetProperty("ShowNameplates", flags));
        }
    }
}
