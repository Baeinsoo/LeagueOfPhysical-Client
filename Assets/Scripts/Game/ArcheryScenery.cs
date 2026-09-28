using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 활쏘기 맵에 새 룩을 입힌다 — 땅·뒤 벽을 툰 재질로, 옆에 나무, 과녁 너머에 먼 언덕. 맵 씬(Art)은 고치지 않는다:
    /// 서버도 같은 씬을 쓰고 맵은 원격 에셋이라 고치면 배포가 따라온다. 판이 끝나면 되돌린다.
    /// <para>게임 스코프가 맵보다 먼저 시작한다(LOPRoom: 스코프 생성 → 러너가 맵 로드) — 씬이 뜰 때마다 다시 찾아 한 번 입힌다.</para>
    /// </summary>
    public class ArcheryScenery : IStartable, System.IDisposable
    {
        private readonly List<(Renderer renderer, Material original)> swapped = new List<(Renderer, Material)>();
        private readonly List<Object> created = new List<Object>();
        private bool dressed;
        private bool listening;

        public void Start()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            listening = true;
            TryDress();
        }

        public void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryDress();

        //  땅(Ground)이 없는 맵(원형 사대 등)이면 아무것도 안 한다.
        private void TryDress()
        {
            if (dressed)
            {
                return;
            }
            var ground = FindInLoadedScenes("Ground");
            if (ground == null)
            {
                return;
            }
            dressed = true;
            var backstop = FindInLoadedScenes("Backstop");
            var grass = Keep(LOPToonMaterials.Create(Hex("#7CC46E")));
            var wood = Keep(LOPToonMaterials.Create(Hex("#8B5E34")));
            var leaf = Keep(LOPToonMaterials.Create(Hex("#45B060")));
            var hill = Keep(LOPToonMaterials.Create(Hex("#9CC0B8")));
            Swap(ground, grass);
            if (backstop != null)
            {
                Swap(backstop, wood);
            }

            var root = new GameObject("ArcheryScenery").transform;
            created.Add(root.gameObject);
            var bounds = ground.bounds;
            foreach (var p in TreeSpots(bounds))
            {
                Prim(root, PrimitiveType.Cylinder, wood, p + new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f));
                Prim(root, PrimitiveType.Sphere, leaf, p + new Vector3(0f, 3f, 0f), Vector3.one * 3.4f);
                Prim(root, PrimitiveType.Sphere, leaf, p + new Vector3(0.9f, 2.5f, 0.4f), Vector3.one * 2.2f);
            }
            foreach (var h in HillSpots(bounds))
            {
                Prim(root, PrimitiveType.Sphere, hill, new Vector3(h.x, h.y, h.z), Vector3.one * h.w * 2f);
            }
        }

        public void Dispose()
        {
            if (listening)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                listening = false;
            }
            dressed = false;
            foreach (var (renderer, original) in swapped)
            {
                if (renderer != null)
                {
                    renderer.sharedMaterial = original;
                }
            }
            swapped.Clear();
            foreach (var o in created)
            {
                if (o != null)
                {
                    if (UnityEngine.Application.isPlaying) { Object.Destroy(o); } else { Object.DestroyImmediate(o); }   // 편집 모드 시험에서도 부른다
                }
            }
            created.Clear();
        }

        /// <summary>땅 양옆 6m 바깥, 앞끝 +30m부터 16m 간격으로 한쪽 다섯 그루(관중석 구간은 비운다).</summary>
        public static List<Vector3> TreeSpots(Bounds ground)
        {
            var spots = new List<Vector3>();
            for (int i = 0; i < 5; i++)
            {
                float z = ground.min.z + 30f + i * 16f;
                spots.Add(new Vector3(ground.min.x - 6f - (i % 2) * 2f, ground.max.y, z));
                spots.Add(new Vector3(ground.max.x + 6f + ((i + 1) % 2) * 2f, ground.max.y, z + 8f));
            }
            return spots;
        }

        /// <summary>과녁 너머 먼 언덕 넷(xyz 가운데, w 반지름). 반쯤 땅에 묻는다.</summary>
        public static List<Vector4> HillSpots(Bounds ground)
        {
            float cx = ground.center.x;
            float far = ground.max.z;
            return new List<Vector4>
            {
                new Vector4(cx - 70f, -12f, far + 60f, 22f),
                new Vector4(cx - 20f, -16f, far + 80f, 28f),
                new Vector4(cx + 30f, -12f, far + 65f, 20f),
                new Vector4(cx + 75f, -14f, far + 55f, 24f),
            };
        }

        private Material Keep(Material m)
        {
            created.Add(m);
            return m;
        }

        private void Swap(Renderer renderer, Material material)
        {
            swapped.Add((renderer, renderer.sharedMaterial));
            renderer.sharedMaterial = material;
        }

        private static void Prim(Transform parent, PrimitiveType type, Material material, Vector3 position, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());   // 판정 무관 — 화살이 걸리면 안 된다(바로 지워 한 프레임도 안 남긴다)
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static Renderer FindInLoadedScenes(string name)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                foreach (var root in SceneManager.GetSceneAt(i).GetRootGameObjects())
                {
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    {
                        if (r.name == name)
                        {
                            return r;
                        }
                    }
                }
            }
            return null;
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
