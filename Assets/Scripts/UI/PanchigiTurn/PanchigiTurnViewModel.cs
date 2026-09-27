using UnityEngine;
using System.Collections.Generic;
using GameFramework.Runner;

namespace LOP.UI
{
    /// <summary>
    /// 내 차례인지와 남은 시간, 그리고 볼링 점수판. 남은 시간은 서버가 보내 준 *마감 틱*에서
    /// 매 프레임 계산한다 — 초마다 메시지를 받을 필요가 없다. 뒤집힌 개수도 마찬가지로 매 프레임
    /// 동전 자세에서 직접 센다 — 동전 회전은 이미 스냅샷으로 들어오므로 따로 받을 것이 없다.
    /// </summary>
    public class PanchigiTurnViewModel
    {
        private readonly PanchigiStateStore store;
        private readonly IGameDataStore gameDataStore;
        private readonly IRunner runner;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly LOP.MasterData.LOPMasterData masterData;
        private readonly PanchigiStrikeInput strikeInput;

        public PanchigiTurnViewModel(PanchigiStateStore store, IGameDataStore gameDataStore, IRunner runner,
            GameFramework.World.EntityRegistry entityRegistry, LOP.MasterData.LOPMasterData masterData,
            PanchigiStrikeInput strikeInput)
        {
            this.store = store;
            this.gameDataStore = gameDataStore;
            this.runner = runner;
            this.entityRegistry = entityRegistry;
            this.masterData = masterData;
            this.strikeInput = strikeInput;
        }

        public string Label()
        {
            string me = gameDataStore.userEntityId;
            if (store.IsFinished(me, FrameCount, Pins))
            {
                return $"끝 · {PanchigiBowlingScore.Total(store.Rolls(me), FrameCount, Pins)}점";
            }

            if (store.IsAiming == false)
            {
                return "동전이 멈추는 중";
            }

            if (store.CurrentEntityId.CurrentValue != me)
            {
                return "다른 사람 차례";
            }

            var at = PanchigiBowlingScore.Locate(store.Rolls(me), FrameCount, Pins);
            return $"내 차례 · {at.Frame + 1}프레임 {at.RollInFrame + 1}번째 · {RemainingSeconds()}";
        }

        public sealed class ScoreRow
        {
            public string Name;
            public Color Color;
            public bool Current;
            public IReadOnlyList<PanchigiFrameView> Frames;
            public int Total;
        }

        //  핀 = 판에 놓인 동전 수 — 4인 판은 8개다. 서버도 동전 수로 센다.
        private int Pins => PanchigiCoin.PinCount(entityRegistry.All, fallback: 6);
        private int FrameCount => masterData.Tables.TbPanchigiConfig.GetOrDefault(1)?.FrameCount ?? 5;

        /// <summary>점수판 — 참가 순서대로 한 줄씩.</summary>
        public IReadOnlyList<ScoreRow> Rows()
        {
            var rows = new List<ScoreRow>();
            IReadOnlyList<string> ids = store.PlayerEntityIds;
            for (int i = 0; i < ids.Count; i++)
            {
                IReadOnlyList<PanchigiRoll> rolls = store.Rolls(ids[i]);
                rows.Add(new ScoreRow
                {
                    Name = ids[i] == gameDataStore.userEntityId ? "나" : "상대",
                    Color = PanchigiPlayerColors.For(i),
                    Current = ids[i] == store.BoardOwnerEntityId,
                    Frames = PanchigiBowlingScore.Frames(rolls, FrameCount, Pins),
                    Total = PanchigiBowlingScore.Total(rolls, FrameCount, Pins),
                });
            }
            return rows;
        }

        /// <summary>게이지를 띄울 때인가 — 내 조준 차례일 때만.</summary>
        public bool IsCharging() => store.IsAimingTurnOf(gameDataStore.userEntityId);

        /// <summary>판이 화면에서 차지하는 네모 — 게이지를 그 옆에 붙이는 데 쓴다.</summary>
        public bool TryGetBoardScreenRect(out UnityEngine.Rect rect) => strikeInput.TryGetBoardScreenRect(out rect);

        /// <summary>막대가 얼마나 찼나 — 0~1.</summary>
        public float Charge() => IsCharging() ? strikeInput.Charge : 0f;

        private int RemainingSeconds()
        {
            double interval = runner.tickUpdater?.interval ?? 0;
            if (interval <= 0)
            {
                return 0;
            }

            long left = store.AimDeadlineTick.CurrentValue - runner.tickUpdater.tick;
            return left <= 0 ? 0 : (int)System.Math.Ceiling(left * interval);
        }
    }
}
