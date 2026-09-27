using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 한 발 승부에서 모두의 몸통에 리액션(튕김·기움)을 얹는다. 카메라는 내 보이는 몸을 따라가므로
    /// 내 튕김 높이(<see cref="LocalLift"/>)를 <see cref="ArcheryShootOffCameraRig"/>가 카메라 중심에서 빼서
    /// 시야는 출렁이지 않는다. 루트(판정 자리)는 건드리지 않고 보이는 몸통(visual)만 움직인다. 자리는 진짜다 — 각자 실제 자리에 선다(<see cref="ArcheryShootOffSeats"/>).
    /// <para>보간기(<see cref="PredictedEntityInterpolator"/>)가 매 프레임 <c>LateUpdate</c>에서 몸통의
    /// <b>월드</b> 위치를 덮어쓰고, 이름표(<see cref="CharacterNameplate"/>)는 그 자리를 읽는다. 그래서 그
    /// 사이에서 얹는다 — 도는 시각은 <see cref="ArcheryShootOffReactionDriver"/>가 잡는다.</para>
    /// </summary>
    public class ArcheryShootOffReactionView : IStartable, System.IDisposable
    {
        private readonly ArcheryCourse course;
        private readonly ActorRegistry actorRegistry;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly IPlayerContext playerContext;
        private readonly ArcheryShootOffResultTracker resultTracker;
        private readonly GameFramework.Runner.IRunner runner;

        private readonly HashSet<string> present = new HashSet<string>();
        private readonly List<string> gone = new List<string>();

        //  지난 프레임에 몸통을 어디에·어떤 회전으로 두었나. 보간기가 이번 프레임에 안 썼으면(샘플이
        //  아직 없을 때) 그대로 남아 있어, 또 얹으면 프레임마다 쌓인다 — 지난번 값을 걷어 내고 다시 얹는다.
        private readonly Dictionary<string, (Transform visual, Vector3 written, Vector3 offset,
                                             Quaternion writtenRotation, Quaternion tilt)> applied =
            new Dictionary<string, (Transform, Vector3, Vector3, Quaternion, Quaternion)>();

        private ArcheryShootOffReactionDriver driver;

        /// <summary>이번 프레임에 내 몸을 위로 올린 양(m). 카메라가 이만큼을 빼서 시야를 붙들어 둔다.</summary>
        public float LocalLift { get; private set; }

        public ArcheryShootOffReactionView(ArcheryCourse course, ActorRegistry actorRegistry,
                                           GameFramework.World.EntityRegistry entityRegistry,
                                           IPlayerContext playerContext,
                                           ArcheryShootOffResultTracker resultTracker,
                                           GameFramework.Runner.IRunner runner)
        {
            this.course = course;
            this.actorRegistry = actorRegistry;
            this.entityRegistry = entityRegistry;
            this.playerContext = playerContext;
            this.resultTracker = resultTracker;
            this.runner = runner;
        }

        public void Start()
        {
            driver = new GameObject(nameof(ArcheryShootOffReactionDriver)).AddComponent<ArcheryShootOffReactionDriver>();
            driver.View = this;
        }

        public void Dispose()
        {
            if (driver != null)
            {
                driver.View = null;
                Object.Destroy(driver.gameObject);
                driver = null;
            }
        }

        /// <summary>보간기 뒤·이름표 앞에서 매 프레임 한 번.</summary>
        public void Apply()
        {
            if (course.IsShootOff == false)
            {
                return;
            }

            float now = Time.time;
            double renderTick = RenderTick();
            string me = playerContext.entityId;

            present.Clear();
            LocalLift = 0f;
            foreach (var entity in entityRegistry.All)
            {
                if (entity.Has<ArcheryScore>() == false)
                {
                    continue;
                }
                present.Add(entity.Id);
                var pose = resultTracker.PoseOf(entity.Id, now, renderTick);
                if (entity.Id == me)
                {
                    LocalLift = pose.Lift;
                }
                Place(entity.Id, pose);
            }

            //  나간 사람의 기록은 들고 있을 이유가 없다.
            gone.Clear();
            foreach (var key in applied.Keys)
            {
                if (present.Contains(key) == false)
                {
                    gone.Add(key);
                }
            }
            foreach (var key in gone)
            {
                applied.Remove(key);
            }
        }

        //  리액션(위아래 튕김, 옆으로 기움)을 보이는 몸통에 얹는다.
        private void Place(string id, ArcheryReactionPose pose)
        {
            if (actorRegistry.TryGet(id, out var actor) == false || actor == null || actor.visualGameObject == null)
            {
                applied.Remove(id);
                return;
            }

            Vector3 offset = Vector3.up * pose.Lift;
            Quaternion tilt = Quaternion.AngleAxis(pose.TiltDegrees, Vector3.forward);

            var visual = actor.visualGameObject.transform;
            Vector3 basePosition = visual.position;
            Quaternion baseRotation = visual.rotation;
            if (applied.TryGetValue(id, out var last) && last.visual == visual)
            {
                if (basePosition == last.written)
                {
                    basePosition -= last.offset;   // 보간기가 이번엔 안 썼다 — 지난번 값을 걷어 낸다
                }
                if (baseRotation == last.writtenRotation)
                {
                    baseRotation *= Quaternion.Inverse(last.tilt);
                }
            }

            Vector3 written = basePosition + offset;
            Quaternion writtenRotation = baseRotation * tilt;
            visual.position = written;
            visual.rotation = writtenRotation;
            applied[id] = (visual, written, offset, writtenRotation, tilt);
        }

        private double RenderTick()
        {
            if (runner?.tickUpdater == null || runner.tickUpdater.interval <= 0d)
            {
                return double.PositiveInfinity;   // IsShowing이 false가 되게 — HUD 뷰모델과 맞춘다
            }
            double interval = runner.tickUpdater.interval;
            return (runner.tickUpdater.elapsedTime - interval) / interval;
        }
    }
}
