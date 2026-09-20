#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// 유니티가 내놓은 Xcode 프로젝트의 Info.plist를 손본다. 두 가지를 한다.
    /// <para>1. 평문 http 허용 — iOS는 https가 아닌 요청을 운영체제 차원에서 막는다. dev 백엔드
    /// 주소가 평문이라 그대로 두면 로그인부터 실패한다. 유니티 쪽 차단
    /// (insecureHttpOption=DevelopmentOnly)이 개발 빌드에서만 평문을 허용하므로, 여는 기준을
    /// 거기에 맞춘다.</para>
    /// <para>2. 암호화 수출 규정 — 이 항목이 없으면 TestFlight에 올릴 때마다 사람이 웹에서 같은
    /// 질문에 답해야 한다.</para>
    /// </summary>
    public class IOSBuildPostProcess : IPostprocessBuildWithReport
    {
        //  환경 자산 굽기(EnvironmentBuildProcessor, 0) 뒤에 돈다.
        public int callbackOrder => 1;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS)
            {
                return;
            }

            var plistPath = Path.Combine(report.summary.outputPath, "Info.plist");
            if (!File.Exists(plistPath))
            {
                Debug.LogWarning($"[LOP] Info.plist를 찾지 못해 후처리를 건너뛴다: {plistPath}");
                return;
            }

            var development = (report.summary.options & BuildOptions.Development) != 0;
            Apply(plistPath, development);
            Debug.Log($"[LOP] iOS Info.plist 후처리 완료 (개발 빌드={development})");
        }

        /// <summary>
        /// plist 하나만 놓고 부를 수 있게 갈라 둔 자리. 빌드를 돌리지 않고 테스트한다.
        /// </summary>
        public static void Apply(string plistPath, bool development)
        {
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

            //  우리 앱은 표준 https 말고 따로 암호화를 쓰지 않는다.
            plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);

            if (development)
            {
                //  주소가 환경마다 달라(dev는 고정 IP, local은 사설망) 도메인별 예외를 적을 수
                //  없다. 개발 빌드 전용이므로 전체 허용으로 둔다.
                var security = plist.root.CreateDict("NSAppTransportSecurity");
                security.SetBoolean("NSAllowsArbitraryLoads", true);
            }

            plist.WriteToFile(plistPath);
        }
    }
}
#endif
