using System;
using System.Collections.Generic;
using GameFramework;
using MessagePipe;
using UnityEngine;

namespace LOP.UI
{
    /// <summary>
    /// 한 발 승부 HUD — 라운드 번호, 남은 시간, 바람, 라운드 결과 목록, 해설 자막.
    /// 값은 코스 데이터와 이미 오는 사건(적중·라운드 결과)에서만 나온다(새 통신 없음, 스펙 §3.8).
    /// View가 매 프레임 <see cref="Tick"/>을 부르고 값을 읽어 간다.
    /// </summary>
    public class ArcheryShootOffHudViewModel : IDisposable
    {
        //  이 세기(m/s²) 이상이면 바람이 세다고 해설한다. 화살표 크기는 WindArrowFull에서 가득 찬다.
        private const float StrongWind = 4f;
        private const float WindArrowFull = 5f;
        private const double LastShotMarginTicks = 2d;

        private readonly GameFramework.Runner.IRunner runner;
        private readonly ArcheryConfig config;
        private readonly ArcheryWorld world;
        private readonly ArcheryCourse course;
        private readonly IGameDataStore gameDataStore;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly IDisposable subscription;

        private readonly ArcheryShootOffResultTracker resultTracker;
        private readonly CameraController cameraController;
        private readonly ArcheryArrowStickSystem stickSystem;
        private readonly ArcheryArrowLandings landings;
        private readonly ArcheryChickenView chickenView;
        private readonly ArcheryCommentary commentary = new ArcheryCommentary(n => UnityEngine.Random.Range(0, n));
        private readonly ArcheryShootOffNarrator narrator = new ArcheryShootOffNarrator();
        private readonly List<string> roster = new List<string>();

        private bool hasIndex;
        private int lastIndex;
        private float now;

        private float myBullAt = float.NegativeInfinity;
        private readonly Queue<(string shooterId, long fireTick)> pendingBulls = new Queue<(string, long)>();

        //  정색 컷인(슬라이스 3) — 라운드 동안 후보를 모으고, 결과가 보이기 시작하는 프레임에 하나를 띄운다.
        private readonly ArcheryCutInFreeze freeze;
        private readonly ArcheryCutInCollector cutIns = new ArcheryCutInCollector();
        private readonly List<string> roundBulls = new List<string>();
        private readonly ArcheryCutInTrigger cutInTrigger = new ArcheryCutInTrigger();
        private readonly ArcheryShootOffTotals totals = new ArcheryShootOffTotals();
        private int cutInsUsed;
        private float cutInAt = float.NegativeInfinity;

        public bool CutInVisible { get; private set; }
        public float CutInSlideX { get; private set; }
        public Vector2 CutInShake { get; private set; }
        public string CutInShout { get; private set; } = string.Empty;
        public string CutInName { get; private set; } = string.Empty;
        public Color CutInSkin { get; private set; } = Color.white;
        public Color CutInHair { get; private set; } = Color.white;

        public Camera Camera => cameraController != null ? cameraController.MainCamera : null;
        public float FlashAlpha { get; private set; }

        public string RoundLabel { get; private set; } = string.Empty;

        /// <summary>−1~1. 부호 = 방향(양수 = 사수 기준 오른쪽으로 민다), 크기 = 세기.</summary>
        public float WindArrow { get; private set; }

        public string WindText { get; private set; } = string.Empty;

        /// <summary>이 라운드에 쏠 수 있는 시간이 얼마나 남았나(1 → 0).</summary>
        public float TimeLeft01 { get; private set; }

        public string Caption => commentary.IsShowing(now) ? commentary.Text : string.Empty;

        public ArcheryShootOffHudViewModel(GameFramework.Runner.IRunner runner, ArcheryWorld world,
                                           ArcheryCourse course, IGameDataStore gameDataStore,
                                           GameFramework.World.EntityRegistry entityRegistry,
                                           ISubscriber<WorldEventBatchToC> batchSubscriber,
                                           ArcheryConfig config,
                                           ArcheryShootOffResultTracker resultTracker,
                                           CameraController cameraController,
                                           ArcheryArrowStickSystem stickSystem,
                                           ArcheryArrowLandings landings, ArcheryChickenView chickenView,
                                           ArcheryCutInFreeze freeze)
        {
            this.freeze = freeze;
            this.runner = runner;
            this.config = config;
            this.world = world;
            this.course = course;
            this.gameDataStore = gameDataStore;
            this.entityRegistry = entityRegistry;
            this.resultTracker = resultTracker;
            this.cameraController = cameraController;
            this.stickSystem = stickSystem;
            subscription = batchSubscriber.Subscribe(OnWorldEventBatch);
            this.landings = landings;
            this.chickenView = chickenView;
            landings.Landed += OnLanded;
            chickenView.Scared += OnChickenScared;
        }

        /// <summary>새로 들어온 남의 10점 하나를 꺼낸다(내 것은 패드가 띄운다). 꽂힌 자리를 아직 모르면 건너뛴다.</summary>
        public bool TryTakeBull(out Vector3 worldPosition, out Color color)
        {
            while (pendingBulls.Count > 0)
            {
                var (shooterId, fireTick) = pendingBulls.Dequeue();
                if (stickSystem.TryGetImpactWorldPosition(shooterId, fireTick, out worldPosition))
                {
                    color = ColorOf(shooterId);
                    return true;
                }
            }
            worldPosition = default;
            color = default;
            return false;
        }

        public void Tick(float now)
        {
            this.now = now;
            float sinceBull = now - myBullAt;
            FlashAlpha = ArcheryBullseyeFx.FlashAlpha(sinceBull);
            if (cameraController != null)
            {
                cameraController.ShakeOffset = ArcheryBullseyeFx.ShakeOffset(sinceBull);
            }
            if (runner?.tickUpdater == null || runner.tickUpdater.interval <= 0d)
            {
                ResultVisible = false;
                IntroAlpha = 0f;
                return;
            }

            double interval = runner.tickUpdater.interval;
            //  화면에 그리는 시각 — ArcheryTargetView와 같은 식이다(내 캐릭터를 그리는 시각).
            double renderTick = (runner.tickUpdater.elapsedTime - interval) / interval;
            long tick = (long)Math.Floor(renderTick);
            long start = world.GameplayStartTick;
            ResultVisible = resultTracker.IsShowing(renderTick);
            SeedTotals();
            UpdateCutIn();
            UpdateIntro(renderTick, start);

            int index = course.IndexAt(tick, start);
            bool inRound = index >= 0 && index < course.StepCount;

            float wind = inRound ? WindAcross(course.WindAt(tick, start)) : 0f;
            WindArrow = Mathf.Clamp(wind / WindArrowFull, -1f, 1f);
            WindText = wind == 0f ? string.Empty : $"바람 {Mathf.Abs(wind):0}";

            if (inRound)
            {
                int multiplier = course.MultiplierAt(index);
                RoundLabel = $"라운드 {index + 1}/{course.StepCount}" + (multiplier > 1 ? $"   점수 {multiplier}배" : "");
                TimeLeft01 = TimeLeftOf(index, start, renderTick, interval);
            }
            else
            {
                RoundLabel = string.Empty;
                TimeLeft01 = 0f;
            }

            if (hasIndex == false)
            {
                //  처음 본 라운드는 알리지 않는다 — 판 중간·끝에 들어온 사람에게 지난 말을 하지 않게.
                hasIndex = true;
                lastIndex = index;
                return;
            }
            if (index != lastIndex)
            {
                OnRoundChanged(index, wind);
                lastIndex = index;
            }
        }

        public bool ResultVisible { get; private set; }

        /// <summary>라운드 소개 카드가 화면을 얼마나 덮나(0~1). 자리가 바뀌는 틱에 1이다.</summary>
        public float IntroAlpha { get; private set; }
        public string IntroTitle { get; private set; } = string.Empty;
        public string IntroDetail { get; private set; } = string.Empty;

        private readonly List<long> seatChangeTicks = new List<long>();
        private long seatChangeStart = long.MinValue;
        private int lastSeatRound = -1;

        //  라운드 소개 카드 — 자리가 바뀌는 틱(결과 화면이 닫히는 틱)을 덮어 순간이동을 가린다.
        //  자리가 바뀐 순간 카메라를 새 자리에서 과녁 쪽으로 돌린다.
        private void UpdateIntro(double renderTick, long start)
        {
            IntroAlpha = 0f;
            if (start == long.MaxValue || course.StepCount == 0)
            {
                return;   // 아직 판이 시작 안 했다
            }
            if (seatChangeStart != start)
            {
                seatChangeTicks.Clear();
                for (int i = 0; i < course.StepCount; i++)
                {
                    seatChangeTicks.Add(i == 0 ? start : course.ResultEndTick(i - 1, start));
                }
                seatChangeStart = start;
            }

            int round = ArcheryShootOffIntro.ActiveRound(renderTick, seatChangeTicks);
            if (round >= 0)
            {
                IntroAlpha = ArcheryShootOffIntro.AlphaAt(renderTick, seatChangeTicks[round]);
                IntroTitle = $"라운드 {round + 1} / {course.StepCount}";
                IntroDetail = IntroDetailOf(round, start);
            }

            int seatRound = course.SeatRoundAt((long)Math.Floor(renderTick), start);
            if (seatRound != lastSeatRound)
            {
                lastSeatRound = seatRound;
                FaceTarget(seatRound);
            }
        }

        private string IntroDetailOf(int round, long start)
        {
            long roundStart = course.RoundCloseTick(round, start) - course.ExposureTicksAt(round);
            float wind = WindAcross(course.WindAt(roundStart + 1, start));
            string text = $"{Mathf.RoundToInt(course.StandDistanceAt(round))}m";
            if (Mathf.Abs(wind) >= 0.5f)
            {
                text += $"   바람 {Mathf.Abs(wind):0} {(wind > 0f ? "오른쪽" : "왼쪽")}";
            }
            int multiplier = course.MultiplierAt(round);
            if (multiplier > 1)
            {
                text += $"   점수 {multiplier}배!";
            }
            return text;
        }

        //  옆 자리로 옮겨지면 과녁이 화면 가운데에서 비켜난다 — 새 자리에서 그 라운드 과녁을 보게 돌려 둔다.
        private void FaceTarget(int round)
        {
            var lane = course.SharedLane;
            if (cameraController == null || lane == null || round < 0 || round >= config.Range.Stands.Count)
            {
                return;
            }
            GameFramework.World.Transform mine = null;
            foreach (var entity in entityRegistry.All)
            {
                if (entity.Id == gameDataStore.userEntityId)
                {
                    mine = entity.Get<GameFramework.World.Transform>();
                    break;
                }
            }
            if (mine == null)
            {
                return;
            }
            Vector3 target = lane.Value.Stands[config.Range.Stands[round].StandIndex];
            float yaw = ArcheryShootOffIntro.YawToward(mine.Position.ToUnity(), target);
            cameraController.AimBy(new Vector2(Mathf.DeltaAngle(cameraController.Yaw, yaw), 0f));
        }
        public int ResultVersion => resultTracker.Version;
        public IReadOnlyList<ArcheryRoundPlacement> ResultByRank => resultTracker.ByRank;
        public int ResultMultiplier => resultTracker.Current?.multiplier ?? 1;
        public float ResultFaceRadius => resultTracker.Current == null ? 0f : course.FaceRadiusAt(resultTracker.Current.roundIndex);
        public IReadOnlyList<ArcheryRingBand> FaceBands => course.FaceBands;
        public float ResultSeconds => now - resultTracker.OpenedAt;
        public string ResultHeadline => ArcheryShootOffResultLayout.Headline(resultTracker.ByRank, NameOf);

        /// <summary>목록에 쓰는 짧은 이름 — 나는 "나", 남은 "{n}P".</summary>
        public string ResultName(string entityId)
        {
            if (entityId == gameDataStore.userEntityId)
            {
                return "나";
            }
            string full = NameOf(entityId);
            return full.EndsWith(" 선수") ? full.Substring(0, full.Length - 3) : full;
        }

        public Color ColorOf(string entityId)
        {
            roster.Clear();
            foreach (var entity in entityRegistry.All)
            {
                if (entity.Has<ArcheryScore>())
                {
                    roster.Add(entity.Id);
                }
            }
            return ArcheryShootOffResultLayout.ColorOf(ArcheryShootOffResultLayout.Roster(roster), entityId);
        }

        private void OnRoundChanged(int index, float wind)
        {
            if (index >= 0 && index < course.StepCount)
            {
                if (Mathf.Abs(wind) >= StrongWind)
                {
                    commentary.TrySay(ArcheryLine.Wind, wind > 0f ? "오른" : "왼", 0, now);
                }
                if (course.MultiplierAt(index) >= 2)
                {
                    commentary.TrySay(ArcheryLine.Hush, string.Empty, 0, now);
                }
                return;
            }

            if (index >= course.StepCount && lastIndex >= 0 && lastIndex < course.StepCount)
            {
                string champion = TopScorer();
                if (champion != null)
                {
                    commentary.TrySay(ArcheryLine.Final, NameOf(champion), 0, now);
                }
            }
        }

        //  마지막으로 쏠 수 있는 틱 = 가장 약하게 쏜 화살도 과녁이 사라지기 전에 닿는 시각(스펙 §3.5).
        private float TimeLeftOf(int index, long start, double renderTick, double interval)
        {
            long close = course.RoundCloseTick(index, start);
            //  경계에 딱 맞춰 끝나면 막대가 비는 순간 쏜 화살이 이미 늦다 — 두 틱 먼저 끝낸다.
            double lastShot = close - course.StandDistanceAt(index) / config.ArrowMinSpeed / interval
                            - LastShotMarginTicks;
            double roundStart = close - course.ExposureTicksAt(index);
            double span = lastShot - roundStart;
            if (span <= 0d)
            {
                return 0f;
            }
            return Mathf.Clamp01((float)((lastShot - renderTick) / span));
        }

        private float WindAcross(Vector3 wind)
        {
            var lane = course.SharedLane;
            if (lane.HasValue == false)
            {
                return 0f;
            }
            return Vector3.Dot(wind, ArcheryTargetMotion.ShooterRightAxis(-lane.Value.Forward));
        }

        private void OnWorldEventBatch(WorldEventBatchToC msg)
        {
            foreach (var rec in msg.Events)
            {
                if (rec.EventCase == WorldEventToC.EventOneofCase.ArcheryRoundResult)
                {
                    OnRoundResult((ArcheryRoundResultEvent)WorldEventWire.FromWire(rec));
                }
                else if (rec.EventCase == WorldEventToC.EventOneofCase.ArcheryHit)
                {
                    //  한 발 승부에서 이 값은 띠 점수(1~10)다 — 점수로 더해지지 않는다.
                    var hit = (ArcheryTargetHitEvent)WorldEventWire.FromWire(rec);
                    if (hit.points == 10)
                    {
                        commentary.TrySay(ArcheryLine.Bull, NameOf(hit.shooterId), 0, now);
                        roundBulls.Add(hit.shooterId);
                        if (hit.shooterId == gameDataStore.userEntityId)
                        {
                            myBullAt = now;   // 내 10점 — 흔들림·번쩍임
                        }
                        else
                        {
                            pendingBulls.Enqueue((hit.shooterId, hit.fireTick));   // 남의 10점 — "10!!"만(조준을 흔들지 않는다)
                        }
                    }
                }
            }
        }

        private void OnRoundResult(ArcheryRoundResultEvent result)
        {
            var line = narrator.LineFor(result, gameDataStore.userEntityId, out string subject, out int number);
            commentary.TrySay(line, NameOf(subject), number, now);

            //  10점 이벤트엔 순위가 없다 — 결과의 순위로 가장 잘한 10점 사수를 고른다.
            foreach (var id in roundBulls)
            {
                foreach (var p in result.placements)
                {
                    if (p.ShooterId == id)
                    {
                        cutIns.OnBull(id, p.Rank);
                    }
                }
            }
            roundBulls.Clear();
            string comeback = line == ArcheryLine.Comeback ? subject : null;
            totals.Add(result.placements, id => entityRegistry.Get(id)?.Get<ArcheryScore>()?.Value ?? 0);
            string last = result.roundIndex == course.StepCount - 1 ? totals.Bottom() : null;
            cutInTrigger.OnResult(ArcheryCutInPicker.Pick(cutIns.Take(comeback, last), cutInsUsed, drawing: false));
        }

        //  결과가 보이기 시작하는 프레임에 한 번 — 당기는 중이면 버린다(조작 중엔 절대 안 뜬다, ArcheryCutInTrigger).
        private void UpdateCutIn()
        {
            var pick = cutInTrigger.Tick(ResultVisible, IsMeDrawing(), cutInsUsed);
            if (pick.Kind != ArcheryCutInKind.None)
            {
                cutInAt = now;
                cutInsUsed++;
                CutInShout = ArcheryGrandTitles.Shout(pick.Kind);
                CutInName = ArcheryGrandTitles.Name(pick.Kind, n => UnityEngine.Random.Range(0, n));
                var colors = ChibiOutfit.ColorsFor(pick.SubjectId);
                CutInSkin = colors.Skin;
                CutInHair = colors.Hair;
            }
            float t = now - cutInAt;
            CutInVisible = ArcheryCutInTimeline.IsActive(t);
            CutInSlideX = ArcheryCutInTimeline.SlideX(t);
            CutInShake = ArcheryCutInTimeline.Shake(t);
            if (freeze != null)
            {
                freeze.Weight = ArcheryCutInTimeline.Freeze(t);
            }
        }

        //  사수가 처음 보이는 프레임의 점수가 총점 출발점(판 시작이면 0).
        private void SeedTotals()
        {
            foreach (var entity in entityRegistry.All)
            {
                var score = entity.Get<ArcheryScore>();
                if (score != null)
                {
                    totals.Seed(entity.Id, score.Value);
                }
            }
        }

        private bool IsMeDrawing()
            => entityRegistry.Get(gameDataStore.userEntityId)?.Get<ArcheryAim>()?.Drawing == true;

        private void OnLanded(ArcheryArrowLanding landing)
        {
            if (landing.Kind == ArcheryLandingKind.Crowd)
            {
                commentary.TrySay(ArcheryLine.CrowdHit, NameOf(landing.ShooterId), 0, now);
            }
            else if (landing.Kind == ArcheryLandingKind.Target && landing.SplitArrow)
            {
                commentary.TrySay(ArcheryLine.RobinHood, NameOf(landing.ShooterId), 0, now);
                cutIns.OnRobinHood(landing.ShooterId);
            }
        }

        private void OnChickenScared(string shooterId) => commentary.TrySay(ArcheryLine.Chicken, NameOf(shooterId), 0, now);

        /// <summary>
        /// 나면 "당신", 남이면 "{n}P 선수". 클라 엔티티엔 표시 이름이 없어서 <b>엔티티 id 서수 순서</b>로
        /// 번호를 매긴다(나 포함) — 어느 클라에서 봐도 같은 사람이 같은 번호이고, 화면 좌우 배치
        /// (<see cref="ArcheryShootOffSeats"/>의 명단)와 같은 순서다.
        /// </summary>
        private string NameOf(string entityId)
        {
            if (entityId == gameDataStore.userEntityId)
            {
                return "당신";
            }

            roster.Clear();
            foreach (var entity in entityRegistry.All)
            {
                if (entity.Has<ArcheryScore>())
                {
                    roster.Add(entity.Id);
                }
            }
            roster.Sort(string.CompareOrdinal);
            int at = roster.IndexOf(entityId);
            return at >= 0 ? $"{at + 1}P 선수" : "선수";
        }

        //  점수 1등. 동점이면 id 서수가 앞선 사람.
        private string TopScorer()
        {
            string best = null;
            int bestScore = int.MinValue;
            foreach (var entity in entityRegistry.All)
            {
                var score = entity.Get<ArcheryScore>();
                if (score == null)
                {
                    continue;
                }
                if (score.Value > bestScore
                    || (score.Value == bestScore && string.CompareOrdinal(entity.Id, best) < 0))
                {
                    best = entity.Id;
                    bestScore = score.Value;
                }
            }
            return best;
        }

        public void Dispose()
        {
            if (freeze != null)
            {
                freeze.Weight = 0f;
            }
            subscription?.Dispose();
            landings.Landed -= OnLanded;
            chickenView.Scared -= OnChickenScared;
            if (cameraController != null)
            {
                cameraController.ShakeOffset = Vector3.zero;
            }
        }
    }
}
