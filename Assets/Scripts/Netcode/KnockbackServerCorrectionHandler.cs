namespace LOP
{
    /// <summary>
    /// 넉백 생존전의 서버 보정 — 맞아서 날아가는지와 피해 %, 총 상태는 서버가 정한다.
    ///
    /// 내 몸은 클라가 굴리므로 위치는 위치 판정이 맞춘다. 하지만 "맞았나"는 서버만 아는 사건이라
    /// 위치가 아직 비슷해도 날림 여부·피해 %가 갈렸으면 되돌려야 한다.
    /// 총(남은 탄·충전·연사 대기)도 비교한다 — 발사 입력이 서버에 못 닿으면 나만 쏜 것이 되어 탄 수가 갈리고,
    /// 그대로 두면 탄이 가득 찰 때까지 "나는 못 쏘는데 서버는 쏘는" 어긋남이 이어진다.
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
                && predicted.DamagePercent == snap.damagePercent
                && predicted.Ammo == snap.knockbackAmmo
                && predicted.NextReloadTick == snap.knockbackNextReloadTick
                && predicted.CooldownEndTick == snap.knockbackCooldownEndTick;
        }

        public void ApplyAuthoritative(GameFramework.World.Entity entity, EntitySnap snap, float deltaTime)
        {
            var body = entity.Get<KnockbackBody>();
            if (body != null)
            {
                body.KnockbackEndTick = snap.knockbackEndTick;
                body.DamagePercent = snap.damagePercent;
            }
            var gun = entity.Get<KnockbackGun>();
            if (gun != null)
            {
                gun.Ammo = snap.knockbackAmmo;
                gun.NextReloadTick = snap.knockbackNextReloadTick;
                gun.CooldownEndTick = snap.knockbackCooldownEndTick;
            }
        }
    }
}
