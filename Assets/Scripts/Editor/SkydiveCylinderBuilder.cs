using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static LOP.EditorTools.SkydiveMapKit;
using Y = LOP.EditorTools.SkydiveCylinderLayout;

namespace LOP.EditorTools
{
    /// <summary>
    /// 원통 시제품 맵을 표(<see cref="SkydiveCylinderLayout"/>)에서 굽는다. 대화상자를 띄우지 않는다(CLI로 돌린다).
    /// 원판·조리개 날개는 부채꼴 메시(메시 콜라이더), 풍차 날개는 상자 — 보이는 모양이 곧 판정이다.
    /// </summary>
    public static class SkydiveCylinderBuilder
    {
        public const string ScenePath = "Assets/Art/Scenes/SkydiveCylinderMap.unity";
        private const float MinPassage = 6f;   // 몸(0.8) + 넉넉한 조종 여유 — 이보다 좁은 틈은 틈이 아니다

        [MenuItem("LOP/Skydive/원통 시제품 굽기")]
        public static void Build()
        {
            string error = Verify();
            if (error != null)
            {
                Debug.LogError($"[SkydiveCylinder] 검사 실패 — {error}. 굽지 않는다.");
                return;
            }

            var scene = System.IO.File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects())
            {
                Object.DestroyImmediate(go);
            }

            var stone = SkydiveMapKit.Stone;
            var wallMat = SkydiveMapKit.StoneDark;
            //  장애물 윗면 격자 — 단색 큰 판은 가까워져도 거리가 안 읽힌다(사용자 10-07). 판과 같이 돌게 메시 UV 기준.
            var discMat = HazardMaterial("HazardDisc", "#C8553D");
            var irisMat = HazardMaterial("HazardIris", "#D98A2B");
            var millMat = HazardMaterial("HazardMill", "#8A5BB0");
            var root = new GameObject("Course").transform;

            //  원통 벽(하늘 유적 탑) — 출구 높이(ExitY)에서 끝난다. 판정은 상자, 그림은 안쪽 면 + 바깥 면
            //  (안쪽만 그리면 카메라가 벽 밖으로 나가도 안 가리고, 바깥 면은 출구를 빠져나온 뒤 위로 탑이 보이게).
            float wallTop = Y.SpawnY + 60f, wallBottom = Y.ExitY, wallH = wallTop - wallBottom, wallCy = (wallTop + wallBottom) * 0.5f;
            float segLen = 2f * Mathf.PI * (Y.Radius + Y.Wall) / Y.WallSegments + 0.5f;
            var walls = new GameObject("Wall").transform;
            walls.SetParent(root, false);
            for (int k = 0; k < Y.WallSegments; k++)
            {
                float deg = k * 360f / Y.WallSegments;
                var radial = Y.OnCircle(1f, deg, 0f);
                var box = Box(walls, $"Wall_{k}", wallMat, radial * (Y.Radius + Y.Wall * 0.5f) + Vector3.up * wallCy,
                              new Vector3(Y.Wall, wallH, segLen), Quaternion.Euler(0f, -deg, 0f));
                box.GetComponent<MeshRenderer>().enabled = false;
                Face(walls, $"Wall_{k}_In", wallMat, radial * Y.Radius + Vector3.up * wallCy, Quaternion.LookRotation(radial),
                     new Vector3(2f * Mathf.PI * Y.Radius / Y.WallSegments + 0.3f, wallH, 1f));   // Quad 앞면(-Z)이 안쪽
                Face(walls, $"Wall_{k}_Out", wallMat, radial * (Y.Radius + Y.Wall) + Vector3.up * wallCy, Quaternion.LookRotation(-radial),
                     new Vector3(2f * Mathf.PI * (Y.Radius + Y.Wall) / Y.WallSegments + 0.3f, wallH, 1f));   // 바깥을 본다
            }

            //  틈새 빛줄기 — 벽에서 비스듬히 아래로 들어오는 빛 기둥(충돌 없음, 가산). 아래로 갈수록 굵다.
            var shaftMat = LightShaftMaterial();
            var shafts = new GameObject("LightShafts").transform;
            shafts.SetParent(root, false);
            foreach (var (y, deg, width) in Y.LightShafts)
            {
                var inward = -Y.OnCircle(1f, deg, 0f);
                var dir = (inward * Mathf.Cos(40f * Mathf.Deg2Rad) + Vector3.down * Mathf.Sin(40f * Mathf.Deg2Rad)).normalized;
                const float len = 90f;
                var start = Y.OnCircle(Y.Radius - 0.5f, deg, y);
                var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(beam.GetComponent<Collider>());
                beam.name = $"Shaft_{y:0}";
                beam.transform.SetParent(shafts, false);
                beam.transform.localPosition = start + dir * (len * 0.5f);
                beam.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
                beam.transform.localScale = new Vector3(width, len * 0.5f, width);
                var br = beam.GetComponent<MeshRenderer>();
                br.sharedMaterial = shaftMat;
                br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            //  출발 고리 + 스폰
            MeshBody(root, "Spawn", stone, Sector("CylSpawnRing", Y.SpawnHole, Y.Radius, 0f, 360f, Vector3.zero), new Vector3(0f, Y.SpawnY, 0f));
            var spawns = new GameObject("Spawns").transform;
            spawns.SetParent(root, false);
            for (int i = 0; i < 8; i++)
            {
                var sp = new GameObject($"Spawn_{i}");
                sp.transform.SetParent(spawns, false);
                sp.transform.localPosition = Y.OnCircle(Y.SpawnRingRadius, i * 45f, Y.SpawnY + Y.Thickness * 0.5f + 1f);
                sp.AddComponent<LOP.SpawnPoint>().Order = i;
            }

            //  도는 원판 — 틈 난 부채꼴 하나를 SpinnerVolume이 돌린다
            foreach (var d in Y.Discs)
            {
                var spinner = Hub(root, d.Name, d.Y).AddComponent<LOP.SpinnerVolume>();
                spinner.StartDegrees = d.StartDegrees;
                spinner.DegreesPerTick = d.DegreesPerTick;
                spinner.Rideable = d.Rideable;
                MeshBody(spinner.transform, "Plate", discMat, Sector($"Cyl{d.Name}", 0f, Y.Radius - 1f, d.GapDegrees, 360f, Vector3.zero), Vector3.zero);
            }

            //  조리개 — 날개(부채꼴)마다 닫힌 자리에서 바깥으로 물러난다
            foreach (var iris in Y.Irises)
            {
                var vol = Hub(root, iris.Name, iris.Y).AddComponent<LOP.IrisVolume>();
                vol.Travel = iris.Travel;
                vol.Rideable = iris.Rideable;
                vol.Period = iris.Period; vol.OpenTicks = iris.OpenTicks; vol.MoveTicks = iris.MoveTicks; vol.Phase = iris.Phase;
                var blades = new List<Transform>();
                float w = 360f / iris.Blades;
                for (int b = 0; b < iris.Blades; b++)
                {
                    //  날개 피벗 = 이등분선 방향 1m — 그 방향이 곧 물러나는 방향이다(IrisVolume.Pose).
                    var pivot = Y.OnCircle(1f, (b + 0.5f) * w, 0f);
                    var blade = MeshBody(vol.transform, $"Blade_{b}", irisMat, Sector($"Cyl{iris.Name}_{b}", 0f, Y.Radius - 1f, b * w, (b + 1) * w, pivot), pivot);
                    blades.Add(blade.transform);
                }
                vol.Blades = blades.ToArray();
                vol.Capture();
            }

            //  풍차 날개 — 상자 날개를 SpinnerVolume이 돌린다
            foreach (var m in Y.Windmills)
            {
                var spinner = Hub(root, m.Name, m.Y).AddComponent<LOP.SpinnerVolume>();
                spinner.StartDegrees = m.StartDegrees;
                spinner.DegreesPerTick = m.DegreesPerTick;
                spinner.Rideable = m.Rideable;
                float len = Y.Radius - 2f;
                var bar = BarMesh($"Cyl{m.Name}_Blade", new Vector3(len, Y.Thickness, m.Width));
                for (int b = 0; b < m.Blades; b++)
                {
                    float deg = b * 360f / m.Blades;
                    var blade = MeshBody(spinner.transform, $"Blade_{b}", millMat, bar, Y.OnCircle(len * 0.5f, deg, 0f));
                    blade.transform.localRotation = Quaternion.Euler(0f, -deg, 0f);   // 로컬 +X가 (cos, 0, sin)을 보게(DoorVolume.PanelRotation과 같은 부호)
                }
            }

            //  닫히는 큰 판 — 원통 모양 고리(가운데 원 r=√2·20) + 사각 구멍 둘레의 네 토막. 사각 판이면 모서리가 탑 밖으로 튀어나온다.
            float holeHalf = Y.DoorHole * 0.5f, inner = holeHalf * 1.4143f;
            MeshBody(root, "DoorSlab_Ring", stone, Sector("CylDoorSlabRing", inner, Y.Radius + 0.5f, 0f, 360f, Vector3.zero), new Vector3(0f, Y.DoorSlabY, 0f));
            float band = inner - holeHalf;
            Box(root, "DoorSlab_N", stone, new Vector3(0f, Y.DoorSlabY, holeHalf + band * 0.5f), new Vector3(Y.DoorHole, Y.Thickness, band), Quaternion.identity);
            Box(root, "DoorSlab_S", stone, new Vector3(0f, Y.DoorSlabY, -holeHalf - band * 0.5f), new Vector3(Y.DoorHole, Y.Thickness, band), Quaternion.identity);
            Box(root, "DoorSlab_E", stone, new Vector3(holeHalf + band * 0.5f, Y.DoorSlabY, 0f), new Vector3(band, Y.Thickness, Y.DoorHole), Quaternion.identity);
            Box(root, "DoorSlab_W", stone, new Vector3(-holeHalf - band * 0.5f, Y.DoorSlabY, 0f), new Vector3(band, Y.Thickness, Y.DoorHole), Quaternion.identity);
            var doors = new GameObject("Doors").transform;
            doors.SetParent(root, false);
            //  문 패널은 유니티 큐브(UV 0~1)라 메시 기준 격자가 안 나온다 — 월드 격자 재질을 따로 쓴다.
            CreateDoorVolume(doors, Y.Door, SkydiveMapKit.Toon("HazardDoor", "#D98A2B", topGrid: 4f, sideGrid: 3f));

            //  세이브 선반 — 벽의 좁은 턱 + 바닥 높이 발판(충돌 없음)
            var padMat = PadMaterial();
            foreach (var l in Y.Ledges)
            {
                MeshBody(root, $"Ledge_{l.Id}", stone, Sector($"CylLedge_{l.Id}", Y.Radius - l.Depth, Y.Radius + 0.5f, l.CenterDegrees - l.ArcDegrees * 0.5f, l.CenterDegrees + l.ArcDegrees * 0.5f, Vector3.zero),
                         new Vector3(0f, l.Y, 0f));
                var pad = Box(root, $"SavePad_{l.Id}", padMat, l.PadCenter, new Vector3(l.Depth - 1f, 0.06f, l.Depth - 1f), Quaternion.identity);
                Object.DestroyImmediate(pad.GetComponent<Collider>());
                var marker = pad.AddComponent<LOP.SavePad>();
                marker.Id = l.Id;
                marker.Label = l.Label;
            }

            //  구름 바닥. 출구 아래는 탁 트인 금빛 하늘.
            //  구름 바다는 옅은 하늘색 — 하얗게 두면 금빛 노출에 다 날아가 아무것도 안 보인다(왕눈 엔딩: 금빛 역광 + 파란 하늘·분홍 구름 귀퉁이).
            MeshBody(root, "CloudFloor", SkydiveMapKit.Toon("CloudFloor", "#86AEEF"), Sector("CylCloudFloor", 0f, Y.CloudFloorRadius, 0f, 360f, Vector3.zero), new Vector3(0f, -Y.Thickness * 0.5f, 0f));
            var puffs = new GameObject("CloudPuffs").transform;
            puffs.SetParent(root, false);
            var puffMat = SkydiveMapKit.Toon("CloudPuff", "#F2C9DA");
            var rng = new System.Random(20261007);
            for (int i = 0; i < 18; i++)
            {
                float deg = (float)rng.NextDouble() * 360f, r = 70f + (float)rng.NextDouble() * 170f, size = 18f + (float)rng.NextDouble() * 26f;
                var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(puff.GetComponent<Collider>());   // 꾸밈 — 밟히지 않는다
                puff.name = $"Puff_{i}";
                puff.transform.SetParent(puffs, false);
                puff.transform.localPosition = Y.OnCircle(r, deg, size * 0.15f);
                puff.transform.localScale = new Vector3(size, size * 0.35f, size * 0.8f);
                puff.GetComponent<MeshRenderer>().sharedMaterial = puffMat;
            }
            //  별만 결승(사용자 10-07) — 도착 판은 없다. 별을 놓치고 구름에 닿으면 출구 아래에서 다시 떨어진다.
            //  구름은 단단하게 둔다 — 별을 잡은 사람은 구름 위에 내려선다(엔딩처럼).
            var retry = new GameObject("Retry").transform;
            retry.SetParent(root, false);
            retry.localPosition = Y.RetryPoint;
            retry.gameObject.AddComponent<LOP.RetryVolume>().BelowY = Y.RetryBelowY;

            //  별 조각 — 닿으면 결승(StarVolume: 판정은 CatchTargetField, 자세는 ObstacleField). 충돌 없음.
            var star = new GameObject("Star").transform;
            star.SetParent(root, false);
            star.localPosition = Y.StarCenter;
            var starVol = star.gameObject.AddComponent<LOP.StarVolume>();
            starVol.Center = Y.StarCenter;
            starVol.OrbitRadius = Y.StarOrbit;
            starVol.DegreesPerTick = Y.StarDegreesPerTick;
            starVol.BobAmplitude = Y.StarBob;
            starVol.BobPeriod = Y.StarBobPeriod;
            starVol.CatchRadius = Y.StarCatchRadius;
            starVol.Pose(0);
            Glow(star, "Halo", StarMaterial("StarHalo", new Color(1f, 0.72f, 0.3f) * 2.6f, 1.2f), Y.StarHaloDiameter);
            Glow(star, "Core", StarMaterial("StarCore", new Color(1f, 0.95f, 0.8f) * 3f, 0f), 7f);
            //  위로 솟는 금빛 기둥 — 출구에서 내려다보면 "저기가 목표"로 읽히게(빛나는 점 하나는 밝은 하늘에 묻힌다).
            var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(beacon.GetComponent<Collider>());
            beacon.name = "Beacon";
            beacon.transform.SetParent(star, false);
            float beaconLen = Y.ExitY - (Y.StarCenter.y + Y.StarBob) - 15f;   // 가장 높이 떠도 출구에 안 닿게
            beacon.transform.localPosition = Vector3.up * (beaconLen * 0.5f);
            beacon.transform.localScale = new Vector3(3f, beaconLen * 0.5f, 3f);   // 가늘게 — 닿을 물체가 아니라 빛줄기로 읽히게
            var bmr = beacon.GetComponent<MeshRenderer>();
            bmr.sharedMaterial = StarMaterial("StarBeacon", new Color(1f, 0.75f, 0.35f) * 0.5f, 2.4f);
            bmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            //  맵별 분위기 — 클라 SkydiveAtmosphere가 내 높이로 읽는다(위는 어둡게, 출구는 금빛).
            var moodGo = new GameObject("Mood");
            moodGo.transform.SetParent(root, false);
            moodGo.AddComponent<LOP.SkydiveMood>().Keys = Y.Mood;

            //  레이저·바람·체크포인트
            var lasers = new GameObject("Lasers").transform;
            lasers.SetParent(root, false);
            foreach (var l in Y.Lasers) { CreateLaserVolume(lasers, l); }
            var winds = new GameObject("Winds").transform;
            winds.SetParent(root, false);
            var windAssets = SkydiveWindAssets.EnsureAssets();
            foreach (var w in Y.Winds) { CreateWindVolume(winds, w.Name, w.Center, w.Radius, w.Height, w.Wind, windAssets); }

            CreateCheckpointMarkers(root, Y.SpawnY, Y.RespawnPoints);
            foreach (var m in root.GetComponentsInChildren<LOP.CheckpointMarker>())
            {
                var pos = m.transform.position;
                if (Y.RespawnPoints.TryGetValue(pos.y, out var want) == false || (want - pos).sqrMagnitude > 0.01f)
                {
                    Object.DestroyImmediate(m.gameObject);   // (0, spawnY, 0)은 출발 고리 구멍 위라 지운다
                }
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[SkydiveCylinder] 구웠다 — {ScenePath}");
        }

        internal static string Verify()
        {
            //  틈 — 가운데 반지름(30)에서 잰 폭이 몸이 조종해 지날 만큼인가
            foreach (var d in Y.Discs)
            {
                float chord = 2f * 30f * Mathf.Sin(d.GapDegrees * 0.5f * Mathf.Deg2Rad);
                if (chord < MinPassage) { return $"{d.Name} 틈이 좁다({chord:0.0}m)"; }
            }
            foreach (var i in Y.Irises)
            {
                if (i.Travel < MinPassage) { return $"{i.Name}이 열려도 구멍이 좁다"; }
                if (i.OpenTicks < 30) { return $"{i.Name} 열린 시간이 너무 짧다"; }
            }
            foreach (var m in Y.Windmills)
            {
                float bladeDeg = 2f * Mathf.Atan2(m.Width * 0.5f, 30f) * Mathf.Rad2Deg;
                float gapDeg = 360f / m.Blades - bladeDeg;
                if (2f * 30f * Mathf.Sin(gapDeg * 0.5f * Mathf.Deg2Rad) < MinPassage) { return $"{m.Name} 날개 사이가 좁다"; }
            }
            //  선반은 도는 것과 높이가 겹치면 쓸린다
            foreach (var l in Y.Ledges)
            {
                foreach (float oy in Y.ObstacleYs())
                {
                    if (Mathf.Abs(l.Y - oy) < 30f) { return $"선반 {l.Label}이 장애물({oy:0})과 너무 가깝다"; }
                }
                foreach (var w in Y.Winds)
                {
                    float r = new Vector2(l.PadCenter.x - w.Center.x, l.PadCenter.z - w.Center.z).magnitude;
                    if (r < w.Radius && Mathf.Abs(l.Y - w.Center.y) < w.Height * 0.5f) { return $"선반 {l.Label}이 바람({w.Name}) 안"; }
                }
            }
            if (Y.RetryPoint.y > Y.ExitY - 10f) { return "다시 떨어지는 자리가 출구에 너무 가깝다"; }
            if (Y.RetryPoint.y < Y.StarCenter.y + Y.StarBob + 40f) { return "다시 떨어지는 자리가 별에 너무 가깝다(겨눌 틈이 없다)"; }
            if (Y.RetryBelowY <= 0f) { return "다시 떨어지기 높이가 구름 윗면 아래 — 구름에 막혀 영영 안 걸린다"; }
            if (Y.StarCenter.y + Y.StarBob > Y.ExitY - 40f) { return "별이 출구에 너무 가깝다 — 빠져나오자마자 공짜로 닿는다"; }
            if (Y.StarCenter.y - Y.StarBob - Y.StarCatchRadius < 8f) { return "별이 바닥에 너무 가깝다 — 구름 위에 서서 닿는다(공중에서 잡아야)"; }
            if (Y.StarOrbit > Y.Radius - 10f) { return "별이 탑 밖으로 돈다 — 출구 아래에서 쫓을 수 있어야"; }
            foreach (float oy in Y.ObstacleYs())
            {
                if (oy < Y.ExitY + 10f) { return $"장애물({oy:0})이 원통 출구 아래 — 탁 트인 하늘엔 원통 벽이 없어 옆으로 빠진다"; }
            }
            return FindTooFastLaser(Y.Lasers);
        }

        // ---- 도우미 ----

        private static void Face(Transform parent, string name, Material m, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(face.GetComponent<Collider>());
            face.name = name;
            face.transform.SetParent(parent, false);
            face.transform.localPosition = pos;
            face.transform.localRotation = rot;
            face.transform.localScale = scale;
            var r = face.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static void Glow(Transform parent, string name, Material m, float diameter)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * diameter;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>빛 셰이더(LOP/LaserGlow, 가산) 재질 — 깊이 옅어짐은 끈다(빛줄기·별은 늘 같은 밝기).</summary>
        private static Material GlowMaterial(string name, Color color, float falloff)
        {
            string path = $"Assets/Art/Materials/Pyramid/{name}.mat";
            var shader = Shader.Find("LOP/LaserGlow");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            m.SetColor("_Color", color);
            m.SetFloat("_Falloff", falloff);
            m.SetFloat("_FadeNear", 100000f);
            m.SetFloat("_FadeFar", 100001f);
            m.SetFloat("_FadeAbove", 1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material LightShaftMaterial() => GlowMaterial("CylLightShaft", new Color(1f, 0.78f, 0.45f) * 0.26f, 2.6f);   // 넓고 옅게 — 막대가 아니라 빛
        private static Material StarMaterial(string name, Color color, float falloff) => GlowMaterial($"Cyl{name}", color, falloff);

        private static GameObject Hub(Transform parent, string name, float y)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            return go;
        }

        /// <summary>메시 몸(그림 + 메시 콜라이더). 위치는 부모 기준.</summary>
        private static GameObject MeshBody(Transform parent, string name, Material m, Mesh mesh, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.layer = LayerMask.NameToLayer("Default");   // 낙하 sweep 마스크가 보는 레이어
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        /// <summary>
        /// 고리 부채꼴 판(두께 = 판 두께, 윗면 y = +두께/2). <paramref name="pivot"/>만큼 꼭짓점을 빼서 그 자리를 원점으로 삼는다.
        /// 각은 도, (cos, 0, sin) 방향. 면마다 꼭짓점을 따로 둬 모서리가 각지게(툰 음영).
        /// </summary>
        internal static Mesh Sector(string assetName, float r0, float r1, float a0, float a1, Vector3 pivot, bool save = true)
        {
            float h = Y.Thickness * 0.5f;
            int steps = Mathf.Max(2, Mathf.CeilToInt((a1 - a0) / 5f));
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var t = new List<int>();

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
            {
                int i = v.Count;
                v.Add(a - pivot); v.Add(b - pivot); v.Add(c - pivot); v.Add(d - pivot);
                for (int k = 0; k < 4; k++) { n.Add(normal); }
                //  유니티 앞면 = Cross(b-a, c-a)가 법선 쪽. 맞으면 그대로, 아니면 뒤집는다.
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) >= 0f) { t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 }); }
                else { t.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 }); }
            }

            Vector3 P(float r, float deg, float y) => Y.OnCircle(r, deg, y);
            for (int s = 0; s < steps; s++)
            {
                float d0 = Mathf.Lerp(a0, a1, (float)s / steps), d1 = Mathf.Lerp(a0, a1, (float)(s + 1) / steps);
                Quad(P(r0, d0, h), P(r1, d0, h), P(r1, d1, h), P(r0, d1, h), Vector3.up);
                Quad(P(r0, d0, -h), P(r0, d1, -h), P(r1, d1, -h), P(r1, d0, -h), Vector3.down);
                var outward = Y.OnCircle(1f, (d0 + d1) * 0.5f, 0f);
                Quad(P(r1, d0, -h), P(r1, d1, -h), P(r1, d1, h), P(r1, d0, h), outward);
                if (r0 > 0.01f) { Quad(P(r0, d0, -h), P(r0, d0, h), P(r0, d1, h), P(r0, d1, -h), -outward); }
            }
            if (a1 - a0 < 359.9f)
            {
                var c0 = Y.OnCircle(1f, a0 - 90f, 0f);   // 시작 끝면은 각이 줄어드는 쪽을 본다
                var c1 = Y.OnCircle(1f, a1 + 90f, 0f);
                Quad(P(r0, a0, -h), P(r1, a0, -h), P(r1, a0, h), P(r0, a0, h), c0);
                Quad(P(r0, a1, -h), P(r0, a1, h), P(r1, a1, h), P(r1, a1, -h), c1);
            }

            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetTriangles(t, 0);
            mesh.SetUVs(0, v.Select(p => new Vector2(p.x, p.z)).ToList());
            mesh.RecalculateBounds();
            return save ? SkydiveMapKit.SaveMesh(mesh, assetName) : mesh;
        }

        /// <summary>가운데가 원점인 상자 메시 — UV = 로컬 (x, z) 미터라 윗면 격자가 날개를 따라 돈다(Unity 큐브의 UV는 면마다 0~1).</summary>
        internal static Mesh BarMesh(string assetName, Vector3 size, bool save = true)
        {
            Vector3 h = size * 0.5f;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var t = new List<int>();
            void Face(Vector3 normal, Vector3 u, Vector3 w)
            {
                //  normal 쪽 면: 가운데 = normal·h, 두 변 = u·h, w·h
                Vector3 c = Vector3.Scale(normal, h), du = Vector3.Scale(u, h), dw = Vector3.Scale(w, h);
                int i = v.Count;
                v.Add(c - du - dw); v.Add(c + du - dw); v.Add(c + du + dw); v.Add(c - du + dw);
                for (int k = 0; k < 4; k++) { n.Add(normal); }
                if (Vector3.Dot(Vector3.Cross(v[i + 1] - v[i], v[i + 2] - v[i]), normal) >= 0f) { t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 }); }
                else { t.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 }); }
            }
            Face(Vector3.up, Vector3.right, Vector3.forward);
            Face(Vector3.down, Vector3.right, Vector3.forward);
            Face(Vector3.right, Vector3.up, Vector3.forward);
            Face(Vector3.left, Vector3.up, Vector3.forward);
            Face(Vector3.forward, Vector3.right, Vector3.up);
            Face(Vector3.back, Vector3.right, Vector3.up);
            var mesh = new Mesh();
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetTriangles(t, 0);
            mesh.SetUVs(0, v.Select(p => new Vector2(p.x, p.z)).ToList());
            mesh.RecalculateBounds();
            return save ? SkydiveMapKit.SaveMesh(mesh, assetName) : mesh;
        }

        private static GameObject Box(Transform parent, string name, Material m, Vector3 center, Vector3 size, Quaternion rotation)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = rotation;
            go.transform.localScale = size;
            go.layer = LayerMask.NameToLayer("Default");
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        /// <summary>장애물 재질 — 윗면 격자 4m(메시 UV 기준, 판과 같이 돈다) + 옆면 줄눈.</summary>
        private static Material HazardMaterial(string name, string hex)
        {
            var m = SkydiveMapKit.Toon(name, hex, topGrid: 4f, sideGrid: 3f);
            m.SetFloat("_TopGridSpace", 1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material PadMaterial()
        {
            const string path = "Assets/Art/Materials/Pyramid/SpiralSavePad.mat";   // 피라미드 개정안과 같은 하늘색 발판
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }
    }
}
