using Google.Protobuf;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 연속 날갯짓 수(spec 2026-10-27 §4)가 와이어를 건너온다 — proto 필드(25)가 생성물에 있는지, AutoMapper 이름 규칙으로 클라 스냅에 옮겨지는지.
    /// 둘 중 하나라도 끊기면 클라는 늘 0을 보고 스냅마다 되돌린다.
    /// </summary>
    public class FlapComboWireTests
    {
        [Test]
        public void 연속_수가_proto를_왕복한다()
        {
            var proto = new global::EntitySnap { FlapCombo = 37 };

            var back = global::EntitySnap.Parser.ParseFrom(proto.ToByteArray());

            Assert.AreEqual(37, back.FlapCombo);
        }

        [Test]
        public void 연속_수가_AutoMapper_컨벤션으로_옮겨진다()
        {
            EntitySnap mapped = MapperConfig.mapper.Map<EntitySnap>(new global::EntitySnap { FlapCombo = 12 });

            Assert.AreEqual(12, mapped.flapCombo);
        }
    }
}
