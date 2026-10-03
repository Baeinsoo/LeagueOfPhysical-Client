using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 스카이다이브에 새 룩을 입힌다 — 코스(선반·기둥·문·바람 화살표)를 툰 재질로 갈고, 평면 구름·회색 바닥은 끄고,
    /// 젤다풍 하늘섬·뭉게구름·착지 섬을 얹는다(충돌체 없음). 맵 씬(Art)은 고치지 않는다: 서버도 같은 씬으로 충돌을 재고 맵은 원격 에셋이다.
    /// <para>게임 스코프가 맵보다 먼저 시작한다 — 씬이 뜰 때마다 다시 찾아 한 번 입히고, 판이 끝나면 되돌린다.</para>
    /// </summary>
    public class SkydiveScenery : IStartable, System.IDisposable
    {
        private readonly List<(Renderer renderer, Material original)> swapped = new List<(Renderer, Material)>();
        private readonly List<Renderer> hidden = new List<Renderer>();
        private readonly List<Object> created = new List<Object>();
        private readonly Dictionary<string, Material> toon = new Dictionary<string, Material>();
        private bool dressed;
        private bool listening;

        public void Start()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            listening = true;
            TryDress();
        }

        public void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryDress();

        public void Dispose()
        {
            if (listening)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                listening = false;
            }
            foreach (var (renderer, original) in swapped)
            {
                if (renderer != null) { renderer.sharedMaterial = original; }
            }
            swapped.Clear();
            foreach (var r in hidden)
            {
                if (r != null) { r.enabled = true; }
            }
            hidden.Clear();
            foreach (var o in created)
            {
                if (o != null) { Kill(o); }
            }
            created.Clear();
            toon.Clear();
            dressed = false;
        }

        private void TryDress()
        {
            if (dressed)
            {
                return;
            }
            bool found = false;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded == false)
                {
                    continue;
                }
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    {
                        found |= Restyle(r);
                    }
                }
            }
            if (found == false)
            {
                return;   // 맵이 아직 없다 — 다음 씬 로드 때 다시
            }
            dressed = true;
            BuildScenery();
        }

        //  재질 이름(또는 바닥 이름)으로 알아본다. 알아본 것이 있으면 true.
        private bool Restyle(Renderer r)
        {
            if (r.name == "Floor")
            {
                Hide(r);   // 착지 섬이 대신한다
                return true;
            }
            var mat = r.sharedMaterial;
            if (mat == null)
            {
                return false;
            }
            string name = mat.name.Replace(" (Instance)", string.Empty);
            switch (name)
            {
                case "SkydiveStone": Swap(r, Shelf()); return true;
                case "SkydiveWindArrow_Weak": Swap(r, Toon("#2E86C8")); return true;
                case "SkydiveWindArrow_Mid": Swap(r, Toon("#17A996")); return true;
                case "SkydiveWindArrow_Strong": Swap(r, Toon("#E0700F")); return true;
                case "SkydiveCloud": Hide(r); return true;   // 뭉게구름이 대신한다
                default: return false;
            }
        }

        private void BuildScenery()
        {
            var root = new GameObject("SkydiveScenery").transform;
            created.Add(root.gameObject);
            var rng = new System.Random(20260930);

            foreach (var island in SkydiveSceneryLayout.IslandSpots())
            {
                Island(root, island.Center, island.Radius, island.Radius * 1.6f, island.Trees, rng);
            }

            //  구름은 층마다 메시 한 장(폰 부담 — 공을 하나하나 오브젝트로 두면 790개가 넘는다).
            var white = Toon("#FFFFFF");
            int seed = 7;
            foreach (var layer in SkydiveSceneryLayout.CloudSpots().GroupBy(c => c.y))
            {
                var mesh = SkydiveSceneryLayout.BuildCloudLayer(layer.ToList(), seed++);
                created.Add(mesh);
                var go = new GameObject("CloudLayer_" + layer.Key);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = white;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            //  착지 섬 — 기존 바닥 충돌은 그대로, 보이는 것만 섬.
            Island(root, new Vector3(0f, -3f, 0f), SkydiveSceneryLayout.LandingRadius, 90f, 8, rng);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f + 0.4f;
                float d = 40f + i * 12f;
                Prim(root, PrimitiveType.Sphere, Toon("#D9B48A"), new Vector3(Mathf.Cos(a) * d, 1f, Mathf.Sin(a) * d), new Vector3(6f, 4f, 5f));
            }
        }

        //  윗면 풀(납작한 원기둥) + 아래 거꾸로 선 바위 원뿔 + 나무.
        private void Island(Transform root, Vector3 top, float radius, float depth, int trees, System.Random rng)
        {
            Prim(root, PrimitiveType.Cylinder, Toon("#7CC46E"), top, new Vector3(radius * 2f, 3f, radius * 2f));
            var cone = SkydiveSceneryLayout.BuildCone(radius, depth, 14);
            created.Add(cone);
            var rock = new GameObject("IslandRock");
            rock.transform.SetParent(root, false);
            rock.transform.position = top - new Vector3(0f, 3f, 0f);
            rock.AddComponent<MeshFilter>().sharedMesh = cone;
            var mr = rock.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Toon("#D9B48A");
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < trees; i++)
            {
                float a = (float)(rng.NextDouble() * Mathf.PI * 2);
                float d = radius * (trees >= 8 ? 0.45f + 0.4f * (float)rng.NextDouble() : 0.6f * (float)rng.NextDouble());
                var p = top + new Vector3(Mathf.Cos(a) * d, 3f, Mathf.Sin(a) * d);
                float s = Mathf.Clamp(radius / 10f, 2f, 5f);
                Prim(root, PrimitiveType.Cylinder, Toon("#8B5E34"), p + new Vector3(0f, s, 0f), new Vector3(s * 0.4f, s, s * 0.4f));
                Prim(root, PrimitiveType.Sphere, Toon("#45B060"), p + new Vector3(0f, s * 2.6f, 0f), Vector3.one * s * 2.2f);
                Prim(root, PrimitiveType.Sphere, Toon("#45B060"), p + new Vector3(s * 0.6f, s * 2.1f, s * 0.3f), Vector3.one * s * 1.4f);
            }
        }

        //  선반 윗면은 타일 무늬 — 단색이면 다가가도 변하는 게 없어 거리가 안 느껴진다(5m 칸, 20m마다 굵은 줄).
        private const float ShelfTile = 5f;

        private Material Shelf()
        {
            if (toon.TryGetValue("shelf", out var m) == false)
            {
                ColorUtility.TryParseHtmlString("#D9B48A", out var color);
                m = LOPToonMaterials.Create(color);
                m.SetFloat("_TopGrid", ShelfTile);
                toon["shelf"] = m;
                created.Add(m);
            }
            return m;
        }

        private Material Toon(string hex)
        {
            if (toon.TryGetValue(hex, out var m) == false)
            {
                ColorUtility.TryParseHtmlString(hex, out var color);
                m = LOPToonMaterials.Create(color);
                toon[hex] = m;
                created.Add(m);
            }
            return m;
        }

        private void Swap(Renderer r, Material material)
        {
            swapped.Add((r, r.sharedMaterial));
            r.sharedMaterial = material;
        }

        private void Hide(Renderer r)
        {
            if (r.enabled)
            {
                r.enabled = false;
                hidden.Add(r);
            }
        }

        private static void Prim(Transform parent, PrimitiveType type, Material material, Vector3 position, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());   // 판정 무관 — 바로 지워 한 프레임도 안 남긴다
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static void Kill(Object o)
        {
            if (UnityEngine.Application.isPlaying) { Object.Destroy(o); } else { Object.DestroyImmediate(o); }
        }
    }
}
