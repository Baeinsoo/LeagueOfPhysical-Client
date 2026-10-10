using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.Tests
{
    //  서버가 생성 데이터 프로토에 실어 보낸 룩·이름·레벨을 클라가 PlayerLook으로 바꾸는 규칙.
    //  룩·이름·레벨이 전부 비어 있으면(구버전 서버거나 몬스터·심판 같은 사람 아닌 몸) null을
    //  돌려서 이름표가 안 달리게 한다 — PlayerLookFromProto.Convert 쪽 규칙.
    public class CreationDataLookClientTests
    {
        static global::CharacterCreationData Proto(
            IDictionary<string, string> look = null, string displayName = "", int accountLevel = 0)
        {
            var proto = new global::CharacterCreationData();
            if (look != null)
            {
                foreach (var (slot, itemCode) in look)
                {
                    proto.Look[slot] = itemCode;
                }
            }
            proto.DisplayName = displayName;
            proto.AccountLevel = accountLevel;
            return proto;
        }

        [Test]
        public void 룩_이름_레벨이_있으면_PlayerLook으로_변환된다()
        {
            var proto = Proto(
                look: new Dictionary<string, string> { ["hat"] = "hat_cube_red", ["top"] = "top_tint_blue" },
                displayName: "Kim",
                accountLevel: 7);

            var look = PlayerLookFromProto.Convert(proto);

            Assert.IsNotNull(look);
            Assert.AreEqual("hat_cube_red", look.SlotOrNull("hat"));
            Assert.AreEqual("top_tint_blue", look.SlotOrNull("top"));
            Assert.AreEqual("Kim", look.DisplayName);
            Assert.AreEqual(7, look.AccountLevel);
        }

        //  map·이름·레벨이 전부 비면 몬스터·심판 같은 사람 아닌 몸(또는 구버전 서버) — null로 와야
        //  worldEntity.Add(look)이 안 불려서 이름표가 안 달린다.
        [Test]
        public void 룩_이름_레벨이_전부_비면_null()
        {
            var proto = Proto(look: null, displayName: "", accountLevel: 0);

            var look = PlayerLookFromProto.Convert(proto);

            Assert.IsNull(look);
        }

        //  기본 룩(슬롯 하나도 안 꾸민) 플레이어도 이름·레벨만 있으면 이름표를 받아야 한다.
        [Test]
        public void 슬롯_없이_이름만_있어도_non_null이고_슬롯은_빈다()
        {
            var proto = Proto(look: null, displayName: "Kim", accountLevel: 0);

            var look = PlayerLookFromProto.Convert(proto);

            Assert.IsNotNull(look);
            Assert.AreEqual("Kim", look.DisplayName);
            Assert.AreEqual(0, look.Slots.Count);
        }

        class FakeGameDataStore : IGameDataStore
        {
            public GameInfo gameInfo { get; set; }
            public string userEntityId { get; set; }
            public void Clear() { }
        }

        [Test]
        public void 룩이_있으면_판치기_엔티티에_PlayerLook이_붙는다()
        {
            var registry = new GameFramework.World.EntityRegistry();
            var creator = new PanchigiPlayerCreator(new FakeGameDataStore(), new PlayerContext(), registry);
            var look = new PlayerLook(new Dictionary<string, string> { ["hat"] = "hat_001" }, "Kim", 3);

            creator.Create(new CharacterCreationData { entityId = "e1", visualId = "", look = look });

            var entity = registry.Get("e1");
            Assert.IsNotNull(entity);
            var attached = entity.Get<PlayerLook>();
            Assert.IsNotNull(attached);
            Assert.AreEqual("Kim", attached.DisplayName);
        }

        [Test]
        public void 룩이_없으면_판치기_엔티티에_PlayerLook이_안_붙는다()
        {
            var registry = new GameFramework.World.EntityRegistry();
            var creator = new PanchigiPlayerCreator(new FakeGameDataStore(), new PlayerContext(), registry);

            creator.Create(new CharacterCreationData { entityId = "e2", visualId = "", look = null });

            var entity = registry.Get("e2");
            Assert.IsNotNull(entity);
            Assert.IsNull(entity.Get<PlayerLook>());
        }
    }
}
