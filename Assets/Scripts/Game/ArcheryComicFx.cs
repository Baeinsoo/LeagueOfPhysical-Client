using UnityEngine;

namespace LOP
{
    public enum ArcheryComicEvent { Bull, RobinHood, Chicken, Miss, CrowdHit }
    public enum ArcheryComicAnchor { ShooterHead, Point }

    public readonly struct ArcheryComicCue
    {
        /// <summary>이펙트 아틀라스 칸(0 효과선 · 1 연기 · 2 "!" · 3 땀 · 4 우울 줄 · 5 반짝이). 첫 칸이 크게, 나머지는 옆에 작게.</summary>
        public readonly int[] Cells;
        public readonly ArcheryComicAnchor Anchor;
        /// <summary>0보다 크면 그 사람 얼굴을 이만큼 놀람으로.</summary>
        public readonly float SurpriseSeconds;

        public ArcheryComicCue(int[] cells, ArcheryComicAnchor anchor, float surpriseSeconds)
        {
            Cells = cells;
            Anchor = anchor;
            SurpriseSeconds = surpriseSeconds;
        }
    }

    /// <summary>사건 → 만화 이펙트(바람의 지휘봉식). 화면을 덮지 않아 조준 중에도 뜬다.</summary>
    public static class ArcheryComicFx
    {
        public const float Life = 0.9f;
        private const float PopPeak = 0.08f;
        private const float PopSettle = 0.12f;
        private const float Fade = 0.25f;

        public static ArcheryComicCue For(ArcheryComicEvent e)
        {
            switch (e)
            {
                case ArcheryComicEvent.Bull: return new ArcheryComicCue(new[] { 0, 5 }, ArcheryComicAnchor.ShooterHead, 0f);
                case ArcheryComicEvent.RobinHood: return new ArcheryComicCue(new[] { 2 }, ArcheryComicAnchor.ShooterHead, Life);
                case ArcheryComicEvent.Chicken: return new ArcheryComicCue(new[] { 2, 3 }, ArcheryComicAnchor.ShooterHead, Life);
                case ArcheryComicEvent.Miss: return new ArcheryComicCue(new[] { 4, 3 }, ArcheryComicAnchor.ShooterHead, 0f);
                default: return new ArcheryComicCue(new[] { 1 }, ArcheryComicAnchor.Point, 0f);
            }
        }

        public static float ScaleAt(float t)
        {
            if (t <= 0f) return 0f;
            if (t < PopPeak) return Mathf.Lerp(0f, 1.15f, t / PopPeak);
            if (t < PopSettle) return Mathf.Lerp(1.15f, 1f, (t - PopPeak) / (PopSettle - PopPeak));
            return 1f;
        }

        public static float AlphaAt(float t) => Mathf.Clamp01((Life - t) / Fade);
    }
}
