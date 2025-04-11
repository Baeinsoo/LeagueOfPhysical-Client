using System;
using UnityEngine;

namespace LOP
{
    public class Room
    {
        public string id;
        public string matchId;
        public RoomStatus status;
        public string ip;
        public ushort port;
    }

    [Serializable]
    public enum RoomStatus
    {
        None = 0,
        CreatingRunner = 1,
        RunnerCreated = 2,
        Initializing = 3,
        WaitingForPlayers = 4,
        StartingGame = 5,
        GameInProgress = 6,
        GameFinished = 7,
        Closing = 8,
        Closed = 9,
        Error = 10,
    }
}
