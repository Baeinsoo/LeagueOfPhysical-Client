using UnityEditor;
using UnityEngine;

namespace LOP.LookDevEditor
{
    /// <summary>
    /// PolyOne 치비 몸 메시를 복사해 정점마다 옷 영역(가장 크게 따르는 뼈 기준)을 정점 색에 굽는다. 원본 fbx는 건드리지 않는다.
    /// </summary>
    public static class LookDevRegionBaker
    {
        public const string OutputPath = "Assets/Characters/Chibi/Mesh/SM_Chibi_Regions.asset";
        private const string Model = "Assets/Art/PolyOne/Chibi Character/Model/SM_Chibi_Character.fbx";
        private const string BodyName = "SM_Chibi_Body";

        [MenuItem("LOP/LookDev/Bake Outfit Regions")]
        public static Mesh Bake()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            SkinnedMeshRenderer body = null;
            foreach (var smr in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.name == BodyName) { body = smr; }
            }
            if (body == null)
            {
                Debug.LogError("[LookDev] 몸 메시를 못 찾았다: " + BodyName);
                return null;
            }

            var mesh = Object.Instantiate(body.sharedMesh);
            mesh.name = "SM_Chibi_Regions";
            var weights = mesh.boneWeights;
            var positions = mesh.vertices;
            var colors = new Color32[mesh.vertexCount];
            for (int i = 0; i < colors.Length; i++)
            {
                string bone = body.bones[weights[i].boneIndex0].name;
                var region = bone == "Head" ? ChibiRegions.HeadRegion(positions[i].y, positions[i].z) : ChibiRegions.RegionOf(bone);
                colors[i] = new Color32(ChibiRegions.Encode(region), 0, 0, 255);
            }
            mesh.colors32 = colors;

            System.IO.Directory.CreateDirectory("Assets/Characters/Chibi/Mesh");
            //  덮어쓰기(CopySerialized)는 정점 색이 갱신되지 않았다 — 지우고 새로 만든다. 씬 빌더가 매번 새로 연결한다.
            AssetDatabase.DeleteAsset(OutputPath);
            AssetDatabase.CreateAsset(mesh, OutputPath);
            AssetDatabase.SaveAssets();
            return mesh;
        }
    }
}
