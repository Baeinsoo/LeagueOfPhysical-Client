using System.IO;
using LOP.UI;
using NUnit.Framework;
using UnityEditor;

namespace LOP.Tests
{
    /// <summary>키트 라커룸 — 셸 틀, 슬롯 줄·가진 것 그리드·전부 기본으로·상점으로 요소.</summary>
    public class LockerViewKitTests
    {
        private const string Uxml = "Assets/UI/Locker/LockerView.uxml";
        private const string Uss = "Assets/UI/Locker/LockerView.uss";

        [Test]
        public void 필수_요소()
        {
            var x = File.ReadAllText(Uxml);
            ShellFrameTests.AssertShellFrame(x);
            foreach (var n in new[] { "locker-slots", "locker-grid", "locker-reset", "locker-to-shop", "locker-message" })
            {
                StringAssert.Contains($"name=\"{n}\"", x);
            }
        }

        [Test]
        public void 고른_슬롯_규칙()
        {
            var s = File.ReadAllText(Uss);
            StringAssert.Contains(".locker-slot--picked", s);
            StringAssert.DoesNotContain("#000", s);
        }

        [Test]
        public void 카드_상태_줄()
        {
            Assert.AreEqual("★ 장착 중", LockerView.StateLine(new LockerItem(1, "x", equipped: true, isDefault: true)));
            Assert.AreEqual("기본", LockerView.StateLine(new LockerItem(1, "x", equipped: false, isDefault: true)));
            Assert.AreEqual("✓ 보유", LockerView.StateLine(new LockerItem(1, "x", equipped: false, isDefault: false)));
        }

        [Test]
        public void 창_목록에_라커룸이_있다()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>("Assets/UI/UIViewCatalog.asset");
            Assert.IsTrue(catalog.TryGet(nameof(LockerView), out var entry));
            Assert.IsNotNull(entry.uxml);
            Assert.IsNotNull(entry.uss);
        }
    }
}
