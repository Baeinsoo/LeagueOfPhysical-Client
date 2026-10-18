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
        private const string FxShader = "Assets/Shaders/LOP/LOPFxParticle.shader";

        private static readonly Dictionary<string, string> SlotColors = new Dictionary<string, string>
        {
            //  투석기
            ["Wood"] = "#8B5E34", ["WoodDark"] = "#5E3B1F", ["Metal"] = "#A7ADB5", ["Rope"] = "#CDB58A",
            //  2차 표적(시안 색)
            ["Water"] = "#3FB3FF", ["WaterLight"] = "#CFF0FF", ["WaterDark"] = "#1D86C9",
            ["Melon"] = "#2F9E44", ["MelonDark"] = "#14632A",
            ["Porcelain"] = "#F6F3EC", ["PorcelainBlue"] = "#3B6FD8",
            ["RocketRed"] = "#E8433A", ["White"] = "#F4F1E8", ["Gold"] = "#F2C230", ["GoldDark"] = "#B8861B",
            ["PinataOrange"] = "#FF8A3D", ["PinataPink"] = "#FF4D8D", ["PinataGreen"] = "#44D17A", ["PinataBlue"] = "#4AA8FF",
            ["Tassel"] = "#B97BFF",
            ["NestBrown"] = "#8A5A2B", ["NestDark"] = "#5E3B1F", ["EggBlue"] = "#BFE3FF",
            ["FlagBlue"] = "#3B5BFF", ["KidYellow"] = "#FFD23F", ["Ink"] = "#2A231C", ["String"] = "#8A8A8A",
        };

        //  색표에 없는 슬롯 — 분홍으로 만들고 빌드 끝에 한 번 알린다(블렌더 슬롯 이름 오타를 놓치지 않게).
        private static readonly SortedSet<string> missingSlots = new SortedSet<string>();

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

            missingSlots.Clear();
            kit.targets = ArcheryCatapultLooks.Codes.Select(c =>
            {
                var mesh = MeshOf(c);
                var materials = MaterialsOf(c);
                return new ArcheryFestivalKit.Entry { code = c, mesh = mesh, materials = materials, radius = BodyRadius(mesh, materials) };
            }).ToArray();
            kit.forbid = Toon("Festival_Forbid", "#E5484D");
            kit.catapultFrame = MeshOf("CatapultFrame"); kit.catapultFrameMaterials = MaterialsOf("CatapultFrame");
            kit.catapultArm = MeshOf("CatapultArm"); kit.catapultArmMaterials = MaterialsOf("CatapultArm");

            //  연출 재질 — 섞기: 5 = SrcAlpha, 10 = OneMinusSrcAlpha, 1 = One(더하기).
            //  번쩍임만 정점 색을 안 쓴다 — 정점 색이 없는 표적 메시에 씌우므로 곱하면 검게 나온다.
            kit.fxSolid = Fx("Fx_Solid", shape: 0, dst: 10, vertexColor: true);
            kit.fxDot = Fx("Fx_Dot", shape: 1, dst: 10, vertexColor: true);
            kit.fxSpark = Fx("Fx_Spark", shape: 1, dst: 1, vertexColor: true);
            kit.fxRing = Fx("Fx_Ring", shape: 2, dst: 10, vertexColor: true);
            kit.fxFlash = Fx("Fx_Flash", shape: 0, dst: 10, vertexColor: false);
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
            foreach (var name in missingSlots)
            {
                Debug.LogWarning("[FestivalKit] 색표에 없는 슬롯: " + name);
            }
            Debug.Log("[FestivalKit] 만들었다: " + KitPath);
        }

        //  몸만 잰다(키트 시험과 같은 규칙) — 꾸밈 슬롯 서브메시의 정점은 뺀다.
        private static float BodyRadius(Mesh mesh, Material[] materials)
        {
            float r = 0f;
            var vertices = mesh.vertices;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                if (s < materials.Length && ArcheryFestivalKit.IsDecorSlot(materials[s].name)) { continue; }
                foreach (int i in mesh.GetIndices(s)) { r = Mathf.Max(r, vertices[i].magnitude); }
            }
            return r;
        }

        private static Material Fx(string name, int shape, int dst, bool vertexColor)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(AssetDatabase.LoadAssetAtPath<Shader>(FxShader));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_Color", Color.white);
            mat.SetFloat("_Shape", shape);
            mat.SetFloat("_SrcBlend", 5f);
            mat.SetFloat("_DstBlend", dst);
            mat.SetFloat("_UseVertexColor", vertexColor ? 1f : 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Mesh MeshOf(string part)
            => AssetDatabase.LoadAllAssetsAtPath($"{ModelDir}/{part}.fbx").OfType<Mesh>().First();

        //  FBX의 재질 슬롯 이름(블렌더 스크립트가 붙였다) 순서대로 툰 재질을 짝짓는다.
        private static Material[] MaterialsOf(string part)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{part}.fbx");
            var slots = go.GetComponentInChildren<MeshRenderer>().sharedMaterials;
            return slots.Select(m =>
            {
                if (!SlotColors.TryGetValue(m.name, out var hex))
                {
                    missingSlots.Add(m.name);
                    hex = "#FF00FF";
                }
                return Toon("Festival_" + m.name, hex);
            }).ToArray();
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
