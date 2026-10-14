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
            var flash = Root.Q("loading-flash");
            //  같은 프레임에 클래스를 붙이면 전환이 안 생긴다 — 한 프레임 뒤 하얗게, 0.5초 뒤 걷는다.
            Root.schedule.Execute(() => flash.AddToClassList("loading-flash--on")).StartingIn(FlashInDelayMs);
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

        private void Refresh()
        {
            var tables = _masterData.Tables;
            int rankedQueueId = QueueChoice.QueueId(QueueKind.Ranked, tables.TbQueue);
            var (ranked, modeId, mapId) = LoadingBoardSource.Resolve(
                _matchmaking.queueId, _matchmaking.gameModeId, _matchmaking.mapId, _room.match, rankedQueueId);
            var mode = modeId == 0 ? null : tables.TbGameMode.GetOrDefault(modeId);
            var map = mapId == 0 ? null : tables.TbMap.GetOrDefault(mapId);
            var text = LoadingBoardText.From(ranked, mode?.Name, map?.Name, mode?.Description);

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
