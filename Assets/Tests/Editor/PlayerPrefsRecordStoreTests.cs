using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>개인 기록을 기기(PlayerPrefs)에 double 그대로 남긴다. 진짜 PlayerPrefs를 쓰므로 시험 전용 키로 하고 지운다.</summary>
    public class PlayerPrefsRecordStoreTests
    {
        private const string Key = "lop.record.test.990001.metric";
        private readonly PlayerPrefsRecordStore store = new PlayerPrefsRecordStore();

        [SetUp]
        public void SetUp() => PlayerPrefs.DeleteKey(Key);

        [TearDown]
        public void TearDown() => PlayerPrefs.DeleteKey(Key);

        [Test]
        public void 없으면_없다고_답한다()
        {
            Assert.IsFalse(store.TryGetBest(Key, out _));
            Assert.IsFalse(store.TryGetBest(null, out _), "키가 없으면(종목·맵 모름) 묻지 않는다");
        }

        [Test]
        public void double을_그대로_돌려준다()
        {
            //  float로 남기면 52.3이 52.29999923706055가 돼 같은 기록이 경신·"+0.00초" 사이를 오간다.
            store.SaveBest(Key, 52.3d);

            Assert.IsTrue(store.TryGetBest(Key, out double best));
            Assert.AreEqual(52.3d, best);
        }

        [Test]
        public void 다른_형식이_들어_있으면_없는_것으로()
        {
            PlayerPrefs.SetInt(Key, 5);

            Assert.IsFalse(store.TryGetBest(Key, out _));
        }
    }
}
