using System;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 멈칫·결승 느려짐(spec §3)을 내 새 그림에 건다. 계산은 <see cref="VisualTimeWarp"/>, 적용은 내 새 예측 보간기의
    /// <see cref="PredictedEntityInterpolator.renderDelaySeconds"/>. 카메라는 그 그림을 따라가므로 같이 선다.
    /// <para>Tick(Update)에서 넣으면 같은 프레임의 보간기 LateUpdate가 쓴다.</para>
    /// </summary>
    public class FlappyVisualTimeWarp : ITickable, IDisposable
    {
        private readonly VisualTimeWarp warp = new VisualTimeWarp();

        //  시험 이음매 — 실제로는 내 새 보간기.
        internal Action<float> applyDelay;
        internal VisualTimeWarp Warp => warp;

        public FlappyVisualTimeWarp(ActorRegistry actorRegistry, IGameDataStore gameDataStore)
        {
            applyDelay = delay =>
            {
                //  판 시작 직후엔 내 id가 비어 있다 — 사전에 null 키를 넣으면 터진다.
                string id = gameDataStore.userEntityId;
                if (string.IsNullOrEmpty(id))
                {
                    return;
                }
                var actor = actorRegistry.Get(id);
                var interpolator = actor != null ? actor.GetComponent<PredictedEntityInterpolator>() : null;
                if (interpolator != null)
                {
                    interpolator.renderDelaySeconds = delay;
                }
            };
        }

        public void Hitstop(float seconds) => warp.Hitstop(seconds);

        public void Slow(float seconds, float rate) => warp.Slow(seconds, rate);

        public void Tick()
        {
            Tick(Time.deltaTime);
        }

        internal void Tick(float deltaTime)
        {
            applyDelay(warp.Advance(deltaTime));
        }

        public void Dispose()
        {
            applyDelay(0f);
        }
    }
}
