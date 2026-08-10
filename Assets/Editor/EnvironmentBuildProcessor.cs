using System;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace LOP.EditorTools
{
    /// <summary>
    /// 플레이어 빌드 전후로 활성 환경 자산을 챙긴다.
    /// CLI 빌드는 <c>BuildScript</c>가 미리 구워 두므로 여기서는 손대지 않고,
    /// 에디터 Build Settings 창으로 굽지 않고 빌드한 경우만 현재 선택으로 대신 구워 준다.
    /// </summary>
    public class EnvironmentBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        //  이 훅이 구운 경우에만 치운다. CLI 빌드가 구운 것은 BuildScript의 finally가 책임진다.
        private static bool bakedHere;

        public void OnPreprocessBuild(BuildReport report)
        {
            bakedHere = false;

            //  파일 존재 여부가 아니라 CLI 인자 유무로 판단한다. 실패한 빌드는 후처리가 안 돌아
            //  .active가 남을 수 있어, 파일이 있다고 "CLI가 구웠다"고 믿으면 그 낡은 파일을 그대로
            //  쓰게 된다.
            if (EnvironmentBaker.EnvironmentFromCommandLine() != null)
            {
                return;
            }

            var environment = EnvironmentBaker.EnvironmentFromEditorPrefs();
            try
            {
                EnvironmentBaker.Bake(environment);
            }
            catch (Exception e)
            {
                throw new BuildFailedException($"활성 환경을 구울 수 없다(환경: {environment}). {e.Message}");
            }

            bakedHere = true;
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (!bakedHere)
            {
                return;
            }

            bakedHere = false;
            EnvironmentBaker.Clear();
        }
    }
}
