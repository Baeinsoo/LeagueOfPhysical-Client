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
        private readonly Dictionary<string, GameObject> dressed = new Dictionary<string, GameObject>();
        private Material faceMaterial;

        public DodgeChibiView(GameFramework.Runner.IRunner runner, DodgeClientState state, ActorRegistry actorRegistry)
        {
            this.runner = runner;
            this.state = state;
            this.actorRegistry = actorRegistry;
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
                    ChibiDresser.Dress(id, visual, faceMaterial);
                    dressed[id] = visual;
                }
                var face = visual.GetComponent<ChibiFace>();
                if (face != null)
                {
                    face.SetExpression(DodgeHitFeedback.ExpressionFor(renderTick, state.InvulnerableUntil(id)));
                }
            }
        }
    }
}
