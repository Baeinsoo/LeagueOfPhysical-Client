using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryCutInGlueTests
    {
        private static ArcheryRoundPlacement P(string id, int rank, int points)
            => new ArcheryRoundPlacement(id, true, Vector2.zero, 0f, rank, points);

        [Test]
        public void 꼴찌는_늦은_스냅샷이_아니라_결과_점수로_센다()
        {
            //  마지막(×2) 라운드 결과가 올 때 스냅샷(ArcheryScore)은 아직 그 전 점수다 — A 10 / B 13.
            var totals = new ArcheryShootOffTotals();
            var stale = new Dictionary<string, int> { ["A"] = 0, ["B"] = 0 };
            totals.Add(new List<ArcheryRoundPlacement> { P("B", 0, 13), P("A", 1, 10) }, id => stale[id]);
            stale["A"] = 10; stale["B"] = 13;   // 이미 알던 사람이라 무시돼야 한다
            totals.Add(new List<ArcheryRoundPlacement> { P("A", 0, 8), P("B", 1, 0) }, id => stale[id]);
            Assert.AreEqual("B", totals.Bottom());   // A 18 · B 13
        }

        [Test]
        public void 처음_보는_사람은_지금_점수에서_시작한다()
        {
            //  판 중간에 들어온 사람 — 지난 라운드 점수는 스냅샷에서 가져온다.
            var totals = new ArcheryShootOffTotals();
            totals.Add(new List<ArcheryRoundPlacement> { P("A", 0, 3), P("B", 1, 1) }, id => id == "A" ? 0 : 20);
            Assert.AreEqual("A", totals.Bottom());   // A 3 · B 21
        }

        [Test]
        public void 처음_나타났을_때_잡은_점수에서_센다()
        {
            //  첫 결과 패킷이 늦어(재전송) 점수 스냅샷이 먼저 왔다 — 결과 순간의 점수엔 이미 이번 라운드가 들어 있다.
            var totals = new ArcheryShootOffTotals();
            totals.Seed("A", 0);
            totals.Seed("B", 0);
            var already = new Dictionary<string, int> { ["A"] = 3, ["B"] = 9 };
            totals.Add(new List<ArcheryRoundPlacement> { P("B", 0, 9), P("A", 1, 3) }, id => already[id]);
            totals.Add(new List<ArcheryRoundPlacement> { P("A", 0, 10), P("B", 1, 0) }, id => already[id]);
            Assert.AreEqual("B", totals.Bottom());   // A 13 · B 9 (두 번 세면 A 16 · B 18 → A가 꼴찌로 잘못)
        }

        [Test]
        public void 나중에_잡아도_처음_값을_지킨다()
        {
            var totals = new ArcheryShootOffTotals();
            totals.Seed("A", 0);
            totals.Seed("A", 50);
            totals.Add(new List<ArcheryRoundPlacement> { P("A", 0, 1) }, id => 99);
            totals.Add(new List<ArcheryRoundPlacement> { P("B", 0, 5) }, id => 99);
            Assert.AreEqual("A", totals.Bottom());   // A 1 · B 104
        }

        [Test]
        public void 결과가_열리는_순간에만_한_번()
        {
            var trigger = new ArcheryCutInTrigger();
            trigger.OnResult(new ArcheryCutInPick(ArcheryCutInKind.Bull, "a"));
            Assert.AreEqual(ArcheryCutInKind.None, trigger.Tick(resultVisible: false, drawing: false, used: 0).Kind);
            Assert.AreEqual(ArcheryCutInKind.Bull, trigger.Tick(resultVisible: true, drawing: false, used: 0).Kind);
            Assert.AreEqual(ArcheryCutInKind.None, trigger.Tick(resultVisible: true, drawing: false, used: 1).Kind);
            trigger.Tick(resultVisible: false, drawing: false, used: 1);
            Assert.AreEqual(ArcheryCutInKind.None, trigger.Tick(resultVisible: true, drawing: false, used: 1).Kind);   // 지난 것은 다시 안 뜬다
        }

        [Test]
        public void 열리는_순간_당기는_중이면_버린다()
        {
            var trigger = new ArcheryCutInTrigger();
            trigger.OnResult(new ArcheryCutInPick(ArcheryCutInKind.Comeback, "a"));
            Assert.AreEqual(ArcheryCutInKind.None, trigger.Tick(resultVisible: true, drawing: true, used: 0).Kind);
            trigger.Tick(resultVisible: false, drawing: false, used: 0);
            Assert.AreEqual(ArcheryCutInKind.None, trigger.Tick(resultVisible: true, drawing: false, used: 0).Kind);   // 버린 것은 다음에도 안 뜬다
        }

        [Test]
        public void 예산을_다_쓰면_안_뜬다()
        {
            var trigger = new ArcheryCutInTrigger();
            trigger.OnResult(new ArcheryCutInPick(ArcheryCutInKind.Bull, "a"));
            Assert.AreEqual(ArcheryCutInKind.None, trigger.Tick(resultVisible: true, drawing: false, used: ArcheryCutInPicker.MaxPerMatch).Kind);
        }
    }
}
