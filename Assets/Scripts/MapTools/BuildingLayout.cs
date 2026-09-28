using System;
using System.Collections.Generic;
using System.Text;

namespace LOP.MapTools
{
    /// <summary>x·y 사각형 하나(z는 안 본다).</summary>
    public readonly struct Box2
    {
        public readonly float X0, Y0, X1, Y1;
        public Box2(float x0, float y0, float x1, float y1) { X0 = x0; Y0 = y0; X1 = x1; Y1 = y1; }

        /// <summary>겹친 폭과 높이가 둘 다 <paramref name="tolerance"/>보다 큰가 — 맞닿기만 한 것은 겹침이 아니다.</summary>
        public bool Overlaps(Box2 other, float tolerance)
            => Math.Min(X1, other.X1) - Math.Max(X0, other.X0) > tolerance
            && Math.Min(Y1, other.Y1) - Math.Max(Y0, other.Y0) > tolerance;
    }

    /// <summary>
    /// 2층 빌딩의 세로 칸과 앞벽(spec 2026-09-28 §2.1·§4). 세로 칸은 회랑 바닥에서부터 아래층·층판·위층·지붕 순이다.
    /// 앞벽은 막힌 두 칸(층판·지붕) 앞만 덮고, 통로와 맞닿는 변은 무너진 모양으로 막힌 칸 안쪽으로 들어가 있다 —
    /// 그래서 입구를 절대 가리지 않는다.
    /// </summary>
    public static class BuildingLayout
    {
        public const float LowerLane = 8f;
        public const float Slab = 2f;
        public const float UpperLane = 5f;
        /// <summary>무너진 가장자리가 막힌 칸 안으로 들어가는 최대 깊이. 층판(2m)의 양 변이 다 들어가도 0.8m가 남는다.</summary>
        public const float FacadeJag = 0.6f;
        public const float FacadeStep = 1f;

        static float Floor(BuildingPiece b, float half) => b.BaseY - half;

        public static Box2 LowerLaneBox(BuildingPiece b, float half)
            => new Box2(b.X0, Floor(b, half), b.X1, Floor(b, half) + LowerLane);

        public static Box2 SlabBox(BuildingPiece b, float half)
            => new Box2(b.X0, Floor(b, half) + LowerLane, b.X1, Floor(b, half) + LowerLane + Slab);

        public static Box2 UpperLaneBox(BuildingPiece b, float half)
            => new Box2(b.X0, Floor(b, half) + LowerLane + Slab, b.X1, Floor(b, half) + LowerLane + Slab + UpperLane);

        public static Box2 RoofBox(BuildingPiece b, float half)
            => new Box2(b.X0, Floor(b, half) + LowerLane + Slab + UpperLane, b.X1, b.BaseY + half);

        /// <summary>0..<see cref="FacadeJag"/> 사이의 결정론적 들쭉날쭉함 — 굽을 때마다 같은 모양이 나온다.</summary>
        public static float Jag(float x)
            => FacadeJag * (0.5f + 0.25f * (float)Math.Sin(x * 2.3) + 0.25f * (float)Math.Sin(x * 5.1));

        /// <summary>앞벽 띠(볼록 사다리꼴, <see cref="CourseProfileRule"/>의 띠와 같은 8개 수 순서).</summary>
        public static List<float[]> Facade(BuildingPiece b, float half)
        {
            Box2 slab = SlabBox(b, half), roof = RoofBox(b, half);
            var strips = new List<float[]>();
            for (float a = b.X0; a < b.X1 - 1e-4f; a += FacadeStep)
            {
                float c = Math.Min(a + FacadeStep, b.X1);
                //  층판 앞: 위아래 변이 모두 통로와 맞닿아 둘 다 안으로 들인다(서로 다른 모양이 되게 위상을 민다).
                strips.Add(CourseProfileRule.Strip(a, slab.Y0 + Jag(a), slab.Y1 - Jag(a + 7f),
                                                   c, slab.Y0 + Jag(c), slab.Y1 - Jag(c + 7f)));
                //  지붕 앞: 아랫변만 위층과 맞닿는다. 윗변은 천장이라 곧다.
                strips.Add(CourseProfileRule.Strip(a, roof.Y0 + Jag(a + 3f), roof.Y1,
                                                   c, roof.Y0 + Jag(c + 3f), roof.Y1));
            }
            return strips;
        }

        /// <summary>통로 칸과 겹치는 앞벽 조각의 가운데 x. 비어 있어야 입구가 늘 보인다.</summary>
        public static List<float> FacadeOverLane(IReadOnlyList<Box2> facades, IReadOnlyList<Box2> lanes, float tolerance)
        {
            var hits = new List<float>();
            foreach (Box2 f in facades)
            {
                foreach (Box2 lane in lanes)
                {
                    if (f.Overlaps(lane, tolerance)) { hits.Add((f.X0 + f.X1) * 0.5f); break; }
                }
            }
            return hits;
        }

        /// <summary>맵 검사 🏢 절. 빌딩이 없으면(통로 칸 0) null — 절을 안 찍는다.</summary>
        public static string EntranceSection(IReadOnlyList<Box2> facades, IReadOnlyList<Box2> lanes)
        {
            if (lanes.Count == 0) { return null; }
            var text = new StringBuilder();
            text.AppendLine("── 🏢 빌딩 입구 ──────────────────────");
            if (facades.Count == 0)
            {
                text.AppendLine("  ❌ 통로 표시는 있는데 앞벽(FlappyBuildingFacade)을 못 찾았다");
                return text.ToString().TrimEnd();
            }
            List<float> hits = FacadeOverLane(facades, lanes, 0.01f);
            if (hits.Count == 0)
            {
                text.AppendLine($"  ✅ 앞벽 {facades.Count}조각이 통로 {lanes.Count}칸을 하나도 안 가린다 — 입구가 늘 보인다");
            }
            else
            {
                var xs = new List<string>();
                foreach (float x in hits) { xs.Add(x.ToString("F1")); }
                text.AppendLine($"  ❌ 앞벽이 통로를 가린다 — x {string.Join(", ", xs)}");
            }
            return text.ToString().TrimEnd();
        }
    }
}
