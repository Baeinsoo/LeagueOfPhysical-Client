using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 탈락한 선수를 들것에 눕혀 가장 가까운 벽 밖으로 내보낸다(그림만, 테마 스펙 §4). 서버는 탈락과 함께 몸을 지우므로
    /// 마지막으로 보인 자리를 기억해 두고, 누운 몸은 같은 치비를 새로 꺼내 그 선수 옷을 입힌다 — 지워지는 몸을 복사하면
    /// 메시지 순서에 따라 이미 사라져 있을 수 있다. 치비를 못 받으면 빈 들것만 나간다.
    /// </summary>
    public class DodgeStretcherView : IStartable, ILateTickable, IDisposable
    {
        private const string ChibiKey = "Assets/Characters/Chibi/Chibi.prefab";
        private const string FaceMaterialKey = "Assets/Characters/Chibi/Materials/ChibiFace.mat";
        private const float BoardHeight = 0.45f;

        private readonly DodgeClientState state;
        private readonly ActorRegistry actorRegistry;
        private readonly DodgePropKit kit;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly CosmeticCatalog catalog;
        private readonly Dictionary<string, Vector2> lastSeen = new Dictionary<string, Vector2>();
        private readonly HashSet<string> gone = new HashSet<string>();
        private readonly List<(GameObject root, Vector2 from, Vector2 exit, float seconds)> rides =
            new List<(GameObject, Vector2, Vector2, float)>();
        private GameObject chibiPrefab;
        private Material faceMaterial;
        private bool seeded;

        public DodgeStretcherView(DodgeClientState state, ActorRegistry actorRegistry, DodgePropKit kit,
                                  GameFramework.World.EntityRegistry entityRegistry, CosmeticCatalog catalog)
        {
            this.state = state;
            this.actorRegistry = actorRegistry;
            this.kit = kit;
            this.entityRegistry = entityRegistry;
            this.catalog = catalog;
        }

        public void Start()
        {
            UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>(ChibiKey).Completed += h =>
                chibiPrefab = h.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded ? h.Result : null;
            UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<Material>(FaceMaterialKey).Completed += h =>
                faceMaterial = h.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded ? h.Result : null;
        }

        public void LateTick()
        {
            foreach (var id in state.PlayerIds)
            {
                if (actorRegistry.TryGet(id, out var actor) && actor != null && actor.visualGameObject != null)
                {
                    var p = actor.visualGameObject.transform.position;
                    lastSeen[id] = new Vector2(p.x, p.z);
                }
                state.TryGetLife(id, out _, out long eliminatedTick);
                // 처음 볼 때 이미 탈락한 사람(재접속 전)은 실어 내지 않는다 — 자막과 같은 규칙.
                if (eliminatedTick >= 0 && gone.Add(id) && seeded && lastSeen.TryGetValue(id, out var at))
                {
                    Spawn(id, at);
                }
            }
            seeded = true;

            for (int i = rides.Count - 1; i >= 0; i--)
            {
                var r = rides[i];
                r.seconds += Time.deltaTime;
                if (r.root == null || DodgeStretcher.Done(r.seconds))
                {
                    if (r.root != null) UnityEngine.Object.Destroy(r.root);
                    rides.RemoveAt(i);
                    continue;
                }
                var p = DodgeStretcher.Position(r.from, r.exit, r.seconds);
                r.root.transform.position = new Vector3(p.x, 0f, p.y);
                rides[i] = r;
            }
        }

        private void Spawn(string id, Vector2 from)
        {
            var exit = DodgeStretcher.ExitPoint(from);
            var root = new GameObject("Stretcher");
            root.transform.SetPositionAndRotation(new Vector3(from.x, 0f, from.y),
                Quaternion.LookRotation(new Vector3(exit.x - from.x, 0f, exit.y - from.y)));

            Material cloth = kit != null ? kit.ropeMaterial : null;
            Material cross = kit != null && kit.slipperMaterials != null && kit.slipperMaterials.Length > 0 ? kit.slipperMaterials[0] : null;
            Part(root, new Vector3(0f, BoardHeight, 0f), new Vector3(0.7f, 0.06f, 1.8f), cloth);          // 천
            Part(root, new Vector3(0.38f, BoardHeight, 0f), new Vector3(0.06f, 0.06f, 2.3f), cloth);      // 손잡이 막대
            Part(root, new Vector3(-0.38f, BoardHeight, 0f), new Vector3(0.06f, 0.06f, 2.3f), cloth);
            Part(root, new Vector3(0f, BoardHeight + 0.035f, -0.6f), new Vector3(0.4f, 0.01f, 0.12f), cross);   // 빨간 십자
            Part(root, new Vector3(0f, BoardHeight + 0.035f, -0.6f), new Vector3(0.12f, 0.01f, 0.4f), cross);

            if (chibiPrefab != null)
            {
                // 발은 앞(나가는 쪽), 머리는 뒤로 눕힌다. 치비 원점은 발밑이고 앞(+z)이 얼굴이라 x로 -90°면 하늘을 본다.
                var body = UnityEngine.Object.Instantiate(chibiPrefab, root.transform, false);
                body.transform.localPosition = new Vector3(0f, BoardHeight + 0.05f, 0.75f);
                body.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                var animator = body.GetComponent<Animator>();
                if (animator != null) animator.enabled = false;
                // 서버가 탈락과 함께 몸을 지워 엔티티가 이미 없을 수 있다 — 그때는 look 없이 기본값으로.
                ChibiDresser.Dress(id, body, faceMaterial, entityRegistry.Get(id)?.Get<PlayerLook>(), catalog);
            }
            rides.Add((root, from, exit, 0f));
        }

        private static void Part(GameObject parent, Vector3 center, Vector3 size, Material material)
        {
            var go = new GameObject("Part");
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = DodgePropMeshes.Primitive(PrimitiveType.Cube);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void Dispose()
        {
            foreach (var r in rides)
            {
                if (r.root != null) UnityEngine.Object.Destroy(r.root);
            }
            rides.Clear();
        }
    }
}
