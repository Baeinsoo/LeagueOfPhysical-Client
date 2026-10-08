using System.Collections.Generic;
using LOP;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 선 찾기·따라가는 점·피벗 적용을 모두 이음매로 주입한다 — 열린 씬(통로선이 있는 FlappyRaceMap 등)이나
    /// 실제 카메라에 결과가 기대지 않는다. "화면 중심" = 따라가는 y + <see cref="FlappyCorridorCamera.Offset"/>.
    /// </summary>
    public class FlappyCorridorCameraTests
    {
        private const float Dt = 0.02f;

        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned) { Object.DestroyImmediate(go); }
            spawned.Clear();
        }

        private FlappyCorridorLine NewLine(params Vector2[] points)
        {
            var go = new GameObject("CorridorLine");
            spawned.Add(go);
            var line = go.AddComponent<FlappyCorridorLine>();
            line.Points = points;
            return line;
        }

        //  follow: 카메라가 따라가는 점(그려진 새 원점 + PivotHeight)을 매번 읽는다.
        private static FlappyCorridorCamera Camera(FlappyCorridorLine line, System.Func<Vector3?> follow,
                                                   List<Vector3> pivots = null)
        {
            var sut = new FlappyCorridorCamera(null);
            sut.findLine = () => line;
            sut.followPoint = follow;
            sut.applyPivot = p => pivots?.Add(p);
            return sut;
        }

        private static void Run(FlappyCorridorCamera sut, float seconds)
        {
            for (float t = 0f; t < seconds; t += Dt) { sut.Tick(Dt); }
        }

        [Test]
        public void 표시가_없으면_오프셋이_0으로_유지된다()
        {
            var pivots = new List<Vector3>();
            var sut = Camera(null, () => new Vector3(5f, 0f, 0f), pivots);

            Run(sut, 3f);

            Assert.AreEqual(0f, sut.Offset, 1e-4f);
            Assert.IsEmpty(pivots, "표시가 없는데 피벗을 건드렸다");
        }

        [Test]
        public void 표시를_못_찾으면_1초에_한_번만_다시_찾는다()
        {
            var sut = Camera(null, () => new Vector3(5f, 0f, 0f));
            int searches = 0;
            sut.findLine = () => { searches++; return null; };

            Run(sut, 3f);   // 150 프레임

            Assert.That(searches, Is.InRange(3, 4));
        }

        [Test]
        public void 충분한_틱_뒤_화면_중심이_통로_중심에_선다()
        {
            var line = NewLine(new Vector2(0f, 0f), new Vector2(10f, 10f));   // CenterAt(5) == 5
            var pivots = new List<Vector3>();
            var sut = Camera(line, () => new Vector3(5f, 0f, 0f), pivots);

            Run(sut, 3f);

            Assert.AreEqual(5f, 0f + sut.Offset, 0.01f);
            Assert.AreEqual(sut.Offset, pivots[pivots.Count - 1].y, 1e-4f);
        }

        //  I1(10-08 최종 리뷰): 새 기준 상대값을 매끄럽게 하던 옛 구현은 날갯짓(±0.75 m, 1.7 Hz)의 오르내림이
        //  거의 그대로 화면에 실렸다(최대 1.1 m 벗어남). 중심은 절대 높이라 새가 움직여도 그대로여야 한다.
        [Test]
        public void 날갯짓으로_새가_오르내려도_화면_중심은_통로_중심에_머문다()
        {
            var line = NewLine(new Vector2(0f, 3f), new Vector2(100f, 3f));
            float birdY = 2.5f;
            var sut = Camera(line, () => new Vector3(5f, birdY, 0f));

            float worst = 0f;
            for (float t = 0f; t < 5f; t += Dt)
            {
                birdY = 2.5f + 0.75f * Mathf.Sin(2f * Mathf.PI * 1.7f * t);
                sut.Tick(Dt);
                if (t > 1f) { worst = Mathf.Max(worst, Mathf.Abs(birdY + sut.Offset - line.CenterAt(5f))); }
            }

            Assert.Less(worst, 0.05f, "날갯짓이 화면 중심을 흔든다");
        }

        [Test]
        public void 관전_대상이_바뀌어도_화면_중심이_튀지_않는다()
        {
            var line = NewLine(new Vector2(0f, 3f), new Vector2(100f, 3f));
            Vector3 follow = new Vector3(5f, 1f, 0f);
            var sut = Camera(line, () => follow);
            Run(sut, 2f);
            float before = follow.y + sut.Offset;

            follow = new Vector3(6f, 6f, 0f);   // 다른 새로 — 5 m 위
            sut.Tick(Dt);

            Assert.AreEqual(before, follow.y + sut.Offset, 0.05f);
        }

        [Test]
        public void 표시를_찾은_순간_화면이_덜컹이지_않는다()
        {
            var line = NewLine(new Vector2(0f, 10f), new Vector2(100f, 10f));
            var sut = Camera(line, () => new Vector3(5f, 0f, 0f));

            sut.Tick(Dt);   // 첫 틱에 찾는다 — 중심은 지금 높이(0)에서 출발해 10으로 다가간다

            Assert.Less(Mathf.Abs(sut.Offset), 1f, "찾자마자 통로 중심으로 순간이동했다");
        }

        [Test]
        public void Dispose하면_카메라_중심을_제자리로()
        {
            var line = NewLine(new Vector2(0f, 0f), new Vector2(10f, 10f));
            var pivots = new List<Vector3>();
            var sut = Camera(line, () => new Vector3(5f, 0f, 0f), pivots);

            Run(sut, 1f);
            sut.Dispose();

            Assert.AreEqual(Vector3.zero, pivots[pivots.Count - 1]);
            Assert.AreEqual(0f, sut.Offset, 1e-4f);
        }
    }
}
