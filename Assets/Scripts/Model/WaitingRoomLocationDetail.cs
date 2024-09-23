using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    [Serializable]
    public class WaitingRoomLocationDetail : LocationDetail
    {
        public string waitingRoomId;
        public string matchmakingTicketId;
    }
}
