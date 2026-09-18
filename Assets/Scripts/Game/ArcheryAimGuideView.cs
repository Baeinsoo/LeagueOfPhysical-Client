using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 당기는 동안 화살이 그릴 길을 미리 선으로 보여 준다.
    ///
    /// <para>화살이 <b>포물선</b>으로 날아가서 화면 한가운데가 곧 맞는 자리가 아니다 — 십자선을
    /// 띄워 봐야 거짓말이 된다. 그래서 시뮬이 쓰는 바로 그 함수(<see cref="ArcheryTrajectory"/>)에
    /// 지금 조준·당김을 넣어 길을 뽑는다. <b>보이는 선이 곧 화살이 가는 길</b>이다.</para>
    ///
    /// <para>당긴 정도에 따라 속도가 달라지므로 선도 같이 길어진다 — 얼마나 당겼는지가 화면에
    /// 그대로 보인다.</para>
    ///
    /// <para><b>선은 화살이 처음 뭔가에 닿는 곳에서 멈춘다.</b> 예전엔 고정된 1.2초만 그렸는데,
    /// 과녁이 12~90m로 벌어지자 그 숫자가 거리마다 틀린 답을 냈다(가까운 과녁은 선이 뚫고
    /// 지나가고, 먼 과녁은 선이 허공에서 끊겼다). 그래서 화살의 수명(<see cref="ArcheryTrajectory.LifetimeSeconds"/>,
    /// 3초)까지 조금씩 앞으로 밀어 보며 땅·벽(<see cref="Physics.Linecast"/>)과 과녁
    /// (<see cref="ArcheryHitTest"/>, 실제 화살이 꽂히는 계산과 같은 함수)을 둘 다 검사해 먼저
    /// 닿는 지점을 찾는다. 아무것도 안 걸리면 3초 전부를 그린다 — "쐈는데 아무것도 안 맞는다"도
    /// 사실이므로 화면이 거짓말을 하지 않는다.</para>
    /// </summary>
    public class ArcheryAimGuideView : IPostLateTickable, System.IDisposable
    {
        //  선을 잇지 않고 점을 띄엄띄엄 놓는다 — 이어 그으면 굵기가 일정한 띠가 되어 화살표처럼
        //  보이고, 멀고 가까움도 안 읽힌다. 점은 거리에 따라 작아져서 깊이가 그대로 보인다.
        private const int DotCount = 30;
        private const float DotSize = 0.0042f;

        //  0.1초 간격으로 앞으로 밀어 보며 부딪히는지 본다. 이 간격 동안 중력이 휘게 하는 정도는
        //  ⅛·g·dt² ≈ 2.5cm — 가장 작은 과녁(반지름 수십cm)보다 훨씬 작아 눈에 안 띈다.
        //  더 잘게 쪼개면 정확도는 거의 안 느는데 화면에 매 프레임 도는 Physics.Linecast 횟수만
        //  최대(3초÷0.1초=)30번까지 늘어난다 — 대부분은 훨씬 일찍 맞아 그 전에 멈춘다.
        private const float StepSeconds = 0.1f;

        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly CameraController cameraController;
        private readonly GameFramework.Runner.IRunner runner;
        private readonly GameFramework.World.IWorld world;
        private readonly ArcheryCourse course;
        private readonly ArcheryConsumed consumed;
        private readonly ArcheryConfig config;

        //  Character 레이어는 일부러 뺀다 — 화살이 사수 자신의 눈높이에서 출발하므로 넣으면
        //  선이 제 몸에 바로 막히고, 옆 레인 사수 몸에도 걸려 정작 노리는 과녁 전에 멈춰 버린다.
        private readonly int worldLayerMask;

        //  매 프레임 새로 만들지 않고 돌려 쓴다 — course.Fill이 매번 채워 준다.
        private readonly List<ArcheryTarget> targetBuffer = new List<ArcheryTarget>();
        private readonly List<LiveTarget> liveTargets = new List<LiveTarget>();

        private readonly List<GameObject> dots = new List<GameObject>();
        private Material _dotMaterial;

        //  선이 멈출지 검사할 때 쓰는, "지금 살아 있는 과녁 + 그 자리"의 짝.
        private readonly struct LiveTarget
        {
            public readonly ArcheryTarget Target;
            public readonly Vector3 Position;

            public LiveTarget(ArcheryTarget target, Vector3 position)
            {
                Target = target;
                Position = position;
            }
        }

        public ArcheryAimGuideView(IPlayerContext playerContext,
                                   GameFramework.World.EntityRegistry entityRegistry,
                                   CameraController cameraController,
                                   GameFramework.Runner.IRunner runner,
                                   GameFramework.World.IWorld world,
                                   ArcheryCourse course,
                                   ArcheryConsumed consumed,
                                   ArcheryConfig config)
        {
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
            this.cameraController = cameraController;
            this.runner = runner;
            this.world = world;
            this.course = course;
            this.consumed = consumed;
            this.config = config;
            this.worldLayerMask = LayerMask.GetMask("Default");
        }

        //  PostLateUpdate다 — CameraController(LateUpdate)가 이번 프레임 회전을 쓴 *뒤*에 돈다.
        //  먼저 돌면 이번 프레임 각으로 그린 점을 카메라가 직전 프레임 각으로 따라와 덜덜거린다.
        public void PostLateTick()
        {
            var aim = MyAim();
            //  임계치를 못 넘었으면 아직 시위가 안 걸린 것이다 — 쏘지도 않을 길을 그리지 않는다.
            if (aim == null || aim.Drawing == false || aim.DrawRatio < ArcheryAimSystem.DrawThreshold)
            {
                Hide();
                return;
            }

            var entity = entityRegistry.Get(playerContext.entityId);
            if (entity == null || entity.Has<GameFramework.World.Transform>() == false)
            {
                Hide();
                return;
            }

            var camera = cameraController.MainCamera;
            if (camera == null)
            {
                Hide();
                return;
            }

            //  속도는 시뮬이 쏠 때 쓰는 것과 같은 값에서 나와야 한다 — 선이 거짓말을 하면
            //  조준선이 있으나 마나다. 당김은 손가락이 끈 거리가 정하므로 그 값을 그대로 읽는다.
            float speed = ArcheryAimSystem.SpeedFor(aim.DrawRatio);

            //  방향은 시뮬 값(aim.Yaw/Pitch)이 아니라 *지금 화면에 그려진 카메라 회전*에서 뽑는다.
            //  시뮬 값은 틱(20ms)마다 계단으로 바뀌는데 화면은 매 프레임 도니까, 시뮬 값으로 그리면
            //  점만 계단으로 튀어 덜덜거린다. 쏠 때 서버로 가는 각도 결국 이 회전에서 읽은 값이라
            //  (ArcheryAimView.SetAim) 거짓말이 되지도 않는다.
            float yaw = camera.transform.eulerAngles.y;
            float pitch = -Mathf.DeltaAngle(0f, camera.transform.eulerAngles.x);

            //  오래 당기고 있으면 실제로 쏠 화살도 흔들린다(ArcheryAimSystem) — 선이 그 흔들림을
            //  안 보여 주면 "흔들리는지도 몰랐는데 빗나갔다"가 된다. 시뮬과 같은 함수에 같은
            //  위상(내 entityId)을 넣어야 같은 순간엔 같은 흔들림이 나온다.
            Vector2 sway = SwayNow(aim);
            yaw += sway.x;
            pitch += sway.y;

            //  발사할 때와 같은 원점·속도로 만든다. 여기가 어긋나면 선이 거짓말을 한다.
            var shot = new ArcheryShot(
                playerContext.entityId,
                0,
                GameFramework.World.EntityMotionExtensions.GetPosition(entity)
                    + new Vector3(0f, ArcheryAimSystem.EyeHeight, 0f),
                ArcheryTrajectory.DirectionFrom(yaw, pitch) * speed);

            float impactSeconds = FindImpactSeconds(shot);
            Draw(shot, camera, impactSeconds);
        }

        //  화살을 실제로 쏘지 않고 그 길만 조금씩 앞으로 밀어 보며, 땅·벽이나 과녁 중 먼저
        //  닿는 것까지 걸리는 시간을 찾는다. 아무것도 안 걸리면 화살의 수명 전부를 돌려준다.
        private float FindImpactSeconds(in ArcheryShot shot)
        {
            RefreshLiveTargets();

            Vector3 from = shot.Origin;
            float t = 0f;
            while (t < ArcheryTrajectory.LifetimeSeconds - 1e-4f)
            {
                float nextT = Mathf.Min(t + StepSeconds, ArcheryTrajectory.LifetimeSeconds);
                Vector3 to = ArcheryTrajectory.PositionAt(shot, nextT);

                //  한 구간 안에 둘 다 있을 수 있다(과녁 바로 뒤에 벽이나 자리 기둥이 서 있다).
                //  먼저 찾은 쪽이 아니라 **먼저 닿는 쪽**에서 멈춰야 한다.
                float nearest = 2f;   // 구간 매개변수(0~1) 밖의 보초값

                if (Physics.Linecast(from, to, out RaycastHit hit, worldLayerMask, QueryTriggerInteraction.Ignore))
                {
                    float segmentLength = Vector3.Distance(from, to);
                    nearest = segmentLength > 1e-6f ? hit.distance / segmentLength : 0f;
                }

                if (TryHitLiveTargets(from, to, out float targetFraction) && targetFraction < nearest)
                {
                    nearest = targetFraction;
                }

                if (nearest <= 1f)
                {
                    return Mathf.Lerp(t, nextT, nearest);
                }

                from = to;
                t = nextT;
            }

            return ArcheryTrajectory.LifetimeSeconds;
        }

        //  지금 이 순간의 흔들림. ArcheryAimSystem이 발사 시 쓰는 것과 같은 함수·같은 위상이다 —
        //  틱 정보가 아직 없으면(씬 진입 초기) 안 흔든다.
        private Vector2 SwayNow(ArcheryAim aim)
        {
            if (runner?.tickUpdater == null)
            {
                return Vector2.zero;
            }
            double interval = runner.tickUpdater.interval;
            if (interval <= 0d)
            {
                return Vector2.zero;
            }

            double renderTick = (runner.tickUpdater.elapsedTime - interval) / interval;
            float heldSeconds = ArcheryAimSystem.HeldSeconds(aim.DrawStartTick, renderTick, (float)interval);
            int phaseSeed = ArcheryShake.PhaseSeedOf(playerContext.entityId);
            return ArcheryShake.Offset(heldSeconds, phaseSeed, config);
        }

        //  살아 있는 과녁 중 이 구간에서 가장 먼저 맞는 것 하나만 본다 — 두 과녁이 겹쳐 있어도
        //  화살은 하나만 맞고 멈춘다.
        private bool TryHitLiveTargets(Vector3 from, Vector3 to, out float fraction)
        {
            float earliest = 2f;   // 선분 매개변수(0~1) 밖의 보초값
            for (int i = 0; i < liveTargets.Count; i++)
            {
                if (ArcheryHitTest.SegmentHitsTarget(from, to, liveTargets[i].Position, liveTargets[i].Target,
                                                     out float segmentT, out _)
                    && segmentT < earliest)
                {
                    earliest = segmentT;
                }
            }

            fraction = earliest;
            return earliest <= 1f;
        }

        //  ArcheryTargetView와 같은 순간(renderTick)을 물어야 한다 — 다른 시각을 쓰면 화면에 보이는
        //  과녁 자리와 선이 멈추는 자리가 어긋난다.
        private void RefreshLiveTargets()
        {
            liveTargets.Clear();
            if (runner?.tickUpdater == null)
            {
                return;   // 씬 진입 초기거나 언로드 도중
            }
            double interval = runner.tickUpdater.interval;
            if (interval <= 0d)
            {
                return;
            }

            double renderTick = (runner.tickUpdater.elapsedTime - interval) / interval;
            int step = course.IndexAt((long)System.Math.Floor(renderTick), world.GameplayStartTick);

            targetBuffer.Clear();
            if (step < 0 || (course.StepCount != 0 && step >= course.StepCount))
            {
                return;
            }
            course.Fill(targetBuffer, step, world.GameplayStartTick);

            for (int i = 0; i < targetBuffer.Count; i++)
            {
                var target = targetBuffer[i];
                if (consumed.IsTargetGone(target.WaveIndex, target.SlotIndex))
                {
                    continue;   // 누군가 먹었다 — 서버가 사라졌다고 한 자리에는 안 멈춘다
                }
                if (ArcheryTargetMotion.IsAlive(target, renderTick, (float)interval) == false)
                {
                    continue;   // 아직 안 솟았거나 이미 떨어졌다
                }
                Vector3 position = ArcheryTargetMotion.PositionAt(target, renderTick, (float)interval);
                liveTargets.Add(new LiveTarget(target, position));
            }
        }

        private void Draw(in ArcheryShot shot, Camera camera, float impactSeconds)
        {
            EnsureDots();
            for (int i = 0; i < DotCount; i++)
            {
                //  0초(활 자리)는 건너뛴다 — 발밑에 점이 하나 붙어 있으면 지저분하다.
                float seconds = impactSeconds * (i + 1) / DotCount;
                dots[i].transform.position = ArcheryTrajectory.PositionAt(shot, seconds);
                //  화면에서 같은 굵기로 보이게 카메라 거리에 비례해 키운다 — 안 하면 코앞의 첫 점만
                //  크게 부풀어 보인다(원근 때문). 여기에 뒤로 갈수록 가늘어지는 감쇠를 얹어 방향을 준다.
                float distance = Vector3.Distance(dots[i].transform.position, camera.transform.position);
                float taper = Mathf.Lerp(1f, 0.55f, (float)i / (DotCount - 1));
                dots[i].transform.localScale = Vector3.one * (DotSize * distance * taper);
                dots[i].SetActive(true);
            }
        }

        private ArcheryAim MyAim()
        {
            if (playerContext.entityId == null)
            {
                return null;
            }
            return entityRegistry.Get(playerContext.entityId)?.Get<ArcheryAim>();
        }

        private void Hide()
        {
            for (int i = 0; i < dots.Count; i++)
            {
                if (dots[i] != null)
                {
                    dots[i].SetActive(false);
                }
            }
        }

        private void EnsureDots()
        {
            while (dots.Count < DotCount)
            {
                var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.Destroy(dot.GetComponent<Collider>());   // 그림일 뿐이다
                var renderer = dot.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = DotMaterial();
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                dots.Add(dot);
            }
        }

        private Material DotMaterial()
        {
            if (_dotMaterial == null)
            {
                //  점은 빛을 받을 이유가 없다 — Unlit이라야 어두운 곳에서도 같은 색으로 보인다.
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                //  과녁(노랑)·함정(파랑)·화살(빨강) 어느 것과도 안 겹치게 흰색에 가깝게 둔다.
                _dotMaterial = new Material(shader) { color = new Color(1f, 1f, 1f, 0.85f) };
            }
            return _dotMaterial;
        }

        public void Dispose()
        {
            for (int i = 0; i < dots.Count; i++)
            {
                if (dots[i] != null)
                {
                    Object.Destroy(dots[i]);
                }
            }
            dots.Clear();

            if (_dotMaterial != null)
            {
                Object.Destroy(_dotMaterial);
                _dotMaterial = null;
            }
        }
    }
}
