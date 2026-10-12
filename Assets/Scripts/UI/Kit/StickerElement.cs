using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 스티커 한 장: 남색 뒷면(이 요소가 그림) 위에 기울어진 크림 앞면(자식 Face가 그림).
    /// USS로는 기울인 모양·어긋난 그림자를 못 그려 Painter2D로 칠한다.
    /// 눌림은 USS가 Face만 뒷면 쪽으로 옮겨서 낸다(.kit-press:active .sticker__face).
    /// </summary>
    [UxmlElement]
    public partial class StickerElement : VisualElement
    {
        private static readonly CustomStyleProperty<float> TiltProp = new("--sticker-tilt");
        private static readonly CustomStyleProperty<float> TailProp = new("--sticker-tail");
        private static readonly CustomStyleProperty<float> RadiusProp = new("--sticker-radius");
        private static readonly CustomStyleProperty<float> BackXProp = new("--sticker-back-x");
        private static readonly CustomStyleProperty<float> BackYProp = new("--sticker-back-y");
        private static readonly CustomStyleProperty<Color> FaceProp = new("--sticker-face");
        private static readonly CustomStyleProperty<Color> BackProp = new("--sticker-back");

        public VisualElement Face { get; }
        public override VisualElement contentContainer => Face;

        public float Tilt = 0.025f;
        public float TailPx;
        public float Radius;
        public float BackX = 6f;
        public float BackY = 9f;
        public Color FaceColor = new Color32(0xFF, 0xF8, 0xE8, 0xFF);
        public Color BackColor = new Color32(0x2C, 0x2A, 0x6B, 0xFF);

        public StickerElement()
        {
            AddToClassList("sticker");
            pickingMode = PickingMode.Ignore;

            Face = new VisualElement { name = "sticker-face", pickingMode = PickingMode.Ignore };
            Face.AddToClassList("sticker__face");
            //  앞면은 보통 흐름으로 부모를 채운다 — 절대 위치로 두면 내용 크기가 바깥에 안 전해져
            //  폭을 정하지 않은 스티커(이름표 등)가 0으로 줄어든다.
            Face.style.flexGrow = 1;
            Face.generateVisualContent += DrawFace;
            hierarchy.Add(Face);

            generateVisualContent += DrawBack;
            RegisterCallback<CustomStyleResolvedEvent>(OnStyleResolved);
        }

        private void OnStyleResolved(CustomStyleResolvedEvent e)
        {
            var s = e.customStyle;
            if (s.TryGetValue(TiltProp, out var t)) Tilt = t;
            if (s.TryGetValue(TailProp, out var tail)) TailPx = tail;
            if (s.TryGetValue(RadiusProp, out var r)) Radius = r;
            if (s.TryGetValue(BackXProp, out var bx)) BackX = bx;
            if (s.TryGetValue(BackYProp, out var by)) BackY = by;
            if (s.TryGetValue(FaceProp, out var fc)) FaceColor = fc;
            if (s.TryGetValue(BackProp, out var bc)) BackColor = bc;
            MarkDirtyRepaint();
            Face.MarkDirtyRepaint();
        }

        private Rect Box(VisualElement e) => new Rect(Vector2.zero, e.layout.size);

        private void DrawBack(MeshGenerationContext ctx)
        {
            var face = Shape(Box(this));
            if (face.Length == 0) return;
            Fill(ctx.painter2D, StickerGeometry.Offset(face, BackX, BackY), BackColor, Box(this), BackX, BackY);
        }

        private void DrawFace(MeshGenerationContext ctx)
        {
            var face = Shape(Box(Face));
            if (face.Length == 0) return;
            Fill(ctx.painter2D, face, FaceColor, Box(Face), 0f, 0f);
        }

        //  둥근 판은 기울이지 않는다(1부 §3.3) — 모서리 둥글기가 있으면 곧은 사각형으로.
        private Vector2[] Shape(Rect r) => StickerGeometry.Face(r, Radius > 0f ? 0f : Tilt, Radius > 0f ? 0f : TailPx);

        private void Fill(Painter2D p, Vector2[] pts, Color color, Rect r, float dx, float dy)
        {
            p.fillColor = color;
            p.BeginPath();
            if (Radius > 0f)
            {
                var b = new Rect(r.x + dx, r.y + dy, r.width, r.height);
                float rad = Mathf.Min(Radius, b.width * 0.5f, b.height * 0.5f);
                p.MoveTo(new Vector2(b.xMin + rad, b.yMin));
                p.LineTo(new Vector2(b.xMax - rad, b.yMin));
                p.ArcTo(new Vector2(b.xMax, b.yMin), new Vector2(b.xMax, b.yMin + rad), rad);
                p.LineTo(new Vector2(b.xMax, b.yMax - rad));
                p.ArcTo(new Vector2(b.xMax, b.yMax), new Vector2(b.xMax - rad, b.yMax), rad);
                p.LineTo(new Vector2(b.xMin + rad, b.yMax));
                p.ArcTo(new Vector2(b.xMin, b.yMax), new Vector2(b.xMin, b.yMax - rad), rad);
                p.LineTo(new Vector2(b.xMin, b.yMin + rad));
                p.ArcTo(new Vector2(b.xMin, b.yMin), new Vector2(b.xMin + rad, b.yMin), rad);
            }
            else
            {
                p.MoveTo(pts[0]);
                for (int i = 1; i < pts.Length; i++) p.LineTo(pts[i]);
            }
            p.ClosePath();
            p.Fill();
        }
    }
}
