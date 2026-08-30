using System.Collections.Generic;

namespace FlappyRace
{
    /// <summary>맵에서 새가 끼는 자리 하나를 감싸는 사각 구역.</summary>
    public readonly struct TrapRegion
    {
        public readonly float MinX;
        public readonly float MaxX;
        public readonly float MinY;
        public readonly float MaxY;

        public TrapRegion(float minX, float maxX, float minY, float maxY)
        {
            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
        }

        public static TrapRegion Around(float x, float y) => new TrapRegion(x, x, y, y);

        public TrapRegion Grown(float x, float y) => new TrapRegion(
            x < MinX ? x : MinX, x > MaxX ? x : MaxX,
            y < MinY ? y : MinY, y > MaxY ? y : MaxY);

        /// <summary>이 구역에서 점까지의 거리. 안이면 0.</summary>
        public float DistanceTo(float x, float y)
        {
            float dx = x < MinX ? MinX - x : (x > MaxX ? x - MaxX : 0f);
            float dy = y < MinY ? MinY - y : (y > MaxY ? y - MaxY : 0f);
            return (float)System.Math.Sqrt(dx * dx + dy * dy);
        }

        public bool IsNear(in TrapRegion other, float distance)
            => DistanceTo(other.MinX, other.MinY) <= distance
            || DistanceTo(other.MaxX, other.MaxY) <= distance
            || DistanceTo(other.MinX, other.MaxY) <= distance
            || DistanceTo(other.MaxX, other.MinY) <= distance;

        public TrapRegion Merged(in TrapRegion other) => new TrapRegion(
            MinX < other.MinX ? MinX : other.MinX, MaxX > other.MaxX ? MaxX : other.MaxX,
            MinY < other.MinY ? MinY : other.MinY, MaxY > other.MaxY ? MaxY : other.MaxY);
    }

    /// <summary>
    /// 낌 지점 하나하나를 사람이 고칠 수 있는 <b>구역</b>으로 묶는다. 스캔은 격자라 한 틈에서
    /// 수십 개의 점이 쏟아지는데, 그걸 그대로 보고하면 목록이 수천 줄이 되어 못 쓴다.
    /// 물리도 엔진도 모르는 순수 계산이라 테스트로 고정할 수 있다.
    /// </summary>
    public static class TrapClustering
    {
        /// <param name="mergeDistance">이 거리 안이면 같은 틈으로 본다.</param>
        public static List<TrapRegion> Cluster(IReadOnlyList<(float X, float Y)> points, float mergeDistance)
        {
            var regions = new List<TrapRegion>();
            if (points == null)
            {
                return regions;
            }

            for (int i = 0; i < points.Count; i++)
            {
                var (x, y) = points[i];
                int hit = -1;
                for (int r = 0; r < regions.Count; r++)
                {
                    if (regions[r].DistanceTo(x, y) <= mergeDistance)
                    {
                        hit = r;
                        break;
                    }
                }
                if (hit < 0)
                {
                    regions.Add(TrapRegion.Around(x, y));
                }
                else
                {
                    regions[hit] = regions[hit].Grown(x, y);
                }
            }

            //  구역이 커지면서 서로 닿게 된 경우를 합친다. 한 번만 돌면 "A와 B가 합쳐진 뒤 C와도
            //  닿게 된" 경우를 놓치므로, 더 합칠 것이 없을 때까지 반복한다.
            bool merged = true;
            while (merged)
            {
                merged = false;
                for (int a = 0; a < regions.Count && !merged; a++)
                {
                    for (int b = a + 1; b < regions.Count; b++)
                    {
                        if (regions[a].IsNear(regions[b], mergeDistance) == false)
                        {
                            continue;
                        }
                        regions[a] = regions[a].Merged(regions[b]);
                        regions.RemoveAt(b);
                        merged = true;
                        break;
                    }
                }
            }

            //  코스를 앞에서부터 훑을 수 있게 x 순으로 낸다.
            regions.Sort((l, r) => l.MinX.CompareTo(r.MinX));
            return regions;
        }
    }
}
