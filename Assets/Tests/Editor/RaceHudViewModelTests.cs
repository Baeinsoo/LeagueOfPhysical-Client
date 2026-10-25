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
        private FakeFlappyAudio audio;
        private RaceHudViewModel viewModel;

        [SetUp]
        public void SetUp()
        {
            source = new FakeSource();
            audio = new FakeFlappyAudio();
            viewModel = new RaceHudViewModel(source, audio);
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

        [Test]
        public void 구간을_넘으면_막과_이름_배너와_구간_소리()
        {
            source.Next = f => { Course(f, 5.0d, new RaceBirdSample("me", 0, 45f)); Mine(f); };
            viewModel.Refresh();
            Assert.AreEqual(HudBannerKind.None, viewModel.BannerKind, "처음 본 장 — 이미 지난 20 m 구간은 안 알린다");

            source.Next = f => { Course(f, 5.1d, new RaceBirdSample("me", 0, 51f)); Mine(f); };
            viewModel.Refresh();

            Assert.AreEqual(HudBannerKind.Section, viewModel.BannerKind);
            Assert.AreEqual("진자 광차", viewModel.BannerTitle);
            Assert.AreEqual("2막", viewModel.BannerActText);
            Assert.AreEqual(FlappyRacePalette.ActColor(2), viewModel.BannerColor);
            CollectionAssert.AreEqual(new[] { FlappySfx.Section }, audio.Played);
            Assert.IsFalse(audio.FinalStretch);
        }

        [Test]
        public void 마지막_구간은_LAST_STRETCH와_팡파르_음악을_올린다()
        {
            source.Next = f => { Course(f, 5.0d, new RaceBirdSample("me", 0, 75f)); Mine(f); };
            viewModel.Refresh();
            source.Next = f => { Course(f, 5.1d, new RaceBirdSample("me", 0, 81f)); Mine(f); };
            viewModel.Refresh();

            Assert.AreEqual(HudBannerKind.LastStretch, viewModel.BannerKind);
            Assert.AreEqual(1.5f, viewModel.BannerScale, 1e-4f, "넘은 그 장은 1.5배에서 찍힌다");
            CollectionAssert.AreEqual(new[] { FlappySfx.LastStretch }, audio.Played, "구간 소리 대신 팡파르만(시제품)");
            Assert.IsTrue(audio.FinalStretch, "음악이 빨라지고 높아진다");
        }

        [Test]
        public void 구간_배너는_1_6초_뒤_사라진다()
        {
            source.Next = f => { Course(f, 5.0d, new RaceBirdSample("me", 0, 45f)); Mine(f); };
            viewModel.Refresh();
            source.Next = f => { Course(f, 5.1d, new RaceBirdSample("me", 0, 51f)); Mine(f); };
            viewModel.Refresh();

            source.Next = f => { Course(f, 5.9d, new RaceBirdSample("me", 0, 54f)); Mine(f); };
            viewModel.Refresh();
            Assert.AreEqual(HudBannerKind.Section, viewModel.BannerKind);
            Assert.AreEqual(1f, viewModel.BannerAlpha, 1e-4f, "0.8초 — 머무는 중");

            source.Next = f => { Course(f, 6.75d, new RaceBirdSample("me", 0, 58f)); Mine(f); };
            viewModel.Refresh();
            Assert.AreEqual(HudBannerKind.None, viewModel.BannerKind, "1.65초 — 사라졌다");
        }

        [Test]
        public void 출발_전에는_넘어도_배너가_없다()
        {
            //  now 1.0초 < 출발 2.0초 — 줄 서기 중.
            source.Next = f => { Course(f, 1.0d, new RaceBirdSample("me", 0, 45f)); Mine(f); };
            viewModel.Refresh();
            source.Next = f => { Course(f, 1.1d, new RaceBirdSample("me", 0, 51f)); Mine(f); };
            viewModel.Refresh();

            Assert.AreEqual(HudBannerKind.None, viewModel.BannerKind);
            Assert.AreEqual(0, audio.Played.Count);
        }

        [Test]
        public void 결승하면_배너를_거둔다()
        {
            source.Next = f => { Course(f, 5.0d, new RaceBirdSample("me", 0, 45f)); Mine(f); };
            viewModel.Refresh();
            source.Next = f => { Course(f, 5.1d, new RaceBirdSample("me", 0, 51f)); Mine(f); };
            viewModel.Refresh();

            source.Next = f => { Course(f, 5.2d, new RaceBirdSample("me", 0, 100f)); Mine(f); f.MyFinishedTick = 255; };
            viewModel.Refresh();

            Assert.AreEqual(HudBannerKind.None, viewModel.BannerKind, "결승 도장·관전 화면과 겹치지 않게");
        }

        [Test]
        public void 표식이_없는_맵은_배너도_소리도_없다()
        {
            source.Next = f => Course(f, 5.0d, new RaceBirdSample("me", 0, 45f));
            viewModel.Refresh();
            source.Next = f => Course(f, 5.1d, new RaceBirdSample("me", 0, 95f));
            viewModel.Refresh();

            Assert.AreEqual(HudBannerKind.None, viewModel.BannerKind);
            Assert.AreEqual(0, audio.Played.Count);
            Assert.IsFalse(audio.FinalStretch);
        }

        [Test]
        public void 추월하면_초록_플러스1과_두_음()
        {
            //  5.0초: 나 2위. 5.1초: 나 1위(출발 3.1초 — 2.5초 유예 밖).
            source.Next = f => Course(f, 5.0d, new RaceBirdSample("me", 0, 50f), new RaceBirdSample("a", 0, 60f));
            viewModel.Refresh();
            source.Next = f => Course(f, 5.1d, new RaceBirdSample("me", 0, 61f), new RaceBirdSample("a", 0, 60f));
            viewModel.Refresh();

            Assert.AreEqual(RaceHudViewModel.RankUpText, viewModel.DeltaText);
            Assert.AreEqual("+1", viewModel.DeltaText);
            Assert.AreEqual(FlappyRacePalette.RankUp, viewModel.DeltaColor);
            Assert.AreEqual(1f, viewModel.DeltaAlpha, 1e-4f);
            CollectionAssert.AreEqual(new[] { FlappySfx.RankUp }, audio.Played);
        }

        [Test]
        public void 밀리면_빨강_마이너스1과_낮은_음()
        {
            source.Next = f => Course(f, 5.0d, new RaceBirdSample("me", 0, 61f), new RaceBirdSample("a", 0, 60f));
            viewModel.Refresh();
            source.Next = f => Course(f, 5.1d, new RaceBirdSample("me", 0, 61f), new RaceBirdSample("a", 0, 62f));
            viewModel.Refresh();

            //  화면 글꼴(블랙한산스·주아)에 진짜 빼기(U+2212)가 없다 — 하이픈이어야 빈 네모가 안 뜬다.
            Assert.AreEqual("-1", viewModel.DeltaText);
            Assert.AreEqual(FlappyRacePalette.RankDown, viewModel.DeltaColor);
            CollectionAssert.AreEqual(new[] { FlappySfx.RankDown }, audio.Played);
        }

        [Test]
        public void 출발_2_5초_안엔_표시도_소리도_없다()
        {
            //  now 3.0·3.1초 = 출발 1.0·1.1초 — 출발 직후 줄 서기 소음(spec §5).
            source.Next = f => Course(f, 3.0d, new RaceBirdSample("me", 0, 5f), new RaceBirdSample("a", 0, 6f));
            viewModel.Refresh();
            source.Next = f => Course(f, 3.1d, new RaceBirdSample("me", 0, 7f), new RaceBirdSample("a", 0, 6f));
            viewModel.Refresh();

            Assert.AreEqual(string.Empty, viewModel.DeltaText);
            Assert.AreEqual(0, audio.Played.Count);
        }

        [Test]
        public void 추월_표시는_1_2초_동안_떠오르며_사라진다()
        {
            source.Next = f => Course(f, 5.0d, new RaceBirdSample("me", 0, 50f), new RaceBirdSample("a", 0, 60f));
            viewModel.Refresh();
            source.Next = f => Course(f, 5.1d, new RaceBirdSample("me", 0, 61f), new RaceBirdSample("a", 0, 60f));
            viewModel.Refresh();

            source.Next = f => Course(f, 5.7d, new RaceBirdSample("me", 0, 64f), new RaceBirdSample("a", 0, 60f));
            viewModel.Refresh();
            Assert.AreEqual(0.5f, viewModel.DeltaAlpha, 1e-4f);
            Assert.AreEqual(12f, viewModel.DeltaRise, 1e-3f, "0.6초 × 초당 20 px");

            source.Next = f => Course(f, 6.35d, new RaceBirdSample("me", 0, 67f), new RaceBirdSample("a", 0, 60f));
            viewModel.Refresh();
            Assert.AreEqual(string.Empty, viewModel.DeltaText, "1.25초 — 사라졌다");
            Assert.AreEqual(0f, viewModel.DeltaAlpha);
        }
    }
}
