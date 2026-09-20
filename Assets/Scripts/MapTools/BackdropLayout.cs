using System.Collections.Generic;
using GameFramework.Rng;

namespace LOP.MapTools
{
    /// <summary>배경 실루엣 한 덩어리. 판정과 무관하므로 값만 있다.</summary>
    public readonly struct BackdropBox
    {
        public readonly float X;
        public readonly float CenterY;
        public readonly float Width;
        public readonly float Height;
        /// <summary>z축 둘레로 기울인 각도. z 범위가 안 변해 층이 흐트러지지 않는다.</summary>
        public readonly float TiltDegrees;

        public BackdropBox(float x, float centerY, float width, float height, float tiltDegrees)
        {
            X = x;
            CenterY = centerY;
            Width = width;
            Height = height;
            TiltDegrees = tiltDegrees;
        }
    }

    /// <summary>
    /// 게임 평면 <b>뒤</b>에 깔리는 두 층의 배치.
    ///
    /// <para><b>왜 순수 계층인가</b>: "코스 전체를 덮는가"는 씬 없이 잴 수 있고, 안 재면
    /// 조용히 틀린다 — 실제로 배경 도시가 557m에서 끝나는데 코스가 612m였다.</para>
    ///
    /// <para><b>기울기는 z축 둘레로만</b> 준다. 다른 축으로 돌리면 블록의 z 범위가 변해
    /// 층이 섞이고, 층 규약 검사의 분류(새의 z대역과 겹치는가)가 흔들린다.</para>
    /// </summary>
    public static class BackdropLayout
    {
        /// <summary>중간층 — 34m 거리. 화면 세로 24.8m를 채워야 하므로 그보다 크게 잡는다.</summary>
        public static List<BackdropBox> Midground(float startX, float length, ulong seed)
        {
            return Layout(startX, length, seed,
                          stepMin: 9f, stepMax: 19f,
                          widthMin: 6f, widthMax: 14f,
                          heightMin: 18f, heightMax: 38f,
                          baseY: -12f,
                          //  뒤로 갈수록 낮아지고(무너진다) 기운다.
                          heightDecay: 0.45f, tiltMax: 14f);
        }

        /// <summary>배경 — 82m 거리. 화면 세로가 59.7m라 40m는 넘겨야 스카이라인으로 읽힌다.</summary>
        public static List<BackdropBox> Skyline(float startX, float length, ulong seed)
        {
            return Layout(startX, length, seed,
                          stepMin: 14f, stepMax: 32f,
                          widthMin: 8f, widthMax: 26f,
                          heightMin: 30f, heightMax: 58f,
                          baseY: -30f,
                          heightDecay: 0.25f, tiltMax: 6f);
        }

        private static List<BackdropBox> Layout(float startX, float length, ulong seed,
                                                float stepMin, float stepMax,
                                                float widthMin, float widthMax,
                                                float heightMin, float heightMax,
                                                float baseY, float heightDecay, float tiltMax)
        {
            var boxes = new List<BackdropBox>();
            if (length <= 0f)
            {
                return boxes;
            }
            var rng = new DeterministicRandom(seed);

            //  코스보다 한 발씩 앞뒤로 넘겨 깐다 — 시작·결승 연출에서 배경이 끊기면 안 된다.
            float margin = stepMax * 2f;
            for (float x = startX - margin; x <= startX + length + margin; x += rng.Range(stepMin, stepMax))
            {
                float t = CourseSectionRule.Progress(x, startX, length);
                float shrink = 1f - heightDecay * t;
                float height = rng.Range(heightMin, heightMax) * shrink;
                float width = rng.Range(widthMin, widthMax);
                float tilt = rng.Range(-tiltMax, tiltMax) * t;   // 앞은 곧고 뒤는 기운다
                boxes.Add(new BackdropBox(x, baseY + height * 0.5f, width, height, tilt));
            }
            return boxes;
        }
    }
}
