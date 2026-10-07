using System.Collections.Generic;
using LOP.MapTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LOP.EditorTools
{
    /// <summary>
    /// 광산 옷 입히기(스펙 §6) — 회색 박스로 구운 코스 위에 블렌더 부품(<c>Assets/Art/Models/Mine</c>)을
    /// <see cref="MineDressingLayout"/>이 정한 자리에 놓고, 범위 안 회색 박스의 <b>렌더러만</b> 끈다(콜라이더는 손대지 않는다).
    ///
    /// <para><b>무엇을 끄나</b>는 이름이 아니라 위치 x로 정한다. 이름은 종류(관문·바닥·천장)를 가를 때만 쓴다.
    /// 관문 파이프는 범위에 조금이라도 걸치면 통째로 입히고 끈다. 바닥·천장 조각은 범위 안에 완전히 든 것만 끄고,
    /// 걸친 조각은 켠 채 두는 대신 바닥·천장 그림을 그 조각 끝까지 이어 덮는다(<see cref="MineDressingLayout.CoverRange"/>).</para>
    ///
    /// <para><b>보이는 면은 판정면(z = 0) 앞에</b> 둔다 — 원근 카메라가 판정면 뒤 면을 가운데로 당겨 그려 틈이 좁아 보이지
    /// 않게(<see cref="FlappyDepthAlignTool"/>와 같은 이유). 관문·바닥·천장 부품은 뒷면 z(Task 3 실측)만큼 앞으로 당긴다.</para>
    ///
    /// <para>전부 렌더 전용이다: 콜라이더 없음, 그림자 끔, 정적 배칭. 결과는 <c>ComposedMap/Dressing</c> 하나에 모이고
    /// 굽기마다 지우고 새로 만든다.</para>
    /// </summary>
    public static class FlappyMineDressing
    {
        private const string DressingName = "Dressing";
        private const string ModelDir = "Assets/Art/Models/Mine";
        private const string MaterialDir = "Assets/Art/Materials/Mine";

        //  배경 난수 시드 — 같은 시드면 같은 배경(굽기를 다시 해도 배경이 안 바뀐다).
        private const int BackgroundSeed = 7;

        //  ── 판정면 앞으로 당기기(부품 뒷면 z, Task 3 실측) ──
        //  관문 셋(GatePlank ±0.65, GateStrap ±0.69, GateCap −0.80~+0.73)은 한 틀에서 만든 것이라 같은 z로 옮긴다.
        private const float GateZ = -0.73f;
        //  TrestleBay ±0.65(RailSpan ±0.55).
        private const float FloorZ = -0.65f;
        //  CeilingRock −1.607~+1.35, Beam ±0.8.
        private const float CeilingRockZ = -1.35f, BeamZ = -0.8f;
        //  GatePlank·GateStrap·GateCap의 x 폭 — 보통 관문 폭과 같다. 긴 관문은 x 축척으로 늘인다.
        private const float GateUnitWidth = 1.95f;

        //  ── 카메라 시야(스펙 §2): z −20, 세로 시야 40°, 대시 때 +3 m. 화면 비는 넓은 폰(20:9)보다 조금 넉넉히 ──
        private const float CameraZ = -20f, CameraDash = 3f, HalfFovDegrees = 20f, MaxAspect = 2.4f, ViewMargin = 2f;

        //  하늘 평면 깊이와 조각 폭 — 조각마다 그 자리 통로 가운데를 따라간다(오르내리는 코스로 펼칠 때 대비).
        private const float SkyZ = 30f, PanelSegment = 10f;

        //  랜턴 빛 원판: 유리 가운데(사슬 위 끝 −1.85, Task 3)에서 조금 앞(랜턴 z ±0.3보다 앞), 지름.
        private const float LanternGlassDrop = 1.85f, GlowFront = 0.4f, GlowDiameter = 1.6f;
        private const int GlowSides = 24;

        private static readonly string[] PartNames =
        {
            "GatePlank", "GateStrap", "GateCap", "TrestleBay", "RailSpan", "CeilingRock", "CaveMouth", "Beam",
            "BgFrame", "Ladder", "Walkway", "BgTrestleBay", "MineCart", "Lantern", "SilhouetteStrip",
        };

        private static readonly string[] MaterialNames =
        {
            "Silhouette_Cave1", "Silhouette_Cave2", "Silhouette_Canyon1", "Silhouette_Canyon2",
            "Sky_Sunset", "Sky_Cave", "Haze_Outside", "Haze_Cave", "LanternGlow",
        };

        /// <summary>
        /// <paramref name="composedMap"/>(회색 박스가 이미 구워진 ComposedMap) 아래 <c>Dressing</c>을 다시 만든다.
        /// [<paramref name="dressFrom"/>, <paramref name="dressTo"/>]가 입히는 범위(보기 구간), 배경은 앞뒤 15 m 더.
        /// 부품·재질이 하나라도 없으면 에러만 남기고 아무것도 안 바꾼다(회색 박스 그대로).
        /// </summary>
        public static void Dress(Transform composedMap, MineCourse course, FlappyMineLook look, float dressFrom, float dressTo)
        {
            Transform old = composedMap.Find(DressingName);
            if (old != null)
            {
                Undo.DestroyObjectImmediate(old.gameObject);
            }

            if (TryLoadKit(out var models, out var materials) == false)
            {
                return;
            }

            var dressing = new GameObject(DressingName).transform;
            dressing.SetParent(composedMap, worldPositionStays: false);
            var g = new Groups(dressing);

            //  1. 회색 박스: 관문은 입히고 끄고, 바닥·천장은 안에 든 것만 끈다.
            var slabSpans = new List<(float x0, float x1)>();
            int gates = 0, hidden = 0;
            for (int i = 0; i < composedMap.childCount; i++)
            {
                Transform t = composedMap.GetChild(i);
                if (t == dressing) { continue; }
                string n = t.name;
                var r = t.GetComponent<MeshRenderer>();
                if (r == null) { continue; }

                bool low = n.StartsWith("PipeLow_");
                if (low || n.StartsWith("PipeHigh_"))
                {
                    //  Pipe()는 가운데 원점 상자(축척 = 폭·높이)라 트랜스폼이 곧 파이프 사각형이다.
                    Vector3 p = t.position, s = t.localScale;
                    float x0 = p.x - s.x * 0.5f, x1 = p.x + s.x * 0.5f;
                    if (MineDressingLayout.SpanOverlaps(x0, x1, dressFrom, dressTo) == false) { continue; }
                    DressGate(g.Gates, models, p.x, s.x, p.y - s.y * 0.5f, p.y + s.y * 0.5f, capAtTop: low);
                    Hide(r);
                    gates++;
                    hidden++;
                }
                else if (n.StartsWith("Floor_") || n.StartsWith("Ceiling_"))
                {
                    Bounds b = r.bounds;
                    slabSpans.Add((b.min.x, b.max.x));
                    if (MineDressingLayout.SpanInside(b.min.x, b.max.x, dressFrom, dressTo))
                    {
                        Hide(r);
                        hidden++;
                    }
                }
            }

            //  2. 바닥·천장 — 걸친 회색 조각 끝까지 넓힌 범위.
            var (coverFrom, coverTo) = MineDressingLayout.CoverRange(slabSpans, dressFrom, dressTo);
            int floor = 0;
            foreach (MinePiece p in MineDressingLayout.TrestleBays(course, coverFrom, coverTo))
            {
                Place(g.Floor, models["TrestleBay"], p, p.Z + FloorZ);
                Place(g.Floor, models["RailSpan"], p, p.Z + FloorZ);
                floor++;
            }

            //  천장은 구역마다 따로: 노을 바깥 = 들보, 굴 입구 = 없음(아치가 덮는다), 굴 안 = 바위.
            int ceiling = 0;
            if (coverFrom < MineDressingLayout.OutsideEnd)
            {
                foreach (MinePiece p in MineDressingLayout.CeilingPieces(course, coverFrom, Mathf.Min(coverTo, MineDressingLayout.OutsideEnd)))
                {
                    Place(g.Ceiling, models["Beam"], p, p.Z + BeamZ);
                    ceiling++;
                }
            }
            if (coverTo > MineDressingLayout.CaveInside)
            {
                foreach (MinePiece p in MineDressingLayout.CeilingPieces(course, Mathf.Max(coverFrom, MineDressingLayout.CaveInside), coverTo))
                {
                    Place(g.Ceiling, models["CeilingRock"], p, p.Z + CeilingRockZ);
                    ceiling++;
                }
            }

            //  3. 굴 입구 아치 — 원점 = 통로 가운데(Task 3), z는 부품 그대로(아래 덩이는 데크 뒤로 들어간다).
            if (MineDressingLayout.SpanOverlaps(MineDressingLayout.OutsideEnd, MineDressingLayout.CaveInside, coverFrom, coverTo))
            {
                var (mx, my) = MineDressingLayout.CaveMouthAt(course);
                Instance(g.Mouth, models["CaveMouth"], new Vector3(mx, my, 0f), 0f, Vector3.one);
            }

            //  4. 배경 — 실루엣(재질을 층·구역으로 갈아 끼운다), 먼 비계·광차, 가운데 층, 랜턴 + 빛 원판.
            var density = new MineDressingLayout.BackgroundDensity(look.frameSpacing, look.ladderChance, look.lanternSpacing, look.cartCount);
            Mesh disc = DiscMesh(GlowSides);
            int background = 0, lanterns = 0;
            foreach (MinePiece p in MineDressingLayout.Background(course, dressFrom, dressTo, BackgroundSeed, density))
            {
                switch (p.Kind)
                {
                    case MinePartKind.CaveSilhouette:
                    case MinePartKind.CanyonSilhouette:
                        var sil = Place(g.Silhouettes, models["SilhouetteStrip"], p, p.Z);
                        string mat = (p.Kind == MinePartKind.CaveSilhouette ? "Silhouette_Cave" : "Silhouette_Canyon") + (p.Layer + 1);
                        sil.GetComponent<MeshRenderer>().sharedMaterial = materials[mat];
                        break;
                    case MinePartKind.BgTrestleBay:
                    case MinePartKind.MineCart:
                        Place(g.Far, models[p.Kind.ToString()], p, p.Z);
                        break;
                    case MinePartKind.Lantern:
                        Place(g.Lights, models["Lantern"], p, p.Z);
                        Glow(g.Lights, disc, materials["LanternGlow"], new Vector3(p.X, p.Y - LanternGlassDrop, p.Z - GlowFront));
                        lanterns++;
                        break;
                    default:
                        Place(g.Mid, models[p.Kind.ToString()], p, p.Z);
                        break;
                }
                background++;
            }

            //  5. 하늘(z 30)·안개 막(z 2.2) — 굴 입구 가운데(29)에서 바깥/굴로 갈린다. 이음매는 아치 뒤.
            float split = MineDressingLayout.CaveMouthX;
            Vector2 skyView = ViewHalfSize(SkyZ), hazeView = ViewHalfSize(MineDressingLayout.HazeZ);
            int panels = 0;
            panels += Panels(g.Sky, course, dressFrom - skyView.x, split, SkyZ, skyView.y * 2f, materials["Sky_Sunset"]);
            panels += Panels(g.Sky, course, split, dressTo + skyView.x, SkyZ, skyView.y * 2f, materials["Sky_Cave"]);
            panels += Panels(g.Haze, course, dressFrom - hazeView.x, split, MineDressingLayout.HazeZ, hazeView.y * 2f, materials["Haze_Outside"]);
            panels += Panels(g.Haze, course, split, dressTo + hazeView.x, MineDressingLayout.HazeZ, hazeView.y * 2f, materials["Haze_Cave"]);

            MakeRenderOnly(dressing.gameObject);
            Undo.RegisterCreatedObjectUndo(dressing.gameObject, "Dress mine course");

            Debug.Log($"[광산 옷] 범위 {dressFrom:F2}~{dressTo:F2} (바닥·천장 그림 {coverFrom:F2}~{coverTo:F2})"
                    + $" · 관문 파이프 {gates}개 · 비계 {floor}칸 · 천장 {ceiling}조각 · 배경 {background}개(랜턴 {lanterns})"
                    + $" · 하늘·안개 {panels}장 · 끈 회색 렌더러 {hidden}개");
        }

        // ---- 관문 ----

        //  파이프 사각형 하나를 판자 칸·쇠띠·쇠테로 정확히 채운다. 폭은 x 축척(보통 관문 1.95면 1).
        private static void DressGate(Transform parent, Dictionary<string, GameObject> models,
                                      float x, float width, float bottom, float top, bool capAtTop)
        {
            float sx = width / GateUnitWidth;
            GateStackLayout stack = MineDressingLayout.GateStack(bottom, top, capAtTop);
            foreach (GateCell cell in stack.Cells)
            {
                Instance(parent, models["GatePlank"], new Vector3(x, cell.Y0, GateZ), 0f, new Vector3(sx, cell.Height, 1f));
            }
            foreach (float y in stack.StrapYs)
            {
                Instance(parent, models["GateStrap"], new Vector3(x, y, GateZ), 0f, new Vector3(sx, 1f, 1f));
            }
            if (stack.Cells.Count > 0)
            {
                //  위 관문(틈이 아래)은 Z축 180° — 원점(윗면)이 아래 끝에 오고 두께가 위로 간다. 볼트(앞 −z)는 그대로 앞이다.
                Instance(parent, models["GateCap"], new Vector3(x, stack.CapY, GateZ), stack.CapFlipped ? 180f : 0f, new Vector3(sx, 1f, 1f));
            }
        }

        // ---- 놓기 ----

        private static GameObject Place(Transform parent, GameObject model, MinePiece p, float z)
        {
            return Instance(parent, model, new Vector3(p.X, p.Y, z), p.AngleDegrees + (p.Flipped ? 180f : 0f),
                            new Vector3(p.ScaleX, p.ScaleY, 1f));
        }

        private static GameObject Instance(Transform parent, GameObject model, Vector3 position, float angleZ, Vector3 scale)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, angleZ));
            go.transform.localScale = scale;
            return go;
        }

        //  하늘·안개: [x0, x1]을 PanelSegment 근처 같은 폭 조각으로, 조각마다 그 자리 통로 가운데에 높이 height.
        private static int Panels(Transform parent, MineCourse course, float x0, float x1, float z, float height, Material material)
        {
            if (!(x1 > x0)) { return 0; }
            int n = Mathf.Max(1, Mathf.CeilToInt((x1 - x0) / PanelSegment));
            float w = (x1 - x0) / n;
            for (int i = 0; i < n; i++)
            {
                float cx = x0 + (i + 0.5f) * w;
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);   // −Z(카메라 쪽)를 보는 1×1 평면
                go.name = $"{material.name}_{i}";
                go.transform.SetParent(parent, worldPositionStays: false);
                go.transform.position = new Vector3(cx, course.CenterAt(cx), z);
                go.transform.localScale = new Vector3(w, height, 1f);
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
            return n;
        }

        private static void Glow(Transform parent, Mesh disc, Material material, Vector3 position)
        {
            var go = new GameObject("LanternGlow");
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.position = position;
            go.transform.localScale = new Vector3(GlowDiameter, GlowDiameter, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = disc;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        //  지름 1, −Z를 보는 원판(씬에 한 벌 — 빛 원판 전부가 같이 쓴다).
        private static Mesh DiscMesh(int sides)
        {
            var vertices = new Vector3[sides + 1];
            var triangles = new int[sides * 3];
            vertices[0] = Vector3.zero;
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                vertices[i + 1] = new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, 0f);
                //  가운데 → 다음 → 지금: 카메라(−z)에서 보면 시계 방향 = 앞면.
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (i + 1) % sides + 1;
                triangles[i * 3 + 2] = i + 1;
            }
            var mesh = new Mesh { name = "MineLanternGlowDisc", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        //  깊이 z 평면에서 카메라가 보는 반 폭·반 높이(대시 거리까지, 여유 포함).
        private static Vector2 ViewHalfSize(float z)
        {
            float halfH = (z - CameraZ + CameraDash) * Mathf.Tan(HalfFovDegrees * Mathf.Deg2Rad);
            return new Vector2(halfH * MaxAspect + ViewMargin, halfH + ViewMargin);
        }

        // ---- 렌더 전용 ----

        private static void Hide(Renderer r)
        {
            Undo.RecordObject(r, "Dress mine course");
            r.enabled = false;
        }

        //  콜라이더 없음(FBX 임포트·Quad 기본 콜라이더 제거), 그림자 끔, 정적 배칭.
        private static void MakeRenderOnly(GameObject root)
        {
            foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(c);
            }
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            }
        }

        // ---- 부품·재질 ----

        private static bool TryLoadKit(out Dictionary<string, GameObject> models, out Dictionary<string, Material> materials)
        {
            models = new Dictionary<string, GameObject>();
            materials = new Dictionary<string, Material>();
            var missing = new List<string>();
            foreach (string name in PartNames)
            {
                var m = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{name}.fbx");
                if (m == null) { missing.Add($"{ModelDir}/{name}.fbx"); } else { models[name] = m; }
            }
            foreach (string name in MaterialNames)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/{name}.mat");
                if (m == null) { missing.Add($"{MaterialDir}/{name}.mat"); } else { materials[name] = m; }
            }
            if (missing.Count > 0)
            {
                Debug.LogError("[광산 옷] 부품·재질이 없어 입히지 않았다(회색 박스 그대로): " + string.Join(", ", missing));
                return false;
            }
            return true;
        }

        //  Dressing 아래 층별 묶음.
        private sealed class Groups
        {
            public readonly Transform Gates, Floor, Ceiling, Mouth, Far, Mid, Silhouettes, Sky, Haze, Lights;

            public Groups(Transform dressing)
            {
                Gates = Child(dressing, "Gates");
                Floor = Child(dressing, "Floor");
                Ceiling = Child(dressing, "Ceiling");
                Mouth = Child(dressing, "Mouth");
                Transform background = Child(dressing, "Background");
                Far = Child(background, "Far");
                Mid = Child(background, "Mid");
                Silhouettes = Child(dressing, "Silhouettes");
                Sky = Child(dressing, "Sky");
                Haze = Child(dressing, "Haze");
                Lights = Child(dressing, "Lights");
            }

            private static Transform Child(Transform parent, string name)
            {
                var t = new GameObject(name).transform;
                t.SetParent(parent, worldPositionStays: false);
                return t;
            }
        }
    }
}
