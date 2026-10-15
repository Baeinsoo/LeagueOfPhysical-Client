using System.IO;
using LOP.UI;
using NUnit.Framework;
using UnityEditor;

namespace LOP.Tests
{
    /// <summary>키트 선수 카드 — 셸 틀, 정보 판·최근 전적 요소, 기록 한 줄 문구, 창 목록의 스타일 연결.</summary>
    public class ProfileViewKitTests
    {
        private const string Uxml = "Assets/UI/Shell/ProfileView.uxml";
        private const string Uss = "Assets/UI/Shell/ProfileView.uss";

        [Test]
        public void 필수_요소()
        {
            var x = File.ReadAllText(Uxml);
            ShellFrameTests.AssertShellFrame(x);
            foreach (var n in new[] { "profile-card", "profile-name", "profile-rename", "profile-level", "profile-rank", "profile-records", "profile-equipped", "profile-to-locker", "profile-status", "profile-history" })
            {
                StringAssert.Contains($"name=\"{n}\"", x);
            }
            StringAssert.Contains("kit-panel", x);
        }

        [Test]
        public void 펼침과_내_줄_규칙()
        {
            var s = File.ReadAllText(Uss);
            StringAssert.Contains(".profile-match__rows--open", s);
            StringAssert.Contains(".profile-row--me", s);
            StringAssert.DoesNotContain("#000", s);
        }

        [Test]
        public void 창_목록은_선수_카드_스타일을_쓴다()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>("Assets/UI/UIViewCatalog.asset");
            Assert.IsTrue(catalog.TryGet(nameof(ProfileView), out var entry));
            Assert.AreEqual(Uss, AssetDatabase.GetAssetPath(entry.uss));
        }

        [Test]
        public void 기록_한_줄()
        {
            var played = new ProfileQueueStats("일반", true, 12, 3, "2.4", null, null, null);
            Assert.AreEqual("일반 12전 3회 1위 · 평균 2.4등", ProfileView.RecordLine(played));

            var none = new ProfileQueueStats("랭크", false, 0, 0, null, null, null, null);
            Assert.AreEqual("랭크 아직 기록이 없습니다", ProfileView.RecordLine(none));
        }

        [Test]
        public void 레벨_한_줄은_받은_것만()
        {
            Assert.AreEqual("Lv 3 · XP 120 / 500", ProfileView.LevelLine("Lv 3", "120 / 500"));
            Assert.AreEqual(string.Empty, ProfileView.LevelLine(string.Empty, string.Empty));
        }
    }
}
