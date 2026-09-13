using System;
using System.Collections.Generic;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// <b>처방</b>의 순수한 부분 — "지형을 Δ만큼 바꾸면 진입 상태 몇 개가 깔때기 안에 들어오나" —
    /// 을 못박는다. 씬도 물리 엔진도 안 쓴다.
    ///
    /// <para>경계값은 <b>손으로 푼 값</b>이다. 칸막이 처방의 문턱이 정확히 <i>두께</i>인 이유는
    /// 기하로 닫힌다: 칸막이가 남아 있는 한 아래 창의 발 최고점과 위 창의 발 최저점 사이가
    /// 한 틱에 갈 수 있는 거리(종단낙하 30 × 0.02 = 0.6m)보다 멀어 건너뛸 수 없고, 두께만큼
    /// 깎으면 두 창이 하나로 합쳐져 <b>넘을 것 자체가 없어진다</b>. 그 사이엔 답이 없다.</para>
    /// </summary>
    public class GatePrescriptionTests
    {
        //  실제 Flappy 값. 여기를 고치면 다른 숫자를 재는 것이다.
        const float ForwardSpeed = 11f;
        const float Gravity = 70f;
        const float MaxFallSpeed = 30f;
        const float FlapImpulse = 23f;
        const float TickSeconds = 0.02f;
        const float BodyHeight = 0.9f;

        static FlightKernel Kernel => new FlightKernel(ForwardSpeed, Gravity, MaxFallSpeed,
                                                       FlapImpulse, TickSeconds, BodyHeight);

        static GateWindow Window(float bottom, float top) => new GateWindow(bottom, top, "밑", "위");

        //  관문 뒤는 안 본다 — 여기서 재는 것은 관문 <안>의 기하가 Δ에 어떻게 반응하나지,
        //  관문 뒤 천장이 아니다.
        static readonly List<GateColumn> NoRunout = null;

        //  창이 x에 따라 변하지 않는 관문. 기하가 x와 무관해야 "몇 번째 틱에 어디"만 남아
        //  손으로 풀 수 있다.
        static List<GateColumn> Uniform(params GateWindow[] windows)
            => new List<GateColumn>
            {
                new GateColumn(0f, windows),
                new GateColumn(1f, windows),
                new GateColumn(2f, windows),
                new GateColumn(3f, windows),
            };

        //  칸막이 두께 2.0m로 갈린 관문. 아래 창에서 발이 가장 높이 갈 수 있는 곳은 1.10,
        //  위 창의 발 최저점은 4.00 — 2.90m 떨어져 있어 한 틱(최대 0.6m)으로는 못 건넌다.
        static List<GateColumn> SplitGate() => Uniform(Window(0f, 2f), Window(4f, 5.3f));

        //  위 창에서 출발하는 진입 상태 둘. 위 창은 높이 1.3m라 날갯짓 한 틱(+0.46m)이 천장을
        //  뚫으므로 그 안에 머물 수 없고, 칸막이가 있는 한 아래 창으로도 못 내려간다.
        static readonly List<GateEntry> SplitEntries = new List<GateEntry>
        {
            new GateEntry(4.0f, 0f),
            new GateEntry(4.1f, 0f),
        };

        static int CountAt(GateEditKind kind, float delta, List<GateColumn> columns,
                           List<GateEntry> entries)
        {
            var edited = GatePrescriptionRule.Edit(columns, kind, delta, columns[0].X);
            return GatePrescriptionRule.CountInFunnel(entries, edited, NoRunout, Kernel, already: null);
        }

        static bool Has(string text, string piece)
            => text.IndexOf(piece, StringComparison.Ordinal) >= 0;

        // ── 지형을 Δ만큼 옮기기 ─────────────────────────────────────────────

        [Test]
        public void 칸막이를_깎으면_양쪽_창이_가운데로_똑같이_자란다()
        {
            var thinned = GatePrescriptionRule.Edit(
                new[] { Window(0f, 2f), Window(4f, 5.3f) }, GateEditKind.DividerThin, 1f, 0f, 0f);
            Assert.AreEqual(2, thinned.Count, "아직 두께 1m가 남아 있으니 두 창이다.");
            Assert.AreEqual(2.5f, thinned[0].Top, 1e-4f, "아래 창의 천장이 Δ/2만큼 올라간다.");
            Assert.AreEqual(3.5f, thinned[1].Bottom, 1e-4f, "위 창의 바닥이 Δ/2만큼 내려온다.");
            Assert.AreEqual(0f, thinned[0].Bottom, 1e-4f, "바깥 경계(바닥)는 안 움직인다.");
            Assert.AreEqual(5.3f, thinned[1].Top, 1e-4f, "바깥 경계(천장)도 안 움직인다.");
        }

        [Test]
        public void 두께만큼_깎으면_두_창이_하나로_합쳐진다()
        {
            var merged = GatePrescriptionRule.Edit(
                new[] { Window(0f, 2f), Window(4f, 5.3f) }, GateEditKind.DividerThin, 2f, 0f, 0f);
            Assert.AreEqual(1, merged.Count, "칸막이가 사라졌으니 창이 하나다.");
            Assert.AreEqual(0f, merged[0].Bottom, 1e-4f);
            Assert.AreEqual(5.3f, merged[0].Top, 1e-4f);
        }

        [Test]
        public void 천장_올리기는_가장_높은_창만_올린다()
        {
            var raised = GatePrescriptionRule.Edit(
                new[] { Window(0f, 2f), Window(4f, 5.3f) }, GateEditKind.CeilingRaise, 1.5f, 0f, 0f);
            Assert.AreEqual(2f, raised[0].Top, 1e-4f, "칸막이는 그대로 둔다.");
            Assert.AreEqual(4f, raised[1].Bottom, 1e-4f);
            Assert.AreEqual(6.8f, raised[1].Top, 1e-4f);
        }

        [Test]
        public void 기울기_줄이기는_입구에서_0이고_앞으로_갈수록_커진다()
        {
            var windows = new[] { Window(0f, 5f) };
            var atPivot = GatePrescriptionRule.Edit(windows, GateEditKind.CeilingSlopeEase,
                                                    0.5f, x: 10f, pivotX: 10f);
            var ahead = GatePrescriptionRule.Edit(windows, GateEditKind.CeilingSlopeEase,
                                                  0.5f, x: 14f, pivotX: 10f);
            var behind = GatePrescriptionRule.Edit(windows, GateEditKind.CeilingSlopeEase,
                                                   0.5f, x: 6f, pivotX: 10f);
            Assert.AreEqual(5f, atPivot[0].Top, 1e-4f, "축에서는 그대로다.");
            Assert.AreEqual(7f, ahead[0].Top, 1e-4f, "4m 앞에서는 0.5×4 = 2m 올라간다.");
            Assert.AreEqual(5f, behind[0].Top, 1e-4f, "축보다 뒤는 안 건드린다.");
        }

        // ── 고치기 전 값 재기 ───────────────────────────────────────────────

        [Test]
        public void 칸막이_두께는_가장_두꺼운_것을_낸다()
        {
            //  칸막이가 1m와 3m 둘. 앞의 것이 <더 얇다> — 그래야 "가장 두꺼운 것"과
            //  "맨 앞의 것"이 갈린다.
            Assert.AreEqual(3f, GatePrescriptionRule.DividerThickness(
                new[] { Window(0f, 2f), Window(3f, 5f), Window(8f, 9f) }), 1e-4f);
            Assert.AreEqual(0f, GatePrescriptionRule.DividerThickness(new[] { Window(0f, 5f) }), 1e-4f,
                            "창이 하나면 칸막이가 없다.");
        }

        [Test]
        public void 천장_하강은_양_끝_열의_높이차를_거리로_나눈_값이다()
        {
            var span = new List<GateColumn>
            {
                new GateColumn(0f, new[] { Window(0f, 10f) }),
                new GateColumn(4f, new[] { Window(0f, 6.92f) }),
            };
            Assert.AreEqual(0.77f, GatePrescriptionRule.CeilingDescent(span), 1e-3f,
                            "4m 동안 3.08m 내려왔으니 x 1m당 0.77m다.");
            Assert.AreEqual(0f, GatePrescriptionRule.CeilingDescent(
                new List<GateColumn> { new GateColumn(0f, new[] { Window(0f, 10f) }) }), 1e-4f,
                "열이 하나뿐이면 기울기를 못 잰다.");
        }

        // ── 단조성 ──────────────────────────────────────────────────────────

        [Test]
        public void Δ가_커질수록_깔때기_안에_들어온_수가_줄지_않는다()
        {
            var columns = SplitGate();
            int previous = -1;
            //  훑기(Scan)가 아니라 세는 함수를 직접 부른다 — 훑기는 "이미 들어온 것은 다시 안
            //  묻는" 지름길을 쓰므로, 그걸로 단조를 재면 자기 자신을 확인하는 셈이 된다.
            for (float delta = 0f; delta <= 2.5f + 1e-4f; delta += 0.25f)
            {
                int inside = CountAt(GateEditKind.DividerThin, delta, columns, SplitEntries);
                Assert.GreaterOrEqual(inside, previous,
                    $"Δ={delta:F2}에서 수가 줄었다 — 자유공간은 넓어지기만 하므로 있을 수 없다.");
                previous = inside;
            }
            Assert.AreEqual(SplitEntries.Count, previous, "끝에서는 둘 다 들어와 있어야 한다.");
        }

        [Test]
        public void 천장을_올려도_들어온_수가_줄지_않는다()
        {
            var columns = Uniform(Window(0f, 3f));
            var entries = new List<GateEntry>
            {
                new GateEntry(1f, -MaxFallSpeed),
                new GateEntry(2f, -MaxFallSpeed),
            };
            int previous = -1;
            for (float delta = 0f; delta <= 6f + 1e-4f; delta += 0.5f)
            {
                int inside = CountAt(GateEditKind.CeilingRaise, delta, columns, entries);
                Assert.GreaterOrEqual(inside, previous, $"Δ={delta:F2}에서 수가 줄었다.");
                previous = inside;
            }
            Assert.AreEqual(entries.Count, previous, "천장을 6m 올리면 둘 다 들어온다.");
        }

        // ── 경계 ────────────────────────────────────────────────────────────

        [Test]
        public void 전부_들어오는_최소Δ는_칸막이가_사라지는_두께에서_처음_맞는다()
        {
            var columns = SplitGate();
            PrescriptionScan scan = GatePrescriptionRule.Scan(
                SplitEntries, columns, NoRunout, Kernel, GateEditKind.DividerThin,
                pivotX: columns[0].X, step: 0.5f, cap: 2f);
            Assert.IsTrue(PrescriptionScan.Found(scan.DeltaForAll), "두께만큼 깎으면 둘 다 들어온다.");
            Assert.AreEqual(2f, scan.DeltaForAll, 1e-4f,
                "손으로 푼 문턱 = 칸막이 두께. 그 아래로는 창 사이가 한 틱(0.6m)보다 멀다.");
            //  그 값과 <한 눈금 아래> 양쪽을 다 확인한다 — 한쪽만 보면 "늘 통과"도 "늘 실패"도
            //  같은 단언을 통과해 버린다.
            Assert.AreEqual(SplitEntries.Count,
                            CountAt(GateEditKind.DividerThin, 2f, columns, SplitEntries),
                            "문턱에서는 전부 들어온다.");
            Assert.Less(CountAt(GateEditKind.DividerThin, 1.5f, columns, SplitEntries),
                        SplitEntries.Count,
                        "한 눈금 아래에서는 전부는 아니다 — 칸막이가 아직 남아 있다.");
        }

        [Test]
        public void 절반_Δ는_전부_Δ보다_먼저_오고_실제로_절반을_채운다()
        {
            var columns = Uniform(Window(0f, 3f));
            var entries = new List<GateEntry>
            {
                new GateEntry(1f, -MaxFallSpeed),
                new GateEntry(2f, -MaxFallSpeed),
            };
            PrescriptionScan scan = GatePrescriptionRule.Scan(
                entries, columns, NoRunout, Kernel, GateEditKind.CeilingRaise,
                pivotX: columns[0].X, step: 0.25f, cap: 8f);
            Assert.IsTrue(PrescriptionScan.Found(scan.DeltaForHalf), "절반을 채우는 Δ가 있어야 한다.");
            Assert.IsTrue(PrescriptionScan.Found(scan.DeltaForAll), "전부를 채우는 Δ도 있어야 한다.");
            Assert.Less(scan.DeltaForHalf, scan.DeltaForAll,
                "두 진입 상태의 문턱이 다르므로 절반이 전부보다 싸다.");
            Assert.GreaterOrEqual(CountAt(GateEditKind.CeilingRaise, scan.DeltaForHalf, columns, entries),
                                  (entries.Count + 1) / 2, "절반 Δ에서 실제로 절반 이상이 들어온다.");
            Assert.Less(CountAt(GateEditKind.CeilingRaise, scan.DeltaForHalf - scan.Step, columns, entries),
                        (entries.Count + 1) / 2, "한 눈금 아래에서는 절반에 못 미친다.");
        }

        // ── 못 찾는 경우 ────────────────────────────────────────────────────

        [Test]
        public void 훑은_Δ_안에_답이_없으면_빈_값과_함께_그렇게_적는다()
        {
            var columns = SplitGate();
            //  칸막이 두께의 절반까지만 훑는다 — 정의상 답이 있을 수 없는 범위다.
            PrescriptionScan scan = GatePrescriptionRule.Scan(
                SplitEntries, columns, NoRunout, Kernel, GateEditKind.DividerThin,
                pivotX: columns[0].X, step: 0.5f, cap: 1f);
            Assert.AreEqual(-1f, PrescriptionScan.None, 1e-6f, "빈 값은 음수 하나로 못박는다.");
            Assert.IsFalse(PrescriptionScan.Found(PrescriptionScan.None), "음수는 찾은 값이 아니다.");
            Assert.AreEqual(PrescriptionScan.None, scan.DeltaForAll, 1e-6f,
                            "전부 들어오는 Δ를 못 찾았으면 빈 값이다.");
            Assert.AreEqual(PrescriptionScan.None, scan.DeltaForHalf, 1e-6f,
                            "절반조차 못 찾았으면 그것도 빈 값이다.");
            Assert.AreEqual(0, scan.Best, "훑은 범위에서는 하나도 안 들어왔다.");
            string line = GatePrescriptionRule.Line(GatePrescriptionRule.WithBaseline(scan, 2f));
            Assert.IsTrue(Has(line, "훑은 Δ 안에서는 전부 들어오는 Δ가 없다"),
                          $"못 찾았다고 적어야 한다: {line}");
            Assert.IsTrue(Has(line, "2개 중 0개"), $"가장 많아야 몇 개인지도 적어야 한다: {line}");
            Assert.IsTrue(Has(line, "절반도 못 채운다"), $"절반도 못 찾았다고 적어야 한다: {line}");
        }

        // ── 못 쟀다 ≠ 안 된다 ──────────────────────────────────────────────

        [Test]
        public void 창이_너무_넓어_굴려_보기가_그만두면_안_된다가_아니라_못_쟀다고_한다()
        {
            //  아주 넓고 긴 관문. 볼 상태가 기하급수로 늘어 굴려 보기가 상태 한도에 먼저 닿는다.
            //  그걸 "막혔다"로 세면 <천장을 올릴수록 더 안 된다>는 거짓말이 된다.
            var windows = new[] { Window(0f, 40f) };
            var columns = new List<GateColumn>();
            for (int i = 0; i <= 9; i++)
            {
                columns.Add(new GateColumn(i, windows));
            }
            var entries = new List<GateEntry> { new GateEntry(20f, 0f) };
            bool rolled = GateFunnelRule.TryRolls(20f, 0f, columns, NoRunout, Kernel,
                                                  out _, out bool gaveUp);
            Assert.IsFalse(rolled, "상태 한도에 걸리면 거짓을 낸다 — 여기까지는 예전과 같다.");
            Assert.IsTrue(gaveUp, "그 거짓은 <못 쟀다>는 뜻이라고 밝혀야 한다.");

            PrescriptionScan scan = GatePrescriptionRule.Scan(
                entries, columns, NoRunout, Kernel, GateEditKind.CeilingRaise,
                pivotX: columns[0].X, step: 0.25f, cap: 8f);
            Assert.IsTrue(scan.GaveUp, "훑기도 그 사실을 들고 나와야 한다.");
            string line = GatePrescriptionRule.Line(GatePrescriptionRule.WithBaseline(scan, 0f));
            Assert.IsTrue(Has(line, "못 쟀다"), $"못 잰 것을 안 된다고 적으면 안 된다: {line}");
        }

        [Test]
        public void 어떤_Δ에도_안_들어오면_가장_많아야_0개라고_적는다()
        {
            //  "0개"는 <가망 없다>가 아니라 <이 손잡이가 아니다>는 뜻이다 — 리포트가 그 구별을
            //  찍는 조건(Best == 0)이 실제로 0인지 못박는다.
            //  <b>아래</b> 칸에 갇힌 진입 상태. 아래 창은 높이 1.3m라 날갯짓(+0.46m/틱)이 자기
            //  천장을 뚫고, 종단낙하로 들어와 바닥에 닿는다. 위 창의 천장을 아무리 올려도
            //  이 새에게는 아무 일도 일어나지 않는다 — 그게 "손잡이가 아니다"의 모양이다.
            var columns = Uniform(Window(0f, 1.3f), Window(4f, 9f));
            var trapped = new List<GateEntry> { new GateEntry(0.3f, -MaxFallSpeed) };
            PrescriptionScan ceiling = GatePrescriptionRule.Scan(
                trapped, columns, NoRunout, Kernel, GateEditKind.CeilingRaise,
                pivotX: columns[0].X, step: 0.5f, cap: 8f);
            Assert.AreEqual(0, ceiling.Best,
                "아래 칸에 갇혔으면 위 칸 천장을 올려도 안 들어온다 — 손잡이가 다르다.");
            Assert.IsFalse(PrescriptionScan.Found(ceiling.DeltaForAll));

            //  같은 자리에서 <칸막이>는 듣는다 — 그래야 "0개"가 가망 없음이 아니라 손잡이
            //  문제라는 말이 성립한다.
            PrescriptionScan divider = GatePrescriptionRule.Scan(
                trapped, columns, NoRunout, Kernel, GateEditKind.DividerThin,
                pivotX: columns[0].X, step: 0.5f, cap: 3f);
            Assert.IsTrue(PrescriptionScan.Found(divider.DeltaForAll),
                "칸막이를 없애면 위 칸과 하나가 되어 날갯짓할 자리가 생긴다.");
        }

        // ── 글 ──────────────────────────────────────────────────────────────

        [Test]
        public void 처방_줄은_Δ가_아니라_바뀐_값을_적는다()
        {
            var columns = SplitGate();
            PrescriptionScan scan = GatePrescriptionRule.WithBaseline(
                GatePrescriptionRule.Scan(SplitEntries, columns, NoRunout, Kernel,
                                          GateEditKind.DividerThin, columns[0].X, 0.5f, 2f),
                baseline: 2f);
            string line = GatePrescriptionRule.Line(scan);
            Assert.IsTrue(Has(line, "칸막이를 2.00m → 0.00m 로 줄이면"),
                          $"디자이너가 옮겨 적을 숫자는 결과 값이다: {line}");
            Assert.IsTrue(Has(line, "2개 중 2개가 깔때기 안"), $"몇 개가 들어오는지 적어야 한다: {line}");
            Assert.IsTrue(Has(line, "사다리 "), $"Δ마다의 수(사다리)를 적어야 한다: {line}");
        }

        [Test]
        public void 기울기_처방_줄은_m당_하강을_적는다()
        {
            var scan = GatePrescriptionRule.WithBaseline(
                new PrescriptionScan(GateEditKind.CeilingSlopeEase, 0f, 0.02f, 0.77f, 12, 12,
                                     deltaForAll: 0.16f, deltaForHalf: 0.08f,
                                     ladder: new[] { new PrescriptionRung(0f, 0) }),
                baseline: 0.77f);
            string line = GatePrescriptionRule.Line(scan);
            Assert.IsTrue(Has(line, "천장 기울기를 0.77 → 0.61 m/m 로 낮추면"),
                          $"고치기 전과 후를 둘 다 적어야 한다: {line}");
            Assert.IsTrue(Has(line, "0.02 m/m"), $"기울기 눈금의 단위는 m/m다: {line}");
        }
    }
}
