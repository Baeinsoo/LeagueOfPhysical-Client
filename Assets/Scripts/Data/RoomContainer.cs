using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public partial class RoomContainer : IDataContainer
    {
        public RoomDto room;
        public MatchDto match;
        public Player player;

        public RoomContainer()
        {
            player = new Player();
        }

        public void Clear()
        {
            room = null;
            match = null;
            player.Clear();
        }
    }
}
