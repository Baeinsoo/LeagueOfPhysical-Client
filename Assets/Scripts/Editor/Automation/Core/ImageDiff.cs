using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace LOP.EditorTools.Automation
{
    public readonly struct ImageDiffResult
    {
        public ImageDiffResult(int differing, int compared)
        {
            Differing = differing;
            Compared = compared;
        }

        public int Differing { get; }
        public int Compared { get; }
        public double Percent => Compared == 0 ? 0 : Differing * 100.0 / Compared;
    }

    /// <summary>
    /// 두 그림의 픽셀 비교(spec §15). 픽셀마다 R·G·B 차이 중 가장 큰 것이 tolerance를 넘으면 "다르다". region(없으면 전체) 안이면서
    /// ignore 밖인 픽셀만 센다. 픽셀 배열은 유니티 텍스처 순서(왼쪽 아래부터 한 줄씩), 구역은 정규화(왼쪽 위 원점) — 화면·ui_query와 같은 좌표.
    /// </summary>
    public static class ImageDiff
    {
        public static readonly Color32 DiffColor = new Color32(255, 0, 0, 255);
        public static readonly Color32 SkipColor = new Color32(0, 0, 96, 255);

        /// <param name="diff">있으면 채운다: 다른 픽셀 빨강, 안 센 픽셀 짙은 파랑, 나머지는 지금 그림을 흐린 회색으로.</param>
        public static ImageDiffResult Compare(Color32[] expected, Color32[] actual, int width, int height, int tolerance,
            Rect? region, IReadOnlyList<Rect> ignore, Color32[] diff)
        {
            if (expected.Length != width * height || actual.Length != width * height) throw new ArgumentException("픽셀 수가 크기와 다르다");

            int differing = 0;
            int compared = 0;
            for (int py = 0; py < height; py++)
            {
                //  텍스처 줄 0이 맨 아래 — 화면 좌표(왼쪽 위 원점)로 뒤집는다.
                float ny = 1f - (py + 0.5f) / height;
                for (int px = 0; px < width; px++)
                {
                    int i = py * width + px;
                    var point = new Vector2((px + 0.5f) / width, ny);
                    bool counted = (region == null || region.Value.Contains(point)) && !InAny(ignore, point);
                    if (!counted)
                    {
                        if (diff != null) diff[i] = SkipColor;
                        continue;
                    }

                    compared++;
                    var a = expected[i];
                    var b = actual[i];
                    int delta = Math.Max(Math.Abs(a.r - b.r), Math.Max(Math.Abs(a.g - b.g), Math.Abs(a.b - b.b)));
                    if (delta > tolerance)
                    {
                        differing++;
                        if (diff != null) diff[i] = DiffColor;
                    }
                    else if (diff != null)
                    {
                        byte gray = (byte)((b.r + b.g + b.b) / 9 + 40);
                        diff[i] = new Color32(gray, gray, gray, 255);
                    }
                }
            }
            return new ImageDiffResult(differing, compared);
        }

        private static bool InAny(IReadOnlyList<Rect> rects, Vector2 point)
        {
            if (rects == null) return false;
            foreach (var rect in rects)
            {
                if (rect.Contains(point)) return true;
            }
            return false;
        }
    }

    /// <summary>시나리오의 정규화 구역 [x, y, w, h](0..1, 왼쪽 위 원점).</summary>
    public static class NormalizedRect
    {
        public static bool TryParse(JToken token, out Rect rect, out string why)
        {
            rect = default;
            if (!(token is JArray array) || array.Count != 4 || array.Any(t => t.Type != JTokenType.Integer && t.Type != JTokenType.Float))
            {
                why = "[x, y, w, h](0..1 수 넷)이어야 한다";
                return false;
            }

            float x = (float)array[0], y = (float)array[1], w = (float)array[2], h = (float)array[3];
            //  1을 조금 넘는 것은 소수 반올림이라 받아 준다.
            if (x < 0 || y < 0 || w <= 0 || h <= 0 || x + w > 1.0001f || y + h > 1.0001f)
            {
                why = "0..1 안의 구역이어야 한다(w·h > 0)";
                return false;
            }
            rect = new Rect(x, y, w, h);
            why = null;
            return true;
        }
    }
}
