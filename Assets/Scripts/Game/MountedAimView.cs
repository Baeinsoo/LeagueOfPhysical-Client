using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 말 활쏘기의 어깨 뒤 3인칭 카메라. 화면 가운데가 겨누는 점을 조준으로 넘기고,
    /// 시위를 당기면 화각을 좁히고 카메라를 가까이 당긴다. 당긴 정도는 시뮬 상태를 읽는다.
    /// <para>화살은 기수 눈(발밑 + <see cref="MountedArcheryConfig.RiderEyeHeight"/>)에서 떠나는데 카메라는
    /// 어깨 옆으로 비켜 있다. 카메라 각을 그대로 넘기면 화살이 가운데 줄과 나란히 비켜 날아가 겨눈 데에 안 꽂힌다 —
    /// 그래서 가운데 줄이 맞히는 점을 찾고, 눈에서 그 점을 향한 각을 조준으로 쓴다.
    /// 가운데 줄은 땅·바위뿐 아니라 <b>화면에 그려진 남의 말·기수</b>(서버와 같은 판정 구)에도 걸린다 —
    /// 사람을 빼면 줄이 몸을 뚫고 먼 땅을 잡아 화살이 몸 옆으로 비켜 간다.</para>
    /// </summary>
    public class MountedAimView : ILateTickable, IDisposable
    {
        // 쏘는 순간 시뮬은 당김을 0으로 한 번에 떨어뜨린다 — 그대로 따라가면 화각이 튄다.
        private const float RisePerSecond = 6f;
        private const float FallPerSecond = 2.5f;

        public const float MaxAimDistance = 300f;

        private readonly PlayerInputManager input;
        private readonly CameraController cameraController;
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly EntityRenderClock renderClock;
        private readonly ActorRegistry actorRegistry;
        private readonly MountedArcheryClientState state;
        private readonly float eyeHeight;
        private readonly List<MountedPredictedHits.Target> bodies = new List<MountedPredictedHits.Target>();
        private readonly int worldLayerMask = LayerMask.GetMask("Default");

        private float shownDrawRatio;
        private bool cameraReset;

        public MountedAimView(PlayerInputManager input, CameraController cameraController,
                              IPlayerContext playerContext, GameFramework.World.EntityRegistry entityRegistry,
                              MountedArcheryConfig config, EntityRenderClock renderClock,
                              ActorRegistry actorRegistry, MountedArcheryClientState state)
        {
            this.renderClock = renderClock;
            this.actorRegistry = actorRegistry;
            this.state = state;
            this.input = input;
            this.cameraController = cameraController;
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
            eyeHeight = config.RiderEyeHeight;
        }

        /// <summary>눈에서 목표 점을 향한 (좌우, 위아래) 각도(도). 위아래는 <b>양수가 위</b>(조준 입력의 부호)다.</summary>
        public static Vector2 AimAngles(Vector3 eye, Vector3 target)
        {
            Vector3 d = target - eye;
            float horizontal = Mathf.Sqrt(d.x * d.x + d.z * d.z);
            return new Vector2(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, Mathf.Atan2(d.y, horizontal) * Mathf.Rad2Deg);
        }

        /// <summary>
        /// 카메라 가운데 줄을 어디서부터 쏠지. 카메라와 기수 사이에 있는 것(뒤의 바위 등)을 맞히면 눈 뒤를
        /// 겨누게 되므로, 줄 위에서 눈과 가장 가까운 점부터 잰다.
        /// </summary>
        public static Vector3 RayStart(Vector3 cameraPosition, Vector3 forward, Vector3 eye)
            => cameraPosition + forward * Mathf.Max(0f, Vector3.Dot(eye - cameraPosition, forward));

        /// <summary>가운데 줄이 맞힌 점, 없으면 줄을 따라 <paramref name="maxDistance"/>만큼 간 점.</summary>
        public static Vector3 AimPoint(Vector3 cameraPosition, Vector3 forward, Vector3 eye, Vector3? hit, float maxDistance)
            => hit ?? RayStart(cameraPosition, forward, eye) + forward * maxDistance;

        /// <summary>
        /// 가운데 줄(<paramref name="start"/>에서 <paramref name="forward"/>로 <paramref name="maxDistance"/>까지)이
        /// 처음 닿는 남의 몸에서 겨눌 점. 내 몸은 뺀다. 없으면 null.
        /// <para>표면 접점이 아니라 처음 닿은 구를 지나는 구간의 가운데를 고른다 — 화살은 줄에서 0.9 m 옆의 눈에서 떠나므로,
        /// 가장자리를 겨누면 경계에 걸려 맞을지가 운이 된다. 줄이 이미 그 몸 안에서 시작하면(나란히 달릴 때) 그 몸은 뺀다 —
        /// 시작점을 겨누면 옆으로 쏜다.</para>
        /// </summary>
        public static Vector3? NearestBodyPoint(Vector3 start, Vector3 forward, float maxDistance,
                                                IReadOnlyList<MountedPredictedHits.Target> bodies, string myId)
        {
            Vector3 end = start + forward * maxDistance;
            float bestEnter = float.MaxValue;
            float bestMiddle = 0f;
            for (int i = 0; i < bodies.Count; i++)
            {
                var body = bodies[i];
                if (body.Id == myId)
                {
                    continue;
                }
                if (MountedArcheryHitTest.SegmentChord(start, end, body.Position, body.HeadingDeg, out float enter, out float exit)
                    && enter > 0f && enter < bestEnter)
                {
                    bestEnter = enter;
                    bestMiddle = (enter + exit) * 0.5f;
                }
            }
            return bestEnter < float.MaxValue ? Vector3.Lerp(start, end, bestMiddle) : (Vector3?)null;
        }

        /// <summary>두 후보 중 <paramref name="start"/>에 가까운 점. 둘 다 없으면 null.</summary>
        public static Vector3? Nearer(Vector3 start, Vector3? a, Vector3? b)
        {
            if (a.HasValue == false) return b;
            if (b.HasValue == false) return a;
            return (a.Value - start).sqrMagnitude <= (b.Value - start).sqrMagnitude ? a : b;
        }

        /// <summary>
        /// 내가 지금 보는 틱 — 서버가 남의 몸을 그 시점으로 되감아 맞힘을 잰다.
        /// 남은 보간 재생 시계(조금 과거)로 그려지므로 남이 있으면 그 틱, 없으면 내 예측 틱.
        /// </summary>
        private long ViewTick(GameFramework.World.Entity self)
        {
            foreach (var other in entityRegistry.All)
            {
                if (other.Id != self.Id && other.Get<Mount>() != null)
                {
                    return renderClock.TickFor(other.Id);
                }
            }
            return renderClock.TickFor(self.Id);
        }

        public void LateTick()
        {
            var camera = cameraController.MainCamera;
            if (camera == null)
            {
                return;
            }

            var entity = playerContext.entityId != null ? entityRegistry.Get(playerContext.entityId) : null;
            if (entity == null)
            {
                // 탈락해 몸이 사라졌다. 당기다 쓰러졌으면 카메라가 확대된 채 남으므로 처음 한 번만 평소로 되돌린다.
                if (cameraReset == false)
                {
                    cameraReset = true;
                    shownDrawRatio = 0f;
                    camera.fieldOfView = MountedCameraZoom.WideFov;
                    cameraController.DistanceOverride = MountedCameraZoom.WideDistance;
                }
                // 어깨 비킴은 내 기수를 화면 왼쪽에 두려는 것이다. 관전 대상은 가운데에 둔다 — 한 번 잡은 월드 고정
                // 값을 두면 카메라가 돌 때 화면이 옆으로 치우친다.
                cameraController.PivotOffset = Vector3.zero;
                return;
            }
            cameraReset = false;

            input.SetViewTick(ViewTick(entity));

            if (entity.Has<GameFramework.World.Transform>())
            {
                // 화면에 보이는 것은 직전 프레임 카메라가 찍은 그림이다 — 그 카메라의 가운데 줄로 잰다.
                Vector3 eye = GameFramework.World.EntityMotionExtensions.GetPosition(entity) + Vector3.up * eyeHeight;
                Vector3 origin = camera.transform.position;
                Vector3 forward = camera.transform.forward;
                Vector3 start = RayStart(origin, forward, eye);
                Vector3? terrain = Physics.Raycast(start, forward, out RaycastHit info,
                                                   MaxAimDistance, worldLayerMask, QueryTriggerInteraction.Ignore)
                    ? info.point
                    : (Vector3?)null;
                MountedPredictedHits.CollectDrawnTargets(actorRegistry, state, entity.Id, bodies);
                Vector3? hit = Nearer(start, terrain, NearestBodyPoint(start, forward, MaxAimDistance, bodies, entity.Id));
                Vector2 angles = AimAngles(eye, AimPoint(origin, forward, eye, hit, MaxAimDistance));
                input.SetAim(angles.x, angles.y);
            }
            else
            {
                // 유니티의 x 회전은 양수가 아래를 보고 조준 각도는 양수가 위라 부호를 뒤집는다(ArcheryAimView와 같다).
                input.SetAim(cameraController.Yaw, -cameraController.Pitch);
            }

            var aim = entity?.Get<ArcheryAim>();
            float target = aim != null && aim.Drawing ? aim.DrawRatio : 0f;
            float rate = target > shownDrawRatio ? RisePerSecond : FallPerSecond;
            shownDrawRatio = Mathf.MoveTowards(shownDrawRatio, target, rate * Time.deltaTime);

            var (fov, distance) = MountedCameraZoom.For(shownDrawRatio);
            camera.fieldOfView = fov;
            cameraController.DistanceOverride = distance;
            // 카메라가 도는 중심을 기수 눈높이로 올리고(씬의 중심 높이와 상관없이), 오른쪽으로 비켜
            // 기수가 화면 왼쪽 아래로 가게 한다.
            cameraController.PivotOffset = Vector3.up * (eyeHeight - cameraController.PivotHeight)
                                           + camera.transform.right * MountedCameraZoom.ShoulderRight;
        }

        public void Dispose()
        {
            if (cameraController != null)
            {
                cameraController.DistanceOverride = null;
                cameraController.PivotOffset = Vector3.zero;
            }
        }
    }
}
