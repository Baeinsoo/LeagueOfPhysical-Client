using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>Dodge 소품 키트(메시·재질)를 만들고 게임 씬 스코프에 연결한다. 몇 번 돌려도 같은 결과.</summary>
    public static class DodgePropKitBuilder
    {
        private const string Dir = "Assets/Dodge/Props";
        private const string KitPath = Dir + "/DodgePropKit.asset";
        private const string ToonShader = "Assets/Shaders/LOP/LOPToon.shader";

        [MenuItem("LOP/Dodge/Build Prop Kit")]
        public static void Build()
        {
            Directory.CreateDirectory(Dir);
            var kit = AssetDatabase.LoadAssetAtPath<DodgePropKit>(KitPath);
            if (kit == null)
            {
                kit = ScriptableObject.CreateInstance<DodgePropKit>();
                AssetDatabase.CreateAsset(kit, KitPath);
            }

            kit.slipperMesh = SaveMesh(DodgePropMeshes.Slipper(), "Slipper");
            kit.jarMesh = SaveMesh(DodgePropMeshes.Jar(), "Jar");
            kit.melonMesh = DodgePropMeshes.Primitive(PrimitiveType.Sphere);

            kit.slipperMaterials = new[]
            {
                Toon("Toon_DodgeSlipperPink", Hex("#FF4F5E")),
                Toon("Toon_DodgeSlipperBlue", Hex("#3B82F6")),
                Toon("Toon_DodgeSlipperOrange", Hex("#F59E0B")),
            };
            kit.jarMaterial = Toon("Toon_DodgeJar", Hex("#7A4A2A"));
            kit.melonMaterial = Toon("Toon_DodgeMelon", Color.white, StripeTexture());
            kit.ropeMaterial = Toon("Toon_DodgeRope", Hex("#F5E6C8"));

            kit.warn = Overlay("Dodge_Warn", new Color(1f, 0.82f, 0.4f, 0.35f));
            kit.shadow = Overlay("Dodge_Shadow", new Color(0f, 0f, 0f, 0.3f));
            kit.juice = Overlay("Dodge_Juice", new Color(1f, 0.31f, 0.37f, 0.8f));
            kit.hot = Overlay("Dodge_Hot", new Color(1f, 0.25f, 0.2f, 0.85f));
            kit.tileWarm = Overlay("Dodge_TileWarm", new Color(1f, 0.7f, 0.28f, 0.45f));
            kit.tileHot = Overlay("Dodge_TileHot", new Color(1f, 0.23f, 0.19f, 0.8f));
            EditorUtility.SetDirty(kit);
            AssetDatabase.SaveAssets();

            // 게임 씬 스코프에 연결 — 씬이 에셋을 잡고 있어야 빌드에 따라간다.
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Dodge.unity", OpenSceneMode.Additive);
            foreach (var go in scene.GetRootGameObjects())
            {
                var scope = go.GetComponentInChildren<DodgeLifetimeScope>(true);
                if (scope == null) continue;
                var so = new SerializedObject(scope);
                so.FindProperty("propKit").objectReferenceValue = kit;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
        }

        private static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = $"{Dir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                mesh.name = name;
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = name;   // CopySerialized는 이름까지 덮는다(메모리 copyserialized-overwrites-more-than-content)
            return existing;
        }

        private static Material Toon(string name, Color color, Texture2D map = null)
        {
            var m = LoadOrCreate(name, AssetDatabase.LoadAssetAtPath<Shader>(ToonShader));
            m.SetColor("_BaseColor", color);
            if (map != null) m.SetTexture("_BaseMap", map);
            m.SetShaderPassEnabled("SRPDefaultUnlit", false);   // 외곽선은 캐릭터만
            EditorUtility.SetDirty(m);
            return m;
        }

        // 바닥 층: 반투명 Unlit — 바닥과 캐릭터를 가리지 않게.
        private static Material Overlay(string name, Color color)
        {
            var m = LoadOrCreate(name, Shader.Find("Universal Render Pipeline/Unlit"));
            m.color = color;
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material LoadOrCreate(string name, Shader shader)
        {
            string path = $"{Dir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            else
            {
                m.shader = shader;
            }
            return m;
        }

        // 수박 줄무늬: 구의 u(경도)를 따라 진한 초록·연한 초록.
        private static Texture2D StripeTexture()
        {
            string path = Dir + "/MelonStripes.asset";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null) return tex;
            tex = new Texture2D(64, 4, TextureFormat.RGBA32, false) { name = "MelonStripes", wrapMode = TextureWrapMode.Repeat };
            var dark = Hex("#1F6B35");
            var light = Hex("#3FA34D");
            for (int x = 0; x < 64; x++)
            {
                var c = (x / 4) % 2 == 0 ? dark : light;
                for (int y = 0; y < 4; y++) tex.SetPixel(x, y, c);
            }
            tex.Apply();
            AssetDatabase.CreateAsset(tex, path);
            return tex;
        }

        private static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }
}
