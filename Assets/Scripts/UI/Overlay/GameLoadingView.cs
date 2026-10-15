using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 로딩판(종목 소개판). 출전 연출의 하얀 빛에서 이어져 경기가 실제로 시작될 때까지 화면을 덮는다(입력 차단).
    /// 일반은 고른 종목을 바로, 랭크·재접속은 경기 정보(match)가 오면 채운다 — 그 저장소는 알림이 없어 0.25초마다 본다.
    /// </summary>
    public class GameLoadingView : UIView
    {
        private const long RefreshMs = 250;
        private const long DotsMs = 400;
        private const long FlashInDelayMs = 16;
        private const long FlashOutDelayMs = 500;

        private readonly IMatchmakingDataStore _matchmaking;
        private readonly IRoomDataStore _room;
        private readonly LOP.MasterData.LOPMasterData _masterData;
        private IVisualElementScheduledItem _refresh;
        private IVisualElementScheduledItem _dots;
        private int _dot;

        public GameLoadingView(IMatchmakingDataStore matchmaking, IRoomDataStore room, LOP.MasterData.LOPMasterData masterData)
        {
            _matchmaking = matchmaking;
            _room = room;
            _masterData = masterData;
        }

        public override UILayer Layer => UILayer.Loading;
        public override bool BlocksUnderlyingInput => true;

        public override void OnOpen()
        {
            base.OnOpen();
            var root = Root.Q("loading-root");
            var flash = Root.Q("loading-flash");
            //  판은 하양을 덮은 채 투명하게 시작한다. 한 프레임 뒤 투명을 풀면(같은 프레임엔 전환이 안 생긴다) 0.35초에
            //  걸쳐 3D 위로 하얀 빛이 차오르고, 0.5초 뒤 하양이 걷히며 판이 드러난다.
            Root.schedule.Execute(() => root.RemoveFromClassList("loading-root--enter")).StartingIn(FlashInDelayMs);
            Root.schedule.Execute(() => flash.RemoveFromClassList("loading-flash--on")).StartingIn(FlashOutDelayMs);
            Refresh();
            _refresh = Root.schedule.Execute(Refresh).Every(RefreshMs);
            _dots = Root.schedule.Execute(AdvanceDots).Every(DotsMs);
        }

        public override void OnClose()
        {
            _refresh?.Pause();
            _dots?.Pause();
            base.OnClose();
        }

        /// <summary>로딩판 글자. 마스터데이터가 아직 없으면(재접속 — 입구 씬) 던지지 않고 "경기장 입장 중…".</summary>
        public static LoadingBoardText ComposeText(int queueId, int gameModeId, int mapId, Match match, LOP.MasterData.Tables tables)
        {
            if (tables == null) return LoadingBoardText.From(false, null, null, null);
            int rankedQueueId = QueueChoice.QueueId(QueueKind.Ranked, tables.TbQueue);
            var (ranked, modeId, mapId2) = LoadingBoardSource.Resolve(queueId, gameModeId, mapId, match, rankedQueueId);
            var mode = modeId == 0 ? null : tables.TbGameMode.GetOrDefault(modeId);
            var map = mapId2 == 0 ? null : tables.TbMap.GetOrDefault(mapId2);
            return LoadingBoardText.From(ranked, mode?.Name, map?.Name, mode?.Description);
        }

        private void Refresh()
        {
            var text = ComposeText(_matchmaking.queueId, _matchmaking.gameModeId, _matchmaking.mapId, _room.match, _masterData.Tables);

            var tag = Root.Q<Label>("loading-tag");
            tag.text = text.Tag;
            tag.EnableInClassList("loading-tag--ranked", text.Ranked);
            KitEllipsis.SetText(Root.Q<Label>("loading-title"), text.Title);
            Root.Q<Label>("loading-map").text = text.Map;
            Root.Q<Label>("loading-desc").text = text.Description;
        }

        private void AdvanceDots()
        {
            _dot = (_dot + 1) % 3;
            Root.Q<Label>("loading-dots").text = _dot == 0 ? "●  ○  ○" : _dot == 1 ? "●  ●  ○" : "●  ●  ●";
        }
    }
}
