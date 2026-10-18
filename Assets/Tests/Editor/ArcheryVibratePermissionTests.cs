using System.Xml;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 안드로이드 매니페스트에 진동 권한이 들어가나. 활쏘기 진동은 자바 진동기를 직접 불러서
    /// 유니티가 이 권한을 자동으로 안 넣는다 — 빠지면 폰에서 진동이 조용히 막힌다.
    /// </summary>
    public class ArcheryVibratePermissionTests
    {
        private const string Ns = "http://schemas.android.com/apk/res/android";

        private static XmlDocument Manifest(string body)
        {
            var doc = new XmlDocument();
            doc.LoadXml($"<manifest xmlns:android=\"{Ns}\" package=\"com.x\">{body}<application /></manifest>");
            return doc;
        }

        private static int CountVibrate(XmlDocument doc)
        {
            int n = 0;
            foreach (XmlNode node in doc.DocumentElement.ChildNodes)
            {
                if (node is XmlElement e && e.LocalName == "uses-permission"
                    && e.GetAttribute("name", Ns) == "android.permission.VIBRATE")
                {
                    n++;
                }
            }
            return n;
        }

        [Test]
        public void 없으면_넣는다()
        {
            var doc = Manifest("<uses-permission android:name=\"android.permission.INTERNET\" />");
            Assert.IsTrue(LOP.EditorTools.AndroidVibratePermission.EnsurePermission(doc));
            Assert.AreEqual(1, CountVibrate(doc));
        }

        [Test]
        public void 이미_있으면_두_번_넣지_않는다()
        {
            var doc = Manifest("<uses-permission android:name=\"android.permission.VIBRATE\" />");
            Assert.IsFalse(LOP.EditorTools.AndroidVibratePermission.EnsurePermission(doc));
            Assert.AreEqual(1, CountVibrate(doc));
        }
    }
}
