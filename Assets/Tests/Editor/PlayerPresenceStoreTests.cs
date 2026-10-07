using System.Collections.Generic;
using NUnit.Framework;
using R3;

namespace LOP.Tests
{
    /// <summary>판 도중 끊긴 사람 — 서버가 보낸 목록에서 바뀐 것만 알린다(끊김·재접속), 늦게 온 옛 소식은 버린다.</summary>
    public class PlayerPresenceStoreTests
    {
        static PlayerPresenceToC Msg(int version, params (string entity, int slot)[] away)
        {
            var m = new PlayerPresenceToC { Version = version };
            foreach (var (entity, slot) in away) m.Away.Add(new PresenceAwayWire { EntityId = entity, Slot = slot });
            return m;
        }

        [Test]
        public void 끊김과_재접속을_알린다()
        {
            var store = new PlayerPresenceStore();
            var seen = new List<PresenceChange>();
            using var _ = store.Changes.Subscribe(seen.Add);

            store.Apply(Msg(1, ("e2", 2)));
            Assert.IsTrue(store.IsAway("e2"));
            store.Apply(Msg(2));
            Assert.IsFalse(store.IsAway("e2"));

            Assert.AreEqual(2, seen.Count);
            Assert.AreEqual(new PresenceChange("e2", 2, true), seen[0]);
            Assert.AreEqual(new PresenceChange("e2", 2, false), seen[1]);
        }

        [Test]
        public void 같은_목록이_다시_와도_다시_알리지_않는다()
        {
            var store = new PlayerPresenceStore();
            var seen = new List<PresenceChange>();
            using var _ = store.Changes.Subscribe(seen.Add);
            store.Apply(Msg(1, ("e2", 2)));
            store.Apply(Msg(2, ("e2", 2)));
            Assert.AreEqual(1, seen.Count);
        }

        [Test]
        public void 옛_판본은_버린다()
        {
            var store = new PlayerPresenceStore();
            store.Apply(Msg(5, ("e2", 2)));
            store.Apply(Msg(3));
            Assert.IsTrue(store.IsAway("e2"));
        }

        [Test]
        public void 알림_문구()
        {
            Assert.AreEqual("2P 선수 연결 끊김", PresenceText.For(new PresenceChange("e2", 2, true)));
            Assert.AreEqual("2P 선수 재접속", PresenceText.For(new PresenceChange("e2", 2, false)));
        }

        //  피하기는 토스트 대신 중계 자막 — 이름은 자막이 늘 쓰는 "nP 선수"(엔티티 id 순)를 따른다.
        [Test]
        public void 피하기_자막은_자막의_선수_이름으로()
        {
            var ids = new[] { "b", "a" };
            Assert.AreEqual("2P 선수 연결 끊김", DodgeCaptions.PresenceLine(ids, new PresenceChange("b", 1, true)));
            Assert.AreEqual("1P 선수 재접속", DodgeCaptions.PresenceLine(ids, new PresenceChange("a", 2, false)));
        }

        [Test]
        public void 판치기_점수판_이름()
        {
            //  이름 칸은 40px라 이름은 그대로 두고, 끊김은 줄 오른쪽 바깥 꼬리표로 따로 붙인다.
            Assert.AreEqual("나", LOP.UI.PanchigiTurnViewModel.RowName(isMe: true));
            Assert.AreEqual("상대", LOP.UI.PanchigiTurnViewModel.RowName(isMe: false));
            Assert.AreEqual("연결 끊김", LOP.UI.PanchigiTurnViewModel.AwayTag(away: true));
            Assert.AreEqual("", LOP.UI.PanchigiTurnViewModel.AwayTag(away: false));
        }
    }
}
