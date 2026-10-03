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
        public void 벽_면엔_돌_쌓은_결이_있다()
        {
            //  400m 민짜 면은 가까이서 결이 없어 "거대한 벽"으로만 보였다 — 옆면 줄눈으로 크기감을 준다.
            Assert.AreEqual(0f, LOPToonMaterials.Create(Color.white).GetFloat("_SideGrid"), "다른 모드엔 영향 없음(기본 꺼짐)");
            Assert.Greater(SkydivePyramidDressing.StoneDark.GetFloat("_SideGrid"), 0f);
            Assert.Greater(SkydivePyramidDressing.Stone.GetFloat("_SideGrid"), 0f);
        }

        [Test]
        public void 꼭대기는_내려다보는_자리다()
        {
            //  제단 뒤로 피라미드 윗단이 솟아 있으면 꼭대기에서 벽을 올려다본다 — 윗단 윗면은 제단 높이까지만.
            var top = SkydivePyramidLayout.PyramidBody[0];
            Assert.LessOrEqual(top.max.y, SkydivePyramidLayout.SpawnY + 1.5f);
        }

        [Test]
        public void 테라스_조형물은_구멍_부활_레이저를_피한다()
        {
            var pieces = SkydivePyramidLayout.SetPieces;
            foreach (var t in SkydivePyramidLayout.Terraces)
            {
                Assert.IsTrue(pieces.Any(p => Mathf.Approximately(p.Y, t.Y)), $"{t.Y:0} 테라스에 조형물이 없다");
            }
            foreach (var p in pieces)
            {
                var t = SkydivePyramidLayout.Terraces.First(x => Mathf.Approximately(x.Y, p.Y));
                Assert.LessOrEqual(Mathf.Abs(p.X) + p.Half, 100f, "놀이 판 밖");
                foreach (var h in t.Holes)
                {
                    Assert.IsTrue(Mathf.Abs(p.X - h.X) > p.Half + h.Half + 6f || Mathf.Abs(p.Z - h.Z) > p.Half + h.Half + 6f, $"{p.Y:0} 조형물이 구멍({h.X:0},{h.Z:0})을 막는다");
                }
                if (SkydivePyramidLayout.RespawnPoints.TryGetValue(p.Y, out var r))
                {
                    Assert.IsTrue(Mathf.Abs(p.X - r.x) > p.Half + 8f || Mathf.Abs(p.Z - r.z) > p.Half + 8f, $"{p.Y:0} 조형물이 부활 지점 위");
                }
                foreach (var l in SkydivePyramidLayout.Lasers)
                {
                    bool sameBand = l.Pivot.y > p.Y - 5f && l.Pivot.y < p.Y + p.Height + 5f;
                    float d = new Vector2(l.Pivot.x - p.X, l.Pivot.z - p.Z).magnitude;
                    Assert.IsFalse(sameBand && d < l.Length + p.Half * 1.42f, $"{p.Y:0} 조형물이 레이저 {l.Name}의 원 안");
                }
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
