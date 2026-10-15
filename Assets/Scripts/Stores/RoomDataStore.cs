using System;
using MessagePipe;

namespace LOP
{
    public class RoomDataStore : IRoomDataStore, IDisposable
    {
        public Room room { get; set; }
        public Match match { get; set; }

        private readonly IDisposable subscriptions;

        public RoomDataStore(
            ISubscriber<GetMatchResponse> getMatchSubscriber,
            ISubscriber<RoomJoinableResponse> roomJoinableSubscriber)
        {
            var bag = DisposableBag.CreateBuilder();
            getMatchSubscriber.Subscribe(HandleGetMatch).AddTo(bag);
            roomJoinableSubscriber.Subscribe(HandleRoomJoinable).AddTo(bag);
            subscriptions = bag.Build();
        }

        public void Dispose()
        {
            subscriptions.Dispose();
        }

        private void HandleGetMatch(GetMatchResponse response)
        {
            match = MapperConfig.mapper.Map<Match>(response.match);
#if UNITY_EDITOR
            //  로컬 검증 전용 — 배포 전 맵(백엔드가 아직 모르는 맵)을 에디터에서 띄운다. 메뉴 LOP/Debug/다음 매치 맵 강제.
            int forced = UnityEditor.EditorPrefs.GetInt("LOP.Debug.ForceMapId", 0);
            if (forced > 0 && match?.rounds != null && match.rounds.Length > 0)
            {
                UnityEngine.Debug.LogWarning($"[Debug] 매치 맵을 {match.rounds[0].mapId} → {forced}로 바꾼다(에디터 전용)");
                match.rounds[0].mapId = forced;
            }
#endif
        }

        private void HandleRoomJoinable(RoomJoinableResponse response)
        {
            room = MapperConfig.mapper.Map<Room>(response.room);
        }

        public void Clear()
        {
            room = null;
            match = null;
        }
    }
}
