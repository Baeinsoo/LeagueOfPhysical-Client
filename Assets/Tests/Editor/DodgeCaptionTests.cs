using System.Collections.Generic;
using System.IO;
using Luban;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class DodgeCaptionTests
    {
        static readonly DodgeConfig C = new DodgeConfig(3, 1.5f, 0.16f, 0.5f, 9f, 6, 2f, 1.8f, 0,
                                                       1.2f, 5f, 0.22f, 2f, 0.25f, 0.7f, 0.45f, 6f, 1.35f, 0.55f);
        static readonly DodgeStageTable T = new DodgeStageTable(new[]
        {
            new DodgeStage("슬리퍼", 10f, new[] { DodgePatternKind.BulletRain }, 1f, 0.5f, 1.8f),
            new DodgeStage("수박", 10f, new[] { DodgePatternKind.Bomb }, 1f, 0.5f, 1.6f),
        });
        static readonly string[] Captions = { "슬리퍼가 날아듭니다.", "" };

        // ── 줄 서기 ──

        [Test]
        public void 한_줄은_정해진_시간만_보인다()
        {
            var q = new DodgeCaptionQueue();
            q.Push("가");
            Assert.AreEqual("가", q.Tick(10.0));
            Assert.AreEqual("가", q.Tick(10.0 + DodgeCaptionQueue.LineSeconds - 0.01));
            Assert.AreEqual("", q.Tick(10.0 + DodgeCaptionQueue.LineSeconds));
        }

        [Test]
        public void 겹치면_차례로_나온다()
        {
            var q = new DodgeCaptionQueue();
            q.Push("가"); q.Push("나");
            Assert.AreEqual("가", q.Tick(0));
            Assert.AreEqual("나", q.Tick(DodgeCaptionQueue.LineSeconds));
            Assert.AreEqual("", q.Tick(DodgeCaptionQueue.LineSeconds * 2));
        }

        // 늦은 해설이 줄줄이 따라오면 지금 일과 안 맞는다 — 밀린 줄이 넘치면 오래된 것부터 버린다.
        [Test]
        public void 밀린_줄이_넘치면_오래된_것을_버린다()
        {
            var q = new DodgeCaptionQueue();
            q.Push("가");
            Assert.AreEqual("가", q.Tick(0));
            q.Push("나"); q.Push("다"); q.Push("라");
            Assert.AreEqual("다", q.Tick(DodgeCaptionQueue.LineSeconds));
            Assert.AreEqual("라", q.Tick(DodgeCaptionQueue.LineSeconds * 2));
        }

        // ── 해설 고르기 ──

        static List<string> Observe(DodgeCaptionDirector d, long tick, string me, params (string id, bool out_)[] players)
        {
            var lines = new List<string>();
            d.Observe(T.At(tick, 0, C), players, me, lines);
            return lines;
        }

        [Test]
        public void 시작하면_개회와_첫_스테이지를_말한다()
        {
            var d = new DodgeCaptionDirector(T, Captions);
            Assert.IsEmpty(Observe(d, -1, "a"));   // 시작 전 — 경기 시작 틱이 아직 안 왔다
            CollectionAssert.AreEqual(new[] { DodgeCaptions.Opening, "슬리퍼가 날아듭니다." }, Observe(d, 0, "a"));
            Assert.IsEmpty(Observe(d, 1, "a"));
        }

        [Test]
        public void 해설이_빈_스테이지는_번호와_이름으로()
        {
            var d = new DodgeCaptionDirector(T, Captions);
            Observe(d, 0, "a");
            CollectionAssert.AreEqual(new[] { "스테이지 2 — 수박" }, Observe(d, 500, "a"));
        }

        [Test]
        public void 서든데스는_연장전이라고_한_번_말한다()
        {
            var d = new DodgeCaptionDirector(T, Captions);
            Observe(d, 0, "a");
            Observe(d, 500, "a");
            CollectionAssert.AreEqual(new[] { DodgeCaptions.SuddenDeath }, Observe(d, 1000, "a"));
            Assert.IsEmpty(Observe(d, 1001, "a"));
        }

        // 번호는 엔티티 id 서수 순서(나 포함) — 어느 클라에서 봐도 같은 사람이 같은 번호(활쏘기와 같은 규칙).
        [Test]
        public void 남이_탈락하면_번호로_부른다()
        {
            var d = new DodgeCaptionDirector(T, Captions);
            Observe(d, 0, "b", ("b", false), ("a", false));
            CollectionAssert.AreEqual(new[] { "1P 선수, 아쉽게 탈락합니다." }, Observe(d, 5, "b", ("b", false), ("a", true)));
            Assert.IsEmpty(Observe(d, 6, "b", ("b", false), ("a", true)));
        }

        [Test]
        public void 내가_탈락하면_관전석으로()
        {
            var d = new DodgeCaptionDirector(T, Captions);
            Observe(d, 0, "b", ("b", false), ("a", false));
            CollectionAssert.AreEqual(new[] { DodgeCaptions.MeEliminated }, Observe(d, 5, "b", ("b", true), ("a", false)));
        }

        // 중간에 붙은 클라(재접속)는 이미 끝난 탈락을 다시 외치지 않는다.
        [Test]
        public void 처음_볼_때_이미_탈락한_사람은_말하지_않는다()
        {
            var d = new DodgeCaptionDirector(T, Captions);
            var lines = Observe(d, 600, "b", ("b", false), ("a", true));
            CollectionAssert.AreEqual(new[] { "스테이지 2 — 수박" }, lines);
        }

        // ── 실제 표 ──

        [Test]
        public void 스테이지_표에_해설이_다_있다()
        {
            string path = Path.GetFullPath(
                "Packages/com.baegames.lop.masterdata.client/Runtime.Generated/StreamingAssets/MasterData/tbdodgestage.bytes");
            var table = new LOP.MasterData.TbDodgeStage(new ByteBuf(File.ReadAllBytes(path)));
            Assert.Greater(table.DataList.Count, 0);
            foreach (var row in table.DataList)
            {
                Assert.IsFalse(string.IsNullOrEmpty(row.Caption), $"스테이지 {row.Id}({row.Name})에 caption이 없다");
            }
        }

        // ── 들것 ──

        [Test]
        public void 들것은_가장_가까운_벽_밖으로_나간다()
        {
            Assert.AreEqual(new Vector2(DodgeStretcher.Outside, 2f), DodgeStretcher.ExitPoint(new Vector2(7f, 2f)));
            Assert.AreEqual(new Vector2(-1f, -DodgeStretcher.Outside), DodgeStretcher.ExitPoint(new Vector2(-1f, -4f)));
        }

        [Test]
        public void 들것은_정해진_시간에_벽_밖에_닿고_끝난다()
        {
            var from = new Vector2(3f, 1f);
            var exit = DodgeStretcher.ExitPoint(from);
            Assert.AreEqual(from, DodgeStretcher.Position(from, exit, 0f));
            Assert.AreEqual(exit, DodgeStretcher.Position(from, exit, DodgeStretcher.Seconds));
            Assert.IsFalse(DodgeStretcher.Done(DodgeStretcher.Seconds - 0.01f));
            Assert.IsTrue(DodgeStretcher.Done(DodgeStretcher.Seconds));
        }
    }
}
