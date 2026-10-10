using System;
using System.Linq;
using LOP.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 칠한 바위 텍스처(<see cref="MineRockPaint"/>, 10-11 Fix 1c)와 바위 덩어리 월드 UV(<see cref="FlappyMineDressing.WorldUvMesh"/>).
    /// 사용자 v3 "A": 큰 덩이 그림자·굵은 금·읽히는 지층, 중간 대비(폰에서 보임), 밝은 테·튀어나온 혹 없음, 큰 판에서 늘어나지 않음.
    /// </summary>
    public class MineRockPaintTests
    {
        private const int N = 256;

        private static MineRockPaint.Settings Settings(double contrast = 1, int cracks = 5, int seed = 7) =>
            new MineRockPaint.Settings(N, 6, 6, contrast, 1, 8, cracks, seed);

        private static double Std(double[] a)
        {
            double m = a.Average();
            return Math.Sqrt(a.Select(x => (x - m) * (x - m)).Average());
        }

        [Test]
        public void 가로_세로로_이어_붙여도_이음매가_없다()
        {
            double[] s = MineRockPaint.Shade(Settings());
            //  끝 열 → 첫 열(감싼 이웃)의 평균 차가 안쪽 이웃 열 사이 차와 같은 수준이어야 한다.
            double ColDiff(int a, int b) => Enumerable.Range(0, N).Average(y => Math.Abs(s[y * N + a] - s[y * N + b]));
            double RowDiff(int a, int b) => Enumerable.Range(0, N).Average(x => Math.Abs(s[a * N + x] - s[b * N + x]));
            double inCol = Enumerable.Range(0, N - 1).Average(x => ColDiff(x, x + 1));
            double inRow = Enumerable.Range(0, N - 1).Average(y => RowDiff(y, y + 1));
            Assert.Less(ColDiff(N - 1, 0), inCol * 2 + 0.005, "왼쪽·오른쪽 끝이 이어지지 않는다");
            Assert.Less(RowDiff(N - 1, 0), inRow * 2 + 0.005, "위·아래 끝이 이어지지 않는다");
        }

        [Test]
        public void 평균은_1이고_명암은_상한_하한_안이며_밝은_화소는_드물다()
        {
            double[] s = MineRockPaint.Shade(Settings());
            Assert.AreEqual(1.0, s.Average(), 0.01, "평균이 바위 평균 색에서 벗어난다(장식 색 기준이 틀어진다)");
            Assert.LessOrEqual(s.Max(), MineRockPaint.MaxShade + 1e-9);
            Assert.GreaterOrEqual(s.Min(), MineRockPaint.MinShade - 1e-9);
            //  밝은 테·혹 없음 — 평균보다 18% 넘게 밝은 화소는 아주 적다(빛 쪽 가장자리·지층 밝은 띠 조금).
            double bright = s.Count(x => x > 1.18) / (double)s.Length;
            Assert.Less(bright, 0.05, $"밝은 화소가 많다 {bright:P1}");
        }

        [Test]
        public void 같은_설정은_같은_그림이고_시드가_다르면_다르다()
        {
            CollectionAssert.AreEqual(MineRockPaint.Shade(Settings()), MineRockPaint.Shade(Settings()));
            CollectionAssert.AreNotEqual(MineRockPaint.Shade(Settings()), MineRockPaint.Shade(Settings(seed: 8)));
        }

        [Test]
        public void 대비는_중간이고_조절판을_따른다()
        {
            double flat = Std(MineRockPaint.Shade(Settings(contrast: 0)));
            double mid = Std(MineRockPaint.Shade(Settings(contrast: 1)));
            double high = Std(MineRockPaint.Shade(Settings(contrast: 2)));
            Assert.Less(flat, 0.005, "대비 0인데 무늬가 남는다");
            //  v2·v3 민짜 띠(strata_calm)는 밝기 폭 < 0.05였다 — 이제 덩이 그림자가 폰에서 읽혀야 하지만 요란하면 안 된다.
            Assert.That(mid, Is.InRange(0.07, 0.16), "기본 대비가 중간이 아니다");
            Assert.Greater(high, mid * 1.4, "대비 조절판이 안 먹는다");
        }

        [Test]
        public void 굵은_금은_어두운_선으로_그려진다()
        {
            //  금만 다르게 — 같은 화소가 금 때문에 확 어두워진 수(덩이 무늬는 같은 시드라 그대로다).
            double[] none = MineRockPaint.Shade(Settings(cracks: 0));
            double[] some = MineRockPaint.Shade(Settings(cracks: 5));
            int darker = Enumerable.Range(0, none.Length).Count(i => some[i] < none[i] - 0.2);
            Assert.Greater(darker, N / 2, "금 5개를 그렸는데 어두운 선이 거의 없다");
            Assert.Less(darker, none.Length / 20, "금이 면을 너무 많이 덮는다(굵은 금 '몇 개')");
        }

        [Test]
        public void Rgba는_바위_평균_색을_따른다()
        {
            var s = Settings();
            byte[] px = MineRockPaint.Rgba(s, 0.4, 0.28, 0.22);
            Assert.AreEqual(N * N * 4, px.Length);
            double r = 0, g = 0, b = 0;
            for (int i = 0; i < N * N; i++) { r += px[i * 4]; g += px[i * 4 + 1]; b += px[i * 4 + 2]; Assert.AreEqual(255, px[i * 4 + 3]); }
            r /= N * N * 255.0; g /= N * N * 255.0; b /= N * N * 255.0;
            Assert.AreEqual(0.4, r, 0.02);
            Assert.AreEqual(0.28, g, 0.02);
            Assert.AreEqual(0.22, b, 0.02);
            Assert.Greater(r, g, "따뜻한 갈색이 아니다");
            Assert.Greater(g, b, "따뜻한 갈색이 아니다");
        }

        // ---- 월드 UV ----

        //  단위 사각형 두 개(서브메시 0 = 바위, 1 = 다른 재질), 왼끝 아래 원점.
        private static Mesh TwoQuads()
        {
            var m = new Mesh();
            m.vertices = new[]
            {
                new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0),
                new Vector3(0, 0, 1), new Vector3(1, 0, 1), new Vector3(0, 1, 1), new Vector3(1, 1, 1),
            };
            m.uv = new[]
            {
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1),
            };
            m.subMeshCount = 2;
            m.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0);
            m.SetTriangles(new[] { 4, 6, 5, 5, 6, 7 }, 1);
            return m;
        }

        [Test]
        public void 바위_UV는_월드_좌표를_타일로_나눈_값이라_축척이_달라도_맞닿은_곳이_이어진다()
        {
            const float tile = 16f;
            var src = TwoQuads();
            //  가는 기둥(0.25 × 7)과 넓은 판(5 × 2.5) — 부품 UV(단위 한 장)라면 무늬가 28배·2배로 다르게 늘어난다.
            var a = Matrix4x4.TRS(new Vector3(30f, -4f, 0f), Quaternion.identity, new Vector3(0.25f, 7f, 0.1f));
            var b = Matrix4x4.TRS(new Vector3(30.25f, -4f, 0f), Quaternion.Euler(0, 0, 10f), new Vector3(5f, 2.5f, 0.1f));
            Mesh ma = FlappyMineDressing.WorldUvMesh(src, a, new[] { true, false }, tile);
            Mesh mb = FlappyMineDressing.WorldUvMesh(src, b, new[] { true, false }, tile);
            try
            {
                foreach (var (m, t) in new[] { (ma, a), (mb, b) })
                {
                    Vector3[] v = m.vertices;
                    Vector2[] uv = m.uv;
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 w = t.MultiplyPoint3x4(v[i]);
                        Assert.AreEqual(w.x / tile, uv[i].x, 1e-5f);
                        Assert.AreEqual(w.y / tile, uv[i].y, 1e-5f);
                    }
                    //  다른 재질 서브메시는 그대로.
                    for (int i = 4; i < 8; i++) { Assert.AreEqual(src.uv[i], uv[i]); }
                }
                //  a의 오른쪽 아래(30.25, −4) = b의 원점 — 같은 UV.
                Assert.AreEqual(ma.uv[1].x, mb.uv[0].x, 1e-5f);
                Assert.AreEqual(ma.uv[1].y, mb.uv[0].y, 1e-5f);
                //  원본 메시는 그대로(FBX 공유 메시를 건드리지 않는다).
                Assert.AreEqual(new Vector2(1, 1), src.uv[3]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(src);
                UnityEngine.Object.DestroyImmediate(ma);
                UnityEngine.Object.DestroyImmediate(mb);
            }
        }
    }
}
