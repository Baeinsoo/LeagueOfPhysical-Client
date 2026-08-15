namespace FlappyRace
{
    /// <summary>추격자가 설 자리를 정한다.</summary>
    public static class FlappyChaserPosition
    {
        /// <summary>
        /// 가속선과 "선두 한 화면 뒤" 중 더 앞선 쪽. 앞의 것이 선두에게도 압박을 주고,
        /// 뒤의 것이 격차가 화면을 넘는 순간 뒤를 잘라 전원을 한 화면에 묶는다.
        /// </summary>
        public static float Resolve(float curveX, float leaderX, float screenWidth)
        {
            float trailing = leaderX - screenWidth;
            return curveX > trailing ? curveX : trailing;
        }
    }
}
