using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

namespace LOP.UI
{
    /// <summary>
    /// 넉백 판 정보 표시: 남은 사람, 원 안내 문구, 내 위험도 %.
    /// 시간 흐름은 <see cref="Tick"/>이 만든다 — 화면(View)이 매 프레임 불러 준다.
    /// </summary>
    public class KnockbackHudViewModel : IDisposable
    {
        private readonly GameFramework.Runner.IRunner runner;
        private readonly KnockbackWorld world;
        private readonly KnockbackConfig config;
        private readonly KnockbackClientState state;
        private readonly IGameDataStore gameDataStore;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly KnockbackKoBroadcaster koBroadcaster;

        private const float KoLineSeconds = 3f;
        private const int KoLineMax = 3;
        private readonly List<(string text, float expireAt)> koLines = new List<(string, float)>();
        private readonly ReactiveProperty<string> koText = new ReactiveProperty<string>("");
        private readonly ReactiveProperty<string> finishText = new ReactiveProperty<string>("");

        /// <summary>중계 자막. 새 줄이 위, 줄바꿈으로 이어 붙인 색 글자(rich text).</summary>
        public ReadOnlyReactiveProperty<string> KoText => koText;
        public ReadOnlyReactiveProperty<string> FinishText => finishText;

        private readonly ReactiveProperty<string> aliveText = new ReactiveProperty<string>("");
        private readonly ReactiveProperty<string> zoneText = new ReactiveProperty<string>("");
        private readonly ReactiveProperty<string> myDamageText = new ReactiveProperty<string>("");
        private readonly ReactiveProperty<Color> myDamageColor = new ReactiveProperty<Color>(Color.white);

        public ReadOnlyReactiveProperty<string> AliveText => aliveText;
        public ReadOnlyReactiveProperty<string> ZoneText => zoneText;

        /// <summary>내 몸이 없으면(탈락·관전) 빈 글자.</summary>
        public ReadOnlyReactiveProperty<string> MyDamageText => myDamageText;
        public ReadOnlyReactiveProperty<Color> MyDamageColor => myDamageColor;

        public KnockbackHudViewModel(GameFramework.Runner.IRunner runner, KnockbackWorld world, KnockbackConfig config,
                                     KnockbackClientState state, IGameDataStore gameDataStore,
                                     GameFramework.World.EntityRegistry entityRegistry,
                                     KnockbackKoBroadcaster koBroadcaster)
        {
            this.runner = runner;
            this.world = world;
            this.config = config;
            this.state = state;
            this.gameDataStore = gameDataStore;
            this.entityRegistry = entityRegistry;
            this.koBroadcaster = koBroadcaster;
            koBroadcaster.Ko += OnKo;
            koBroadcaster.Finished += OnFinished;
        }

        private void OnFinished(Vector3? _) => finishText.Value = "경기 종료!";

        private void OnKo(KoAnnouncement a) => AddLine(KoLine(a), KoLineSeconds);

        private void AddLine(string text, float seconds)
        {
            koLines.Insert(0, (text, Time.unscaledTime + seconds));
            if (koLines.Count > KoLineMax) koLines.RemoveRange(KoLineMax, koLines.Count - KoLineMax);
            RenderKo();
        }

        public const string CrateNoticeText = "보급 상자! 주우면 다음 탄이 충격파 폭탄";
        private const float CrateNoticeSeconds = 5f;
        private bool crateAnnounced;

        public const string CrackNoticeText = "금 간 바닥 — 사거리 끝 탄 두 발이면 칸이 무너진다";
        private const float CrackNoticeSeconds = 6f;
        private bool crackAnnounced;

        /// <summary>금 가는 바닥 맵에서 처음 한 번만 안내한다.</summary>
        public static bool ShouldAnnounceCrack(bool alreadyAnnounced, bool crackEnabled) => crackEnabled && !alreadyAnnounced;

        /// <summary>첫 상자가 예고될 때 한 번만 안내한다.</summary>
        public static bool ShouldAnnounceCrate(bool alreadyAnnounced, bool crateExists) => crateExists && !alreadyAnnounced;

        private void RenderKo()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < koLines.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(koLines[i].text);
            }
            koText.Value = sb.ToString();
        }

        /// <summary>문장에 팀 색을 입힌다. 이름 속 꺾쇠는 rich text로 읽히지 않게 뺀다.</summary>
        public static string KoLine(KoAnnouncement a)
        {
            string line = KnockbackKoFeed.Line(
                a.KillerName == null ? null : Colorize(a.KillerName, a.KillerColor),
                Colorize(a.VictimName, a.VictimColor), a.Chain, a.Streak);
            return line;
        }

        private static string Colorize(string name, Color? color)
        {
            string safe = name.Replace("<", "").Replace(">", "");
            return color.HasValue ? $"<color=#{ColorUtility.ToHtmlStringRGB(color.Value)}>{safe}</color>" : safe;
        }

        /// <summary>
        /// 원 안내 문구. 정수 틱 하나로 정한다. 다음 단계 공개는 앞 단계 축소가 끝나는 순간이라 단계 사이에 빈 틈은 없다.
        /// </summary>
        public static string ZoneTextFor(IReadOnlyList<ZoneStage> stages, long tick, float interval,
                                         long firstRevealTick, int lastStageIndex, bool hazardEdge = false)
        {
            ZoneStage? latest = null;
            for (int i = stages.Count - 1; i >= 0; i--)
            {
                if (stages[i].revealTick <= tick) { latest = stages[i]; break; }
            }
            if (latest == null)
            {
                if (firstRevealTick == long.MaxValue) return "";
                return $"원 공개까지 {SecondsLeft(firstRevealTick - tick, interval)}초";
            }
            var s = latest.Value;
            if (tick < s.shrinkStartTick) return $"원이 줄어들기까지 {SecondsLeft(s.shrinkStartTick - tick, interval)}초";
            if (s.index >= lastStageIndex && lastStageIndex > 0)
            {
                if (!hazardEdge) return "마지막 원";
                // 마지막 축소가 끝난 뒤에는 바닥이 바깥부터 무너진다(서든데스).
                if (tick >= s.shrinkEndTick) return "바닥이 무너집니다 — 가운데로!";
                // 반지름이 0까지 가는 축소 중일 때만 "전부"가 사실이다.
                if (s.toRadius <= 0f) return "마지막 — 전부 위험 지대";
            }
            // 축소가 끝나도 다음 단계 메시지가 오기 전 한두 틱은 같은 문구 — 깜박이지 않게.
            return hazardEdge ? "원이 줄어듭니다 — 바깥은 위험 지대" : "원이 줄어듭니다 — 바깥 칸이 무너져요";
        }

        private static int SecondsLeft(long ticks, float interval)
            => Mathf.Max(0, Mathf.CeilToInt(ticks * interval - 1e-4f));

        /// <summary>첫 원이 공개될 틱. 판이 아직 시작하지 않았으면 long.MaxValue. 서버 KnockbackZoneSystem과 같은 정의.</summary>
        private long FirstRevealTick(double interval)
        {
            long start = world.GameplayStartTick;
            return start == long.MaxValue
                ? long.MaxValue
                : start + (long)System.Math.Ceiling(config.ZoneFirstRevealSeconds / interval);
        }

        public static string TeamAliveText(int mine, int theirs) => $"우리 팀 {mine} · 상대 {theirs}";

        private string AliveTextNow()
        {
            if (state.PlayerCount == 0) return "";
            int team = state.TeamCount >= 2 ? state.MyTeamOf(gameDataStore?.userEntityId, gameDataStore?.formerEntityId) : 0;
            return team == 0
                ? $"남은 사람 {state.AliveCount}"
                : TeamAliveText(state.AliveCountOfTeam(team), state.AliveCountOfOtherTeams(team));
        }

        public void Tick()
        {
            if (koLines.RemoveAll(l => Time.unscaledTime >= l.expireAt) > 0) RenderKo();

            if (ShouldAnnounceCrate(crateAnnounced, state.Crate.HasValue))
            {
                crateAnnounced = true;
                AddLine(CrateNoticeText, CrateNoticeSeconds);
            }

            if (ShouldAnnounceCrack(crackAnnounced, config.CrackEnabled))
            {
                crackAnnounced = true;
                AddLine(CrackNoticeText, CrackNoticeSeconds);
            }

            aliveText.Value = AliveTextNow();

            var tickUpdater = runner?.tickUpdater;
            if (tickUpdater != null && tickUpdater.interval > 0d)
            {
                double interval = tickUpdater.interval;
                double renderTick = (tickUpdater.elapsedTime - interval) / interval;
                zoneText.Value = ZoneTextFor(state.ZoneStages, (long)System.Math.Floor(renderTick), (float)interval,
                                             FirstRevealTick(interval), config.ZoneStages.Count, config.EdgeMode == 1);
            }

            string myId = gameDataStore?.userEntityId;
            var body = string.IsNullOrEmpty(myId) ? null : entityRegistry?.Get(myId)?.Get<KnockbackBody>();
            if (body == null)
            {
                myDamageText.Value = "";
                return;
            }
            myDamageText.Value = $"{body.DamagePercent}%";
            myDamageColor.Value = KnockbackDamageLabel.ColorFor(body.DamagePercent);
        }

        public void Dispose()
        {
            koBroadcaster.Ko -= OnKo;
            koBroadcaster.Finished -= OnFinished;
            koText.Dispose();
            finishText.Dispose();
            aliveText.Dispose();
            zoneText.Dispose();
            myDamageText.Dispose();
            myDamageColor.Dispose();
        }
    }
}
