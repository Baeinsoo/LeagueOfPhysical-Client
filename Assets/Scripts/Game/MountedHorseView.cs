using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 기수 밑에 회색 도형 말을 깔고 기수(치비)를 안장 높이로 올린다. 말은 그림일 뿐이다 —
    /// 콜라이더가 없고 이동은 시뮬의 캡슐이 한다. 내 기수만 조준 방향으로 몸을 돌린다.
    /// <para>몸 그림의 뿌리(visual)는 보간기가 매 프레임 LateUpdate에서 월드 자세째 덮어쓴다(이 뷰는 그보다 먼저 돈다).
    /// 그래서 뿌리는 건드리지 않는다 — 말은 뿌리의 자식으로 붙여 보간된 자리를 같이 따라가게 하고,
    /// 안장 높이와 상체 회전은 뿌리 아래 모델 자식(<see cref="ChibiRig.ModelChildName"/>)에만 준다.</para>
    /// </summary>
    public class MountedHorseView : ILateTickable, System.IDisposable
    {
        public const float SaddleHeight = 1.25f;
        public const float SaddleBack = -0.1f;

        private sealed class Seat
        {
            public GameObject Visual;
            public GameObject Horse;
            public Transform Model;
            public Vector3 RestPosition;
            public Quaternion RestRotation;
        }

        private readonly ActorRegistry actorRegistry;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly IPlayerContext playerContext;

        // 몸마다 마지막으로 말을 입힌 그림. 그림이 바뀌면(다시 로드) 다시 입힌다.
        private readonly Dictionary<string, Seat> built = new Dictionary<string, Seat>();
        private readonly List<string> scratch = new List<string>();
        private Material horseMaterial;

        public MountedHorseView(ActorRegistry actorRegistry, GameFramework.World.EntityRegistry entityRegistry,
                                IPlayerContext playerContext)
        {
            this.actorRegistry = actorRegistry;
            this.entityRegistry = entityRegistry;
            this.playerContext = playerContext;
        }

        /// <summary>말 머리 방향에서 조준 방향까지 가까운 쪽으로 잰 각도(도). 기수 모델의 말 기준 회전이다.</summary>
        public static float RiderLocalYaw(float headingYaw, float aimYaw) => Mathf.DeltaAngle(headingYaw, aimYaw);

        /// <summary>모델의 원래 자리에서 안장으로 옮긴 자리(뿌리 기준).</summary>
        public static Vector3 SeatLocalPosition(Vector3 restLocalPosition)
            => restLocalPosition + new Vector3(0f, SaddleHeight, SaddleBack);

        public void LateTick()
        {
            scratch.Clear();
            foreach (var pair in built)
            {
                if (pair.Value.Visual == null)
                {
                    scratch.Add(pair.Key);
                }
            }
            foreach (var id in scratch)
            {
                built.Remove(id);   // 말은 그림의 자식이라 그림과 함께 이미 사라졌다
            }

            foreach (var entity in entityRegistry.All)
            {
                if (entity.Get<Mount>() == null || actorRegistry.TryGet(entity.Id, out var actor) == false)
                {
                    continue;
                }
                var visual = actor.visualGameObject;
                if (visual == null)
                {
                    continue;
                }

                if (built.TryGetValue(entity.Id, out var seat) == false || seat.Visual != visual)
                {
                    seat = Dress(visual);
                    built[entity.Id] = seat;
                }
                if (seat.Model == null)
                {
                    continue;
                }

                // 뿌리의 방향은 직전 프레임에 보간기가 쓴 말 머리다 — 돌고 있을 때 한 프레임 늦을 뿐이다.
                float localYaw = entity.Id == playerContext.entityId && entity.Get<ArcheryAim>() is ArcheryAim aim
                    ? RiderLocalYaw(visual.transform.eulerAngles.y, aim.Yaw)
                    : 0f;
                seat.Model.localPosition = SeatLocalPosition(seat.RestPosition);
                seat.Model.localRotation = Quaternion.Euler(0f, localYaw, 0f) * seat.RestRotation;
            }
        }

        private Seat Dress(GameObject visual)
        {
            var model = visual.transform.Find(ChibiRig.ModelChildName);
            if (model == null)
            {
                Debug.LogWarning("[MountedHorseView] 모델 자식이 없는 몸이라 안장에 못 올린다: " + visual.name);
            }
            return new Seat
            {
                Visual = visual,
                Horse = BuildHorse(visual.transform),
                Model = model,
                RestPosition = model != null ? model.localPosition : Vector3.zero,
                RestRotation = model != null ? model.localRotation : Quaternion.identity,
            };
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
                if (pair.Value.Horse != null)
                {
                    Object.Destroy(pair.Value.Horse);
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
