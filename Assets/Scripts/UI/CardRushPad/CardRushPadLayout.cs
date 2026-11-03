using UnityEngine;

namespace LOP.UI
{
    /// <summary>
    /// 카드 조작판 배치(패널 단위, 아래쪽 원점). 카드·칩·연속 수를 화면 아래 한 띠에 모은다 — 그 띠 위는 내 몸 자리다.
    ///
    /// <para>예전 배치(가운데 아래, 폭 28%·높이 300 + 위에 칩)는 띠가 화면 아래 절반 가까이(가로 0.48)를 덮어 내 몸과
    /// 풍덩·박치기가 카드 뒤에 숨었다. 이제 띠 높이는 화면의 약 4분의 1이고, 칩은 카드 오른쪽(지금 → 다음 순서로
    /// 시간이 흐르는 방향), 연속 수는 왼쪽에 둔다. <see cref="CardRushCamera"/>는 몸의 발이 이 띠 위에 오게 선다.</para>
    ///
    /// <para>패널은 1920×1080 Expand 축척이라 세로 화면에선 패널이 길쭉해진다(1920×3413). 그래서 크기는 화면 높이와
    /// 폭을 함께 보고 정한다 — 높이만 보면 세로 화면에서 카드가 화면 폭을 넘고, 고정 px면 세로에서 깨알만 해진다.</para>
    /// </summary>
    public readonly struct CardRushPadLayout
    {
        public const float MarginOfHeight = 0.025f;
        public const float CardOfHeight = 0.21f;
        public const float CardMaxOfWidth = 0.30f;   //  세로 화면에서 카드 높이 상한(폭 기준)
        public const float CardAspect = 1.45f;
        public const float CardMaxWidthOfWidth = 0.44f;
        public const float ChipOfCard = 0.36f;
        public const float NextChipOfNow = 0.75f;
        public const float GapOfNow = 0.15f;

        public readonly Rect Card;          //  x = 왼쪽, y = 아래(bottom)
        public readonly Rect Chips;         //  칩 줄 전체(지금 + 다음 둘)
        public readonly float NowChip;      //  지금 칩 한 변
        public readonly float NextChip;     //  다음 칩 한 변
        public readonly float ChipGap;
        public readonly Rect Streak;        //  연속 수 글 상자(카드 왼쪽, 오른쪽 정렬)
        public readonly float GlyphFontSize;
        public readonly float StreakFontSize;

        /// <summary>띠 맨 위(화면 높이에 대한 비율, 아래가 0). 이보다 위가 몸 자리다.</summary>
        public readonly float BandTop01;

        private CardRushPadLayout(Rect card, Rect chips, float now, float next, float gap, Rect streak,
                                  float glyph, float streakFont, float bandTop01)
        {
            Card = card;
            Chips = chips;
            NowChip = now;
            NextChip = next;
            ChipGap = gap;
            Streak = streak;
            GlyphFontSize = glyph;
            StreakFontSize = streakFont;
            BandTop01 = bandTop01;
        }

        public static CardRushPadLayout Compute(float panelWidth, float panelHeight)
        {
            float w = Mathf.Max(1f, panelWidth);
            float h = Mathf.Max(1f, panelHeight);

            float margin = h * MarginOfHeight;
            float cardH = Mathf.Min(h * CardOfHeight, w * CardMaxOfWidth);
            float cardW = Mathf.Min(cardH * CardAspect, w * CardMaxWidthOfWidth);
            var card = new Rect((w - cardW) * 0.5f, margin, cardW, cardH);

            //  칩 줄: [간격] 지금 [간격] 다음 [간격] 다음 — 카드 오른쪽 남은 폭 안에 들어가게 줄인다.
            float side = (w - cardW) * 0.5f;
            float rowUnits = 1f + 2f * NextChipOfNow + 3f * GapOfNow;
            float now = Mathf.Min(cardH * ChipOfCard, side * 0.9f / rowUnits);
            float next = now * NextChipOfNow;
            float gap = now * GapOfNow;
            float centerY = margin + cardH * 0.5f;
            var chips = new Rect(card.xMax + gap, centerY - now * 0.5f, now + 2f * next + 2f * gap, now);

            //  연속 수: 카드 왼쪽 남은 폭, 카드 가운데 높이.
            float streakFont = now * 0.45f;
            var streak = new Rect(gap, centerY - streakFont, side - 2f * gap, streakFont * 2f);

            return new CardRushPadLayout(card, chips, now, next, gap, streak,
                                         glyph: cardH * 0.6f, streakFont: streakFont,
                                         bandTop01: (margin + cardH) / h);
        }
    }
}
