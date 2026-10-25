using System.Collections.Generic;
using UnityEngine;

namespace LOP.UI
{
    /// <summary>
    /// 정보 HUD가 한 프레임에 보는 세상 한 장. 월드·그림·시계를 읽는 일(<see cref="IRaceHudSource"/>)과
    /// 그것으로 순위·시간을 정하는 일(<see cref="RaceHudViewModel"/>)을 갈라 두려고 있다 — 뒤쪽을 가짜 한 장으로 시험한다.
    /// </summary>
    public class RaceHudFrame
    {
        public readonly List<RaceBirdSample> Birds = new List<RaceBirdSample>();
        public string MyId;
        /// <summary>출발 틱. <c>long.MaxValue</c> = 아직 모름(<see cref="MatchStartState"/>와 같은 표현).</summary>
        public long StartTick = long.MaxValue;
        public double NowSeconds;
        public double SecondsPerTick;
        public long MyFinishedTick = FinishState.NotFinished;
        public float StartX;
        public float FinishX;
        /// <summary>출발 x와 결승선 x를 둘 다 알고 결승선이 앞에 있다.</summary>
        public bool HasCourse;
        /// <summary>맵의 구간 표식(x 오름차순). 표식이 없는 맵(전통)이면 비어 있다.</summary>
        public readonly List<CourseSection> Sections = new List<CourseSection>();

        public void Clear()
        {
            Birds.Clear();
            MyId = null;
            StartTick = long.MaxValue;
            NowSeconds = 0d;
            SecondsPerTick = 0d;
            MyFinishedTick = FinishState.NotFinished;
            StartX = 0f;
            FinishX = 0f;
            HasCourse = false;
            Sections.Clear();
        }
    }

    /// <summary>HUD 한 장을 채운다. 실물은 <see cref="RaceHudSource"/>.</summary>
    public interface IRaceHudSource
    {
        void Fill(RaceHudFrame frame);
    }

    /// <summary>진행 막대의 점 하나.</summary>
    public readonly struct RaceHudDot
    {
        public readonly float Ratio;
        public readonly bool Mine;
        public readonly Color Color;

        public RaceHudDot(float ratio, bool mine, Color color)
        {
            Ratio = ratio;
            Mine = mine;
            Color = color;
        }
    }

    /// <summary>진행 막대 바탕의 구간 띠 하나(spec §1·§5). 비율은 막대 위 자리(0 = 출발, 1 = 결승선).</summary>
    public readonly struct RaceHudBand
    {
        public readonly float From;
        public readonly float To;
        public readonly Color Color;

        public RaceHudBand(float from, float to, Color color)
        {
            From = from;
            To = to;
            Color = color;
        }
    }

    /// <summary>지금 HUD에 뜬 큰 글자 띠(spec §5). 한 번에 하나다.</summary>
    public enum HudBannerKind
    {
        None,
        /// <summary>"n막" + 구간 이름 + 막 색 띠.</summary>
        Section,
        /// <summary>마지막 구간 — 분홍 띠 LAST STRETCH!.</summary>
        LastStretch,
    }
}
