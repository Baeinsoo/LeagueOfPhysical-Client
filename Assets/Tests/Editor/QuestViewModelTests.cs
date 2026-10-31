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
            Assert.IsTrue(vm.Daily.CurrentValue[0].ShowReroll, "잠겼을 뿐 버튼 자리는 그대로 — spec §7 '잠김'(m1)");
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

        //  m1: 남은 횟수 0이어도 일일 안 끝난 칸은 버튼 자리가 있다 — 잠겼을 뿐이다. 주간은 자리 자체가 없다.
        [Test]
        public void 남은_횟수_0도_버튼_자리는_유지한다()
        {
            var (vm, _, _, _) = Build(() => Ok(rerollsLeft: 0));
            Assert.IsTrue(vm.Daily.CurrentValue[0].ShowReroll);
            Assert.IsFalse(vm.Daily.CurrentValue[0].CanReroll);
            Assert.IsFalse(vm.Daily.CurrentValue[1].ShowReroll, "끝난 칸은 버튼 자리가 없다");
            Assert.IsFalse(vm.Weekly.CurrentValue[0].ShowReroll, "주간은 버튼 자리 자체가 없다");
        }

        //  m3: 아직·못 받았으면 화면이 "오늘"·"이번 주" 빈 머리를 숨긴다(QuestView가 HasData로 quest-scroll을 접는다).
        [Test]
        public void 못_받으면_HasData가_거짓이고_받으면_참이다()
        {
            var (broken, _, _, _) = Build(() => throw new Exception("404"));
            Assert.IsFalse(broken.HasData.CurrentValue);

            var (vm, _, _, _) = Build(() => Ok());
            Assert.IsTrue(vm.HasData.CurrentValue);
        }

        //  I2: 리셋 다시 받기가 실패해도(오프라인·Disabled·같은 기간 응답) 매 Tick(1초)마다 보내지 않는다 — 물러서기 동안은 한 번만.
        [Test]
        public void 리셋_다시_받기가_실패해도_매_틱마다_보내지_않는다()
        {
            int fetches = 0;
            var store = new QuestStore(new FakeUsers(), new FakeSeen(),
                (u, ct) =>
                {
                    fetches++;
                    //  처음엔 이미 끝난 일일 기간으로 받고, 그 뒤로는 계속 실패한다(오프라인 흉내).
                    if (fetches == 1) return UniTask.FromResult(Ok(dailyEnds: Server.AddMinutes(-1)));
                    throw new Exception("network down");
                },
                (u, r, ct) => UniTask.FromResult(new RerollQuestResponse { code = ResponseCode.SUCCESS }),
                () => Server);
            var vm = new QuestViewModel(store);
            int afterOpen = fetches;

            for (int i = 0; i < 5; i++) vm.Tick();

            Assert.AreEqual(afterOpen + 1, fetches, "물러서기 동안은 한 번만 더 보낸다 — 매 틱마다 치지 않는다");
        }

        //  I2: 다시 받기가 성공해 새 기간(미래 endsAt)이 오면 더는 다시 보내지 않는다.
        [Test]
        public void 리셋_다시_받기가_성공하면_새_기간이라_더_안_보낸다()
        {
            int fetches = 0;
            DateTime nextEnds = Server.AddHours(20);
            var store = new QuestStore(new FakeUsers(), new FakeSeen(),
                (u, ct) =>
                {
                    fetches++;
                    return UniTask.FromResult(Ok(dailyEnds: fetches == 1 ? Server.AddMinutes(-1) : nextEnds));
                },
                (u, r, ct) => UniTask.FromResult(new RerollQuestResponse { code = ResponseCode.SUCCESS }),
                () => Server);
            var vm = new QuestViewModel(store);

            vm.Tick();
            vm.Tick();
            vm.Tick();

            Assert.AreEqual(2, fetches, "두 번째부터는 끝난 시각이 미래라 더 안 보낸다");
            Assert.AreEqual(nextEnds, store.Daily.CurrentValue.endsAt);
        }

        //  m2: 바꾸기가 80012로 되돌아와 다시 받은 뒤, 그사이 새로 끝난 칸은 "봤다"로 적는다 — 안 그러면 판에서 본
        //  완료가 로비 배지로 다시 뜬다.
        [Test]
        public void 사라진_칸_다시_받은_뒤_새로_끝난_칸을_본_것으로_적는다()
        {
            var seen = new FakeSeen();
            int fetches = 0;
            var store = new QuestStore(new FakeUsers(), seen,
                (u, ct) =>
                {
                    fetches++;
                    var daily = new QuestDailyDto
                    {
                        periodKey = "2026-10-30", endsAt = Server.AddHours(3), rerollsLeft = 1,
                        //  처음엔 d1이 안 끝났고, 바꾸기 뒤 다시 받으면 퀘스트가 바뀌어 끝난 칸으로 온다.
                        quests = new[] { new QuestItemDto { id = "d1", tier = 1, title = "d1", unit = "count", progress = fetches == 1 ? 1 : 2, target = 2, completed = fetches != 1, gameModeIds = new int[0] } },
                    };
                    return UniTask.FromResult(new GetQuestsResponse
                    {
                        code = ResponseCode.SUCCESS, serverNow = Server, daily = daily,
                        weekly = new QuestWeeklyDto { periodKey = "2026-10-26", endsAt = Server.AddDays(2), quests = new QuestItemDto[0] },
                    });
                },
                (u, r, ct) => UniTask.FromResult(new RerollQuestResponse { code = ResponseCode.QUEST_NOT_FOUND }),
                () => Server);
            var vm = new QuestViewModel(store);
            Assert.IsFalse(seen.Ids.Contains("d1"), "처음엔 안 끝나 본 칸이 아니다");

            vm.RerollAsync("d1").GetAwaiter().GetResult();

            Assert.IsTrue(seen.Ids.Contains("d1"), "다시 받은 뒤 끝난 칸을 본 것으로 적는다");
            Assert.AreEqual(0, store.UnseenCompleted.CurrentValue, "그래서 로비 배지로 다시 안 뜬다");
        }

        //  m2: 리셋 다시 받기도 같은 이유로 MarkAllSeen을 부른다.
        [Test]
        public void 리셋_다시_받은_뒤_새로_끝난_칸도_본_것으로_적는다()
        {
            var seen = new FakeSeen();
            int fetches = 0;
            var store = new QuestStore(new FakeUsers(), seen,
                (u, ct) =>
                {
                    fetches++;
                    var daily = new QuestDailyDto
                    {
                        periodKey = "2026-10-30",
                        //  처음엔 이미 끝난 기간으로 와 리셋 다시 받기를 일으키고, 그다음엔 새 기간·끝난 칸으로 온다.
                        endsAt = fetches == 1 ? Server.AddMinutes(-1) : Server.AddHours(20),
                        rerollsLeft = 1,
                        quests = new[] { new QuestItemDto { id = "d1", tier = 1, title = "d1", unit = "count", progress = fetches == 1 ? 1 : 2, target = 2, completed = fetches != 1, gameModeIds = new int[0] } },
                    };
                    return UniTask.FromResult(new GetQuestsResponse
                    {
                        code = ResponseCode.SUCCESS, serverNow = Server, daily = daily,
                        weekly = new QuestWeeklyDto { periodKey = "2026-10-26", endsAt = Server.AddDays(2), quests = new QuestItemDto[0] },
                    });
                },
                (u, r, ct) => UniTask.FromResult(new RerollQuestResponse { code = ResponseCode.SUCCESS }),
                () => Server);
            var vm = new QuestViewModel(store);
            Assert.IsFalse(seen.Ids.Contains("d1"));

            vm.Tick();

            Assert.IsTrue(seen.Ids.Contains("d1"), "리셋으로 다시 받은 뒤 끝난 칸을 본 것으로 적는다");
            Assert.AreEqual(0, store.UnseenCompleted.CurrentValue);
        }
    }
}
