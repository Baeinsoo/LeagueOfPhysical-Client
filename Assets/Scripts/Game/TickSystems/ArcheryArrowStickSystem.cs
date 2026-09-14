using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 날아가던 화살이 과녁 면에 닿는 순간을 <b>틱마다</b> 찾아 둔다. 화면은 이걸 읽어 화살을 거기서
    /// 멈춰 그린다.
    ///
    /// <para><b>왜 뷰가 아니라 틱 시스템인가.</b> 서버가 하는 판정(<c>ArcheryHitSystem</c>)과 같은
    /// 모양이어야 한다 — 틱마다 "직전 틱부터 이번 틱까지" 한 구간씩 보면 빠짐도 겹침도 없다.
    /// 프레임마다 보면 프레임 레이트에 따라 구간이 벌어지거나 겹쳐서, 같은 판인데 기기마다 다르게
    /// 보인다.</para>
    ///
    /// <para><b>점수는 여기서 정하지 않는다.</b> 누가 먼저 맞혔는지는 서버만 안다(과녁이 두세 개뿐이라
    /// 남이 먼저 먹었을 확률이 높다 — spec 7.2). 여기서 하는 일은 그림을 과녁 면에서 세우는 것뿐이라,
    /// 남이 먼저 먹었더라도 띄웠던 점수가 취소되는 일이 없다.</para>
    /// </summary>
    public class ArcheryArrowStickSystem : GameFramework.Runner.ITickSystem
    {
        private readonly ArcheryWorld world;
        private readonly ArcheryConfig config;
        private readonly IMatchSeed matchSeed;
        private readonly ArcheryConsumed consumed;
        private readonly float tickInterval;

        private readonly Dictionary<(string shooterId, long fireTick), Impact> impacts =
            new Dictionary<(string, long), Impact>();
        private readonly Dictionary<(string shooterId, long fireTick), long> checkedUpToTick =
            new Dictionary<(string, long), long>();
        private readonly List<ArcheryTarget> targets = new List<ArcheryTarget>();
        private readonly List<(string, long)> stale = new List<(string, long)>();

        public readonly struct Impact
        {
            public readonly int Wave;
            public readonly int Slot;

            /// <summary>발사 뒤 이만큼 지난 시점에 닿았다(초).</summary>
            public readonly float Seconds;

            public Impact(int wave, int slot, float seconds)
            {
                Wave = wave;
                Slot = slot;
                Seconds = seconds;
            }
        }

        public ArcheryArrowStickSystem(ArcheryWorld world, ArcheryConfig config, IMatchSeed matchSeed,
                                       ArcheryConsumed consumed, float tickInterval)
        {
            this.world = world;
            this.config = config;
            this.matchSeed = matchSeed;
            this.consumed = consumed;
            this.tickInterval = tickInterval;
        }

        /// <summary>이 화살이 어디에 꽂혔나. 아직 안 닿았으면 false.</summary>
        public bool TryGetImpact(string shooterId, long fireTick, out Impact impact)
            => impacts.TryGetValue((shooterId, fireTick), out impact);

        public void Tick(long tick, float deltaTime)
        {
            int wave = ArcheryWaveGenerator.WaveIndexAt(tick, world.GameplayStartTick, config);
            targets.Clear();
            if (wave >= 0)
            {
                ArcheryWaveGenerator.Fill(targets, matchSeed.Value, wave, config);
            }

            var shots = world.Shots;
            for (int i = 0; i < shots.Count; i++)
            {
                Advance(shots[i], wave, tick);
            }

            ForgetGoneArrows(shots);
        }

        private void Advance(in ArcheryShot shot, int wave, long tick)
        {
            var key = (shot.ShooterId, shot.FireTick);
            if (impacts.ContainsKey(key) || wave < 0)
            {
                return;
            }

            //  남의 화살은 서버를 거쳐 오느라 이미 여러 틱 지난 뒤에 목록에 들어온다. 그래서 "직전 틱
            //  하나"만 보면 그 사이의 교차를 통째로 놓친다 — 가까운 과녁일수록 잘 놓친다.
            //  어디까지 봤는지 틱 단위로 기억해 두고 발사 틱부터 따라잡는다.
            if (checkedUpToTick.TryGetValue(key, out long from) == false)
            {
                from = shot.FireTick;
            }

            for (long t = from; t < tick; t++)
            {
                float fromSeconds = (t - shot.FireTick) * tickInterval;
                float toSeconds = fromSeconds + tickInterval;
                if (CrossesLiveTarget(shot, wave, fromSeconds, toSeconds, out int slot, out float at))
                {
                    impacts[key] = new Impact(wave, slot, at);
                    checkedUpToTick[key] = tick;
                    return;
                }
            }
            checkedUpToTick[key] = tick;
        }

        private bool CrossesLiveTarget(in ArcheryShot shot, int wave, float fromSeconds, float toSeconds,
                                       out int slot, out float atSeconds)
        {
            Vector3 from = ArcheryTrajectory.PositionAt(shot, fromSeconds);
            Vector3 to = ArcheryTrajectory.PositionAt(shot, toSeconds);
            for (int i = 0; i < targets.Count; i++)
            {
                //  이미 먹힌 과녁에는 안 꽂힌다 — 서버가 사라졌다고 한 자리다.
                if (consumed.IsTargetGone(wave, targets[i].SlotIndex))
                {
                    continue;
                }
                if (ArcheryHitTest.SegmentHitsSphere(from, to, targets[i].Origin, targets[i].Radius, out float t))
                {
                    slot = targets[i].SlotIndex;
                    atSeconds = Mathf.Lerp(fromSeconds, toSeconds, t);
                    return true;
                }
            }
            slot = -1;
            atSeconds = 0f;
            return false;
        }

        //  월드가 수명 다한 화살을 지우면 우리 기록도 같이 지운다. 안 그러면 한 판 내내 자란다.
        private void ForgetGoneArrows(IReadOnlyList<ArcheryShot> shots)
        {
            if (impacts.Count == 0 && checkedUpToTick.Count == 0)
            {
                return;
            }

            var live = new HashSet<(string, long)>();
            for (int i = 0; i < shots.Count; i++)
            {
                live.Add((shots[i].ShooterId, shots[i].FireTick));
            }

            stale.Clear();
            foreach (var pair in checkedUpToTick)
            {
                if (live.Contains(pair.Key) == false)
                {
                    stale.Add(pair.Key);
                }
            }
            for (int i = 0; i < stale.Count; i++)
            {
                checkedUpToTick.Remove(stale[i]);
                impacts.Remove(stale[i]);
            }
        }
    }
}
