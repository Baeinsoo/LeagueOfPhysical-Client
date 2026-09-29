using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 무적인 동안 몸을 깜빡인다(나와 남 모두). 무적 끝 틱은 서버가 상태로 보낸 값이다 — 클라는 맞음을 판단하지 않는다.
    /// 끄는 것은 이 뷰가 끈 렌더러만 다시 켠다 — 원래 꺼져 있던 부품을 켜 버리지 않게.
    /// </summary>
    public class DodgeBlinkView : ILateTickable, IDisposable
    {
        private readonly GameFramework.Runner.IRunner runner;
        private readonly DodgeClientState state;
        private readonly ActorRegistry actorRegistry;
        private readonly Dictionary<string, (GameObject visual, int count, Renderer[] renderers)> cache =
            new Dictionary<string, (GameObject, int, Renderer[])>();
        private readonly HashSet<Renderer> hiddenByUs = new HashSet<Renderer>();

        public DodgeBlinkView(GameFramework.Runner.IRunner runner, DodgeClientState state, ActorRegistry actorRegistry)
        {
            this.runner = runner;
            this.state = state;
            this.actorRegistry = actorRegistry;
        }

        public void LateTick()
        {
            if (runner?.tickUpdater == null || runner.tickUpdater.interval <= 0d)
            {
                return;
            }
            double interval = runner.tickUpdater.interval;
            // 위험 그림과 같은 틱 — 내 몸이 그려지는 시각(DodgeHazardView 참고).
            long renderTick = (long)Math.Floor((runner.tickUpdater.elapsedTime - interval) / interval);

            foreach (var id in state.PlayerIds)
            {
                if (!actorRegistry.TryGet(id, out var actor) || actor == null || actor.visualGameObject == null)
                {
                    continue;   // 탈락해 몸이 사라졌거나 아직 안 생겼다
                }
                var renderers = RenderersOf(id, actor.visualGameObject);
                bool visible = DodgeHitFeedback.BodyVisible(renderTick, state.InvulnerableUntil(id));
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    if (!visible && r.enabled)
                    {
                        r.enabled = false;
                        hiddenByUs.Add(r);
                    }
                    else if (visible && hiddenByUs.Remove(r))
                    {
                        r.enabled = true;
                    }
                }
            }
        }

        // 몸이 바뀌거나 자식이 늘면(얼굴 판은 몸보다 늦게 붙는다) 새로 모은다 — 매 프레임 GetComponentsInChildren을 부르지 않게.
        private Renderer[] RenderersOf(string id, GameObject visual)
        {
            int count = visual.transform.hierarchyCount;
            if (cache.TryGetValue(id, out var c) && c.visual == visual && c.count == count)
            {
                return c.renderers;
            }
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            cache[id] = (visual, count, renderers);
            return renderers;
        }

        public void Dispose()
        {
            foreach (var r in hiddenByUs)
            {
                if (r != null) r.enabled = true;
            }
            hiddenByUs.Clear();
            cache.Clear();
        }
    }
}
