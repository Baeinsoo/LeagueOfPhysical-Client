using System;
using System.Collections.Generic;
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
        private const float StrongWind = 15f;
        private const float WindArrowFull = 20f;
        private const double LastShotMarginTicks = 2d;

        private readonly GameFramework.Runner.IRunner runner;
        private readonly ArcheryWorld world;
        private readonly ArcheryCourse course;
        private readonly IGameDataStore gameDataStore;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly IDisposable subscription;

        private readonly ArcheryCommentary commentary = new ArcheryCommentary(n => UnityEngine.Random.Range(0, n));
        private readonly ArcheryShootOffNarrator narrator = new ArcheryShootOffNarrator();
        private readonly List<(string name, int points, string detail)> resultRows =
            new List<(string name, int points, string detail)>();
        private readonly List<string> roster = new List<string>();

        private bool hasIndex;
        private int lastIndex;
        private float now;

        public string RoundLabel { get; private set; } = string.Empty;

        /// <summary>−1~1. 부호 = 방향(양수 = 사수 기준 오른쪽으로 민다), 크기 = 세기.</summary>
        public float WindArrow { get; private set; }

        public string WindText { get; private set; } = string.Empty;

        /// <summary>이 라운드에 쏠 수 있는 시간이 얼마나 남았나(1 → 0).</summary>
        public float TimeLeft01 { get; private set; }

        /// <summary>순위 순서. 이름·이번 라운드 점수·과녁 중심에서 떨어진 거리.</summary>
        public IReadOnlyList<(string name, int points, string detail)> ResultRows => resultRows;

        public bool ResultVisible { get; private set; }

        /// <summary>결과 목록이 새로 채워질 때마다 오른다 — View가 이걸 보고 한 번만 다시 그린다.</summary>
        public int ResultVersion { get; private set; }

        public string Caption => commentary.IsShowing(now) ? commentary.Text : string.Empty;

        public ArcheryShootOffHudViewModel(GameFramework.Runner.IRunner runner, ArcheryWorld world,
                                           ArcheryCourse course, IGameDataStore gameDataStore,
                                           GameFramework.World.EntityRegistry entityRegistry,
                                           ISubscriber<WorldEventBatchToC> batchSubscriber)
        {
            this.runner = runner;
            this.world = world;
            this.course = course;
            this.gameDataStore = gameDataStore;
            this.entityRegistry = entityRegistry;
            subscription = batchSubscriber.Subscribe(OnWorldEventBatch);
        }

        public void Tick(float now)
        {
            this.now = now;
            if (runner?.tickUpdater == null || runner.tickUpdater.interval <= 0d)
            {
                return;
            }

            double interval = runner.tickUpdater.interval;
            //  화면에 그리는 시각 — ArcheryTargetView와 같은 식이다(내 캐릭터를 그리는 시각).
            double renderTick = (runner.tickUpdater.elapsedTime - interval) / interval;
            long tick = (long)Math.Floor(renderTick);
            long start = world.GameplayStartTick;
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

        private void OnRoundChanged(int index, float wind)
        {
            if (index >= 0 && index < course.StepCount)
            {
                ResultVisible = false;
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
            double lastShot = close - course.StandDistanceAt(index) / ArcheryAimSystem.MinSpeed / interval
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
                    }
                }
            }
        }

        private void OnRoundResult(ArcheryRoundResultEvent result)
        {
            resultRows.Clear();
            foreach (var p in result.placements)
            {
                string detail = p.Hit ? $"{Mathf.RoundToInt(p.Distance * 100f)}cm" : "빗나감";
                resultRows.Add((NameOf(p.ShooterId), p.Points, detail));
            }
            ResultVisible = true;
            ResultVersion++;

            var line = narrator.LineFor(result, gameDataStore.userEntityId, out string subject, out int number);
            commentary.TrySay(line, NameOf(subject), number, now);
        }

        /// <summary>
        /// 나면 "당신", 남이면 "{n}P 선수". 클라 엔티티엔 표시 이름이 없어서 <b>엔티티 id 서수 순서</b>로
        /// 번호를 매긴다(나 포함) — 어느 클라에서 봐도 같은 사람이 같은 번호이고, 화면 좌우 배치
        /// (<see cref="ArcheryShootOffLineupView"/>)와 같은 순서다.
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
            subscription?.Dispose();
        }
    }
}
