using System.Collections.Generic;
using System.Text;

namespace LOP.MapTools
{
    /// <summary>죽기 직전으로 되돌아가 "그 틱에 다르게 눌렀으면 더 갔을까"를 직접 물어 본 결과.
    ///
    /// <para><b>왜 이 측정인가 — 반대 가설이면 숫자가 달라지기 때문이다.</b> 겨냥 규칙이 문제라면
    /// 봇이 안 누른 자리에서 눌렀을 때 <i>더 간다</i>. 지형이 정말 못 지나가는 것이라면 어느 틱에
    /// 무엇을 해도 <i>더 못 간다</i>. 두 가설이 서로 다른 답을 예측하므로 이 숫자로 결론을 낼 수
    /// 있다. (짝인 막힘:뜻없음 두 계수기는 그렇지 않다 — 살아 있는 봇이면 어느 가설에서도 늘
    /// "뜻 없음"이 압도한다. 그래서 그 비만으로는 아무것도 못 고른다.)</para></summary>
    public readonly struct Counterfactual
    {
        /// <summary>실제로 되돌려 봤는가. false면 안 돌렸다는 뜻이므로 숫자를 아예 안 찍는다 —
        /// 재지 못한 것을 0으로 찍으면 "되돌려 봤는데 하나도 못 살렸다"로 읽힌다.</summary>
        public readonly bool Measured;
        /// <summary>죽기 전 몇 틱을 시험했나. 분모다 — 없으면 "17곳"이 많은지 적은지 못 읽는다.</summary>
        public readonly int Tried;
        /// <summary>되돌렸더니 유의미하게 더 간 자리의 수.</summary>
        public readonly int Savable;
        /// <summary>그중 <b>강제로 누르게</b> 해서 더 간 자리의 수. 나머지는 반대로 강제로 안
        /// 누르게 한 것이다. 이 갈림이 곧 "겨냥이 너무 소심한가 / 너무 성급한가"다.</summary>
        public readonly int SavableByFlap;
        /// <summary>살릴 수 있던 자리 중 가장 이른 것이 죽기 몇 틱 전인가. 이를수록 봇이 일찍부터
        /// 잘못 가고 있었다는 뜻이다.</summary>
        public readonly int EarliestTicksBeforeDeath;
        /// <summary>그 가장 이른 자리에서 몇 미터를 더 갔나.</summary>
        public readonly float EarliestGain;
        /// <summary>그 자리의 뒤집기가 "강제로 누름"이었나(false면 "강제로 안 누름").</summary>
        public readonly bool EarliestForcedFlap;

        public Counterfactual(bool measured, int tried, int savable, int savableByFlap,
                              int earliestTicksBeforeDeath, float earliestGain, bool earliestForcedFlap)
        {
            Measured = measured;
            Tried = tried;
            Savable = savable;
            SavableByFlap = savableByFlap;
            EarliestTicksBeforeDeath = earliestTicksBeforeDeath;
            EarliestGain = earliestGain;
            EarliestForcedFlap = earliestForcedFlap;
        }
    }

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
        /// <summary>누르고 싶었는데 아치 훑기가 막은 틱 수(BotPilot.Decide의 CeilingBlocked).
        /// <para><b>이 숫자만으로 결론을 내면 안 된다.</b> 아래 <see cref="UnwillingTicks"/>와의
        /// 비는 봇의 실력이 아니라 <i>맵이 얼마나 트였나</i>를 잰다 — 완벽한 봇이 열린 하늘을
        /// 날아도 1:∞이 나온다. 처방은 되돌리기(<see cref="LOP.MapTools.Counterfactual"/>)가 낸다.</para></summary>
        public readonly int VetoedTicks;
        /// <summary>애초에 누를 뜻이 없던 틱 수(BotPilot.Decide의 WantsFlap=false).
        /// <see cref="VetoedTicks"/>와 짝이며, 같은 이유로 <b>둘의 비만으로는 결론을 못 낸다</b>.</summary>
        public readonly int UnwillingTicks;
        /// <summary>굴려 보기(<see cref="BotRollout"/>)가 기반 정책의 답을 실제로 뒤집은 틱 수.
        /// <para>0이면 전방탐색이 아무 일도 안 한 것이다 — 도달 거리가 그대로일 때 "정책이 사실상
        /// 같았다"와 "정책은 달랐는데 그래도 소용없었다"를 가르는 유일한 숫자다.</para></summary>
        public readonly int RolloutDeviations;
        /// <summary>죽기 직전으로 되돌려 "다르게 눌렀으면 살았나"를 직접 물어 본 결과.
        /// 위 두 계수기와 달리 <b>양쪽으로 열려 있다</b> — 자세한 이유는 <see cref="LOP.MapTools.Counterfactual"/>.</summary>
        public readonly Counterfactual Counterfactual;

        public BotDiagnostics(float endX, float endY, bool touched, int ticks, int blindTicks,
                              float farthestX, int tickLimit,
                              string hitColliderPath = null, float hitVerticalSpeed = 0f,
                              int vetoedTicks = 0, int unwillingTicks = 0,
                              Counterfactual counterfactual = default,
                              int rolloutDeviations = 0)
        {
            RolloutDeviations = rolloutDeviations;
            Counterfactual = counterfactual;
            EndX = endX;
            EndY = endY;
            Touched = touched;
            Ticks = ticks;
            BlindTicks = blindTicks;
            FarthestX = farthestX;
            TickLimit = tickLimit;
            HitColliderPath = hitColliderPath;
            HitVerticalSpeed = hitVerticalSpeed;
            VetoedTicks = vetoedTicks;
            UnwillingTicks = unwillingTicks;
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
        /// <summary>탐색이 <b>그 틱에</b> 새가 있다고 믿었던 높이. 재생이 실제로 간 <see cref="Y"/>와
        /// 얼마나 벌어졌는지가 곧 격자 편향의 크기다.</summary>
        public readonly float SearchY;
        /// <summary><see cref="SearchY"/>를 실제로 구했는가. 못 구했으면(경로가 그 틱까지
        /// 닿지 않는 등) 안 찍는다 — 0.0을 그대로 찍으면 "탐색은 0m로 봤다"는 측정값으로 읽힌다.</summary>
        public readonly bool HasSearchY;
        /// <summary>부딪히기 <b>직전</b> 틱 — 마지막으로 자유롭게 움직인 틱 — 의 탐색-재생 높이 차이.
        /// <para>왜 따로 두나: <see cref="Y"/>는 이동 커널이 벽에 <i>잘라낸 뒤</i>의 값이라
        /// 그 차이가 실제 편향이 아니다. vy가 +18.8이면 그 틱의 자유 이동만 0.376m인데 새는
        /// 그 도중에 멈췄으므로, 인쇄된 차이의 상당 부분(실측 최대 80%)이 충돌 클램프일 수 있다.
        /// 즉 충돌 후 차이는 <b>상한</b>이고, 직전 틱 차이가 실제 편향에 가깝다.</para></summary>
        public readonly float PrevDiff;
        /// <summary><see cref="PrevDiff"/>를 실제로 구했는가(1틱째에 부딪혔거나 탐색 높이가
        /// 없으면 못 구한다).</summary>
        public readonly bool HasPrevDiff;

        public ReplayMismatch(bool detected, int tick, float x, float y, float verticalSpeed,
                              string colliderPath, float searchY = 0f, bool hasSearchY = false,
                              float prevDiff = 0f, bool hasPrevDiff = false)
        {
            PrevDiff = prevDiff;
            HasPrevDiff = hasPrevDiff;
            Detected = detected;
            Tick = tick;
            X = x;
            Y = y;
            VerticalSpeed = verticalSpeed;
            ColliderPath = colliderPath;
            SearchY = searchY;
            HasSearchY = hasSearchY;
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
            //  봇 부검 줄(막힘/뜻없음 계수기 포함)이 한 줄이라도 찍혔는가 — 찍혔을 때만 그
            //  계수기를 어떻게 읽어야 하는지 경고를 단다. 안 찍힌 리포트에 경고만 뜨면 없는
            //  숫자를 조심하라는 말이 된다.
            bool anyBotAutopsy = false;
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
                        anyBotAutopsy = true;
                        AppendBotDiagnostics(text, run.Bot, run.BotFlaps, startX, finishX);
                        AppendReplayMismatch(text, run.Replay, startX, finishX);
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
                    anyBotAutopsy = true;
                    AppendBotDiagnostics(text, run.Bot, run.BotFlaps, startX, finishX);
                }
            }
            //  두 계수기는 정확하지만 <b>혼자서는 결론을 못 낸다</b> — 실측 기준선이 그렇다.
            //  리포트가 그 비로 처방을 고르게 안내하면 안 되므로, 계수기가 찍힐 때마다 그
            //  기준선을 함께 적는다.
            if (anyBotAutopsy)
            {
                text.AppendLine("  (막힘:뜻없음 비는 혼자서는 결론을 못 낸다 — 완벽한 봇이 열린 하늘을 날면 1:∞이고");
                text.AppendLine("   물리적 최소 회랑에서도 1:2.4다. 즉 이 비는 \"맵이 얼마나 트였나\"에 가깝다.");
                text.AppendLine("   무엇을 고쳐야 하는지는 위 \"되돌리기\"가 답한다.)");
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

            //  봇이 못 간 이유가 "못 눌렀다"인지 "안 눌렀다"인지 — 이 두 숫자가 다음에 무엇을
            //  고칠지 정한다(거부가 압도적이면 아치가 통로에 안 들어가는 것이라 계획하는 봇이
            //  필요하고, 누를 뜻이 없던 쪽이 압도적이면 겨냥 규칙이 목표를 너무 아래로 잡는 것이다).
            text.AppendLine($"                             {CounterPhrase(bot)}");

            //  이 줄이 실제 처방을 낸다 — 위 두 계수기와 달리 가설에 따라 답이 갈리기 때문이다.
            //  안 쟀으면 아예 안 찍는다(0곳으로 찍으면 "되돌려 봤는데 못 살렸다"로 읽힌다).
            if (bot.Counterfactual.Measured)
            {
                text.AppendLine($"                             {CounterfactualPhrase(bot.Counterfactual)}");
            }
        }

        //  두 숫자를 한 문장으로. 두 줄에 나눠 찍으면 "어느 쪽이 압도적인가"를 눈으로 비교하기
        //  어려워진다 — 이 출력의 목적이 바로 그 비교다.
        //  셋째 숫자(굴려 보기가 뒤집은 틱)는 앞의 둘과 다른 것을 잰다 — 앞의 둘은 <b>기반
        //  정책</b>이 무엇을 하려 했는가고, 이건 그 위의 전방탐색이 실제로 몇 번이나 다르게
        //  골랐는가다. 도달 거리가 안 변했을 때 여기가 0인지 아닌지로 원인이 갈린다.
        static string CounterPhrase(in BotDiagnostics bot)
            => $"누르려다 막힘 {bot.VetoedTicks}틱 / 누를 뜻 없음 {bot.UnwillingTicks}틱"
             + $" / 굴려 보고 뒤집음 {bot.RolloutDeviations}틱";

        //  "죽기 직전으로 되돌려 다르게 눌렀으면 살았나". 살릴 수 있던 자리가 하나도 없다는 것도
        //  결론이므로 그 경우에도 문장을 낸다 — 침묵과 "0곳"은 다른 뜻이다.
        static string CounterfactualPhrase(in Counterfactual cf)
        {
            if (cf.Savable == 0)
            {
                //  "창 안에서는"을 반드시 붙인다. 실측으로 확인된 함정이다 — 같은 자리가
                //  창 30틱에서는 0곳, 60틱에서는 6곳이 나왔다. 창을 안 밝히면 이 줄이
                //  "지형이 막았다"는 <b>틀린</b> 결론으로 읽힌다.
                return $"되돌리기: 죽기 전 {cf.Tried}틱 어디서 다르게 눌러도 더 못 갔다"
                     + " → 이 창 안에서는 겨냥이 아니라 지형이 막았다 (창 밖은 안 봤다)";
            }
            string how = cf.EarliestForcedFlap ? "눌렀으면" : "안 눌렀으면";
            return $"되돌리기: 죽기 전 {cf.Tried}틱 중 {cf.Savable}곳에서 다르게 눌렀으면 더 갔다"
                 + $" (그중 누름 강제 {cf.SavableByFlap}곳)"
                 + $" · 가장 이른 곳: 죽기 {cf.EarliestTicksBeforeDeath}틱 전, 거기서 {how} +{cf.EarliestGain:F1}m";
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
        static void AppendReplayMismatch(StringBuilder text, in ReplayMismatch replay,
                                         float startX, float finishX)
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

            //  "탐색은 거기를 통과 가능하다고 믿었다" — 그 믿음을 적으면 격자 편향이 얼마나
            //  벌어졌는지가 바로 읽힌다. 차이의 부호도 함께 찍는다(탐색이 위로 봤나 아래로 봤나).
            if (replay.HasSearchY)
            {
                var diff = new StringBuilder(
                    $"                             탐색은 그 틱에 y={replay.SearchY:+0.0;-0.0;0.0}로 봤다"
                    + $" (충돌 후 차이 {replay.SearchY - replay.Y:+0.0;-0.0;0.0}m");
                //  충돌 후 차이는 이동 커널이 벽에 잘라낸 뒤의 값이라 <b>상한</b>이다 —
                //  직전(마지막으로 자유로웠던) 틱의 차이가 실제 편향에 가깝다. 둘을 나란히
                //  찍어야 읽는 사람이 상한을 편향으로 착각하지 않는다.
                if (replay.HasPrevDiff)
                {
                    diff.Append($" / 직전 틱 차이 {replay.PrevDiff:+0.00;-0.00;0.00}m");
                }
                diff.Append(")");
                text.AppendLine(diff.ToString());
                AppendReplayBiasReading(text, replay, startX, finishX);
            }
        }

        //  "직전 틱 차이"를 <b>비율</b>로도 적는다. 이 줄이 없으면 "0.45m"가 작아 보인다 —
        //  올바른 독해는 반대다: 코스의 7.7%에서 이미 반 미터면 치명적이고, 편향은 틱마다
        //  같은 방향으로 쌓이므로 단조 증가한다.
        static void AppendReplayBiasReading(StringBuilder text, in ReplayMismatch replay,
                                            float startX, float finishX)
        {
            if (replay.HasPrevDiff == false || replay.Tick <= 0)
            {
                return;
            }
            float covered = replay.X - startX;
            float course = finishX - startX;
            if (covered <= 0f || course <= 0f)
            {
                return;
            }
            float perTick = replay.PrevDiff / replay.Tick;
            //  전진 속도가 상수라 "코스 전체 틱 수"는 지금까지의 틱을 간 거리 비율로 늘리면 된다.
            float courseTicks = replay.Tick * (course / covered);
            float extrapolated = perTick * courseTicks;
            text.AppendLine($"                             편향 읽기: 충돌 후 차이는 상한이다(벽에 잘린 값)."
                          + $" 직전 틱 차이 {replay.PrevDiff:+0.00;-0.00;0.00}m ÷ {replay.Tick}틱"
                          + $" = {perTick:+0.000;-0.000;0.000}m/틱.");
            text.AppendLine($"                                   여기는 코스의 {covered / course * 100f:F1}%인데 벌써 이만큼 벌어졌고,"
                          + $" 같은 비율로 끝(약 {courseTicks:F0}틱)까지 가면 약 {extrapolated:F1}m다"
                          + " — 작다는 뜻이 아니라 단조 증가한다는 뜻이다.");
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
            text.AppendLine("  (\"되돌리기 N곳\" = 죽기 직전 몇 틱을 되돌려 반대로 눌러 봤을 때 더 간 자리의 수."
                          + " 0곳이면 지형이 막은 것이고, 여러 곳이면 겨냥이 놓친 것이다.)");
            for (int i = 0; i < rows.Count; i++)
            {
                HeightSweepRow row = rows[i];
                //  값이 라벨 바로 뒤에 붙게 두고(정렬은 뒤에서 채운다) — 사이에 정렬 공백이
                //  끼면 "시작 y=-8.0"이 한 덩어리로 안 남아 찾기가 어려워진다.
                var line = new StringBuilder($"  시작 y={row.StartY:F1}".PadRight(18));
                //  ①의 봇 줄과 같은 두 숫자를 여기도 붙인다 — 스폰은 넷뿐이지만 이 줄들은
                //  수십 개라, "못 눌렀나 안 눌렀나"를 묻는 이 질문의 진짜 표본은 이쪽이다.
                //  줄이 길어지지 않게 짧은 꼴로 쓴다.
                string counters = $"  (막힘 {row.Bot.VetoedTicks}틱/뜻없음 {row.Bot.UnwillingTicks}틱)";
                //  훑기 줄은 수십 개라 되돌리기 문장을 통째로 붙이면 표가 안 읽힌다 — 결론을
                //  내는 한 숫자(살릴 수 있던 자리 개수)만 붙인다. 안 쟀으면 안 붙인다.
                if (row.Bot.Counterfactual.Measured)
                {
                    counters += $"  되돌리기 {row.Bot.Counterfactual.Savable}곳";
                }
                if (row.SpawnBlocked)
                {
                    //  못 날린 줄에는 안 붙인다 — 0/0을 찍으면 "날려 봤는데 둘 다 0이었다"로 읽힌다.
                    line.Append("그 높이가 지형 안 — 못 날림");
                    text.AppendLine(line.ToString());
                    continue;
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
                line.Append(counters);
                text.AppendLine(line.ToString());
            }
            text.AppendLine();
        }
    }
}
