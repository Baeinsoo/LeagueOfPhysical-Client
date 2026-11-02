namespace LOP
{
    /// <summary>TbKnockbackConfig id=1 → 공용 KnockbackConfig. 클·서가 같은 행을 읽어야 바닥과 속도가 같다.</summary>
    public class KnockbackConfigProvider
    {
        private readonly LOP.MasterData.LOPMasterData md;
        private readonly IRoomDataStore roomDataStore;

        public KnockbackConfigProvider(LOP.MasterData.LOPMasterData md, IRoomDataStore roomDataStore)
        {
            this.md = md;
            this.roomDataStore = roomDataStore;
        }

        //  지금 판의 게임 모드 id로 팀 수를 찾는다. 표에 없으면 개인전.
        public KnockbackMatchMode GetMatchMode()
        {
            var rounds = roomDataStore.match?.rounds;
            if (rounds == null || rounds.Length == 0) return KnockbackMatchMode.Solo;
            var round = rounds[MatchSceneResolver.CurrentRoundIndex(rounds.Length)];
            var row = md.Tables.TbKnockbackMode.GetOrDefault(round.gameModeId);
            return row == null ? KnockbackMatchMode.Solo : new KnockbackMatchMode(row.TeamCount);
        }

        public KnockbackConfig Get()
        {
            var r = md.Tables.TbKnockbackConfig.GetOrDefault(1);
            if (r == null)
            {
                throw new System.InvalidOperationException(
                    "TbKnockbackConfig id 1 이 없다 — TableFiles에 tbknockbackconfig가 있나");
            }
            return new KnockbackConfig(r.MatchDurationSeconds, r.MoveSpeed, r.MoveGain, r.ArenaRadius,
                                       r.TileSize, r.HoleRadius, r.FallDepth, r.SpawnRadius,
                                       r.ShotSpeed, r.ShotRange, r.ShotRadius, r.HitRadius, r.AmmoMax, r.ReloadSeconds, r.FireCooldownSeconds, r.Recoil, r.DamagePerHit, r.KnockbackBase, r.KnockbackScale, r.KnockbackCarry, r.StunBase, r.StunScale, r.StunGain, r.BodyRestitution, r.ZoneFirstRevealSeconds,
                                       ZoneStages(),
                                       new KnockbackPillarLayout(r.PillarCount, r.PillarRingRadius, r.PillarRadius, r.PillarHeight),
                                       r.EdgeMode, r.HazardDamagePerSecond, r.HazardSpeedScale);
        }

        //  엑셀 줄이 옮겨져도 1단계부터 순서대로 나가도록 id로 정렬한다. 원 밖 피해는 이 모드에 없다(0).
        private System.Collections.Generic.List<MountedArcheryZoneStageConfig> ZoneStages()
        {
            var stages = new System.Collections.Generic.List<MountedArcheryZoneStageConfig>();
            foreach (var row in System.Linq.Enumerable.OrderBy(md.Tables.TbKnockbackZone.DataList, x => x.Id))
            {
                stages.Add(new MountedArcheryZoneStageConfig(row.RestSeconds, row.ShrinkSeconds, row.EndRadius, 0f));
            }
            return stages;
        }
    }
}
