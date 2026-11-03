using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    public enum CardRushSfx { CardTick, Whoosh, Thunk, Slap, Splash, Fanfare }

    /// <summary>소리를 정할 때 보는 내 몸 한 장면. <see cref="Has"/>가 거짓이면 몸·코스가 아직 없다.</summary>
    public struct CardRushAudioFrame
    {
        public bool Has;
        public int Step;
        public long MoveStartTick;
        public long StunEndTick;
        public int StunTicks;       // 틀린 던짐 틱 = StunEndTick − StunTicks
        public int MoveTicks;       // 진짜 던짐 둘 사이는 이보다 멀다(이동이 끝나야 다음 시작) — 그보다 가까우면 보정이 옮긴 같은 던짐
        public double DrawnTick;    // 이 장면의 그림 틱 — 꽂힘·철썩은 이 틱이 착지 틱을 넘는 장면에서 한 번 낸다
        public bool Finished;
        public bool FaceUp;
        public CardColor Color;
    }

    /// <summary>
    /// 앞뒤 장면을 비교해 낼 소리를 고른다(spec §7.5, 2026-11-03 §5.3). 던짐(휙)은 상태가 바뀐 장면에서,
    /// 꽂힘(탁)·철썩은 그림 틱이 착지 틱을 넘는 장면에서 한 번 — 같은 장면은 몇 번 봐도 조용하다.
    /// </summary>
    /// <summary>마지막으로 울린 휙·탁·철썩의 던짐 틱 — 보정이 던짐 결과·틱을 바꿔도 같은 던짐은 한 번만 울린다(최종 리뷰 m2). 0 = 없음.</summary>
    public struct CardRushAudioMemory
    {
        public long Whoosh;
        public long Thunk;
        public long Slap;
    }

    public static class CardRushAudioCues
    {
        public static void Detect(in CardRushAudioFrame prev, in CardRushAudioFrame now, CardRushTheme theme, List<CardRushSfx> into)
        {
            var memory = new CardRushAudioMemory();
            Detect(prev, now, theme, into, ref memory);
        }

        /// <summary>
        /// 같은 던짐인가 — 진짜 던짐 둘은 이동(MoveTicks)보다 멀리 떨어진다(맞음 뒤 이동이 끝나야, 틀림 뒤 멈춤이 끝나야 다음 시작).
        /// 그보다 가까운 던짐 틱은 보정이 옮긴(또는 틀림 ↔ 맞음으로 바꾼) 같은 던짐이다.
        /// </summary>
        private static bool SameThrow(long last, long at, int gap) => gap > 0 && last != 0 && System.Math.Abs(at - last) < gap;

        //  같은 던짐이면 소리 없이 기억만 옮기고, 아니면 울리고 기억한다.
        private static bool Fresh(ref long last, long at, int gap)
        {
            bool fresh = SameThrow(last, at, gap) == false;
            last = at;
            return fresh;
        }

        public static void Detect(in CardRushAudioFrame prev, in CardRushAudioFrame now, CardRushTheme theme, List<CardRushSfx> into,
                                  ref CardRushAudioMemory memory)
        {
            int gap = now.MoveTicks;
            //  첫 장면은 기준만 잡는다 — 들어오자마자(재접속 등) 지난 일로 소리를 내지 않게.
            if (prev.Has == false || now.Has == false)
            {
                return;
            }
            if (now.Step > prev.Step)
            {
                //  마지막 맞힘이 곧 완주 — 휙 대신 팡파르 하나(완주는 한 번뿐이라 늘 운다).
                if (now.Finished) into.Add(CardRushSfx.Fanfare);
                else if (Fresh(ref memory.Whoosh, now.MoveStartTick, gap)) into.Add(CardRushSfx.Whoosh);
            }
            if (now.StunEndTick > prev.StunEndTick)
            {
                long wrongAt = now.StunEndTick - now.StunTicks;
                if (theme == CardRushTheme.StoneBridge) into.Add(CardRushSfx.Splash);
                else if (Fresh(ref memory.Whoosh, wrongAt, gap)) into.Add(CardRushSfx.Whoosh);
            }
            //  맞은 카드가 문에 꽂히는 틱(완주 땐 팡파르만).
            if (now.MoveStartTick != 0 && now.Finished == false
                && Crossed(prev.DrawnTick, now.DrawnTick, now.MoveStartTick + CardRushThrowFx.FlightTicks)
                && Fresh(ref memory.Thunk, now.MoveStartTick, gap))
            {
                into.Add(CardRushSfx.Thunk);
            }
            //  틀린 카드가 문에 튕겨 얼굴에 닿는 틱 — 문 박치기 테마만(돌다리는 풍덩 그대로).
            if (theme == CardRushTheme.DoorDash && now.StunEndTick != 0
                && Crossed(prev.DrawnTick, now.DrawnTick, now.StunEndTick - now.StunTicks + CardRushThrowFx.SlapTick)
                && Fresh(ref memory.Slap, now.StunEndTick - now.StunTicks, gap))
            {
                into.Add(CardRushSfx.Slap);
            }
            //  머리 위 카드가 통통 튀는 조건과 같다: 새로 앞면이 되거나 색이 바뀌면.
            if (now.FaceUp && (prev.FaceUp == false || now.Color != prev.Color))
            {
                into.Add(CardRushSfx.CardTick);
            }
        }

        //  (prev, now] — 착지 틱을 막 넘은 장면에서만.
        private static bool Crossed(double prevTick, double nowTick, double at) => prevTick < at && at <= nowTick;
    }

    /// <summary>
    /// 카드 러시 소리 한 곳(spec §7.5). 내 몸 상태 변화(카드 색·맞음·틀림·완주)만 듣는다 — 남의 소리는 내지 않는다(8명이면 시끄럽다).
    /// 파일 없이 <see cref="FlappySynth"/>의 발진기·잡음으로 시작할 때 한 번 짓는다.
    /// <para>공용 경기 화면(RaceHud·RaceFinish)이 부르는 <see cref="IFlappyAudio"/> 자리도 이것이다 — 추월·도장 소리는 플래피 클립 그대로.
    /// 단 <see cref="FlappySfx.Finish"/>는 삼킨다: 완주 팡파르는 이 모드의 세 음이 상태에서 이미 냈다(두 번 울리지 않게).
    /// 배경음은 이 모드에 없다(spec §7.5 표에 없음) — 음악 메서드는 아무것도 안 한다.</para>
    /// </summary>
    /// <remarks>틱·보간 뒤(IPostLateTickable)에서 듣는다 — 째깍이 머리 위 카드와 같은 순간·같은 틱(최종 리뷰 I1).</remarks>
    public class CardRushAudio : IFlappyAudio, IInitializable, IPostLateTickable, IDisposable
    {
        /// <summary>카드 째깍 — 아주 작게. 리듬을 귀로도 읽게 하되 거슬리지 않게(spec §7.5 −18 dB).</summary>
        public const float TickVolumeDb = -18f;
        public const float WhooshVolumeDb = -10f;
        public const float ThunkVolumeDb = -8f;
        public const float SlapVolumeDb = -6f;
        public const float MissVolumeDb = -2f;   //  돌다리 풍덩
        public const float FanfareVolumeDb = -3f;
        public const float RaceUiVolumeDb = 0f;

        /// <summary>
        /// 철썩 뒤 "뾰잉"을 멈춤의 이 비율에 낸다 — 몸개그가 벌떡 일어서는 순간(<see cref="CardRushGag"/> Slap 0.75).
        /// 철썩 소리 자체가 던짐 + <see cref="CardRushThrowFx.SlapTick"/>에 나므로 클립 안에선 그만큼 당겨 둔다.
        /// </summary>
        public const float SlapStandUpAt = 0.75f;

        /// <summary>클립 안 뾰잉 시각(초) — 철썩이 난 뒤 벌떡 일어서기까지.</summary>
        public static float SlapStandUpDelay(float stunSeconds)
            => Mathf.Max(0f, stunSeconds * SlapStandUpAt - CardRushThrowFx.SlapTick * CardRushTiming.TickInterval);

        public static float Gain(float db) => Mathf.Pow(10f, db / 20f);

        private readonly CardRushWorld world;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly IGameDataStore gameDataStore;
        private readonly EntityRenderClock renderClock;
        private readonly PlayerInputManager input;
        private readonly Dictionary<CardRushSfx, AudioClip> clips = new Dictionary<CardRushSfx, AudioClip>();
        private readonly Dictionary<FlappySfx, AudioClip> raceClips = new Dictionary<FlappySfx, AudioClip>();
        private readonly List<AudioClip> synthesized = new List<AudioClip>();
        private readonly List<CardRushSfx> cues = new List<CardRushSfx>();
        private GameObject root;
        private AudioSource source;
        private bool disposed;
        private CardRushAudioFrame last;
        private CardRushAudioMemory cueMemory;
        private string lastEntityId;

        //  시험 이음매 — 실제로는 AudioSource.PlayOneShot(클립, 크기). 시험은 무엇이 어떤 크기로 불렸는지만 본다.
        internal Action<AudioClip, float> playOneShot;

        public CardRushAudio(CardRushWorld world, GameFramework.World.EntityRegistry entityRegistry,
                             IGameDataStore gameDataStore, EntityRenderClock renderClock, PlayerInputManager input)
        {
            this.input = input;
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
                case CardRushSfx.Whoosh:
                    //  카드가 손을 떠나는 휙 — 걸러 낸 잡음의 대역 가운데를 0.12초에 1.2 → 3 kHz로 훑는다. 잡음 겹은 필터가 고정이라
                    //  가운데를 등비로 올린 짧은 겹 넷을 겹쳐 훑기를 만든다(연속이 길어져도 같은 소리).
                    return WhooshSweep(1200f, 3000f, 0.12f, 4);
                case CardRushSfx.Thunk:
                    //  문에 꽂히는 탁 — 짧은 사인 220 Hz + 잡음 파열 0.06초(마른 나무).
                    return new[]
                    {
                        FlappySynth.Part.Tone(220f, 0.06f, FlappySynth.Wave.Sine, 0.8f, 0f, 0.7f),
                        FlappySynth.Part.Noise(0.06f, 0.6f, 1400f, 1.0f),
                    };
                case CardRushSfx.Splash:
                    //  걸러 낸 잡음 0.35초 — 낮은 첨벙 + 위에 흩어지는 물방울.
                    return new[]
                    {
                        FlappySynth.Part.Noise(0.35f, 1.0f, 700f, 0.6f),
                        FlappySynth.Part.Noise(0.28f, 0.45f, 2600f, 0.9f, 0.03f),
                    };
                case CardRushSfx.Slap:
                {
                    //  얼굴에 철썩 — 넓은 잡음 파열 0.08초(낮은 Q, 빠른 감쇠: 짧은 앞 겹이 손바닥 터짐), 그리고 일어설 때
                    //  용수철 미끄럼음(뾰잉 — 위로 미끄러지는 세모파, 멈춤의 0.75).
                    float standUp = SlapStandUpDelay(stunSeconds);
                    return new[]
                    {
                        FlappySynth.Part.Noise(0.08f, 0.9f, 2200f, 0.35f),
                        FlappySynth.Part.Noise(0.025f, 0.8f, 3500f, 0.5f),
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

        /// <summary>대역 가운데를 from → to(등비)로 훑는 잡음 — 겹 count개가 duration을 나눠 조금씩 겹친다.</summary>
        internal static FlappySynth.Part[] WhooshSweep(float from, float to, float duration, int count)
        {
            var parts = new FlappySynth.Part[count];
            float slot = duration / count;
            for (int i = 0; i < count; i++)
            {
                float f = from * Mathf.Pow(to / from, count == 1 ? 0f : i / (float)(count - 1));
                //  가운데 겹이 가장 크다 — 휙 하고 부풀었다 사라진다.
                float v = 0.5f + 0.4f * Mathf.Sin(Mathf.PI * (i + 0.5f) / count);
                parts[i] = FlappySynth.Part.Noise(slot * 1.6f, v, f, 1.2f, i * slot);
            }
            return parts;
        }

        internal static float VolumeOf(CardRushSfx sfx) => sfx switch
        {
            CardRushSfx.CardTick => Gain(TickVolumeDb),
            CardRushSfx.Whoosh => Gain(WhooshVolumeDb),
            CardRushSfx.Thunk => Gain(ThunkVolumeDb),
            CardRushSfx.Splash => Gain(MissVolumeDb),
            CardRushSfx.Slap => Gain(SlapVolumeDb),
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

        public void PostLateTick()
        {
            string id = gameDataStore?.userEntityId;
            if (id != lastEntityId)
            {
                //  몸이 바뀌면(재접속) 기준을 새로 잡는다.
                lastEntityId = id;
                last = default;
                cueMemory = default;
            }
            Observe(ReadFrame(id));
        }

        /// <summary>새 장면을 보고 바뀐 만큼 소리를 낸다. 시험은 이것으로 장면을 직접 넣는다.</summary>
        internal void Observe(in CardRushAudioFrame frame)
        {
            cues.Clear();
            CardRushAudioCues.Detect(last, frame, world != null && world.Rules != null ? world.Rules.Theme : CardRushTheme.StoneBridge, cues, ref cueMemory);
            last = frame;
            foreach (var sfx in cues)
            {
                Play(sfx);
            }
        }

        /// <summary>내 몸 한 장면. 시험은 이것으로 째깍의 틱 기준을 본다.</summary>
        internal CardRushAudioFrame ReadFrame(string id)
        {
            var runner = string.IsNullOrEmpty(id) ? null : entityRegistry.Get(id)?.Get<CardRushRunner>();
            var course = world.Course;
            if (runner == null || course == null)
            {
                return default;
            }
            var s = runner.State;
            long drawnTick = renderClock.TickFor(id);
            //  카드 색은 머리 위 카드와 같은 틱(PlayerInputManager.NextInputTick)으로 본다 — 본 색과 들은 째깍이 같은 순간.
            //  러너 틱으로 보면 이번 틱 입력을 단 뒤엔 머리 위 카드보다 한 틱 늦게 째깍인다.
            long cardTick = input != null ? input.NextInputTick : drawnTick;
            var card = CardRushHeadCard.Compute(s, cardTick, world.GameplayStartTick, course, world.Timing);
            return new CardRushAudioFrame
            {
                Has = true,
                Step = s.Step,
                MoveStartTick = s.MoveStartTick,
                StunEndTick = s.StunEndTick,
                StunTicks = world.Timing.StunTicks,
                MoveTicks = world.Timing.MoveTicks,
                DrawnTick = drawnTick,
                Finished = s.IsFinished(course.StepCount),
                FaceUp = card.Visible && card.FaceUp,
                Color = card.Color,
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
