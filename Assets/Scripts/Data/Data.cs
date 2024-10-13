using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public partial class Data
    {
        public static UserContainer User { get; } = new UserContainer();
        public static MatchMakingContainer MatchMaking { get; } = new MatchMakingContainer();
        public static RoomContainer Room { get; } = new RoomContainer();
    }
}
