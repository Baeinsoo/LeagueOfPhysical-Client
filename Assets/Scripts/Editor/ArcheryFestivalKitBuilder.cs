using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>축제 키트(FBX 메시 + 툰 재질)를 만들고 활쏘기 게임 씬 스코프에 연결한다.</summary>
    public static class ArcheryFestivalKitBuilder
    {
        private const string ModelDir = "Assets/Art/Models/Festival";
        private const string MaterialDir = "Assets/Art/Materials/Festival";
        private const string KitDir = "Assets/Archery/Festival";
        private const string KitPath = KitDir + "/ArcheryFestivalKit.asset";
        private const string ToonShader = "Assets/Shaders/LOP/LOPToon.shader";

        private static readonly Dictionary<string, string> SlotColors = new Dictionary<string, string>
        {
            ["Wood"] = "#8B5E34", ["WoodDark"] = "#5E3B1F", ["Metal"] = "#A7ADB5", ["MetalDark"] = "#5B6168",
            ["Red"] = "#D9342B", ["Green"] = "#3E9B45", ["Gold"] = "#F2C230", ["GoldDark"] = "#B8861B",
            ["White"] = "#F4F1E8", ["Cloth"] = "#2F5DA8", ["Straw"] = "#D8B36A", ["Petal"] = "#F28DB2", ["Rope"] = "#CDB58A",
        };

        [MenuItem("LOP/Archery/Build Festival Kit")]
        public static void Build()
        {
            Directory.CreateDirectory(KitDir);
            Directory.CreateDirectory(MaterialDir);
            var kit = AssetDatabase.LoadAssetAtPath<ArcheryFestivalKit>(KitPath);
            if (kit == null)
            {
                kit = ScriptableObject.CreateInstance<ArcheryFestivalKit>();
                AssetDatabase.CreateAsset(kit, KitPath);
            }

            var codes = new[] { "Shield", "Helmet", "Apple", "Grail", "KingFlag", "FlowerBasket" };
            kit.targets = codes.Select(c => new ArcheryFestivalKit.Entry
            {
                code = c, mesh = MeshOf(c), materials = MaterialsOf(c),
            }).ToArray();
            kit.forbid = Toon("Festival_Forbid", "#E5484D");
            kit.shieldHalf = MeshOf("ShieldHalf"); kit.shieldHalfMaterials = MaterialsOf("ShieldHalf");
            kit.appleHalf = MeshOf("AppleHalf"); kit.appleHalfMaterials = MaterialsOf("AppleHalf");
            kit.coin = MeshOf("Coin"); kit.coinMaterial = MaterialsOf("Coin")[0];
            kit.catapultFrame = MeshOf("CatapultFrame"); kit.catapultFrameMaterials = MaterialsOf("CatapultFrame");
            kit.catapultArm = MeshOf("CatapultArm"); kit.catapultArmMaterials = MaterialsOf("CatapultArm");
            EditorUtility.SetDirty(kit);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Archery.unity", OpenSceneMode.Additive);
            foreach (var go in scene.GetRootGameObjects())
            {
                var scope = go.GetComponentInChildren<ArcheryLifetimeScope>(true);
                if (scope == null) { continue; }
                var so = new SerializedObject(scope);
                so.FindProperty("festivalKit").objectReferenceValue = kit;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
            Debug.Log("[FestivalKit] 만들었다: " + KitPath);
        }

        private static Mesh MeshOf(string part)
            => AssetDatabase.LoadAllAssetsAtPath($"{ModelDir}/{part}.fbx").OfType<Mesh>().First();

        //  FBX의 재질 슬롯 이름(블렌더 스크립트가 붙였다) 순서대로 툰 재질을 짝짓는다.
        private static Material[] MaterialsOf(string part)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{part}.fbx");
            var slots = go.GetComponentInChildren<MeshRenderer>().sharedMaterials;
            return slots.Select(m => Toon("Festival_" + m.name, SlotColors.TryGetValue(m.name, out var hex) ? hex : "#FF00FF")).ToArray();
        }

        private static Material Toon(string name, string hex)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(AssetDatabase.LoadAssetAtPath<Shader>(ToonShader));
                AssetDatabase.CreateAsset(mat, path);
            }
            ColorUtility.TryParseHtmlString(hex, out var color);
            mat.SetColor("_BaseColor", color);
            mat.SetShaderPassEnabled("SRPDefaultUnlit", false);   // 외곽선은 캐릭터만
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
