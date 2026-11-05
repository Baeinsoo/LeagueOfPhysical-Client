using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>카드 러시 소리(spec 2026-11-05 §11.5). 결과마다 하나, 내 몸만.</summary>
    public enum CardRushSfx { CardTick, Smash, Whiff, Shield, Dash, Lift, Clang, Tumble, Squash, ZoneOn, Fanfare }

    /// <summary>소리를 정할 때 보는 내 몸 한 장면. <see cref="Has"/>가 거짓이면 몸·코스가 아직 없다.</summary>
    public struct CardRushAudioFrame
    {
        public bool Has;
        public int ObstacleEvent;
        public long ObstacleEventTick;
        public int ZoneEvent;
        public long ZoneEventTick;
        public long ZoneTick;
        public long BlockEndTick;
        public long DashStartTick;
        public long FinishTick;
        public bool FaceUp;
        public CardColor Color;

        public static CardRushAudioFrame Of(in CardRushRunnerState s, in CardRushHeadCardFace card) => new CardRushAudioFrame
        {
            Has = true,
            ObstacleEvent = s.ObstacleEvent,
            ObstacleEventTick = s.ObstacleEventTick,
            ZoneEvent = s.ZoneEvent,
            ZoneEventTick = s.ZoneEventTick,
            ZoneTick = s.ZoneTick,
            BlockEndTick = s.BlockEndTick,
            DashStartTick = s.DashStartTick,
            FinishTick = s.FinishTick,
            FaceUp = card.Visible && card.FaceUp,
            Color = card.Color,
        };
    }

    /// <summary>
    /// 마지막으로 울린 결과의 (종류, 틱) — 갈래마다 하나. 되감기는 같은 결과를 같은 틱에 다시 만들고, 그 사이 장면이 옛 결과로
    /// 잠깐 돌아갈 수도 있다. 울린 틱보다 늦은 결과(또는 같은 틱 다른 결과)만 새것이다(2차 m2 교훈 — (결과, 결과 틱)으로 거른다).
    /// </summary>
    public struct CardRushAudioMemory
    {
        public int ObstacleEvent;
        public long ObstacleTick;
        public int ZoneEvent;
        public long ZoneEventTick;
        public long ZoneOnTick;
        public long ShieldTick;
        public long DashTick;
        public long FinishTick;
    }

    public static class CardRushAudioCues
    {
        /// <summary>결과(종류, 틱)마다 소리 하나. 바뀐 장면에서 한 번, 되감아 같은 결과를 다시 봐도 조용하다.</summary>
        public static void Detect(in CardRushAudioFrame prev, in CardRushAudioFrame now, List<CardRushSfx> into, ref CardRushAudioMemory memory)
        {
            if (now.Has == false)
            {
                return;
            }
            //  첫 장면은 기준만 잡는다 — 들어오자마자(재접속 등) 지난 일로 소리를 내지 않게.
            if (prev.Has == false)
            {
                memory = Baseline(now);
                return;
            }

            var ev = (CardRushObstacleEvent)now.ObstacleEvent;
            if (ev != CardRushObstacleEvent.None && Fresh(memory.ObstacleEvent, memory.ObstacleTick, now.ObstacleEvent, now.ObstacleEventTick))
            {
                memory.ObstacleEvent = now.ObstacleEvent;
                memory.ObstacleTick = now.ObstacleEventTick;
                into.Add(ev switch
                {
                    CardRushObstacleEvent.Smashed => CardRushSfx.Smash,
                    CardRushObstacleEvent.Whiff => CardRushSfx.Whiff,
                    CardRushObstacleEvent.Lifted => CardRushSfx.Lift,
                    CardRushObstacleEvent.Blocked => CardRushSfx.Clang,
                    _ => CardRushSfx.Tumble,
                });
            }

            var zone = (CardRushZoneEvent)now.ZoneEvent;
            if (zone != CardRushZoneEvent.None && Fresh(memory.ZoneEvent, memory.ZoneEventTick, now.ZoneEvent, now.ZoneEventTick))
            {
                memory.ZoneEvent = now.ZoneEvent;
                memory.ZoneEventTick = now.ZoneEventTick;
                //  빠져나간 것은 조용히 — 피한 것까지 울리면 구역마다 소리가 두 번이다(켜짐 + 결과).
                if (zone == CardRushZoneEvent.Held) into.Add(CardRushSfx.Clang);
                else if (zone == CardRushZoneEvent.Hit) into.Add(CardRushSfx.Squash);
            }

            if (now.ZoneTick != 0 && now.ZoneTick > memory.ZoneOnTick)
            {
                memory.ZoneOnTick = now.ZoneTick;
                into.Add(CardRushSfx.ZoneOn);
            }

            //  방패·대시는 쓴 틱이 곧 결과 틱이다(방패 = 끝 틱 − 50).
            if (now.BlockEndTick != 0 && now.BlockEndTick > memory.ShieldTick)
            {
                memory.ShieldTick = now.BlockEndTick;
                into.Add(CardRushSfx.Shield);
            }
            if (now.DashStartTick != 0 && now.DashStartTick > memory.DashTick)
            {
                memory.DashTick = now.DashStartTick;
                into.Add(CardRushSfx.Dash);
            }

            //  완주는 한 번뿐이다 — 보정이 결승 틱을 옮겨도 다시 울리지 않는다.
            if (now.FinishTick != 0 && memory.FinishTick == 0)
            {
                memory.FinishTick = now.FinishTick;
                into.Add(CardRushSfx.Fanfare);
            }

            //  머리 위 카드가 넘어가는 조건과 같다: 새로 앞면이 되거나 색이 바뀌면.
            if (now.FaceUp && (prev.FaceUp == false || now.Color != prev.Color))
            {
                into.Add(CardRushSfx.CardTick);
            }
        }

        //  늦은 틱이면 새 결과. 같은 틱이면 종류가 달라야(보정이 결과를 바꿈) 새것. 이른 틱은 되감기 중 옛 장면이다.
        private static bool Fresh(int lastKind, long lastTick, int kind, long tick)
            => tick > lastTick || (tick == lastTick && kind != lastKind);

        private static CardRushAudioMemory Baseline(in CardRushAudioFrame now) => new CardRushAudioMemory
        {
            ObstacleEvent = now.ObstacleEvent,
            ObstacleTick = now.ObstacleEventTick,
            ZoneEvent = now.ZoneEvent,
            ZoneEventTick = now.ZoneEventTick,
            ZoneOnTick = now.ZoneTick,
            ShieldTick = now.BlockEndTick,
            DashTick = now.DashStartTick,
            FinishTick = now.FinishTick,
        };
    }

    /// <summary>
    /// 카드 러시 소리 한 곳(spec 2026-11-05 §11.5). 내 몸 상태의 결과(와장창·꽈당·방패…)만 듣는다 — 남의 소리는 내지 않는다(8명이면 시끄럽다).
    /// 파일 없이 <see cref="FlappySynth"/>의 발진기·잡음으로 시작할 때 한 번 짓는다.
    /// <para>공용 경기 화면(RaceHud·RaceFinish)이 부르는 <see cref="IFlappyAudio"/> 자리도 이것이다 — 추월·도장 소리는 플래피 클립 그대로.
    /// 단 <see cref="FlappySfx.Finish"/>는 삼킨다: 완주 팡파르는 이 모드의 세 음이 상태에서 이미 냈다(두 번 울리지 않게).</para>
    /// </summary>
    /// <remarks>틱·보간 뒤(IPostLateTickable)에서 듣는다 — 째깍이 머리 위 카드와 같은 순간·같은 틱.</remarks>
    public class CardRushAudio : IFlappyAudio, IInitializable, IPostLateTickable, IDisposable
    {
        /// <summary>카드 째깍 — 아주 작게. 리듬을 귀로도 읽게 하되 거슬리지 않게.</summary>
        public const float TickVolumeDb = -18f;
        public const float SmashVolumeDb = -5f;
        public const float SmallVolumeDb = -12f;   //  헛방·방패·대시·구역 켜짐 — 내가 한 일의 짧은 확인음
        public const float LiftVolumeDb = -10f;
        public const float ClangVolumeDb = -7f;
        public const float HurtVolumeDb = -3f;     //  꽈당·으악 — 몸개그는 크게
        public const float FanfareVolumeDb = -3f;
        public const float RaceUiVolumeDb = 0f;

        public static float Gain(float db) => Mathf.Pow(10f, db / 20f);

        private readonly CardRushWorld world;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly IGameDataStore gameDataStore;
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
                             IGameDataStore gameDataStore, PlayerInputManager input)
        {
            this.input = input;
            this.world = world;
            this.entityRegistry = entityRegistry;
            this.gameDataStore = gameDataStore;
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

            foreach (CardRushSfx sfx in Enum.GetValues(typeof(CardRushSfx)))
            {
                clips[sfx] = Synthesize("CardRushSfx_" + sfx, FlappySynth.Render(Recipe(sfx), 1000 + (int)sfx));
            }
            foreach (FlappySfx sfx in Enum.GetValues(typeof(FlappySfx)))
            {
                raceClips[sfx] = Synthesize("CardRushRace_" + sfx, FlappySynth.Render(sfx));
            }
        }

        /// <summary>소리 조리법(spec §11.5 목록). 크기는 재생할 때 dB 상수로 준다 — 여기 값은 모양만.</summary>
        internal static FlappySynth.Part[] Recipe(CardRushSfx sfx)
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
                case CardRushSfx.Smash:
                    //  나무 깨짐 0.15초 — 마른 잡음 파열 둘(낮은 나무 몸통 + 높은 쪼개짐)과 짧은 둔탁음.
                    return new[]
                    {
                        FlappySynth.Part.Noise(0.15f, 1.0f, 900f, 0.5f),
                        FlappySynth.Part.Noise(0.08f, 0.7f, 2800f, 0.9f, 0.02f),
                        FlappySynth.Part.Tone(160f, 0.08f, FlappySynth.Wave.Triangle, 0.6f, 0f, 0.6f),
                    };
                case CardRushSfx.Whiff:
                    //  가벼운 휙 — 걸러 낸 잡음 0.1초.
                    return new[] { FlappySynth.Part.Noise(0.1f, 0.6f, 1600f, 1.2f) };
                case CardRushSfx.Shield:
                    //  낮은 웅 0.1초.
                    return new[] { FlappySynth.Part.Tone(180f, 0.1f, FlappySynth.Wave.Sine, 0.7f, 0f, 1.3f) };
                case CardRushSfx.Dash:
                    //  짧은 슉 — 대역이 위로 미끄러지는 잡음.
                    return new[]
                    {
                        FlappySynth.Part.Noise(0.06f, 0.6f, 1200f, 1.0f),
                        FlappySynth.Part.Noise(0.07f, 0.7f, 2600f, 1.0f, 0.04f),
                    };
                case CardRushSfx.Lift:
                    //  끼익 — 위로 미끄러지는 가는 톱니파.
                    return new[] { FlappySynth.Part.Tone(520f, 0.22f, FlappySynth.Wave.Saw, 0.25f, 0f, 1.6f) };
                case CardRushSfx.Clang:
                    //  탱 — 쇠 울림(두 음 배음).
                    return new[]
                    {
                        FlappySynth.Part.Tone(880f, 0.25f, FlappySynth.Wave.Triangle, 0.5f),
                        FlappySynth.Part.Tone(1320f, 0.18f, FlappySynth.Wave.Sine, 0.3f),
                    };
                case CardRushSfx.Tumble:
                    //  낮은 쿵 + 용수철(뾰잉) — 기절이 풀리며 벌떡 서는 무렵.
                    return new[]
                    {
                        FlappySynth.Part.Tone(110f, 0.25f, FlappySynth.Wave.Sine, 0.9f, 0f, 0.5f),
                        FlappySynth.Part.Noise(0.12f, 0.6f, 300f, 0.7f),
                        FlappySynth.Part.Tone(260f, 0.22f, FlappySynth.Wave.Triangle, 0.35f, 0.45f, 2.6f),
                    };
                case CardRushSfx.Squash:
                    //  바위 쿵 — 꽈당보다 더 낮고 무겁게.
                    return new[]
                    {
                        FlappySynth.Part.Tone(70f, 0.4f, FlappySynth.Wave.Sine, 1.0f, 0f, 0.6f),
                        FlappySynth.Part.Noise(0.3f, 0.8f, 180f, 0.8f, 0f, FlappySynth.Filter.LowPass),
                    };
                case CardRushSfx.ZoneOn:
                    //  작은 띠링 한 번.
                    return new[]
                    {
                        FlappySynth.Part.Tone(1175f, 0.08f, FlappySynth.Wave.Triangle, 0.4f),
                        FlappySynth.Part.Tone(1568f, 0.12f, FlappySynth.Wave.Triangle, 0.35f, 0.07f),
                    };
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
            CardRushSfx.Smash => Gain(SmashVolumeDb),
            CardRushSfx.Whiff or CardRushSfx.Shield or CardRushSfx.Dash or CardRushSfx.ZoneOn => Gain(SmallVolumeDb),
            CardRushSfx.Lift => Gain(LiftVolumeDb),
            CardRushSfx.Clang => Gain(ClangVolumeDb),
            CardRushSfx.Tumble or CardRushSfx.Squash => Gain(HurtVolumeDb),
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
            CardRushAudioCues.Detect(last, frame, cues, ref cueMemory);
            last = frame;
            foreach (var sfx in cues)
            {
                Play(sfx);
            }
        }

        /// <summary>내 몸 한 장면. 카드 색은 머리 위 카드와 같은 틱(다음 입력 틱)으로 본다 — 본 색과 들은 째깍이 같은 순간.</summary>
        internal CardRushAudioFrame ReadFrame(string id)
        {
            var runner = string.IsNullOrEmpty(id) ? null : entityRegistry.Get(id)?.Get<CardRushRunner>();
            var track = world?.Track;
            if (runner == null || track == null || input == null)
            {
                return default;
            }
            var card = CardRushHeadCard.Compute(runner.State, input.NextInputTick, world.GameplayStartTick, runner.Lane, track);
            return CardRushAudioFrame.Of(runner.State, card);
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
