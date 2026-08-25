using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// EntitySnap.proto의 stunned 필드가 AutoMapper convention(ForMember 없이 이름만 맞춰 매핑)으로
    /// 실제로 옮겨지는지 확인한다. ProtoMapperProfile은 이 필드를 명시적으로 다루지 않으므로,
    /// convention이 깨지면 클라가 항상 false를 보게 되는데 그걸 잡아낼 테스트가 이거 하나뿐이다.
    /// </summary>
    public class StunFieldMapperTests
    {
        [Test]
        public void 스턴_필드가_AutoMapper_컨벤션으로_옮겨진다()
        {
            var proto = new global::EntitySnap { Stunned = true };

            EntitySnap mapped = MapperConfig.mapper.Map<EntitySnap>(proto);

            Assert.IsTrue(mapped.stunned);
        }
    }
}
