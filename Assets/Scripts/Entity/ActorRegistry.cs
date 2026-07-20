using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// id→뷰 앵커(<see cref="LOPActor"/>) 인덱스. World <c>EntityRegistry</c>(id→데이터 진실원본)와 별개 축.
    /// 로직·조율 없는 dumb 인덱스 — <see cref="EntityBinder"/>가 채우고 비운다. 소비자는 서버 스냅 수신 등에서
    /// id→actor가 필요할 때 여기를 조회한다.
    /// </summary>
    public class ActorRegistry
    {
        private readonly Dictionary<string, LOPActor> actors = new Dictionary<string, LOPActor>();

        public void Add(LOPActor actor)
        {
            actors[actor.entityId] = actor;
        }

        public bool Remove(string entityId)
        {
            return actors.Remove(entityId);
        }

        public LOPActor Get(string entityId)
        {
            return actors.TryGetValue(entityId, out var actor) ? actor : null;
        }

        public bool TryGet(string entityId, out LOPActor actor)
        {
            return actors.TryGetValue(entityId, out actor);
        }

        public bool Contains(string entityId)
        {
            return actors.ContainsKey(entityId);
        }

        public int Count => actors.Count;

        public IEnumerable<LOPActor> All => actors.Values;
    }
}
