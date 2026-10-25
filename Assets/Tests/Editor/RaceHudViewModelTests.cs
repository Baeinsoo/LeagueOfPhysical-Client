using System;
using System.Collections.Generic;
using LOP.UI;
using NUnit.Framework;
using R3;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>정보 HUD의 ViewModel. 월드는 가짜 소스가 채운 한 장(<see cref="RaceHudFrame"/>)으로만 본다.</summary>
    public class RaceHudViewModelTests
    {
        private sealed class FakeSource : IRaceHudSource
        {
            public Action<RaceHudFrame> Next;
            public void Fill(RaceHudFrame frame) => Next?.Invoke(frame);
        }

        private FakeSource source;
        private RaceHudViewModel viewModel;

        [SetUp]
        public void SetUp()
        {
            source = new FakeSource();
            viewModel = new RaceHudViewModel(source);
        }

        //  출발 틱 100(2초), 틱 0.02초, 출발 x 0 → 결승 x 100.
        private static void Course(RaceHudFrame f, double now, params RaceBirdSample[] birds)
        {
            f.MyId = "me";
            f.StartTick = 100;
            f.SecondsPerTick = 0.02d;
            f.NowSeconds = now;
            f.StartX = 0f;
            f.FinishX = 100f;
            f.HasCourse = true;
            f.Birds.AddRange(birds);
        }

        //  광산처럼 표식 셋(출발 0 → 결승 100): 20 m 1막, 50 m 2막, 80 m 3막(마지막).
        private static void Mine(RaceHudFrame f)
        {
            f.Sections.Add(new CourseSection("계단 내리막", 1, false, 20f));
            f.Sections.Add(new CourseSection("진자 광차", 2, false, 50f));
            f.Sections.Add(new CourseSection("고수 갈림길", 3, true, 80f));
        }

        [Test]
        public void 구간_띠는_구간_시작부터_다음_구간까지_막_색()
        {
            source.Next = f => { Course(f, 5d, new RaceBirdSample("me", 0, 10f)); Mine(f); };

            viewModel.Refresh();

            var bands = viewModel.Bands;
            Assert.AreEqual(3, bands.Count);
            Assert.AreEqual(0.2f, bands[0].From, 1e-5f);
            Assert.AreEqual(0.5f, bands[0].To, 1e-5f);
            Assert.AreEqual(FlappyRacePalette.ActColor(1), bands[0].Color);
            Assert.AreEqual(0.5f, bands[1].From, 1e-5f);
            Assert.AreEqual(0.8f, bands[1].To, 1e-5f);
            Assert.AreEqual(FlappyRacePalette.ActColor(2), bands[1].Color);
            Assert.AreEqual(0.8f, bands[2].From, 1e-5f);
            Assert.AreEqual(1f, bands[2].To, 1e-5f, "마지막 띠는 결승선까지");
            Assert.AreEqual(FlappyRacePalette.ActColor(3), bands[2].Color);
        }

        [Test]
        public void 구간이_없으면_띠도_없다()
        {
            //  전통 맵 — 막대는 지금처럼 단색(spec §1 "없으면 단색").
            source.Next = f => Course(f, 5d, new RaceBirdSample("me", 0, 10f));

            viewModel.Refresh();

            Assert.AreEqual(0, viewModel.Bands.Count);
        }

        [Test]
        public void 시계는_출발부터_재고_결승하면_멈춘다()
        {
            source.Next = f => Course(f, 5.0d, new RaceBirdSample("me", 0, 10f));
            viewModel.Refresh();
            Assert.AreEqual("0:03.0", viewModel.ClockText.CurrentValue);

            source.Next = f => { Course(f, 10.0d, new RaceBirdSample("me", 0, 100f)); f.MyFinishedTick = 200; };
            viewModel.Refresh();
            Assert.AreEqual("0:02.0", viewModel.ClockText.CurrentValue, "결승 틱 200 = 4초 → 출발부터 2초");
        }

        [Test]
        public void 순위와_전체_수()
        {
            source.Next = f => Course(f, 5d,
                new RaceBirdSample("me", 0, 50f), new RaceBirdSample("a", 0, 80f), new RaceBirdSample("b", 1, 100f));

            viewModel.Refresh();

            Assert.AreEqual(3, viewModel.Rank.CurrentValue);
            Assert.AreEqual(3, viewModel.Total.CurrentValue);
        }

        [Test]
        public void 내_새가_없으면_순위_0()
        {
            source.Next = f => Course(f, 5d, new RaceBirdSample("a", 0, 80f));

            viewModel.Refresh();

            Assert.AreEqual(0, viewModel.Rank.CurrentValue);
        }

        [Test]
        public void 순위가_바뀌면_튕김을_한_번_알리고_0_8초_안엔_다시_안_알린다()
        {
            int pops = 0;
            viewModel.RankPopped.Subscribe(_ => pops++);

            //  출발 2초 + 3.0초 = 5.0초: 나 2위.
            source.Next = f => Course(f, 5.0d, new RaceBirdSample("me", 0, 50f), new RaceBirdSample("a", 0, 60f));
            viewModel.Refresh();
            //  5.1초: 나 1위.
            source.Next = f => Course(f, 5.1d, new RaceBirdSample("me", 0, 61f), new RaceBirdSample("a", 0, 60f));
            viewModel.Refresh();
            //  5.2초: 다시 2위 — 0.1초 만이라 삼킨다.
            source.Next = f => Course(f, 5.2d, new RaceBirdSample("me", 0, 61f), new RaceBirdSample("a", 0, 62f));
            viewModel.Refresh();

            Assert.AreEqual(1, pops);
            Assert.AreEqual(2, viewModel.Rank.CurrentValue, "숫자는 삼켜도 바뀐다");
        }

        [Test]
        public void 점은_남_먼저_나를_마지막에_내_점은_노랑()
        {
            source.Next = f => Course(f, 5d, new RaceBirdSample("me", 0, 25f), new RaceBirdSample("a", 0, 50f));

            viewModel.Refresh();

            Assert.AreEqual(2, viewModel.Dots.Count);
            Assert.IsFalse(viewModel.Dots[0].Mine);
            Assert.AreEqual(0.5f, viewModel.Dots[0].Ratio, 1e-5f);
            Assert.AreEqual(FlappyRacePalette.OpponentNeutral, viewModel.Dots[0].Color);
            Assert.IsTrue(viewModel.Dots[1].Mine, "내 점이 마지막이라 겹쳐도 위에 그려진다");
            Assert.AreEqual(0.25f, viewModel.Dots[1].Ratio, 1e-5f);
            Assert.AreEqual(FlappyRacePalette.Mine, viewModel.Dots[1].Color);
            Assert.AreEqual(0.25f, viewModel.MyProgress.CurrentValue, 1e-5f);
        }

        //  사용자 결정(10-25): 게임이 선수에게 색을 배정하지 않는다 — 상대는 누구든 같은 중립색이다(옷이 생기면 그쪽이 이긴다).
        [Test]
        public void 상대가_여럿이어도_모두_같은_중립색이다()
        {
            source.Next = f => Course(f, 5d,
                new RaceBirdSample("me", 0, 10f), new RaceBirdSample("a", 0, 20f), new RaceBirdSample("b", 0, 30f));

            viewModel.Refresh();

            int opponentCount = 0;
            foreach (var dot in viewModel.Dots)
            {
                if (dot.Mine) { continue; }
                opponentCount++;
                Assert.AreEqual(FlappyRacePalette.OpponentNeutral, dot.Color);
            }
            Assert.AreEqual(2, opponentCount);
        }

        [Test]
        public void 코스를_모르면_막대를_숨긴다()
        {
            source.Next = f => { Course(f, 5d, new RaceBirdSample("me", 0, 25f)); f.HasCourse = false; };

            viewModel.Refresh();

            Assert.IsFalse(viewModel.ShowProgress.CurrentValue);
        }

        [Test]
        public void 내가_결승하면_순위판을_숨긴다()
        {
            source.Next = f => Course(f, 5d, new RaceBirdSample("me", 0, 100f));
            viewModel.Refresh();
            Assert.IsTrue(viewModel.ShowRank.CurrentValue, "결승 전엔 순위판을 보인다");

            source.Next = f => { Course(f, 5d, new RaceBirdSample("me", 0, 100f)); f.MyFinishedTick = 200; };
            viewModel.Refresh();

            Assert.IsFalse(viewModel.ShowRank.CurrentValue, "결승·결과 화면이 등수를 보여 주니 순위판은 숨긴다(관전 나가기 버튼과 겹침)");
        }

        [Test]
        public void 프레임은_매번_비우고_다시_채운다()
        {
            source.Next = f => Course(f, 5d, new RaceBirdSample("me", 0, 25f), new RaceBirdSample("a", 0, 50f));
            viewModel.Refresh();
            source.Next = f => Course(f, 5.1d, new RaceBirdSample("me", 0, 26f));
            viewModel.Refresh();

            Assert.AreEqual(1, viewModel.Total.CurrentValue, "떠난 새가 남아 있지 않다");
            Assert.AreEqual(1, viewModel.Dots.Count);
        }
    }
}
