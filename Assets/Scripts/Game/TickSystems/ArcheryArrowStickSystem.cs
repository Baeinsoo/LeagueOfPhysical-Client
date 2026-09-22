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
        private readonly ArcheryCourse course;
        private readonly ArcheryConsumed consumed;
        private readonly float tickInterval;

        private readonly Dictionary<(string shooterId, long fireTick), Impact> impacts =
            new Dictionary<(string, long), Impact>();
        private readonly Dictionary<(string shooterId, long fireTick), long> checkedUpToTick =
            new Dictionary<(string, long), long>();
        private readonly List<ArcheryTarget> targets = new List<ArcheryTarget>();
        private readonly List<(string, long)> stale = new List<(string, long)>();

        // TryGetTarget 전용 조회 목록 — Tick()이 판정에 쓰는 targets를 여기서 덮어쓰면
        // 서로의 내용을 지운다. 같은 웨이브를 다시 물으면 새로 안 채우고 이걸 재사용한다.
        private readonly List<ArcheryTarget> queryTargets = new List<ArcheryTarget>();
        private int queryWave = -1;

        public readonly struct Impact
        {
            public readonly int Wave;
            public readonly int Slot;

            /// <summary>발사 뒤 이만큼 지난 시점에 닿았다(초).</summary>
            public readonly float Seconds;

            /// <summary>꽂힌 지점 − 그때 과녁 중심. 과녁이 움직여도 화살이 같이 따라가는 데 쓴다.</summary>
            public readonly Vector3 OffsetFromTarget;

            public Impact(int wave, int slot, float seconds, Vector3 offsetFromTarget)
            {
                Wave = wave;
                Slot = slot;
                Seconds = seconds;
                OffsetFromTarget = offsetFromTarget;
            }
        }

        public ArcheryArrowStickSystem(ArcheryWorld world, ArcheryCourse course,
                                       ArcheryConsumed consumed, float tickInterval)
        {
            this.world = world;
            this.course = course;
            this.consumed = consumed;
            this.tickInterval = tickInterval;
        }

        /// <summary>이 화살이 어디에 꽂혔나. 아직 안 닿았으면 false.</summary>
        public bool TryGetImpact(string shooterId, long fireTick, out Impact impact)
            => impacts.TryGetValue((shooterId, fireTick), out impact);

        /// <summary>
        /// 그 화살이 <b>꽂힌 순간의 월드 좌표</b>. 화면이 "+10"을 거기서 띄우는 데 쓴다.
        ///
        /// <para>꽂힌 *뒤*의 자리가 아니라 <b>그 순간</b>이다 — 과녁은 계속 움직이지만 점수 표시는
        /// 맞은 자리에 남아야 "어디를 맞혔길래 그 점수인가"가 읽힌다.</para>
        /// </summary>
        public bool TryGetImpactWorldPosition(string shooterId, long fireTick, out Vector3 position)
        {
            position = default;
            if (TryGetImpact(shooterId, fireTick, out var impact) == false
                || TryGetTarget(impact.Wave, impact.Slot, out var target) == false)
            {
                return false;
            }

            double hitTick = fireTick + impact.Seconds / tickInterval;
            position = ArcheryTargetMotion.PositionAt(target, hitTick, tickInterval) + impact.OffsetFromTarget;
            return true;
        }

        /// <summary>그 웨이브 그 자리의 과녁. 뷰가 꽂힌 화살을 과녁에 붙여 그리는 데 쓴다.</summary>
        public bool TryGetTarget(int wave, int slot, out ArcheryTarget target)
        {
            if (wave != queryWave)
            {
                queryTargets.Clear();
                if (wave >= 0 && (course.StepCount == 0 || wave < course.StepCount))
                {
                    course.Fill(queryTargets, wave, world.GameplayStartTick);
                }
                queryWave = wave;
            }

            for (int i = 0; i < queryTargets.Count; i++)
            {
                if (queryTargets[i].SlotIndex == slot)
                {
                    target = queryTargets[i];
                    return true;
                }
            }
            target = default;
            return false;
        }

        public void Tick(long tick, float deltaTime)
        {
            int wave = course.IndexAt(tick, world.GameplayStartTick);
            targets.Clear();
            //  아직 출발 전이 아니고, 사거리 코스라면 순서가 끝나지도 않은 경우에만 채운다
            //  (웨이브 맵은 StepCount가 0이라 뒤쪽 조건에 안 걸린다).
            if (wave >= 0 && (course.StepCount == 0 || wave < course.StepCount))
            {
                course.Fill(targets, wave, world.GameplayStartTick);
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
            //
            //  ⚠️ 이 따라잡기 루프는 과거 틱을 판정하면서도 **지금 틱의 웨이브 목록(targets)**을
            //  쓴다 — 틱별로 따로 채우지 않는다(그러려면 틱별 웨이브 조회가 필요해 지금 범위 밖).
            //  지금 안 터지는 건 쉼 23틱(WavePeriodTicks120 − BurstTicks97)이 남의 입력 지연
            //  (대략 10틱)보다 넉넉히 길어서, 따라잡는 구간이 "지금 웨이브"를 벗어나는 일이 실제로
            //  안 생기기 때문이다. 이 여유가 좁아지거나(웨이브를 빡빡하게 채우거나) 입력 지연이
            //  늘어나면(패킷 손실·핑 급등) 과거 틱을 엉뚱한 웨이브의 과녁으로 판정하게 된다 —
            //  배포 데이터 검사는 이 조건을 보지 않는다.
            if (checkedUpToTick.TryGetValue(key, out long from) == false)
            {
                from = shot.FireTick;
            }

            for (long t = from; t < tick; t++)
            {
                float fromSeconds = (t - shot.FireTick) * tickInterval;
                float toSeconds = fromSeconds + tickInterval;
                if (CrossesLiveTarget(shot, wave, t + 1, fromSeconds, toSeconds,
                                      out int slot, out float at, out Vector3 targetAt))
                {
                    Vector3 offset = ArcheryTrajectory.PositionAt(shot, at) - targetAt;
                    impacts[key] = new Impact(wave, slot, at, offset);
                    checkedUpToTick[key] = tick;
                    return;
                }
            }
            checkedUpToTick[key] = tick;
        }

        private bool CrossesLiveTarget(in ArcheryShot shot, int wave, long tick,
                                       float fromSeconds, float toSeconds,
                                       out int slot, out float atSeconds, out Vector3 targetAt)
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
                //  서버 판정과 **같은 시각**을 쓴다 — 다르면 화면에선 꽂혔는데 점수는 안 나거나
                //  그 반대다. 있는 자리와 살아 있는지를 둘 다 이 한 시각으로 묻는 것까지 같아야 한다.
                double at = tick - 0.5;

                if (ArcheryTargetMotion.IsAlive(targets[i], at, tickInterval) == false)
                {
                    continue;
                }

                Vector3 candidateTargetAt = ArcheryTargetMotion.PositionAt(targets[i], at, tickInterval);

                if (ArcheryHitTest.SegmentHitsTarget(from, to, candidateTargetAt, targets[i], out float t, out _))
                {
                    slot = targets[i].SlotIndex;
                    atSeconds = Mathf.Lerp(fromSeconds, toSeconds, t);
                    targetAt = candidateTargetAt;
                    return true;
                }
            }
            slot = -1;
            atSeconds = 0f;
            targetAt = default;
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
