using System;

namespace LOP.UI
{
    /// <summary>
    /// 가로 카드 줄의 넘김 계산. 위치(offset)는 줄을 왼쪽으로 민 픽셀 수이고, 0이면 첫 카드가 맨 앞이다.
    /// 멈추는 자리는 "카드 왼쪽 끝이 줄 앞에 오는 곳"(카드 간격의 배수)과 맨 끝이다.
    /// </summary>
    public readonly struct CarouselPaging
    {
        private const float Slack = 1f;

        /// <summary>보이는 폭.</summary>
        public readonly float Viewport;
        /// <summary>카드 한 장이 차지하는 가로 길이(폭 + 좌우 여백).</summary>
        public readonly float Stride;
        /// <summary>줄 양 끝 여백.</summary>
        public readonly float Padding;
        public readonly int Count;

        public CarouselPaging(float viewport, float stride, float padding, int count)
        {
            Viewport = viewport;
            Stride = stride;
            Padding = padding;
            Count = count;
        }

        public float ContentWidth => Padding * 2 + Stride * Count;
        public float MaxOffset => Math.Max(0f, ContentWidth - Viewport);
        /// <summary>한 번에 다 보이는 카드 수(최소 1).</summary>
        public int PerPage => Stride <= 0 ? 1 : Math.Max(1, (int)Math.Floor((Viewport - Padding * 2) / Stride));
        private float PageWidth => PerPage * Stride;

        public bool CanPrev(float offset) => offset > Slack;
        public bool CanNext(float offset) => offset < MaxOffset - Slack;

        public int PageCount => MaxOffset <= Slack ? 1 : (int)Math.Ceiling((MaxOffset - Slack) / PageWidth) + 1;

        public int PageIndex(float offset)
        {
            if (!CanNext(offset)) return PageCount - 1;
            return Math.Min(PageCount - 1, (int)Math.Round(offset / PageWidth));
        }

        /// <summary>가장 가까운 멈춤 자리.</summary>
        public float Snap(float offset)
        {
            float max = MaxOffset;
            if (Stride <= 0 || offset >= max) return max;
            if (offset <= 0) return 0;
            float card = (float)Math.Round(offset / Stride) * Stride;
            return Math.Abs(max - offset) < Math.Abs(card - offset) ? max : Math.Min(card, max);
        }

        /// <summary>화살표 한 번 — 한 화면(보이는 카드 수)만큼 넘긴다.</summary>
        public float Step(float offset, int direction)
        {
            float target = Snap(offset) + direction * PageWidth;
            return Clamp(Snap(Clamp(target)));
        }

        /// <summary>index번 카드가 다 보이게 하는 가장 가까운 위치. 이미 보이면 그대로.</summary>
        public float Reveal(int index, float offset)
        {
            if (index < 0 || index >= Count) return offset;
            float left = index * Stride;
            float right = (index - PerPage + 1) * Stride;
            if (offset > left) return Clamp(left);
            if (offset < right) return Clamp(right);
            return offset;
        }

        private float Clamp(float offset) => Math.Max(0f, Math.Min(MaxOffset, offset));
    }
}
