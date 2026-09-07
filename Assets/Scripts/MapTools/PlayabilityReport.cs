using System.Collections.Generic;
using System.Text;

namespace LOP.MapTools
{
    /// <summary>스폰 한 자리의 클린런 결과. <see cref="VerifiedByReplay"/>는 진짜 커널로 재생해 확인했는가.</summary>
    public readonly struct SpawnCleanRun
    {
        public readonly string Name;
        public readonly float Y;
        public readonly CleanRunResult Result;
        public readonly bool VerifiedByReplay;

        public SpawnCleanRun(string name, float y, CleanRunResult result, bool verifiedByReplay)
        {
            Name = name;
            Y = y;
            Result = result;
            VerifiedByReplay = verifiedByReplay;
        }
    }

    public static class PlayabilityReport
    {
        public static string Build(string mapName, float startX, float finishX, in FlappyConfig config,
                                   IReadOnlyList<SpawnCleanRun> cleanRuns, string trapSection,
                                   IReadOnlyList<StunBudgetPoint> budget, EarliestCatch earliest,
                                   float heightGrid, float minY, float maxY)
        {
            var text = new StringBuilder();
            float cleanRunSeconds = (finishX - startX) / config.ForwardSpeed;

            text.AppendLine("════ Flappy 맵 검사 ════");
            text.AppendLine($"맵: {mapName}        코스 x {startX:F0} → {finishX:F0}"
                          + $" ({finishX - startX:F0}m)   클린런 {cleanRunSeconds:F1}초");
            text.AppendLine($"물리: 전진 {config.ForwardSpeed:F0}  날갯짓 {config.FlapImpulse:F0}"
                          + $"  중력 {config.Gravity:F0}  최대낙하 {config.MaxFallSpeed:F0}"
                          + $"  몸 r{config.BodyRadius:F2} h{config.BodyHeight:F2}  높이눈금 {heightGrid:F2}");
            text.AppendLine($"추격자: 시작 {config.ChaserStartX:F0}  초기 {config.ChaserInitialSpeed:F0}"
                          + $"  가속 {config.ChaserAcceleration}  상한 {config.ChaserMaxSpeed:F0}"
                          + $"      스턴 {config.StunTime} + 무적 {config.InvulnTime}");
            //  이 y대역이 ①(클린런)·②(낌 지점) 둘 다의 탐색 상/하한이다(Default 콜라이더
            //  최고~최저점). ①만 읽는 사람도 봐야 한다 — 천장 없는 맵이면 이 위로 날아 넘는,
            //  실제로는 되는 경로를 탐색이 못 보고도 ❌를 찍을 수 있어서다.
            text.AppendLine($"탐색 대역 y[{minY:F1}~{maxY:F1}] (천장 없으면 그 위 경로는 검색 밖)");
            text.AppendLine();

            text.AppendLine("── ① 클린런 (자리별) ──────────────────");
            bool anyFail = false, anyProven = false, anyUnproven = false;
            for (int i = 0; i < cleanRuns.Count; i++)
            {
                SpawnCleanRun run = cleanRuns[i];
                if (run.Result.Reachable)
                {
                    if (run.VerifiedByReplay)
                    {
                        anyProven = true;
                        text.AppendLine($"  {run.Name} (y={run.Y:F0})   ✅  날갯짓 {CountFlaps(run.Result)}회");
                    }
                    else
                    {
                        //  ✅와 같은 글자를 쓰면 "재생으로 증명됨"과 "탐색만 찾았고 증명 못 함"이
                        //  구분 안 된다 — spec §3.7이 재생을 증명으로 정의하므로, 증명 안 된
                        //  성공은 ✅도 ❌도 아닌 제 글자(🟡)를 가져야 한다.
                        anyUnproven = true;
                        text.AppendLine($"  {run.Name} (y={run.Y:F0})   🟡  날갯짓 {CountFlaps(run.Result)}회"
                                      + "   ⚠️ 탐색은 찾았으나 재생이 어긋남");
                    }
                }
                else
                {
                    anyFail = true;
                    text.AppendLine($"  {run.Name} (y={run.Y:F0})   ❌  x={run.Result.BlockedX:F1}에서 막힘");
                    //  R11 — NarrowestCount == 0은 "0폭 회랑을 쟀다"가 아니라 "출발 직후
                    //  과도기(탐색 폭이 늘기를 멈추기 전)에 막혀 회랑 자체를 측정 못 했다"는
                    //  뜻이다. 그대로 숫자를 찍으면 사람이 없는 병목 좌표를 고치러 간다.
                    if (run.Result.NarrowestCount == 0)
                    {
                        text.AppendLine("                             최협 회랑: 측정 안 됨"
                                      + " — 탐색 폭이 늘어나길 멈추기 전에 막혔다");
                    }
                    else
                    {
                        text.AppendLine($"                             최협 회랑 x={run.Result.NarrowestX:F1}"
                                      + $"  생존 {run.Result.NarrowestCount}"
                                      + $"  높이 폭 {run.Result.NarrowestHeightSpan:F1}m");
                    }
                }
            }
            bool anyPass = anyProven || anyUnproven;
            if (anyPass && anyFail)
            {
                text.AppendLine("  ⚠️ 일부 자리만 불가 — 자리 배정이 곧 불이익이다");
            }
            //  통과/실패는 갈리지 않아도 "증명됐다"와 "증명 못 했다"가 자리마다 갈리는 것도
            //  같은 종류의 불공정이다 — 어떤 자리는 확실히 안전하다고 보장할 수 있고 어떤
            //  자리는 못 한다면, 그 확신의 차이도 자리 배정에 달렸다.
            else if (anyProven && anyUnproven)
            {
                text.AppendLine("  ⚠️ 일부 자리만 증명됨 — 자리마다 안전 확신의 정도가 다르다");
            }
            //  ❌와 🟡은 원인이 다른 두 실패다 — 한 문장·한 처방으로 묶으면 처방이 안 맞는
            //  쪽까지 같이 받는다. 실제로 나온 글자에만, 그 글자에 맞는 처방으로 각각 찍는다.
            if (anyFail)
            {
                float suggestedGrid = heightGrid * 0.5f;
                text.AppendLine($"  (❌는 높이 눈금(HeightGrid) {heightGrid:F2}가 굵어 생긴 결과일 수 있다"
                              + $" — 눈금을 {suggestedGrid:F2}로 줄여 다시 눌러 볼 것)");
            }
            if (anyUnproven)
            {
                //  🟡의 원인은 눈금이 굵어서가 아니라 반올림 편향이 틱마다 누적된 것이다 —
                //  눈금을 좁혀도 비례해서 나아지지 않는다(docs/ROADMAP.md에 원인 기록).
                //  통하지 않는 처방을 안내하지 않는다.
                text.AppendLine("  (🟡는 탐색이 경로를 찾았지만 진짜 커널 재생이 어긋난 결과다 —"
                              + " 원인은 파악돼 있으며, 높이 눈금을 좁히는 것은 안정적인 해법이"
                              + " 아니다. 자세한 내용은 docs/ROADMAP.md 참고)");
            }
            text.AppendLine();

            text.AppendLine("── ② 낌 지점 ─────────────────────────");
            text.AppendLine(trapSection);
            text.AppendLine();

            text.AppendLine("── ③ 스턴 예산 ───────────────────────");
            text.AppendLine("  경과   클린런위치   허용    가능");
            for (int i = 0; i < budget.Count; i++)
            {
                StunBudgetPoint point = budget[i];
                string mark = point.PossibleStuns > point.AllowedStuns ? "  ⚠️" : "";
                text.AppendLine($"  {point.ElapsedSeconds,4:F1}초   {point.CleanRunX,7:F0}m"
                              + $"   {point.AllowedStuns,3}번  {point.PossibleStuns,3}번{mark}");
            }
            if (earliest.Caught)
            {
                text.AppendLine($"  ⚠️ 최속 탈락 {earliest.Seconds:F1}초 · {earliest.StunCount}번째"
                              + $" — 앞 {earliest.Seconds / cleanRunSeconds * 100f:F0}%는 압박 없음");
            }
            else
            {
                text.AppendLine("  골인 전에는 잡힐 수 없다 — 추격자가 압박이 되지 않는다");
            }
            text.AppendLine($"  (가정: 스턴 아닌 시간은 {config.ForwardSpeed:F0}으로 온전히 전진."
                          + " 실제는 이보다 나쁘다)");
            text.AppendLine("════════════════════════");
            return text.ToString();
        }

        static int CountFlaps(in CleanRunResult result)
        {
            int count = 0;
            for (int i = 0; i < result.Flaps.Count; i++)
            {
                if (result.Flaps[i]) { count++; }
            }
            return count;
        }
    }
}
