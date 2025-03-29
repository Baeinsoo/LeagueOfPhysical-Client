using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public partial class Data
    {
        public static UserDataContext User { get; } = new UserDataContext();
        public static MatchMakingDataContext MatchMaking { get; } = new MatchMakingDataContext();
        public static RoomDataContext Room { get; } = new RoomDataContext();
    }
}
