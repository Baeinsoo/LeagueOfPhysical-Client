using System;
using System.Collections.Generic;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// 관문 통과 기록과 진입 깔때기의 <b>순수한 부분</b>을 못박는다. 씬도 물리 엔진도 안 쓴다 —
    /// 여기서 재는 것은 규칙이지 맵이 아니다.
    ///
    /// <para>깔때기 경계값은 <b>손으로 푼 값</b>이다. 창을 몸(0.9)보다 0.4m만 높게 잡으면
    /// 날갯짓(한 틱에 +0.46m)이 천장에 막혀 아예 불가능해지므로, 궤적이 순수 포물선
    /// <c>y_k = y0 + 0.02·k·v − 0.014·k(k+1)</c> 하나로 닫힌다. 그 부등식을 풀어 나온 값을
    /// 그대로 단언한다 — 코드가 낸 값을 코드로 확인하는 자기충족 테스트가 되지 않게.</para>
    /// </summary>
    public class GateFunnelTests
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

        //  관문 뒤를 <b>볼 수 없는</b> 경우. 이 관문들은 관문 안의 규칙만 재려는 것이라 뒤를 주지
        //  않는다 — 지평이 아치 끝까지 늘어난 뒤에도 관문 안의 답은 그대로여야 한다.
        static readonly List<GateColumn> NoRunout = null;

        //  날갯짓이 불가능한 낮은 창 하나짜리 관문. 창 높이 1.3 = 몸 0.9 + 여유 0.4인데
        //  날갯짓 한 틱이 0.46m를 올리므로 어디서 눌러도 천장을 뚫는다.
        static List<GateColumn> FlatGate()
        {
            var windows = new[] { Window(0f, 1.3f) };
            return new List<GateColumn>
            {
                new GateColumn(0f, windows),
                new GateColumn(0.5f, windows),
            };
        }

        // ── 창에 들어가나 ───────────────────────────────────────────────────

        [Test]
        public void 창을_지나는_높이는_그_창_번호를_낸다()
        {
            var windows = new[] { Window(0f, 5f) };
            Assert.AreEqual(0, GateFunnelRule.WindowAt(0f, BodyHeight, windows), "발이 딱 바닥이면 들어간다.");
            Assert.AreEqual(0, GateFunnelRule.WindowAt(4.1f, BodyHeight, windows), "머리가 딱 천장이면 들어간다.");
        }

        [Test]
        public void 창_바닥에_못_미치면_안_들어간다()
        {
            var windows = new[] { Window(0f, 5f) };
            Assert.AreEqual(-1, GateFunnelRule.WindowAt(-0.01f, BodyHeight, windows));
        }

        [Test]
        public void 머리가_창_천장을_뚫으면_안_들어간다()
        {
            var windows = new[] { Window(0f, 5f) };
            //  발만 보면 4.2도 창 안이다 — 몸 높이를 빼고 재야 걸린다.
            Assert.AreEqual(-1, GateFunnelRule.WindowAt(4.2f, BodyHeight, windows),
                            "발만 보고 판정하면 머리가 천장을 뚫은 자리를 통과로 센다.");
        }

        [Test]
        public void 창이_둘이면_들어간_쪽의_번호를_낸다()
        {
            var windows = new[] { Window(-18.4f, -13.4f), Window(-10.2f, -5.25f) };
            Assert.AreEqual(0, GateFunnelRule.WindowAt(-16f, BodyHeight, windows));
            Assert.AreEqual(1, GateFunnelRule.WindowAt(-9f, BodyHeight, windows));
            Assert.AreEqual(-1, GateFunnelRule.WindowAt(-12f, BodyHeight, windows),
                            "칸막이 안은 어느 창도 아니다.");
        }

        [Test]
        public void 한_틱에_훑고_지나간_구간_전부가_한_창_안이어야_한다()
        {
            var windows = new[] { Window(-18.4f, -13.4f), Window(-10.2f, -5.25f) };
            //  위 창 −9에서 아래 창 −17로 한 틱에 떨어졌다 — 끝점만 보면 둘 다 창 안이라
            //  통과로 세지만, 실제로는 칸막이를 뚫고 지나갔다.
            Assert.IsFalse(GateFunnelRule.SpanFits(-17f, -9f, BodyHeight, windows),
                           "끝점만 보면 칸막이를 통과한다.");
            Assert.IsTrue(GateFunnelRule.SpanFits(-17f, -16f, BodyHeight, windows));
        }

        // ── 창까지 얼마나 모자랐나 ──────────────────────────────────────────

        [Test]
        public void 창_바닥에_못_미쳤으면_모자란_만큼이_양수다()
        {
            var windows = new[] { Window(0f, 5f), Window(10f, 15f) };
            int index = GateFunnelRule.NearestWindow(-1f, BodyHeight, windows, out float shortfall);
            Assert.AreEqual(0, index);
            Assert.AreEqual(1f, shortfall, 1e-4f);
        }

        [Test]
        public void 천장을_넘겼으면_내려가야_할_만큼이_음수다()
        {
            var windows = new[] { Window(0f, 5f) };
            int index = GateFunnelRule.NearestWindow(4.5f, BodyHeight, windows, out float shortfall);
            Assert.AreEqual(0, index);
            //  발이 있을 수 있는 가장 높은 자리는 5 − 0.9 = 4.1이다.
            Assert.AreEqual(-0.4f, shortfall, 1e-4f);
        }

        [Test]
        public void 창_안에서_멈췄으면_모자람이_0이다()
        {
            var windows = new[] { Window(0f, 5f) };
            GateFunnelRule.NearestWindow(2f, BodyHeight, windows, out float shortfall);
            Assert.AreEqual(0f, shortfall, 1e-6f);
        }

        [Test]
        public void 몸이_안_들어가는_창은_후보가_아니다()
        {
            //  0.5m짜리 창이 더 가깝지만 몸(0.9)이 안 들어간다 — 그걸 고르면
            //  "0.1m만 올라가면 된다"는 거짓 처방이 나온다.
            var windows = new[] { Window(1.1f, 1.6f), Window(5f, 10f) };
            int index = GateFunnelRule.NearestWindow(1f, BodyHeight, windows, out float shortfall);
            Assert.AreEqual(1, index);
            Assert.AreEqual(4f, shortfall, 1e-4f);
        }

        // ── 깔때기 — 경계 양쪽 ──────────────────────────────────────────────
        //
        //  창 [0, 1.3], 관문 x 0~0.5. 한 틱에 0.22m 전진하므로 3틱(0.66m)이면 빠져나간다.
        //  날갯짓은 천장에 막혀 불가능하니 궤적은 y_k = y0 + 0.02·k·v − 0.014·k(k+1) 하나뿐이고,
        //  발은 늘 [0, 0.4] 안에 있어야 한다.

        [Test]
        public void 너무_빨리_떨어지면_바닥에_닿는다()
        {
            //  y0=0.35, v=−4 → 3틱째 y = 0.35 − 0.24 − 0.168 = −0.058 < 0.
            Assert.IsFalse(GateFunnelRule.Rolls(0.35f, -4f, FlatGate(), NoRunout, Kernel));
        }

        [Test]
        public void 조금_덜_떨어지면_지난다()
        {
            //  y0=0.35, v=−2 → 3틱째 y = 0.35 − 0.12 − 0.168 = +0.062 ≥ 0.
            Assert.IsTrue(GateFunnelRule.Rolls(0.35f, -2f, FlatGate(), NoRunout, Kernel));
        }

        [Test]
        public void 너무_빨리_올라가면_천장에_닿는다()
        {
            //  y0=0.35, v=+6 → 1틱째 y = 0.35 + 0.092 = 0.442 > 0.4(발 상한).
            Assert.IsFalse(GateFunnelRule.Rolls(0.35f, 6f, FlatGate(), NoRunout, Kernel));
        }

        [Test]
        public void 조금_덜_올라가면_지난다()
        {
            //  y0=0.35, v=+2 → 가장 높은 1틱째가 0.362 ≤ 0.4.
            Assert.IsTrue(GateFunnelRule.Rolls(0.35f, 2f, FlatGate(), NoRunout, Kernel));
        }

        [Test]
        public void 깔때기가_높이마다_통과하는_속도_범위를_낸다()
        {
            //  줄은 창 바닥(0)부터 발 상한(0.4)까지. 간격 0.35면 정확히 두 줄이다.
            List<FunnelRow> rows = GateFunnelRule.Funnel(FlatGate(), NoRunout, Kernel,
                                                         yStep: 0.35f, verticalSpeedStep: 2f);
            Assert.AreEqual(2, rows.Count);

            //  바닥에서 출발하면 <올라오면서> 들어와야 3틱을 버틴다: v ≥ 2.8 → 격자로 +4.
            //  위로는 3틱째가 0.4를 넘지 않아야 하니 v ≤ 9.47 → 격자로 +8.
            Assert.AreEqual(0f, rows[0].Y, 1e-4f);
            Assert.IsTrue(rows[0].Passes);
            Assert.AreEqual(4f, rows[0].MinVerticalSpeed, 1e-4f);
            Assert.AreEqual(8f, rows[0].MaxVerticalSpeed, 1e-4f);

            //  0.35에서는 3틱째가 −3.03 ≤ v, 1틱째가 v ≤ 2.5 → 격자로 −2 ~ +2
            //  (+4면 1틱째 발이 0.402로 상한 0.4를 넘는다).
            Assert.AreEqual(0.35f, rows[1].Y, 1e-4f);
            Assert.IsTrue(rows[1].Passes);
            Assert.AreEqual(-2f, rows[1].MinVerticalSpeed, 1e-4f);
            Assert.AreEqual(2f, rows[1].MaxVerticalSpeed, 1e-4f);
            Assert.IsFalse(rows[1].Gapped, "이 줄은 −2~+2가 전부 통과라 끊긴 데가 없다.");
        }

        [Test]
        public void 창_밖에서_들어오면_어떤_속도로도_못_지난다()
        {
            //  발이 0.5면 머리가 1.4로 천장(1.3)을 뚫은 채 들어온 것이다.
            for (float v = -MaxFallSpeed; v <= FlapImpulse; v += 1f)
            {
                Assert.IsFalse(GateFunnelRule.Rolls(0.5f, v, FlatGate(), NoRunout, Kernel),
                               $"창 밖에서 출발한 vy={v}가 통과로 셌다.");
            }
        }

        // ── 통과 기록 ───────────────────────────────────────────────────────

        static List<FlightSample> Path(params (float X, float Y, float Vy)[] samples)
        {
            var path = new List<FlightSample>(samples.Length);
            foreach (var s in samples)
            {
                path.Add(new FlightSample(s.X, s.Y, s.Vy));
            }
            return path;
        }

        static List<GateColumn> SplitGate()
        {
            var open = new[] { Window(-19.75f, -2.45f) };
            var split = new[] { Window(-18.4f, -13.4f), Window(-10.2f, -5.25f) };
            return new List<GateColumn>
            {
                new GateColumn(80.5f, open),
                new GateColumn(81f, open),
                new GateColumn(81.5f, split),
                new GateColumn(82f, split),
                new GateColumn(82.5f, split),
                new GateColumn(83f, open),
                new GateColumn(83.5f, open),
            };
        }

        [Test]
        public void 관문을_지났으면_입구_상태와_들어간_창을_적는다()
        {
            GateCrossing crossing = GateFunnelRule.Cross(
                "_1", 80.5f, 83.5f, SplitGate(),
                Path((79f, -16f, -5f), (80.6f, -16.2f, -6f), (84f, -17f, -7f)), BodyHeight);
            Assert.AreEqual(GateOutcome.Passed, crossing.Outcome);
            Assert.AreEqual(-16.2f, crossing.EntryY, 1e-4f);
            Assert.AreEqual(-6f, crossing.EntryVerticalSpeed, 1e-4f);
            Assert.AreEqual(0, crossing.EntryWindow, "아래 창으로 들어갔다.");
            Assert.AreEqual(-19.75f, crossing.EntryWindowSpan.Bottom, 1e-4f);
            Assert.AreEqual(-16.2f - (-19.75f), crossing.EntryAboveBottom, 1e-4f);
        }

        [Test]
        public void 관문_안에서_멈췄으면_모자란_만큼을_적는다()
        {
            GateCrossing crossing = GateFunnelRule.Cross(
                "_2", 80.5f, 83.5f, SplitGate(),
                Path((80.6f, -6f, -12f), (81.6f, -10.2f, -30f)), BodyHeight);
            Assert.AreEqual(GateOutcome.Stopped, crossing.Outcome);
            Assert.AreEqual(-10.2f, crossing.StopY, 1e-4f);
            Assert.AreEqual(-30f, crossing.StopVerticalSpeed, 1e-4f);
            Assert.AreEqual(1, crossing.NearestWindow, "칸막이 위에 얹혔으니 위 창이 가장 가깝다.");
            //  번호가 아니라 y범위로도 같은 창을 가리켜야 한다 — 리포트가 찍는 것은 이쪽이다.
            Assert.AreEqual(-10.2f, crossing.NearestWindowSpan.Bottom, 1e-4f);
            Assert.AreEqual(-5.25f, crossing.NearestWindowSpan.Top, 1e-4f);
            //  위 창 바닥이 −10.2이므로 발은 이미 바닥에 있다 — 모자란 것은 0이다.
            Assert.AreEqual(0f, crossing.Shortfall, 1e-4f);
        }

        [Test]
        public void 칸막이_속에서_멈췄으면_창까지의_거리를_적는다()
        {
            GateCrossing crossing = GateFunnelRule.Cross(
                "_3", 80.5f, 83.5f, SplitGate(),
                Path((80.6f, -6f, -12f), (81.6f, -12.4f, -30f)), BodyHeight);
            Assert.AreEqual(GateOutcome.Stopped, crossing.Outcome);
            //  −12.4에서 아래 창 발 상한(−13.4+0.9−0.9 → −18.4~−14.3)까지는 1.9 내려가야 하고,
            //  위 창 바닥(−10.2)까지는 2.2 올라가야 한다 — 가까운 쪽은 아래 창이다.
            Assert.AreEqual(0, crossing.NearestWindow);
            Assert.AreEqual(-1.9f, crossing.Shortfall, 1e-4f);
        }

        [Test]
        public void 관문에_오지도_못했으면_그렇게_적는다()
        {
            GateCrossing crossing = GateFunnelRule.Cross(
                "_4", 80.5f, 83.5f, SplitGate(),
                Path((10f, 0f, 0f), (40.25f, -3f, -12f)), BodyHeight);
            Assert.AreEqual(GateOutcome.NeverArrived, crossing.Outcome);
            Assert.AreEqual(40.25f, crossing.FarthestX, 1e-4f);
        }

        // ── 관문 고르기 ─────────────────────────────────────────────────────

        [Test]
        public void 멀리_떨어진_멈춤은_다른_관문이다()
        {
            var gates = GateFunnelRule.Cluster(
                new[] { 81.4f, 82.3f, 82.5f, 329.5f, 370.5f, 370.5f },
                gap: 3f, pad: 1f, sampleStep: 0.5f);
            Assert.AreEqual(3, gates.Count);
            //  가장 많이 죽인 자리가 맨 앞이다.
            Assert.AreEqual(3, gates[0].StopCount);
            Assert.AreEqual(80.0f, gates[0].StartX, 1e-4f, "80.4를 눈금 아래로 내려 80.0이 된다.");
            Assert.AreEqual(83.5f, gates[0].EndX, 1e-4f);
            Assert.AreEqual(2, gates[1].StopCount);
            Assert.AreEqual(370.5f - 1f, gates[1].StartX, 1e-4f);
        }

        [Test]
        public void 멈춘_비행이_없으면_관문도_없다()
        {
            Assert.AreEqual(0, GateFunnelRule.Cluster(Array.Empty<float>(), 3f, 1f, 0.5f).Count);
            //  아예 안 잰 경우(null)도 빈 결과여야 한다 — 빈 리스트만 시험하면 위 가드는
            //  아무것도 막지 않는 줄이 된다(빈 리스트는 가드가 없어도 빈 결과가 나온다).
            Assert.AreEqual(0, GateFunnelRule.Cluster(null, 3f, 1f, 0.5f).Count);
        }

        [Test]
        public void 가장_좁힌_열이_관문의_얼굴이다()
        {
            //  x 81.5~82.5는 가장 넓은 창이 5.0m인데 그 앞뒤는 17.3m다 — 처음 좁아지는 81.5.
            Assert.AreEqual(2, GateFunnelRule.FaceColumn(SplitGate(), BodyHeight));
        }

        [Test]
        public void 창이_많다고_얼굴이_되지는_않는다()
        {
            //  뒤 열은 창이 둘이지만 그중 넓은 것이 8m다 — 하늘에 떠 있는 장식 주머니가
            //  열을 우승시켜 정작 좁은 자리를 가리는 일을 막는다.
            var gate = new List<GateColumn>
            {
                new GateColumn(0f, new[] { Window(0f, 5f) }),
                new GateColumn(0.5f, new[] { Window(0f, 8f), Window(20f, 21f) }),
            };
            Assert.AreEqual(0, GateFunnelRule.FaceColumn(gate, BodyHeight));
        }

        // ── 리포트 ──────────────────────────────────────────────────────────

        static GateReport SampleReport()
        {
            var columns = SplitGate();
            var report = new GateReport
            {
                StartX = 80.5f,
                EndX = 83.5f,
                StopCount = 2,
                Columns = columns,
                FaceIndex = GateFunnelRule.FaceColumn(columns, BodyHeight),
                Rotating = false,
            };
            report.Crossings.Add(GateFunnelRule.Cross(
                "_1", 80.5f, 83.5f, columns,
                Path((80.6f, -16.2f, -6f), (84f, -17f, -7f)), BodyHeight));
            report.Crossings.Add(GateFunnelRule.Cross(
                "_2", 80.5f, 83.5f, columns,
                Path((80.6f, -6f, -12f), (81.6f, -10.2f, -30f)), BodyHeight));
            report.Crossings.Add(GateFunnelRule.Cross(
                "_3", 80.5f, 83.5f, columns, Path((10f, 0f, 0f)), BodyHeight));
            return report;
        }

        static string Section(GateReport report)
            => GateFunnelRule.Section(new[] { report }, Kernel, requiredBand: 4.912f,
                                      verticalSpeedStep: 2f);

        // ── 진단의 결론 ─────────────────────────────────────────────────────

        //  창 하나짜리 아주 넓은 관문 — 어떤 진입도 깔때기 안이다.
        static List<GateColumn> OpenGate()
        {
            var open = new[] { Window(-20f, 0f) };
            return new List<GateColumn> { new GateColumn(0f, open), new GateColumn(0.5f, open) };
        }

        //  가운데 열이 통째로 막힌 관문 — 어떤 진입도 깔때기 밖이다.
        static List<GateColumn> WalledGate()
        {
            var open = new[] { Window(-20f, 0f) };
            return new List<GateColumn>
            {
                new GateColumn(0f, open),
                new GateColumn(0.5f, Array.Empty<GateWindow>()),
                new GateColumn(1f, open),
            };
        }

        static GateReport Verdict(List<GateColumn> columns, (float Y, float Vy)[] passes,
                                  (float Y, float Vy)[] fails, List<GateColumn> runout = null)
        {
            float endX = columns[columns.Count - 1].X;
            var report = new GateReport
            {
                StartX = columns[0].X,
                EndX = endX,
                StopCount = fails.Length,
                Columns = columns,
                Runout = runout,
                FaceIndex = GateFunnelRule.FaceColumn(columns, BodyHeight),
            };
            foreach (var p in passes)
            {
                //  끝을 넘어간 표본이 있으면 통과다.
                report.Crossings.Add(GateFunnelRule.Cross("통과", columns[0].X, endX, columns,
                    Path((columns[0].X + 0.01f, p.Y, p.Vy), (endX + 1f, p.Y, p.Vy)), BodyHeight));
            }
            foreach (var f in fails)
            {
                //  관문 안에서 끝나면 실패다.
                report.Crossings.Add(GateFunnelRule.Cross("실패", columns[0].X, endX, columns,
                    Path((columns[0].X + 0.01f, f.Y, f.Vy), (columns[0].X + 0.02f, f.Y, f.Vy)), BodyHeight));
            }
            return report;
        }

        [Test]
        public void 통과와_실패의_진입_vy가_안_겹치면_그렇게_적는다()
        {
            string text = Section(Verdict(OpenGate(),
                passes: new[] { (-10f, 20f), (-10f, 17.4f) },
                fails: new[] { (-10f, -8f) }));
            Assert.IsTrue(text.IndexOf("통과 2개: 진입 vy +17.4~+20.0 (올라가는 중)",
                                       StringComparison.Ordinal) >= 0, text);
            Assert.IsTrue(text.IndexOf("실패 1개: 진입 vy -8.0~-8.0 (떨어지는 중)   겹침 없음",
                                       StringComparison.Ordinal) >= 0, text);
            Assert.IsTrue(text.IndexOf("이 관문은 <올라가며 들어가야> 지난다.", StringComparison.Ordinal) >= 0);
        }

        [Test]
        public void 겹치면_vy로는_안_갈린다고_적는다()
        {
            //  같은 vy(+5)로 통과한 비행과 실패한 비행이 둘 다 있다 — 여기서 "올라가며 들어가야
            //  한다"고 적으면 거짓말이 된다.
            string text = Section(Verdict(OpenGate(),
                passes: new[] { (-10f, 5f), (-10f, 12f) },
                fails: new[] { (-10f, 5f), (-10f, -2f) }));
            Assert.IsTrue(text.IndexOf("겹침 있음", StringComparison.Ordinal) >= 0, text);
            Assert.IsTrue(text.IndexOf("진입 vy만으로는 갈리지 않는다", StringComparison.Ordinal) >= 0);
            Assert.IsTrue(text.IndexOf("들어가야> 지난다", StringComparison.Ordinal) < 0);
        }

        //  깔때기 <b>안</b>은 다시 겨냥 탓의 근거가 된다 — 지평이 <확정된 아치의 끝>까지 늘어난
        //  뒤로는, 관문을 나선 뒤 박는 궤적을 통과로 세지 않기 때문이다(2026-09-12에 그 지평을
        //  고쳤다). 지평을 되돌리면 이 문장이 다시 거짓이 되므로 여기서 못박는다.
        [Test]
        public void 실패가_깔때기_안이면_겨냥_탓이라고_적는다()
        {
            string text = Section(Verdict(OpenGate(),
                passes: new[] { (-10f, 20f) }, fails: new[] { (-10f, -8f), (-12f, -6f) }));
            Assert.IsTrue(text.IndexOf("실패 2개의 진입 상태는 모두 깔때기 안이므로 지형이 아니라 겨냥이 원인이다.",
                                       StringComparison.Ordinal) >= 0, text);
        }

        //  하나는 안, 하나는 밖인 관문. 창 [0, 1.3]에서 y=0.35로 들어오면 vy=−2는 지나고
        //  vy=−4는 못 지난다(위 두 경계 테스트가 손으로 푼 값) — 그래서 둘을 갈라 적어야 한다.
        [Test]
        public void 안과_밖이_섞이면_겨냥과_지형을_갈라_적는다()
        {
            string text = Section(Verdict(FlatGate(),
                passes: Array.Empty<(float, float)>(),
                fails: new[] { (0.35f, -2f), (0.35f, -4f) }));
            Assert.IsTrue(text.IndexOf("실패 2개 중 1개는 깔때기 안이므로 겨냥이 원인이고,"
                                       + " 나머지 1개는 깔때기 밖이므로 지형이 원인이다.",
                                       StringComparison.Ordinal) >= 0, text);
        }

        [Test]
        public void 절은_지평이_확정된_아치의_끝이라고_밝힌다()
        {
            string text = Section(Verdict(OpenGate(),
                passes: new[] { (-10f, 20f) }, fails: new[] { (-10f, -8f) }));
            Assert.IsTrue(text.IndexOf("<확정된 아치의 끝>이다", StringComparison.Ordinal) >= 0, text);
            //  틱수·거리는 커널에서 나온다 — 상수로 박으면 중력을 바꾼 날 조용히 거짓이 된다.
            Assert.IsTrue(text.IndexOf("18틱(3.96m)", StringComparison.Ordinal) >= 0, text);
        }

        //  고쳐지지 않은 낙관은 <조용히> 두지 않는다 — 기운 천장에서 창이 넓게 잡히는 크기를
        //  절이 스스로 밝혀야 읽는 사람이 0.1m짜리 판정을 곧이곧대로 믿지 않는다.
        [Test]
        public void 절은_기운_천장에서_창이_넓게_잡힌다고_밝힌다()
        {
            string text = Section(Verdict(OpenGate(),
                passes: new[] { (-10f, 20f) }, fails: new[] { (-10f, -8f) }));
            Assert.IsTrue(text.IndexOf("r·√(1+m²)", StringComparison.Ordinal) >= 0, text);
            Assert.IsTrue(text.IndexOf("0.11m", StringComparison.Ordinal) >= 0, text);
        }

        [Test]
        public void 실패가_깔때기_밖이면_지형_탓이라고_적는다()
        {
            //  가운데 열이 막혀 어떤 진입도 못 지난다 — 같은 문장을 반대로 적어야 한다.
            string text = Section(Verdict(WalledGate(),
                passes: Array.Empty<(float, float)>(), fails: new[] { (-10f, -8f) }));
            Assert.IsTrue(text.IndexOf("실패 1개의 진입 상태는 모두 깔때기 밖이므로 겨냥이 아니라 지형이 원인이다.",
                                       StringComparison.Ordinal) >= 0, text);
            Assert.IsTrue(text.IndexOf("겹침", StringComparison.Ordinal) < 0,
                          "통과가 하나도 없으면 겹칠 것도 없다.");
        }

        [Test]
        public void 절이_창_둘을_따로_적고_갈림을_짚는다()
        {
            string text = Section(SampleReport());
            //  ⚠️ StringAssert는 문화권 비교라 기호가 늘 매칭된다 — Ordinal로 봐야 한다.
            Assert.IsTrue(text.IndexOf("창1", StringComparison.Ordinal) >= 0);
            Assert.IsTrue(text.IndexOf("창2", StringComparison.Ordinal) >= 0);
            Assert.IsTrue(text.IndexOf("창이 2개다", StringComparison.Ordinal) >= 0,
                          "창이 둘이라는 사실 자체가 이 절의 요점이다.");
        }

        [Test]
        public void 안_도는_관문은_도착_시점과_무관하다고_밝힌다()
        {
            string text = Section(SampleReport());
            Assert.IsTrue(text.IndexOf("도착 시점과 무관", StringComparison.Ordinal) >= 0);
            Assert.IsTrue(text.IndexOf("⚠️ 이 관문엔 도는 지형이 있다", StringComparison.Ordinal) < 0);
        }

        [Test]
        public void 도는_관문은_한_위상의_그림이라고_밝힌다()
        {
            GateReport report = SampleReport();
            report.Rotating = true;
            string text = Section(report);
            Assert.IsTrue(text.IndexOf("⚠️ 이 관문엔 도는 지형이 있다", StringComparison.Ordinal) >= 0);
            Assert.IsTrue(text.IndexOf("도착 시점과 무관", StringComparison.Ordinal) < 0);
        }

        [Test]
        public void 도달_못_한_비행은_한_줄로_접는다()
        {
            string text = Section(SampleReport());
            Assert.IsTrue(text.IndexOf("도달 못 함 1줄", StringComparison.Ordinal) >= 0);
            Assert.IsTrue(text.IndexOf("_3", StringComparison.Ordinal) < 0,
                          "도달 못 한 줄까지 이름을 늘어놓으면 절이 관문 하나에 22줄이 된다.");
        }

        // ── 지평 — 확정된 아치의 끝까지 ─────────────────────────────────────
        //
        //  날갯짓은 세로 속도를 <덮어쓰므로> 한 번 누르면 정점까지 다 올라간다. 그래서 "관문 끝을
        //  넘었다"에서 눈을 감으면 관문 <밖>에서 박는 궤적을 통과로 세게 된다. 아래 첫 두 테스트가
        //  2026-09-12에 실제로 그랬던 사례다(진짜 커널로 x=84.61에서 박은 그 궤적).

        //  실측 재료: 관문 x 80.0~83.5, 칸막이 윗면 −10.24와 천장 −5.22 사이의 창.
        static List<GateColumn> Task30Gate()
        {
            var windows = new[] { Window(-10.24f, -5.22f) };
            var columns = new List<GateColumn>();
            for (int i = 0; i < 8; i++)
            {
                columns.Add(new GateColumn(80f + i * 0.5f, windows));
            }
            return columns;
        }

        //  관문 뒤로 <내려오는> 긴 천장. 실측값 그대로 — x=83.5에서 −4.36이고 x 1m당 0.772m씩
        //  낮아진다(x=86.0에서 −6.29). 바닥은 멀리 두어 천장만 재게 한다.
        static List<GateColumn> Task30Runout()
        {
            var columns = new List<GateColumn>();
            for (int i = 0; i < 9; i++)
            {
                float x = 84f + i * 0.5f;
                columns.Add(new GateColumn(x, new[] { Window(-20f, -4.36f - 0.772f * (x - 83.5f)) }));
            }
            return columns;
        }

        static string Show(IReadOnlyList<bool> flaps)
        {
            var text = new System.Text.StringBuilder();
            for (int i = 0; flaps != null && i < flaps.Count; i++)
            {
                text.Append(flaps[i] ? 'F' : '.');
            }
            return text.ToString();
        }

        //  ⚠️ 이 테스트가 빨강이면 지평이 관문 끝으로 되돌아간 것이다.
        [Test]
        public void 관문_뒤에서_박는_궤적은_통과로_세지_않는다()
        {
            Assert.IsFalse(GateFunnelRule.Rolls(-7.17f, -30f, Task30Gate(), Task30Runout(), Kernel),
                           "관문 안에서 누른 날갯짓의 아치가 관문 뒤 내려오는 천장에 박는다.");
        }

        //  같은 진입인데 관문 뒤를 <안 보면> 통과로 센다 — 고친 것이 무엇인지를 못박는 대조군이다.
        //  조작열까지 실측과 같아야 한다(다섯째 틱에 딱 한 번 누른다).
        [Test]
        public void 관문_끝에서_눈을_감으면_그_궤적이_통과로_센다()
        {
            List<bool> flaps;
            Assert.IsTrue(GateFunnelRule.TryRolls(-7.17f, -30f, Task30Gate(), NoRunout, Kernel, out flaps));
            Assert.AreEqual(".....F..........", Show(flaps),
                            "2026-09-12 실측에서 깔때기가 낸 그 조작열이어야 한다.");
        }

        //  창 하나짜리 높은 회랑 x 0~1.0. 관문 뒤는 통째로 막아 둔다 — 보기만 하면 반드시 걸린다.
        static List<GateColumn> TallShortGate()
        {
            var windows = new[] { Window(0f, 10f) };
            return new List<GateColumn>
            {
                new GateColumn(0f, windows),
                new GateColumn(0.5f, windows),
                new GateColumn(1f, windows),
            };
        }

        static List<GateColumn> SolidRunout()
        {
            var columns = new List<GateColumn>();
            for (int i = 0; i < 6; i++)
            {
                columns.Add(new GateColumn(1.5f + i * 0.5f, Array.Empty<GateWindow>()));
            }
            return columns;
        }

        //  떨어지며 나서면 확정된 것이 없다 — 다음 틱에 눌러 올라갈 수 있으므로 관문 뒤가
        //  무엇이든 이 판정을 바꾸지 않는다.
        [Test]
        public void 떨어지며_나서면_관문_뒤를_안_본다()
        {
            Assert.IsTrue(GateFunnelRule.Rolls(1f, 0f, TallShortGate(), SolidRunout(), Kernel),
                          "관문 뒤가 통째로 막혀 있어도, 떨어지며 나섰으면 확정된 것이 없다.");
        }

        //  <b>안 눌러도</b> 올라가는 중일 수 있다(입구 vy가 양수). 그 오름도 취소할 수 없으므로
        //  "안 눌렀으면 관문 끝에서 끝내도 된다"는 참이 아니다 — 지평은 누름이 아니라 <아직
        //  올라가는가>로 잡는다. (y=1, vy=+10으로 들어오면 x=1.10에서 vy=+3.0으로 나선다.)
        [Test]
        public void 안_눌러도_올라가는_중이면_관문_뒤를_본다()
        {
            Assert.IsFalse(GateFunnelRule.Rolls(1f, 10f, TallShortGate(), SolidRunout(), Kernel),
                           "올라가는 중에 나섰으면 그 오름은 확정된 것이라 관문 뒤에서 박는다.");
            Assert.IsTrue(GateFunnelRule.Rolls(1f, 10f, TallShortGate(), NoRunout, Kernel),
                          "관문 뒤를 못 보면 예전처럼 통과로 센다 — 위 판정이 지평에서 온 것임을 가른다.");
        }

        //  긴 회랑 x 0~8.0(33틱). 한 아치로는 끝까지 못 가므로 두 번 이상 누른다.
        static List<GateColumn> LongGate()
        {
            var windows = new[] { Window(0f, 6f) };
            var columns = new List<GateColumn>();
            for (int i = 0; i < 17; i++)
            {
                columns.Add(new GateColumn(i * 0.5f, windows));
            }
            return columns;
        }

        static List<GateColumn> LongRunout(float ceiling)
        {
            var columns = new List<GateColumn>();
            for (int i = 0; i < 10; i++)
            {
                columns.Add(new GateColumn(8.5f + i * 0.5f, new[] { Window(0f, ceiling) }));
            }
            return columns;
        }

        //  누른 틱이 여럿일 때 지평을 정하는 것은 <마지막> 누름이다. 이 관문의 가장 늦은 답은
        //  <b>마지막 틱에 누르고 나가는 것</b>이라(y=0.89, vy=+23으로 나선다) 그 아치 4.01m가
        //  통째로 관문 뒤에 있다. 천장이 그 아치를 받아 주면 그대로 통과고, 못 받아 주면 그
        //  갈래만 버려지고 <덜 늦게 누르는> 다른 갈래가 답이 된다.
        [Test]
        public void 마지막_누름의_아치가_지평을_정한다()
        {
            List<bool> roomy;
            Assert.IsTrue(GateFunnelRule.TryRolls(0.2f, 0f, LongGate(), LongRunout(6f), Kernel, out roomy));
            Assert.IsTrue(roomy[roomy.Count - 1],
                          "천장 6.0은 발 상한 5.1 — 0.89에서 4.01m 오르는 아치가 들어간다.");

            List<bool> tight;
            Assert.IsTrue(GateFunnelRule.TryRolls(0.2f, 0f, LongGate(), LongRunout(5f), Kernel, out tight));
            Assert.IsFalse(tight[tight.Count - 1],
                           "천장 5.0은 발 상한 4.1 — 마지막 틱 누름의 아치(4.90)가 안 들어가므로"
                           + " 그 갈래는 버려져야 한다.");
        }

        // ── 지평의 길이는 물리에서 나온다 ───────────────────────────────────

        [Test]
        public void 아치_틱수와_거리는_중력과_임펄스에서_나온다()
        {
            //  23 / (70 × 0.02) = 16.43 → 올림 17, 누른 틱 하나를 더해 18. 전진은 18 × 0.22m.
            Assert.AreEqual(18, Kernel.ArcTicks);
            Assert.AreEqual(3.96f, Kernel.ArcDistance, 1e-4f);

            //  중력이 절반이면 정점까지 두 배 가까이 걸린다: 23 / 0.7 = 32.86 → 33 + 1.
            var lightGravity = new FlightKernel(ForwardSpeed, 35f, MaxFallSpeed, FlapImpulse,
                                                TickSeconds, BodyHeight);
            Assert.AreEqual(34, lightGravity.ArcTicks);

            //  임펄스가 두 배여도 같은 수다: 46 / 1.4 = 32.86 → 33 + 1.
            var strongFlap = new FlightKernel(ForwardSpeed, Gravity, MaxFallSpeed, 46f,
                                              TickSeconds, BodyHeight);
            Assert.AreEqual(34, strongFlap.ArcTicks);
        }

        //  x=0에서 천장 2.0으로 시작해 x 1m당 1.2m씩 <내려오는> 회랑. 막 누른 아치(4.01m)는 곧장
        //  박고, 다 오른 아치의 꼬리(vy=+3이면 0.10m만 더 오른다)는 빠져나간다. 천장이 내려오므로
        //  <더 멀리 보면> 떨어지는 몸도 결국 걸린다 — 지평이 <남은 오름>만큼이어야 한다는 뜻이다.
        static List<GateColumn> DescendingRunout()
        {
            var columns = new List<GateColumn>();
            for (int i = 0; i < 9; i++)
            {
                float x = i * 0.5f;
                columns.Add(new GateColumn(x, new[] { Window(-20f, 2f - 1.2f * x) }));
            }
            return columns;
        }

        [Test]
        public void 아치가_남아_있는_만큼만_본다()
        {
            Assert.IsFalse(GateFunnelRule.ArcClears(0f, 0f, 23f, DescendingRunout(), Kernel),
                           "막 누르고 나섰으면 4.01m를 더 오르므로 두 틱 만에 천장에 박는다.");
            Assert.IsTrue(GateFunnelRule.ArcClears(0f, 0f, 3f, DescendingRunout(), Kernel),
                          "vy=+3이면 0.10m만 더 오르고 끝난다 — 그 뒤로 내려오는 천장에 걸리는 것은"
                          + " 확정된 것이 아니라 다음 날갯짓으로 피할 수 있는 일이다.");
        }

        [Test]
        public void 볼_지형이_없으면_더_보지_않는다()
        {
            Assert.IsTrue(GateFunnelRule.ArcClears(0f, 0f, 23f, NoRunout, Kernel),
                          "관문 뒤를 못 보면 막혔다고도 안 막혔다고도 할 수 없다.");
        }

        // ── 천장 쪽만 통과를 취소한다 ───────────────────────────────────────

        [Test]
        public void 바닥_쪽으로_벗어난_것은_통과를_취소하지_않는다()
        {
            var windows = new[] { Window(0f, 5f) };
            Assert.IsFalse(GateFunnelRule.CeilingBlocks(-1f, -0.5f, BodyHeight, windows),
                           "바닥 밑으로 벗어난 것은 누르면 피할 수 있다.");
            Assert.IsTrue(GateFunnelRule.CeilingBlocks(4.5f, 4.6f, BodyHeight, windows),
                          "천장을 넘긴 것은 누르면 더 올라갈 뿐이라 못 피한다.");
            Assert.IsFalse(GateFunnelRule.CeilingBlocks(1f, 1.5f, BodyHeight, windows),
                           "창 안이면 막힌 것이 아니다.");
            Assert.IsTrue(GateFunnelRule.CeilingBlocks(1f, 1.5f, BodyHeight, Array.Empty<GateWindow>()),
                          "들어갈 창이 아예 없는 열은 피할 길이 없다.");
        }

        // ── 깔때기가 좁아진다 ───────────────────────────────────────────────

        //  창 [0, 1.3]인 관문 뒤에 천장이 0.15m 낮은 회랑([0, 1.15])이 이어진다.
        static List<GateColumn> LowerRunout()
        {
            var columns = new List<GateColumn>();
            for (int i = 0; i < 9; i++)
            {
                columns.Add(new GateColumn(1f + i * 0.5f, new[] { Window(0f, 1.15f) }));
            }
            return columns;
        }

        [Test]
        public void 지평을_늘리면_깔때기가_좁아진다()
        {
            List<FunnelRow> before = GateFunnelRule.Funnel(FlatGate(), NoRunout, Kernel,
                                                           yStep: 0.35f, verticalSpeedStep: 2f);
            List<FunnelRow> after = GateFunnelRule.Funnel(FlatGate(), LowerRunout(), Kernel,
                                                          yStep: 0.35f, verticalSpeedStep: 2f);
            //  바닥 줄에서 vy=+8은 x=0.66을 vy=+3.8로 나서서 0.38까지 더 오른다 — 머리가 1.28이라
            //  관문 안(1.3)은 지나도 관문 뒤(1.15)는 못 지난다. +6은 0.2까지만 올라 그대로 지난다.
            Assert.AreEqual(8f, before[0].MaxVerticalSpeed, 1e-4f);
            Assert.AreEqual(6f, after[0].MaxVerticalSpeed, 1e-4f);
            Assert.AreEqual(4f, after[0].MinVerticalSpeed, 1e-4f, "아래쪽 경계는 그대로다.");
            //  0.35 줄은 어느 쪽도 올라가며 나서지 않아 판정이 안 바뀐다.
            Assert.AreEqual(before[1].MinVerticalSpeed, after[1].MinVerticalSpeed, 1e-4f);
            Assert.AreEqual(before[1].MaxVerticalSpeed, after[1].MaxVerticalSpeed, 1e-4f);
        }

        // ── 관문 뒤를 얼마나 봤는지 적는다 ──────────────────────────────────

        //  OpenGate(창 −20~0, x 0~0.5) 뒤로 같은 창을 x=5.0까지 이어 붙인 것 — 4.50m라 아치가 다 든다.
        static List<GateColumn> OpenRunout()
        {
            var open = new[] { Window(-20f, 0f) };
            var columns = new List<GateColumn>();
            for (int i = 1; i <= 9; i++)
            {
                columns.Add(new GateColumn(0.5f + i * 0.5f, open));
            }
            return columns;
        }

        [Test]
        public void 아치가_다_들어가면_그렇게_적는다()
        {
            string text = Section(Verdict(OpenGate(),
                passes: new[] { (-10f, 20f) }, fails: new[] { (-10f, -8f) },
                runout: OpenRunout()));
            Assert.IsTrue(text.IndexOf("관문 뒤 4.50m까지 함께 본다", StringComparison.Ordinal) >= 0, text);
        }

        [Test]
        public void 아치보다_짧게_보면_경고한다()
        {
            string text = Section(Verdict(OpenGate(),
                passes: new[] { (-10f, 20f) }, fails: new[] { (-10f, -8f) }));
            Assert.IsTrue(text.IndexOf("⚠️ 관문 뒤로 볼 수 있는 지형이 0.00m뿐이다(아치 3.96m보다 짧다)",
                                       StringComparison.Ordinal) >= 0, text);
        }
    }
}
