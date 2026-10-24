using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 시위를 당기는 동안 화살이 지금 떼면 실제로 닿을 자리에 작은 점을 찍는다.
    /// 조준선은 곧은 줄이지만 화살은 중력으로 떨어진다(30 m에서 약 1.2 m) — 멀리 쏠 때 얼마나 위를 겨눌지 보이게 한다.
    /// 닿는 곳은 땅·바위와 화면에 그려진 남의 몸(서버와 같은 판정 구)이다.
    /// </summary>
    public class MountedLandingMarker : ILateTickable, IDisposable
    {
        private const float StepSeconds = 1f / 60f;
        // 화면에서 늘 비슷한 크기로 보이게 카메라와의 거리에 비례해 키운다.
        private const float SizePerMeter = 0.012f;
        private const float MinSize = 0.08f;

        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly ActorRegistry actorRegistry;
        private readonly MountedArcheryClientState state;
        private readonly CameraController cameraController;
        private readonly MountedArcheryConfig config;
        private readonly List<MountedPredictedHits.Target> bodies = new List<MountedPredictedHits.Target>();

        private readonly Func<Vector3, Vector3, Vector3?> firstContact;

        private GameObject marker;
        private Material material;

        public MountedLandingMarker(IPlayerContext playerContext, GameFramework.World.EntityRegistry entityRegistry,
                                    ActorRegistry actorRegistry, MountedArcheryClientState state,
                                    CameraController cameraController, MountedArcheryConfig config)
        {
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
            this.actorRegistry = actorRegistry;
            this.state = state;
            this.cameraController = cameraController;
            this.config = config;
            firstContact = FirstContact;
        }

        /// <summary>
        /// <paramref name="origin"/>에서 <paramref name="velocity"/>로 떠난 화살이 <paramref name="maxSeconds"/> 안에 처음 닿는 점.
        /// <paramref name="firstContact"/>는 한 걸음 구간(앞→뒤)에서 처음 닿는 점을 준다. 안 닿으면 null.
        /// </summary>
        public static Vector3? LandingPoint(Vector3 origin, Vector3 velocity, float maxSeconds, float stepSeconds,
                                           Func<Vector3, Vector3, Vector3?> firstContact)
        {
            var shot = new ArcheryShot(string.Empty, 0, origin, velocity);
            Vector3 previous = origin;
            int steps = Mathf.CeilToInt(maxSeconds / stepSeconds);
            for (int i = 1; i <= steps; i++)
            {
                Vector3 current = ArcheryTrajectory.PositionAt(shot, i * stepSeconds);
                Vector3? contact = firstContact(previous, current);
                if (contact.HasValue)
                {
                    return contact;
                }
                previous = current;
            }
            return null;
        }

        public void LateTick()
        {
            var entity = playerContext.entityId != null ? entityRegistry.Get(playerContext.entityId) : null;
            var aim = entity?.Get<ArcheryAim>();
            var camera = cameraController.MainCamera;
            // 문턱 아래에서 떼면 화살이 안 나간다 — 나가지 않을 발의 자리는 찍지 않는다.
            if (aim == null || aim.Drawing == false || aim.DrawRatio < ArcheryAimSystem.DrawThreshold || camera == null || entity.Has<GameFramework.World.Transform>() == false)
            {
                Hide();
                return;
            }

            Vector3 origin = GameFramework.World.EntityMotionExtensions.GetPosition(entity) + Vector3.up * config.RiderEyeHeight;
            float speed = ArcheryAimSystem.SpeedFor(aim.DrawRatio, config.ArrowMinSpeed, config.ArrowMaxSpeed);
            Vector3 velocity = ArcheryTrajectory.DirectionFrom(aim.Yaw, aim.Pitch) * speed;

            MountedPredictedHits.CollectDrawnTargets(actorRegistry, state, entity.Id, bodies);
            Vector3? point = LandingPoint(origin, velocity, ArcheryTrajectory.LifetimeSeconds, StepSeconds, firstContact);
            if (point.HasValue == false)
            {
                Hide();
                return;
            }

            Show(point.Value, camera.transform.position);
        }

        private Vector3? FirstContact(Vector3 from, Vector3 to)
        {
            Vector3? terrain = MountedTerrain.Linecast(from, to, out RaycastHit hit)
                ? hit.point
                : (Vector3?)null;
            Vector3? body = null;
            float best = float.MaxValue;
            foreach (var target in bodies)
            {
                if (MountedArcheryHitTest.SegmentHits(from, to, target.Position, target.HeadingDeg, out float along) && along < best)
                {
                    best = along;
                    body = Vector3.Lerp(from, to, along);
                }
            }
            return MountedAimView.Nearer(from, terrain, body);
        }

        private void Show(Vector3 point, Vector3 cameraPosition)
        {
            if (marker == null)
            {
                marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = "MountedLandingMarker";
                UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
                material = LOPToonMaterials.Create(new Color(1f, 0.25f, 0.2f));
                var renderer = marker.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            marker.SetActive(true);
            marker.transform.position = point;
            marker.transform.localScale = Vector3.one * Mathf.Max(MinSize, Vector3.Distance(cameraPosition, point) * SizePerMeter);
        }

        private void Hide()
        {
            if (marker != null)
            {
                marker.SetActive(false);
            }
        }

        public void Dispose()
        {
            if (marker != null)
            {
                UnityEngine.Object.Destroy(marker);
                marker = null;
            }
            if (material != null)
            {
                UnityEngine.Object.Destroy(material);
                material = null;
            }
        }
    }
}
