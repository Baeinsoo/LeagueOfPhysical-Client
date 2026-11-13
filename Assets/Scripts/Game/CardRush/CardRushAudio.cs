using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>카드 러시 소리(4차 spec §9). 결과마다 하나, 내 몸만.</summary>
    public enum CardRushSfx { CardTick, Grab, Throw, Smash, Whiff, Jump, Land, Splash, Expire, Dash, Lift, Tumble, Squash, ZoneOn, Fanfare }

    /// <summary>소리를 정할 때 보는 내 몸 한 장면. <see cref="Has"/>가 거짓이면 몸·코스가 아직 없다.</summary>
    public struct CardRushAudioFrame
    {
        public bool Has;
        public int ObstacleIndex;       // 상태 그대로 — 결과 칸은 그 앞 장애물(번호 − 1)의 결과다
        public int ZoneIndex;           // 상태 그대로 — 결과 칸은 그 앞 구역(번호 − 1), 켜짐(ZoneTick)은 이 구역
        public int ObstacleEvent;
        public long ObstacleEventTick;
        public long WhiffTick;
        public int ZoneEvent;
        public long ZoneEventTick;
        public long ZoneTick;
        public long DashStartTick;
        public long LockTick;
        public long ExpireTick;
        public long JumpStartTick;
        public long ShotTick;
        public long FinishTick;
        public bool FaceUp;
        public CardColor Color;

        public static CardRushAudioFrame Of(in CardRushRunnerState s, in CardRushHeadCardFace card) => new CardRushAudioFrame
        {
            Has = true,
            ObstacleIndex = s.ObstacleIndex,
            ZoneIndex = s.ZoneIndex,
            ObstacleEvent = s.ObstacleEvent,
            ObstacleEventTick = s.ObstacleEventTick,
            WhiffTick = s.WhiffTick,
            ZoneEvent = s.ZoneEvent,
            ZoneEventTick = s.ZoneEventTick,
            ZoneTick = s.ZoneTick,
            DashStartTick = s.DashStartTick,
            LockTick = s.LockTick,
            ExpireTick = s.ExpireTick,
            JumpStartTick = s.JumpStartTick,
            ShotTick = s.ShotTick,
            FinishTick = s.FinishTick,
            FaceUp = card.Visible && card.FaceUp,
            Color = card.Color,
        };
    }

    /// <summary>
    /// 마지막으로 울린 결과 — 장애물·구역은 (번호, 종류), 나머지는 틱. 되감기는 같은 결과를 다시 만들고, 그 사이 장면이 옛 결과로
    /// 잠깐 돌아갈 수도 있다. 보정은 같은 장애물의 결과 틱을 늦게 옮기기도 한다(늦게 닿은 탭을 서버가 버림). 그래서 장애물·구역은
    /// 틱이 아니라 번호로 거른다 — 더 뒤 번호이거나, 같은 번호인데 결과 종류가 바뀐 것만 새것이다(최종 리뷰 m2).
    /// </summary>
    public struct CardRushAudioMemory
    {
        public int ObstacleIndex;       // 마지막으로 울린 장애물 번호(결과가 난 것)
        public int ObstacleEvent;
        public long WhiffTick;
        public int ZoneIndex;           // 마지막으로 결과를 울린 구역 번호
        public int ZoneEvent;
        public int ZoneOnIndex;         // 마지막으로 켜짐을 울린 구역 번호
        public long DashTick;
        public long LockTick;
        public long ExpireTick;
        public long JumpTick;
        public long ShotTick;
        public long LandedJumpTick;     // 마지막으로 내려앉음을 울린 점프(시작 틱)
        public long FellTick;           // 마지막으로 빠짐(풍덩)을 울린 틱
        public long FinishTick;
    }

    public static class CardRushAudioCues
    {
        /// <summary>
        /// 같은 사건을 보정이 몇 틱 옮겨도 다시 안 울리는 틈. 잡기·던지기·점프·사라짐·빠짐은 다음 같은 사건까지 최소 20틱(쿨다운 0.4초,
        /// 빠짐은 기절 0.8초 + 1.5 m 되돌아옴)이라 10틱 안의 이동은 같은 사건이다.
        /// </summary>
        public const int SameEventGapTicks = 10;

        /// <summary>장애물·구역 하나에 결과 소리 하나. 바뀐 장면에서 한 번, 되감아 같은 결과를 다시 봐도·결과 틱만 옮겨도 조용하다.</summary>
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
            if (ev == CardRushObstacleEvent.Fell)
            {
                //  빠짐은 번호가 안 올라 같은 장애물에 여러 번 날 수 있다 — 틱으로 거른다.
                if (now.ObstacleEventTick >= memory.FellTick + SameEventGapTicks)
                {
                    memory.FellTick = now.ObstacleEventTick;
                    into.Add(CardRushSfx.Splash);
                }
            }
            else
            {
                int cleared = now.ObstacleIndex - 1;   //  결과 칸은 늘 바로 앞에 치운 장애물의 것
                if (ev != CardRushObstacleEvent.None && Fresh(memory.ObstacleIndex, memory.ObstacleEvent, cleared, now.ObstacleEvent))
                {
                    memory.ObstacleIndex = cleared;
                    memory.ObstacleEvent = now.ObstacleEvent;
                    //  넘음(Jumped)은 점프 소리가 이미 났다 — 결과 소리는 없다.
                    switch (ev)
                    {
                        case CardRushObstacleEvent.Smashed: into.Add(CardRushSfx.Smash); break;
                        case CardRushObstacleEvent.Lifted: into.Add(CardRushSfx.Lift); break;
                        case CardRushObstacleEvent.Tumbled: into.Add(CardRushSfx.Tumble); break;
                    }
                }
            }

            //  헛방은 결과 칸과 따로 — 올라감 직후 헛방이 그 결과를 다시 울리지 않는다.
            if (now.WhiffTick != 0 && now.WhiffTick > memory.WhiffTick)
            {
                memory.WhiffTick = now.WhiffTick;
                into.Add(CardRushSfx.Whiff);
            }

            var zone = (CardRushZoneEvent)now.ZoneEvent;
            int resolved = now.ZoneIndex - 1;
            if (zone != CardRushZoneEvent.None && Fresh(memory.ZoneIndex, memory.ZoneEvent, resolved, now.ZoneEvent))
            {
                memory.ZoneIndex = resolved;
                memory.ZoneEvent = now.ZoneEvent;
                //  빠져나간 것은 조용히 — 피한 것까지 울리면 구역마다 소리가 두 번이다(켜짐 + 결과).
                if (zone == CardRushZoneEvent.Hit) into.Add(CardRushSfx.Squash);
            }

            //  켜짐은 구역마다 한 번 — 보정이 트리거 밟은 틱을 옮겨도 같은 구역이면 조용하다.
            if (now.ZoneTick != 0 && now.ZoneIndex > memory.ZoneOnIndex)
            {
                memory.ZoneOnIndex = now.ZoneIndex;
                into.Add(CardRushSfx.ZoneOn);
            }

            //  대시는 쓴 틱이 곧 결과 틱이다.
            if (now.DashStartTick != 0 && now.DashStartTick > memory.DashTick)
            {
                memory.DashTick = now.DashStartTick;
                into.Add(CardRushSfx.Dash);
            }

            //  잡기·던지기·점프·사라짐은 상태가 그 틱을 그대로 갖는다 — 새 틱이면 한 번.
            if (Fresh(now.LockTick, memory.LockTick))
            {
                memory.LockTick = now.LockTick;
                into.Add(CardRushSfx.Grab);
            }
            if (Fresh(now.ShotTick, memory.ShotTick))
            {
                memory.ShotTick = now.ShotTick;
                into.Add(CardRushSfx.Throw);
            }
            if (Fresh(now.JumpStartTick, memory.JumpTick))
            {
                memory.JumpTick = now.JumpStartTick;
                into.Add(CardRushSfx.Jump);
            }
            //  내려앉음 — 점프 틱이 지워지는 순간(부딪혀 내려앉아도·밀려도). 같은 점프는 되감아도 한 번.
            if (prev.JumpStartTick != 0 && now.JumpStartTick == 0 && prev.JumpStartTick >= memory.LandedJumpTick + SameEventGapTicks)
            {
                memory.LandedJumpTick = prev.JumpStartTick;
                into.Add(CardRushSfx.Land);
            }
            if (Fresh(now.ExpireTick, memory.ExpireTick))
            {
                memory.ExpireTick = now.ExpireTick;
                into.Add(CardRushSfx.Expire);
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

        private static bool Fresh(long tick, long last) => tick != 0 && tick >= last + SameEventGapTicks;

        //  뒤 번호면 새 결과. 같은 번호면 종류가 달라야(보정이 결과를 바꿈) 새것. 앞 번호는 되감기 중 옛 장면이다.
        private static bool Fresh(int lastIndex, int lastKind, int index, int kind)
            => index > lastIndex || (index == lastIndex && kind != lastKind);

        private static CardRushAudioMemory Baseline(in CardRushAudioFrame now) => new CardRushAudioMemory
        {
            ObstacleIndex = now.ObstacleIndex - 1,
            ObstacleEvent = now.ObstacleEvent == (int)CardRushObstacleEvent.Fell ? 0 : now.ObstacleEvent,
            WhiffTick = now.WhiffTick,
            ZoneIndex = now.ZoneIndex - 1,
            ZoneEvent = now.ZoneEvent,
            //  켜진 채로 들어왔으면 이 구역은 이미 울린 셈 — 아니면 아직(앞 번호).
            ZoneOnIndex = now.ZoneTick != 0 ? now.ZoneIndex : now.ZoneIndex - 1,
            DashTick = now.DashStartTick,
            LockTick = now.LockTick,
            ExpireTick = now.ExpireTick,
            JumpTick = now.JumpStartTick,
            ShotTick = now.ShotTick,
            LandedJumpTick = now.JumpStartTick,
            FellTick = now.ObstacleEvent == (int)CardRushObstacleEvent.Fell ? now.ObstacleEventTick : 0,
            FinishTick = now.FinishTick,
        };
    }

    /// <summary>
    /// 카드 러시 소리 한 곳(4차 spec §9). 내 몸 상태의 결과(와장창·꽈당·풍덩…)만 듣는다 — 남의 소리는 내지 않는다(8명이면 시끄럽다).
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
        public const float SmallVolumeDb = -12f;   //  잡기·던지기·헛방·점프·내려앉음·사라짐·대시·구역 켜짐 — 내가 한 일의 짧은 확인음
        public const float LiftVolumeDb = -10f;
        public const float SplashVolumeDb = -6f;
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
                case CardRushSfx.Grab:
                    //  짧은 딸깍 둘 — 카드를 집는 소리(째깍보다 한 칸 낮고 길다).
                    return new[]
                    {
                        FlappySynth.Part.Noise(0.03f, 0.8f, 3000f, 1.2f),
                        FlappySynth.Part.Tone(1500f, 0.04f, FlappySynth.Wave.Sine, 0.4f, 0.015f),
                    };
                case CardRushSfx.Throw:
                    //  휙 — 위로 올라가는 걸러 낸 잡음.
                    return new[]
                    {
                        FlappySynth.Part.Noise(0.09f, 0.6f, 1500f, 1.0f),
                        FlappySynth.Part.Noise(0.06f, 0.5f, 3000f, 1.0f, 0.04f),
                    };
                case CardRushSfx.Whiff:
                    //  퐁 — 힘 빠진 짧은 내림음.
                    return new[] { FlappySynth.Part.Tone(520f, 0.12f, FlappySynth.Wave.Sine, 0.5f, 0f, 0.5f) };
                case CardRushSfx.Jump:
                    //  뿅 — 위로 튀는 짧은 음.
                    return new[] { FlappySynth.Part.Tone(380f, 0.14f, FlappySynth.Wave.Sine, 0.55f, 0f, 2.2f) };
                case CardRushSfx.Land:
                    //  톡 — 짧고 낮은 닿는 소리.
                    return new[]
                    {
                        FlappySynth.Part.Tone(180f, 0.06f, FlappySynth.Wave.Sine, 0.7f, 0f, 0.7f),
                        FlappySynth.Part.Noise(0.03f, 0.4f, 500f, 0.8f),
                    };
                case CardRushSfx.Splash:
                    //  풍덩 — 낮은 첨벙 + 물보라 잡음.
                    return new[]
                    {
                        FlappySynth.Part.Tone(150f, 0.22f, FlappySynth.Wave.Sine, 0.9f, 0f, 0.4f),
                        FlappySynth.Part.Noise(0.2f, 0.7f, 600f, 0.8f, 0.02f, FlappySynth.Filter.LowPass),
                    };
                case CardRushSfx.Expire:
                    //  피식 — 바람 빠지는 짧은 잡음.
                    return new[] { FlappySynth.Part.Noise(0.14f, 0.5f, 2400f, 1.2f) };
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
            CardRushSfx.Grab or CardRushSfx.Throw or CardRushSfx.Whiff or CardRushSfx.Jump or CardRushSfx.Land
                or CardRushSfx.Expire or CardRushSfx.Dash or CardRushSfx.ZoneOn => Gain(SmallVolumeDb),
            CardRushSfx.Lift => Gain(LiftVolumeDb),
            CardRushSfx.Splash => Gain(SplashVolumeDb),
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
