using System;
using System.Collections.Generic;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// 정적 지형의 좁힘을 세 등급(✅ 보장 / ⚠️ 가능 / ❌ 불가)으로 가르는 산술을 못박는다.
    /// 씬도 물리도 안 쓴다 — 여기서 재는 것은 <b>규칙</b>이지 맵이 아니다.
    /// </summary>
    public class StaticPinchTests
    {
        //  물리 표본 — 규칙을 재려고 얼려 둔 값이다(게임이 날갯짓 23 / 중력 70을 쓰던 시절 수치).
        //  아래 경계값들을 이 수치로 손으로 풀어 놨으니 여기만 고치면 그 값들이 통째로 틀려진다.
        //  지금 게임 값은 MasterData(TbFlappyConfig)에 있고 프로덕션은 거기서 읽어 기준을 그때그때
        //  계산하므로, 이 상수가 게임과 달라도 맵 검사 결과는 옳다.
        const float FlapImpulse = 23f;
        const float Gravity = 70f;
        const float TickSeconds = 0.02f;
        const float BodyHeight = 0.9f;
        const float ForwardSpeed = 11f;
        const float SampleStep = 0.5f;

        static float Required => StaticPinchRule.RequiredBand(FlapImpulse, Gravity, TickSeconds, BodyHeight);

        static StaticPinch Pinch(float band, float length, float startX = 0f)
            => new StaticPinch(startX, startX + length, band, bandBottom: -18.4f,
                               bandTop: -18.4f + band, floor: "ComposedMap/Cube", ceiling: "ComposedMap/Cube");

        static PinchVerdict Judge(in StaticPinch pinch)
            => StaticPinchRule.Judge(pinch, Required, BodyHeight, ForwardSpeed, Gravity);

        static string Section(params StaticPinch[] pinches)
            => StaticPinchRule.Section(pinches, Required, BodyHeight, ForwardSpeed, Gravity, SampleStep);

        static string Section(IReadOnlyList<StaticSplit> splits, params StaticPinch[] pinches)
            => StaticPinchRule.Section(pinches, Required, BodyHeight, ForwardSpeed, Gravity,
                                       SampleStep, splits);

        // ── 갈림 ────────────────────────────────────────────────────────────

        const float SplitMinLength = 1f;

        static GateWindow W(float bottom, float top, string floor = "ComposedMap/Cube",
                            string ceiling = "ComposedMap/Cube")
            => new GateWindow(bottom, top, floor, ceiling);

        //  x 80.0~83.5가 3.16m 칸막이로 두 창(각 5.03m)으로 갈린 회랑. 앞뒤는 창 하나짜리
        //  17.3m 회랑이다 — 접고 보면 둘이 같은 등급을 받는, 바로 그 실측 모양이다.
        static List<GateColumn> SplitCourse()
        {
            var one = new[] { W(-19.75f, -2.45f) };
            var two = new[] { W(-18.43f, -13.40f), W(-10.24f, -5.21f) };
            var columns = new List<GateColumn>();
            for (float x = 79f; x <= 85f + 1e-4f; x += SampleStep)
            {
                columns.Add(new GateColumn(x, x >= 80f - 1e-4f && x <= 83.5f + 1e-4f ? two : one));
            }
            return columns;
        }

        static List<StaticSplit> Splits(List<GateColumn> columns)
            => StaticPinchRule.Splits(columns, BodyHeight, SampleStep, SplitMinLength);

        [Test]
        public void 창이_둘로_갈린_구간을_따로_찍는다()
        {
            List<StaticSplit> splits = Splits(SplitCourse());
            Assert.AreEqual(1, splits.Count, "80.0~83.5 한 구간이다 — 앞뒤 창 하나짜리는 갈림이 아니다.");
            Assert.AreEqual(80f, splits[0].StartX, 1e-3f);
            Assert.AreEqual(83.5f, splits[0].EndX, 1e-3f);
            Assert.AreEqual(2, splits[0].WindowCount);
            //  표본 8개 × 0.5m — 좁힘과 같은 보수적 셈이다.
            Assert.AreEqual(4f, splits[0].Length, 1e-3f);
            Assert.AreEqual(3.16f, splits[0].Divider, 0.005f, "칸막이 두께 = 아래 창 천장 ~ 위 창 바닥.");
            Assert.AreEqual("ComposedMap/Cube", splits[0].DividerName);
        }

        [Test]
        public void 창이_하나뿐이면_갈림이_아니다()
        {
            var one = new[] { W(-19.75f, -2.45f) };
            var columns = new List<GateColumn>();
            for (float x = 0f; x < 10f; x += SampleStep)
            {
                columns.Add(new GateColumn(x, one));
            }
            Assert.AreEqual(0, Splits(columns).Count);
            Assert.AreEqual(0, StaticPinchRule.Splits(null, BodyHeight, SampleStep, SplitMinLength).Count,
                            "아예 안 훑은 경우(null)도 빈 결과여야 한다.");
        }

        [Test]
        public void 몸이_안_들어가는_틈은_창으로_세지_않는다()
        {
            //  둘째 '창'이 0.5m라 몸(0.9m)이 안 들어간다 — 새가 고를 수 있는 자리가 아니므로
            //  이 회랑은 갈린 것이 아니다. 이 줄이 없으면 손가락만 한 틈이 갈림을 만든다.
            var decoy = new[] { W(-19.75f, -2.45f), W(0f, 0.5f) };
            var columns = new List<GateColumn>();
            for (float x = 0f; x < 10f; x += SampleStep)
            {
                columns.Add(new GateColumn(x, decoy));
            }
            Assert.AreEqual(0, Splits(columns).Count);
        }

        [Test]
        public void 표본_한_칸만_갈린_것은_버린다()
        {
            //  창이 붙는 경계를 스친 한 칸짜리는 가로막은 칸막이가 아니다.
            var one = new[] { W(-19.75f, -2.45f) };
            var two = new[] { W(-18.43f, -13.40f), W(-10.24f, -5.21f) };
            var columns = new List<GateColumn>
            {
                new GateColumn(0f, one),
                new GateColumn(0.5f, two),
                new GateColumn(1f, one),
            };
            Assert.AreEqual(0, Splits(columns).Count, "0.5m < 최소 1m이라 버려야 한다.");
            //  두 칸이면 센다 — 문턱이 실제로 그 자리에 있다는 증거(없으면 위 단언이 공짜다).
            columns.Insert(2, new GateColumn(0.75f, two));
            Assert.AreEqual(1, StaticPinchRule.Splits(columns, BodyHeight, SampleStep, SplitMinLength).Count);
        }

        [Test]
        public void 갈렸다_붙었다_하면_구간이_끊긴다()
        {
            var one = new[] { W(-19.75f, -2.45f) };
            var two = new[] { W(-18.43f, -13.40f), W(-10.24f, -5.21f) };
            var columns = new List<GateColumn>
            {
                new GateColumn(0f, two), new GateColumn(0.5f, two),
                new GateColumn(1f, one),
                new GateColumn(1.5f, two), new GateColumn(2f, two),
            };
            List<StaticSplit> splits = Splits(columns);
            Assert.AreEqual(2, splits.Count, "붙은 열에서 끊긴다 — 억지로 이으면 회랑 절반이 한 구간이 된다.");
            Assert.AreEqual(0f, splits[0].StartX, 1e-3f);
            Assert.AreEqual(1.5f, splits[1].StartX, 1e-3f);
        }

        [Test]
        public void 갈림은_등급을_바꾸지_않고_사실만_덧붙인다()
        {
            //  좁은 구간이 하나도 없는(= 전부 ✅인) 코스에도 갈림은 찍혀야 한다 — 그게 이 절의
            //  맹점이었다. 등급 문구는 그대로 두고 갈림 줄만 는다.
            string text = Section(Splits(SplitCourse()));
            Assert.IsTrue(text.IndexOf("좁은 구간 없음", StringComparison.Ordinal) >= 0,
                          "등급은 여전히 <가장 넓은 창> 기준 그대로다.");
            Assert.IsTrue(text.IndexOf("갈림 1곳", StringComparison.Ordinal) >= 0);
            Assert.IsTrue(text.IndexOf("창 2개", StringComparison.Ordinal) >= 0);
            Assert.IsTrue(text.IndexOf("칸막이 3.2m", StringComparison.Ordinal) >= 0);
            Assert.IsTrue(text.IndexOf("막는 것: ComposedMap/Cube", StringComparison.Ordinal) >= 0);
            Assert.IsTrue(text.IndexOf("-18.43~-13.40 / -10.24~-5.21", StringComparison.Ordinal) >= 0,
                          "창이 둘이라는 말만으로는 어디가 갈렸는지 못 고친다.");
        }

        [Test]
        public void 갈림이_없으면_갈림_줄도_없다()
        {
            string text = Section(new List<StaticSplit>());
            Assert.IsTrue(text.IndexOf("갈림", StringComparison.Ordinal) < 0,
                          "없는 것을 0곳이라고 찍고 주의까지 붙이면 절이 길어지기만 한다.");
            Assert.IsTrue(text.IndexOf("⚠ x", StringComparison.Ordinal) < 0);
        }

        [Test]
        public void 절이_갈림의_어려움을_스스로_말한다()
        {
            string text = Section(Splits(SplitCourse()));
            Assert.IsTrue(text.IndexOf("폭이 넉넉해도 갈림은 어렵다", StringComparison.Ordinal) >= 0);
            Assert.IsTrue(text.IndexOf("관문 통과·깔때기", StringComparison.Ordinal) >= 0,
                          "어느 창인지는 ①이 답한다는 것을 여기서 가리켜야 한다.");
        }

        // ── 기준 ────────────────────────────────────────────────────────────

        [Test]
        public void 기준은_2b와_같은_아치_더하기_몸이다()
        {
            //  숫자를 박지 않았다는 증거 — 위 표본 물리값을 넣으면 4.912가 나온다.
            Assert.AreEqual(4.912f, Required, 0.0005f);
            Assert.AreEqual(ObstaclePlacementRule.RequiredBand(FlapImpulse, Gravity, TickSeconds, BodyHeight),
                            Required, 1e-6f, "②-b와 기준이 갈리면 같은 원리라고 말할 수 없다.");
        }

        // ── L_max 공식 ──────────────────────────────────────────────────────

        [Test]
        public void L_max는_전진속도와_체공시간의_곱이다()
        {
            //  밴드 3.60 → R=2.70 → t=√(8·2.70/70)=0.5555초 → 11×0.5555 = 6.11m.
            Assert.AreEqual(6.110f, StaticPinchRule.MaxLength(3.60f, BodyHeight, ForwardSpeed, Gravity), 0.005f);
        }

        [Test]
        public void 중력이_세지면_L_max가_줄고_전진이_빨라지면_늘어난다()
        {
            float baseline = StaticPinchRule.MaxLength(3.60f, BodyHeight, ForwardSpeed, Gravity);
            //  중력 4배 → 체공 절반 → L_max 절반. 상수로 박으면 이 단언이 빨강이 된다.
            Assert.AreEqual(baseline * 0.5f,
                            StaticPinchRule.MaxLength(3.60f, BodyHeight, ForwardSpeed, Gravity * 4f), 1e-3f);
            //  전진 2배 → L_max 2배.
            Assert.AreEqual(baseline * 2f,
                            StaticPinchRule.MaxLength(3.60f, BodyHeight, ForwardSpeed * 2f, Gravity), 1e-3f);
        }

        [Test]
        public void 밴드가_몸높이보다_좁으면_R이_음수라_L_max는_0이다()
        {
            //  몸조차 안 들어간다 — 어떤 길이도 못 지난다. 음수 R을 그대로 √에 넣으면 NaN이 되고,
            //  NaN은 모든 비교가 거짓이라 조용히 ⚠️(통과 가능)로 새어 나간다.
            Assert.AreEqual(0f, StaticPinchRule.MaxLength(0.5f, BodyHeight, ForwardSpeed, Gravity));
            Assert.AreEqual(0f, StaticPinchRule.MaxLength(0f, BodyHeight, ForwardSpeed, Gravity));
            Assert.AreEqual(0f, StaticPinchRule.MaxLength(BodyHeight, BodyHeight, ForwardSpeed, Gravity),
                            "딱 몸높이면 여유가 0이라 아예 못 뜬다.");

            PinchVerdict verdict = Judge(Pinch(band: 0.5f, length: 2f));
            Assert.AreEqual(PinchGrade.Impossible, verdict.Grade);
            Assert.AreEqual(0f, verdict.MaxLength);
            Assert.AreEqual(2f, verdict.ShortenBy, 1e-4f, "줄일 양은 길이 전부다 — 0m가 돼야 한다.");
        }

        // ── 세 등급 ─────────────────────────────────────────────────────────

        [Test]
        public void 밴드가_기준_이상이면_길이와_무관하게_보장이다()
        {
            Assert.AreEqual(PinchGrade.Guaranteed, Judge(Pinch(Required, length: 0.5f)).Grade,
                            "딱 기준만큼이면 아치가 밴드 안에 정확히 들어간다.");
            Assert.AreEqual(PinchGrade.Guaranteed, Judge(Pinch(Required + 2f, length: 500f)).Grade,
                            "넓으면 아무리 길어도 높이를 유지하며 지난다.");
            Assert.AreEqual(0f, Judge(Pinch(Required, length: 500f)).WidenBy);
            Assert.AreEqual(0f, Judge(Pinch(Required, length: 500f)).ShortenBy);
        }

        [Test]
        public void 좁아도_L_max_이하면_가능이고_넘으면_불가다()
        {
            //  밴드 3.60의 L_max는 6.110m다. 그 경계 양쪽에서 등급이 갈려야 한다.
            float limit = StaticPinchRule.MaxLength(3.60f, BodyHeight, ForwardSpeed, Gravity);

            Assert.AreEqual(PinchGrade.Possible, Judge(Pinch(3.60f, limit - 0.01f)).Grade);
            Assert.AreEqual(PinchGrade.Possible, Judge(Pinch(3.60f, limit)).Grade,
                            "딱 L_max면 포물선 끝이 출구에 닿는다 — 가능 쪽이다.");
            Assert.AreEqual(PinchGrade.Impossible, Judge(Pinch(3.60f, limit + 0.01f)).Grade);
        }

        [Test]
        public void 처방은_기준과_L_max에서_바로_나온_산술이다()
        {
            float limit = StaticPinchRule.MaxLength(3.60f, BodyHeight, ForwardSpeed, Gravity);
            PinchVerdict impossible = Judge(Pinch(3.60f, length: 12f));

            Assert.AreEqual(Required - 3.60f, impossible.WidenBy, 1e-4f);
            Assert.AreEqual(1.312f, impossible.WidenBy, 0.0005f);
            Assert.AreEqual(12f - limit, impossible.ShortenBy, 1e-4f);
            Assert.AreEqual(5.890f, impossible.ShortenBy, 0.005f);

            //  ⚠️는 이미 길이가 충분히 짧으므로 줄일 것이 없다 — 넓히는 처방만 남는다.
            PinchVerdict possible = Judge(Pinch(3.60f, length: 2f));
            Assert.AreEqual(0f, possible.ShortenBy);
            Assert.AreEqual(Required - 3.60f, possible.WidenBy, 1e-4f);
        }

        [Test]
        public void 넓힌_뒤에는_보장이_되고_줄인_뒤에는_가능이_된다()
        {
            //  처방을 그대로 따랐을 때 실제로 그 등급이 되는지 — 처방이 말뿐인지 아닌지의 시험.
            StaticPinch bad = Pinch(3.60f, length: 12f);
            PinchVerdict verdict = Judge(bad);

            //  1mm의 여유는 float 반올림용이다 — 처방을 딱 맞춰 되먹이면 마지막 비트에서 갈린다.
            Assert.AreEqual(PinchGrade.Guaranteed,
                            Judge(Pinch(3.60f + verdict.WidenBy + 0.001f, length: 12f)).Grade);
            Assert.AreEqual(PinchGrade.Possible,
                            Judge(Pinch(3.60f, length: 12f - verdict.ShortenBy - 0.001f)).Grade);
        }

        // ── 한 x를 값 하나로 접기 ────────────────────────────────────────────

        static FreeBand Enclosed(float bottom, float top, string floor = "Fill/Ground", string ceiling = "Fill/Roof")
            => new FreeBand(bottom, top, floor, ceiling, openBelow: false, openAbove: false);

        [Test]
        public void 갇힌_띠_중_가장_넓은_것이_그_x의_밴드다()
        {
            PinchColumn column = StaticPinchRule.Column(82f, new List<FreeBand>
            {
                Enclosed(-18.4f, -13.9f),   // 4.50
                Enclosed(-9.7f, -5.2f, "Fill/Pillar", "Fill/Roof"),  // 4.50
                Enclosed(20f, 26f, "A", "B"),                        // 6.00 — 더 넓다
            });
            Assert.IsFalse(column.Open);
            Assert.AreEqual(6.00f, column.Band, 1e-4f);
            Assert.AreEqual(20f, column.BandBottom, 1e-4f);
            Assert.AreEqual("A", column.Floor);
            Assert.AreEqual("B", column.Ceiling);
        }

        [Test]
        public void 하늘로_트인_띠는_밴드로_안_세지만_그_x를_통과로_만들지도_않는다()
        {
            //  실측 x=366의 모양이다 — 아래 허공 · 좁은 창 둘 · 위로 열린 하늘.
            //  트인 띠가 있다고 그 x를 건너뛰면 FillPinchTop의 좁힘을 통째로 놓친다.
            PinchColumn column = StaticPinchRule.Column(366f, new List<FreeBand>
            {
                new FreeBand(-79f, -75.8f, null, "Fill/Floor", openBelow: true, openAbove: false),
                Enclosed(-49.1f, -44.5f, "Fill/FillPinchBottom", "Fill/FillPinchTop"),
                Enclosed(-43.1f, -38.4f, "Fill/FillPinchTop", "ComposedMap/Cube"),
                new FreeBand(-11.8f, 36f, "ComposedMap/Cube", null, openBelow: false, openAbove: true),
            });
            Assert.IsFalse(column.Open, "갇힌 회랑이 있으면 하늘이 뚫려 있어도 좁힘을 본다.");
            Assert.AreEqual(4.70f, column.Band, 1e-4f, "갇힌 띠 둘 중 넓은 쪽(-43.1~-38.4)이다.");
            Assert.AreEqual("Fill/FillPinchTop", column.Floor);
        }

        [Test]
        public void 갇힌_띠가_없으면_트인_것과_통째로_막힌_것을_가른다()
        {
            //  ②-b가 겪은 것과 같은 함정이다: 둘 다 "갇힌 띠 0개"라 숫자로는 똑같이 0m인데,
            //  하나는 통과 가능(하늘)이고 하나는 통과 불가(벽)다. 뭉치면 열린 하늘을 벽으로
            //  보고하고 엉뚱한 데를 고치게 된다.
            PinchColumn sky = StaticPinchRule.Column(500f, new List<FreeBand>
            {
                new FreeBand(-8.9f, 36f, "ComposedMap/Cube", null, openBelow: false, openAbove: true),
            });
            Assert.IsTrue(sky.Open);
            Assert.AreEqual(0f, sky.Band);

            PinchColumn solid = StaticPinchRule.Column(500f, new List<FreeBand>());
            Assert.IsFalse(solid.Open, "빈틈이 하나도 없는 x는 벽이다 — 통과로 세면 안 된다.");
            Assert.AreEqual(0f, solid.Band);

            var columns = new List<PinchColumn> { sky, solid };
            List<StaticPinch> pinches = StaticPinchRule.Segments(columns, Required, SampleStep);
            Assert.AreEqual(1, pinches.Count, "하늘은 좁힘이 아니고 벽만 좁힘이다.");
            Assert.AreEqual(PinchGrade.Impossible, Judge(pinches[0]).Grade);
        }

        // ── 연속 구간으로 묶기 ───────────────────────────────────────────────

        static PinchColumn Narrow(float x, float band)
            => new PinchColumn(x, band, -18.4f, -18.4f + band, "ComposedMap/Cube", "ComposedMap/Cube", open: false);

        static PinchColumn Wide(float x)
            => new PinchColumn(x, 12f, -6f, 6f, "ComposedMap/Cube", "ComposedMap/Cube", open: false);

        [Test]
        public void 이어진_x들이_한_구간이_되고_넓은_x에서_끊긴다()
        {
            var columns = new List<PinchColumn>
            {
                Wide(80f), Narrow(80.5f, 4.5f), Narrow(81f, 3.6f), Narrow(81.5f, 4.5f),
                Wide(82f), Narrow(82.5f, 2f),
            };
            List<StaticPinch> pinches = StaticPinchRule.Segments(columns, Required, SampleStep);

            Assert.AreEqual(2, pinches.Count);
            //  표본 셋 × 간격 0.5 = 1.5m. 표본 하나가 덮는 칸까지 보수적으로 센다.
            Assert.AreEqual(80.25f, pinches[0].StartX, 1e-4f);
            Assert.AreEqual(81.75f, pinches[0].EndX, 1e-4f);
            Assert.AreEqual(1.5f, pinches[0].Length, 1e-4f);
            //  구간의 밴드는 그 안 최솟값이다 — 넓은 쪽을 쓰면 "지나갈 수 있다"고 거짓말한다.
            Assert.AreEqual(3.6f, pinches[0].Band, 1e-4f);
            //  표본 하나짜리 구간도 길이가 0이 아니다(0이면 무조건 ⚠️가 된다).
            Assert.AreEqual(0.5f, pinches[1].Length, 1e-4f);
        }

        [Test]
        public void 트인_x는_구간을_끊는다()
        {
            var open = new PinchColumn(81f, 0f, 0f, 0f, null, null, open: true);
            var columns = new List<PinchColumn> { Narrow(80.5f, 3.6f), open, Narrow(81.5f, 3.6f) };
            Assert.AreEqual(2, StaticPinchRule.Segments(columns, Required, SampleStep).Count);
        }

        [Test]
        public void 끝까지_좁으면_마지막_구간도_닫힌다()
        {
            //  루프가 리스트 끝에서 구간을 안 닫으면 이 구간이 통째로 사라진다.
            var columns = new List<PinchColumn> { Wide(80f), Narrow(80.5f, 3.6f), Narrow(81f, 3.6f) };
            List<StaticPinch> pinches = StaticPinchRule.Segments(columns, Required, SampleStep);
            Assert.AreEqual(1, pinches.Count);
            Assert.AreEqual(1f, pinches[0].Length, 1e-4f);
        }

        [Test]
        public void 좁은_x가_없으면_구간도_없다()
        {
            Assert.AreEqual(0, StaticPinchRule.Segments(
                new List<PinchColumn> { Wide(80f), Wide(80.5f) }, Required, SampleStep).Count);
            Assert.AreEqual(0, StaticPinchRule.Segments(null, Required, SampleStep).Count);
        }

        // ── 절 ──────────────────────────────────────────────────────────────

        [Test]
        public void 절은_통과불가에_두_처방을_다_적는다()
        {
            //  기호는 Ordinal로 찾는다 — StringAssert는 문화권 비교라 없는 기호에도 매칭된다.
            string section = Section(Pinch(3.60f, length: 12f));

            //  줄마다 앞머리가 붙으므로 "❌ x"로 찾는다 — 맨 위 등급 수 줄에는 세 기호가 다 나와서,
            //  기호만 찾으면 어떤 등급이 찍혔는지 못 가른다.
            Assert.GreaterOrEqual(section.IndexOf("❌ x", StringComparison.Ordinal), 0);
            Assert.Less(section.IndexOf("⚠️ x", StringComparison.Ordinal), 0);
            Assert.GreaterOrEqual(section.IndexOf("밴드를 1.31m 넓히면 ✅", StringComparison.Ordinal), 0);
            Assert.GreaterOrEqual(section.IndexOf("구간을 5.89m 줄이면 ⚠️", StringComparison.Ordinal), 0);
            Assert.GreaterOrEqual(section.IndexOf("가장 넓은 밴드 3.60m", StringComparison.Ordinal), 0);
            Assert.GreaterOrEqual(section.IndexOf("L_max 6.11m", StringComparison.Ordinal), 0);
            Assert.GreaterOrEqual(section.IndexOf("막는 것: 밑 ComposedMap/Cube · 위 ComposedMap/Cube",
                                                 StringComparison.Ordinal), 0);
        }

        [Test]
        public void 절은_가능하나보장아님에_줄이기_처방을_안_적는다()
        {
            //  이미 짧아서 ⚠️인 구간에 "더 줄여라"를 적으면 안 쓸 손잡이를 쓰게 만든다.
            string section = Section(Pinch(3.60f, length: 2f));

            Assert.GreaterOrEqual(section.IndexOf("⚠️ x", StringComparison.Ordinal), 0);
            Assert.Less(section.IndexOf("❌ x", StringComparison.Ordinal), 0);
            Assert.GreaterOrEqual(section.IndexOf("밴드를 1.31m 넓히면 ✅", StringComparison.Ordinal), 0);
            Assert.Less(section.IndexOf("줄이면", StringComparison.Ordinal), 0);
        }

        [Test]
        public void 절은_보장된_구간에_처방을_안_적는다()
        {
            string section = Section(Pinch(Required + 0.5f, length: 30f));

            Assert.GreaterOrEqual(section.IndexOf("✅ x", StringComparison.Ordinal), 0);
            Assert.Less(section.IndexOf("❌ x", StringComparison.Ordinal), 0);
            Assert.Less(section.IndexOf("넓히면", StringComparison.Ordinal), 0);
        }

        [Test]
        public void 절은_등급을_세어_머리말에_적는다()
        {
            string section = Section(Pinch(3.60f, 12f, startX: 80f), Pinch(3.60f, 2f, startX: 300f));
            Assert.GreaterOrEqual(
                section.IndexOf("좁은 구간 2개 — ❌ 통과 불가 1개 · ⚠️ 가능하나 보장 아님 1개 · ✅ 보장 0개",
                                StringComparison.Ordinal), 0);
        }

        [Test]
        public void 절은_밴드가_아예_없는_구간을_0m가_아니라_말로_적는다()
        {
            string section = Section(new StaticPinch(80f, 82f, 0f, 0f, 0f, null, null));
            Assert.GreaterOrEqual(section.IndexOf("자유 밴드 없음 — 이 구간은 통째로 막혀 있다",
                                                  StringComparison.Ordinal), 0);
            Assert.GreaterOrEqual(section.IndexOf("막는 것: 밑 (모름) · 위 (모름)", StringComparison.Ordinal), 0);
        }

        [Test]
        public void 절은_좁은_구간이_없을_때_그렇게_말한다()
        {
            //  빈 절을 찍으면 "재 봤더니 없다"가 아니라 "여기도 봐야 한다"로 잘못 읽힌다.
            string section = StaticPinchRule.Section(new List<StaticPinch>(), Required, BodyHeight,
                                                     ForwardSpeed, Gravity, SampleStep);
            Assert.GreaterOrEqual(section.IndexOf("좁은 구간 없음", StringComparison.Ordinal), 0);
            Assert.Less(section.IndexOf("❌", StringComparison.Ordinal), 0);
        }

        [Test]
        public void 절은_보장하지_않는_것을_스스로_밝힌다()
        {
            string section = Section(Pinch(3.60f, length: 12f));

            //  ②-b와 같은 주의 문구 — 밴드가 있다는 것과 거기 갈 수 있다는 것은 다르다.
            Assert.GreaterOrEqual(
                section.IndexOf("밴드가 있다는 것과 새가 거기 도달할 수 있다는 것은 다르다",
                                StringComparison.Ordinal), 0);
            //  L_max가 무엇을 전제하는지도 밝힌다.
            Assert.GreaterOrEqual(section.IndexOf("*이상적인 진입*을 전제한다", StringComparison.Ordinal), 0);
            //  숫자를 박지 않았다는 것이 절에도 드러나야 한다 — 물리값이 그대로 찍힌다.
            Assert.GreaterOrEqual(section.IndexOf("아치+몸(4.91m)", StringComparison.Ordinal), 0);
            Assert.GreaterOrEqual(section.IndexOf("√(8R/70)", StringComparison.Ordinal), 0);
        }
    }
}
