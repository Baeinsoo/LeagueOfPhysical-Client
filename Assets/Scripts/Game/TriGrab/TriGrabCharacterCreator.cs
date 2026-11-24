namespace LOP
{
    /// <summary>
    /// 삼색 그랩의 몸 — 보통 캐릭터 몸을 만든 뒤 걷는 속도만 이 모드 값(<see cref="LOP.TriGrab.TriGrabConfig.MoveSpeed"/>)으로 바꾼다.
    /// 서버도 같은 일을 한다(서버 TriGrabCharacterCreator). 내 몸 예측이 서버와 같은 속도여야 보정 떨림이 없다.
    /// </summary>
    public sealed class TriGrabCharacterCreator : ICharacterCreator
    {
        private readonly ICharacterCreator inner;
        private readonly GameFramework.World.EntityRegistry registry;

        public TriGrabCharacterCreator(ICharacterCreator inner, GameFramework.World.EntityRegistry registry)
        {
            this.inner = inner;
            this.registry = registry;
        }

        public void Create(CharacterCreationData data)
        {
            inner.Create(data);
            var stats = registry.Get(data.entityId)?.Get<GameFramework.World.Stats>();
            if (stats != null)
            {
                stats.BaseStats[(int)GameFramework.World.EntityStatType.MoveSpeed] = LOP.TriGrab.TriGrabConfig.MoveSpeed;
            }
        }
    }
}
