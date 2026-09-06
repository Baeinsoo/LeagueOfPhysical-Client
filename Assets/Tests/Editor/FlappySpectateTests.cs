using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// "지금 누구를 보고 있나"의 규칙. 카메라와 추격자 벽이 <b>같은 답</b>을 봐야 한다 —
    /// 벽은 보고 있는 새와 같은 시각으로 그려야 하는데(내 새는 앞, 남의 새는 뒤),
    /// 둘이 다른 새를 고르면 벽이 엉뚱한 시각에 그려진다.
    /// </summary>
    public class FlappySpectateTests
    {
        private sealed class FakeGameDataStore : IGameDataStore
        {
            public GameInfo gameInfo { get; set; }
            public string userEntityId { get; set; }
            public void Clear() { }
        }

        private static Entity Bird(string id, float x)
        {
            var bird = new Entity(id);
            bird.Add(new EntityKind(EntityType.Character));
            bird.Add(new GameFramework.World.Transform { Position = new System.Numerics.Vector3(x, 0f, 0f) });
            return bird;
        }

        private static EntityRegistry Registry(params Entity[] entities)
        {
            var registry = new EntityRegistry();
            foreach (var entity in entities)
            {
                registry.Add(entity);
            }
            return registry;
        }

        private static FlappySpectate Spectate(EntityRegistry registry, string myEntityId)
        {
            var spectate = new FlappySpectate(registry, new FakeGameDataStore { userEntityId = myEntityId });
            spectate.Refresh();
            return spectate;
        }

        [Test]
        public void 내_새가_살아_있으면_내_새를_본다()
        {
            var spectate = Spectate(Registry(Bird("me", 50f), Bird("other", 10f)), "me");

            Assert.AreEqual("me", spectate.Current);
        }

        [Test]
        public void 내_새가_없으면_가장_뒤처진_새를_본다()
        {
            //  선두가 아니라 꼴찌를 본다 — 다음에 잡힐 사람이라 벽이 같은 화면에 있다.
            var spectate = Spectate(Registry(Bird("a", 50f), Bird("b", 10f), Bird("c", 30f)), "me");

            Assert.AreEqual("b", spectate.Current);
        }

        [Test]
        public void 같은_자리면_id가_작은_쪽을_본다()
        {
            //  레지스트리 순회 순서는 정해져 있지 않다. 안 정하면 프레임마다 카메라가 오간다.
            var spectate = Spectate(Registry(Bird("b", 10f), Bird("a", 10f)), "me");

            Assert.AreEqual("a", spectate.Current);
        }

        [Test]
        public void 새가_하나도_없으면_아무도_안_본다()
        {
            var spectate = Spectate(Registry(), "me");

            Assert.IsNull(spectate.Current);
            Assert.AreEqual(0, spectate.Candidates.Count);
            Assert.DoesNotThrow(() => spectate.Next());   // 눌러도 터지지 않는다
        }

        [Test]
        public void 새가_아닌_것은_세지_않는다()
        {
            //  아이템도 레지스트리에 있고 x가 더 작을 수 있다. 카메라가 그쪽으로 가면 안 된다.
            var item = new Entity("item");
            item.Add(new EntityKind(EntityType.Item));
            item.Add(new GameFramework.World.Transform { Position = new System.Numerics.Vector3(-100f, 0f, 0f) });

            var spectate = Spectate(Registry(item, Bird("bird", 10f)), "me");

            Assert.AreEqual("bird", spectate.Current);
        }

        [Test]
        public void 목록은_선두부터다()
        {
            var spectate = Spectate(Registry(Bird("b", 10f), Bird("a", 50f), Bird("c", 30f)), "watcher");

            CollectionAssert.AreEqual(new[] { "a", "c", "b" }, spectate.Candidates);
        }

        [Test]
        public void 다음을_누르면_한_칸_뒤로_가고_끝에서_되돌아온다()
        {
            var spectate = Spectate(Registry(Bird("a", 50f), Bird("b", 10f), Bird("c", 30f)), "watcher");
            //  내가 없으므로 꼴찌 "b"(마지막)에서 시작한다.
            Assert.AreEqual("b", spectate.Current);

            spectate.Next();
            Assert.AreEqual("a", spectate.Current, "끝에서 처음으로 되돌아온다");

            spectate.Next();
            Assert.AreEqual("c", spectate.Current);
        }

        [Test]
        public void 이전을_누르면_한_칸_앞으로_가고_처음에서_되돌아온다()
        {
            var spectate = Spectate(Registry(Bird("a", 50f), Bird("b", 10f), Bird("c", 30f)), "watcher");
            Assert.AreEqual("b", spectate.Current);

            spectate.Prev();
            Assert.AreEqual("c", spectate.Current);

            spectate.Prev();
            Assert.AreEqual("a", spectate.Current);

            spectate.Prev();
            Assert.AreEqual("b", spectate.Current, "처음에서 끝으로 되돌아온다");
        }

        [Test]
        public void 내_새가_완주하면_후보에서_빠진다()
        {
            //  내 새의 통과는 클라 시뮬이 직접 판정한다(FinishState).
            var mine = Bird("me", 50f);
            mine.Add(new FinishState { FinishedTick = 100 });

            var spectate = Spectate(Registry(mine, Bird("other", 10f)), "me");

            Assert.AreEqual("other", spectate.Current);
            CollectionAssert.DoesNotContain(spectate.Candidates, "me");
        }

        [Test]
        public void 남의_새가_완주하면_후보에서_빠진다()
        {
            //  남의 통과는 클라가 판정하지 않는다 — 서버가 스냅샷에 실어 준 등수로만 안다.
            var other = Bird("other", 50f);
            other.Add(new FinishPlacement { Value = 1 });

            var spectate = Spectate(Registry(other, Bird("last", 10f)), "watcher");

            Assert.AreEqual("last", spectate.Current);
            CollectionAssert.DoesNotContain(spectate.Candidates, "other");
        }

        [Test]
        public void 보던_사람이_사라지면_다시_고른다()
        {
            var registry = Registry(Bird("a", 50f), Bird("b", 10f), Bird("c", 30f));
            var spectate = Spectate(registry, "watcher");
            spectate.Next();
            Assert.AreEqual("a", spectate.Current);

            registry.Remove("a");
            spectate.Refresh();

            Assert.AreEqual("b", spectate.Current, "사라졌으면 꼴찌 규칙으로 돌아간다");
        }

        [Test]
        public void 보던_사람이_그대로면_수동_선택이_유지된다()
        {
            //  이게 깨지면 자동 추적이 매 틱 수동 선택을 덮어써서 ◀ ▶ 가 아무 소용이 없다.
            var registry = Registry(Bird("a", 50f), Bird("b", 10f), Bird("c", 30f));
            var spectate = Spectate(registry, "watcher");
            spectate.Next();
            Assert.AreEqual("a", spectate.Current);

            spectate.Refresh();
            spectate.Refresh();

            Assert.AreEqual("a", spectate.Current);
        }

        [Test]
        public void 내가_완주한_뒤_고른_사람이_유지된다()
        {
            //  이 기능의 핵심 약속이다. 완주하면 나는 후보에서 빠지지만 레지스트리에는 남아 있는데,
            //  "내가 후보에 있으면 나" 가드가 그 상태까지 잡아채면 ◀ ▶ 가 매 틱 무효가 된다.
            var mine = Bird("me", 90f);
            mine.Add(new FinishState { FinishedTick = 100 });
            var registry = Registry(mine, Bird("a", 50f), Bird("b", 10f));
            var spectate = new FlappySpectate(registry, new FakeGameDataStore { userEntityId = "me" });
            spectate.Refresh();
            Assert.AreEqual("b", spectate.Current, "완주한 나는 후보에서 빠지고 꼴찌부터 본다");

            spectate.Next();
            Assert.AreEqual("a", spectate.Current);

            spectate.Refresh();
            spectate.Refresh();

            Assert.AreEqual("a", spectate.Current);
        }

        [Test]
        public void 내_새가_나중에_등록되면_나에게_돌아온다()
        {
            //  스폰 순서는 보장되지 않는다. 내 새가 등록되기 전에 한 틱이라도 돌면 남을 고르는데,
            //  그 선택이 굳어 버리면 내가 달리는 내내 남을 보게 된다.
            var registry = Registry(Bird("other", 10f));
            var spectate = new FlappySpectate(registry, new FakeGameDataStore { userEntityId = "me" });
            spectate.Refresh();
            Assert.AreEqual("other", spectate.Current);

            //  내 새는 선두다 — 꼴찌 폴백이 아니라 "나" 규칙이 골랐음을 가른다.
            registry.Add(Bird("me", 50f));
            spectate.Refresh();

            Assert.AreEqual("me", spectate.Current);
        }
    }
}
