namespace LOP
{
    /// <summary>스냅에서 위치 말고 맞춰야 할 게 없는 게임용(예: 판치기). 아무 일도 하지 않는다.</summary>
    public class NoServerCorrection : IServerCorrectionHandler
    {
        public bool Matches(long tick, EntitySnap snap) => true;
        public void ApplyAuthoritative(GameFramework.World.Entity entity, EntitySnap snap, float deltaTime) { }
    }
}
