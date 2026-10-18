using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace LOP.EditorTools
{
    /// <summary>
    /// 안드로이드 매니페스트에 <c>android.permission.VIBRATE</c>를 넣는다.
    ///
    /// <para>유니티는 스크립트가 <c>Handheld.Vibrate</c>를 부를 때만 이 권한을 자동으로 넣는다.
    /// 활쏘기 진동은 길이·세기를 정하려고 <c>AndroidHaptics</c>가 자바 진동기를 직접 부르므로 자동으로
    /// 안 들어간다 — 없으면 진동이 보안 예외로 막힌다(2026-10-18 APK 매니페스트에 INTERNET·멀티캐스트만 있었다).
    /// 일반 권한이라 사용자에게 묻는 창은 안 뜬다.</para>
    ///
    /// <para><c>#if UNITY_ANDROID</c>로 감싸지 않는다 — 이 인터페이스는 유니티 6에선 모든 플랫폼의 에디터에
    /// 있고(UnityEditor.CoreModule) 안드로이드 빌드에서만 불린다. 감싸면 맥 대상으로 켜 둔 에디터가 이 파일을
    /// 컴파일하지 않아, 깨져도 안드로이드 빌드에서야 드러난다.</para>
    /// </summary>
    public class AndroidVibratePermission : IPostGenerateGradleAndroidProject
    {
        private const string AndroidNs = "http://schemas.android.com/apk/res/android";
        private const string Permission = "android.permission.VIBRATE";

        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            //  path는 unityLibrary 모듈이다. 여기 매니페스트가 앱 매니페스트로 합쳐진다.
            string manifest = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            var doc = new XmlDocument();
            doc.Load(manifest);
            if (doc.DocumentElement == null)
            {
                UnityEngine.Debug.LogError("[AndroidVibratePermission] 매니페스트에 manifest 태그가 없다 — 진동 권한을 못 넣었다");
                return;
            }
            if (EnsurePermission(doc))
            {
                doc.Save(manifest);
            }
        }

        /// <summary>진동 권한이 없으면 넣는다. 넣었으면 참 — 이미 있으면 그대로 두고 거짓.</summary>
        public static bool EnsurePermission(XmlDocument doc)
        {
            var root = doc.DocumentElement;
            if (root == null)
            {
                return false;
            }
            foreach (XmlNode node in root.ChildNodes)
            {
                if (node is XmlElement e && e.LocalName == "uses-permission"
                    && e.GetAttribute("name", AndroidNs) == Permission)
                {
                    return false;   // 이미 있다
                }
            }

            var element = doc.CreateElement("uses-permission");
            element.SetAttribute("name", AndroidNs, Permission);
            root.AppendChild(element);
            return true;
        }
    }
}
