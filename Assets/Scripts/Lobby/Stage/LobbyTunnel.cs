using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>로비 선수 입장 터널을 기본 도형으로 세운다 — 바닥·벽·천장·기둥·보·바닥 줄·끝 빛 판. 콜라이더는 지운다(로비엔 물리가 없다).</summary>
    public static class LobbyTunnel
    {
        public const string RootName = "LobbyTunnel";

        public static GameObject Build(Transform parent, TunnelLayout layout, out Renderer endGlow)
        {
            var root = new GameObject(RootName);
            root.transform.SetParent(parent, false);

            var wall = MakeMaterial("#2C2A6B");
            var pillar = MakeMaterial("#3D3A86");
            var floor = MakeMaterial("#262458");
            var stripe = MakeMaterial("#FFD23F");
            var glow = MakeMaterial("#FFF8E8");
            //  끝 빛은 그림자 없이 밝게 — 그림자색을 바탕색과 같게, 외곽선 없음.
            if (glow.HasProperty("_ShadowColor")) glow.SetColor("_ShadowColor", glow.GetColor("_BaseColor"));
            if (glow.HasProperty("_OutlineWidth")) glow.SetFloat("_OutlineWidth", 0f);

            float w = layout.Width, h = layout.Height, len = layout.Length, half = w / 2f;
            Box(root, "Floor", floor, new Vector3(0f, -0.05f, len / 2f), new Vector3(w + 0.4f, 0.1f, len));
            Box(root, "WallLeft", wall, new Vector3(-half - 0.1f, h / 2f, len / 2f), new Vector3(0.2f, h, len));
            Box(root, "WallRight", wall, new Vector3(half + 0.1f, h / 2f, len / 2f), new Vector3(0.2f, h, len));
            Box(root, "Ceiling", wall, new Vector3(0f, h + 0.05f, len / 2f), new Vector3(w + 0.4f, 0.1f, len));
            float px = half - layout.PillarSize / 2f;
            foreach (float z in layout.PillarZs())
            {
                Box(root, $"PillarL_{z}", pillar, new Vector3(-px, h / 2f, z), new Vector3(layout.PillarSize, h, layout.PillarSize));
                Box(root, $"PillarR_{z}", pillar, new Vector3(px, h / 2f, z), new Vector3(layout.PillarSize, h, layout.PillarSize));
                Box(root, $"Beam_{z}", pillar, new Vector3(0f, layout.BeamY, z), new Vector3(w, layout.BeamThickness, layout.PillarSize));
            }
            Box(root, "FloorStripe", stripe, new Vector3(0f, 0.005f, len / 2f), new Vector3(layout.FloorStripeWidth, 0.01f, len));

            var end = GameObject.CreatePrimitive(PrimitiveType.Quad);
            end.name = "EndGlow";
            Object.DestroyImmediate(end.GetComponent<Collider>());
            end.transform.SetParent(root.transform, false);
            end.transform.localPosition = new Vector3(0f, h / 2f, layout.EndGlowZ);
            end.transform.localScale = new Vector3(w, h, 1f);
            endGlow = end.GetComponent<Renderer>();
            endGlow.sharedMaterial = glow;
            return root;
        }

        /// <summary>Build가 코드로 만든 재질을 지운다 — 오브젝트와 달리 씬이 내려가도 남는다.</summary>
        public static void DestroyMaterials(GameObject root)
        {
            if (root == null) return;
            var seen = new HashSet<Material>();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var m = r.sharedMaterial;
                if (m != null && seen.Add(m)) Object.Destroy(m);
            }
        }

        private static void Box(GameObject root, string name, Material material, Vector3 position, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Material MakeMaterial(string hex)
        {
            var shader = Shader.Find("LOP/Toon");
            if (shader == null)
            {
                Debug.LogWarning("[LobbyTunnel] LOP/Toon 셰이더를 못 찾아 URP Lit로 대신한다.");
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }
            var m = new Material(shader);
            ColorUtility.TryParseHtmlString(hex, out var c);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            return m;
        }
    }
}
