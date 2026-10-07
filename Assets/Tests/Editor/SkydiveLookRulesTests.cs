using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class SkydiveLookRulesTests
    {
        [Test]
        public void 다이브는_집중_맞으면_놀람_그밖_보통()
        {
            Assert.AreEqual(ChibiExpression.Focus, SkydiveLookRules.ChibiFace(SkydiveMotionState.Skydiving, 0.8f, false, false));
            Assert.AreEqual(ChibiExpression.Normal, SkydiveLookRules.ChibiFace(SkydiveMotionState.Skydiving, 0.2f, false, false));
            Assert.AreEqual(ChibiExpression.Normal, SkydiveLookRules.ChibiFace(SkydiveMotionState.Skydiving, 0.8f, true, false));   // 패러세일은 다이브 아님
            Assert.AreEqual(ChibiExpression.Surprise, SkydiveLookRules.ChibiFace(SkydiveMotionState.Skydiving, 0.8f, false, true));
            Assert.AreEqual(ChibiExpression.Normal, SkydiveLookRules.ChibiFace(SkydiveMotionState.Walking, 1f, false, false));
        }

        [Test]
        public void 위로_크게_튀면_순간이동()
        {
            Assert.IsTrue(SkydiveLookRules.Teleported(1000f, 1400f));
            Assert.IsFalse(SkydiveLookRules.Teleported(1000f, 1005f));
            Assert.IsFalse(SkydiveLookRules.Teleported(1000f, 900f));
        }

        [Test]
        public void 속도선은_30에서_90까지()
        {
            Assert.AreEqual(0f, SkydiveLookRules.SpeedLines(20f));
            Assert.AreEqual(0.5f, SkydiveLookRules.SpeedLines(60f), 1e-4f);
            Assert.AreEqual(1f, SkydiveLookRules.SpeedLines(120f));
        }
    }
}
