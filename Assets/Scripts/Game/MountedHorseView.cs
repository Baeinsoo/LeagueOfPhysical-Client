using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 기수 밑에 회색 도형 말을 깔고 기수(치비)를 안장 높이로 올린다. 말은 그림일 뿐이다 —
    /// 콜라이더가 없고 이동은 시뮬의 캡슐이 한다. 내 기수만 조준 방향으로 몸을 돌린다.
    /// </summary>
    public class MountedHorseView : ILateTickable, System.IDisposable
    {
        private const float SaddleHeight = 1.25f;
        private const float SaddleBack = -0.1f;

        private readonly ActorRegistry actorRegistry;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly IPlayerContext playerContext;

        // 몸마다 마지막으로 말을 입힌 기수 모델. 모델이 바뀌면(다시 로드) 다시 올린다.
        private readonly Dictionary<string, (GameObject rider, GameObject horse)> built =
            new Dictionary<string, (GameObject, GameObject)>();
        private readonly List<string> scratch = new List<string>();
        private Material horseMaterial;

        public MountedHorseView(ActorRegistry actorRegistry, GameFramework.World.EntityRegistry entityRegistry,
                                IPlayerContext playerContext)
        {
            this.actorRegistry = actorRegistry;
            this.entityRegistry = entityRegistry;
            this.playerContext = playerContext;
        }

        public void LateTick()
        {
            scratch.Clear();
            foreach (var pair in built)
            {
                if (pair.Value.rider == null)
                {
                    scratch.Add(pair.Key);
                }
            }
            foreach (var id in scratch)
            {
                if (built[id].horse != null)
                {
                    Object.Destroy(built[id].horse);
                }
                built.Remove(id);
            }

            foreach (var entity in entityRegistry.All)
            {
                if (entity.Get<Mount>() == null || actorRegistry.TryGet(entity.Id, out var actor) == false)
                {
                    continue;
                }
                var rider = actor.visualGameObject;
                if (rider == null)
                {
                    continue;
                }

                if (built.TryGetValue(entity.Id, out var state) == false || state.rider != rider)
                {
                    if (state.horse != null)
                    {
                        Object.Destroy(state.horse);
                    }
                    built[entity.Id] = (rider, BuildHorse(rider.transform.parent));
                    rider.transform.localPosition = new Vector3(0f, SaddleHeight, SaddleBack);
                }

                if (entity.Id == playerContext.entityId)
                {
                    TurnRiderToAim(entity, rider.transform);
                }
            }
        }

        // 말은 가는 방향을 보고 기수만 조준 방향으로 몸을 튼다 — 말 기준 상대 각도로 준다.
        private static void TurnRiderToAim(GameFramework.World.Entity entity, Transform rider)
        {
            var aim = entity.Get<ArcheryAim>();
            if (aim == null || rider.parent == null)
            {
                return;
            }
            float horseYaw = rider.parent.eulerAngles.y;
            rider.localRotation = Quaternion.Euler(0f, Mathf.DeltaAngle(horseYaw, aim.Yaw), 0f);
        }

        private GameObject BuildHorse(Transform parent)
        {
            if (horseMaterial == null)
            {
                horseMaterial = LOPToonMaterials.Create(new Color(0.62f, 0.63f, 0.66f));
            }

            var root = new GameObject("MountedHorse");
            root.transform.SetParent(parent, false);

            AddPart(root.transform, "Body", new Vector3(0f, 1.4f, 0f), new Vector3(0.85f, 0.85f, 2.0f), 0f);
            AddPart(root.transform, "Neck", new Vector3(0f, 1.95f, 0.95f), new Vector3(0.42f, 0.95f, 0.5f), -31f);
            AddPart(root.transform, "Head", new Vector3(0f, 2.32f, 1.35f), new Vector3(0.38f, 0.4f, 0.85f), 0f);
            foreach (float x in new[] { -0.27f, 0.27f })
            {
                foreach (float z in new[] { -0.72f, 0.72f })
                {
                    AddPart(root.transform, "Leg", new Vector3(x, 0.52f, z), new Vector3(0.17f, 1.05f, 0.2f), 0f);
                }
            }
            return root;
        }

        private void AddPart(Transform parent, string name, Vector3 position, Vector3 scale, float pitchDegrees)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            // 이동은 시뮬 캡슐이 한다 — 말 도형이 화살 레이캐스트나 물리에 걸리면 안 된다.
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = Quaternion.Euler(pitchDegrees, 0f, 0f);
            part.transform.localScale = scale;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = horseMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void Dispose()
        {
            foreach (var pair in built)
            {
                if (pair.Value.horse != null)
                {
                    Object.Destroy(pair.Value.horse);
                }
            }
            built.Clear();
            if (horseMaterial != null)
            {
                Object.Destroy(horseMaterial);
                horseMaterial = null;
            }
        }
    }
}
