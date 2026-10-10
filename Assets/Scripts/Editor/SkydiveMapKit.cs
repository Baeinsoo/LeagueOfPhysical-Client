using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// 스카이다이브 맵을 굽는 도우미 모음 — 판 조각내기, 레이저·문·바람·체크포인트 표식 만들기, 툰 재질·메시 저장.
    /// 옛 더미 코스(SkydiveCourseBuilder)와 피라미드 꾸밈에서 원통 맵이 쓰는 것만 옮겨 왔다(10-07, 옛 맵 넷 정리).
    /// </summary>
    internal static class SkydiveMapKit
    {
        private const float SlabThickness = 3f;

        // 문 패널 두께. 선반 두께에서 위아래로 조금씩 물려 둔다 — 판과 면이 정확히 겹치면
        // 물러난 패널이 판 표면과 같은 평면에 놓여 바닥이 깜빡인다(z-파이팅).
        private const float PanelRecess = 0.1f;

        private const float PanelThickness = SlabThickness - 2f * PanelRecess;

        // 화살표 밀도. 개수는 부피(반지름×높이)에 비례한다 — 세기가 아니라 "큰 볼륨에서
        // 성기지 않게"를 위한 값이다. 반지름25×높이120(작은 기둥)에서 14개가 나오게 골랐다.
        private const float ArrowCountDivisor = 250f;

        private const int ArrowCountMin = 14;

        private const int ArrowCountMax = 200;

        // 선반 하나의 구멍 하나. HasDoor=true는 "가까운 구멍"(문이 여닫혀 다이브로 달려들어야
        // 타이밍이 맞는다), false는 "먼 구멍"(문이 없어 항상 열려 있지만 대자로만 닿는다) —
        // 스펙 §1·§3.1(2026-09-06-skydive-doors-and-branching-design).
        internal readonly struct Hole
        {
            public readonly float X;
            public readonly float Z;
            public readonly float Half;
            public readonly bool HasDoor;

            public Hole(float x, float z, float side, bool hasDoor)
            {
                X = x;
                Z = z;
                Half = side * 0.5f;
                HasDoor = hasDoor;
            }
        }

        internal readonly struct WindSpec
        {
            public readonly string Name;
            public readonly Vector3 Center;
            public readonly float Radius;
            public readonly float Height;
            public readonly Vector3 Wind;

            public WindSpec(string name, Vector3 center, float radius, float height, Vector3 wind)
            {
                Name = name;
                Center = center;
                Radius = radius;
                Height = height;
                Wind = wind;
            }
        }

        internal readonly struct LaserSpec
        {
            public readonly string Name;
            public readonly Vector3 Pivot;
            public readonly float Length;
            public readonly float Radius;
            public readonly float StartAngleDegrees;
            public readonly float AngularSpeedDegreesPerTick;
            public readonly float SweepHalfRangeDegrees;
            public readonly int Period;
            public readonly int OnTicks;
            public readonly int Phase;

            public LaserSpec(string name, Vector3 pivot, float length, float radius,
                             float startAngleDegrees, float angularSpeedDegreesPerTick,
                             float sweepHalfRangeDegrees, int period, int onTicks, int phase)
            {
                Name = name;
                Pivot = pivot;
                Length = length;
                Radius = radius;
                StartAngleDegrees = startAngleDegrees;
                AngularSpeedDegreesPerTick = angularSpeedDegreesPerTick;
                SweepHalfRangeDegrees = sweepHalfRangeDegrees;
                Period = period;
                OnTicks = onTicks;
                Phase = phase;
            }

            public LOP.Laser ToLaser() => new LOP.Laser(
                new System.Numerics.Vector3(Pivot.x, Pivot.y, Pivot.z),
                Length, Radius,
                StartAngleDegrees * Mathf.Deg2Rad,
                AngularSpeedDegreesPerTick * Mathf.Deg2Rad,
                SweepHalfRangeDegrees * Mathf.Deg2Rad,
                Period, OnTicks, Phase);
        }

        // 한 틱에 이보다 크게 돌면 다음 자리를 눈으로 예측할 수 없다 — 피할 수 없는 것은
        // 장애물이 아니라 주사위다.
        private const float MaxAngularSpeedDegreesPerTick = 15f;

        internal readonly struct DoorSpec
        {
            public readonly string Name;

            /// <summary>구멍 중심. 문 허브가 놓이는 자리다.</summary>
            public readonly Vector3 Center;

            /// <summary>덮는 폭의 절반(=구멍 반폭). 패널 하나는 이 값의 절반 길이다.</summary>
            public readonly float HalfWidth;

            /// <summary>미끄러지는 방향과 직교하는 쪽 절반.</summary>
            public readonly float HalfDepth;

            /// <summary>패널이 미끄러지는 방향(XZ 평면 각, 도).</summary>
            public readonly float AxisAngleDegrees;

            public readonly int Period;
            public readonly int OpenTicks;
            public readonly int MoveTicks;
            public readonly int Phase;

            /// <summary>위에 선 사람을 패널과 같이 옮길지(DoorVolume.Rideable). 기본 꺼짐 — 열리면 떨어지는 관문.</summary>
            public readonly bool Rideable;

            public DoorSpec(string name, Vector3 center, float halfWidth, float halfDepth,
                            float axisAngleDegrees, int period, int openTicks, int moveTicks, int phase, bool rideable = false)
            {
                Rideable = rideable;
                Name = name;
                Center = center;
                HalfWidth = halfWidth;
                HalfDepth = halfDepth;
                AxisAngleDegrees = axisAngleDegrees;
                Period = period;
                OpenTicks = openTicks;
                MoveTicks = moveTicks;
                Phase = phase;
            }

            /// <summary>닫혀 있는 틱 수. 나머지 셋에서 나온다.</summary>
            public int ClosedTicks => Period - OpenTicks - 2 * MoveTicks;

            public LOP.Door ToDoor() => new LOP.Door(
                new System.Numerics.Vector3(Center.x, Center.y, Center.z),
                HalfWidth, HalfDepth, PanelThickness,
                AxisAngleDegrees * Mathf.Deg2Rad,
                Period, OpenTicks, MoveTicks, Phase);
        }

        // 판의 사각형 한 조각(XZ 평면). Name은 이 조각이 어느 구멍의 어느 쪽에서 떨어져 나왔는지
        // (N/S/E/W를 이어 붙인 것)라서, 씬 diff나 디버깅에서 어느 조각인지 바로 읽힌다.
        internal readonly struct Plate
        {
            public readonly string Name;
            public readonly float XMin, XMax, ZMin, ZMax;

            public Plate(string name, float xMin, float xMax, float zMin, float zMax)
            {
                Name = name;
                XMin = xMin;
                XMax = xMax;
                ZMin = zMin;
                ZMax = zMax;
            }

            public float Width => XMax - XMin;
            public float Depth => ZMax - ZMin;
            public float Area => Width * Depth;
        }

        /// <summary>
        /// 판에서 구멍들을 도려내고 남은 사각형 조각들을 준다. 하나의 큰 판에 구멍을 뚫을 수는
        /// 없어서(상자 콜라이더는 볼록한 덩어리뿐) 조각으로 쪼갠다 — 구멍이 하나면 북/남/동/서
        /// 네 조각이 나오고, 구멍이 더 있으면 그때까지 남은 조각들에 같은 식을 반복한다.
        /// GameObject를 만들지 않는 순수 함수라 Unity 없이 검사할 수 있다.
        /// </summary>
        internal static List<Plate> Carve(in Plate plate, IReadOnlyList<Hole> holes)
        {
            var plates = new List<Plate> { plate };

            for (int h = 0; h < holes.Count; h++)
            {
                Hole hole = holes[h];
                float holeXMin = hole.X - hole.Half;
                float holeXMax = hole.X + hole.Half;
                float holeZMin = hole.Z - hole.Half;
                float holeZMax = hole.Z + hole.Half;

                var next = new List<Plate>();
                foreach (Plate piece in plates)
                {
                    //  이 구멍과 이 조각이 겹치는 부분만 도려낸다 — 조각이 이미 이전 구멍으로
                    //  좁아져 있을 수 있으므로 구멍 범위를 조각 범위로 한 번 더 자른다.
                    float xLo = Mathf.Max(piece.XMin, holeXMin);
                    float xHi = Mathf.Min(piece.XMax, holeXMax);
                    float zLo = Mathf.Max(piece.ZMin, holeZMin);
                    float zHi = Mathf.Min(piece.ZMax, holeZMax);

                    if (xLo >= xHi || zLo >= zHi)
                    {
                        next.Add(piece);   // 안 겹치는 조각은 그대로 둔다
                        continue;
                    }

                    string stem = piece.Name.Length == 0 ? string.Empty : piece.Name + "_";
                    if (piece.ZMax > zHi)
                    {
                        next.Add(new Plate(stem + "N", piece.XMin, piece.XMax, zHi, piece.ZMax));
                    }
                    if (zLo > piece.ZMin)
                    {
                        next.Add(new Plate(stem + "S", piece.XMin, piece.XMax, piece.ZMin, zLo));
                    }
                    if (piece.XMax > xHi)
                    {
                        next.Add(new Plate(stem + "E", xHi, piece.XMax, zLo, zHi));
                    }
                    if (xLo > piece.XMin)
                    {
                        next.Add(new Plate(stem + "W", piece.XMin, xLo, zLo, zHi));
                    }
                }
                plates = next;
            }

            return plates;
        }

        internal static GameObject CreateWindVolume(Transform parent, string name, Vector3 center,
                                                    float radius, float height, Vector3 wind,
                                                    WindVisualAssets assets)
        {
            var go = new GameObject(name);
            if (parent != null)
            {
                go.transform.SetParent(parent, worldPositionStays: false);
            }
            go.transform.localPosition = center;

            var marker = go.AddComponent<LOP.WindVolume>();
            marker.Radius = radius;
            marker.Height = height;
            marker.Wind = wind;

            float speed = wind.magnitude;
            if (assets == null || assets.IsComplete == false || speed <= 0.001f)
            {
                return go;
            }

            var arrows = new GameObject("Arrows");
            arrows.transform.SetParent(go.transform, worldPositionStays: false);
            CreateWindArrows(arrows.transform, name, radius, height, wind, speed, assets);

            // 범위 표시는 한 부모 아래에 통째로 모은다 — 나중에 옵션으로 끌 때 이 하나만 끄면
            // 화살표와 흐름만 남는다.
            var bounds = new GameObject("Bounds");
            bounds.transform.SetParent(go.transform, worldPositionStays: false);
            CreateWindBounds(bounds.transform, radius, height, speed, assets);

            var visualizer = go.AddComponent<LOP.WindVolumeVisualizer>();
            visualizer.ArrowsRoot = arrows.transform;
            visualizer.BoundsRoot = bounds;

            return go;
        }

        // 레이저는 그리지 않는다(뷰는 별도 작업) — 여기서는 판정 마커만 굽는다.
        internal static GameObject CreateLaserVolume(Transform parent, in LaserSpec spec)
        {
            var go = new GameObject(spec.Name);
            if (parent != null)
            {
                go.transform.SetParent(parent, worldPositionStays: false);
            }
            go.transform.localPosition = spec.Pivot;

            var marker = go.AddComponent<LOP.LaserVolume>();
            marker.Length = spec.Length;
            marker.Radius = spec.Radius;
            marker.StartAngleDegrees = spec.StartAngleDegrees;
            marker.AngularSpeedDegreesPerTick = spec.AngularSpeedDegreesPerTick;
            marker.SweepHalfRangeDegrees = spec.SweepHalfRangeDegrees;
            marker.Period = spec.Period;
            marker.OnTicks = spec.OnTicks;
            marker.Phase = spec.Phase;
            return go;
        }

        /// <summary>
        /// 문 하나 — 허브(<see cref="LOP.DoorVolume"/>) 밑에 패널 둘.
        ///
        /// <para><b>레이저와 달리 콜라이더를 남긴다.</b> 문은 닫히는 동안 사람을 밀어내는 <b>벽</b>이라,
        /// 레이저 마커처럼 콜라이더를 지우면 밀려나지도 않고 그냥 통과해 버린다.</para>
        ///
        /// <para>허브에는 회전을 넣지 않는다 — 패널 오프셋이 이미 <c>AxisAngle</c>로 월드 축 기준
        /// 방향을 잡으므로 부모가 또 돌면 자식 로컬 좌표가 두 번 꺾인다.</para>
        /// </summary>
        //  체크포인트 표식 — 스폰 고도에도 하나 둔다. 빠지면 맨 위 선반이 스폰이 되어,
        //  그 위에서 죽은 사람이 아래 선반으로 순간이동한다(이득).
        internal static GameObject CreateCheckpointMarkers(Transform parent, float spawnY,
                                                           IReadOnlyDictionary<float, Vector3> respawnPoints)
        {
            var root = new GameObject("Checkpoints");
            if (parent != null)
            {
                root.transform.SetParent(parent, worldPositionStays: false);
            }
            Add(root.transform, $"Checkpoint_{spawnY:0}", new Vector3(0f, spawnY, 0f));
            foreach (var pair in respawnPoints)
            {
                Add(root.transform, $"Checkpoint_{pair.Key:0}", pair.Value);
            }
            return root;

            static void Add(Transform parent, string name, Vector3 position)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, worldPositionStays: false);
                go.transform.localPosition = position;
                go.AddComponent<LOP.CheckpointMarker>();
            }
        }

        internal static GameObject CreateDoorVolume(Transform parent, in DoorSpec spec, Material material)
        {
            var go = new GameObject(spec.Name);
            if (parent != null)
            {
                go.transform.SetParent(parent, worldPositionStays: false);
            }
            go.transform.localPosition = spec.Center;
            go.transform.localRotation = Quaternion.identity;

            var marker = go.AddComponent<LOP.DoorVolume>();
            marker.HalfWidth = spec.HalfWidth;
            marker.HalfDepth = spec.HalfDepth;
            marker.Thickness = PanelThickness;
            marker.AxisAngleDegrees = spec.AxisAngleDegrees;
            marker.Period = spec.Period;
            marker.OpenTicks = spec.OpenTicks;
            marker.MoveTicks = spec.MoveTicks;
            marker.Phase = spec.Phase;
            marker.Rideable = spec.Rideable;

            marker.PanelA = CreateDoorPanel(go.transform, spec.Name + "_A", spec, material);
            marker.PanelB = CreateDoorPanel(go.transform, spec.Name + "_B", spec, material);

            //  씬에 저장되는 자세 하나는 있어야 한다. 런타임에는 매 틱 다시 잡힌다.
            marker.Pose(0d);
            return go;
        }

        private static Transform CreateDoorPanel(Transform parent, string name,
                                                 in DoorSpec spec, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, worldPositionStays: false);
            box.transform.localRotation = LOP.DoorVolume.PanelRotation(spec.AxisAngleDegrees);
            //  판정 상자(DoorGeometry)의 반치수는 (HalfWidth/2, Thickness/2, HalfDepth)다 —
            //  크기는 그 두 배. 여기가 어긋나면 보이는 도형과 죽이는 도형이 갈린다.
            box.transform.localScale = new Vector3(spec.HalfWidth, PanelThickness, spec.HalfDepth * 2f);
            box.layer = LayerMask.NameToLayer("Default");   // sweep 마스크가 보는 레이어
            if (material != null)
            {
                box.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
            return box.transform;
        }

        private static void CreateWindArrows(Transform parent, string name, float radius, float height,
                                             Vector3 wind, float speed, WindVisualAssets assets)
        {
            int count = Mathf.Clamp(Mathf.RoundToInt(radius * height / ArrowCountDivisor),
                                    ArrowCountMin, ArrowCountMax);
            Material material = assets.ArrowFor(speed);
            Quaternion rotation = Quaternion.LookRotation(wind / speed);

            for (int k = 0; k < count; k++)
            {
                // 황금각 나선 — 난수 없이 고르게 흩어진다. 다시 구워도 같은 자리에 나온다.
                float t = (k + 0.5f) / count;
                float angle = k * 2.39996f;
                float r = radius * Mathf.Sqrt(t);

                var arrow = new GameObject($"{name}_Arrow{k}");
                arrow.transform.SetParent(parent, worldPositionStays: false);
                arrow.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * r, (t - 0.5f) * height, Mathf.Sin(angle) * r);
                arrow.transform.localRotation = rotation;
                // 길이 = 바람이 1초에 미는 거리. 세기가 곧 화살표 크기라 범례가 필요 없다.
                arrow.transform.localScale = Vector3.one * speed;

                arrow.AddComponent<MeshFilter>().sharedMesh = assets.Arrow;
                arrow.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
        }

        private static void CreateWindBounds(Transform parent, float radius, float height,
                                             float speed, WindVisualAssets assets)
        {
            var shell = new GameObject("Shell");
            shell.transform.SetParent(parent, worldPositionStays: false);
            // 원본은 반지름 0.5·높이 1이라 지름과 높이를 그대로 스케일로 준다.
            shell.transform.localScale = new Vector3(radius * 2f, height, radius * 2f);

            shell.AddComponent<MeshFilter>().sharedMesh = assets.Shell;
            shell.AddComponent<MeshRenderer>().sharedMaterial = assets.ShellFor(speed);
        }

        // 구멍을 얼마나 촘촘히 훑을지. 한 변을 이만큼 나눈다.
        private const int GateGridSteps = 12;

        // 몇 틱까지 봐야 "언젠가 열린다"를 말할 수 있나. 표의 가장 긴 주기보다 넉넉히 크게.
        private const int GateSampleTicks = 240;

        // 통과하려면 몸이 들어갈 자리가 있어야 한다.
        // internal — TbSkydiveConfig와 값이 같은지 EditMode 테스트가 대조한다(SkydiveWindLagConsistencyTests).
        internal const float BodyRadiusForGateCheck = 0.4f;

        internal static bool GateEverOpens(float shelfY, in Hole hole, float upperY, IReadOnlyList<LaserSpec> lasers)
        {
            var beams = new List<LOP.Laser>();
            for (int i = 0; i < lasers.Count; i++)
            {
                // 이 구간(선반~바로 위 선반) 안에 피벗이 있는 레이저만 이 구멍의 문지기다 —
                // 다른 구간의 빔은 여기까지 닿지 않으니 막는지 안 막는지와 무관하다.
                float pivotY = lasers[i].Pivot.y;
                if (pivotY <= shelfY || pivotY > upperY)
                {
                    continue;
                }
                beams.Add(lasers[i].ToLaser());
            }

            for (int tick = 0; tick < GateSampleTicks; tick++)
            {
                ScanHole(hole, beams, tick, out bool anyClear, out _);
                if (anyClear)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 구멍 안을 격자로 훑어, 이 틱에 빔에 <b>닿는 점</b>과 <b>안 닿는 점</b>이 각각 있는지 본다.
        /// 문지기 검사는 "안 닿는 점이 하나라도 있나"(=언젠가 열린다), 안전한 구멍 검사는 "닿는 점이
        /// 하나라도 있나"(=쓸린다)를 묻는다 — 두 물음이 같은 이 한 벌을 쓴다.
        /// </summary>
        private static void ScanHole(in Hole hole, List<LOP.Laser> beams, int tick,
                                     out bool anyClear, out bool anyLit)
        {
            anyClear = false;
            anyLit = false;
            float step = hole.Half * 2f / GateGridSteps;

            for (int ix = 0; ix <= GateGridSteps; ix++)
            {
                for (int iz = 0; iz <= GateGridSteps; iz++)
                {
                    float x = hole.X - hole.Half + ix * step;
                    float z = hole.Z - hole.Half + iz * step;

                    bool lit = false;
                    for (int b = 0; b < beams.Count; b++)
                    {
                        LOP.Laser beam = beams[b];
                        if (LOP.LaserGeometry.Lit(beam, tick) == false)
                        {
                            continue;
                        }
                        LOP.LaserGeometry.SegmentAt(beam, tick, out var a, out var bb);
                        //  빔은 수평이고 낙하는 수직이라, 구멍의 기둥이 막혔는지는 XZ 평면에서
                        //  정해진다. Y를 지우고 재면 3D 루틴을 그대로 다시 쓸 수 있다.
                        var flatPoint = new System.Numerics.Vector3(x, 0f, z);
                        var flatA = new System.Numerics.Vector3(a.X, 0f, a.Z);
                        var flatB = new System.Numerics.Vector3(bb.X, 0f, bb.Z);
                        float d = LOP.LaserSweep.SegmentDistance(flatPoint, flatPoint, flatA, flatB);
                        if (d <= BodyRadiusForGateCheck + beam.Radius)
                        {
                            lit = true;
                            break;
                        }
                    }

                    if (lit)
                    {
                        anyLit = true;
                    }
                    else
                    {
                        anyClear = true;
                    }
                }
            }
        }

        /// <summary>한 틱에 너무 크게 도는 레이저가 있으면 그 설명을, 없으면 null을 준다.</summary>
        internal static string FindTooFastLaser(IReadOnlyList<LaserSpec> lasers)
        {
            for (int i = 0; i < lasers.Count; i++)
            {
                float speed = Mathf.Abs(lasers[i].AngularSpeedDegreesPerTick);
                if (speed > MaxAngularSpeedDegreesPerTick)
                {
                    return $"{lasers[i].Name}가 한 틱에 {speed:0.#}° 돈다 — " +
                           $"{MaxAngularSpeedDegreesPerTick:0.#}°를 넘으면 눈으로 못 읽는다";
                }
            }
            return null;
        }

        private const string MaterialDir = "Assets/Art/Materials/Pyramid";

        private const string MeshDir = "Assets/Art/Models/Pyramid";

        public static Material Stone => Toon("Stone", "#D9B48A", topGrid: 8f, sideGrid: 4f);

        public static Material StoneDark => Toon("StoneDark", "#A9825A", sideGrid: 4f);

        public static Material Jungle => Toon("Jungle", "#5FA35A");

        //  메시 에셋 덮어쓰기 — 지우고 새로 만들면 GUID가 바뀌어 씬 참조·원격 에셋이 깨진다.
        internal static Mesh SaveMesh(Mesh mesh, string name)
        {
            Directory.CreateDirectory(MeshDir);
            string path = $"{MeshDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                mesh.name = name;
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            existing.Clear();
            existing.indexFormat = mesh.indexFormat;
            existing.vertices = mesh.vertices;
            existing.normals = mesh.normals;
            existing.uv = mesh.uv;
            existing.colors = mesh.colors;
            existing.triangles = mesh.triangles;
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        internal static Material Toon(string name, string hex, float topGrid = 0f, float sideGrid = 0f)
        {
            Directory.CreateDirectory(MaterialDir);
            string path = $"{MaterialDir}/Pyramid{name}.mat";
            ColorUtility.TryParseHtmlString(hex, out var color);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("LOP/Toon"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.SetFloat("_TopGrid", topGrid);
            m.SetFloat("_SideGrid", sideGrid);
            m.SetShaderPassEnabled("SRPDefaultUnlit", false);   // 외곽선은 캐릭터만
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
