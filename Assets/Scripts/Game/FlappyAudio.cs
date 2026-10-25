using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 경기 소리 한 곳(spec §6): 효과음(<see cref="Play"/>), 배경음(시작·정지·마지막 구간), 음악 잠깐 낮추기.
    /// 클립은 시작할 때 한 번 짓는다(<see cref="FlappyDiveFx"/>의 충전음과 같은 런타임 <c>AudioClip.Create</c>) —
    /// <see cref="FlappySoundBank"/> 칸에 파일이 꽂혀 있으면 그걸 쓴다.
    /// <para>2D 소리(spatialBlend 0)다. 마스터 음량·음소거는 기기 음량을 따른다(spec §6 — 슬라이더는 이번에 안 만든다).
    /// 다이브 충전음·알갱이 소리는 아직 각자 AudioSource를 쓴다(옮기는 건 다음).</para>
    /// </summary>
    public class FlappyAudio : IFlappyAudio, IInitializable, ITickable, IDisposable
    {
        /// <summary>쿵 소리 때 음악을 이만큼으로 낮췄다가(시제품 duck) …</summary>
        internal const float DuckLevel = 0.35f;

        /// <summary>… 이 시간에 걸쳐 곧게 되돌린다.</summary>
        internal const float DuckRecoverSeconds = 0.6f;

        /// <summary>
        /// 마지막 구간 배경음 재생 속도. 시제품은 템포 1.068배·음높이 1.0595배(반음)를 따로 줬다 — 재생 속도 하나로 하면
        /// 둘이 같이 1.068배(약 1.14반음)가 된다. 파일 배경음에도 그대로 먹는 방법이라 이쪽을 택했다.
        /// </summary>
        internal const float FinalStretchPitch = 1.068f;

        private readonly FlappySoundBank bank;
        private readonly Dictionary<FlappySfx, AudioClip> clips = new Dictionary<FlappySfx, AudioClip>();
        private readonly List<AudioClip> synthesized = new List<AudioClip>();
        private GameObject root;
        private AudioSource sfxSource;
        private AudioSource musicSource;
        private AudioClip musicClip;
        private float duckAge = float.MaxValue;
        //  치운 뒤엔 되살아나지 않는다 — Play·StartMusic·SetFinalStretch가 느슨하게 부르는
        //  Initialize()가 치운 GameObject·클립을 다시 짓지 못하게 막는다.
        private bool disposed;

        //  시험 이음매 — 실제로는 효과음 AudioSource.PlayOneShot. 시험은 무엇이 불렸는지만 본다.
        internal Action<AudioClip> playOneShot;
        internal bool MusicPlaying { get; private set; }
        internal AudioClip MusicClip => musicClip;
        internal float MusicPitch => musicSource != null ? musicSource.pitch : 1f;
        internal float MusicVolume => musicSource != null ? musicSource.volume : 1f;

        public FlappyAudio(FlappySoundBankSlot slot)
        {
            bank = slot?.Bank;
            playOneShot = clip =>
            {
                if (sfxSource != null && clip != null)
                {
                    sfxSource.PlayOneShot(clip);
                }
            };
        }

        /// <summary>경기 스코프가 지어질 때 클립을 다 짓는다 — 첫 쿵에서 짓느라 한 프레임 끊기지 않게.</summary>
        public void Initialize()
        {
            if (disposed || root != null)
            {
                return;
            }
            root = new GameObject("FlappyAudio");
            sfxSource = MakeSource(loop: false);
            musicSource = MakeSource(loop: true);
            foreach (FlappySfx sfx in Enum.GetValues(typeof(FlappySfx)))
            {
                AudioClip fromFile = bank != null ? bank.ClipFor(sfx) : null;
                clips[sfx] = fromFile != null ? fromFile : Synthesize("FlappySfx_" + sfx, FlappySynth.Render(sfx));
            }
            AudioClip musicFile = bank != null ? bank.Music : null;
            musicClip = musicFile != null ? musicFile : Synthesize("FlappyMusic", FlappySynth.MusicLoop());
            musicSource.clip = musicClip;
        }

        internal AudioClip ClipOf(FlappySfx sfx)
        {
            Initialize();
            return clips.TryGetValue(sfx, out var clip) ? clip : null;
        }

        public void Play(FlappySfx sfx)
        {
            if (disposed)
            {
                return;
            }
            playOneShot(ClipOf(sfx));
        }

        public void StartMusic()
        {
            if (disposed)
            {
                return;
            }
            Initialize();
            if (MusicPlaying)
            {
                return;
            }
            MusicPlaying = true;
            musicSource.Play();
        }

        public void StopMusic()
        {
            MusicPlaying = false;
            if (musicSource == null)
            {
                return;
            }
            musicSource.Stop();
            //  다음 판이 빠른 채로 시작하지 않게(같은 스코프에서 다시 트는 일은 지금 없지만 값이 남는 게 함정이다).
            musicSource.pitch = 1f;
        }

        public void SetFinalStretch(bool on)
        {
            if (disposed)
            {
                return;
            }
            Initialize();
            musicSource.pitch = on ? FinalStretchPitch : 1f;
        }

        public void DuckMusic()
        {
            duckAge = 0f;
        }

        public void Tick()
        {
            Tick(Time.deltaTime);
        }

        internal void Tick(float deltaTime)
        {
            if (duckAge < float.MaxValue)
            {
                duckAge += deltaTime;
            }
            if (musicSource != null)
            {
                musicSource.volume = DuckGain(duckAge);
            }
        }

        /// <summary>낮춘 지 age초 뒤 음악 음량(0.35 → 0.6초에 1로 곧게).</summary>
        internal static float DuckGain(float age)
        {
            if (age >= DuckRecoverSeconds)
            {
                return 1f;
            }
            return Mathf.Lerp(DuckLevel, 1f, Mathf.Max(0f, age) / DuckRecoverSeconds);
        }

        private AudioSource MakeSource(bool loop)
        {
            var source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
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
            if (musicSource != null)
            {
                musicSource.Stop();
            }
            MusicPlaying = false;
            DestroyObject(root);
            root = null;
            sfxSource = null;
            musicSource = null;
            //  꽂힌 파일은 에셋이라 지우지 않는다 — 자기가 지은 것만.
            foreach (var clip in synthesized)
            {
                DestroyObject(clip);
            }
            synthesized.Clear();
            clips.Clear();
            musicClip = null;
        }

        //  플레이 중이 아니면(EditMode 시험) Destroy가 안 먹는다. LOP 네임스페이스엔 Application 싱글톤이 있어 UnityEngine을 붙인다.
        private static void DestroyObject(UnityEngine.Object obj)
        {
            if (obj == null)
            {
                return;
            }
            if (UnityEngine.Application.isPlaying) { UnityEngine.Object.Destroy(obj); }
            else { UnityEngine.Object.DestroyImmediate(obj); }
        }
    }
}
