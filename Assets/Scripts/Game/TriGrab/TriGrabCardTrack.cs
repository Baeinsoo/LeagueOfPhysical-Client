using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 카드 자리 기록. 서버가 2틱마다 보내는 카드 자리(<see cref="TriGrabCardsToC"/>, 비신뢰)를 틱 순서로 모아 두고,
    /// 그리는 시각(원격 몸과 같은 렌더 틱)을 감싸는 앞뒤 두 샘플 사이를 잇는다. 범위 밖이면 가장 가까운 샘플에 멈춘다
    /// (GameFramework <see cref="GameFramework.Netcode.SnapshotInterpolation.Solve"/>와 같은 브래킷 탐색 — 외삽하지 않는다).
    /// </summary>
    public class TriGrabCardTrack
    {
        /// <summary>보관하는 길이(틱). 가장 최근 샘플에서 이만큼 지난 것은 버린다.</summary>
        public const long KeepTicks = 50;

        private sealed class Sample
        {
            public long Tick;
            public readonly Dictionary<int, Vector2> Pos = new Dictionary<int, Vector2>();
        }

        private readonly TriGrabClientState state;
        private readonly List<Sample> samples = new List<Sample>();
        private readonly Stack<Sample> pool = new Stack<Sample>();
        private readonly List<double> times = new List<double>();
        private readonly List<Vector2> found = new List<Vector2>();

        public TriGrabCardTrack(TriGrabClientState state)
        {
            this.state = state;
        }

        public int Count => samples.Count;
        public long NewestTick => samples.Count > 0 ? samples[samples.Count - 1].Tick : long.MinValue;

        /// <summary>한 틱의 카드 자리. 이미 받은 가장 최근 틱보다 오래되거나 같으면 버린다(늦게 온 비신뢰 소식).</summary>
        public bool Push(long tick, IList<int> ids, IList<float> xs, IList<float> zs)
        {
            if (samples.Count > 0 && tick <= NewestTick)
            {
                return false;
            }
            var s = pool.Count > 0 ? pool.Pop() : new Sample();
            s.Tick = tick;
            s.Pos.Clear();
            int n = Mathf.Min(ids.Count, Mathf.Min(xs.Count, zs.Count));
            for (int i = 0; i < n; i++) s.Pos[ids[i]] = new Vector2(xs[i], zs[i]);
            samples.Add(s);
            while (samples.Count > 0 && samples[0].Tick < tick - KeepTicks)
            {
                pool.Push(samples[0]);
                samples.RemoveAt(0);
            }
            return true;
        }

        /// <summary>
        /// 그 렌더 틱의 카드 자리. 사라진(Gone) 카드와 샘플이 하나도 없는 카드는 false.
        /// </summary>
        public bool TryGet(int id, double renderTick, out Vector2 pos)
        {
            pos = default;
            if (state != null && state.TryGetCard(id, out var card) && card.Gone)
            {
                return false;
            }
            times.Clear();
            found.Clear();
            foreach (var s in samples)
            {
                if (!s.Pos.TryGetValue(id, out var p)) continue;
                times.Add(s.Tick);
                found.Add(p);
            }
            if (times.Count == 0)
            {
                return false;
            }
            var b = GameFramework.Netcode.SnapshotInterpolation.Solve(times, renderTick);
            pos = Vector2.LerpUnclamped(found[b.Lower], found[b.Upper], b.Alpha);
            return true;
        }

        public void Clear()
        {
            foreach (var s in samples) pool.Push(s);
            samples.Clear();
        }
    }
}
