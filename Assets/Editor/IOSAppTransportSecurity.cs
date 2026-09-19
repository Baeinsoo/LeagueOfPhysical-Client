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
    /// iOS는 https가 아닌 평문 http 요청을 운영체제 차원에서 막는다. dev 백엔드 주소가 평문
    /// http라 그대로 두면 로그인·매칭 요청이 전부 실패하므로, 개발 빌드에 한해 그 차단을 푼다.
    /// <para>릴리스 빌드에는 열지 않는다 — 유니티 쪽 차단(insecureHttpOption=DevelopmentOnly)이
    /// 이미 개발 빌드에서만 평문을 허용하고 있어, 둘의 기준을 같게 맞춘다.</para>
    /// </summary>
    public class IOSAppTransportSecurity : IPostprocessBuildWithReport
    {
        //  환경 자산 굽기(EnvironmentBuildProcessor, 0) 뒤에 돈다.
        public int callbackOrder => 1;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS)
            {
                return;
            }

            if ((report.summary.options & BuildOptions.Development) == 0)
            {
                return;
            }

            var plistPath = Path.Combine(report.summary.outputPath, "Info.plist");
            if (!File.Exists(plistPath))
            {
                Debug.LogWarning($"[LOP] Info.plist를 찾지 못해 평문 http 허용을 건너뛴다: {plistPath}");
                return;
            }

            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

            //  주소가 환경마다 달라(dev는 고정 IP, local은 사설망) 도메인별 예외를 적을 수 없다.
            //  개발 빌드 전용이므로 전체 허용으로 둔다.
            var security = plist.root.CreateDict("NSAppTransportSecurity");
            security.SetBoolean("NSAllowsArbitraryLoads", true);

            plist.WriteToFile(plistPath);
            Debug.Log("[LOP] 개발 빌드: iOS 평문 http 허용(NSAllowsArbitraryLoads)을 켰다.");
        }
    }
}
#endif
