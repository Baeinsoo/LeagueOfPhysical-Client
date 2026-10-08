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
        //  ── 카메라 시야(스펙 §2): z −23(10-08, 20에서 물림 — FlappyCameraDistance), 세로 시야 40°, 대시 때 +3 m.
        //  화면 비는 넓은 폰(20:9)보다 조금 넉넉히 ──
        private const float CameraZ = -23f, CameraDash = 3f, HalfFovDegrees = 20f, MaxAspect = 2.4f, ViewMargin = 2f;

        //  하늘 평면 깊이와 조각 폭 — 조각마다 그 자리 통로 가운데를 따라간다(오르내리는 코스로 펼칠 때 대비).
        private const float SkyZ = 30f, PanelSegment = 10f;

        //  ── 바깥 → 굴 이음매 ──
        //  안개 막(z 2.2)은 판정면과 거의 같이 움직이므로 굴 입구 폭(24~34) 그대로 섞는다. 하늘(z 30)은 시차가 커서
        //  아치(29)가 화면에 있는 동안 아치 뒤로 보이는 하늘 x가 약 10~48을 오간다 — 그 폭 전체(29 ± 20)에 걸쳐 섞는다.
        //  한 x에서 색이 확 바뀌면 수직선이 보인다(10-08 캡처).
        private const float SkyBlendHalf = 20f, BlendSegment = 2f;

        //  랜턴 빛: 유리 가운데(사슬 위 끝 −1.85, Task 3)에서 조금 앞(랜턴 z ±0.3보다 앞). 가장자리로 갈수록 옅어지는
        //  방사형 텍스처(<c>lantern_glow.png</c>)를 붙인 사각형이라, 원판보다 크게 잡아도 테두리가 안 보인다.
        private const float LanternGlassDrop = 1.85f, GlowFront = 0.4f, GlowSize = 2.6f;

        private static readonly string[] PartNames =
        {
            "GatePlank", "GateStrap", "GateCap", "TrestleBay", "RailSpan", "CeilingRock", "CaveMouth", "Beam",
            "BgFrame", "Ladder", "Walkway", "BgTrestleBay", "MineCart", "Lantern", "SilhouetteStrip",
        };

        private static readonly string[] MaterialNames =
        {
            "Silhouette_Cave1", "Silhouette_Cave2", "Silhouette_Canyon1", "Silhouette_Canyon2",
            "Sky_Sunset", "Sky_Cave", "Sky_Mouth", "Haze_Outside", "Haze_Cave", "Haze_Mouth",
            "Haze_Far_Outside", "Haze_Far_Cave", "Haze_Far_Mouth", "LanternGlow",
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

            //  3. 굴 입구 아치 — 원점 = 통로 가운데, 뒷면 = z 0(10-08 다시 깎음: 깊이 2 m 틀이라 전부 판정면 앞·안개 막 앞).
            //  위 띠 아랫면 = 천장선, 아래 턱 윗면 = 바닥선이고 아래 턱은 데크 앞(z −2.05~−1.32)에만 있다.
            if (MineDressingLayout.SpanOverlaps(MineDressingLayout.OutsideEnd, MineDressingLayout.CaveInside, coverFrom, coverTo))
            {
                var (mx, my) = MineDressingLayout.CaveMouthAt(course);
                Instance(g.Mouth, models["CaveMouth"], new Vector3(mx, my, 0f), 0f, Vector3.one);
            }

            //  4. 배경 — 실루엣(재질을 층·구역으로 갈아 끼운다), 먼 비계·광차, 가운데 층, 랜턴 + 빛 원판.
            var density = new MineDressingLayout.BackgroundDensity(look.frameSpacing, look.ladderChance, look.lanternSpacing, look.cartCount,
                                                                   look.farScale, look.midScale);
            Mesh glowQuad = GlowQuadMesh();
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
                        Glow(g.Lights, glowQuad, materials["LanternGlow"], new Vector3(p.X, p.Y - LanternGlassDrop, p.Z - GlowFront));
                        lanterns++;
                        break;
                    default:
                        Place(g.Mid, models[p.Kind.ToString()], p, p.Z);
                        break;
                }
                background++;
            }

            //  5. 하늘(z 30)·안개 막(z 2.2) — 바깥/굴 사이를 그러데이션 띠(*_Mouth, 텍스처 u 0 = 바깥 → 1 = 굴)로 잇는다.
            float skyFrom = MineDressingLayout.CaveMouthX - SkyBlendHalf, skyTo = MineDressingLayout.CaveMouthX + SkyBlendHalf;
            float hazeFrom = MineDressingLayout.OutsideEnd, hazeTo = MineDressingLayout.CaveInside;
            Vector2 skyView = ViewHalfSize(SkyZ);
            float skyH = skyView.y * 2f;
            int panels = 0;
            panels += Panels(g.Sky, course, dressFrom - skyView.x, skyFrom, SkyZ, skyH, materials["Sky_Sunset"]);
            panels += BlendStrip(g.Sky, course, skyFrom, skyTo, SkyZ, skyH, materials["Sky_Mouth"]);
            panels += Panels(g.Sky, course, skyTo, dressTo + skyView.x, SkyZ, skyH, materials["Sky_Cave"]);
            //  안개 막 두 겹: 판정면 바로 뒤(배경 전부, Haze_*), 가운데 층 뒤(먼 비계·실루엣만 한 겹 더, Haze_Far_* — 더 진하다).
            foreach (var (hazeZ, prefix) in new[] { (MineDressingLayout.HazeZ, "Haze_"), (MineDressingLayout.FarHazeZ, "Haze_Far_") })
            {
                Vector2 hazeView = ViewHalfSize(hazeZ);
                float hazeH = hazeView.y * 2f;
                panels += Panels(g.Haze, course, dressFrom - hazeView.x, hazeFrom, hazeZ, hazeH, materials[prefix + "Outside"]);
                panels += BlendStrip(g.Haze, course, hazeFrom, hazeTo, hazeZ, hazeH, materials[prefix + "Mouth"]);
                panels += Panels(g.Haze, course, hazeTo, dressTo + hazeView.x, hazeZ, hazeH, materials[prefix + "Cave"]);
            }

            MakeRenderOnly(dressing.gameObject);
            Undo.RegisterCreatedObjectUndo(dressing.gameObject, "Dress mine course");

            Debug.Log($"[광산 옷] 범위 {dressFrom:F2}~{dressTo:F2} (바닥·천장 그림 {coverFrom:F2}~{coverTo:F2})"
                    + $" · 관문 파이프 {gates}개 · 비계 {floor}칸 · 천장 {ceiling}조각 · 배경 {background}개(랜턴 {lanterns})"
                    + $" · 하늘·안개 {panels}장 · 끈 회색 렌더러 {hidden}개");
        }

        // ---- 관문 ----

        //  파이프 사각형 하나를 판자 칸·쇠띠·쇠테로 정확히 채운다. 가로는 1.95 이하 칸으로 이어 깐다(긴 관문도 늘이지 않는다).
        private static void DressGate(Transform parent, Dictionary<string, GameObject> models,
                                      float x, float width, float bottom, float top, bool capAtTop)
        {
            GateStackLayout stack = MineDressingLayout.GateStack(bottom, top, capAtTop);
            foreach (GatePlankColumn col in MineDressingLayout.GateColumns(x, width))
            {
                float cx = col.X, sx = col.ScaleX;
                foreach (GateCell cell in stack.Cells)
                {
                    Instance(parent, models["GatePlank"], new Vector3(cx, cell.Y0, GateZ), 0f, new Vector3(sx, cell.Height, 1f));
                }
                foreach (float y in stack.StrapYs)
                {
                    Instance(parent, models["GateStrap"], new Vector3(cx, y, GateZ), 0f, new Vector3(sx, 1f, 1f));
                }
                if (stack.Cells.Count > 0)
                {
                    //  위 관문(틈이 아래)은 Z축 180° — 원점(윗면)이 아래 끝에 오고 두께가 위로 간다. 볼트(앞 −z)는 그대로 앞이다.
                    Instance(parent, models["GateCap"], new Vector3(cx, stack.CapY, GateZ), stack.CapFlipped ? 180f : 0f, new Vector3(sx, 1f, 1f));
                }
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

        //  바깥/굴 그러데이션 띠: [x0, x1]을 BlendSegment 근처 칸으로 나눈 메시 하나. 칸 경계마다 그 자리 통로 가운데에 높이 height,
        //  u = 0(x0) → 1(x1), v = 0(아래) → 1(위) — 텍스처(FlappyMineMaterials의 *_mouth_blend.png)가 u로 바깥 → 굴을 섞는다.
        private static int BlendStrip(Transform parent, MineCourse course, float x0, float x1, float z, float height, Material material)
        {
            if (!(x1 > x0)) { return 0; }
            int n = Mathf.Max(1, Mathf.CeilToInt((x1 - x0) / BlendSegment));
            var vertices = new Vector3[(n + 1) * 2];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[n * 6];
            float midX = (x0 + x1) / 2f, midY = course.CenterAt(midX);
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n, x = Mathf.Lerp(x0, x1, t), cy = course.CenterAt(x);
                //  메시는 띠 가운데(midX, midY, z)를 원점으로 — 정점은 거기서 잰 상대 좌표.
                vertices[i * 2] = new Vector3(x - midX, cy - height / 2f - midY, 0f);
                vertices[i * 2 + 1] = new Vector3(x - midX, cy + height / 2f - midY, 0f);
                uvs[i * 2] = new Vector2(t, 0f);
                uvs[i * 2 + 1] = new Vector2(t, 1f);
            }
            for (int i = 0; i < n; i++)
            {
                int bl = i * 2, tl = bl + 1, br = bl + 2, tr = bl + 3;
                //  카메라(−z)에서 보면 시계 방향 = 앞면(Unity Quad와 같은 감김).
                triangles[i * 6] = bl; triangles[i * 6 + 1] = tr; triangles[i * 6 + 2] = br;
                triangles[i * 6 + 3] = tr; triangles[i * 6 + 4] = bl; triangles[i * 6 + 5] = tl;
            }
            var mesh = new Mesh { name = material.name + "_Strip", vertices = vertices, uv = uvs, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject(material.name);
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.position = new Vector3(midX, midY, z);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return 1;
        }

        private static void Glow(Transform parent, Mesh quad, Material material, Vector3 position)
        {
            var go = new GameObject("LanternGlow");
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.position = position;
            go.transform.localScale = new Vector3(GlowSize, GlowSize, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        //  1×1, −Z를 보는 UV 사각형(씬에 한 벌 — 빛 전부가 같이 쓴다). 둥근 모양·옅어짐은 텍스처가 맡는다.
        private static Mesh GlowQuadMesh()
        {
            var mesh = new Mesh
            {
                name = "MineLanternGlowQuad",
                vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) },
                uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) },
                //  카메라(−z)에서 보면 시계 방향 = 앞면.
                triangles = new[] { 0, 3, 1, 3, 0, 2 },
            };
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
