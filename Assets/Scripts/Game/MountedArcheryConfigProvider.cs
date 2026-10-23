namespace LOP
{
    /// <summary>TbMountedArcheryConfig id=1 → 공용 MountedArcheryConfig. 클·서가 같은 행을 읽어야 같은 말·화살로 움직인다.</summary>
    public class MountedArcheryConfigProvider
    {
        private readonly LOP.MasterData.LOPMasterData md;

        public MountedArcheryConfigProvider(LOP.MasterData.LOPMasterData md)
        {
            this.md = md;
        }

        public MountedArcheryConfig Get()
        {
            var r = md.Tables.TbMountedArcheryConfig.GetOrDefault(1);
            if (r == null)
            {
                throw new System.InvalidOperationException(
                    "TbMountedArcheryConfig id 1 이 없다 — TableFiles에 tbmountedarcheryconfig가 있나");
            }
            return new MountedArcheryConfig(
                matchDurationSeconds: r.MatchDurationSeconds,
                arrowMinSpeed: r.ArrowMinSpeed,
                arrowMaxSpeed: r.ArrowMaxSpeed,
                riderEyeHeight: r.RiderEyeHeight,
                mount: new MountSettings(r.MountMaxSpeed, r.MountAcceleration, r.MountDeceleration,
                                         r.TurnRateStoppedDeg, r.TurnRateAtMaxDeg, r.SharpTurnDeg,
                                         r.SharpTurnSpeedFactor, r.DrawSpeedFactor),
                maxHp: r.MaxHp,
                arrowDamage: r.ArrowDamage,
                lagCompMaxSeconds: r.LagCompMaxSeconds);
        }
    }
}
