using NUnit.Framework;

namespace LOP.Tests
{
    public class ChibiRegionsTests
    {
        [TestCase("Head", ChibiRegion.Skin)]
        [TestCase("Neck", ChibiRegion.Skin)]
        [TestCase("Left_Hand", ChibiRegion.Skin)]
        [TestCase("RightHandIndex3", ChibiRegion.Skin)]
        [TestCase("Spine1", ChibiRegion.Top)]
        [TestCase("Spine2", ChibiRegion.Top)]
        [TestCase("RightShoulder", ChibiRegion.Top)]
        [TestCase("Left_Arm", ChibiRegion.Top)]
        [TestCase("RightForeArm", ChibiRegion.Sleeve)]
        [TestCase("Left_ForeArm", ChibiRegion.Sleeve)]
        [TestCase("Hips", ChibiRegion.Bottom)]
        [TestCase("Left_UpLeg", ChibiRegion.Bottom)]
        [TestCase("RightLeg", ChibiRegion.Bottom)]
        [TestCase("Left_Foot", ChibiRegion.Shoe)]
        [TestCase("RightFoot", ChibiRegion.Shoe)]
        public void 뼈_이름으로_옷_영역을_정한다(string bone, ChibiRegion expected)
        {
            Assert.AreEqual(expected, ChibiRegions.RegionOf(bone));
        }

        [Test]
        public void 모르는_뼈는_피부()
        {
            Assert.AreEqual(ChibiRegion.Skin, ChibiRegions.RegionOf("Tail"));
        }

        [Test]
        public void 영역은_정점_색_빨강_채널에_6단계로_담긴다()
        {
            Assert.AreEqual(0, ChibiRegions.Encode(ChibiRegion.Skin));
            Assert.AreEqual(255, ChibiRegions.Encode(ChibiRegion.Hair));
            foreach (ChibiRegion r in System.Enum.GetValues(typeof(ChibiRegion)))
            {
                Assert.AreEqual(r, ChibiRegions.Decode(ChibiRegions.Encode(r)));
            }
        }

        //  머리 정점(모델 단위, 키 0.49): 정수리 쪽과 뒤통수는 머리카락, 얼굴 쪽은 피부.
        [TestCase(0.46f, 0.05f, ChibiRegion.Hair)]
        [TestCase(0.34f, -0.08f, ChibiRegion.Hair)]
        [TestCase(0.31f, 0.13f, ChibiRegion.Skin)]
        [TestCase(0.25f, 0.10f, ChibiRegion.Skin)]
        public void 머리는_정수리와_뒤통수만_머리카락(float y, float z, ChibiRegion expected)
        {
            Assert.AreEqual(expected, ChibiRegions.HeadRegion(y, z));
        }
    }
}
