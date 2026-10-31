using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;

namespace LOP.Tests
{
    /// <summary>QuestStore(spec 2026-10-30 §7) — 받기·실패·꺼짐·바꾸기·서버 시각·배지. 가짜 조회를 꽂는다(EconomyStoreTests와 같은 방식).</summary>
    public class QuestStoreTests
    {
        private sealed class FakeUsers : IUserDataStore
        {
            public User user { get; set; } = new User { id = "me" };
            public UserProfile userProfile { get; set; }
            public ReadOnlyReactiveProperty<UserLocation> userLocation => null;
            public IReadOnlyDictionary<int, UserRating> userRatingByQueueId => null;
            public void Clear() { }
        }

        private sealed class FakeSeen : IQuestSeenStore
        {
            public readonly HashSet<string> Ids = new HashSet<string>();
            public bool IsSeen(string questId) => Ids.Contains(questId);
            public void MarkSeen(IEnumerable<string> questIds) { foreach (var id in questIds) Ids.Add(id); }
        }

        private static readonly DateTime Server = new DateTime(2026, 10, 30, 3, 0, 0, DateTimeKind.Utc);

        private static QuestItemDto Item(string id, bool completed = false) =>
            new QuestItemDto { id = id, tier = 1, title = id, unit = "count", progress = completed ? 2 : 0, target = 2, completed = completed, gameModeIds = new int[0] };

        private static GetQuestsResponse Ok(params QuestItemDto[] daily) => new GetQuestsResponse
        {
            code = ResponseCode.SUCCESS,
            serverNow = Server,
            daily = new QuestDailyDto { periodKey = "2026-10-30", endsAt = Server.AddHours(17), rerollsLeft = 1, quests = daily },
            weekly = new QuestWeeklyDto { periodKey = "2026-10-26", endsAt = Server.AddDays(2).AddHours(17), quests = new[] { Item("w1", completed: true) } },
        };

        private static QuestStore Store(Func<string, CancellationToken, UniTask<GetQuestsResponse>> fetch, FakeSeen seen = null,
            Func<string, RerollQuestRequest, CancellationToken, UniTask<RerollQuestResponse>> reroll = null, Func<DateTime> utcNow = null) =>
            new QuestStore(new FakeUsers(), seen ?? new FakeSeen(), fetch,
                reroll ?? ((u, r, ct) => UniTask.FromResult(new RerollQuestResponse { code = ResponseCode.SUCCESS })),
                utcNow ?? (() => Server));

        [Test]
        public void 받으면_일일_주간을_싣고_Ready()
        {
            var store = Store((u, ct) => UniTask.FromResult(Ok(Item("d1"), Item("d2"))));
            Assert.AreEqual(QuestLoadState.Loading, store.State.CurrentValue);

            Assert.IsTrue(store.RefreshAsync(CancellationToken.None).GetAwaiter().GetResult());

            Assert.AreEqual(QuestLoadState.Ready, store.State.CurrentValue);
            Assert.AreEqual(2, store.Daily.CurrentValue.quests.Length);
            Assert.AreEqual("w1", store.Weekly.CurrentValue.quests[0].id);
        }

        [Test]
        public void 꺼져_있으면_Disabled()
        {
            var store = Store((u, ct) => UniTask.FromResult(new GetQuestsResponse { code = ResponseCode.ECONOMY_DISABLED }));
            store.RefreshAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(QuestLoadState.Disabled, store.State.CurrentValue);
        }

        [Test]
        public void 실패하면_옛_값을_두고_처음이면_Failed()
        {
            int calls = 0;
            var store = Store((u, ct) =>
            {
                calls++;
                if (calls == 1) return UniTask.FromResult(Ok(Item("d1")));
                throw new Exception("404");
            });
            store.RefreshAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsFalse(store.RefreshAsync(CancellationToken.None).GetAwaiter().GetResult());
            Assert.AreEqual("d1", store.Daily.CurrentValue.quests[0].id, "옛 값이 남는다");
            Assert.AreEqual(QuestLoadState.Ready, store.State.CurrentValue);

            var fresh = Store((u, ct) => throw new Exception("404"));
            fresh.RefreshAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(QuestLoadState.Failed, fresh.State.CurrentValue);
        }

        [Test]
        public void 남은_시간은_서버_시각_차이로_센다()
        {
            //  기기 시계가 서버보다 2시간 빠르다 — 받은 뒤 10분이 흐르면 서버 기준 03:10이어야 한다.
            DateTime device = Server.AddHours(2);
            var store = Store((u, ct) => UniTask.FromResult(Ok(Item("d1"))), utcNow: () => device);
            store.RefreshAsync(CancellationToken.None).GetAwaiter().GetResult();

            device = device.AddMinutes(10);

            Assert.AreEqual(Server.AddMinutes(10), store.ServerNow);
        }

        [Test]
        public void 배지는_본_적_없는_끝난_칸_수이고_다_보면_0()
        {
            var seen = new FakeSeen();
            var store = Store((u, ct) => UniTask.FromResult(Ok(Item("d1", completed: true), Item("d2"))), seen);
            store.RefreshAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(2, store.UnseenCompleted.CurrentValue, "일일 d1 + 주간 w1");
            Assert.AreEqual(1, store.CompletedDaily.CurrentValue);

            store.MarkAllSeen();

            Assert.AreEqual(0, store.UnseenCompleted.CurrentValue);
            CollectionAssert.AreEquivalent(new[] { "d1", "w1" }, seen.Ids.ToArray(), "안 끝난 칸은 '봤다'로 적지 않는다");
        }

        [Test]
        public void 다시_받아도_본_칸은_배지에_안_센다()
        {
            var seen = new FakeSeen();
            seen.Ids.Add("d1");
            var store = Store((u, ct) => UniTask.FromResult(Ok(Item("d1", completed: true))), seen);
            store.RefreshAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(1, store.UnseenCompleted.CurrentValue, "w1만");
        }

        [Test]
        public void 바꾸기가_성공하면_일일_묶음을_덮고_코드를_돌려준다()
        {
            var changed = new QuestDailyDto { periodKey = "2026-10-30", endsAt = Server.AddHours(17), rerollsLeft = 0, quests = new[] { Item("d1") } };
            changed.quests[0].title = "새 퀘스트";
            RerollQuestRequest sent = null;
            var store = Store((u, ct) => UniTask.FromResult(Ok(Item("d1"))),
                reroll: (u, r, ct) => { sent = r; return UniTask.FromResult(new RerollQuestResponse { code = ResponseCode.SUCCESS, daily = changed }); });
            store.RefreshAsync(CancellationToken.None).GetAwaiter().GetResult();

            int code = store.RerollAsync("d1", CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(ResponseCode.SUCCESS, code);
            Assert.AreEqual("d1", sent.questId);
            Assert.AreEqual("새 퀘스트", store.Daily.CurrentValue.quests[0].title);
            Assert.AreEqual(0, store.Daily.CurrentValue.rerollsLeft);
        }

        [Test]
        public void 바꾸기_실패는_코드만_돌려주고_연결_실패는_0()
        {
            var store = Store((u, ct) => UniTask.FromResult(Ok(Item("d1"))),
                reroll: (u, r, ct) => UniTask.FromResult(new RerollQuestResponse { code = ResponseCode.QUEST_REROLL_UNAVAILABLE }));
            store.RefreshAsync(CancellationToken.None).GetAwaiter().GetResult();
            var before = store.Daily.CurrentValue;

            Assert.AreEqual(ResponseCode.QUEST_REROLL_UNAVAILABLE, store.RerollAsync("d1", CancellationToken.None).GetAwaiter().GetResult());
            Assert.AreSame(before, store.Daily.CurrentValue);

            var broken = Store((u, ct) => UniTask.FromResult(Ok(Item("d1"))), reroll: (u, r, ct) => throw new Exception("net"));
            Assert.AreEqual(0, broken.RerollAsync("d1", CancellationToken.None).GetAwaiter().GetResult());
        }
    }
}
