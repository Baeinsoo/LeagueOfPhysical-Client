namespace LOP
{
    /// <summary>FlapWang(캐릭터 게임)의 서버 보정 — 상태이상이 서버 권위다.</summary>
    public class LOPServerCorrectionHandler : IServerCorrectionHandler
    {
        private readonly LOPWorld world;   // 같은 게임 안이므로 구체를 직접 본다
        private readonly StatusEffectSystem statusEffectSystem;
        private readonly StatusEffectDataProvider statusEffectDataProvider;

        public LOPServerCorrectionHandler(
            LOPWorld world,
            StatusEffectSystem statusEffectSystem,
            StatusEffectDataProvider statusEffectDataProvider)
        {
            this.world = world;
            this.statusEffectSystem = statusEffectSystem;
            this.statusEffectDataProvider = statusEffectDataProvider;
        }

        // 위치가 가까워도 서버 상태이상 목록이 다르면 게이트를 연다: 남이 나에게 건 효과(슬로우 등)는
        // 내가 예측할 수 없어서, 가만히 서 있다 슬로우가 걸려도 위치 오차는 0으로 남기 때문이다.
        // 비교는 반드시 같은 시점끼리 해야 한다 — 앵커 틱에 "내가 그때 예측했던" 목록 vs 서버가 앵커
        // 틱에 갖고 있던 목록. (지금 살아있는 목록과 비교하면 클라가 서버보다 앞서 달리는 리드 구간
        // 내내 시점이 어긋나 보여, 효과가 걸리거나 끝날 때마다 매 스냅에서 불필요한 롤백이 발생한다.)
        // id 집합뿐 아니라 만료틱도 봐야 한다 — 몬스터가 쿨다운 없이 계속 때리면 서버가 슬로우를
        // 계속 재적용해 만료틱만 밀리는데, id 집합은 그대로라 id만 비교하면 이 발산을 놓친다.
        public bool Matches(long tick, EntitySnap snap)
        {
            // 앵커 틱 기록이 없으면(정상 경로엔 없는 엣지) 비교 불가 — 불일치로 단정하지 않고 위치 판정에 맡긴다.
            if (!world.TryGetSavedStatusEffects(tick, snap.entityId, out var predicted))
            {
                return true;
            }
            return !StatusEffectReconcileGate.ShouldReconcile(
                predicted, snap.statusEffects, statusEffectDataProvider.Get);
        }

        public void ApplyAuthoritative(GameFramework.World.Entity entity, EntitySnap snap)
        {
            statusEffectSystem.ApplyAuthoritativeState(entity, snap.statusEffects, statusEffectDataProvider.Get);
        }
    }
}
