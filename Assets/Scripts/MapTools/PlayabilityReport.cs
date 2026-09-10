using System.Collections.Generic;
using System.Text;

namespace LOP.MapTools
{
    /// <summary>봇이 멈춘 자리와 이유 — 봇이 실패한 자리(🟡/❌)를 진단하기 위한 것이다. 봇이
    /// 통과했으면(✅) 이 값은 안 쓰인다 — 증명된 자리는 부검하지 않는다.</summary>
    public readonly struct BotDiagnostics
    {
        public readonly float EndX;
        public readonly float EndY;
        /// <summary>무언가에 닿아 멈췄는가. false면 닿지 않고 제한 틱을 다 써서 멈춘 것이다.</summary>
        public readonly bool Touched;
        public readonly int Ticks;
        /// <summary>겨냥할 틈을 못 찾아(BotPilot.Decide의 GapFound=false) 근거 없이 날갯짓한
        /// 틱 수. 크면 "봇이 보고도 놓친 것"이 아니라 "봇이 애초에 못 본 것"이다.</summary>
        public readonly int BlindTicks;
        /// <summary>가장 멀리 갔던 자리. 부딪혀 뒤로 밀리면 EndX보다 앞이다 — 다르면 EndX만
        /// 찍은 퍼센트가 "실제로 얼마나 갔었는지"를 과소평가한다.</summary>
        public readonly float FarthestX;
        /// <summary>이번 비행에 허용된 최대 틱 수. Ticks만 찍으면 분모가 없어 크고 작음을
        /// 판단할 수 없다.</summary>
        public readonly int TickLimit;
        /// <summary>봇을 멈춰 세운 콜라이더의 이름(계층 경로). null이면 무엇에 닿았는지 못
        /// 집어냈다는 뜻이다 — "닿았다"만 알고 무엇에 닿았는지는 모르는 상태라, 그럴 땐
        /// 원인 줄을 아예 안 찍는다(모르는 것을 아는 척하지 않는다).</summary>
        public readonly string HitColliderPath;
        /// <summary>닿기 직전의 세로 속도. 부호가 곧 "올라가다 위에 걸렸나 / 떨어지다 아래에
        /// 걸렸나"다. 닿은 뒤 값이 아니라 <b>직전</b> 값이어야 한다 — 이동 커널이 벽 방향
        /// 속도를 지우고 나면 부호가 사라져 아무것도 못 읽는다.</summary>
        public readonly float HitVerticalSpeed;

        public BotDiagnostics(float endX, float endY, bool touched, int ticks, int blindTicks,
                              float farthestX, int tickLimit,
                              string hitColliderPath = null, float hitVerticalSpeed = 0f)
        {
            EndX = endX;
            EndY = endY;
            Touched = touched;
            Ticks = ticks;
            BlindTicks = blindTicks;
            FarthestX = farthestX;
            TickLimit = tickLimit;
            HitColliderPath = hitColliderPath;
            HitVerticalSpeed = hitVerticalSpeed;
        }
    }

    /// <summary>탐색이 찾은 날갯짓 순서를 진짜 커널로 재생했을 때 어긋난 자리. "어긋났다"는
    /// 사실만으로는 사람이 원인을 못 짚는다 — 몇 번째 틱에, 어디서, 무엇에 닿았는지가 있어야
    /// 지형을 보러 갈지 탐색의 반올림을 보러 갈지 정할 수 있다.</summary>
    public readonly struct ReplayMismatch
    {
        /// <summary>재생을 실제로 돌려 어긋난 자리를 집어냈는가. false면 재생을 안 돌렸거나
        /// (봇이 통과해 탐색 자체를 생략) 끝까지 무충돌이었다는 뜻이다.</summary>
        public readonly bool Detected;
        /// <summary>몇 번째 틱에서 처음 닿았는가(1부터 센다 — 0틱째는 없다).</summary>
        public readonly int Tick;
        public readonly float X;
        public readonly float Y;
        /// <summary>닿기 직전의 세로 속도. <see cref="BotDiagnostics.HitVerticalSpeed"/>와 같은 뜻.</summary>
        public readonly float VerticalSpeed;
        public readonly string ColliderPath;

        public ReplayMismatch(bool detected, int tick, float x, float y, float verticalSpeed,
                              string colliderPath)
        {
            Detected = detected;
            Tick = tick;
            X = x;
            Y = y;
            VerticalSpeed = verticalSpeed;
            ColliderPath = colliderPath;
        }
    }

    /// <summary>진단용 한 줄 — 스폰이 아닌 높이에서 봇을 날려 본 결과. 판정에는 안 쓴다.</summary>
    public readonly struct HeightSweepRow
    {
        public readonly float StartY;
        public readonly bool Reached;
        /// <summary>그 높이가 지형 안이라 날려 보지도 못했다. 실패가 아니라 측정 불가다.</summary>
        public readonly bool SpawnBlocked;
        public readonly BotDiagnostics Bot;

        public HeightSweepRow(float startY, bool reached, bool spawnBlocked, in BotDiagnostics bot)
        {
            StartY = startY;
            Reached = reached;
            SpawnBlocked = spawnBlocked;
            Bot = bot;
        }
    }

    /// <summary>스폰 한 자리의 클린런 결과. <see cref="VerifiedByReplay"/>는 진짜 커널로 재생해 확인했는가.</summary>
    public readonly struct SpawnCleanRun
    {
        public readonly string Name;
        public readonly float Y;
        public readonly CleanRunResult Result;
        public readonly bool VerifiedByReplay;
        /// <summary>봇이 진짜 커널로 끝까지 갔는가. true면 이 자리는 증명된 것이다.</summary>
        public readonly bool BotReached;
        public readonly int BotFlaps;
        public readonly BotDiagnostics Bot;
        /// <summary>스폰 자체가 지형 안에 파묻혀 있어 검사가 성립하지 않는다. ✅/🟡/❌ 어디에도
        /// 속하지 않는 <b>넷째 상태</b>다 — 봇도 탐색도 이 자리엔 답할 것이 없고, 고칠 것은
        /// 맵의 지형이 아니라 스폰 마커의 위치다.</summary>
        public readonly bool SpawnInsideTerrain;
        /// <summary>재생이 어긋난 자리(🟡)의 부검 정보. <see cref="VerifiedByReplay"/>가
        /// false일 때만 뜻이 있다.</summary>
        public readonly ReplayMismatch Replay;

        public SpawnCleanRun(string name, float y, CleanRunResult result, bool verifiedByReplay,
                             bool botReached, int botFlaps, BotDiagnostics bot,
                             bool spawnInsideTerrain = false, ReplayMismatch replay = default)
        {
            Name = name;
            Y = y;
            Result = result;
            VerifiedByReplay = verifiedByReplay;
            BotReached = botReached;
            BotFlaps = botFlaps;
            Bot = bot;
            SpawnInsideTerrain = spawnInsideTerrain;
            Replay = replay;
        }
    }

    public static class PlayabilityReport
    {
        public static string Build(string mapName, float startX, float finishX, in FlappyConfig config,
                                   IReadOnlyList<SpawnCleanRun> cleanRuns, string trapSection,
                                   IReadOnlyList<StunBudgetPoint> budget, EarliestCatch earliest,
                                   float heightGrid, float minY, float maxY,
                                   IReadOnlyList<HeightSweepRow> heightSweep = null)
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
            //  이 y대역이 ①(클린런)·②(낌 지점) 둘 다의 *탐색* 상/하한이다(Default 콜라이더
            //  최고~최저점). ①만 읽는 사람도 봐야 한다 — 천장 없는 맵이면 이 위로 날아 넘는,
            //  실제로는 되는 경로를 탐색이 못 보고도 ❌를 찍을 수 있어서다.
            //  이 대역은 봇에게는 다른 뜻이다: 봇의 스캔 표 크기만 정할 뿐 비행 자체는 제약하지
            //  않는다. 그래서 대역 위로 날아오른 봇도 통과 판정을 받는데, 탐색이었다면 같은
            //  경로를 거부했을 것이다. 게임에 천장도 킬플레인도 없으니 그 통과가 옳은 답일 수
            //  있다 — 다만 "봇과 탐색이 같은 대역을 본다"는 말은 정확하지 않다는 뜻이라
            //  그렇게 적는다. (여기 글리프를 쓰지 않는 것은 의도적이다 — 머리말은 자리별
            //  판정과 무관하게 늘 찍히므로, 글리프를 넣으면 "이 리포트에 ✅가 있다"가 언제나
            //  참이 되어 자리별 판정을 확인하는 검사가 통째로 공허해진다.)
            text.AppendLine($"탐색 대역 y[{minY:F1}~{maxY:F1}] (천장 없으면 그 위 경로는 탐색 밖 —"
                          + " 단 봇의 비행은 이 대역에 갇히지 않는다: 대역은 봇의 스캔 표 크기만 정한다."
                          + " 그래서 이 위로 올라간 봇은 통과로 찍히고, 탐색은 같은 경로를 거부한다)");
            text.AppendLine();

            text.AppendLine("── ① 클린런 (자리별) ──────────────────");
            bool anyFail = false, anyProven = false, anyUnproven = false, anyBuried = false;
            for (int i = 0; i < cleanRuns.Count; i++)
            {
                SpawnCleanRun run = cleanRuns[i];
                //  파묻힌 스폰은 ✅/🟡/❌ 어느 쪽도 아니다 — 봇도 탐색도 이 자리엔 답할 것이
                //  없다. 세 글자 중 하나를 빌려 쓰면 그 글자의 뜻이 흐려지므로 제 글자를 준다.
                if (run.SpawnInsideTerrain)
                {
                    anyBuried = true;
                    text.AppendLine($"  {run.Name} (y={run.Y:F0})   ⛔  스폰이 지형 안 — 검사 불가");
                }
                //  봇이 진짜 커널로 끝까지 갔으면 그 궤적 자체가 증명이다 — 탐색 결과가 뭐든
                //  (심지어 안 돌았어도) 이 자리는 끝이다. 봇이 못 갔을 때만 Result/VerifiedByReplay로
                //  갈라 "맵이 불가능"과 "탐색은 찾았지만 증명 못 함"을 구분한다.
                else if (run.BotReached)
                {
                    anyProven = true;
                    text.AppendLine($"  {run.Name} (y={run.Y:F0})   ✅  봇 통과 · 날갯짓 {run.BotFlaps}회");
                }
                else if (run.Result.Reachable)
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
                        //  이 줄의 날갯짓 수는 탐색이 찾은 경로의 것이다 — 바로 아래 "봇:" 줄의
                        //  것과는 다른 비행이다. 라벨 없이 숫자만 찍으면 봇이 실제로 낸 값과
                        //  섞여 읽혀 "봇이 몇 번 날갯짓했나"를 잘못 짚게 된다.
                        text.AppendLine($"  {run.Name} (y={run.Y:F0})   🟡  탐색 경로 날갯짓 {CountFlaps(run.Result)}회"
                                      + "   ⚠️ 봇은 못 갔고 탐색은 찾았으나 재생이 어긋남");
                        AppendBotDiagnostics(text, run.Bot, run.BotFlaps, startX, finishX);
                        AppendReplayMismatch(text, run.Replay);
                    }
                }
                else
                {
                    anyFail = true;
                    //  이 x는 탐색이 막힌 지점이다 — 바로 아래 "봇:" 줄의 x(봇이 멈춘 지점)와는
                    //  다른 값이다. 둘 다 "x="만 찍으면 어느 쪽인지 라벨로 구분할 수 없다.
                    text.AppendLine($"  {run.Name} (y={run.Y:F0})   ❌  탐색 x={run.Result.BlockedX:F1}에서 막힘");
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
                    AppendBotDiagnostics(text, run.Bot, run.BotFlaps, startX, finishX);
                }
            }
            //  파묻힌 스폰은 통과에도 실패에도 안 든다 — 아래 공정성/처방 문구들이 이 자리를
            //  "된다"나 "안 된다" 어느 쪽으로도 세지 않게 한다.
            bool anyPass = anyProven || anyUnproven;
            if (anyBuried)
            {
                text.AppendLine("  ⛔ 지형에 파묻힌 스폰이 있다 — 그 자리는 봇도 탐색도 돌리지 못했다."
                              + " 스폰 마커를 지형 밖으로 옮기고 다시 검사할 것"
                              + " (맵 지형이 아니라 마커 자리의 문제다).");
            }
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
                //  🟡는 이제 "봇이 못 갔다"는 사실을 담는다 — 그것이 곧 맵이 불가능하다는
                //  뜻은 아니다(봇의 한계일 수 있다). 탐색이 찾은 경로가 있다는 것과, 그
                //  경로가 진짜 커널 재생에서 어긋나 증명은 못 했다는 것은 별개다. 재생
                //  불일치의 원인은 눈금이 굵어서가 아니라 반올림 편향이 틱마다 누적된
                //  것이다 — 눈금을 좁혀도 비례해서 나아지지 않는다(docs/ROADMAP.md에
                //  원인 기록). 통하지 않는 처방을 안내하지 않는다.
                text.AppendLine("  (🟡는 봇이 못 갔지만 탐색은 경로를 찾은 결과다 — 맵이 불가능하다는"
                              + " 뜻이 아니라 봇이 못 간 것일 수 있다. 탐색이 찾은 경로는 진짜 커널"
                              + " 재생에서 어긋나 증명하지 못했다 — 원인은 파악돼 있으며, 높이 눈금을"
                              + " 좁히는 것은 안정적인 해법이 아니다. 자세한 내용은 docs/ROADMAP.md 참고)");
            }
            text.AppendLine();

            AppendHeightSweep(text, heightSweep, startX, finishX);

            text.AppendLine("── ② 낌 지점 ─────────────────────────");
            text.AppendLine(trapSection);
            //  ②는 *일부러 지형 안에서* 출발시켜 빠져나오는지 보는 검사인데, 검사기의 한 틱
            //  (Step)은 실제 게임(FlappyWorld)이 매 틱 하는 밀어내기·벽 방향 속도 지우기를
            //  안 돌린다. 그래서 실제 게임이라면 밀려나 빠져나왔을 자리를 "낌"으로 보고할 수
            //  있다. ①의 *증명된* 통과에는 무해하다(닿은 적 없는 새는 겹치지도 않는다) —
            //  그러나 ②는 레벨 디자이너가 보고 실제로 손대는 출력이라 여기 적어 둔다.
            text.AppendLine("  (주의: 이 검사는 실제 게임이 매 틱 하는 밀어내기를 안 돌린다 —"
                          + " 게임에서는 밀려나 빠져나오는 자리도 낌으로 보고될 수 있다. 손대기 전에"
                          + " 실제로 껴 보는지 한 번 확인할 것)");
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

        //  봇이 못 간 자리(🟡/❌)마다 어디서 왜 멈췄는지를 같은 형식으로 찍는다 — 네 자리를
        //  나란히 놓았을 때 x가 같은지 다른지가 한눈에 들어와야 한다: 같으면 장애물 하나가
        //  전부를 막는 것이고, 다르면 파일럿(봇) 자체가 약한 것이다. 증명된 자리(✅)는 이
        //  함수를 타지 않는다 — 부검할 실패가 없다.
        static void AppendBotDiagnostics(StringBuilder text, in BotDiagnostics bot, int botFlaps,
                                         float startX, float finishX)
        {
            //  Ticks==0은 실제로 봇을 날린 적이 없다는 뜻이다 — FlyBot은 무슨 일이 있어도
            //  최소 1틱은 돌고서야 return한다(0틱 반환 경로가 없다). 여기서 0들을 그대로
            //  찍으면 "x=0.0에서 죽었다"처럼 측정값으로 보인다 — 최협 회랑의 "측정 안 됨"과
            //  같은 원칙: 재지 못했으면 쟀다고 말하지 않는다.
            if (bot.Ticks == 0)
            {
                text.AppendLine("                             봇: 측정 안 됨 — 진단 데이터 없음");
                return;
            }

            float percent = (bot.EndX - startX) / (finishX - startX) * 100f;
            //  닿아서(Touched) 멈춘 것과, 안 닿았는데 제한 틱을 다 써서 멈춘 것은 원인이
            //  다르다 — 후자는 봇이 제자리 근처를 맴돌았다는 뜻이라 처방이 또 다르다.
            string reason = bot.Touched ? "닿음" : "틱 소진(못 닿음)";
            //  틱 수 하나만 찍으면 분모가 없어 크고 작음을 판단할 수 없다 — 예산 대비 비율로
            //  같이 찍는다("25%"면 예산의 1/4만에 죽었다는 뜻이 바로 읽힌다).
            float tickPercent = (float)bot.Ticks / bot.TickLimit * 100f;
            var line = new StringBuilder(
                $"                             봇: x={bot.EndX:F1} y={bot.EndY:F1}"
                + $" (코스 {percent:F0}%)  {reason} · {bot.Ticks}/{bot.TickLimit}틱({tickPercent:F0}%)"
                //  "봇" 접두를 달아 바로 위 검증 줄의 "탐색 경로 날갯짓"과 다른 비행의
                //  수치임을 라벨로 구분한다 — 값이 갈릴 때(현실적으로는 대부분) 어느 쪽이
                //  어느 쪽인지 헷갈리지 않게.
                + $" · 봇 날갯짓 {botFlaps}회 · 목표 없음 {bot.BlindTicks}틱");
            //  부딪혀 뒤로 밀리면 멈춘 자리(EndX)가 가장 멀리 갔던 자리(FarthestX)보다 뒤다 —
            //  퍼센트만 보면 "실제로 얼마나 갔었는지"를 과소평가한다. 갈릴 때만 덧붙인다
            //  (같으면 군더더기라 안 찍는다). float 오차 대비 작은 여유(0.01)를 둔다.
            if (bot.FarthestX > bot.EndX + 0.01f)
            {
                float farthestPercent = (bot.FarthestX - startX) / (finishX - startX) * 100f;
                line.Append($" · 최고 도달 x={bot.FarthestX:F1} (코스 {farthestPercent:F0}%)");
            }
            text.AppendLine(line.ToString());

            //  무엇에 닿았는지까지 나와야 "맵이 어려운 것"과 "봇이 못 푸는 것"이 갈린다 —
            //  "닿음"만으로는 읽는 사람이 씬을 열어 그 자리를 눈으로 찾아야 한다.
            //  닿지 않았거나(틱 소진) 무엇에 닿았는지 못 집어냈으면 안 찍는다.
            if (bot.Touched && string.IsNullOrEmpty(bot.HitColliderPath) == false)
            {
                text.AppendLine("                                  "
                              + ImpactPhrase(bot.HitVerticalSpeed, bot.HitColliderPath));
            }
        }

        //  "무엇에·어느 쪽으로 가다 닿았나"를 한 문장으로. 세로 속도의 부호가 곧 방향이다.
        //  ("천장"·"바닥"이라 단정하지 않는 것은 의도적이다 — 닿은 면이 실제로 위인지 아래인지는
        //   재지 않았고, 아는 것은 새가 어느 쪽으로 움직이던 중이었나뿐이다. 단정하면 아래에서
        //   위로 솟은 기둥 옆구리를 "천장"이라 부르게 된다.)
        static string ImpactPhrase(float verticalSpeed, string colliderPath)
        {
            string direction;
            if (verticalSpeed > 0.5f) { direction = "↑ 오르다 부딪힘"; }
            else if (verticalSpeed < -0.5f) { direction = "↓ 떨어지다 부딪힘"; }
            else { direction = "→ 수평으로 부딪힘"; }
            //  부호를 반드시 보이게 찍는다(+12.4 / -30.0) — 이 줄의 값은 부호가 전부다.
            return $"{direction} (vy={verticalSpeed:+0.0;-0.0;0.0}) :: {colliderPath}";
        }

        //  재생이 어긋난 자리의 부검. "어긋났다"만으로는 지형을 보러 갈지 탐색의 반올림을
        //  보러 갈지 정할 수 없다 — 틱·자리·닿은 것이 있어야 사람이 원인을 짚는다.
        static void AppendReplayMismatch(StringBuilder text, in ReplayMismatch replay)
        {
            if (replay.Detected == false)
            {
                return;
            }
            var line = new StringBuilder(
                $"                             재생 어긋남: {replay.Tick}틱째"
                + $" x={replay.X:F1} y={replay.Y:F1} (vy={replay.VerticalSpeed:+0.0;-0.0;0.0})");
            if (string.IsNullOrEmpty(replay.ColliderPath) == false)
            {
                line.Append($" :: {replay.ColliderPath}");
            }
            text.AppendLine(line.ToString());
        }

        //  판정이 아니라 진단이다 — 스폰이 아닌 높이에서도 날려 봐서, 봇이 막히는 자리가 시작
        //  높이에 따라 연속적으로 움직이는지 본다. 계단처럼 두세 값으로만 갈리면 지형이 아니라
        //  봇(또는 그 입력)이 정보를 잃고 있다는 신호다. 절을 따로 두는 이유: ①의 판정에 섞이면
        //  "스폰도 아닌 자리가 실패했다"가 맵의 결함으로 읽힌다.
        static void AppendHeightSweep(StringBuilder text, IReadOnlyList<HeightSweepRow> rows,
                                      float startX, float finishX)
        {
            //  훑지 않았으면 절 자체를 안 찍는다 — 빈 표는 "훑었는데 아무것도 없었다"로 읽힌다.
            if (rows == null || rows.Count == 0)
            {
                return;
            }
            text.AppendLine("── ① 진단 — 시작 높이 훑기 ────────────");
            text.AppendLine("  (판정이 아니라 진단이다. 맵의 스폰이 아닌 높이에서도 날려 봐서, 봇이"
                          + " 막히는 자리가");
            text.AppendLine("   시작 높이에 따라 어떻게 움직이는지 본다. 결과가 계단처럼 두세 값으로만"
                          + " 갈리면");
            text.AppendLine("   지형이 아니라 봇이 정보를 잃고 있다는 뜻이다.)");
            for (int i = 0; i < rows.Count; i++)
            {
                HeightSweepRow row = rows[i];
                //  값이 라벨 바로 뒤에 붙게 두고(정렬은 뒤에서 채운다) — 사이에 정렬 공백이
                //  끼면 "시작 y=-8.0"이 한 덩어리로 안 남아 찾기가 어려워진다.
                var line = new StringBuilder($"  시작 y={row.StartY:F1}".PadRight(18));
                if (row.SpawnBlocked)
                {
                    line.Append("그 높이가 지형 안 — 못 날림");
                }
                else if (row.Reached)
                {
                    line.Append("골인");
                }
                else
                {
                    float percent = (row.Bot.EndX - startX) / (finishX - startX) * 100f;
                    line.Append($"x={row.Bot.EndX:F1} (코스 {percent:F0}%)");
                    if (row.Bot.Touched && string.IsNullOrEmpty(row.Bot.HitColliderPath) == false)
                    {
                        line.Append("  " + ImpactPhrase(row.Bot.HitVerticalSpeed, row.Bot.HitColliderPath));
                    }
                    else if (row.Bot.Touched == false)
                    {
                        line.Append("  틱 소진(못 닿음)");
                    }
                }
                text.AppendLine(line.ToString());
            }
            text.AppendLine();
        }
    }
}
