using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 충돌 묶음(spec §3). 내 새가 기절에 <b>들어가는 순간</b>(예측 기준 — 서버 교정으로 기절이 생겨도 같은 값으로 들어온다):
    /// 트라우마 +0.55 + 돌 파편 + 쿵 소리(음악 잠깐 낮춤). 남의 새가 기절하면 작은 파편 + <b>지금 보는 새</b>와
    /// 14 m 안이면 작은 쿵. 판정과 무관한 화면·소리 연출이다(10-25: 그림은 늘 실제 위치를 그리므로 멈칫은 없다).
    /// <para>"지금 보는 새"는 내가 달리는 동안은 늘 나 자신이지만, 완주한 뒤 관전 중엔 다른 새가 된다
    /// (<see cref="FlappySpectate"/>). 그래서 기준점을 내 새가 아니라 관전 대상으로 잡는다 — 안 그러면 관전 중인 화면과
    /// 먼 내 완주 자리를 기준으로 가깝고 먼지를 재서 코앞 충돌이 조용하다.</para>
    /// <para>새마다 직전 기절 여부를 쥐고 "아니었다 → 이다"일 때만 터뜨린다. 처음 본 새는 기준만 잡는다 — 그림이 기절 도중에
    /// 붙으면(늦은 로드·재접속) 이미 지난 충돌을 터뜨리지 않는다.</para>
    /// </summary>
    public class FlappyCrashFx : ITickable
    {
        internal const float TraumaKick = 0.55f;
        internal const float FarSoundRange = 14f;

        //  시제품 burst 색: 흙(돌가루)과 불티.
        private static readonly Color Dust = new Color32(0xd9, 0xc3, 0xa8, 0xff);
        private static readonly Color Spark = new Color32(0xff, 0xcf, 0x8a, 0xff);

        private readonly IFlappyBirdLooks looks;
        private readonly FlappyCameraShake shake;
        private readonly FlappyBurstFx bursts;
        private readonly IFlappyAudio audio;
        private readonly FlappyConfig config;
        private readonly FlappySpectate spectate;

        private readonly List<FlappyBirdLook> buffer = new List<FlappyBirdLook>();
        private readonly Dictionary<string, bool> wasStunned = new Dictionary<string, bool>();
        private readonly HashSet<string> seen = new HashSet<string>();
        private readonly List<string> gone = new List<string>();

        //  시험 이음매 — 실제로는 FlappySpectate.Current(단일 주인). FlappySpectate를 매 틱 다시
        //  Refresh하면 안 된다(FlappyHudCoordinator가 이미 그 틱에 한 번 돌렸다 — 결승 붙잡기 타이머가
        //  이중으로 흐른다), 그래서 여기선 읽기만 한다.
        internal Func<string> watchedEntityId;

        public FlappyCrashFx(IFlappyBirdLooks looks, FlappyCameraShake shake,
                             FlappyBurstFx bursts, IFlappyAudio audio, FlappyConfig config, FlappySpectate spectate)
        {
            this.looks = looks;
            this.shake = shake;
            this.bursts = bursts;
            this.audio = audio;
            this.config = config;
            this.spectate = spectate;
            watchedEntityId = () => spectate.Current;
        }

        public void Tick()
        {
            looks.Fill(buffer);
            string watchedId = watchedEntityId();
            Vector3? watched = null;
            foreach (var bird in buffer)
            {
                if (bird.Id == watchedId)
                {
                    watched = Center(bird);
                }
            }

            seen.Clear();
            foreach (var bird in buffer)
            {
                seen.Add(bird.Id);
                bool known = wasStunned.TryGetValue(bird.Id, out bool before);
                wasStunned[bird.Id] = bird.Stunned;
                if (known == false || before || bird.Stunned == false)
                {
                    continue;
                }
                if (bird.Mine)
                {
                    OnMyCrash(Center(bird));
                }
                else
                {
                    OnOtherCrash(Center(bird), watched);
                }
            }

            //  그림이 사라진 새는 잊는다 — 다시 붙으면 처음 본 새처럼 기준부터 잡는다.
            gone.Clear();
            foreach (var id in wasStunned.Keys)
            {
                if (seen.Contains(id) == false)
                {
                    gone.Add(id);
                }
            }
            foreach (var id in gone)
            {
                wasStunned.Remove(id);
            }
        }

        private Vector3 Center(FlappyBirdLook bird) => bird.Visual.position + Vector3.up * config.BodyRadius;

        private void OnMyCrash(Vector3 at)
        {
            shake.AddTrauma(TraumaKick);
            bursts.Burst(at, Dust, 22, 9f, 0.6f, 0.2f);
            bursts.Burst(at, Spark, 8, 6f, 0.4f, 0.14f);
            audio.Play(FlappySfx.Crash);
            audio.DuckMusic();
        }

        //  남의 충돌은 화면을 흔들지 않는다 — 흔들리면 내가 박은 줄 안다. 소리는 x 거리만 본다(시제품과 같다, 통로라 위아래는 좁다).
        //  기준은 지금 보는 새(watched) — 내가 달리는 동안은 나, 완주 뒤 관전 중엔 관전 대상. 그림이 아직 안 붙었으면(또는
        //  볼 사람이 아예 없으면) 소리는 내지 않는다 — 기준점이 없다.
        private void OnOtherCrash(Vector3 at, Vector3? watched)
        {
            bursts.Burst(at, Dust, 10, 6f, 0.45f, 0.14f);
            if (watched.HasValue && Mathf.Abs(at.x - watched.Value.x) < FarSoundRange)
            {
                audio.Play(FlappySfx.CrashFar);
            }
        }
    }
}
