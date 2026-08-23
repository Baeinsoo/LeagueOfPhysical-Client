namespace LOP
{
    /// <summary>
    /// 캐릭터는 전부 예측하고 그 외는 보간한다(Flappy Race). 몸싸움처럼 서로 부딪히는 게 게임성인
    /// 경우, 남을 지연된 위치에 두면 "화면에 안 닿았는데 밀리는" 판정이 된다.
    /// </summary>
    public class CharactersPredictedSyncPolicy : IEntitySyncPolicy
    {
        public EntitySyncMode For(GameFramework.World.Entity entity)
        {
            return entity.Get<EntityKind>()?.Kind == EntityType.Character
                ? EntitySyncMode.Predicted
                : EntitySyncMode.Interpolated;
        }
    }
}
