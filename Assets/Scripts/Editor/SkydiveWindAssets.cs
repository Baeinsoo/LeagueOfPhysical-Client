using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LOP.EditorTools
{
    /// <summary>
    /// 바람 시각물이 쓰는 메시·머티리얼을 만들어 두고 찾아 준다.
    ///
    /// <para>에셋은 <b>아트 서브모듈</b>에 둔다 — 맵 씬이 그 안에 있고, 서버도 같은 서브모듈을
    /// 마운트해 읽기 때문이다. 클라 쪽 폴더에 두면 서버에서 참조가 끊긴다.</para>
    /// </summary>
    internal static class SkydiveWindAssets
    {
        private const string ModelFolder = "Assets/Art/Models";
        private const string MaterialFolder = "Assets/Art/Materials";

        private const string ArrowMeshPath = ModelFolder + "/SkydiveWindArrow.asset";
        private const string ShellMeshPath = ModelFolder + "/SkydiveWindShell.asset";

        // 세기 3단계. 하늘을 배경으로 놓고 고른 색이라 연한 하늘색은 쓰지 않는다 — 배경에 묻는다.
        private static readonly string[] BandNames = { "Weak", "Mid", "Strong" };
        private static readonly Color[] BandColors =
        {
            new Color(0.180f, 0.525f, 0.784f),   // 약  #2E86C8
            new Color(0.090f, 0.663f, 0.588f),   // 중  #17A996
            new Color(0.878f, 0.439f, 0.059f),   // 강  #E0700F
        };

        // 면은 지나가며 뚫고 다니는 것이라 아주 옅게 — 진하면 안쪽이 안 보인다.
        private const float ShellAlpha = 0.12f;

        /// <summary>세기가 어느 단계인지. 0=약(≤10), 1=중(11~15), 2=강(16~).</summary>
        internal static int StrengthBand(float speed)
        {
            if (speed <= 10f) return 0;
            return speed <= 15f ? 1 : 2;
        }

        /// <summary>에셋이 없으면 만들고, 있으면 내용만 새로 고쳐서 돌려준다.</summary>
        internal static WindVisualAssets EnsureAssets()
        {
            if (AssetDatabase.IsValidFolder(ModelFolder) == false)
            {
                AssetDatabase.CreateFolder("Assets/Art", "Models");
            }

            var assets = new WindVisualAssets
            {
                Arrow = EnsureAsset(ArrowMeshPath, SkydiveWindMeshes.CreateArrow),
                Shell = EnsureAsset(ShellMeshPath, SkydiveWindMeshes.CreateShell),
                ArrowMaterials = new Material[BandNames.Length],
                ShellMaterials = new Material[BandNames.Length],
            };

            for (int band = 0; band < BandNames.Length; band++)
            {
                int captured = band;
                assets.ArrowMaterials[band] = EnsureAsset(
                    $"{MaterialFolder}/SkydiveWindArrow_{BandNames[band]}.mat",
                    () => CreateArrowMaterial(BandColors[captured]));
                assets.ShellMaterials[band] = EnsureAsset(
                    $"{MaterialFolder}/SkydiveWindShell_{BandNames[band]}.mat",
                    () => CreateShellMaterial(BandColors[captured]));
            }

            AssetDatabase.SaveAssets();
            return assets;
        }

        /// <summary>이미 구워 둔 에셋을 찾아만 온다. 하나라도 없으면 <c>IsComplete</c>가 false다.</summary>
        internal static WindVisualAssets LoadAssets()
        {
            var assets = new WindVisualAssets
            {
                Arrow = AssetDatabase.LoadAssetAtPath<Mesh>(ArrowMeshPath),
                Shell = AssetDatabase.LoadAssetAtPath<Mesh>(ShellMeshPath),
                ArrowMaterials = new Material[BandNames.Length],
                ShellMaterials = new Material[BandNames.Length],
            };

            for (int band = 0; band < BandNames.Length; band++)
            {
                assets.ArrowMaterials[band] = AssetDatabase.LoadAssetAtPath<Material>(
                    $"{MaterialFolder}/SkydiveWindArrow_{BandNames[band]}.mat");
                assets.ShellMaterials[band] = AssetDatabase.LoadAssetAtPath<Material>(
                    $"{MaterialFolder}/SkydiveWindShell_{BandNames[band]}.mat");
            }

            return assets;
        }

        /// <summary>메시·머티리얼 없이도 도는 테스트용 묶음. 디스크를 건드리지 않는다.</summary>
        internal static WindVisualAssets CreateInMemory()
        {
            var assets = new WindVisualAssets
            {
                Arrow = SkydiveWindMeshes.CreateArrow(),
                Shell = SkydiveWindMeshes.CreateShell(),
                ArrowMaterials = new Material[BandNames.Length],
                ShellMaterials = new Material[BandNames.Length],
            };

            for (int band = 0; band < BandNames.Length; band++)
            {
                assets.ArrowMaterials[band] = CreateArrowMaterial(BandColors[band]);
                assets.ShellMaterials[band] = CreateShellMaterial(BandColors[band]);
            }

            return assets;
        }

        // 새로 CreateAsset을 부르면 GUID가 바뀌어 씬 참조가 통째로 끊긴다. 이미 있으면 내용만 덮는다.
        internal static T EnsureAsset<T>(string path, System.Func<T> factory) where T : Object
        {
            T generated = factory();

            //  CopySerialized는 "내용만"이 아니라 이름까지 통째로 덮는다. 갓 만든 것은 이름이
            //  셰이더 이름이라, 미리 파일 이름으로 맞춰 두지 않으면 두 번째 굽기부터 에셋
            //  이름이 뭉개진다. 덮은 뒤에 되돌리지 않는 이유는 그러면 순서에 기대는 코드가 되어
            //  줄을 옮기는 순간 조용히 다시 깨지기 때문이다.
            generated.name = System.IO.Path.GetFileNameWithoutExtension(path);

            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, path);
                return generated;
            }

            EditorUtility.CopySerialized(generated, existing);
            Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static Material CreateArrowMaterial(Color color)
        {
            // 화살표는 불투명이다 — 반투명이면 하늘에 묻혀서 방향이 안 읽힌다.
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.15f);
            return material;
        }

        private static Material CreateShellMaterial(Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", new Color(color.r, color.g, color.b, ShellAlpha));

            // 안에서도 밖에서도 보여야 한다 — 뚫고 지나가는 면이라 한쪽 면만 그리면 안이 뚫린다.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }
    }

    /// <summary>바람 볼륨 하나를 그리는 데 필요한 것 전부.</summary>
    internal sealed class WindVisualAssets
    {
        public Mesh Arrow;
        public Mesh Shell;
        public Material[] ArrowMaterials;
        public Material[] ShellMaterials;

        public bool IsComplete
        {
            get
            {
                if (Arrow == null || Shell == null) return false;
                if (ArrowMaterials == null || ShellMaterials == null) return false;
                for (int i = 0; i < ArrowMaterials.Length; i++)
                {
                    if (ArrowMaterials[i] == null || ShellMaterials[i] == null) return false;
                }
                return true;
            }
        }

        public Material ArrowFor(float speed) => ArrowMaterials[SkydiveWindAssets.StrengthBand(speed)];
        public Material ShellFor(float speed) => ShellMaterials[SkydiveWindAssets.StrengthBand(speed)];
    }
}
