namespace LOP
{
    /// <summary>
    /// Luban <c>TbSkydiveConfig</c>(전역 단일 행, id=1)을 LOP-Shared <see cref="SkydiveConfig"/>로 옮기는
    /// 사이드 로컬 어댑터. (Shared는 MasterData 패키지 비참조 → 여기서 변환. <see cref="FlappyConfigProvider"/> 대칭.)
    /// </summary>
    public class SkydiveConfigProvider
    {
        private readonly LOP.MasterData.LOPMasterData md;

        public SkydiveConfigProvider(LOP.MasterData.LOPMasterData md)
        {
            this.md = md;
        }

        public SkydiveConfig Get()
        {
            var r = md.Tables.TbSkydiveConfig.GetOrDefault(1);
            if (r == null)
            {
                throw new System.InvalidOperationException(
                    "TbSkydiveConfig id=1 행을 찾을 수 없음 — MasterData 미로드 또는 SkydiveConfig 데이터 누락");
            }
            return new SkydiveConfig(
                r.SpreadFallSpeed, r.DiveFallSpeed, r.GlideFallSpeed,
                r.SpreadMoveSpeed, r.DiveMoveSpeed, r.GlideMoveSpeed,
                r.SpreadTurnAccel, r.DiveTurnAccel, r.GlideTurnAccel,
                r.FallApproach, r.PostureRate,
                r.BodyRadius, r.BodyHeight, r.GroundY,
                r.StaminaMax, r.GlideDrain, r.GroundRecover, r.EmergencyGlideTime,
                r.GroundMoveSpeed, r.GroundAccel, r.JumpPower, r.PoseClearance, r.FallBrake,
                r.GlideWindLag, r.SpreadWindLag, r.DiveWindLag);
        }
    }
}
