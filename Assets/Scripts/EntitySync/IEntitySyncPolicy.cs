namespace LOP
{
    /// <summary>
    /// 이 게임이 각 엔티티를 어떻게 따라갈지 정한다. 클라 게임 스코프가 구현체를 등록한다.
    /// 판정 재료는 로컬 유저 id와 엔티티가 이미 들고 있는 것뿐이다 — 게임 상태를 뒤지기 시작하면
    /// 그건 정책이 아니라 로직이다.
    /// </summary>
    public interface IEntitySyncPolicy
    {
        EntitySyncMode For(GameFramework.World.Entity entity);
    }
}
