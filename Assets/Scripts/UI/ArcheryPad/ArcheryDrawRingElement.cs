using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 조준점 둘레의 가는 당김 고리. 위(12시)에서 시작해 시계 방향으로 <see cref="Fill"/>만큼 찬다.
    /// 무엇을 어떤 색으로 보일지는 <see cref="ArcheryDrawRing"/>이 정하고, 여기는 칠하기만 한다.
    ///
    /// <para>요소 크기가 고리의 지름이다(USS가 정한다). 가운데는 비워 둔다 — 조준점과 과녁을 가리면 안 된다.</para>
    /// </summary>
    [UxmlElement]
    public partial class ArcheryDrawRingElement : VisualElement
    {
        /// <summary>선 굵기(패널 px). 1080p 기준 2.5px — 조준점 테두리(2px)와 비슷하게 가늘게.</summary>
        public const float LineWidth = 2.5f;

        //  번쩍일 때 선이 이만큼 더 굵어진다(배). 바깥에 같은 색의 옅은 띠도 함께 퍼진다.
        private const float FlashWidthBoost = 1.2f;
        private const float FlashHaloWidth = 6f;

        private float _fill;
        private Color _color = Color.clear;
        private float _flash;

        /// <summary>채운 비율(0~1).</summary>
        public float Fill => _fill;

        public ArcheryDrawRingElement()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        /// <summary>
        /// 그릴 값을 넣는다. 셋 다 그대로면 다시 그리지 않는다 — 매 프레임 불려도 메시를 헛되이 안 만든다.
        /// <paramref name="flash"/>는 만작 번쩍임의 세기(0~1).
        /// </summary>
        public void Set(float fill, Color color, float flash)
        {
            fill = Mathf.Clamp01(fill);
            flash = Mathf.Clamp01(flash);
            if (Mathf.Approximately(fill, _fill) && color == _color && Mathf.Approximately(flash, _flash))
            {
                return;
            }
            _fill = fill;
            _color = color;
            _flash = flash;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext ctx)
        {
            var size = contentRect.size;
            if (_fill <= 0f || !(size.x > 0f) || !(size.y > 0f) || _color.a <= 0f)
            {
                return;
            }

            //  선이 요소 밖으로 삐져나가지 않게 반지름에서 선 굵기 절반을 뺀다.
            float width = LineWidth * (1f + FlashWidthBoost * _flash);
            float radius = Mathf.Min(size.x, size.y) * 0.5f - LineWidth * 0.5f;
            Vector2 center = size * 0.5f;

            //  패널은 아래가 +y라 각도가 시계 방향으로 는다. -90°가 12시다.
            const float top = -90f;
            float end = top + 360f * _fill;
            var painter = ctx.painter2D;

            if (_flash > 0f)
            {
                //  번쩍임: 고리 바깥으로 같은 색의 옅은 띠가 퍼졌다 사라진다.
                var halo = _color;
                halo.a *= 0.45f * _flash;
                painter.strokeColor = halo;
                painter.lineWidth = FlashHaloWidth * _flash + width;
                painter.lineCap = LineCap.Butt;
                painter.BeginPath();
                painter.Arc(center, radius + FlashHaloWidth * 0.5f * _flash, top, end, ArcDirection.Clockwise);
                painter.Stroke();
            }

            painter.strokeColor = _color;
            painter.lineWidth = width;
            painter.lineCap = LineCap.Butt;
            painter.BeginPath();
            painter.Arc(center, radius, top, end, ArcDirection.Clockwise);
            painter.Stroke();
        }
    }
}
