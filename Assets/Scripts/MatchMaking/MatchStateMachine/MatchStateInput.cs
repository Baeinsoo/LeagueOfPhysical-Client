using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public enum MatchStateInput
    {
        StateDone = 0,

        //  States
        Idle = 100,
        RequestMatchmaking = 101,
        CancelMatchmaking = 102,
        InWaitingRoom = 103,
        InGameRoom = 104,
        CheckMatchState = 105,
    }
}
