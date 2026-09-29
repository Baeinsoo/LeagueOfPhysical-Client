using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 낙하 속도선 — 화면 가장자리에서 가운데 쪽으로 향하는 흰 선. 가운데(시야)는 비운다. 빠를수록 진하고 바깥으로 흐른다.
    /// 그림 에셋 없이 코드로 그린다(고정 시드라 모양은 매번 같다).
    /// </summary>
    public sealed class SpeedStreaksElement : VisualElement
    {
        private const int Count = 36;
        private const float ClearCenter = 0.35f;   // 화면 대각선 반의 35% 안은 비운다

        private readonly float[] angle = new float[Count];
        private readonly float[] length = new float[Count];
        private readonly float[] width = new float[Count];
        private readonly float[] phase = new float[Count];
        private float strength;
        private float flow;

        /// <summary>0~1. 0이면 아무것도 안 그린다.</summary>
        public float Strength
        {
            get => strength;
            set
            {
                float v = Mathf.Clamp01(value);
                if (Mathf.Approximately(v, strength) && v == 0f)
                {
                    return;
                }
                strength = v;
                flow = Time.time;
                MarkDirtyRepaint();
            }
        }

        public SpeedStreaksElement()
        {
            pickingMode = PickingMode.Ignore;
            var rng = new System.Random(90);
            for (int i = 0; i < Count; i++)
            {
                angle[i] = (float)(rng.NextDouble() * Mathf.PI * 2);
                length[i] = 0.12f + (float)rng.NextDouble() * 0.18f;
                width[i] = 1.5f + (float)rng.NextDouble() * 3f;
                phase[i] = (float)rng.NextDouble();
            }
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext ctx)
        {
            if (strength <= 0f)
            {
                return;
            }
            var r = contentRect;
            if (r.width <= 0f || r.height <= 0f)
            {
                return;
            }
            var center = r.center;
            float half = new Vector2(r.width, r.height).magnitude * 0.5f;
            var p = ctx.painter2D;
            p.lineCap = LineCap.Round;
            for (int i = 0; i < Count; i++)
            {
                //  선마다 자기 위상으로 안쪽 끝이 바깥으로 흐른다(빠를수록 빨리).
                float t = Mathf.Repeat(phase[i] + flow * (0.6f + strength * 1.4f), 1f);
                float inner = Mathf.Lerp(ClearCenter, 1f, t) * half;
                float outer = inner + length[i] * half * (0.5f + strength);
                var dir = new Vector2(Mathf.Cos(angle[i]), Mathf.Sin(angle[i]));
                p.strokeColor = new Color(1f, 1f, 1f, 0.55f * strength * (1f - t * 0.5f));
                p.lineWidth = width[i] * (0.6f + strength * 0.8f);
                p.BeginPath();
                p.MoveTo(center + dir * inner);
                p.LineTo(center + dir * outer);
                p.Stroke();
            }
        }
    }
}
