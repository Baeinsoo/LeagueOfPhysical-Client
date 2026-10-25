using System.Collections.Generic;

namespace LOP
{
    /// <summary>새 한 마리에 대해 순간 연출이 아는 것: 누구, 내 새인가, 지금 기절 중인가, 그려진 그림.</summary>
    public readonly struct FlappyBirdLook
    {
        public readonly string Id;
        public readonly bool Mine;
        public readonly bool Stunned;
        public readonly UnityEngine.Transform Visual;

        public FlappyBirdLook(string id, bool mine, bool stunned, UnityEngine.Transform visual)
        {
            Id = id;
            Mine = mine;
            Stunned = stunned;
            Visual = visual;
        }
    }

    /// <summary>그림이 붙은 새들을 한 번에 읽는다. 충돌 묶음(<see cref="FlappyCrashFx"/>)과 기절 별(<see cref="FlappyStunStars"/>)이 같은 답을 본다.</summary>
    public interface IFlappyBirdLooks
    {
        void Fill(List<FlappyBirdLook> into);
    }

    /// <summary>
    /// 실물. 내 새의 기절은 <b>예측 FlappyStun</b>에서 읽는다 — 내가 부딪힌 틱에 바로 알고, 서버 교정으로 기절이 생겨도
    /// 교정 처리기가 같은 값을 고쳐 넣으므로 같은 길로 들어온다(spec §3). 남의 새는 스냅이 FlappyStun을 안 채우므로
    /// <see cref="StunAppearance.State"/>(스냅의 기절 끝 틱으로 칠한 상태)를 읽는다.
    /// </summary>
    public class FlappyBirdLooks : IFlappyBirdLooks
    {
        private readonly ActorRegistry actorRegistry;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly IGameDataStore gameDataStore;

        public FlappyBirdLooks(ActorRegistry actorRegistry, GameFramework.World.EntityRegistry entityRegistry,
                               IGameDataStore gameDataStore)
        {
            this.actorRegistry = actorRegistry;
            this.entityRegistry = entityRegistry;
            this.gameDataStore = gameDataStore;
        }

        public void Fill(List<FlappyBirdLook> into)
        {
            into.Clear();
            string myId = gameDataStore.userEntityId;
            foreach (var actor in actorRegistry.All)
            {
                if (actor == null)
                {
                    continue;
                }
                var visual = actor.visualGameObject;
                if (visual == null)
                {
                    continue;   //  그림이 아직 안 붙었다(Addressables 로드 중) — 연출을 붙일 자리가 없다
                }
                bool mine = string.IsNullOrEmpty(myId) == false && actor.entityId == myId;
                bool stunned;
                if (mine)
                {
                    stunned = (entityRegistry.Get(myId)?.Get<FlappyStun>()?.StunRemaining ?? 0f) > 0f;
                }
                else
                {
                    var appearance = actor.GetComponent<StunAppearance>();
                    stunned = appearance != null && appearance.State == StunVisual.Stunned;
                }
                into.Add(new FlappyBirdLook(actor.entityId, mine, stunned, visual.transform));
            }
        }
    }
}
