using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// DodgeMap에 체육관 겉모습을 입힌다. 충돌체(Floor·벽 넷)는 재질만 바꾸고, 새 장식은 콜라이더 없이 ArenaLook 아래에만 둔다.
    /// 맵은 아트 서브모듈이라 결과는 그 레포에 커밋한다. 몇 번 돌려도 같은 결과.
    /// </summary>
    public static class DodgeArenaLookBuilder
    {
        private const string MapPath = "Assets/Art/Scenes/DodgeMap.unity";
        private const string MatDir = "Assets/Art/Materials/Dodge";
        private const string ToonShader = "Assets/Shaders/LOP/LOPToon.shader";

        [MenuItem("LOP/Dodge/Build Arena Look")]
        public static void Build()
        {
            Directory.CreateDirectory(MatDir);
            var court = Toon("Toon_DodgeCourt", "#D9A866");
            var outer = Toon("Toon_DodgeCourtOuter", "#8B6A45");
            var line = Toon("Toon_DodgeCourtLine", "#FFF6E5");
            var wall = Toon("Toon_DodgeWall", "#E8E4DC");
            var mat = Toon("Toon_DodgeWallMat", "#3B82F6");
            var bleacher = Toon("Toon_DodgeBleacher", "#C9A77E");

            // 다른 씬을 건드리지 않게 맵만 덧붙여 열고 저장한 뒤 닫는다.
            var scene = EditorSceneManager.OpenScene(MapPath, OpenSceneMode.Additive);
            GameObject look = null, thrower = null;
            foreach (var go in scene.GetRootGameObjects())
            {
                switch (go.name)
                {
                    case "ArenaLook": look = go; break;
                    case "Thrower": thrower = go; break;
                    case "Floor": go.GetComponent<MeshRenderer>().sharedMaterial = court; break;
                    case "WallN": case "WallS": case "WallE": case "WallW":
                        go.GetComponent<MeshRenderer>().sharedMaterial = wall; break;
                }
            }
            if (look != null) Object.DestroyImmediate(look);

            // 예전 고정 심판 충돌체는 지운다 — 심판은 이제 서버가 움직이는 캐릭터라 몸이 따라다닌다.
            if (thrower != null) Object.DestroyImmediate(thrower);
            look = new GameObject("ArenaLook");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(look, scene);

            Box(look, "OuterFloor", new Vector3(0f, -0.02f, 0f),
                new Vector3(DodgeArenaLayout.OuterFloorSize, 0.02f, DodgeArenaLayout.OuterFloorSize), outer);
            int i = 0;
            foreach (var (c, s) in DodgeArenaLayout.CourtLines()) Box(look, "Line" + i++, c, s, line);
            Ring(look, "CenterCircle", DodgeArenaLayout.CenterCircleRadius, line);
            // 벽 안쪽 아래 1m에 파란 매트(벽 앞 2cm)
            Box(look, "MatN", new Vector3(0f, 0.5f, 8.98f), new Vector3(18f, 1f, 0.02f), mat);
            Box(look, "MatS", new Vector3(0f, 0.5f, -8.98f), new Vector3(18f, 1f, 0.02f), mat);
            Box(look, "MatE", new Vector3(8.98f, 0.5f, 0f), new Vector3(0.02f, 1f, 18f), mat);
            Box(look, "MatW", new Vector3(-8.98f, 0.5f, 0f), new Vector3(0.02f, 1f, 18f), mat);
            i = 0;
            foreach (var (c, s) in DodgeArenaLayout.Bleachers()) Box(look, "Bleacher" + i++, c, s, bleacher);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
        }

        private static void Box(GameObject parent, string name, Vector3 center, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());   // 장식 — 이동이 벽으로 여기면 안 된다
            go.name = name;
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // 가운데 원: 얇은 상자 32개를 둘레에 돌려 세운다
        private static void Ring(GameObject parent, string name, float radius, Material m)
        {
            var ring = new GameObject(name);
            ring.transform.SetParent(parent.transform, false);
            const int n = 32;
            float seg = 2f * Mathf.PI * radius / n;
            for (int k = 0; k < n; k++)
            {
                float a = (k + 0.5f) * Mathf.PI * 2f / n;
                Box(ring, "Arc" + k, new Vector3(Mathf.Cos(a) * radius, 0.005f, Mathf.Sin(a) * radius),
                    new Vector3(DodgeArenaLayout.LineWidth, 0.004f, seg * 1.05f), m);
                ring.transform.GetChild(k).localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
            }
        }

        private static Material Toon(string name, string hex)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(AssetDatabase.LoadAssetAtPath<Shader>(ToonShader)) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            ColorUtility.TryParseHtmlString(hex, out var c);
            m.SetColor("_BaseColor", c);
            m.SetShaderPassEnabled("SRPDefaultUnlit", false);   // 외곽선은 캐릭터만
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
