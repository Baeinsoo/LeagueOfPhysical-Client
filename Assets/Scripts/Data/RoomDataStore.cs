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
