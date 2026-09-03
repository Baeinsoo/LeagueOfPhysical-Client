namespace LOP
{
    /// <summary>
    /// 지금 누구를 보고 있나. 살아 있으면 내 새, 탈락했으면 <b>생존자 중 꼴찌</b>다 —
    /// 다음에 잡힐 사람이라 추격자가 같은 화면 안에 있다(선두를 보면 벽이 화면 밖이라
    /// 아무 일도 안 일어난다).
    ///
    /// <para>카메라와 벽 그리기가 같은 답을 봐야 해서 규칙을 한 곳에 둔다.</para>
    /// </summary>
    public static class FlappyWatchTarget
    {
        public static string Resolve(GameFramework.World.EntityRegistry entityRegistry, string myEntityId)
        {
            if (string.IsNullOrEmpty(myEntityId) == false && entityRegistry.Get(myEntityId) != null)
            {
                return myEntityId;
            }

            string best = null;
            float bestX = float.MaxValue;
            foreach (var entity in entityRegistry.All)
            {
                if (entity.Get<EntityKind>()?.Kind != EntityType.Character)
                {
                    continue;
                }
                var body = entity.Get<GameFramework.World.Transform>();
                if (body == null)
                {
                    continue;
                }
                //  같은 자리면 id가 작은 쪽. 레지스트리 순회 순서가 정해져 있지 않아서,
                //  안 정하면 프레임마다 카메라가 두 새 사이를 오갈 수 있다.
                if (body.Position.X < bestX ||
                    (body.Position.X == bestX && string.CompareOrdinal(entity.Id, best) < 0))
                {
                    best = entity.Id;
                    bestX = body.Position.X;
                }
            }
            return best;
        }
    }
}
