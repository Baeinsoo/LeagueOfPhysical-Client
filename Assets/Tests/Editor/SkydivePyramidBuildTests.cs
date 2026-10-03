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
            for (int i = 1; i < 4; i++)
            {
                var dummy = SkydiveCourseBuilder.Shelves[i];
                var t = SkydivePyramidLayout.Terraces[i];
                Assert.AreEqual(dummy.Y + SkydivePyramidLayout.Shift, t.Y);
                Assert.AreEqual(dummy.Holes.Length, t.Holes.Length);
            }
        }

        [Test]
        public void 첫_테라스는_문_없는_큰_구멍_하나_제단을_덮는다()
        {
            //  스펙 §2.1 익히기 — 제단 어디서 뛰어내려도 구멍으로 떨어진다. 문·갈림길은 2800부터.
            var first = SkydivePyramidLayout.Terraces[0];
            Assert.AreEqual(1, first.Holes.Length);
            Assert.IsFalse(first.Holes[0].HasDoor);
            Assert.GreaterOrEqual(first.Holes[0].Half, SkydivePyramidLayout.AltarHalf);
            Assert.IsFalse(SkydivePyramidLayout.TerraceDoors.Any(d => Mathf.Approximately(d.Center.y, 3200f)));
            Assert.IsTrue(SkydivePyramidLayout.Lasers.Any(l => l.Pivot.y > 3200f && l.Pivot.y < SkydivePyramidLayout.SpawnY), "3400 느린 레이저");
        }

        [Test]
        public void 앞마당_위에서는_놀이_폭_밖으로_못_나간다()
        {
            //  제단에서 대자로 400m 떨어지면 옆으로 77m 간다 — 막지 않으면 테라스를 다 건너뛰고 바닥에 닿는다.
            //  동·서·남은 보이는 경계벽, 북은 피라미드 몸체·섬 바위(충돌체)가 막는다.
            float top = SkydivePyramidLayout.SpawnY + 50f, bottom = SkydivePyramidLayout.PorchY;
            foreach (var probe in new[] { new Vector3(-101f, 0f, 0f), new Vector3(101f, 0f, 0f), new Vector3(0f, 0f, -101f), new Vector3(0f, 0f, 101f) })
            {
                for (float y = bottom + 1f; y < top; y += 25f)
                {
                    var p = new Vector3(probe.x, y, probe.z);
                    Assert.IsTrue(SkydivePyramidLayout.Solids.Any(b => b.Contains(p)), $"{p}에 막는 것이 없다 — 코스를 건너뛴다");
                }
            }
        }

        [Test]
        public void 경계벽은_그림자를_드리우지_않는다()
        {
            //  해가 남쪽에서 비춘다 — 2300m짜리 남쪽 벽이 그림자를 드리우면 코스 전체가 어두워진다(플레이 관측).
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(SkydivePyramidBuilder.ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                var walls = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true)).Where(r => r.name.StartsWith("BoundaryWall")).ToArray();
                Assert.AreEqual(SkydivePyramidLayout.BoundaryWalls.Length, walls.Length);
                foreach (var w in walls)
                {
                    Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, w.shadowCastingMode, w.name);
                }
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void 꾸밈은_충돌체가_없고_메시는_에셋으로_남는다()
        {
            //  꾸밈(구름·덩굴·석상 눈·나무·울타리 빔)은 판정과 무관하다 — 충돌체가 있으면 보이지 않는 곳에서 몸이 걸린다.
            //  코드로 만든 메시는 에셋으로 저장하지 않으면 씬을 다시 열 때 사라진다(원격 에셋·서버에서 빈 메시).
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(SkydivePyramidBuilder.ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                var dressing = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "Dressing");
                Assert.IsNotNull(dressing, "꾸밈이 없다");
                Assert.AreEqual(0, dressing.GetComponentsInChildren<Collider>(true).Length);
                var filters = dressing.GetComponentsInChildren<MeshFilter>(true);
                Assert.Greater(filters.Length, 50);
                foreach (var f in filters)
                {
                    Assert.IsNotNull(f.sharedMesh, f.name);
                    Assert.IsTrue(UnityEditor.EditorUtility.IsPersistent(f.sharedMesh), f.name + " 메시가 저장되지 않았다");
                }
                //  판정 상자는 우리 툰 재질이다(회색 블록 아웃 끝).
                var terrace = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true)).First(r => r.name.StartsWith("Terrace_"));
                Assert.AreEqual("LOP/Toon", terrace.sharedMaterial.shader.name);
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true); }
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
