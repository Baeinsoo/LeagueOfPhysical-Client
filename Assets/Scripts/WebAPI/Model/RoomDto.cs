using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    [Serializable]
    public class RoomDto
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
        Spawning = 1,
        Spawned = 2,
        Ready = 3,
        Playing = 4,
        Finished = 5,
    }
}
