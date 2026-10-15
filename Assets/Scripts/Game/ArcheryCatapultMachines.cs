using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 사대 너머 양옆 투석기 — 표적이 어디서 날아오는지 읽히게 한다(spec §4 슬라이스 2). 그림일 뿐이라 판정과 무관하고,
    /// 시각은 무리의 준비·발사 틱(<see cref="ArcheryCatapultSchedule"/>)에 맞춘다.
    /// </summary>
    public static class ArcheryCatapultMachines
    {
        public const float SideOffsetM = 22f;   // 표적 길(±16 m)과 관중석 밖
        public const float ForwardM = 44f;      // 거리대 가운데쯤
        public const float ArmRestDeg = 0f;
        public const float ArmCockedDeg = -20f;
        public const float ArmFiredDeg = 110f;
        public const float LobApexM = 7f;
        private const int SwingTicks = 8;       // 0.16초에 휘두른다
        private const int SettleTicks = 40;     // 0.8초에 제자리로

        public static Vector3 Position(int side, Vector3 laneOrigin, Vector3 forward, Vector3 right)
            => laneOrigin + right * (side * SideOffsetM) + forward * ForwardM;

        public static int SideOf(float lateral) => lateral < 0f ? -1 : +1;

        public static float ArmAngle(long tick, long cueTick, long launchTick)
        {
            if (tick < cueTick)
            {
                return ArmRestDeg;
            }
            if (tick < launchTick)
            {
                float k = (tick - cueTick) / (float)Mathf.Max(1, launchTick - cueTick);
                return Mathf.Lerp(ArmRestDeg, ArmCockedDeg, k);
            }
            long since = tick - launchTick;
            if (since < SwingTicks)
            {
                return Mathf.Lerp(ArmCockedDeg, ArmFiredDeg, since / (float)SwingTicks);
            }
            since -= SwingTicks;
            return since < SettleTicks ? Mathf.Lerp(ArmFiredDeg, ArmRestDeg, since / (float)SettleTicks) : ArmRestDeg;
        }

        /// <summary>바구니에서 출발점까지(s = 0~1). 두 점을 잇는 직선 위에 4·꼭대기·s(1−s)만큼 띄운다.</summary>
        public static Vector3 Lob(Vector3 bucket, Vector3 origin, float s)
        {
            s = Mathf.Clamp01(s);
            return Vector3.Lerp(bucket, origin, s) + Vector3.up * (4f * LobApexM * s * (1f - s));
        }
    }
}
