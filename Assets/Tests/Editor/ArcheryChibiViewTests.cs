using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryChibiViewTests
    {
        private const string ChibiPath = "Assets/Characters/Chibi/Chibi.prefab";
        private const string FacePath = "Assets/Characters/Chibi/Materials/ChibiFace.mat";

        [Test]
        public void 얼굴_재질도_원격_그룹에_있다()
        {
            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
            var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(FacePath));
            Assert.IsNotNull(entry);
            Assert.AreEqual(FacePath, entry.address);
            Assert.AreEqual("Character", entry.parentGroup.Name);
        }
    }
}
