namespace LOP
{
    /// <summary>
    /// Luban <c>TbFlappyConfig</c>(전역 단일 행, id=1)을 LOP-Shared <see cref="FlappyConfig"/>로 옮기는
    /// 사이드 로컬 어댑터. (Shared는 MasterData 패키지 비참조 → 여기서 변환. <see cref="AbilityDataProvider"/> 대칭.)
    /// </summary>
    public class FlappyConfigProvider
    {
        private readonly LOP.MasterData.LOPMasterData md;

        public FlappyConfigProvider(LOP.MasterData.LOPMasterData md)
        {
            this.md = md;
        }

        public FlappyConfig Get()
        {
            // 없으면 Luban의 애매한 KeyNotFoundException 대신 원인을 짚어 크게 실패
            var r = md.Tables.TbFlappyConfig.GetOrDefault(1);
            if (r == null)
            {
                throw new System.InvalidOperationException(
                    "TbFlappyConfig id=1 행을 찾을 수 없음 — MasterData 미로드 또는 FlappyConfig 데이터 누락");
            }
            return new FlappyConfig(
                r.ForwardSpeed, r.FlapImpulse, r.Gravity, r.MaxFallSpeed,
                r.BodyRadius, r.BodyHeight, r.Restitution,
                stunTime: r.StunTime,
                invulnTime: r.InvulnTime,
                dashMult: r.DashMult,
                dashDuration: r.DashDuration,
                dashChargeBase: r.DashChargeBase,
                dashChargeDive: r.DashChargeDive);
        }
    }
}
