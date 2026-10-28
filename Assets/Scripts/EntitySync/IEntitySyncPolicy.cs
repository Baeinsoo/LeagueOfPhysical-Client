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

        /// <summary>
        /// 내 몸의 서버 보정을 화면에서 녹이는 시간(초). 0이면 녹이지 않고 바로 맞춘다 — 내가 조종하는 몸을
        /// 녹이면 그동안 손과 화면이 어긋나기 때문이다. 서버가 정한 사건으로 내 몸이 크게 옮겨지는 모드만 켠다.
        /// </summary>
        float LocalCorrectionSmoothTime { get; }
    }
}
