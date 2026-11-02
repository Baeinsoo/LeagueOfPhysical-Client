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
                                     GameFramework.World.EntityRegistry entityRegistry)
        {
            this.runner = runner;
            this.world = world;
            this.config = config;
            this.state = state;
            this.gameDataStore = gameDataStore;
            this.entityRegistry = entityRegistry;
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
            if (s.index >= lastStageIndex && lastStageIndex > 0) return hazardEdge ? "마지막 — 전부 위험 지대" : "마지막 원";
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
            aliveText.Dispose();
            zoneText.Dispose();
            myDamageText.Dispose();
            myDamageColor.Dispose();
        }
    }
}
