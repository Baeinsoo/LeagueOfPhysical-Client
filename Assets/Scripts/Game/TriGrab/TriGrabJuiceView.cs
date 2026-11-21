using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>삼색 그랩 소리 종류(3차). 파일 없이 <see cref="FlappySynth"/>로 짓는다.</summary>
    public enum TriGrabSfx { Reveal, MyCorrect, MyWrong, OtherCorrect, FinalRound }

    /// <summary>
    /// 잡았을 때의 손맛(3차). 서버가 정한 잡기 결과를 보고 카드 자리에 파편을 튀기고, 내 결과면 소리와 카메라 흔들림을 더한다.
    /// 라운드 시작(카드가 뒤집히는 순간)과 결승 라운드 준비에도 소리를 낸다.
    /// 실제 위치는 건드리지 않는다 — 카메라·입자·소리만 움직인다.
    /// </summary>
    public class TriGrabJuiceView : IStartable, System.IDisposable
    {
        public const float SfxVolume = 0.55f;
        public const float ShakeSeconds = 0.28f;
        public const float WrongShakeMeters = 0.35f;
        public const float CorrectPunchMeters = 0.6f;

        private readonly GameFramework.Runner.IRunner runner;
        private readonly TriGrabClientState state;
        private readonly IPlayerContext playerContext;

        private readonly HashSet<(string owner, long fire)> seenResults = new HashSet<(string, long)>();
        private readonly Dictionary<TriGrabSfx, AudioClip> clips = new Dictionary<TriGrabSfx, AudioClip>();
        private GameObject root;
        private AudioSource source;
        private ParticleSystem burst;
        private Material burstMaterial;
        private KnockbackLateUpdateDriver driver;
        private bool first = true;
        private int revealedRound, finalAnnouncedRound;

        private float shakeAt = -100f;
        private float shakeMeters;
        private bool shakePunch;
        private Vector3 shakeDir;
        private Vector3 cameraBase;
        private Vector3 lastApplied;
        private bool shaking;

        public TriGrabJuiceView(GameFramework.Runner.IRunner runner, TriGrabClientState state, IPlayerContext playerContext)
        {
            this.runner = runner;
            this.state = state;
            this.playerContext = playerContext;
        }

        public void Start()
        {
            root = new GameObject("TriGrabJuice");
            source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            foreach (TriGrabSfx sfx in System.Enum.GetValues(typeof(TriGrabSfx)))
            {
                var data = FlappySynth.Render(Recipe(sfx), 2000 + (int)sfx);
                var clip = AudioClip.Create("TriGrabSfx_" + sfx, data.Length, 1, FlappySynth.Rate, false);
                clip.SetData(data, 0);
                clips[sfx] = clip;
            }
            burst = MakeBurst(root.transform);
            driver = KnockbackLateUpdateDriver.Create("TriGrabJuiceDriver", OnLateUpdate);
        }

        /// <summary>소리 조리법. 정답은 위로 오르는 두 음, 오답은 낮게 긁히는 소리.</summary>
        internal static FlappySynth.Part[] Recipe(TriGrabSfx sfx)
        {
            switch (sfx)
            {
                case TriGrabSfx.Reveal:
                    return new[]
                    {
                        FlappySynth.Part.Noise(0.12f, 0.6f, 2600f, 0.8f),
                        FlappySynth.Part.Tone(660f, 0.08f, FlappySynth.Wave.Triangle, 0.35f, 0.02f),
                    };
                case TriGrabSfx.MyCorrect:
                    return new[]
                    {
                        FlappySynth.Part.Noise(0.03f, 0.8f, 3400f, 1.2f),
                        FlappySynth.Part.Tone(880f, 0.09f, FlappySynth.Wave.Triangle, 0.55f, 0.02f),
                        FlappySynth.Part.Tone(1319f, 0.22f, FlappySynth.Wave.Triangle, 0.5f, 0.09f),
                    };
                case TriGrabSfx.MyWrong:
                    return new[]
                    {
                        FlappySynth.Part.Tone(180f, 0.28f, FlappySynth.Wave.Saw, 0.45f, 0f, 0.6f),
                        FlappySynth.Part.Noise(0.18f, 0.4f, 700f, 0.9f, 0.02f, FlappySynth.Filter.LowPass),
                    };
                case TriGrabSfx.OtherCorrect:
                    return new[] { FlappySynth.Part.Tone(1046f, 0.08f, FlappySynth.Wave.Sine, 0.25f) };
                default:
                    //  결승 — 짧은 세 음(솔·도·미, 마지막 길게).
                    return new[]
                    {
                        FlappySynth.Part.Tone(784f, 0.1f, FlappySynth.Wave.Triangle, 0.5f, 0f),
                        FlappySynth.Part.Tone(1046f, 0.1f, FlappySynth.Wave.Triangle, 0.5f, 0.1f),
                        FlappySynth.Part.Tone(1319f, 0.35f, FlappySynth.Wave.Triangle, 0.5f, 0.2f),
                    };
            }
        }

        private void OnLateUpdate()
        {
            double renderTick = TriGrabCarouselView.RenderTickOf(runner);
            if (double.IsNaN(renderTick) || !state.Received) return;
            long tick = (long)System.Math.Floor(renderTick);

            WatchRound(tick);
            WatchResults(renderTick);
            ApplyShake();
            first = false;
        }

        private void WatchRound(long tick)
        {
            var phase = state.PhaseAt(tick);
            if (phase == TriGrabPhase.Prep && TriGrabConfig.IsFinalRound(state.Round) && finalAnnouncedRound != state.Round)
            {
                finalAnnouncedRound = state.Round;
                if (!first) Play(TriGrabSfx.FinalRound);
            }
            if (phase == TriGrabPhase.Live && revealedRound != state.Round)
            {
                revealedRound = state.Round;
                //  카드가 뒤집히는 순간에만 — 늦게 들어와 이미 한참 돈 라운드는 조용히.
                if (!first && tick - state.Clock.LiveStartTick < 10) Play(TriGrabSfx.Reveal);
            }
        }

        /// <summary>새로 정해진 잡기 결과. 처음 받은 상태에 이미 있던 결과(다시 접속 등)는 연출하지 않는다.</summary>
        /// <remarks>서버 소식은 왕복만큼 늦게 오므로(렌더 틱은 이미 닿은 틱을 지나 있다) 받는 즉시 터뜨린다.</remarks>
        public static bool IsNewResult(TriGrabHook hook, HashSet<(string, long)> seen)
        {
            if (hook == null || hook.Result == TriGrabHookResult.None) return false;
            return seen.Add((hook.OwnerId, hook.FireTick));
        }

        private void WatchResults(double renderTick)
        {
            string me = playerContext.entityId;
            foreach (var hook in state.Hooks)
            {
                if (!IsNewResult(hook, seenResults) || first) continue;
                bool correct = hook.Result == TriGrabHookResult.Correct;
                var at = hook.TipAt(hook.TurnTick);
                Burst(new Vector3(at.x, TriGrabHookView.HookHeight, at.y), correct, hook.OwnerId == me);
                if (hook.OwnerId == me)
                {
                    Play(correct ? TriGrabSfx.MyCorrect : TriGrabSfx.MyWrong);
                    StartShake(correct, new Vector3(hook.Dir.x, 0f, hook.Dir.y));
                }
                else if (correct)
                {
                    Play(TriGrabSfx.OtherCorrect);
                }
            }
            //  지나간 라운드의 기억은 버린다(라운드마다 갈고리는 많아야 수십 개).
            if (seenResults.Count > 256) seenResults.Clear();
        }

        private void Play(TriGrabSfx sfx)
        {
            if (source != null && clips.TryGetValue(sfx, out var clip)) source.PlayOneShot(clip, SfxVolume);
        }

        private void Burst(Vector3 at, bool correct, bool mine)
        {
            if (burst == null) return;
            int count = mine ? 40 : 22;
            var colors = correct
                ? new[] { TriGrabPalette.Correct, new Color(1f, 0.95f, 0.55f), Color.white }
                : new[] { TriGrabPalette.Wrong, new Color(0.35f, 0.35f, 0.4f) };
            for (int i = 0; i < count; i++)
            {
                var dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.8f + 0.2f;
                var p = new ParticleSystem.EmitParams
                {
                    position = at,
                    velocity = dir * Random.Range(correct ? 3.5f : 2f, correct ? 7f : 4f),
                    startColor = colors[i % colors.Length],
                    startSize = Random.Range(0.25f, mine ? 0.6f : 0.42f),
                    startLifetime = Random.Range(0.35f, 0.7f),
                };
                burst.Emit(p, 1);
            }
        }

        private void StartShake(bool punch, Vector3 dir)
        {
            shakeAt = Time.unscaledTime;
            shakePunch = punch;
            shakeMeters = punch ? CorrectPunchMeters : WrongShakeMeters;
            shakeDir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward;
        }

        /// <summary>
        /// 흔들림 몫(카메라 위치에 더할 값). 정답은 쏜 쪽으로 한 번 쑥 밀었다 돌아오고, 오답은 좌우로 덜덜 떤다. 끝나면 0.
        /// </summary>
        public static Vector3 ShakeOffset(float elapsed, bool punch, float meters, Vector3 dir)
        {
            if (elapsed < 0f || elapsed >= ShakeSeconds) return Vector3.zero;
            float k = 1f - elapsed / ShakeSeconds;
            if (punch) return dir * (meters * Mathf.Sin(Mathf.PI * elapsed / ShakeSeconds) * k);
            var side = Vector3.Cross(Vector3.up, dir).normalized;
            return side * (meters * k * Mathf.Sin(elapsed * 70f));
        }

        private void ApplyShake()
        {
            var camera = Camera.main;
            if (camera == null) return;
            var t = camera.transform;
            float elapsed = Time.unscaledTime - shakeAt;
            var offset = ShakeOffset(elapsed, shakePunch, shakeMeters, shakeDir);
            if (offset == Vector3.zero)
            {
                if (shaking)
                {
                    //  남이 그 사이 카메라를 옮겼으면(화면비 맞춤) 그 자리를 존중한다.
                    if (t.position == lastApplied) t.position = cameraBase;
                    shaking = false;
                }
                return;
            }
            if (!shaking || t.position != lastApplied) cameraBase = t.position;
            shaking = true;
            lastApplied = cameraBase + offset;
            t.position = lastApplied;
        }

        private ParticleSystem MakeBurst(Transform parent)
        {
            var go = new GameObject("TriGrabBurst");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1.2f;
            main.maxParticles = 600;
            main.startSpeed = 0f;
            var emission = ps.emission;
            emission.enabled = false;   //  Emit()으로만 낸다
            var shape = ps.shape;
            shape.enabled = false;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            burstMaterial = new Material(shader) { name = "TriGrabBurst" };
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = burstMaterial;
            ps.Play();
            return ps;
        }

        public void Dispose()
        {
            if (driver != null) Object.Destroy(driver.gameObject);
            if (root != null) Object.Destroy(root);
            if (burstMaterial != null) Object.Destroy(burstMaterial);
            foreach (var clip in clips.Values) if (clip != null) Object.Destroy(clip);
            clips.Clear();
        }
    }
}
