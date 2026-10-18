using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// Dodge 선수가 치비면 얼굴 판·저지를 입히고(한 발 승부와 같은 방식), 무적 동안 놀란 표정을 짓게 한다.
    /// 치비가 아닌 몸(옛 서버의 기사)은 건드리지 않는다. 얼굴 재질을 못 받으면 얼굴 없이 간다.
    /// </summary>
    public class DodgeChibiView : IStartable, ILateTickable
    {
        private const string FaceMaterialKey = "Assets/Characters/Chibi/Materials/ChibiFace.mat";

        private readonly GameFramework.Runner.IRunner runner;
        private readonly DodgeClientState state;
        private readonly ActorRegistry actorRegistry;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly CosmeticCatalog catalog;
        private readonly AppearanceColorPalette palette;
        private readonly Dictionary<string, GameObject> dressed = new Dictionary<string, GameObject>();
        private Material faceMaterial;
        private readonly HashSet<string> players = new HashSet<string>();

        public DodgeChibiView(GameFramework.Runner.IRunner runner, DodgeClientState state, ActorRegistry actorRegistry,
                              GameFramework.World.EntityRegistry entityRegistry, CosmeticCatalog catalog,
                              AppearanceColorPalette palette)
        {
            this.runner = runner;
            this.state = state;
            this.actorRegistry = actorRegistry;
            this.entityRegistry = entityRegistry;
            this.catalog = catalog;
            this.palette = palette;
        }

        public void Start()
        {
            UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<Material>(FaceMaterialKey).Completed += h =>
            {
                faceMaterial = h.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded ? h.Result : null;
                dressed.Clear();   // 재질보다 몸이 먼저 떴으면 얼굴 없이 입었다 — 다시 입힌다
            };
        }

        public void LateTick()
        {
            if (runner?.tickUpdater == null || runner.tickUpdater.interval <= 0d)
            {
                return;
            }
            double interval = runner.tickUpdater.interval;
            long renderTick = (long)System.Math.Floor((runner.tickUpdater.elapsedTime - interval) / interval);
            players.Clear();
            foreach (var id in state.PlayerIds) players.Add(id);
            foreach (var id in state.PlayerIds)
            {
                if (!actorRegistry.TryGet(id, out var actor) || actor == null || actor.visualGameObject == null)
                {
                    continue;
                }
                var visual = actor.visualGameObject;
                if (!ChibiDresser.IsChibi(visual))
                {
                    continue;
                }
                if (!dressed.TryGetValue(id, out var last) || last != visual)
                {
                    ChibiDresser.Dress(visual, faceMaterial, entityRegistry.Get(id)?.Get<PlayerLook>(), catalog, palette);
                    dressed[id] = visual;
                }
                var face = visual.GetComponent<ChibiFace>();
                if (face != null)
                {
                    face.SetReaction(DodgeHitFeedback.ExpressionFor(renderTick, state.InvulnerableUntil(id)));
                }
            }

            // 선수가 아닌 치비 = 탄막 투척 심판(서버가 식대로 세우는 캐릭터). 같은 심판 옷을 입힌다.
            foreach (var actor in actorRegistry.All)
            {
                if (actor == null || actor.visualGameObject == null || players.Contains(actor.entityId)) continue;
                var visual = actor.visualGameObject;
                if (!ChibiDresser.IsChibi(visual)) continue;
                if (!dressed.TryGetValue(actor.entityId, out var last) || last != visual)
                {
                    ChibiDresser.Dress(visual, faceMaterial, RefereeLook, catalog, palette);
                    dressed[actor.entityId] = visual;
                }
            }
        }

        /// <summary>심판의 정해진 모습 — 선수 기본 옷(파랑)과 구별되게 초록 상의.</summary>
        public static readonly PlayerLook RefereeLook = new PlayerLook(
            new System.Collections.Generic.Dictionary<string, string> { ["top"] = "top_tint_green" }, "", 1,
            new CharacterAppearance(102, 202));
    }
}
