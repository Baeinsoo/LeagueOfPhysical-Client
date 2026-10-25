using System;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 판의 큰 순간에 소리·연출을 건다. 내 새가 생기면 배경음(spec §6), 결승하면 음악을 끄고(spec §5 "결승하면 음악이 끝난다")
    /// 꽃가루 한 번(spec §3), 탈락하면 음악만 끈다. 결승음·도장음은 도장 창(<see cref="LOP.UI.RaceFinishViewModel"/>)이 낸다.
    /// 그림은 늘 실제 위치를 그리므로 결승 느려짐은 없다(사용자 결정 2026-10-25).
    /// <para>결승은 알림이 없다 — 매 틱 바뀌는 상태(FinishState)라 <see cref="FlappyHudCoordinator"/>처럼 여기서도 본다. "달리는 걸 본 뒤"의
    /// 결승만 연출한다 — 이미 결승한 채로 들어오면(재접속) 지난 순간을 다시 터뜨리지 않는다.</para>
    /// </summary>
    public class FlappyRaceCues : ITickable, IDisposable
    {
        internal const int ConfettiPerColor = 14;

        //  시제품 꽃가루 4색.
        private static readonly Color[] ConfettiColors =
        {
            new Color32(0xff, 0xd2, 0x57, 0xff),
            new Color32(0xff, 0x8f, 0xb1, 0xff),
            new Color32(0x7f, 0xd1, 0xff, 0xff),
            new Color32(0xa5, 0xe8, 0x6a, 0xff),
        };

        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly ActorRegistry actorRegistry;
        private readonly IGameDataStore gameDataStore;
        private readonly IFlappyAudio audio;
        private readonly FlappyBurstFx bursts;
        private readonly FlappyConfig config;

        private bool musicOn;
        private bool musicDone;
        private bool sawRacing;

        //  시험 이음매 — 0~1.
        internal Func<float> random = () => UnityEngine.Random.value;

        public FlappyRaceCues(GameFramework.World.EntityRegistry entityRegistry, ActorRegistry actorRegistry,
                              IGameDataStore gameDataStore, IFlappyAudio audio,
                              FlappyBurstFx bursts, FlappyConfig config)
        {
            this.entityRegistry = entityRegistry;
            this.actorRegistry = actorRegistry;
            this.gameDataStore = gameDataStore;
            this.audio = audio;
            this.bursts = bursts;
            this.config = config;
        }

        public void Tick()
        {
            //  판 시작 직후엔 내 id가 비어 있다 — 사전에 null 키를 넣으면 터진다.
            string id = gameDataStore.userEntityId;
            var mine = string.IsNullOrEmpty(id) ? null : entityRegistry.Get(id);
            bool finished = mine?.Get<FinishState>()?.Finished == true;

            if (mine != null && finished == false)
            {
                sawRacing = true;
                if (musicOn == false && musicDone == false)
                {
                    audio.StartMusic();
                    musicOn = true;
                }
                return;
            }

            //  결승했거나 몸이 사라졌다(탈락·판 정리) — 음악은 한 번만 끈다.
            if (musicOn && musicDone == false)
            {
                audio.StopMusic();
                musicDone = true;
            }
            if (finished && sawRacing)
            {
                sawRacing = false;
                OnMyFinish(id);
            }
        }

        private void OnMyFinish(string id)
        {
            var actor = actorRegistry.Get(id);
            var visual = actor != null ? actor.visualGameObject : null;
            if (visual == null)
            {
                return;
            }
            //  시제품은 결승선 위쪽에 뿌렸다. 카메라가 내 새를 따라가므로 내 새 위쪽에 뿌린다(결승 순간이라 결승선 바로 앞이다).
            Vector3 center = visual.transform.position + Vector3.up * config.BodyRadius;
            foreach (var color in ConfettiColors)
            {
                var at = center + new Vector3((random() - 0.5f) * 3f, 1f + random() * 2f, 0f);
                bursts.Burst(at, color, ConfettiPerColor, 7f, 1.2f, 0.18f);
            }
        }

        public void Dispose()
        {
            if (musicOn && musicDone == false)
            {
                audio.StopMusic();
                musicDone = true;
            }
        }
    }
}
