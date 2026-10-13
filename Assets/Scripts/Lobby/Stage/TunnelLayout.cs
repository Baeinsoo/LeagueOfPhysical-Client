using System.Collections.Generic;

namespace LOP
{
    /// <summary>로비 터널 수치(m). 카메라는 z=0 근처에서 +z(끝 빛)를 본다.</summary>
    public sealed class TunnelLayout
    {
        public float Width = 6f;
        public float Height = 4.2f;
        public float Length = 24f;
        public float PillarSpacing = 3f;
        public float PillarSize = 0.5f;
        public float FloorStripeWidth = 0.18f;
        public float BeamDrop = 0.3f;
        public float BeamThickness = 0.35f;

        /// <summary>기둥 쌍의 z — 0부터 간격마다, 끝 빛(Length) 앞까지.</summary>
        public IReadOnlyList<float> PillarZs()
        {
            var zs = new List<float>();
            for (int i = 0; i * PillarSpacing < Length - 1e-4f; i++) zs.Add(i * PillarSpacing);
            return zs;
        }

        public float BeamY => Height - BeamDrop;
        public float EndGlowZ => Length;
    }
}
