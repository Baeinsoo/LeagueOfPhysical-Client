using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class ResponseCode
    {
        public const int SUCCESS = 200;

        #region MatchMaking
        public const int INVALID_TO_MATCH_MAKING = 10000;
        public const int ALREADY_IN_GAME = 10001;

        public const int MATCH_MAKING_TICKET_NOT_EXIST = 10100;
        public const int NOT_MATCH_MAKING_STATE = 10101;
        #endregion

        #region Match
        public const int MATCH_NOT_EXIST = 20000;
        #endregion

        #region User
        public const int USER_NOT_EXIST = 30000;
        #endregion

        #region WaitingRoom
        public const int WAITING_ROOM_NOT_EXIST = 40000;
        public const int FAIL_TO_LEAVE_WAITING_ROOM = 40001;
        #endregion

        #region Room
        public const int ROOM_NOT_EXIST = 50000;
        #endregion

        public const int UNKNOWN_ERROR = 5000000;
    }
}
