using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 기수 밑에 회색 도형 말을 깔고 기수(치비)를 안장 높이로 올린다. 말은 그림일 뿐이다 —
    /// 콜라이더가 없고 이동은 시뮬의 캡슐이 한다. 기수는 조준 방향으로 상체를 돌린다.
    /// <para>몸 그림의 뿌리(visual)는 보간기가 매 프레임 LateUpdate에서 월드 자세째 덮어쓴다(이 뷰는 그보다 먼저 돈다).
    /// 그래서 뿌리는 건드리지 않는다 — 말은 뿌리의 자식으로 붙여 보간된 자리를 같이 따라가게 하고,
    /// 안장 높이와 상체 회전은 뿌리 아래 모델 자식(<see cref="ChibiRig.ModelChildName"/>)에만 준다.</para>
    /// </summary>
    public class MountedHorseView : ILateTickable, System.IDisposable
    {
        //  치비 키가 약 0.85m라 1.55m에서는 다리가 높이 0.85m 몸통 상자(윗면 1.825m) 안에 가리고 상체가 등 위로 보인다.
        public const float SaddleHeight = 1.55f;
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
        private readonly EntityRenderClock renderClock;

        // 몸마다 마지막으로 말을 입힌 그림. 그림이 바뀌면(다시 로드) 다시 입힌다.
        private readonly Dictionary<string, Seat> built = new Dictionary<string, Seat>();
        private readonly List<string> scratch = new List<string>();
        private Material horseMaterial;

        public MountedHorseView(ActorRegistry actorRegistry, GameFramework.World.EntityRegistry entityRegistry,
                                IPlayerContext playerContext, EntityRenderClock renderClock)
        {
            this.renderClock = renderClock;
            this.actorRegistry = actorRegistry;
            this.entityRegistry = entityRegistry;
            this.playerContext = playerContext;
        }

        /// <summary>말 머리 방향에서 조준 방향까지 가까운 쪽으로 잰 각도(도). 기수 모델의 말 기준 회전이다.</summary>
        public static float RiderLocalYaw(float headingYaw, float aimYaw) => Mathf.DeltaAngle(headingYaw, aimYaw);

        /// <summary>
        /// 남의 조준: 그 몸을 <b>그리는 틱</b> 이하에서 가장 최근에 받은 입력의 조준 방향.
        /// 남의 입력은 서버를 거쳐 와서 클라의 라이브 틱보다 늦게 도착한다 — 지금 틱의 입력은 아직 없으므로,
        /// 몸을 그리는(조금 과거) 틱까지 도착한 것 중 마지막 값을 쓴다. 아직 하나도 없으면 false.
        /// </summary>
        public static bool TryGetRemoteAimYaw(InputBuffer buffer, long drawnTick, out float yaw)
        {
            yaw = 0f;
            if (buffer == null)
            {
                return false;
            }
            InputCommand latest = null;
            foreach (var pair in buffer.Commands)   // 틱 오름차순
            {
                if (pair.Key > drawnTick)
                {
                    break;
                }
                latest = pair.Value;
            }
            if (latest == null)
            {
                return false;
            }
            yaw = latest.AimYaw;
            return true;
        }

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
                float localYaw = TryGetAimYaw(entity, out float aimYaw)
                    ? RiderLocalYaw(visual.transform.eulerAngles.y, aimYaw)
                    : 0f;
                seat.Model.localPosition = SeatLocalPosition(seat.RestPosition);
                seat.Model.localRotation = Quaternion.Euler(0f, localYaw, 0f) * seat.RestRotation;
            }
        }

        // 내 기수는 시뮬의 조준, 남의 기수는 서버가 중계한 남의 입력(몸을 그리는 틱까지 받은 마지막 것)의 조준 방향을 따른다.
        private bool TryGetAimYaw(GameFramework.World.Entity entity, out float yaw)
        {
            if (entity.Id == playerContext.entityId)
            {
                var aim = entity.Get<ArcheryAim>();
                yaw = aim != null ? aim.Yaw : 0f;
                return aim != null;
            }

            return TryGetRemoteAimYaw(entity.Get<InputBuffer>(), renderClock.TickFor(entity.Id), out yaw);
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
