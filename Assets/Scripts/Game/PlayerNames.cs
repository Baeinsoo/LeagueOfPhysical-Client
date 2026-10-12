namespace LOP
{
    /// <summary>
    /// 엔티티의 실제 표시 이름(몸의 <see cref="PlayerLook"/>)을 찾는다. 룩이 없거나·이름이 비었거나·
    /// 엔티티를 못 찾으면(몬스터·구버전 서버·이미 디스폰된 몸) 호출부가 건넨 기존 문구를 그대로 쓴다.
    /// </summary>
    public static class PlayerNames
    {
        public static string For(GameFramework.World.EntityRegistry registry, string entityId, string fallback)
        {
            string name = registry?.Get(entityId)?.Get<PlayerLook>()?.DisplayName;
            return string.IsNullOrEmpty(name) ? fallback : name;
        }
    }
}
