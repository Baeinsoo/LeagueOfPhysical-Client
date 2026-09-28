using LOP.LookDev;
using NUnit.Framework;

namespace LOP.Tests
{
    public class LookDevRegionsTests
    {
        [TestCase("Head", LookDevRegion.Skin)]
        [TestCase("Neck", LookDevRegion.Skin)]
        [TestCase("Left_Hand", LookDevRegion.Skin)]
        [TestCase("RightHandIndex3", LookDevRegion.Skin)]
        [TestCase("Spine1", LookDevRegion.Top)]
        [TestCase("Spine2", LookDevRegion.Top)]
        [TestCase("RightShoulder", LookDevRegion.Top)]
        [TestCase("Left_Arm", LookDevRegion.Top)]
        [TestCase("RightForeArm", LookDevRegion.Sleeve)]
        [TestCase("Left_ForeArm", LookDevRegion.Sleeve)]
        [TestCase("Hips", LookDevRegion.Bottom)]
        [TestCase("Left_UpLeg", LookDevRegion.Bottom)]
        [TestCase("RightLeg", LookDevRegion.Bottom)]
        [TestCase("Left_Foot", LookDevRegion.Shoe)]
        [TestCase("RightFoot", LookDevRegion.Shoe)]
        public void 뼈_이름으로_옷_영역을_정한다(string bone, LookDevRegion expected)
        {
            Assert.AreEqual(expected, LookDevRegions.RegionOf(bone));
        }

        [Test]
        public void 모르는_뼈는_피부()
        {
            Assert.AreEqual(LookDevRegion.Skin, LookDevRegions.RegionOf("Tail"));
        }

        [Test]
        public void 영역은_정점_색_빨강_채널에_6단계로_담긴다()
        {
            Assert.AreEqual(0, LookDevRegions.Encode(LookDevRegion.Skin));
            Assert.AreEqual(255, LookDevRegions.Encode(LookDevRegion.Hair));
            foreach (LookDevRegion r in System.Enum.GetValues(typeof(LookDevRegion)))
            {
                Assert.AreEqual(r, LookDevRegions.Decode(LookDevRegions.Encode(r)));
            }
        }

        //  머리 정점(모델 단위, 키 0.49): 정수리 쪽과 뒤통수는 머리카락, 얼굴 쪽은 피부.
        [TestCase(0.46f, 0.05f, LookDevRegion.Hair)]
        [TestCase(0.34f, -0.08f, LookDevRegion.Hair)]
        [TestCase(0.31f, 0.13f, LookDevRegion.Skin)]
        [TestCase(0.25f, 0.10f, LookDevRegion.Skin)]
        public void 머리는_정수리와_뒤통수만_머리카락(float y, float z, LookDevRegion expected)
        {
            Assert.AreEqual(expected, LookDevRegions.HeadRegion(y, z));
        }
    }
}
