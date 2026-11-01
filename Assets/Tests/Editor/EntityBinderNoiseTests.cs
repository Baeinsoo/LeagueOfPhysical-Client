using NUnit.Framework;

namespace LOP.Tests
{
    public class EntityBinderNoiseTests
    {
        [TestCase("", "", "e5", true, true)]
        [TestCase("", "e3", "e5", true, false)]
        [TestCase("e1", "", "e5", true, false)]
        [TestCase("", "", "e5", false, false)]
        [TestCase(null, null, "e5", true, true)]
        //  서버는 몸이 살아 있을 때도 옛 몸 id를 채운다 — 그 몸이 지금 묶이는데 내 몸 id가 비었으면 진짜 문제다.
        [TestCase("", "e3", "e3", true, true)]
        public void 내_몸을_못_알아볼_때만_에러를_낸다(string userEntityId, string formerEntityId, string boundEntityId,
                                                 bool isCharacter, bool expected)
        {
            Assert.AreEqual(expected,
                EntityBinder.ShouldReportMissingUserEntity(userEntityId, formerEntityId, boundEntityId, isCharacter));
        }
    }
}
