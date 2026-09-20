using System.Collections.Generic;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// 중간층·배경 실루엣의 배치. 게임에 안 닿는 것들이라 규칙이 느슨하지만, <b>코스 전체를
    /// 덮는가</b>와 <b>같은 씨앗이면 같은 그림인가</b>는 못박는다 — 배경이 중간에 끊기는 사고가
    /// 실제로 있었다(도시가 557m에서 끝나는데 코스는 612m였다).
    /// </summary>
    public class BackdropLayoutTests
    {
        const float StartX = 0f;
        const float Length = 612f;

        [Test]
        public void 중간층이_코스_전체를_덮는다()
        {
            List<BackdropBox> boxes = BackdropLayout.Midground(StartX, Length, seed: 1UL);

            Assert.Greater(boxes.Count, 0);
            Assert.LessOrEqual(boxes[0].X, StartX, "코스 시작보다 앞에서 시작해야 한다");
            Assert.GreaterOrEqual(boxes[boxes.Count - 1].X, StartX + Length, "코스 끝을 넘겨야 한다");
        }

        [Test]
        public void 배경도_코스_전체를_덮는다()
        {
            List<BackdropBox> boxes = BackdropLayout.Skyline(StartX, Length, seed: 1UL);

            Assert.LessOrEqual(boxes[0].X, StartX);
            Assert.GreaterOrEqual(boxes[boxes.Count - 1].X, StartX + Length);
        }

        [Test]
        public void 같은_씨앗이면_같은_그림이다()
        {
            List<BackdropBox> a = BackdropLayout.Midground(StartX, Length, seed: 7UL);
            List<BackdropBox> b = BackdropLayout.Midground(StartX, Length, seed: 7UL);

            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].X, b[i].X, 1e-4f);
                Assert.AreEqual(a[i].Height, b[i].Height, 1e-4f);
            }
        }

        [Test]
        public void 다른_씨앗이면_다른_그림이다()
        {
            List<BackdropBox> a = BackdropLayout.Midground(StartX, Length, seed: 7UL);
            List<BackdropBox> b = BackdropLayout.Midground(StartX, Length, seed: 8UL);

            bool same = a.Count == b.Count;
            if (same)
            {
                for (int i = 0; i < a.Count; i++)
                {
                    if (System.Math.Abs(a[i].Height - b[i].Height) > 1e-4f) { same = false; break; }
                }
            }
            Assert.IsFalse(same, "씨앗이 안 먹고 있다");
        }

        [Test]
        public void 배경이_중간층보다_크다()
        {
            //  82m 거리에서 화면 세로가 59.7m다 — 6m짜리 건물은 자갈로 보인다(실제로 그랬다).
            float mid = Tallest(BackdropLayout.Midground(StartX, Length, 3UL));
            float sky = Tallest(BackdropLayout.Skyline(StartX, Length, 3UL));

            Assert.Greater(sky, mid);
            Assert.Greater(sky, 40f, "배경은 화면(59.7m)의 절반은 넘어야 스카이라인으로 읽힌다");
        }

        [Test]
        public void 뒤로_갈수록_중간층이_낮고_기울어진다()
        {
            //  구간 1은 서 있고 구간 3은 무너져 있다 — 그게 보여야 진행이 읽힌다.
            List<BackdropBox> boxes = BackdropLayout.Midground(StartX, Length, seed: 5UL);

            float firstThird = MeanHeight(boxes, StartX, StartX + Length / 3f);
            float lastThird = MeanHeight(boxes, StartX + Length * 2f / 3f, StartX + Length);
            Assert.Less(lastThird, firstThird, "뒤 구간이 더 낮아야 한다");

            float firstTilt = MeanTilt(boxes, StartX, StartX + Length / 3f);
            float lastTilt = MeanTilt(boxes, StartX + Length * 2f / 3f, StartX + Length);
            Assert.Greater(lastTilt, firstTilt, "뒤 구간이 더 기울어야 한다");
        }

        [Test]
        public void 길이가_0이면_비어_있다()
        {
            Assert.AreEqual(0, BackdropLayout.Midground(StartX, 0f, 1UL).Count);
            Assert.AreEqual(0, BackdropLayout.Skyline(StartX, -5f, 1UL).Count);
        }

        static float Tallest(List<BackdropBox> boxes)
        {
            float h = 0f;
            foreach (BackdropBox b in boxes) { if (b.Height > h) { h = b.Height; } }
            return h;
        }

        static float MeanHeight(List<BackdropBox> boxes, float from, float to)
        {
            float sum = 0f; int n = 0;
            foreach (BackdropBox b in boxes)
            {
                if (b.X >= from && b.X < to) { sum += b.Height; n++; }
            }
            return n == 0 ? 0f : sum / n;
        }

        static float MeanTilt(List<BackdropBox> boxes, float from, float to)
        {
            float sum = 0f; int n = 0;
            foreach (BackdropBox b in boxes)
            {
                if (b.X >= from && b.X < to) { sum += System.Math.Abs(b.TiltDegrees); n++; }
            }
            return n == 0 ? 0f : sum / n;
        }
    }
}
