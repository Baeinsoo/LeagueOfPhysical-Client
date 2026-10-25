using System.Collections.Generic;

namespace LOP
{
    /// <summary>순위·진행 막대가 새 한 마리에 대해 아는 것 전부.</summary>
    public readonly struct RaceBirdSample
    {
        public readonly string Id;

        /// <summary>서버가 정한 결승 등수(1부터, 0 = 아직).</summary>
        public readonly int Placement;

        /// <summary>화면에 그려진 x. 남의 새는 보간된 자리다 — 화면과 막대·순위가 같은 말을 해야 한다(spec §1).</summary>
        public readonly float X;

        public RaceBirdSample(string id, int placement, float x)
        {
            Id = id;
            Placement = placement;
            X = x;
        }
    }

    /// <summary>
    /// 화면에 띄우는 순위. 판정 순위가 아니다 — 판정은 서버가 통과 틱으로 한다.
    /// <para>남은 약 0.23초 늦게 그려지므로 접전에선 내 순위가 실제보다 좋게 보일 수 있다(spec §10 열린 항목).
    /// 그래도 그림 x를 쓰는 이유는 화면 속 새의 앞뒤와 숫자가 어긋나면 그쪽이 더 이상하기 때문이다.</para>
    /// </summary>
    public static class RaceStandings
    {
        /// <summary>선두가 0번이 되게 제자리 정렬. 비교가 전순서(id까지)라 정렬이 불안정해도 답은 하나다.</summary>
        public static void Order(List<RaceBirdSample> birds)
        {
            birds.Sort(Compare);
        }

        /// <summary>정렬된 목록에서 이 새의 순위(1부터). 없으면 0 — 화면은 0이면 순위를 숨긴다.</summary>
        public static int RankOf(IReadOnlyList<RaceBirdSample> ordered, string id)
        {
            if (id == null)
            {
                return 0;
            }
            for (int i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].Id == id)
                {
                    return i + 1;
                }
            }
            return 0;
        }

        private static int Compare(RaceBirdSample a, RaceBirdSample b)
        {
            bool aDone = a.Placement > 0;
            bool bDone = b.Placement > 0;
            if (aDone != bDone)
            {
                return aDone ? -1 : 1;   //  결승한 새가 늘 앞
            }

            int order = aDone
                ? a.Placement.CompareTo(b.Placement)   //  결승한 새끼리는 서버 등수
                : b.X.CompareTo(a.X);                  //  달리는 새끼리는 앞선(큰) x가 먼저
            //  같은 x로 출발하는 게 기본이라 동률이 흔하다 — id로 못 박아야 숫자가 안 깜빡인다.
            return order != 0 ? order : string.CompareOrdinal(a.Id, b.Id);
        }
    }
}
