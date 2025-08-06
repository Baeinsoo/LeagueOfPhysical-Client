using GameFramework;

namespace LOP
{
    public class RoomDataStore : IRoomDataStore
    {
        public Room room { get; set; }
        public Match match { get; set; }

        public RoomDataStore()
        {
            EventBus.Default.Subscribe<GetMatchResponse>(EventTopic.WebResponse, HandleGetMatch);
            EventBus.Default.Subscribe<RoomJoinableResponse>(EventTopic.WebResponse, HandleRoomJoinable);
        }

        private void HandleGetMatch(GetMatchResponse response)
        {
            match = MapperConfig.mapper.Map<Match>(response.match);
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
