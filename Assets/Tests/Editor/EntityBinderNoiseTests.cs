using NUnit.Framework;

namespace LOP.Tests
{
    public class EntityBinderNoiseTests
    {
        [TestCase("", "", true, true)]
        [TestCase("", "e3", true, false)]
        [TestCase("e1", "", true, false)]
        [TestCase("", "", false, false)]
        [TestCase(null, null, true, true)]
        public void 내_몸을_못_알아볼_때만_에러를_낸다(string userEntityId, string formerEntityId, bool isCharacter, bool expected)
        {
            Assert.AreEqual(expected, EntityBinder.ShouldReportMissingUserEntity(userEntityId, formerEntityId, isCharacter));
        }
    }
}
