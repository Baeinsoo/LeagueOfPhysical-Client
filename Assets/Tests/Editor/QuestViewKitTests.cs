using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using LOP.UI;
using NUnit.Framework;
using R3;
using UnityEditor;
using UnityEngine.UIElements;

namespace LOP.Tests
{
    /// <summary>키트 퀘스트 판 — 셸 틀, 오늘·이번 주 묶음 요소, 카탈로그 등록.</summary>
    public class QuestViewKitTests
    {
        private const string Uxml = "Assets/UI/Quest/QuestView.uxml";
        private const string Uss = "Assets/UI/Quest/QuestView.uss";

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
            public bool IsSeen(string questId) => false;
            public void MarkSeen(IEnumerable<string> questIds) { }
        }

        [Test]
        public void 필수_요소()
        {
            var x = File.ReadAllText(Uxml);
            ShellFrameTests.AssertShellFrame(x);
            foreach (var n in new[] { "quest-message", "quest-scroll", "quest-daily-remaining", "quest-reroll-left", "quest-daily-list", "quest-weekly-remaining", "quest-weekly-list" })
            {
                StringAssert.Contains($"name=\"{n}\"", x);
            }
            StringAssert.Contains("text=\"오늘\"", x);
            StringAssert.Contains("text=\"이번 주\"", x);
        }

        [Test]
        public void 끝난_칸_규칙과_색()
        {
            var s = File.ReadAllText(Uss);
            StringAssert.Contains(".quest-card--done", s);
            StringAssert.Contains(".quest-card__stamp", s);
            StringAssert.DoesNotContain("#000", s);
        }

        [Test]
        public void 카탈로그에_있다() => StringAssert.Contains("viewName: QuestView", File.ReadAllText("Assets/UI/UIViewCatalog.asset"));

        //  사람 확인: 바꾸기 버튼이 --primary나 --info 없이 스티커 기본(크림) 바탕에 흰 글자를 쓰면 거의 안 보인다
        //  (다른 kit-btn은 전부 이 둘 중 하나를 쓴다). 버튼을 실제로 그려 꾸밈 클래스를 지킨다.
        [Test]
        public void 바꾸기_버튼은_대비_있는_꾸밈_클래스를_쓴다()
        {
            var daily = new QuestDailyDto
            {
                periodKey = "x", endsAt = DateTime.UtcNow.AddHours(3), rerollsLeft = 1,
                quests = new[] { new QuestItemDto { id = "d1", tier = 1, title = "d1", unit = "count", progress = 1, target = 2, gameModeIds = new int[0] } },
            };
            var weekly = new QuestWeeklyDto { periodKey = "y", endsAt = DateTime.UtcNow.AddDays(2), quests = new QuestItemDto[0] };
            var response = new GetQuestsResponse { code = ResponseCode.SUCCESS, serverNow = DateTime.UtcNow, daily = daily, weekly = weekly };
            var store = new QuestStore(new FakeUsers(), new FakeSeen(),
                (u, ct) => UniTask.FromResult(response),
                (u, r, ct) => UniTask.FromResult(new RerollQuestResponse { code = ResponseCode.SUCCESS }),
                () => DateTime.UtcNow);
            var vm = new QuestViewModel(store);
            var view = new QuestView(vm);

            var root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Uxml).Instantiate();
            view.Initialize(root);
            view.OnOpen();

            var reroll = root.Q<Button>(className: "quest-card__reroll");
            Assert.IsNotNull(reroll, "바꾸기 버튼이 그려졌다");
            Assert.IsTrue(reroll.ClassListContains("kit-btn--info") || reroll.ClassListContains("kit-btn--primary"),
                "다른 kit-btn처럼 --info나 --primary로 바탕 대비를 준다");

            view.Dispose();
        }
    }
}
