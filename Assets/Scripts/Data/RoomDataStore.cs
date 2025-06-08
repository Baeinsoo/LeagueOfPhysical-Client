using GameFramework;

namespace LOP
{
    public class RoomDataStore : IRoomDataStore
    {
        public Room room { get; set; }
        public Match match { get; set; }

        [DataListen(typeof(GetMatchResponse))]
        private void HandleGetMatch(GetMatchResponse response)
        {
            match = MapperConfig.mapper.Map<Match>(response.match);
        }

        [DataListen(typeof(RoomJoinableResponse))]
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
