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
    /// 움직이는 장애물이 있는 구간은 출발 박자를 한 바퀴(근처 장애물 주기의 최소공배수)에 걸쳐 0.1초마다 훑는다(10-13).
    /// 코스 전체 약 33분(10-13, 출발 박자 훑기 포함) — 고칠 구간만 <c>onlySection</c>으로 돌려라. 결과는 콘솔과 <c>Logs/mine-section-check.txt</c>에 남긴다(긴 에디터 작업은 콘솔이 사라질 수 있다).</para>
    /// </summary>
    public static class FlappyMineSectionCheck
    {
        private const string MapScenePath = "Assets/Art/Scenes/FlappyRaceMap.unity";
        private const string LogPath = "Logs/mine-section-check.txt";
        private const float TickSeconds = 0.02f;
        //  구간 시작보다 이만큼 앞에서 출발한다 — 구간 입구로 들어가는 높이도 고를 수 있게.
        private const float LeadIn = 4f;
        public static readonly int[] FlapTickLimits = { 12, 15 };
        private const int PhaseStepTicks = 5;   // 출발 박자를 0.1초마다 훑는다(spec §4)
        private const int ReferenceLimit = 15;   // 합격선 — 초당 3.3번
        private const float MaxCycleSeconds = 30f;   // 출발 박자 한 바퀴 상한 — 넘으면 돌리지 않고 알린다

        [MenuItem("LOP/Debug/Flappy 광산 구간 통과 검사")]
        public static void RunMenu()
        {
            string report = Run(out _);
            Debug.Log(report);
        }

        /// <summary>검사를 돌려 보고서를 돌려준다. <paramref name="failures"/> = 막힌 (구간, 빠르기) 수. 맵 씬이 아니면 -1.
        /// <paramref name="onlySection"/>을 주면 그 이름의 구간만 본다(빠른 반복용).</summary>
        public static string Run(out int failures, string onlySection = null)
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
            var movers = FlappySceneMovers.Collect();
            //  코스 규칙과 구운 씬이 다르면(코드만 바꾸고 안 구웠다) 검사가 엉뚱한 장애물을 본다.
            if (movers.Lifts != course.Lifts.Count || movers.Pendulums != course.Pendulums.Count || movers.Rockfalls != course.Rockfalls.Count)
            {
                //  로그도 남긴다 — 로그를 기다리는 쪽(--detach 폴링)이 끝없이 기다리지 않게.
                return WriteLog($"[광산 구간 검사] 씬의 움직이는 장애물(승강기 {movers.Lifts}·광차 {movers.Pendulums}·낙석 {movers.Rockfalls})이 "
                     + $"코스({course.Lifts.Count}·{course.Pendulums.Count}·{course.Rockfalls.Count})와 다르다 — 맵을 다시 구워라.\n");
            }
            var sections = new List<(string name, float x)>();
            foreach (MineSection s in course.Sections) { sections.Add((s.Name, s.X)); }
            sections.Add(("결승", course.Length));
            //  구간마다 훑을 한 바퀴(근처 장애물 주기의 최소공배수) — 너무 길면 돌리기 전에 멈춘다(10-13 리뷰 판정: 30초 넘으면 사람이 정한다).
            var cycles = new int[sections.Count];
            var tooLong = new StringBuilder();
            for (int i = 0; i + 1 < sections.Count; i++)
            {
                cycles[i] = movers.CycleTicksNear(sections[i].x - LeadIn, sections[i + 1].x, TickSeconds);
                if (onlySection != null && sections[i].name != onlySection) { continue; }
                if (cycles[i] * TickSeconds > MaxCycleSeconds) { tooLong.AppendLine($"  {sections[i].name} x {sections[i].x:F1}: {cycles[i] * TickSeconds:F1}초"); }
            }
            if (tooLong.Length > 0)
            {
                return WriteLog($"[광산 구간 검사] 출발 박자 한 바퀴가 {MaxCycleSeconds:F0}초를 넘는 구간이 있어 돌리지 않았다:\n{tooLong}");
            }
            var sb = new StringBuilder();
            try
            {
                var query = new GameFramework.Physics.UnityCollisionQuery();
                int mask = LayerMask.GetMask("Default", FlappyHologram.LayerName);
                //  게임과 같은 몸: 지름보다 길면 누운 캡슐(FlappyBodyShape.IsLying), 아니면 공(세운 캡슐 높이 = 지름).
                float lying = cfg.BodyLength > cfg.BodyRadius * 2f ? cfg.BodyLength : 0f;
                float dx = cfg.ForwardSpeed * TickSeconds;

                (bool, float) Step(float x, float y, float vy)
                {
                    var foot = new Vector3(x, y, 0f);
                    //  게임 순서(FlappyWorld.Mutation): 이 틱 자세(beginTick이 세웠다) → 장애물이 쳤나 → 이동.
                    if (movers.Count > 0 && LOP.FlappyMoverOverlap.StruckBy(foot, cfg.BodyRadius, cfg.BodyHeight, mask, lying))
                    {
                        return (false, y);
                    }
                    var r = KinematicMover.Move(new KinematicMoveInput(foot, new Vector3(cfg.ForwardSpeed, vy, 0f),
                        cfg.BodyRadius, cfg.BodyHeight, TickSeconds, mask, stepOffset: 0f, groundProbe: 0f, lyingLength: lying), query);
                    //  안 닿았으면 커널은 정확히 속도만큼 옮긴다 — 어긋났으면 어딘가 닿아 멈추거나 미끄러진 것(게임은 그때 스턴).
                    bool clean = Mathf.Abs(r.position.x - (x + dx)) < 1e-4f && Mathf.Abs(r.position.y - (y + vy * TickSeconds)) < 1e-4f;
                    return (clean, r.position.y);
                }

                sb.AppendLine($"[광산 구간 검사] 몸 {(lying > 0f ? $"누운 캡슐 {lying}" : $"공 지름 {cfg.BodyRadius * 2f}")} · 코스 {course.Length:F1} m"
                              + $" · 움직이는 장애물 승강기 {movers.Lifts}·광차 {movers.Pendulums}·낙석 {movers.Rockfalls}");
                failures = 0;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i + 1 < sections.Count; i++)
                {
                    if (onlySection != null && sections[i].name != onlySection) { continue; }
                    float x0 = sections[i].x - LeadIn, x1 = sections[i + 1].x;
                    int cycle = cycles[i];
                    var starts = new List<long> { 0 };
                    for (long t = PhaseStepTicks; t < cycle; t += PhaseStepTicks) { starts.Add(t); }
                    //  근처에 장애물이 없는 구간은 구운 자세로 — 앞 구간이 세운 자세가 남아 있으면 엉뚱한 자리에 서 있다.
                    if (cycle == 0) { movers.Restore(); }
                    System.Action<long> pose = cycle > 0 ? t => movers.PoseForTick(t, TickSeconds) : (System.Action<long>)null;
                    MineTube tunnel = null;
                    foreach (MineTube t in course.Tubes)
                    {
                        if (float.IsNaN(t.Low) == false && t.X0 > x0 && t.X0 < x1 && (tunnel == null || t.Gap < tunnel.Gap)) { tunnel = t; }
                    }
                    foreach (bool forceTunnel in tunnel == null ? new[] { false } : new[] { false, true })
                    {
                        var line = new StringBuilder($"{sections[i].name}{(forceTunnel ? " [고수 굴]" : "")} x {x0:F1}→{x1:F1}"
                                                     + (cycle > 0 ? $" (출발 박자 {starts.Count}개, 한 바퀴 {cycle * TickSeconds:F1}초)" : "") + ":");
                        foreach (int limit in FlapTickLimits)
                        {
                            System.Func<float, float, bool> keep = null;
                            if (forceTunnel)
                            {
                                MineTube t = tunnel;
                                keep = (x, y) => x <= t.X0 + 0.7f || x >= t.X1 || (y + cfg.BodyRadius >= t.Low && y + cfg.BodyRadius <= t.High);
                            }
                            //  합격선(초당 3.3번)은 모든 출발 박자를, 초당 4번은 0틱 한 번만(진단).
                            var ticks = limit == ReferenceLimit ? starts : new List<long> { 0 };
                            int blocked = 0;
                            float firstBlockX = float.MaxValue;
                            long firstBlockStart = 0;
                            foreach (long t0 in ticks)
                            {
                                SectionReachResult r = SectionReach.Run(x0, x1, StartHeights(course, x0, cfg.BodyRadius),
                                    cfg.ForwardSpeed, cfg.FlapImpulse, cfg.Gravity, cfg.MaxFallSpeed, TickSeconds, limit, Step, keep,
                                    beginTick: pose, startTick: t0);
                                if (r.Passed) { continue; }
                                blocked++;
                                if (r.BlockedX < firstBlockX) { firstBlockX = r.BlockedX; firstBlockStart = t0; }
                            }
                            float perSecond = 1f / (limit * TickSeconds);
                            line.Append(blocked == 0
                                ? $"  초당 {perSecond:F1}번 ✔"
                                : $"  초당 {perSecond:F1}번 ✘ 출발 박자 {blocked}/{ticks.Count} 막힘(가장 이른 x≈{firstBlockX:F1}, 출발 {firstBlockStart * TickSeconds:F1}초)");
                            if (blocked > 0) { failures++; }
                        }
                        sb.AppendLine(line.ToString());
                    }
                }
                sb.AppendLine($"막힘 {failures}건 · {watch.ElapsedMilliseconds / 1000f:F0}초");
            }
            finally
            {
                movers.Restore();
            }
            //  자세를 돌려놓은 뒤에 적는다. 스크립트로 바꾼 자세는 isDirty를 안 켜므로 기억한 자세와 하나하나 맞대 본다.
            int leaked = movers.PoseMismatches();
            sb.AppendLine(leaked == 0 ? "자세 복원 ✔" : $"자세 복원 ✘ 구운 자세와 다른 Transform {leaked}개 — 씬에 검사 자세가 남았다");
            if (leaked > 0) { failures++; }
            sb.AppendLine($"씬 더티: {scene.isDirty}");
            return WriteLog(sb.ToString());
        }

        private static string WriteLog(string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.WriteAllText(LogPath, text);
            return text;
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
