using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// EntitySnap.proto의 스턴 필드가 AutoMapper convention(ForMember 없이 이름만 맞춰 매핑)으로
    /// 실제로 옮겨지는지 확인한다. ProtoMapperProfile은 이 필드들을 명시적으로 다루지 않으므로,
    /// convention이 깨지면 클라가 항상 0을 보게 되는데 그걸 잡아낼 테스트가 이거뿐이다.
    /// </summary>
    public class StunFieldMapperTests
    {
        [Test]
        public void 멈춤_종료틱이_AutoMapper_컨벤션으로_옮겨진다()
        {
            var proto = new global::EntitySnap { StunEndTick = 1234 };

            EntitySnap mapped = MapperConfig.mapper.Map<EntitySnap>(proto);

            Assert.AreEqual(1234, mapped.stunEndTick);
        }

        [Test]
        public void 무적_종료틱이_AutoMapper_컨벤션으로_옮겨진다()
        {
            var proto = new global::EntitySnap { InvulnEndTick = 5678 };

            EntitySnap mapped = MapperConfig.mapper.Map<EntitySnap>(proto);

            Assert.AreEqual(5678, mapped.invulnEndTick);
        }
    }
}
