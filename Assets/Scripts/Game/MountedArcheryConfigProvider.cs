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
                                         r.SharpTurnSpeedFactor, r.DrawSpeedFactor, r.MountReverseSpeed),
                maxHp: r.MaxHp,
                arrowDamage: r.ArrowDamage,
                lagCompMaxSeconds: r.LagCompMaxSeconds,
                zoneInitialRadius: r.ZoneInitialRadius,
                zoneFirstRevealSeconds: r.ZoneFirstRevealSeconds,
                zoneStages: ZoneStages(),
                aimAssistConeDeg: r.AimAssistConeDeg,
                aimAssistRange: r.AimAssistRange,
                aimAssistFriction: r.AimAssistFriction,
                aimAssistTrack: r.AimAssistTrack,
                zoneOvertimeDpsGrowth: r.ZoneOvertimeDpsGrowth,
                botAimErrorDeg: r.BotAimErrorDeg,
                botReactionMinSeconds: r.BotReactionMinSeconds,
                botReactionMaxSeconds: r.BotReactionMaxSeconds,
                botEngageRange: r.BotEngageRange);
        }

        //  엑셀 줄이 옮겨져도 1단계부터 순서대로 나가도록 id로 정렬한다.
        private System.Collections.Generic.List<MountedArcheryZoneStageConfig> ZoneStages()
        {
            var stages = new System.Collections.Generic.List<MountedArcheryZoneStageConfig>();
            foreach (var row in System.Linq.Enumerable.OrderBy(md.Tables.TbMountedArcheryZone.DataList, x => x.Id))
            {
                stages.Add(new MountedArcheryZoneStageConfig(
                    row.RestSeconds, row.ShrinkSeconds, row.EndRadius, row.DamagePerSecond));
            }
            return stages;
        }
    }
}
