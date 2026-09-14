using System;
using System.Collections.Generic;

namespace LOP.MapTools
{
    /// <summary>한 갈래를 지평 끝까지 굴려 보고 <b>그 갈래에 대해 잴 수 있는 것 전부</b>.
    /// <see cref="RolloutBranch"/>(지금 기준이 쓰는 두 값)의 확장판이다 — 굴리는 일은 똑같고,
    /// 굴리는 동안 세어 두는 값만 늘렸다.
    ///
    /// <para><b>"끝"은 지평 끝이 아니라 <i>굴리기가 멈춘 자리</i>다.</b> 갈래가 도중에 닿아
    /// 죽으면 거기가 끝이다. 그래서 죽은 갈래의 <see cref="EndVerticalSpeed"/>는 "벽을 맞기
    /// 직전의 속도"이지 "지평 끝의 자세"가 아니다 — 그 구별이 중요한 신호는 살아남은 갈래끼리
    /// 견줄 때만 뜻이 있다.</para></summary>
    public readonly struct BranchOutcome
    {
        /// <summary>굴리는 동안 안 닿고 버틴 틱 수. 전진 속도가 상수라 <see cref="ReachX"/>와
        /// 사실상 같은 값이다 — 이 둘이 하나뿐인 기준이라는 것이 이 측정의 출발점이다.</summary>
        public readonly int AliveTicks;
        public readonly float ReachX;
        /// <summary>멈춘 자리의 세로 속도. 양수면 올라가는 중, 음수면 떨어지는 중이다.</summary>
        public readonly float EndVerticalSpeed;
        /// <summary>멈춘 자리에서 위아래로 빈 곳 중 <b>좁은 쪽</b>(m). 목숨을 정하는 것은 넓은
        /// 쪽이 아니라 좁은 쪽이라 최솟값을 쓴다.</summary>
        public readonly float EndClearance;
        /// <summary>굴리는 동안 천장 가드가 <b>안 막은</b> 틱 수 — 선택지가 살아 있던 시간.</summary>
        public readonly int OpenTicks;
        /// <summary>깔때기 안인지 <b>쟀는가</b>. 굴려 봤는데 관문 입구까지 못 갔으면(지평이
        /// 짧거나 그 전에 죽었으면) 물을 것이 없어 false다 — 그때 이 신호는 "아니다"가 아니라
        /// <b>기권</b>한다. 둘을 뭉개면 "못 쟀다"가 "나쁘다"로 둔갑한다.</summary>
        public readonly bool FunnelMeasured;
        /// <summary>관문 입구를 지나는 그 순간의 (높이, 세로속도)가 깔때기 안이었나.</summary>
        public readonly bool InFunnel;

        public BranchOutcome(int aliveTicks, float reachX, float endVerticalSpeed, float endClearance,
                             int openTicks, bool funnelMeasured, bool inFunnel)
        {
            AliveTicks = aliveTicks;
            ReachX = reachX;
            EndVerticalSpeed = endVerticalSpeed;
            EndClearance = endClearance;
            OpenTicks = openTicks;
            FunnelMeasured = funnelMeasured;
            InFunnel = inFunnel;
        }

        public RolloutBranch AsRolloutBranch() => new RolloutBranch(AliveTicks, ReachX, EndVerticalSpeed);
    }

    /// <summary>갈래를 고르는 데 쓸 수 있는 후보 신호들. <see cref="Current"/>가 <b>지금 쓰는
    /// 것</b>이라 다른 신호는 전부 이것과 견준다 — 비교 대상이 없으면 적중률은 뜻이 없다.</summary>
    public enum BranchSignal
    {
        /// <summary>지금 쓰는 기준 그대로(<see cref="BotRollout.Prefer"/>) — 오래 산 쪽,
        /// 같으면 멀리 간 쪽.</summary>
        Current,
        /// <summary>산 틱 수만. <see cref="Current"/>의 ①만 떼어낸 것이다.</summary>
        AliveTicks,
        /// <summary>도달 x만. <see cref="Current"/>의 ②만 떼어낸 것이다.</summary>
        ReachX,
        /// <summary>끝에서 <b>올라가는 중</b>인 쪽이 낫다고 본다.</summary>
        EndVerticalSpeed,
        /// <summary>끝에서 <b>덜 끼인</b> 쪽이 낫다고 본다.</summary>
        EndClearance,
        /// <summary>굴리는 동안 <b>고를 기회가 더 많이 살아 있던</b> 쪽이 낫다고 본다.</summary>
        OpenTicks,
        /// <summary>관문 깔때기 <b>안으로</b> 들어간 쪽이 낫다고 본다.</summary>
        InFunnel,
    }

    /// <summary>되돌리기가 알려 준 "좋은 갈래" — 사후에 아는 정답이다.</summary>
    public enum GoodBranch
    {
        Flap,
        Coast,
    }

    /// <summary>채점할 틱 하나 — 두 갈래의 성적과, 되돌리기가 알려 준 정답.</summary>
    public readonly struct ScoredTick
    {
        public readonly BranchOutcome Flapped;
        public readonly BranchOutcome Coasted;
        public readonly GoodBranch Good;

        public ScoredTick(in BranchOutcome flapped, in BranchOutcome coasted, GoodBranch good)
        {
            Flapped = flapped;
            Coasted = coasted;
            Good = good;
        }
    }

    /// <summary>한 신호의 성적표. 세 비율의 분모는 <see cref="Ranked"/>(그 신호가 <b>입을 연</b>
    /// 틱)다 — 기권한 틱까지 분모에 넣으면 말수가 적은 신호가 공짜로 좋아 보인다.</summary>
    public readonly struct SignalTally
    {
        /// <summary>좋은 갈래를 더 높게 매겼다.</summary>
        public readonly int Hits;
        /// <summary>두 갈래를 구별하지 못했다.</summary>
        public readonly int Ties;
        /// <summary>나쁜 갈래를 더 높게 매겼다.</summary>
        public readonly int Misses;
        /// <summary>잴 수가 없어 기권했다(깔때기 신호만 해당).</summary>
        public readonly int Abstained;

        public SignalTally(int hits, int ties, int misses, int abstained)
        {
            Hits = hits;
            Ties = ties;
            Misses = misses;
            Abstained = abstained;
        }

        public int Ranked => Hits + Ties + Misses;
        public int Total => Ranked + Abstained;
        public float HitRate => Ranked == 0 ? 0f : Hits / (float)Ranked;
        public float TieRate => Ranked == 0 ? 0f : Ties / (float)Ranked;
        public float MissRate => Ranked == 0 ? 0f : Misses / (float)Ranked;
    }

    /// <summary>
    /// 후보 신호들을 <b>정답을 아는 자리에서 채점</b>한다.
    ///
    /// <para><b>왜 정답을 알 수 있나.</b> 되돌리기(counterfactual)가 이미 "이 틱에서 반대로
    /// 눌렀으면 더 갔다"를 재고 있다. 더 갔으면 <b>안 고른 쪽</b>이 좋은 갈래였고, 반대로
    /// 뒤집어서 <b>덜</b> 갔으면 <b>고른 쪽</b>이 좋은 갈래였다. 사후에 아는 이 사실이 채점표다.</para>
    ///
    /// <para><b>왜 양쪽 라벨을 다 써야 하나 — 안 그러면 측정이 스스로를 속인다.</b>
    /// "뒤집었더니 더 갔다"인 틱만 모아 채점하면, 그 틱들에서는 정답이 <i>언제나</i> 안 고른
    /// 쪽이다. 그러면 <b>지금 기준의 반대만 말하는 신호</b>가 아무 내용 없이 100%를 받는다.
    /// 그래서 "뒤집었더니 덜 갔다"인 틱(정답 = 고른 쪽)도 반드시 함께 넣는다. 두 라벨이 섞여
    /// 있어야 신호가 <b>실제로 좋고 나쁨을 가르는지</b>를 묻는 채점이 된다.</para>
    /// </summary>
    public static class BranchSignals
    {
        /// <summary>이 신호가 보기에 어느 갈래가 나은가. +1 누르는 쪽, −1 안 누르는 쪽, 0 구별 못 함.
        /// 잴 수 없는 신호면 false를 돌려주고 <paramref name="prefer"/>는 뜻이 없다.</summary>
        /// <param name="epsilon">실수를 견주는 신호에서 "사실상 같다"로 볼 폭. 정수·참거짓
        /// 신호는 이 값을 안 본다.</param>
        public static bool TryPrefer(BranchSignal signal, in BranchOutcome flapped, in BranchOutcome coasted,
                                     float epsilon, out int prefer)
        {
            switch (signal)
            {
                case BranchSignal.Current:
                    //  규칙을 베끼지 않고 <b>봇이 쓰는 그 함수</b>를 부른다 — 베끼면 둘이
                    //  갈라져 "지금 기준의 적중률"이 지금 기준의 것이 아니게 된다.
                    prefer = BotRollout.Prefer(flapped.AsRolloutBranch(), coasted.AsRolloutBranch(), epsilon);
                    return true;
                case BranchSignal.AliveTicks:
                    prefer = Compare(flapped.AliveTicks, coasted.AliveTicks);
                    return true;
                case BranchSignal.ReachX:
                    prefer = Compare(flapped.ReachX, coasted.ReachX, epsilon);
                    return true;
                case BranchSignal.EndVerticalSpeed:
                    prefer = Compare(flapped.EndVerticalSpeed, coasted.EndVerticalSpeed, epsilon);
                    return true;
                case BranchSignal.EndClearance:
                    prefer = Compare(flapped.EndClearance, coasted.EndClearance, epsilon);
                    return true;
                case BranchSignal.OpenTicks:
                    prefer = Compare(flapped.OpenTicks, coasted.OpenTicks);
                    return true;
                case BranchSignal.InFunnel:
                    if (flapped.FunnelMeasured == false || coasted.FunnelMeasured == false)
                    {
                        prefer = 0;
                        return false;
                    }
                    prefer = flapped.InFunnel == coasted.InFunnel ? 0 : (flapped.InFunnel ? 1 : -1);
                    return true;
            }
            throw new ArgumentOutOfRangeException(nameof(signal), signal, "unknown branch signal.");
        }

        /// <summary>정답을 아는 틱들에서 이 신호를 채점한다.</summary>
        public static SignalTally Score(BranchSignal signal, IReadOnlyList<ScoredTick> ticks, float epsilon)
        {
            if (ticks == null)
            {
                throw new ArgumentNullException(nameof(ticks));
            }
            int hits = 0, ties = 0, misses = 0, abstained = 0;
            for (int i = 0; i < ticks.Count; i++)
            {
                if (TryPrefer(signal, ticks[i].Flapped, ticks[i].Coasted, epsilon, out int prefer) == false)
                {
                    abstained++;
                    continue;
                }
                if (prefer == 0)
                {
                    ties++;
                    continue;
                }
                bool picked = prefer > 0 ? ticks[i].Good == GoodBranch.Flap : ticks[i].Good == GoodBranch.Coast;
                if (picked)
                {
                    hits++;
                }
                else
                {
                    misses++;
                }
            }
            return new SignalTally(hits, ties, misses, abstained);
        }

        /// <summary>정답을 모르는 틱까지 포함해 <b>이 신호가 몇 번이나 입을 다무는가</b>만 센다
        /// (②가 묻는 동점 비율). 채점과 달리 라벨이 필요 없어 <b>굴려 본 모든 틱</b>에 쓸 수 있다.</summary>
        public static int CountTies(BranchSignal signal, IReadOnlyList<BranchOutcome> flapped,
                                    IReadOnlyList<BranchOutcome> coasted, float epsilon)
        {
            if (flapped == null)
            {
                throw new ArgumentNullException(nameof(flapped));
            }
            if (coasted == null)
            {
                throw new ArgumentNullException(nameof(coasted));
            }
            if (flapped.Count != coasted.Count)
            {
                throw new ArgumentException(
                    $"two branches must come in pairs — got {flapped.Count} flapped and {coasted.Count} coasted.",
                    nameof(coasted));
            }
            int ties = 0;
            for (int i = 0; i < flapped.Count; i++)
            {
                if (TryPrefer(signal, flapped[i], coasted[i], epsilon, out int prefer) && prefer == 0)
                {
                    ties++;
                }
            }
            return ties;
        }

        private static int Compare(int flapped, int coasted)
            => flapped == coasted ? 0 : (flapped > coasted ? 1 : -1);

        //  문턱은 <b>초과</b>라야 한다 — 이상으로 두면 epsilon이 정확히 0일 때 부동소수 잡음이
        //  우열로 세어진다(지금 기준이 쓰는 문턱과 같은 규약).
        private static int Compare(float flapped, float coasted, float epsilon)
            => Math.Abs(flapped - coasted) > epsilon ? (flapped > coasted ? 1 : -1) : 0;
    }
}
