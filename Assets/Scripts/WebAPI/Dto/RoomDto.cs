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
}
