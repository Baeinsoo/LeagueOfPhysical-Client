using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 한 발 승부에서 남의 몸을 옆·앞으로 비스듬히 옮겨 그리고 리액션(튕김·기움)을 얹는다. 나는 빼는데,
    /// 카메라가 내 보이는 몸을 따라가서(1인칭, 눈 앞 0.4m) 내 몸을 흔들면 시야가 흔들리기 때문이다.
    /// 루트(판정과 같은 자리)와 화살 시작점(<see cref="DisplayOffsetOf"/>)은 건드리지 않고
    /// 보이는 몸통(visual)만 옮긴다.
    /// <para>보간기(<see cref="PredictedEntityInterpolator"/>)가 매 프레임 <c>LateUpdate</c>에서 몸통의
    /// <b>월드</b> 위치를 덮어쓰고, 이름표(<see cref="CharacterNameplate"/>)는 그 자리를 읽는다. 그래서 그
    /// 사이에서 보간기가 써 둔 자리 위에 간격을 얹는다 — 도는 시각은 <see cref="ArcheryShootOffLineupDriver"/>가 잡는다.</para>
    /// </summary>
    public class ArcheryShootOffLineupView : IStartable, System.IDisposable
    {
        private readonly ArcheryCourse course;
        private readonly ActorRegistry actorRegistry;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly IPlayerContext playerContext;
        //  매 프레임 정렬하므로 비교 함수를 한 번만 만든다(메서드 그룹을 넘기면 부를 때마다 새로 만든다).
        private static readonly System.Comparison<string> ByOrdinal = string.CompareOrdinal;

        private readonly List<string> others = new List<string>();
        private readonly List<string> gone = new List<string>();
        private readonly Dictionary<string, Vector3> offsets = new Dictionary<string, Vector3>();

        //  지난 프레임에 몸통을 어디에·어떤 회전으로 두었나. 보간기가 이번 프레임에 안 썼으면(샘플이
        //  아직 없을 때) 그대로 남아 있어, 또 얹으면 프레임마다 쌓인다 — 지난번 값을 걷어 내고 다시 얹는다.
        private readonly Dictionary<string, (Transform visual, Vector3 written, Vector3 offset,
                                             Quaternion writtenRotation, Quaternion tilt)> applied =
            new Dictionary<string, (Transform, Vector3, Vector3, Quaternion, Quaternion)>();

        private readonly ArcheryShootOffResultTracker resultTracker;
        private readonly GameFramework.Runner.IRunner runner;

        public ArcheryShootOffLineupView(ArcheryCourse course, ActorRegistry actorRegistry,
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

        /// <summary>그 사수의 몸이 화면에서 판정 자리보다 얼마나 옆에 그려지나. 한 발 승부가 아니면 0.</summary>
        public Vector3 DisplayOffsetOf(string entityId)
            => entityId != null && offsets.TryGetValue(entityId, out var o) ? o : Vector3.zero;

        private ArcheryShootOffLineupDriver driver;

        public void Start()
        {
            driver = new GameObject(nameof(ArcheryShootOffLineupDriver)).AddComponent<ArcheryShootOffLineupDriver>();
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

        /// <summary>보간기 뒤·이름표 앞에서 매 프레임 한 번</summary>
        public void Apply()
        {
            offsets.Clear();
            var lane = course.SharedLane;
            if (lane == null)
            {
                return;   // 한 발 승부가 아니거나 맵 씬이 아직 안 떴다
            }

            Vector3 right = ArcheryTargetMotion.ShooterRightAxis(-lane.Value.Forward);
            float now = Time.time;
            double renderTick = RenderTick();

            //  모든 프레임에서 같은 자리를 지키게 id 순서로 줄 세운다. 나는 가운데라 간격 순번에서 뺀다.
            others.Clear();
            string me = playerContext.entityId;
            foreach (var entity in entityRegistry.All)
            {
                if (entity.Has<ArcheryScore>() && entity.Id != me)
                {
                    others.Add(entity.Id);
                }
            }
            others.Sort(ByOrdinal);

            for (int i = 0; i < others.Count; i++)
            {
                offsets[others[i]] = right * ArcheryShootOffLineup.SlotOffset(i)
                                   + lane.Value.Forward * ArcheryShootOffLineup.ForwardOffset(i);
            }

            if (me != null)
            {
                //  카메라가 내 몸을 따라가서 내 리액션은 시야 출렁임이 된다 — 나는 자세를 안 얹는다
                Place(me, Vector3.zero, ArcheryReactionPose.Zero);
            }
            for (int i = 0; i < others.Count; i++)
            {
                string id = others[i];
                Place(id, offsets[id], resultTracker.PoseOf(id, now, renderTick));
            }

            //  나간 사람의 기록은 들고 있을 이유가 없다.
            gone.Clear();
            foreach (var key in applied.Keys)
            {
                if (key != me && offsets.ContainsKey(key) == false)
                {
                    gone.Add(key);
                }
            }
            foreach (var key in gone)
            {
                applied.Remove(key);
            }
        }

        //  좌우 간격 + 리액션(위아래 튕김, 옆으로 기움)을 보이는 몸통에 얹는다.
        private void Place(string id, Vector3 lineOffset, ArcheryReactionPose pose)
        {
            if (actorRegistry.TryGet(id, out var actor) == false || actor == null || actor.visualGameObject == null)
            {
                applied.Remove(id);
                return;
            }

            Vector3 offset = lineOffset + Vector3.up * pose.Lift;
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
