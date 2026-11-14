using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 카드 러시의 둥근 카드 버튼(4.2차) — 오른쪽 아래에 크게, 트페 스킬 버튼처럼. 받은 카드 모습(<see cref="CardRushHeadCardFace"/>)을
    /// 그리기만 한다: 평소 뒷면(숨쉬기), 도는 색(가는 6초 링), 잡은 색(굵은 5초 링·흰 테두리), 흐림(기절·쿨다운), 사라짐(작아짐).
    /// 상태도 입력도 모른다 — 탭은 <see cref="CardRushTapGrab"/>가 이 요소 위인지 물어 받는다.
    /// </summary>
    public sealed class CardRushCardButton : VisualElement
    {
        /// <summary>원 반지름 = 요소 반 변 × 이 몫. 바깥에 링·숨쉬기 자리를 남긴다.</summary>
        public const float BodyOfHalf = 0.8f;
        public const float RingOfHalf = 0.92f;
        public const float RingWidthOfHalf = 0.09f;
        public const float ThinRingWidthOfHalf = 0.045f;
        public const float RimOfHalf = 0.06f;
        public const float DimAlpha = 0.4f;

        private static readonly Color RingColor = Color.white;
        private static readonly Color HeldRim = new Color(1f, 0.97f, 0.85f);
        private static readonly Color Decal = Color.white;

        private CardRushHeadCardFace face;
        private bool hasFace;

        public CardRushCardButton()
        {
            pickingMode = PickingMode.Position;
            generateVisualContent += Draw;
        }

        public CardRushHeadCardFace Face => face;

        /// <summary>모습이 바뀌었을 때만 다시 그린다(매 프레임 불려도 된다).</summary>
        public void SetFace(in CardRushHeadCardFace next)
        {
            if (hasFace && Same(face, next))
            {
                return;
            }
            face = next;
            hasFace = true;
            style.visibility = next.Visible ? Visibility.Visible : Visibility.Hidden;
            MarkDirtyRepaint();
        }

        /// <summary>원 크기 배율 — 사라지는 중 1 → 0, 평소 숨쉬기 커진 반 1.08.</summary>
        public static float ScaleOf(in CardRushHeadCardFace f)
            => f.Vanish01 > 0f ? Mathf.Clamp01(1f - f.Vanish01) : f.Ready && f.ReadyBig ? CardRushHeadCardView.ReadyScale : 1f;

        /// <summary>바탕 색 — 앞면은 카드 색, 뒷면은 회색. 흐리면 반투명.</summary>
        public static Color FillOf(in CardRushHeadCardFace f)
        {
            var c = f.FaceUp ? CardRushCardPalette.ColorOf(f.Color) : CardRushCardMaterials.Back;
            c.a = f.Dim ? DimAlpha : 1f;
            return c;
        }

        /// <summary>링 굵기(반 변 몫) — 잡음 굵게, 돎 가늘게, 그 밖이나 깜빡 꺼짐이면 0.</summary>
        public static float RingWidthOf(in CardRushHeadCardFace f)
            => CardRushHeadCardView.RingShown(f) && f.RingT01 > 0f ? (f.Rolling ? ThinRingWidthOfHalf : RingWidthOfHalf) : 0f;

        private static bool Same(in CardRushHeadCardFace a, in CardRushHeadCardFace b)
            => a.Visible == b.Visible && a.FaceUp == b.FaceUp && a.Color == b.Color && a.Dim == b.Dim && a.Held == b.Held
               && a.Rolling == b.Rolling && a.Ready == b.Ready && a.ReadyBig == b.ReadyBig && a.RingBlink == b.RingBlink
               && Mathf.Approximately(a.RingT01, b.RingT01) && Mathf.Approximately(a.Vanish01, b.Vanish01);

        private void Draw(MeshGenerationContext ctx)
        {
            if (hasFace == false || face.Visible == false)
            {
                return;
            }
            var rect = contentRect;
            float half = Mathf.Min(rect.width, rect.height) * 0.5f;
            if (half <= 0f)
            {
                return;
            }
            var center = rect.center;
            float scale = ScaleOf(face);
            float r = half * BodyOfHalf * scale;
            var p = ctx.painter2D;
            float alpha = face.Dim ? DimAlpha : 1f;

            p.fillColor = FillOf(face);
            p.BeginPath();
            p.Arc(center, r, 0f, 360f);
            p.Fill();

            p.strokeColor = WithAlpha(face.Held ? HeldRim : CardRushCardMaterials.Gold, alpha);
            p.lineWidth = half * RimOfHalf * scale;
            p.BeginPath();
            p.Arc(center, r, 0f, 360f);
            p.Stroke();

            p.fillColor = WithAlpha(face.FaceUp ? Decal : CardRushCardMaterials.BackMark, alpha);
            if (face.FaceUp)
            {
                Shape(p, face.Color, center, r * 0.42f);
            }
            else
            {
                Diamond(p, center, r * 0.32f, r * 0.45f);
            }

            float ringWidth = RingWidthOf(face);
            if (ringWidth > 0f)
            {
                //  12시에서 시계 방향으로 남은 몫만큼.
                p.strokeColor = RingColor;
                p.lineWidth = half * ringWidth;
                p.lineCap = LineCap.Round;
                p.BeginPath();
                p.Arc(center, half * RingOfHalf, -90f, -90f + 360f * Mathf.Clamp01(face.RingT01));
                p.Stroke();
            }
        }

        //  앞면 모양 — 머리 위 카드와 같다(빨 ●, 노 ▲, 파 ■).
        private static void Shape(Painter2D p, CardColor color, Vector2 c, float s)
        {
            p.BeginPath();
            switch (color)
            {
                case CardColor.Red:
                    p.Arc(c, s, 0f, 360f);
                    break;
                case CardColor.Yellow:
                    p.MoveTo(c + new Vector2(0f, -s));
                    p.LineTo(c + new Vector2(s * 0.95f, s * 0.75f));
                    p.LineTo(c + new Vector2(-s * 0.95f, s * 0.75f));
                    p.ClosePath();
                    break;
                default:
                    float h = s * 0.8f;
                    p.MoveTo(c + new Vector2(-h, -h));
                    p.LineTo(c + new Vector2(h, -h));
                    p.LineTo(c + new Vector2(h, h));
                    p.LineTo(c + new Vector2(-h, h));
                    p.ClosePath();
                    break;
            }
            p.Fill();
        }

        private static void Diamond(Painter2D p, Vector2 c, float w, float h)
        {
            p.BeginPath();
            p.MoveTo(c + new Vector2(0f, -h));
            p.LineTo(c + new Vector2(w, 0f));
            p.LineTo(c + new Vector2(0f, h));
            p.LineTo(c + new Vector2(-w, 0f));
            p.ClosePath();
            p.Fill();
        }

        private static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}
