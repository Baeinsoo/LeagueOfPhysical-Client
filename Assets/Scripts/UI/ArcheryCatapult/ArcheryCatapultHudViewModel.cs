using System;
using System.Collections.Generic;
using GameFramework;
using MessagePipe;
using UnityEngine;

namespace LOP.UI
{
    /// <summary>
    /// 투석기 사냥 HUD — 라운드 소개(내 자리), "준비!", 남은 시간, 라운드 결과판, "OO +3" 팝업, 사격 금지.
    /// 무리·라운드 시각은 <see cref="ArcheryCatapultSchedule"/>이, 점수는 스냅샷이, 누가 맞혔는지는 적중 사건이 준다.
    /// View가 매 프레임 <see cref="Tick"/>을 부르고 값을 읽어 간다(한 발 승부 HUD와 같은 꼴).
    /// </summary>
    public sealed class ArcheryCatapultHudViewModel : IDisposable
    {
        public readonly struct Row
        {
            public readonly string Name;
            public readonly int Total;
            public readonly int Gain;
            public readonly Color Color;
            public Row(string name, int total, int gain, Color color) { Name = name; Total = total; Gain = gain; Color = color; }
        }

        private readonly GameFramework.Runner.IRunner runner;
        private readonly ArcheryWorld world;
        private readonly ArcheryCourse course;
        private readonly ArcheryConfig config;
        private readonly IGameDataStore gameDataStore;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly CameraController cameraController;
        private readonly LOP.MasterData.LOPMasterData md;
        private readonly IDisposable subscription;

        private readonly List<string> roster = new List<string>();
        private readonly List<Row> rows = new List<Row>();
        private readonly List<ArcheryTarget> scratch = new List<ArcheryTarget>();
        private readonly Queue<(Vector3 world, Color color, string text, ArcheryCatapultPopupStyle style)> popups
            = new Queue<(Vector3, Color, string, ArcheryCatapultPopupStyle)>();
        private readonly Dictionary<string, int> roundStartScore = new Dictionary<string, int>();
        private int baselineRound = -1;

        //  내 사격 금지 — 서버가 적중 사건에 실어 보낸 끝 틱과 그 길이(막대 비율의 분모).
        private long myStunUntilTick;
        private long myStunTicks;

        public string RoundLabel { get; private set; } = string.Empty;
        public float TimeLeft01 { get; private set; }
        public bool CueVisible { get; private set; }
        public bool IntroVisible { get; private set; }
        public string IntroTitle { get; private set; } = string.Empty;
        public string IntroDetail { get; private set; } = string.Empty;
        public bool ResultVisible { get; private set; }
        public IReadOnlyList<Row> ResultRows => rows;
        public bool StunVisible { get; private set; }
        public float StunLeft01 { get; private set; }
        public Camera Camera => cameraController != null ? cameraController.MainCamera : null;

        public ArcheryCatapultHudViewModel(GameFramework.Runner.IRunner runner, ArcheryWorld world, ArcheryCourse course,
                                           ArcheryConfig config, IGameDataStore gameDataStore,
                                           GameFramework.World.EntityRegistry entityRegistry,
                                           CameraController cameraController,
                                           LOP.MasterData.LOPMasterData md,
                                           ISubscriber<WorldEventBatchToC> batchSubscriber)
        {
            this.runner = runner;
            this.world = world;
            this.course = course;
            this.config = config;
            this.gameDataStore = gameDataStore;
            this.entityRegistry = entityRegistry;
            this.cameraController = cameraController;
            this.md = md;
            subscription = batchSubscriber.Subscribe(OnWorldEventBatch);
        }

        public void Tick(float now)
        {
            if (runner?.tickUpdater == null || runner.tickUpdater.interval <= 0d)
            {
                IntroVisible = ResultVisible = CueVisible = StunVisible = false;
                return;
            }
            double interval = runner.tickUpdater.interval;
            //  화면에 그리는 시각 — ArcheryTargetView와 같은 식이다.
            long tick = (long)Math.Floor((runner.tickUpdater.elapsedTime - interval) / interval);
            long start = world.GameplayStartTick;
            var s = config.Catapult;
            FillRoster();

            var phase = ArcheryCatapultSchedule.PhaseAt(s, tick, start, out int round);
            int shown = Mathf.Max(round, 0);
            RoundLabel = ArcheryCatapultHudText.RoundLabel(shown, s.RoundCount);

            if (phase == ArcheryCatapultPhase.Intro && baselineRound != round)
            {
                RememberRoundStart(round);
            }

            IntroVisible = phase == ArcheryCatapultPhase.Intro;
            if (IntroVisible)
            {
                IntroTitle = "라운드 " + (round + 1);
                IntroDetail = ArcheryCatapultHudText.SeatText(MySlot(tick, start));
            }

            if (phase == ArcheryCatapultPhase.Hunt)
            {
                long huntEnd = ArcheryCatapultSchedule.HuntEndTick(s, shown, start);
                TimeLeft01 = Mathf.Clamp01((huntEnd - tick) / (float)s.HuntTicks);
            }
            else
            {
                TimeLeft01 = phase == ArcheryCatapultPhase.Intro ? 1f : 0f;
            }

            int volley = ArcheryCatapultSchedule.VolleyIndexAt(s, tick, start);
            CueVisible = phase == ArcheryCatapultPhase.Hunt && volley >= 0 && volley < s.VolleyCount
                      && tick < ArcheryCatapultSchedule.LaunchTick(s, volley, start);

            ResultVisible = phase == ArcheryCatapultPhase.Result || phase == ArcheryCatapultPhase.Over;
            if (ResultVisible)
            {
                BuildRows();
            }

            //  사격 금지는 "이 틱까지(포함)" — 막대는 사건이 알려 준 길이에 대한 남은 비율이다.
            StunVisible = myStunTicks > 0 && tick <= myStunUntilTick;
            StunLeft01 = StunVisible ? Mathf.Clamp01((myStunUntilTick - tick) / (float)myStunTicks) : 0f;
        }

        public bool TryTakePopup(out Vector3 worldPosition, out Color color, out string text, out ArcheryCatapultPopupStyle style)
        {
            if (popups.Count == 0)
            {
                worldPosition = default; color = default; text = null; style = default;
                return false;
            }
            (worldPosition, color, text, style) = popups.Dequeue();
            return true;
        }

        private void OnWorldEventBatch(WorldEventBatchToC msg)
        {
            bool rosterFilled = false;
            foreach (var rec in msg.Events)
            {
                if (rec.EventCase != WorldEventToC.EventOneofCase.ArcheryHit)
                {
                    continue;
                }
                var hit = (ArcheryTargetHitEvent)WorldEventWire.FromWire(rec);
                if (hit.shooterId == gameDataStore.userEntityId && hit.stunUntilTick > 0 && hit.stunUntilTick > hit.hitTick)
                {
                    myStunUntilTick = hit.stunUntilTick;
                    myStunTicks = hit.stunUntilTick - hit.hitTick;
                }
                if (runner?.tickUpdater == null
                    || ArcheryCatapultHits.TryLocate(course, world.GameplayStartTick, (float)runner.tickUpdater.interval,
                                                     hit, scratch, out var target, out var at) == false)
                {
                    continue;
                }
                if (rosterFilled == false)
                {
                    FillRoster();
                    rosterFilled = true;
                }
                var style = target.IsJackpot ? ArcheryCatapultPopupStyle.Jackpot : ArcheryCatapultPopupStyle.Normal;
                popups.Enqueue((at, ColorOf(hit.shooterId), ArcheryCatapultHudText.PopupText(NameOf(hit.shooterId), hit.points), style));
                //  종은 "땡~"을 금색으로 위에 하나 더 — 소리를 끄고 하는 사람이 많다(spec 2차 §4, 시각 우선).
                string code = md.Tables.TbArcheryTarget.GetOrDefault(target.KindId)?.Code;
                if (ArcheryHitFxRecipe.For(code).Ding)
                {
                    popups.Enqueue((at + Vector3.up * 1.2f, new Color(1f, 0.84f, 0.3f), ArcheryCatapultHudText.DingText, ArcheryCatapultPopupStyle.Ding));
                }
            }
        }

        private int MySlot(long tick, long start)
        {
            int me = roster.IndexOf(gameDataStore.userEntityId);
            return me < 0 ? 0 : course.SeatSlotAt(me, roster.Count, tick, start);
        }

        private void RememberRoundStart(int round)
        {
            roundStartScore.Clear();
            foreach (var entity in entityRegistry.All)
            {
                var score = entity.Get<ArcheryScore>();
                if (score != null) { roundStartScore[entity.Id] = score.Value; }
            }
            baselineRound = round;
        }

        private void BuildRows()
        {
            rows.Clear();
            foreach (var id in roster)
            {
                int total = entityRegistry.Get(id)?.Get<ArcheryScore>()?.Value ?? 0;
                int gain = total - (roundStartScore.TryGetValue(id, out var before) ? before : 0);
                rows.Add(new Row(NameOf(id), total, gain, ColorOf(id)));
            }
            //  점수 높은 순, 같으면 이름 순 — List.Sort는 안정 정렬이 아니라 끝까지 갈라야 줄이 프레임마다 안 흔들린다.
            rows.Sort((a, b) => b.Total != a.Total ? b.Total.CompareTo(a.Total) : string.CompareOrdinal(a.Name, b.Name));
        }

        //  명단(점수가 붙은 엔티티, id 순). Tick·사건 묶음마다 한 번 채우고, 이름·색은 그 명단을 읽기만 한다.
        private void FillRoster()
        {
            roster.Clear();
            foreach (var entity in entityRegistry.All)
            {
                if (entity.Has<ArcheryScore>()) { roster.Add(entity.Id); }
            }
            roster.Sort(string.CompareOrdinal);
        }

        private string NameOf(string entityId)
            => entityId == gameDataStore.userEntityId ? "나" : ArcheryShootOffResultLayout.NameOf(entityRegistry, roster, entityId);

        private Color ColorOf(string entityId) => ArcheryShootOffResultLayout.ColorOf(roster, entityId);

        public void Dispose() => subscription?.Dispose();
    }
}
