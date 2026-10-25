using System;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 플래피 소리를 코드로 짓는다(spec §6 — 소리 파일이 생기기 전까지의 임시). 조리법은 시제품 "광산 레이스 적용판"의
    /// <c>sfx</c>(WebAudio 발진기·잡음)를 그대로 옮겼다 — 시제품에서 들어 본 소리가 게임에서도 난다.
    /// <para>순수 계산이라 EditMode로 고정한다. 잡음은 소리마다 정해진 씨앗을 써서 늘 같은 파형이 나온다.</para>
    /// </summary>
    public static class FlappySynth
    {
        public const int Rate = 44100;

        //  시제품 master 게인. 겹친 소리가 1을 넘어 찢어지지 않게 클립에 미리 곱해 둔다.
        internal const float Master = 0.5f;

        //  마지막 음이 0.0001까지 내려간 뒤 잘리지 않게 붙이는 꼬리.
        internal const float TailSeconds = 0.03f;

        internal const int MusicSteps = 32;
        internal const float MusicBpm = 124f;

        /// <summary>배경음 한 박(8분음표) 길이 — 시제품 <c>60/(124×tempo)/2</c>.</summary>
        internal static float MusicStepSeconds => 60f / MusicBpm / 2f;

        internal enum Wave { Sine, Square, Triangle, Saw }

        internal enum Filter { BandPass, LowPass }

        /// <summary>소리 한 겹: 발진기 하나(tone) 또는 걸러 낸 잡음 하나(noise). 인자 순서는 시제품 함수와 같다.</summary>
        internal readonly struct Part
        {
            public readonly bool IsNoise;
            public readonly Wave Wave;
            public readonly Filter Filter;
            public readonly float Freq;
            public readonly float Duration;
            public readonly float Volume;
            public readonly float When;
            public readonly float Slide;
            public readonly float Q;

            private Part(bool isNoise, Wave wave, Filter filter, float freq, float duration, float volume, float when, float slide, float q)
            {
                IsNoise = isNoise;
                Wave = wave;
                Filter = filter;
                Freq = freq;
                Duration = duration;
                Volume = volume;
                When = when;
                Slide = slide;
                Q = q;
            }

            /// <summary>시제품 <c>tone(f, d, type, v, when, slide)</c>. slide면 끝에서 f×slide까지 지수로 미끄러진다.</summary>
            public static Part Tone(float f, float d, Wave wave, float v, float when = 0f, float slide = 0f)
                => new Part(false, wave, Filter.BandPass, f, d, v, when, slide, 0f);

            /// <summary>시제품 <c>noise(d, v, f, q, when, type)</c> — 점점 작아지는 흰 잡음을 2차 필터로 거른다.</summary>
            public static Part Noise(float d, float v, float f, float q, float when = 0f, Filter type = Filter.BandPass)
                => new Part(true, Wave.Sine, type, f, d, v, when, 0f, q);
        }

        internal static Part[] Recipe(FlappySfx sfx)
        {
            switch (sfx)
            {
                case FlappySfx.Flap:
                    return new[] { Part.Noise(0.08f, 0.16f, 1800f, 0.8f) };
                case FlappySfx.Crash:
                    return new[] { Part.Tone(130f, 0.35f, Wave.Sine, 0.55f, 0f, 0.4f), Part.Noise(0.25f, 0.38f, 450f, 0.7f) };
                case FlappySfx.CrashFar:
                    return new[] { Part.Tone(210f, 0.14f, Wave.Sine, 0.1f, 0f, 0.5f) };
                case FlappySfx.CountBeat:
                    return new[] { Part.Tone(660f, 0.12f, Wave.Square, 0.12f) };
                case FlappySfx.Go:
                    return new[] { Part.Tone(990f, 0.4f, Wave.Square, 0.15f), Part.Tone(1320f, 0.4f, Wave.Triangle, 0.1f, 0.04f) };
                case FlappySfx.RankUp:
                    return new[] { Part.Tone(880f, 0.08f, Wave.Triangle, 0.18f), Part.Tone(1175f, 0.12f, Wave.Triangle, 0.18f, 0.07f) };
                case FlappySfx.RankDown:
                    return new[] { Part.Tone(330f, 0.16f, Wave.Triangle, 0.12f, 0f, 0.8f) };
                case FlappySfx.Section:
                    return new[] { Part.Tone(523f, 0.1f, Wave.Triangle, 0.1f), Part.Tone(784f, 0.16f, Wave.Triangle, 0.1f, 0.08f) };
                case FlappySfx.Rumble:
                    return new[] { Part.Noise(0.45f, 0.22f, 140f, 0.9f, 0f, Filter.LowPass), Part.Tone(70f, 0.4f, Wave.Sine, 0.14f, 0f, 0.8f) };
                case FlappySfx.Stamp:
                    return new[] { Part.Noise(0.06f, 0.3f, 900f, 0.6f), Part.Tone(180f, 0.12f, Wave.Sine, 0.25f, 0f, 0.6f) };
                case FlappySfx.Finish:
                    return new[]
                    {
                        Part.Tone(523f, 0.25f, Wave.Triangle, 0.18f, 0f),
                        Part.Tone(659f, 0.25f, Wave.Triangle, 0.18f, 0.09f),
                        Part.Tone(784f, 0.25f, Wave.Triangle, 0.18f, 0.18f),
                        Part.Tone(1047f, 0.25f, Wave.Triangle, 0.18f, 0.27f),
                        Part.Tone(1047f, 0.6f, Wave.Sine, 0.12f, 0.4f),
                    };
                case FlappySfx.NewBest:
                    return new[]
                    {
                        Part.Tone(784f, 0.18f, Wave.Triangle, 0.16f, 0f),
                        Part.Tone(988f, 0.18f, Wave.Triangle, 0.16f, 0.08f),
                        Part.Tone(1319f, 0.18f, Wave.Triangle, 0.16f, 0.16f),
                    };
                case FlappySfx.LastStretch:
                    return new[] { Part.Tone(784f, 0.12f, Wave.Square, 0.1f), Part.Tone(1047f, 0.2f, Wave.Square, 0.1f, 0.1f) };
                default:
                    throw new ArgumentOutOfRangeException(nameof(sfx), sfx, "조리법이 없는 소리");
            }
        }

        /// <summary>효과음 한 개의 표본(모노, <see cref="Rate"/>). 길이 = 제일 늦게 끝나는 겹 + 꼬리.</summary>
        public static float[] Render(FlappySfx sfx)
        {
            Part[] parts = Recipe(sfx);
            float end = 0f;
            foreach (var p in parts)
            {
                end = Mathf.Max(end, p.When + p.Duration);
            }
            var data = new float[Mathf.CeilToInt((end + TailSeconds) * Rate)];
            var rng = new System.Random((int)sfx + 1);
            foreach (var p in parts)
            {
                Write(data, p, rng, wrap: false);
            }
            return data;
        }

        /// <summary>
        /// 배경음 한 바퀴(시제품 <c>startBgm</c>): 베이스(톱니, 2박마다) + 아르페지오(세모, 매 박) + 하이햇(잡음, 4박마다).
        /// 32박이면 베이스 네 마디·아르페지오 옥타브 오르내림이 한 번씩 돌아 처음과 맞물린다. 끝을 넘는 꼬리는 앞으로 감아 이음매가 없다.
        /// </summary>
        public static float[] MusicLoop()
        {
            float sp = MusicStepSeconds;
            var data = new float[Mathf.RoundToInt(MusicSteps * sp * Rate)];
            var rng = new System.Random(124);
            float[] bass = { 110f, 110f, 131f, 98f };
            float[] arp = { 440f, 523f, 659f, 523f, 494f, 587f, 740f, 587f };
            for (int step = 0; step < MusicSteps; step++)
            {
                float w = step * sp;
                if (step % 2 == 0)
                {
                    Write(data, Part.Tone(bass[(step >> 3) % 4], sp * 1.6f, Wave.Saw, 0.05f, w, 0.99f), rng, wrap: true);
                }
                Write(data, Part.Tone(arp[step % 8] * (1f + ((step >> 4) % 2) * 0.122f), sp * 0.8f, Wave.Triangle, 0.035f, w), rng, wrap: true);
                if (step % 4 == 0)
                {
                    Write(data, Part.Noise(0.04f, 0.05f, 6000f, 2f, w), rng, wrap: true);
                }
            }
            return data;
        }

        /// <summary>
        /// WebAudio 시제품과 같은 소리 모양: 0.0001에서 0.01초에 peak까지 지수로 오르고, duration에 0.0001까지 지수로 내려간다.
        /// </summary>
        internal static float Envelope(float t, float duration, float peak)
        {
            const float floor = 0.0001f;
            const float attack = 0.01f;
            if (peak <= floor)
            {
                return 0f;
            }
            if (t < attack)
            {
                return floor * Mathf.Pow(peak / floor, Mathf.Max(0f, t) / attack);
            }
            float u = Mathf.Clamp01((t - attack) / Mathf.Max(1e-4f, duration - attack));
            return peak * Mathf.Pow(floor / peak, u);
        }

        private static void Write(float[] data, Part p, System.Random rng, bool wrap)
        {
            int start = Mathf.RoundToInt(p.When * Rate);
            int n = Mathf.Max(1, Mathf.RoundToInt(p.Duration * Rate));
            if (p.IsNoise)
            {
                WriteNoise(data, p, start, n, rng, wrap);
            }
            else
            {
                WriteTone(data, p, start, n, wrap);
            }
        }

        private static void Put(float[] data, int index, float value, bool wrap)
        {
            if (wrap)
            {
                index %= data.Length;
            }
            else if (index >= data.Length)
            {
                return;
            }
            data[index] += value;
        }

        private static void WriteTone(float[] data, Part p, int start, int n, bool wrap)
        {
            //  slide: 시제품 exponentialRampToValueAtTime(max(30, f×slide), t+d).
            float endFreq = p.Slide > 0f ? Mathf.Max(30f, p.Freq * p.Slide) : p.Freq;
            double phase = 0d;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = p.Slide > 0f ? p.Freq * Mathf.Pow(endFreq / p.Freq, t / p.Duration) : p.Freq;
                phase += f / (double)Rate;
                float x = (float)(phase - Math.Floor(phase));
                Put(data, start + i, Osc(p.Wave, x) * Envelope(t, p.Duration, p.Volume) * Master, wrap);
            }
        }

        //  x = 한 주기 안의 자리(0~1).
        private static float Osc(Wave wave, float x)
        {
            switch (wave)
            {
                case Wave.Square: return x < 0.5f ? 1f : -1f;
                case Wave.Triangle: return 1f - 4f * Mathf.Abs(Mathf.Round(x - 0.25f) - (x - 0.25f));
                case Wave.Saw: return 2f * x - 1f;
                default: return Mathf.Sin(2f * Mathf.PI * x);
            }
        }

        private static void WriteNoise(float[] data, Part p, int start, int n, System.Random rng, bool wrap)
        {
            var filter = Biquad.Make(p.Filter, p.Freq, p.Q);
            for (int i = 0; i < n; i++)
            {
                //  시제품: 흰 잡음 × (1 − i/n) — 점점 작아진다.
                float white = (float)(rng.NextDouble() * 2d - 1d) * (1f - i / (float)n);
                Put(data, start + i, filter.Next(white) * p.Volume * Master, wrap);
            }
        }

        //  RBJ 쿡북 2차 필터(WebAudio BiquadFilterNode와 같은 식). 대역통과는 가운데 이득 0 dB.
        private struct Biquad
        {
            private float b0, b1, b2, a1, a2, x1, x2, y1, y2;

            public static Biquad Make(Filter type, float freq, float q)
            {
                float w0 = 2f * Mathf.PI * Mathf.Min(freq, Rate * 0.45f) / Rate;
                float alpha = Mathf.Sin(w0) / (2f * Mathf.Max(1e-3f, q));
                float cos = Mathf.Cos(w0);
                float a0 = 1f + alpha;
                var f = new Biquad();
                if (type == Filter.LowPass)
                {
                    f.b0 = (1f - cos) / 2f / a0;
                    f.b1 = (1f - cos) / a0;
                    f.b2 = f.b0;
                }
                else
                {
                    f.b0 = alpha / a0;
                    f.b1 = 0f;
                    f.b2 = -alpha / a0;
                }
                f.a1 = -2f * cos / a0;
                f.a2 = (1f - alpha) / a0;
                return f;
            }

            public float Next(float x)
            {
                float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1;
                x1 = x;
                y2 = y1;
                y1 = y;
                return y;
            }
        }
    }
}
