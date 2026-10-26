namespace LOP
{
    /// <summary>
    /// 넉백 생존전의 서버 보정 — 맞아서 날아가는지와 피해 %는 서버가 정한다.
    ///
    /// 내 몸은 클라가 굴리므로 위치는 위치 판정이 맞춘다. 하지만 "맞았나"는 서버만 아는 사건이라
    /// 위치가 아직 비슷해도 날림 여부·피해 %가 갈렸으면 되돌려야 한다.
    /// </summary>
    public class KnockbackServerCorrectionHandler : IServerCorrectionHandler
    {
        private readonly KnockbackWorld world;

        public KnockbackServerCorrectionHandler(KnockbackWorld world)
        {
            this.world = world;
        }

        //  같은 시점끼리 비교한다 — 앵커 틱에 내가 예측했던 값 vs 서버가 그 틱에 가진 값.
        //  날림은 켜짐/꺼짐만 본다(끝 틱이 1틱 달라도 다음 틱 시뮬이 좁힌다 — Flappy 스턴과 같은 관례).
        public bool Matches(long tick, EntitySnap snap)
        {
            if (!world.TryGetSaved(tick, snap.entityId, out var predicted))
            {
                return true;
            }
            return (predicted.KnockbackEndTick > tick) == (snap.knockbackEndTick > tick)
                && predicted.DamagePercent == snap.damagePercent;
        }

        public void ApplyAuthoritative(GameFramework.World.Entity entity, EntitySnap snap, float deltaTime)
        {
            var body = entity.Get<KnockbackBody>();
            if (body == null)
            {
                return;
            }
            body.KnockbackEndTick = snap.knockbackEndTick;
            body.DamagePercent = snap.damagePercent;
        }
    }
}
