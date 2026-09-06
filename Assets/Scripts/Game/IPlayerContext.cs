using GameFramework;

namespace LOP
{
    /// <summary>
    /// <b>지금 살아 있는 내 엔티티의 손잡이.</b> 내 몸이 사라지면(추격자에게 잡히는 등)
    /// <see cref="EntityBinder"/>가 <see cref="entityId"/>와 <see cref="actor"/>를 내린다.
    ///
    /// <para>정체를 묻는 것이 아니다 — "이 판에서 서버가 나에게 배정한 id"는 죽어도 안 변하고,
    /// 그건 <see cref="IGameDataStore.userEntityId"/>가 답한다. 둘은 같은 값을 담지만
    /// <b>수명이 다르다</b>: 이쪽은 몸이 있는 동안만, 저쪽은 판이 끝날 때까지.</para>
    ///
    /// <para>그래서 <c>entityId != null</c>이면 <b>그 엔티티가 레지스트리에 있다</b>.
    /// 클라에서 레지스트리 제거는 <c>EntitySpawner.FlushDespawns</c> 한 곳뿐이고 그 안에서
    /// 같은 호출로 <c>EntityDestroyed</c>를 쏘기 때문이다. 이 값을 쓰기 전에 레지스트리를
    /// 다시 확인할 필요가 없다.</para>
    /// </summary>
    public interface IPlayerContext
    {
        ISession session { get; set; }
        string entityId { get; set; }
        LOPActor actor { get; set; }
    }
}
