using System;

namespace LOP.MapTools
{
    /// <summary>
    /// 장애물이 판정면보다 <b>뒤로</b> 뻗어 있을 때 화면에서 틈이 얼마나 좁아 보이는지.
    ///
    /// <para>원근 카메라에서 멀리 있는 면은 소실점 쪽으로 당겨져 보인다. 장애물이 화면 위쪽에
    /// 있으면 그 당겨짐이 아래로 — 즉 틈 안으로 — 향하므로, 플레이어는 실제보다 좁은 틈을 본다.
    /// 통과 여유가 3~5cm인 맵에서는 이 오차가 여유보다 크다.</para>
    ///
    /// <para>상태 없는 순수 계산이라 <c>*System</c>이 아니라 static 커널이다
    /// (<see cref="StaticPinch"/>·<see cref="WindmillPhase"/>와 같은 짝).</para>
    /// </summary>
    public static class VisualHonesty
    {
        /// <summary>화면이 담는 세로 반높이(판정면 기준). 카메라가 보는 범위를 잰다.</summary>
        public static float ScreenHalfHeight(float cameraDistance, float verticalFovDegrees)
        {
            double halfAngle = verticalFovDegrees * 0.5 * Math.PI / 180.0;
            return (float)(cameraDistance * Math.Tan(halfAngle));
        }

        /// <summary>
        /// 뒷면이 화면에서 나타나는 높이. 소실점 쪽으로 <c>C/(C+d)</c>만큼 당겨진다.
        /// </summary>
        /// <param name="trueHeight">판정면에서의 진짜 높이(화면 중앙 기준).</param>
        /// <param name="backDepth">판정면보다 뒤로 뻗은 두께. 0이면 왜곡이 없다.</param>
        public static float ApparentHeight(float trueHeight, float cameraDistance, float backDepth)
        {
            return trueHeight * cameraDistance / (cameraDistance + backDepth);
        }

        /// <summary>
        /// 틈 안으로 파고들어 보이는 양. 이만큼 틈이 좁아 보인다.
        /// </summary>
        public static float Intrusion(float trueHeight, float cameraDistance, float backDepth)
        {
            return trueHeight * backDepth / (cameraDistance + backDepth);
        }
    }
}
