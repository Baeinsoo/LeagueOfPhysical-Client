using System.Collections.Generic;
using LOP.UI;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    //  상태 메시지 -> KnockbackClientState -> 중계 방송기 -> 자막 문장까지, 실제 받는 길을 지나게 한다.
    public class KnockbackKoBroadcasterTests
    {
        static GameFramework.World.Entity Body(string id, string name, bool withPosition)
        {
            var e = new GameFramework.World.Entity(id);
            e.Add(new EntityKind(EntityType.Character));
            e.Add(new PlayerLook(null, name, 1));
            if (withPosition)
            {
                e.Add(new GameFramework.World.Transform { Position = new System.Numerics.Vector3(3f, 0f, 4f) });
            }
            return e;
        }

        static KnockbackStateToC Msg(int version, long bGone, string bBy)
        {
            var m = new KnockbackStateToC { Version = version };
            m.Players.Add(new KnockbackPlayerWire { EntityId = "a", EliminatedTick = -1 });
            m.Players.Add(new KnockbackPlayerWire { EntityId = "b", EliminatedTick = bGone, KoBy = bBy });
            return m;
        }

        [Test]
        public void 상태가_들어오면_장외_한_줄이_생기고_문장이_맞다()
        {
            var state = new KnockbackClientState();
            var registry = new GameFramework.World.EntityRegistry();
            registry.Add(Body("a", "철수", true));
            registry.Add(Body("b", "영희", true));
            var broadcaster = new KnockbackKoBroadcaster(state, registry, null);
            var lines = new List<string>();
            var finishes = new List<Vector3?>();
            broadcaster.Ko += a => lines.Add(KnockbackHudViewModel.KoLine(a));
            broadcaster.Finished += p => finishes.Add(p);

            state.Apply(Msg(1, -1, ""));
            Assert.AreEqual(0, lines.Count, "아무도 탈락하지 않았다");

            state.Apply(Msg(2, 100, "a"));
            Assert.AreEqual(1, lines.Count);
            Assert.AreEqual("철수의 한 방! 영희 장외", lines[0]);
            Assert.AreEqual(1, finishes.Count, "둘 중 하나가 남아 판이 끝난다");
            Assert.AreEqual(new Vector3(3f, 0f, 4f), finishes[0].Value);

            state.Apply(Msg(3, 100, "a"));
            Assert.AreEqual(1, lines.Count, "같은 탈락은 다시 알리지 않는다");
        }

        [Test]
        public void 자멸은_스스로_장외_문장이다()
        {
            var state = new KnockbackClientState();
            var registry = new GameFramework.World.EntityRegistry();
            registry.Add(Body("b", "영희", true));
            var broadcaster = new KnockbackKoBroadcaster(state, registry, null);
            var lines = new List<string>();
            broadcaster.Ko += a => lines.Add(KnockbackHudViewModel.KoLine(a));

            state.Apply(Msg(1, -1, ""));
            state.Apply(Msg(2, 100, ""));
            Assert.AreEqual("영희 선수, 스스로 장외", lines[0]);
        }

        [Test]
        public void 떨어진_자리를_모르면_피니시에_자리가_없다()
        {
            var state = new KnockbackClientState();
            var registry = new GameFramework.World.EntityRegistry();
            registry.Add(Body("b", "영희", false));
            var broadcaster = new KnockbackKoBroadcaster(state, registry, null);
            var finishes = new List<Vector3?>();
            broadcaster.Finished += p => finishes.Add(p);

            state.Apply(Msg(1, -1, ""));
            state.Apply(Msg(2, 100, "a"));
            Assert.AreEqual(1, finishes.Count);
            Assert.IsNull(finishes[0], "자리를 모르면 줌을 걸지 않는다");
        }
    }
}
