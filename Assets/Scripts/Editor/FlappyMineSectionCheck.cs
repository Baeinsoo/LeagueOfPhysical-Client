using System.Collections.Generic;
using System.IO;
using System.Text;
using LOP.MapTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// 광산 맵 구간별 통과 검사(10-12). 열린 맵 씬의 콜라이더에 <b>게임의 진짜 이동 커널</b>(<see cref="KinematicMover"/>)을 대고,
    /// 구간마다 "한 번도 안 부딪히고 지나가는 날갯짓 순서가 있나"를 <see cref="SectionReach"/>로 찾는다.
    /// 고수 갈림길은 한 번 더 — 굴 칸에 갇혀 고수 굴을 지나가나.
    ///
    /// <para>빠르기 두 단계로 본다: 초당 4번(12틱, 사람 한계 근처)과 초당 3.3번(15틱, 이 맵의 기준 — 10-12 사용자 결정).
    /// 코스 전체 약 1분. 결과는 콘솔과 <c>Logs/mine-section-check.txt</c>에 남긴다(긴 에디터 작업은 콘솔이 사라질 수 있다).</para>
    /// </summary>
    public static class FlappyMineSectionCheck
    {
        private const string MapScenePath = "Assets/Art/Scenes/FlappyRaceMap.unity";
        private const string LogPath = "Logs/mine-section-check.txt";
        private const float TickSeconds = 0.02f;
        //  구간 시작보다 이만큼 앞에서 출발한다 — 구간 입구로 들어가는 높이도 고를 수 있게.
        private const float LeadIn = 4f;
        public static readonly int[] FlapTickLimits = { 12, 15 };

        [MenuItem("LOP/Debug/Flappy 광산 구간 통과 검사")]
        public static void RunMenu()
        {
            string report = Run(out _);
            Debug.Log(report);
        }

        /// <summary>검사를 돌려 보고서를 돌려준다. <paramref name="failures"/> = 막힌 (구간, 빠르기) 수. 맵 씬이 아니면 -1.</summary>
        public static string Run(out int failures)
        {
            failures = -1;
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != MapScenePath)
            {
                return $"[광산 구간 검사] 맵 씬({MapScenePath})을 먼저 열어라 — 지금 {scene.path}";
            }
            if (FlappyClassicCourseBuilder.TryReadConfig(out LOP.MasterData.FlappyConfig cfg) == false)
            {
                return "[광산 구간 검사] FlappyConfig를 못 읽었다.";
            }
            Physics.SyncTransforms();
            var course = MineCourseRule.Layout(new MinePhysics(cfg.ForwardSpeed, cfg.FlapImpulse, cfg.Gravity, cfg.MaxFallSpeed, TickSeconds));
            var query = new GameFramework.Physics.UnityCollisionQuery();
            int mask = LayerMask.GetMask("Default", FlappyHologram.LayerName);
            //  게임과 같은 몸: 지름보다 길면 누운 캡슐(FlappyBodyShape.IsLying), 아니면 공(세운 캡슐 높이 = 지름).
            float lying = cfg.BodyLength > cfg.BodyRadius * 2f ? cfg.BodyLength : 0f;
            float dx = cfg.ForwardSpeed * TickSeconds;

            (bool, float) Step(float x, float y, float vy)
            {
                var r = KinematicMover.Move(new KinematicMoveInput(new Vector3(x, y, 0f), new Vector3(cfg.ForwardSpeed, vy, 0f),
                    cfg.BodyRadius, cfg.BodyHeight, TickSeconds, mask, stepOffset: 0f, groundProbe: 0f, lyingLength: lying), query);
                //  안 닿았으면 커널은 정확히 속도만큼 옮긴다 — 어긋났으면 어딘가 닿아 멈추거나 미끄러진 것(게임은 그때 스턴).
                bool clean = Mathf.Abs(r.position.x - (x + dx)) < 1e-4f && Mathf.Abs(r.position.y - (y + vy * TickSeconds)) < 1e-4f;
                return (clean, r.position.y);
            }

            var sections = new List<(string name, float x)>();
            foreach (MineSection s in course.Sections) { sections.Add((s.Name, s.X)); }
            sections.Add(("결승", course.Length));

            var sb = new StringBuilder();
            sb.AppendLine($"[광산 구간 검사] 몸 {(lying > 0f ? $"누운 캡슐 {lying}" : $"공 지름 {cfg.BodyRadius * 2f}")} · 코스 {course.Length:F1} m");
            failures = 0;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i + 1 < sections.Count; i++)
            {
                float x0 = sections[i].x - LeadIn, x1 = sections[i + 1].x;
                MineTube tunnel = null;
                foreach (MineTube t in course.Tubes)
                {
                    if (float.IsNaN(t.Low) == false && t.X0 > x0 && t.X0 < x1 && (tunnel == null || t.Gap < tunnel.Gap)) { tunnel = t; }
                }
                foreach (bool forceTunnel in tunnel == null ? new[] { false } : new[] { false, true })
                {
                    var line = new StringBuilder($"{sections[i].name}{(forceTunnel ? " [고수 굴]" : "")} x {x0:F1}→{x1:F1}:");
                    foreach (int limit in FlapTickLimits)
                    {
                        System.Func<float, float, bool> keep = null;
                        if (forceTunnel)
                        {
                            MineTube t = tunnel;
                            keep = (x, y) => x <= t.X0 + 0.7f || x >= t.X1 || (y + cfg.BodyRadius >= t.Low && y + cfg.BodyRadius <= t.High);
                        }
                        SectionReachResult r = SectionReach.Run(x0, x1, StartHeights(course, x0, cfg.BodyRadius),
                            cfg.ForwardSpeed, cfg.FlapImpulse, cfg.Gravity, cfg.MaxFallSpeed, TickSeconds, limit, Step, keep);
                        float perSecond = 1f / (limit * TickSeconds);
                        line.Append(r.Passed ? $"  초당 {perSecond:F1}번 ✔" : $"  초당 {perSecond:F1}번 ✘(x≈{r.BlockedX:F1}에서 막힘)");
                        if (r.Passed == false) { failures++; }
                    }
                    sb.AppendLine(line.ToString());
                }
            }
            sb.AppendLine($"막힘 {failures}건 · {watch.ElapsedMilliseconds / 1000f:F0}초");
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.WriteAllText(LogPath, sb.ToString());
            return sb.ToString();
        }

        //  통로 안 발밑 높이를 5 cm마다 — 몸이 바닥·천장에서 떨어진 곳만.
        private static IEnumerable<float> StartHeights(MineCourse c, float x, float radius)
        {
            float lo = c.CenterAt(x) - c.HalfAt(x), hi = c.CenterAt(x) + c.HalfAt(x);
            for (float center = lo + radius + 0.01f; center < hi - radius - 0.01f; center += 0.05f)
            {
                yield return center - radius;
            }
        }
    }
}
