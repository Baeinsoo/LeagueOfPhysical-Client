using System.Linq;
using LOP.EditorTools;
using NUnit.Framework;
using UnityEngine;
using L = LOP.EditorTools.SkydivePyramidLayout;

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
            CollectionAssert.AreEquivalent(new[] { 3600f, 3200f, 2000f, 1300f, 450f }, L.RespawnPoints.Keys.ToArray());
            Assert.AreEqual(L.SpawnY, L.RespawnPoints.Keys.Max());
        }

        [Test]
        public void 나선은_층마다_40도_빠른_구멍은_다이브_안전한_구멍은_대자_거리()
        {
            for (int k = 2; k <= 4; k++)
            {
                Assert.AreEqual(40f, Mathf.DeltaAngle(L.Theta(k - 1), L.Theta(k)), 0.001f);
                var fastStep = (L.OnCircle(L.FastRadius, k) - L.OnCircle(L.FastRadius, k - 1)).magnitude;
                var safeStep = (L.OnCircle(L.SafeRadius, k) - L.OnCircle(L.SafeRadius, k - 1)).magnitude;
                Assert.AreEqual(41f, fastStep, 1f, "빠른 구멍 사이 — 다이브 도달(33) + 반폭(12) 안");
                Assert.AreEqual(75f, safeStep, 1f, "안전한 구멍 사이 — 대자(77)로만");
            }
        }

        [Test]
        public void 위층_구멍_어디서도_안전한_구멍에_다이브로_못_닿는다()
        {
            //  길 검사는 구멍 가운데에서 잰다 — 넓은 구멍(익히기 60m)의 가장자리에서 다이브(33m)로 안전한 구멍에 닿으면 갈림길이 무너진다(리뷰).
            const float diveReach = 34f;
            for (int k = 1; k < L.Terraces.Length; k++)
            {
                foreach (var from in L.Terraces[k - 1].Holes)
                {
                    foreach (var to in L.Terraces[k].Holes.Where(h => h.HasDoor == false))
                    {
                        float gapX = Mathf.Max(0f, Mathf.Abs(from.X - to.X) - from.Half - to.Half);
                        float gapZ = Mathf.Max(0f, Mathf.Abs(from.Z - to.Z) - from.Half - to.Half);
                        Assert.Greater(new Vector2(gapX, gapZ).magnitude, diveReach,
                            $"{L.Terraces[k - 1].Y:0} 구멍({from.X:0},{from.Z:0}) 가장자리에서 {L.Terraces[k].Y:0} 안전한 구멍({to.X:0},{to.Z:0})까지 다이브로 닿는다");
                    }
                }
            }
        }

        [Test]
        public void 꾸밈이_구멍_위를_가로지르거나_갱도_출구를_가리지_않는다()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(SkydivePyramidBuilder.ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                var renderers = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
                //  판 둘레 띠는 그 판 위에만 — 옛 원점 기준 띠가 안전한 구멍 위를 다리처럼 가로질렀다.
                foreach (var edge in renderers.Where(r => r.name.StartsWith("TerraceEdge_")))
                {
                    var c = edge.bounds.center;
                    bool onSomePlate = Enumerable.Range(1, 4).Any(k => Mathf.Abs(c.x - L.PlateCenter(k).x) <= 101f && Mathf.Abs(c.z - L.PlateCenter(k).y) <= 101f
                                                                     && Mathf.Abs(c.y - L.TerraceYs[k - 1]) < 5f);
                    Assert.IsTrue(onSomePlate, $"{edge.name}가 판 밖에 떠 있다({c})");
                }
                //  갱도 출구 구멍의 낙하 기둥을 바위가 덮으면 캐릭터가 바위 속으로 사라진다.
                var exit = L.ShaftLedges.Last().Holes[0];
                var cone = renderers.First(r => r.name == "IslandCone").bounds;
                bool overlap = cone.min.x < exit.X + exit.Half && cone.max.x > exit.X - exit.Half && cone.min.z < exit.Z + exit.Half && cone.max.z > exit.Z - exit.Half;
                Assert.IsFalse(overlap, "섬 바위가 갱도 출구 낙하 기둥을 덮는다");
                //  체크포인트 표식은 고도마다 하나, 3600은 제단 위.
                var markers = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LOP.CheckpointMarker>(true)).ToArray();
                Assert.AreEqual(L.RespawnPoints.Count, markers.Length, "같은 고도에 표식이 둘이면 나중 것이 이긴다 — 순서가 바뀌면 공중에서 부활");
                var top = markers.Single(m => Mathf.Approximately(m.transform.position.y, L.SpawnY));
                Assert.AreEqual(L.AltarXZ.x, top.transform.position.x, 0.01f);
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void 첫_테라스는_문_없는_큰_구멍_하나가_제단을_덮는다()
        {
            var first = L.Terraces[0];
            Assert.AreEqual(1, first.Holes.Length);
            Assert.IsFalse(first.Holes[0].HasDoor);
            Assert.GreaterOrEqual(first.Holes[0].Half, L.AltarHalf);
            Assert.AreEqual(L.AltarXZ.x, first.Holes[0].X, 0.01f);
            Assert.AreEqual(L.AltarXZ.y, first.Holes[0].Z, 0.01f);
            Assert.IsTrue(L.Lasers.Any(l => l.Pivot.y > 3200f && l.Pivot.y < L.SpawnY), "3400 느린 레이저");
        }

        [Test]
        public void 층마다_경계가_다음_테라스_둘레를_막는다()
        {
            //  띠(판 k 높이 ~ 위 판 높이)의 가운데 높이에서 판 k 둘레 바로 바깥 점들이 모두 벽 안이어야 한다 — 옆으로 흘러 한 층을 건너뛰지 못하게.
            var bands = new (int plate, float low, float high)[]
            {
                (1, 3200f, L.SpawnY), (2, 2800f, 3200f), (3, 2400f, 2800f), (4, 2000f, 2400f), (5, L.PorchY, 2000f),
            };
            var walls = L.BandWalls;
            foreach (var (plate, low, high) in bands)
            {
                var c = L.PlateCenter(plate);
                float y = (low + high) * 0.5f;
                foreach (var d in new[] { new Vector2(102f, 0f), new Vector2(-102f, 0f), new Vector2(0f, 102f), new Vector2(0f, -102f),
                                          new Vector2(102f, 60f), new Vector2(-102f, -60f), new Vector2(60f, 102f), new Vector2(-60f, -102f) })
                {
                    var p = new Vector3(c.x + d.x, y, c.y + d.y);
                    Assert.IsTrue(walls.Any(w => w.Contains(p)), $"판 {plate} 띠의 {p}에 벽이 없다 — 한 층을 건너뛴다");
                }
            }
        }

        [Test]
        public void 몸체는_다음_층_낙하_칸을_침범하지_않는다()
        {
            foreach (var (rect, low, high) in L.BodyPieces)
            {
                int k = System.Array.FindIndex(L.TerraceYs, y => Mathf.Abs(y - 1.5f - high) < 0.01f) + 1;
                Assert.Greater(k, 0, "몸체 조각의 판을 못 찾았다");
                var next = L.PlateCenter(k + 1);
                //  경계에 딱 붙은 조각(104.0)이 부동소수로 103.99가 되는 것은 침범이 아니다 — 0.05 여유.
                bool overlapX = rect.XMin < next.x + 103.95f && rect.XMax > next.x - 103.95f;
                bool overlapZ = rect.ZMin < next.y + 103.95f && rect.ZMax > next.y - 103.95f;
                Assert.IsFalse(overlapX && overlapZ, $"판 {k} 몸체 {rect.Name}가 판 {k + 1}의 낙하 칸(띠 벽 안)에 들어온다");
            }
        }

        [Test]
        public void 앞마당은_2000_구멍_아래고_갱도_위가_아니다()
        {
            var porchParts = new[] { L.Porch }.Concat(L.PorchSides).ToArray();
            var shaft = L.ShaftFloor;
            foreach (var h in L.Terraces[3].Holes)
            {
                Assert.IsTrue(porchParts.Any(p => h.X >= p.XMin && h.X <= p.XMax && h.Z >= p.ZMin && h.Z <= p.ZMax), $"2000 구멍({h.X:0},{h.Z:0}) 아래 앞마당이 없다");
                bool overShaft = h.X + h.Half > shaft.XMin - L.ShaftWall && h.X - h.Half < shaft.XMax + L.ShaftWall
                              && h.Z + h.Half > shaft.ZMin - L.ShaftWall && h.Z - h.Half < shaft.ZMax + L.ShaftWall;
                Assert.IsFalse(overShaft, $"2000 구멍({h.X:0},{h.Z:0})이 갱도 위다 — 앞마당을 건너뛴다");
            }
        }

        [Test]
        public void 앞마당은_놀이_폭을_다_덮고_구멍이_없다()
        {
            var t = L.PorchOffset;
            var porchParts = new[] { L.Porch }.Concat(L.PorchSides).ToArray();
            var shaft = L.ShaftFloor;
            for (float x = -99f; x <= 99f; x += 2f)
            {
                for (float z = -99f; z <= 99f; z += 2f)
                {
                    float wx = t.x + x, wz = t.z + z;
                    bool inShaft = wx > shaft.XMin - L.ShaftWall && wx < shaft.XMax + L.ShaftWall && wz > shaft.ZMin - L.ShaftWall;
                    if (inShaft) { continue; }
                    Assert.IsTrue(porchParts.Any(p => wx >= p.XMin && wx <= p.XMax && wz >= p.ZMin && wz <= p.ZMax), $"({wx:0},{wz:0})에 1300 바닥이 없다 — 갱도를 건너뛴다");
                }
            }
        }

        [Test]
        public void 테라스_조형물은_구멍_부활_레이저를_피한다()
        {
            var pieces = L.SetPieces;
            for (int k = 1; k <= 4; k++)
            {
                var t = L.Terraces[k - 1];
                var c = L.PlateCenter(k);
                var mine = pieces.Where(p => Mathf.Approximately(p.Y, t.Y)).ToArray();
                Assert.IsNotEmpty(mine, $"{t.Y:0} 테라스에 조형물이 없다");
                foreach (var p in mine)
                {
                    Assert.LessOrEqual(Mathf.Abs(p.X - c.x) + p.Half, L.PlateHalf, "판 밖");
                    Assert.LessOrEqual(Mathf.Abs(p.Z - c.y) + p.Half, L.PlateHalf, "판 밖");
                    foreach (var h in t.Holes)
                    {
                        Assert.IsTrue(Mathf.Abs(p.X - h.X) > p.Half + h.Half + 6f || Mathf.Abs(p.Z - h.Z) > p.Half + h.Half + 6f, $"{p.Y:0} 조형물이 구멍을 막는다");
                    }
                    if (L.RespawnPoints.TryGetValue(p.Y, out var r))
                    {
                        Assert.IsTrue(Mathf.Abs(p.X - r.x) > p.Half + 8f || Mathf.Abs(p.Z - r.z) > p.Half + 8f, $"{p.Y:0} 조형물이 부활 지점 위");
                    }
                    foreach (var l in L.Lasers)
                    {
                        bool sameBand = l.Pivot.y > p.Y - 5f && l.Pivot.y < p.Y + p.Height + 5f;
                        float d = new Vector2(l.Pivot.x - p.X, l.Pivot.z - p.Z).magnitude;
                        Assert.IsFalse(sameBand && d < l.Length + p.Half * 1.42f, $"{p.Y:0} 조형물이 레이저 {l.Name}의 원 안");
                    }
                }
            }
        }

        [Test]
        public void 피라미드_부활_지점은_모두_판_위_구멍_밖()
        {
            Assert.IsNull(SkydivePyramidBuilder.FindBadRespawn());
        }

        [Test]
        public void 벽_면엔_돌_쌓은_결이_있다()
        {
            Assert.AreEqual(0f, LOPToonMaterials.Create(Color.white).GetFloat("_SideGrid"), "다른 모드엔 영향 없음(기본 꺼짐)");
            Assert.Greater(SkydivePyramidDressing.StoneDark.GetFloat("_SideGrid"), 0f);
            Assert.Greater(SkydivePyramidDressing.Stone.GetFloat("_SideGrid"), 0f);
        }

        [Test]
        public void 경계벽은_그림자를_드리우지_않고_면을_그리지_않는다()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(SkydivePyramidBuilder.ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                var bodies = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true)).Where(r => r.name.StartsWith("Body_")).ToArray();
                Assert.IsNotEmpty(bodies);
                Assert.IsTrue(bodies.All(b => b.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off), "몸체 그림자가 아래 테라스를 덮는다");
                var walls = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true)).Where(r => r.name.StartsWith("BandWall")).ToArray();
                Assert.AreEqual(L.BandWalls.Length, walls.Length);
                foreach (var w in walls)
                {
                    Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, w.shadowCastingMode, w.name);
                    Assert.IsFalse(w.enabled, "벽 면은 그리지 않는다 — 울타리 빔이 보여 준다");
                }
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void 꾸밈은_충돌체가_없고_메시는_저장된다()
        {
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
                var terrace = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true)).First(r => r.name.StartsWith("Terrace_"));
                Assert.AreEqual("LOP/Toon", terrace.sharedMaterial.shader.name);
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true); }
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

            //  LoadAsync는 EditMode에서 기다리기 위험하다 — 패키지의 .bytes를 직접 읽는다.
            string path = System.IO.Path.GetFullPath(
                "Packages/com.baegames.lop.masterdata.client/Runtime.Generated/StreamingAssets/MasterData/tbmap.bytes");
            var table = new LOP.MasterData.TbMap(new Luban.ByteBuf(System.IO.File.ReadAllBytes(path)));
            var map = table.GetOrDefault(9);
            Assert.IsNotNull(map, "TbMap에 9번 맵이 없다");
            Assert.AreEqual(SkydivePyramidBuilder.ScenePath, map.ScenePath);
        }
    }
}
