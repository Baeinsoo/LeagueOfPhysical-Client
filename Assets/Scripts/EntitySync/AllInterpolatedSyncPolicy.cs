namespace LOP
{
    /// <summary>
    /// 아무것도 예측하지 않는다(판치기). 동전은 서버가 PhysX로 굴리고 클라는 스냅을 보간해 볼 뿐이라
    /// 클라가 굴릴 규칙이 없고, 플레이어는 아바타가 없어 움직이지 않는다.
    /// </summary>
    public class AllInterpolatedSyncPolicy : IEntitySyncPolicy
    {
        public EntitySyncMode For(GameFramework.World.Entity entity)
        {
            return EntitySyncMode.Interpolated;
        }
    }
}
