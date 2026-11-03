using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    public enum CardRushSfx { CardTick, Ding, Splash, Bonk, Fanfare }

    /// <summary>소리를 정할 때 보는 내 몸 한 장면. <see cref="Has"/>가 거짓이면 몸·코스가 아직 없다.</summary>
    public struct CardRushAudioFrame
    {
        public bool Has;
        public int Step;
        public long StunEndTick;
        public bool Finished;
        public bool FaceUp;
        public CardColor Color;
    }

    /// <summary>앞뒤 장면을 비교해 낼 소리를 고른다(spec §7.5). 상태가 바뀐 것만 듣는다 — 같은 장면은 몇 번 봐도 조용하다.</summary>
    public static class CardRushAudioCues
    {
        public static void Detect(in CardRushAudioFrame prev, in CardRushAudioFrame now, CardRushTheme theme, List<CardRushSfx> into)
        {
            //  첫 장면은 기준만 잡는다 — 들어오자마자(재접속 등) 지난 일로 소리를 내지 않게.
            if (prev.Has == false || now.Has == false)
            {
                return;
            }
            if (now.Step > prev.Step)
            {
                //  마지막 맞힘이 곧 완주 — 딩 대신 팡파르 하나.
                into.Add(now.Finished ? CardRushSfx.Fanfare : CardRushSfx.Ding);
            }
            if (now.StunEndTick > prev.StunEndTick)
            {
                into.Add(theme == CardRushTheme.StoneBridge ? CardRushSfx.Splash : CardRushSfx.Bonk);
            }
            //  HUD 카드가 통통 튀는 조건과 같다: 새로 앞면이 되거나 색이 바뀌면.
            if (now.FaceUp && (prev.FaceUp == false || now.Color != prev.Color))
            {
                into.Add(CardRushSfx.CardTick);
            }
        }
    }

    /// <summary>
    /// 카드 러시 소리 한 곳(spec §7.5). 내 몸 상태 변화(카드 색·맞음·틀림·완주)만 듣는다 — 남의 소리는 내지 않는다(8명이면 시끄럽다).
    /// 파일 없이 <see cref="FlappySynth"/>의 발진기·잡음으로 시작할 때 한 번 짓는다.
    /// <para>공용 경기 화면(RaceHud·RaceFinish)이 부르는 <see cref="IFlappyAudio"/> 자리도 이것이다 — 추월·도장 소리는 플래피 클립 그대로.
    /// 단 <see cref="FlappySfx.Finish"/>는 삼킨다: 완주 팡파르는 이 모드의 세 음이 상태에서 이미 냈다(두 번 울리지 않게).
    /// 배경음은 이 모드에 없다(spec §7.5 표에 없음) — 음악 메서드는 아무것도 안 한다.</para>
    /// </summary>
    public class CardRushAudio : IFlappyAudio, IInitializable, ITickable, IDisposable
    {
        /// <summary>카드 째깍 — 아주 작게. 리듬을 귀로도 읽게 하되 거슬리지 않게(spec §7.5 −18 dB).</summary>
        public const float TickVolumeDb = -18f;
        public const float DingVolumeDb = -4f;
        public const float MissVolumeDb = -2f;
        public const float FanfareVolumeDb = -3f;
        public const float RaceUiVolumeDb = 0f;

        /// <summary>맞음 "딩" — 연속이 길어져도 음을 올리지 않는다(플래피 콤보와 같은 결정).</summary>
        public const float DingHz = 880f;
        public const float DingSeconds = 0.12f;

        /// <summary>박치기 뒤 "뾰잉"을 멈춤의 이 비율에 낸다 — 몸개그가 벌떡 일어서는 순간(<see cref="CardRushGag"/> Bonk 0.70).</summary>
        public const float BonkStandUpAt = 0.70f;

        public static float Gain(float db) => Mathf.Pow(10f, db / 20f);

        private readonly CardRushWorld world;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly IGameDataStore gameDataStore;
        private readonly EntityRenderClock renderClock;
        private readonly Dictionary<CardRushSfx, AudioClip> clips = new Dictionary<CardRushSfx, AudioClip>();
        private readonly Dictionary<FlappySfx, AudioClip> raceClips = new Dictionary<FlappySfx, AudioClip>();
        private readonly List<AudioClip> synthesized = new List<AudioClip>();
        private readonly List<CardRushSfx> cues = new List<CardRushSfx>();
        private GameObject root;
        private AudioSource source;
        private bool disposed;
        private CardRushAudioFrame last;
        private string lastEntityId;

        //  시험 이음매 — 실제로는 AudioSource.PlayOneShot(클립, 크기). 시험은 무엇이 어떤 크기로 불렸는지만 본다.
        internal Action<AudioClip, float> playOneShot;

        public CardRushAudio(CardRushWorld world, GameFramework.World.EntityRegistry entityRegistry,
                             IGameDataStore gameDataStore, EntityRenderClock renderClock)
        {
            this.world = world;
            this.entityRegistry = entityRegistry;
            this.gameDataStore = gameDataStore;
            this.renderClock = renderClock;
            playOneShot = (clip, volume) =>
            {
                if (source != null && clip != null)
                {
                    source.PlayOneShot(clip, volume);
                }
            };
        }

        /// <summary>경기 스코프가 지어질 때 클립을 다 짓는다 — 첫 소리에서 짓느라 끊기지 않게.</summary>
        public void Initialize()
        {
            if (disposed || root != null)
            {
                return;
            }
            root = new GameObject("CardRushAudio");
            source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;

            float stunSeconds = (world != null ? world.Timing.StunTicks : CardRushTiming.Ticks(0.6f)) * CardRushTiming.TickInterval;
            foreach (CardRushSfx sfx in Enum.GetValues(typeof(CardRushSfx)))
            {
                clips[sfx] = Synthesize("CardRushSfx_" + sfx, FlappySynth.Render(Recipe(sfx, stunSeconds), 1000 + (int)sfx));
            }
            foreach (FlappySfx sfx in Enum.GetValues(typeof(FlappySfx)))
            {
                raceClips[sfx] = Synthesize("CardRushRace_" + sfx, FlappySynth.Render(sfx));
            }
        }

        /// <summary>소리 조리법(spec §7.5 표). 크기는 재생할 때 dB 상수로 준다 — 여기 값은 모양만.</summary>
        internal static FlappySynth.Part[] Recipe(CardRushSfx sfx, float stunSeconds)
        {
            switch (sfx)
            {
                case CardRushSfx.CardTick:
                    //  아주 짧은 딸깍: 높은 대역 잡음 + 짧은 높은 음.
                    return new[]
                    {
                        FlappySynth.Part.Noise(0.025f, 0.9f, 3800f, 1.4f),
                        FlappySynth.Part.Tone(2400f, 0.03f, FlappySynth.Wave.Sine, 0.4f),
                    };
                case CardRushSfx.Ding:
                    return new[] { FlappySynth.Part.Tone(DingHz, DingSeconds, FlappySynth.Wave.Sine, 0.6f) };
                case CardRushSfx.Splash:
                    //  걸러 낸 잡음 0.35초 — 낮은 첨벙 + 위에 흩어지는 물방울.
                    return new[]
                    {
                        FlappySynth.Part.Noise(0.35f, 1.0f, 700f, 0.6f),
                        FlappySynth.Part.Noise(0.28f, 0.45f, 2600f, 0.9f, 0.03f),
                    };
                case CardRushSfx.Bonk:
                {
                    //  낮은 쿵 0.2초, 그리고 일어설 때 용수철 미끄럼음(뾰잉 — 위로 미끄러지는 세모파).
                    float standUp = stunSeconds * BonkStandUpAt;
                    return new[]
                    {
                        FlappySynth.Part.Tone(120f, 0.2f, FlappySynth.Wave.Sine, 0.9f, 0f, 0.45f),
                        FlappySynth.Part.Noise(0.07f, 0.6f, 500f, 0.8f),
                        FlappySynth.Part.Tone(260f, 0.22f, FlappySynth.Wave.Triangle, 0.35f, standUp, 2.6f),
                    };
                }
                case CardRushSfx.Fanfare:
                    //  세 음 짧은 팡파르(도·미·솔, 마지막만 길게).
                    return new[]
                    {
                        FlappySynth.Part.Tone(523f, 0.12f, FlappySynth.Wave.Triangle, 0.5f, 0f),
                        FlappySynth.Part.Tone(659f, 0.12f, FlappySynth.Wave.Triangle, 0.5f, 0.12f),
                        FlappySynth.Part.Tone(784f, 0.4f, FlappySynth.Wave.Triangle, 0.5f, 0.24f),
                    };
                default:
                    throw new ArgumentOutOfRangeException(nameof(sfx), sfx, "조리법이 없는 소리");
            }
        }

        internal static float VolumeOf(CardRushSfx sfx) => sfx switch
        {
            CardRushSfx.CardTick => Gain(TickVolumeDb),
            CardRushSfx.Ding => Gain(DingVolumeDb),
            CardRushSfx.Splash => Gain(MissVolumeDb),
            CardRushSfx.Bonk => Gain(MissVolumeDb),
            _ => Gain(FanfareVolumeDb),
        };

        internal AudioClip ClipOf(CardRushSfx sfx)
        {
            Initialize();
            return clips.TryGetValue(sfx, out var clip) ? clip : null;
        }

        internal AudioClip ClipOf(FlappySfx sfx)
        {
            Initialize();
            return raceClips.TryGetValue(sfx, out var clip) ? clip : null;
        }

        public void Play(CardRushSfx sfx)
        {
            if (disposed)
            {
                return;
            }
            playOneShot(ClipOf(sfx), VolumeOf(sfx));
        }

        /// <summary>공용 경기 화면이 부르는 소리. 완주 팡파르만 삼킨다(이 모드의 팡파르가 상태에서 낸다).</summary>
        public void Play(FlappySfx sfx)
        {
            if (disposed || sfx == FlappySfx.Finish)
            {
                return;
            }
            playOneShot(ClipOf(sfx), Gain(RaceUiVolumeDb));
        }

        public void StartMusic() { }
        public void StopMusic() { }
        public void SetFinalStretch(bool on) { }
        public void DuckMusic() { }

        public void Tick()
        {
            string id = gameDataStore?.userEntityId;
            if (id != lastEntityId)
            {
                //  몸이 바뀌면(재접속) 기준을 새로 잡는다.
                lastEntityId = id;
                last = default;
            }
            Observe(ReadFrame(id));
        }

        /// <summary>새 장면을 보고 바뀐 만큼 소리를 낸다. 시험은 이것으로 장면을 직접 넣는다.</summary>
        internal void Observe(in CardRushAudioFrame frame)
        {
            cues.Clear();
            CardRushAudioCues.Detect(last, frame, world != null && world.Rules != null ? world.Rules.Theme : CardRushTheme.StoneBridge, cues);
            last = frame;
            foreach (var sfx in cues)
            {
                Play(sfx);
            }
        }

        private CardRushAudioFrame ReadFrame(string id)
        {
            var runner = string.IsNullOrEmpty(id) ? null : entityRegistry.Get(id)?.Get<CardRushRunner>();
            var course = world.Course;
            if (runner == null || course == null)
            {
                return default;
            }
            var s = runner.State;
            //  카드 색은 조작판과 같은 틱(내 몸 = 지금 예측 틱 = 틱 사이의 다음 입력 틱)으로 본다 — 본 색과 들은 째깍이 같은 순간.
            bool faceUp = CardRushCard.TryColorAt(s, renderClock.TickFor(id), course, out var color);
            return new CardRushAudioFrame
            {
                Has = true,
                Step = s.Step,
                StunEndTick = s.StunEndTick,
                Finished = s.IsFinished(course.StepCount),
                FaceUp = faceUp,
                Color = color,
            };
        }

        private AudioClip Synthesize(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, FlappySynth.Rate, false);
            clip.SetData(data, 0);
            synthesized.Add(clip);
            return clip;
        }

        public void Dispose()
        {
            disposed = true;
            CardRushGagFx.DestroyObject(root);
            root = null;
            source = null;
            foreach (var clip in synthesized)
            {
                CardRushGagFx.DestroyObject(clip);
            }
            synthesized.Clear();
            clips.Clear();
            raceClips.Clear();
        }
    }
}
