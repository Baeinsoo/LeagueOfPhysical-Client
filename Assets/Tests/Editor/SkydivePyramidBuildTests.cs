using System.Linq;
using LOP.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class SkydivePyramidBuildTests
    {
        [Test]
        public void 굽기_검사를_통과한다()
        {
            Assert.IsNull(SkydivePyramidBuilder.Verify());
        }

        [Test]
        public void 체크포인트는_구간마다_다섯이고_맨_위가_스폰이다()
        {
            CollectionAssert.AreEquivalent(new[] { 3600f, 3200f, 2000f, 1300f, 450f }, SkydivePyramidLayout.RespawnPoints.Keys.ToArray());
            Assert.AreEqual(SkydivePyramidLayout.SpawnY, SkydivePyramidLayout.RespawnPoints.Keys.Max());
        }

        [Test]
        public void 테라스는_더미_위_4층을_600m_올린_것이다()
        {
            CollectionAssert.AreEqual(new[] { 3200f, 2800f, 2400f, 2000f }, SkydivePyramidLayout.Terraces.Select(s => s.Y).ToArray());
            for (int i = 0; i < 4; i++)
            {
                var dummy = SkydiveCourseBuilder.Shelves[i];
                var t = SkydivePyramidLayout.Terraces[i];
                Assert.AreEqual(dummy.Y + SkydivePyramidLayout.Shift, t.Y);
                Assert.AreEqual(dummy.Holes.Length, t.Holes.Length);
            }
        }

        [Test]
        public void 앞마당은_놀이_폭을_다_덮고_구멍이_없다()
        {
            var porch = SkydivePyramidLayout.Porch;
            Assert.LessOrEqual(porch.XMin, -100f);
            Assert.GreaterOrEqual(porch.XMax, 100f);
            Assert.LessOrEqual(porch.ZMin, -100f);
            //  앞마당 북쪽 끝이 갱도 남쪽 벽(z 35)까지 와야 틈으로 빠지지 않는다.
            Assert.GreaterOrEqual(porch.ZMax, 35f);

            //  갱도(±35) 밖, z 35..100도 덮여야 한다 — 지붕 옆으로 떨어져 갱도를 건너뛰는 길을 막는다.
            for (float x = -99f; x <= 99f; x += 2f)
            {
                for (float z = -99f; z <= 99f; z += 2f)
                {
                    bool inShaft = Mathf.Abs(x) < SkydivePyramidLayout.ShaftXHalf + SkydivePyramidLayout.ShaftWall && z > porch.ZMax;
                    if (inShaft) { continue; }
                    bool covered = (x >= porch.XMin && x <= porch.XMax && z >= porch.ZMin && z <= porch.ZMax)
                        || SkydivePyramidLayout.PorchSides.Any(p => x >= p.XMin && x <= p.XMax && z >= p.ZMin && z <= p.ZMax);
                    Assert.IsTrue(covered, $"({x},{z})에 1300 바닥이 없다 — 갱도를 건너뛴다");
                }
            }
        }

        [Test]
        public void 맵_표와_원격_에셋에_등록돼_있다()
        {
            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
            var guid = UnityEditor.AssetDatabase.AssetPathToGUID(SkydivePyramidBuilder.ScenePath);
            var entry = settings.FindAssetEntry(guid);
            Assert.IsNotNull(entry, "Scene 그룹에 없으면 원격 에셋이 안 올라가 방에서 맵을 못 연다");
            Assert.AreEqual(SkydivePyramidBuilder.ScenePath, entry.address);
            Assert.AreEqual("Scene", entry.parentGroup.Name);

            //  LoadAsync는 EditMode에서 기다리기 위험하다 — 패키지의 .bytes를 직접 읽는다(SkydiveLandingMasterDataConsistencyTests와 같은 방식).
            string path = System.IO.Path.GetFullPath(
                "Packages/com.baegames.lop.masterdata.client/Runtime.Generated/StreamingAssets/MasterData/tbmap.bytes");
            var table = new LOP.MasterData.TbMap(new Luban.ByteBuf(System.IO.File.ReadAllBytes(path)));
            var map = table.GetOrDefault(9);
            Assert.IsNotNull(map, "TbMap에 9번 맵이 없다");
            Assert.AreEqual(SkydivePyramidBuilder.ScenePath, map.ScenePath);
        }

        [Test]
        public void 피라미드_부활_지점은_모두_판_위_구멍_밖()
        {
            Assert.IsNull(SkydivePyramidBuilder.FindBadRespawn());
        }
    }
}
