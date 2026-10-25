using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 상대 새를 차분한 색으로 칠한다(spec §2). 내 새만 채도 높은 노랑으로 남아야 여러 새 사이에서 한눈에 찾는다.
    /// <para>머티리얼을 직접 갈지 않고 <see cref="StunAppearance.SetBaseTint"/>에 맡긴다 — 새 머티리얼의 주인은 그 하나다
    /// (둘이면 기절이 풀릴 때 칠이 지워진다). 판정과 무관한 클라 전용 연출.</para>
    /// <para>새 꾸밈(코스튬)은 아직 없다. 생기면 꾸민 새는 칠하지 않는다(spec "꾸밈이 생기면 그쪽이 이긴다") — 그 슬라이스에서 여기를 고친다.</para>
    /// </summary>
    public class FlappyOpponentTint : ITickable
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly ActorRegistry actorRegistry;
        private readonly IGameDataStore gameDataStore;
        private readonly FlappyOpponentColors colors;

        //  이미 칠을 넘긴 새. StunAppearance가 모델 교체 때 칠을 스스로 다시 입히므로 한 번이면 된다.
        private readonly HashSet<string> tinted = new HashSet<string>();
        private readonly List<string> gone = new List<string>();

        public FlappyOpponentTint(GameFramework.World.EntityRegistry entityRegistry, ActorRegistry actorRegistry,
                                  IGameDataStore gameDataStore, FlappyOpponentColors colors)
        {
            this.entityRegistry = entityRegistry;
            this.actorRegistry = actorRegistry;
            this.gameDataStore = gameDataStore;
            this.colors = colors;
        }

        public void Tick()
        {
            foreach (var entity in entityRegistry.All)
            {
                if (entity.Get<EntityKind>()?.Kind != EntityType.Character
                    || entity.Id == gameDataStore.userEntityId
                    || tinted.Contains(entity.Id))
                {
                    continue;
                }
                var appearance = actorRegistry.Get(entity.Id)?.GetComponent<StunAppearance>();
                if (appearance == null)
                {
                    continue;   //  액터가 아직 없다 — 다음 프레임에 다시 본다
                }
                appearance.SetBaseTint(colors.ColorOf(entity.Id), IsBodyMaterial);
                tinted.Add(entity.Id);
            }

            //  떠난 새는 목록에서 뺀다 — 같은 id로 다시 들어오면(재접속) 새 액터를 다시 칠해야 한다.
            gone.Clear();
            foreach (var id in tinted)
            {
                if (entityRegistry.Get(id) == null)
                {
                    gone.Add(id);
                }
            }
            foreach (var id in gone)
            {
                tinted.Remove(id);
            }
        }

        //  새 FBX의 몸 머티리얼 이름은 BodyMaterial(부리 BeakMaterial). 부리까지 칠하면 새가 한 덩어리 색이 된다.
        internal static bool IsBodyMaterial(Material material)
            => material != null && material.name.IndexOf("Body", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
