using System;
using System.Linq;
using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>퀘스트 문구(spec 2026-10-30 §7). 글꼴에 −·⬆·⬇가 없다 — 쓰는 기호는 ·, ×, →, /, !뿐.</summary>
    public class QuestFormatTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 30, 3, 0, 0, DateTimeKind.Utc);

        private static MatchQuestProgressDto Q(string title, string period, int after, int target, bool completed, string unit = "count") =>
            new MatchQuestProgressDto { id = title, title = title, period = period, before = 0, after = after, target = target, completed = completed, unit = unit };

        [Test]
        public void 진행_표기_분이면_분을_붙인다()
        {
            Assert.AreEqual("2/4", QuestFormat.Progress(2, 4, "count"));
            Assert.AreEqual("3/10분", QuestFormat.Progress(3, 10, "minutes"));
        }

        [Test]
        public void 비율은_0에서_1로_자른다()
        {
            Assert.AreEqual(0.5f, QuestFormat.Ratio(2, 4), 1e-5f);
            Assert.AreEqual(1f, QuestFormat.Ratio(5, 4), 1e-5f);
            Assert.AreEqual(0f, QuestFormat.Ratio(1, 0), 1e-5f);
        }

        [Test]
        public void 보상_표기_0인_쪽은_뺀다()
        {
            Assert.AreEqual("🪙 100 · 200 XP", QuestFormat.Reward(100, 200));
            Assert.AreEqual("200 XP", QuestFormat.Reward(0, 200));
            Assert.AreEqual("🪙 1,500", QuestFormat.Reward(1500, 0));
        }

        [Test]
        public void 난이도_표기()
        {
            Assert.AreEqual("쉬움", QuestFormat.Tier(1));
            Assert.AreEqual("보통", QuestFormat.Tier(2));
            Assert.AreEqual("어려움", QuestFormat.Tier(3));
            Assert.AreEqual(string.Empty, QuestFormat.Tier(9));
        }

        [Test]
        public void 남은_시간()
        {
            Assert.AreEqual("2일 4시간 남음", QuestFormat.Remaining(Now.AddDays(2).AddHours(4).AddMinutes(30), Now));
            Assert.AreEqual("3시간 12분 남음", QuestFormat.Remaining(Now.AddHours(3).AddMinutes(12).AddSeconds(40), Now));
            Assert.AreEqual("12분 남음", QuestFormat.Remaining(Now.AddMinutes(12).AddSeconds(5), Now));
            Assert.AreEqual("곧 바뀌어요", QuestFormat.Remaining(Now.AddSeconds(59), Now));
            Assert.AreEqual("곧 바뀌어요", QuestFormat.Remaining(Now.AddMinutes(-5), Now));
        }

        [Test]
        public void 바꾸기_문구()
        {
            Assert.AreEqual("바꾸기 1번 남음", QuestFormat.Rerolls(1));
            Assert.AreEqual("오늘 바꾸기를 다 썼어요", QuestFormat.Rerolls(0));
            Assert.AreEqual(string.Empty, QuestFormat.RerollMessage(ResponseCode.SUCCESS));
            Assert.AreEqual("오늘은 더 바꿀 수 없어요", QuestFormat.RerollMessage(ResponseCode.QUEST_REROLL_UNAVAILABLE));
            Assert.AreEqual("끝난 퀘스트는 바꿀 수 없어요", QuestFormat.RerollMessage(ResponseCode.QUEST_ALREADY_COMPLETED));
            Assert.AreEqual("퀘스트가 바뀌어 다시 불러왔어요", QuestFormat.RerollMessage(ResponseCode.QUEST_NOT_FOUND));
            Assert.AreEqual("퀘스트를 쉬고 있어요", QuestFormat.RerollMessage(ResponseCode.ECONOMY_DISABLED));
            Assert.AreEqual("연결이 불안정해요. 다시 해 주세요", QuestFormat.RerollMessage(0));
        }

        [Test]
        public void 결과_줄_완료가_먼저_오늘이_이번_주보다_먼저()
        {
            var lines = QuestFormat.ResultLines(new[]
            {
                Q("아무 종목 4판 하기", "daily", 3, 4, false),
                Q("기절 없이 플래피 레이스 3번 완주하기", "weekly", 3, 3, true),
                Q("경기 시간 10분 채우기", "daily", 7, 10, false, "minutes"),
            });

            CollectionAssert.AreEqual(new[]
            {
                "이번 주 · 기절 없이 플래피 레이스 3번 완주하기 3/3 완료!",
                "오늘 · 아무 종목 4판 하기 3/4",
                "오늘 · 경기 시간 10분 채우기 7/10분",
            }, lines);
        }

        [Test]
        public void 결과_줄이_넘치면_마지막_자리에_외_N개()
        {
            var lines = QuestFormat.ResultLines(new[]
            {
                Q("a", "daily", 1, 2, false), Q("b", "daily", 1, 2, false), Q("c", "weekly", 1, 9, false), Q("d", "weekly", 1, 9, false),
            });

            CollectionAssert.AreEqual(new[] { "오늘 · a 1/2", "오늘 · b 1/2", "외 2개" }, lines);
        }

        [Test]
        public void 결과_줄_없거나_null이면_빈_목록()
        {
            Assert.AreEqual(0, QuestFormat.ResultLines(null).Count);
            Assert.AreEqual(0, QuestFormat.ResultLines(new MatchQuestProgressDto[] { null }).Count);
        }

        [Test]
        public void 화면_문구에_없는_글자를_안_쓴다()
        {
            var all = new[]
            {
                QuestFormat.Progress(3, 10, "minutes"), QuestFormat.Reward(100, 200), QuestFormat.Remaining(Now.AddDays(1), Now),
                QuestFormat.Rerolls(0), QuestFormat.RerollMessage(0), QuestFormat.Tier(3),
            }.Concat(QuestFormat.ResultLines(new[] { Q("x", "daily", 1, 1, true), Q("y", "weekly", 0, 1, false), Q("z", "daily", 0, 1, false), Q("w", "daily", 0, 1, false) }));

            foreach (var s in all)
            {
                foreach (var bad in new[] { "−", "⬆", "⬇" })
                {
                    Assert.IsFalse(s.Contains(bad, StringComparison.Ordinal), $"'{s}'에 '{bad}'");
                }
            }
        }
    }
}
