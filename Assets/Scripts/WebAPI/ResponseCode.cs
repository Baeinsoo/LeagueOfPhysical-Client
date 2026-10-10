using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    //  값은 백엔드 @lop/server-core의 responseCode.interface.ts와 **같아야 한다** —
    //  같은 와이어 계약을 두 언어로 적어 둔 것이라 한쪽만 고치면 조용히 어긋난다.
    public class ResponseCode
    {
        public const int SUCCESS = 200;

        #region Matchmaking
        public const int INVALID_TO_MATCH_MAKING = 10000;
        public const int ALREADY_IN_GAME = 10001;

        public const int MATCH_MAKING_TICKET_NOT_EXIST = 10100;
        public const int NOT_MATCH_MAKING_STATE = 10101;
        public const int INVALID_QUEUE = 10102;
        public const int INVALID_GAME_MODE = 10103;
        public const int INVALID_MAP = 10104;
        public const int PARTY_TOO_LARGE = 10105;
        #endregion

        #region Match
        public const int MATCH_NOT_EXIST = 20000;
        public const int INVALID_MATCH_RESULT = 20001;
        #endregion

        #region User
        public const int USER_NOT_EXIST = 30000;
        public const int INVALID_DISPLAY_NAME = 30001;
        #endregion

        #region Room
        public const int ROOM_NOT_EXIST = 50000;
        public const int ROOM_NOT_JOINABLE = 50001;
        #endregion

        #region User Location
        public const int USER_LOCATION_NOT_EXIST = 60000;
        #endregion

        #region User Rating
        public const int USER_RATING_NOT_EXIST = 70000;
        #endregion

        #region Economy
        //  백엔드 responseCode.interface.ts의 Economy region과 값이 같아야 한다 — 바꾸면 양쪽을 함께 고친다.
        public const int INSUFFICIENT_FUNDS = 80000;
        public const int COSMETIC_ALREADY_OWNED = 80001;
        public const int COSMETIC_NOT_PURCHASABLE = 80002;
        public const int PRICE_MISMATCH = 80003;
        public const int COSMETIC_NOT_OWNED = 80004;
        public const int ACCOUNT_FROZEN = 80005;
        public const int ECONOMY_DISABLED = 80006;
        public const int IDEMPOTENCY_CONFLICT = 80007;
        public const int COSMETIC_NOT_EXIST = 80008;
        public const int SLOT_MISMATCH = 80009;
        public const int WALLET_FULL = 80010;
        //  내부 API(역분개) 전용이지만 같은 번호대라 클라 사본에도 함께 둔다.
        public const int ALREADY_REVERSED = 80011;
        #endregion

        public const int UNKNOWN_ERROR = 5000000;
    }
}
