using System.Collections.Generic;
using GameFramework;
using MessagePipe;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 한 발 승부의 지난 라운드 결과와 선수별 마지막 10점 시각을 모아 둔다. 결과 패널(HUD)과 캐릭터
    /// 리액션(좌우 배치 뷰)이 같은 값을 읽는다 — 둘이 따로 세면 패널은 닫혔는데 캐릭터는 계속 뛴다.
    /// </summary>
    public class ArcheryShootOffResultTracker : MessageHandlerBase
    {
        private readonly ISubscriber<WorldEventBatchToC> batchSubscriber;
        private readonly ArcheryCourse course;
        private readonly ArcheryWorld world;
        private readonly ArcheryConfig config;

        private readonly List<ArcheryRoundPlacement> byRank = new List<ArcheryRoundPlacement>();
        private readonly Dictionary<string, float> bullAt = new Dictionary<string, float>();
        private long closeTick = long.MinValue;

        public ArcheryRoundResultEvent Current { get; private set; }
        public IReadOnlyList<ArcheryRoundPlacement> ByRank => byRank;
        public float OpenedAt { get; private set; }
        public int Version { get; private set; }

        public ArcheryShootOffResultTracker(ISubscriber<WorldEventBatchToC> batchSubscriber, ArcheryCourse course,
                                            ArcheryWorld world, ArcheryConfig config)
        {
            this.batchSubscriber = batchSubscriber;
            this.course = course;
            this.world = world;
            this.config = config;
        }

        protected override void Subscribe() => Track(batchSubscriber.Subscribe(OnWorldEventBatch));

        private void OnWorldEventBatch(WorldEventBatchToC msg)
        {
            foreach (var rec in msg.Events)
            {
                if (rec.EventCase == WorldEventToC.EventOneofCase.ArcheryRoundResult)
                {
                    var result = (ArcheryRoundResultEvent)WorldEventWire.FromWire(rec);
                    long close = ArcheryShootOffResultLayout.CloseTick(
                        course.RoundCloseTick(result.roundIndex, world.GameplayStartTick), config.Range.StepGapTicks);
                    OnRoundResult(result, Time.time, close);
                }
                else if (rec.EventCase == WorldEventToC.EventOneofCase.ArcheryHit)
                {
                    OnHit((ArcheryTargetHitEvent)WorldEventWire.FromWire(rec), Time.time);
                }
            }
        }

        public void OnRoundResult(ArcheryRoundResultEvent result, float now, long closeTick)
        {
            Current = result;
            byRank.Clear();
            byRank.AddRange(result.placements);
            byRank.Sort((a, b) => a.Rank != b.Rank ? a.Rank.CompareTo(b.Rank) : a.Distance.CompareTo(b.Distance));
            OpenedAt = now;
            this.closeTick = closeTick;
            Version++;
        }

        //  한 발 승부에서 적중 사건의 points는 띠 점수(1~10)다.
        public void OnHit(ArcheryTargetHitEvent hit, float now)
        {
            if (hit.points == 10)
            {
                bullAt[hit.shooterId] = now;
            }
        }

        public bool IsShowing(double renderTick) => Current != null && renderTick < closeTick;

        public ArcheryReactionPose PoseOf(string entityId, float now, double renderTick)
        {
            var role = ArcheryReactionRole.None;
            if (IsShowing(renderTick))
            {
                for (int i = 0; i < byRank.Count; i++)
                {
                    if (byRank[i].ShooterId == entityId)
                    {
                        role = ArcheryReaction.RoleOf(byRank[i].Rank, byRank.Count, byRank[i].Hit);
                        break;
                    }
                }
            }
            float bull = bullAt.TryGetValue(entityId, out float at) ? at : float.NegativeInfinity;
            return ArcheryReaction.PoseAt(now, bull, role, OpenedAt);
        }
    }
}
