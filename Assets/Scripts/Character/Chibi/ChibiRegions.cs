namespace LOP
{
    public enum ChibiRegion { Skin, Top, Sleeve, Bottom, Shoe, Hair }

    /// <summary>
    /// PolyOne 치비 몸은 UV가 팔레트 한 칸이라 텍스처로 옷을 못 나눈다 — 정점이 가장 크게 따르는 뼈로 영역을 정한다.
    /// 영역은 정점 색 빨강 채널에 0~255 여섯 단계로 담기고, `LOP/Toon`이 영역마다 색을 고른다.
    /// </summary>
    public static class ChibiRegions
    {
        public static ChibiRegion RegionOf(string boneName)
        {
            //  순서가 중요하다 — "ForeArm"은 "Arm"보다, "UpLeg"·"Foot"은 "Leg"보다 먼저 본다.
            if (boneName.Contains("Foot")) { return ChibiRegion.Shoe; }
            if (boneName.Contains("Hand")) { return ChibiRegion.Skin; }
            if (boneName.Contains("ForeArm")) { return ChibiRegion.Sleeve; }
            if (boneName.Contains("Arm") || boneName.Contains("Shoulder") || boneName.Contains("Spine")) { return ChibiRegion.Top; }
            if (boneName.Contains("Leg") || boneName.Contains("Hips")) { return ChibiRegion.Bottom; }
            return ChibiRegion.Skin;
        }

        /// <summary>머리 정점(모델 단위, 키 0.49m 기준)의 영역 — 정수리 쪽과 뒤통수는 머리카락, 얼굴 쪽은 피부.</summary>
        public static ChibiRegion HeadRegion(float y, float z)
            => y > 0.40f || (y > 0.26f && z < -0.02f) ? ChibiRegion.Hair : ChibiRegion.Skin;

        public static byte Encode(ChibiRegion region) => (byte)((int)region * 255 / 5);

        public static ChibiRegion Decode(byte value) => (ChibiRegion)System.Math.Round(value * 5 / 255.0);
    }
}
