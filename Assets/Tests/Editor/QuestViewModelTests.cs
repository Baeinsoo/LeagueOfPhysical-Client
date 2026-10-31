using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using LOP.UI;
using NUnit.Framework;
using R3;

namespace LOP.Tests
{
    /// <summary>퀘스트 판 VM(spec 2026-10-30 §7) — 카드·남은 시간·바꾸기·문구·배지 지우기.</summary>
    public class QuestViewModelTests
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

        private static GetQuestsResponse Ok(int rerollsLeft = 1, DateTime? dailyEnds = null) => new GetQuestsResponse
        {
            code = ResponseCode.SUCCESS,
            serverNow = Server,
            daily = new QuestDailyDto
            {
                periodKey = "2026-10-30", endsAt = dailyEnds ?? Server.AddHours(3).AddMinutes(12), rerollsLeft = rerollsLeft,
                quests = new[]
                {
                    new QuestItemDto { id = "d1", tier = 1, title = "경기 시간 5분 채우기", unit = "minutes", progress = 2, target = 5, rewardCoins = 50, rewardXp = 100, gameModeIds = new int[0] },
                    new QuestItemDto { id = "d2", tier = 2, title = "아무 종목 4판 하기", unit = "count", progress = 4, target = 4, completed = true, rewardCoins = 100, rewardXp = 200, gameModeIds = new int[0] },
                },
            },
            weekly = new QuestWeeklyDto
            {
                periodKey = "2026-10-26", endsAt = Server.AddDays(2).AddHours(4),
                quests = new[] { new QuestItemDto { id = "w1", tier = 3, title = "1위 3번 하기", unit = "count", progress = 1, target = 3, rewardCoins = 500, rewardXp = 1000, gameModeIds = new int[0] } },
            },
        };

        private static (QuestViewModel vm, QuestStore store, FakeSeen seen, List<string> rerolled) Build(
            Func<GetQuestsResponse> fetch, Func<RerollQuestResponse> reroll = null)
        {
            var seen = new FakeSeen();
            var rerolled = new List<string>();
            var store = new QuestStore(new FakeUsers(), seen,
                (u, ct) => UniTask.FromResult(fetch()),
                (u, r, ct) => { rerolled.Add(r.questId); return UniTask.FromResult(reroll?.Invoke() ?? new RerollQuestResponse { code = ResponseCode.SUCCESS, daily = Ok(0).daily }); },
                () => Server);
            return (new QuestViewModel(store), store, seen, rerolled);
        }

        [Test]
        public void 카드는_제목_난이도_진행_보상_완료를_싣는다()
        {
            var (vm, _, _, _) = Build(() => Ok());

            var d = vm.Daily.CurrentValue;
            Assert.AreEqual(2, d.Count);
            Assert.AreEqual("경기 시간 5분 채우기", d[0].Title);
            Assert.AreEqual("쉬움", d[0].TierText);
            Assert.AreEqual("2/5분", d[0].ProgressText);
            Assert.AreEqual(0.4f, d[0].Ratio, 1e-5f);
            Assert.AreEqual("🪙 50 · 100 XP", d[0].RewardText);
            Assert.IsFalse(d[0].Completed);
            Assert.IsTrue(d[1].Completed);
            Assert.AreEqual("1위 3번 하기", vm.Weekly.CurrentValue[0].Title);
        }

        [Test]
        public void 바꾸기는_안_끝난_일일_칸만_남은_횟수가_있을_때만()
        {
            var (vm, _, _, _) = Build(() => Ok(rerollsLeft: 1));
            Assert.IsTrue(vm.Daily.CurrentValue[0].CanReroll);
            Assert.IsFalse(vm.Daily.CurrentValue[1].CanReroll, "끝난 칸");
            Assert.IsFalse(vm.Weekly.CurrentValue[0].CanReroll, "주간은 안 바꾼다");
            Assert.AreEqual("바꾸기 1번 남음", vm.RerollText.CurrentValue);

            var (spent, _, _, _) = Build(() => Ok(rerollsLeft: 0));
            Assert.IsFalse(spent.Daily.CurrentValue[0].CanReroll);
            Assert.AreEqual("오늘 바꾸기를 다 썼어요", spent.RerollText.CurrentValue);
        }

        [Test]
        public void 남은_시간은_틱마다_서버_시각으로()
        {
            var (vm, _, _, _) = Build(() => Ok());
            vm.Tick();
            Assert.AreEqual("3시간 12분 남음", vm.DailyRemaining.CurrentValue);
            Assert.AreEqual("2일 4시간 남음", vm.WeeklyRemaining.CurrentValue);
        }

        [Test]
        public void 여는_순간_끝난_칸은_본_것으로_적는다()
        {
            var (_, store, seen, _) = Build(() => Ok());
            Assert.IsTrue(seen.Ids.Contains("d2"));
            Assert.AreEqual(0, store.UnseenCompleted.CurrentValue);
        }

        [Test]
        public void 바꾸기_성공이면_문구가_비고_실패면_까닭을_말한다()
        {
            var (vm, _, _, rerolled) = Build(() => Ok());
            vm.RerollAsync("d1").GetAwaiter().GetResult();
            CollectionAssert.AreEqual(new[] { "d1" }, rerolled);
            Assert.AreEqual(string.Empty, vm.Message.CurrentValue);
            Assert.AreEqual("오늘 바꾸기를 다 썼어요", vm.RerollText.CurrentValue, "응답의 일일 묶음(남은 0)으로 덮였다");

            var (bad, _, _, _) = Build(() => Ok(), () => new RerollQuestResponse { code = ResponseCode.QUEST_REROLL_UNAVAILABLE });
            bad.RerollAsync("d1").GetAwaiter().GetResult();
            Assert.AreEqual("오늘은 더 바꿀 수 없어요", bad.Message.CurrentValue);
        }

        [Test]
        public void 칸이_사라졌다는_답이면_다시_받는다()
        {
            int fetches = 0;
            var (vm, _, _, _) = Build(() => { fetches++; return Ok(); }, () => new RerollQuestResponse { code = ResponseCode.QUEST_NOT_FOUND });
            int before = fetches;
            vm.RerollAsync("d1").GetAwaiter().GetResult();
            Assert.AreEqual(before + 1, fetches);
            Assert.AreEqual("퀘스트가 바뀌어 다시 불러왔어요", vm.Message.CurrentValue);
        }

        [Test]
        public void 바꾸는_동안엔_바꾸기를_막는다()
        {
            var gate = new UniTaskCompletionSource<RerollQuestResponse>();
            var seen = new FakeSeen();
            int calls = 0;
            var store = new QuestStore(new FakeUsers(), seen, (u, ct) => UniTask.FromResult(Ok()),
                (u, r, ct) => { calls++; return gate.Task; }, () => Server);
            var vm = new QuestViewModel(store);

            vm.RerollAsync("d1").Forget();
            Assert.IsTrue(vm.Busy.CurrentValue);
            Assert.IsFalse(vm.Daily.CurrentValue[0].CanReroll);
            vm.RerollAsync("d1").Forget();
            Assert.AreEqual(1, calls, "응답 전 두 번째는 안 보낸다");

            gate.TrySetResult(new RerollQuestResponse { code = ResponseCode.SUCCESS, daily = Ok(0).daily });
            Assert.IsFalse(vm.Busy.CurrentValue);
        }

        [Test]
        public void 꺼져_있거나_못_받으면_그렇게_말한다()
        {
            var (off, _, _, _) = Build(() => new GetQuestsResponse { code = ResponseCode.ECONOMY_DISABLED });
            Assert.AreEqual("퀘스트를 쉬고 있어요", off.Message.CurrentValue);

            var (broken, _, _, _) = Build(() => throw new Exception("404"));
            Assert.AreEqual("퀘스트를 불러오지 못했어요", broken.Message.CurrentValue);
        }
    }
}
