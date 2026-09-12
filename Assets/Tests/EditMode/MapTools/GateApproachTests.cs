using System;
using System.Collections.Generic;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// 관문 앞 판단 진단의 <b>순수한 부분</b>을 못박는다 — 창 뽑기, 세 가설 분류, 섞였을 때의 비율,
    /// 그리고 갈림에서 별개 공간을 거르는 경계.
    ///
    /// <para>이 진단은 <b>판정이 아니라 측정</b>이다. 그래서 여기서 지키는 것은 "봇이 옳게
    /// 골랐나"가 아니라 <b>"기록이 가리키는 가설을 잘못 읽지 않나"</b>다 — 읽기를 틀리면
    /// 엉뚱한 규칙을 고치게 된다(겨냥 규칙을 짐작으로 고쳤다가 도달이 28%→3%로 떨어진 적이 있다).</para>
    /// </summary>
    public class GateApproachTests
    {
        //  굴려 보기가 "사실상 같은 거리"로 보는 폭(몸 지름 0.45×2). 실제 검사가 쓰는 값과 같다.
        const float SameReach = 0.9f;
        const float FaceX = 82f;

        static ApproachSample Guarded(int tick)
            //  가드가 막은 틱: 굴려 보지 않았으므로 두 갈래 값이 없다(0).
            => new ApproachSample(tick, 76f, -1.2f, -21.4f, wantsFlap: true, ceilingSafe: false,
                                  rolledOut: false, flapAliveTicks: 0, flapReachX: 0f,
                                  coastAliveTicks: 0, coastReachX: 0f, flapped: false);

        static ApproachSample FlapDies(int tick)
            //  둘 다 굴렸는데 누르는 갈래가 얼굴 열(82)에 못 닿고 죽는다.
            => new ApproachSample(tick, 78f, -6f, -12f, wantsFlap: true, ceilingSafe: true,
                                  rolledOut: true, flapAliveTicks: 8, flapReachX: 79.8f,
                                  coastAliveTicks: 20, coastReachX: 82.4f, flapped: false);

        static ApproachSample Tied(int tick)
            //  둘 다 창을 끝까지 살아남았다 — 전진이 상수라 도달 거리까지 같다.
            => new ApproachSample(tick, 78f, -6f, -12f, wantsFlap: false, ceilingSafe: true,
                                  rolledOut: true, flapAliveTicks: 60, flapReachX: 91.2f,
                                  coastAliveTicks: 60, coastReachX: 91.2f, flapped: false);

        static ApproachSample FlapWorse(int tick)
            => new ApproachSample(tick, 78f, -6f, -12f, wantsFlap: true, ceilingSafe: true,
                                  rolledOut: true, flapAliveTicks: 40, flapReachX: 90f,
                                  coastAliveTicks: 60, coastReachX: 91.2f, flapped: false);

        static ApproachSample Pressed(int tick)
            => new ApproachSample(tick, 78f, -6f, -12f, wantsFlap: true, ceilingSafe: true,
                                  rolledOut: true, flapAliveTicks: 60, flapReachX: 91.2f,
                                  coastAliveTicks: 12, coastReachX: 80.6f, flapped: true);

        static ApproachSample At(int tick, float x)
            => new ApproachSample(tick, x, -6f, -12f, wantsFlap: false, ceilingSafe: true,
                                  rolledOut: true, flapAliveTicks: 60, flapReachX: 91.2f,
                                  coastAliveTicks: 60, coastReachX: 91.2f, flapped: false);

        // ── 창 뽑기 ─────────────────────────────────────────────────────────

        [Test]
        public void Window_takes_the_last_ticks_before_entry()
        {
            var samples = new List<ApproachSample>();
            for (int i = 0; i < 50; i++)
            {
                samples.Add(At(i, 70f + i * 0.22f));
            }
            //  진입 = x가 80 이상이 되는 첫 틱.
            var window = GateApproachRule.Window(samples, gateStartX: 80f, windowTicks: 10);

            Assert.That(window.Count, Is.EqualTo(10));
            Assert.That(window[window.Count - 1].X, Is.LessThan(80f));
            Assert.That(window[0].Tick, Is.EqualTo(window[window.Count - 1].Tick - 9));
        }

        [Test]
        public void Window_is_empty_when_the_flight_never_reached_the_gate()
        {
            var samples = new List<ApproachSample> { At(0, 70f), At(1, 71f) };

            Assert.That(GateApproachRule.Window(samples, gateStartX: 80f, windowTicks: 30),
                        Is.Empty);
        }

        [Test]
        public void Window_clamps_to_the_start_of_a_short_flight()
        {
            var samples = new List<ApproachSample> { At(0, 79.6f), At(1, 79.8f), At(2, 80.1f) };

            var window = GateApproachRule.Window(samples, gateStartX: 80f, windowTicks: 30);

            Assert.That(window.Count, Is.EqualTo(2));
        }

        // ── 세 가설 ─────────────────────────────────────────────────────────

        [Test]
        public void Classify_calls_a_guarded_tick_guard_blocked()
        {
            Assert.That(GateApproachRule.Classify(Guarded(1), FaceX, SameReach),
                        Is.EqualTo(ApproachShape.GuardBlocked));
        }

        [Test]
        public void Classify_calls_a_short_flap_branch_flap_dies_short()
        {
            Assert.That(GateApproachRule.Classify(FlapDies(1), FaceX, SameReach),
                        Is.EqualTo(ApproachShape.FlapDiesShort));
        }

        [Test]
        public void Classify_calls_equal_branches_a_tie()
        {
            Assert.That(GateApproachRule.Classify(Tied(1), FaceX, SameReach),
                        Is.EqualTo(ApproachShape.Tie));
        }

        [Test]
        public void Classify_calls_a_shorter_lived_flap_branch_worse()
        {
            Assert.That(GateApproachRule.Classify(FlapWorse(1), FaceX, SameReach),
                        Is.EqualTo(ApproachShape.FlapWorse));
        }

        [Test]
        public void Classify_ignores_reach_differences_inside_the_same_reach_window()
        {
            //  0.5m 차이는 몸 지름(0.9m)보다 작다 — 굴려 보기가 동률로 보는 폭이라 여기서도 동률이다.
            var almost = new ApproachSample(1, 78f, -6f, -12f, wantsFlap: false, ceilingSafe: true,
                                            rolledOut: true, flapAliveTicks: 60, flapReachX: 90.7f,
                                            coastAliveTicks: 60, coastReachX: 91.2f, flapped: false);

            Assert.That(GateApproachRule.Classify(almost, FaceX, SameReach),
                        Is.EqualTo(ApproachShape.Tie));
        }

        [Test]
        public void Classify_calls_a_tick_the_bot_actually_pressed_flapped()
        {
            Assert.That(GateApproachRule.Classify(Pressed(1), FaceX, SameReach),
                        Is.EqualTo(ApproachShape.Flapped));
        }

        // ── 판정 ────────────────────────────────────────────────────────────

        static List<ApproachSample> Many(Func<int, ApproachSample> make, int count, int from = 0)
        {
            var samples = new List<ApproachSample>();
            for (int i = 0; i < count; i++)
            {
                samples.Add(make(from + i));
            }
            return samples;
        }

        [Test]
        public void Verdict_points_at_the_ceiling_guard_when_that_shape_dominates()
        {
            var samples = Many(Guarded, 28);
            samples.AddRange(Many(Pressed, 2, 28));

            ApproachTally tally = GateApproachRule.Tally(samples, FaceX, SameReach);

            Assert.That(tally.GuardBlocked, Is.EqualTo(28));
            Assert.That(tally.Flapped, Is.EqualTo(2));
            StringAssert.Contains("① 천장 가드가 막는다", GateApproachRule.Verdict(tally));
        }

        [Test]
        public void Verdict_points_at_being_too_low_when_flapping_also_dies()
        {
            ApproachTally tally = GateApproachRule.Tally(Many(FlapDies, 25), FaceX, SameReach);

            Assert.That(tally.FlapDiesShort, Is.EqualTo(25));
            StringAssert.Contains("② 이미 너무 낮다", GateApproachRule.Verdict(tally));
        }

        [Test]
        public void Verdict_points_at_the_evaluation_when_the_branches_tie()
        {
            ApproachTally tally = GateApproachRule.Tally(Many(Tied, 30), FaceX, SameReach);

            Assert.That(tally.Undecided, Is.EqualTo(30));
            StringAssert.Contains("③ 평가가 구별 못 한다", GateApproachRule.Verdict(tally));
        }

        [Test]
        public void Verdict_reports_the_mix_when_shapes_are_mixed()
        {
            var samples = Many(Guarded, 18);
            samples.AddRange(Many(Tied, 8, 18));
            samples.AddRange(Many(FlapDies, 4, 26));

            ApproachTally tally = GateApproachRule.Tally(samples, FaceX, SameReach);
            string verdict = GateApproachRule.Verdict(tally);

            Assert.That(tally.GuardBlocked, Is.EqualTo(18));
            Assert.That(tally.Undecided, Is.EqualTo(8));
            Assert.That(tally.FlapDiesShort, Is.EqualTo(4));
            Assert.That(tally.NotFlapped, Is.EqualTo(30));
            StringAssert.Contains("① 천장 가드가 막는다", verdict);
            StringAssert.Contains("안 누른 30틱 중", verdict);
            StringAssert.Contains("천장 가드가 막음 18틱", verdict);
            StringAssert.Contains("눌러도 관문 전에 죽음 4틱", verdict);
            StringAssert.Contains("평가가 구별 못 함 8틱", verdict);
        }

        [Test]
        public void Verdict_refuses_to_pick_when_two_shapes_tie_for_the_lead()
        {
            var samples = Many(Guarded, 10);
            samples.AddRange(Many(Tied, 10, 10));

            string verdict = GateApproachRule.Verdict(GateApproachRule.Tally(samples, FaceX, SameReach));

            StringAssert.Contains("하나로 안 갈린다", verdict);
            StringAssert.Contains("더 재야 한다", verdict);
        }

        [Test]
        public void Verdict_says_so_when_the_bot_pressed_on_every_tick()
        {
            string verdict = GateApproachRule.Verdict(
                GateApproachRule.Tally(Many(Pressed, 12), FaceX, SameReach));

            StringAssert.Contains("안 누른 틱이 없다", verdict);
        }

        // ── 절 ──────────────────────────────────────────────────────────────

        [Test]
        public void Section_prints_the_table_only_for_the_first_flights()
        {
            var approaches = new List<GateApproach>
            {
                new GateApproach
                {
                    Name = "PlayerSpawn_2", EntryY = -7.17f, EntryVerticalSpeed = -30f,
                    InFunnel = true, TicksAfterEntry = 14, Samples = Many(Guarded, 3),
                },
                new GateApproach
                {
                    Name = "시작 y=-8.0", EntryY = -7.22f, EntryVerticalSpeed = -30f,
                    InFunnel = true, TicksAfterEntry = 14, Samples = Many(Guarded, 3),
                },
            };

            string section = GateApproachRule.Section(80f, 83.5f, 82f, approaches, SameReach,
                                                      tableLimit: 1);

            StringAssert.Contains("관문 앞 판단", section);
            StringAssert.Contains("PlayerSpawn_2", section);
            StringAssert.Contains("깔때기 안", section);
            //  표 머리글은 딱 한 번(첫 비행)만 나온다.
            Assert.That(CountOrdinal(section, "누르고싶나"), Is.EqualTo(1));
            //  가드가 막은 틱은 굴려 보지 않았으므로 두 칸이 —다.
            Assert.That(CountOrdinal(section, "막힘"), Is.EqualTo(3));
            StringAssert.Contains("▶ 관문 전체 6틱", section);
        }

        //  StringAssert는 문화권 비교라 이모지·기호가 없는 문자열에도 매칭된다 — 세는 것은
        //  반드시 Ordinal로 한다.
        static int CountOrdinal(string text, string needle)
        {
            int count = 0;
            int at = 0;
            while ((at = text.IndexOf(needle, at, StringComparison.Ordinal)) >= 0)
            {
                count++;
                at += needle.Length;
            }
            return count;
        }

        // ── 갈림에서 별개 공간 거르기 ───────────────────────────────────────

        //  아치(4.012) + 몸(0.9). 이보다 두꺼운 칸막이는 넘어갈 수 없으므로 별개 공간이다.
        const float RequiredBand = 4.912001f;

        static StaticSplit SplitWithDivider(float divider)
            => new StaticSplit(80f, 83f, 2,
                               new List<GateWindow> { new GateWindow(-18f, -13f),
                                                      new GateWindow(-13f + divider, -8f + divider) },
                               divider, "칸막이", 3f);

        [Test]
        public void Splits_keep_a_divider_exactly_at_the_arch_plus_body()
        {
            var kept = StaticPinchRule.WithoutSeparateSpaces(
                new List<StaticSplit> { SplitWithDivider(RequiredBand) }, RequiredBand, out int removed);

            Assert.That(removed, Is.EqualTo(0));
            Assert.That(kept.Count, Is.EqualTo(1));
        }

        [Test]
        public void Splits_drop_a_divider_just_over_the_arch_plus_body()
        {
            var kept = StaticPinchRule.WithoutSeparateSpaces(
                new List<StaticSplit> { SplitWithDivider(RequiredBand + 0.01f) },
                RequiredBand, out int removed);

            Assert.That(removed, Is.EqualTo(1));
            Assert.That(kept, Is.Empty);
        }

        [Test]
        public void Splits_drop_only_the_separate_spaces()
        {
            var splits = new List<StaticSplit>
            {
                SplitWithDivider(0.5f), SplitWithDivider(28.1f), SplitWithDivider(22.8f),
                SplitWithDivider(11f), SplitWithDivider(3f),
            };

            var kept = StaticPinchRule.WithoutSeparateSpaces(splits, RequiredBand, out int removed);

            Assert.That(removed, Is.EqualTo(3));
            Assert.That(kept.Count, Is.EqualTo(2));
        }

        [Test]
        public void Section_says_how_many_separate_spaces_were_dropped()
        {
            string section = StaticPinchRule.Section(
                new List<StaticPinch>(), RequiredBand, bodyHeight: 0.9f, forwardSpeed: 11f,
                gravity: 70f, sampleStep: 0.5f,
                splits: new List<StaticSplit> { SplitWithDivider(0.5f) }, separateSpaces: 3);

            StringAssert.Contains("갈림 1곳 (별개 공간 3곳 제외)", section);
        }

        [Test]
        public void Section_still_says_it_measured_when_every_split_was_a_separate_space()
        {
            string section = StaticPinchRule.Section(
                new List<StaticPinch>(), RequiredBand, bodyHeight: 0.9f, forwardSpeed: 11f,
                gravity: 70f, sampleStep: 0.5f,
                splits: new List<StaticSplit>(), separateSpaces: 2);

            StringAssert.Contains("갈림 0곳 (별개 공간 2곳 제외)", section);
        }
    }
}
