using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 한 발 승부에서 남의 몸을 좌우로 옮겨 그린다. 루트(판정과 같은 자리)는 건드리지 않고
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
        private readonly List<string> others = new List<string>();
        private readonly List<string> gone = new List<string>();
        private readonly Dictionary<string, Vector3> offsets = new Dictionary<string, Vector3>();

        //  지난 프레임에 몸통을 어디에 두었나. 보간기가 이번 프레임에 안 썼으면(샘플이 아직 없을 때)
        //  몸통이 그 자리 그대로라, 간격을 또 얹으면 프레임마다 옆으로 밀려 나간다.
        private readonly Dictionary<string, (Transform visual, Vector3 written, Vector3 offset)> applied =
            new Dictionary<string, (Transform, Vector3, Vector3)>();

        public ArcheryShootOffLineupView(ArcheryCourse course, ActorRegistry actorRegistry,
                                         GameFramework.World.EntityRegistry entityRegistry,
                                         IPlayerContext playerContext)
        {
            this.course = course;
            this.actorRegistry = actorRegistry;
            this.entityRegistry = entityRegistry;
            this.playerContext = playerContext;
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

        /// <summary>남의 몸통을 옆으로 옮긴다. 보간기 뒤·이름표 앞에서 매 프레임 한 번.</summary>
        public void Apply()
        {
            offsets.Clear();
            var lane = course.SharedLane;
            if (lane == null)
            {
                return;   // 한 발 승부가 아니거나 맵 씬이 아직 안 떴다
            }

            Vector3 right = ArcheryTargetMotion.ShooterRightAxis(-lane.Value.Forward);

            //  모든 프레임에서 같은 자리를 지키게 id 순서로 줄 세운다. 나는 가운데라 빼고 센다.
            others.Clear();
            foreach (var entity in entityRegistry.All)
            {
                if (entity.Has<ArcheryScore>() && entity.Id != playerContext.entityId)
                {
                    others.Add(entity.Id);
                }
            }
            others.Sort(string.CompareOrdinal);

            for (int i = 0; i < others.Count; i++)
            {
                string id = others[i];
                Vector3 offset = right * ArcheryShootOffLineup.SlotOffset(i);
                offsets[id] = offset;

                if (actorRegistry.TryGet(id, out var actor) == false || actor == null || actor.visualGameObject == null)
                {
                    applied.Remove(id);
                    continue;
                }

                var visual = actor.visualGameObject.transform;
                Vector3 basePosition = visual.position;
                if (applied.TryGetValue(id, out var last) && last.visual == visual && basePosition == last.written)
                {
                    basePosition -= last.offset;   // 보간기가 이번엔 안 썼다 — 지난번 간격을 걷어 내고 다시 얹는다
                }

                Vector3 written = basePosition + offset;
                visual.position = written;
                applied[id] = (visual, written, offset);
            }

            //  나간 사람의 기록은 들고 있을 이유가 없다.
            gone.Clear();
            foreach (var key in applied.Keys)
            {
                if (offsets.ContainsKey(key) == false)
                {
                    gone.Add(key);
                }
            }
            foreach (var key in gone)
            {
                applied.Remove(key);
            }
        }
    }
}
