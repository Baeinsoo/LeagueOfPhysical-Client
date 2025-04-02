using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameFramework;

namespace LOP
{
    public partial class RoomDataContext : IDataContext
    {
        public Type[] subscribedTypes => new Type[] { };

        public RoomDto room;
        public MatchDto match;
        public Player player;

        public RoomDataContext()
        {
            player = new Player();
        }

        public void UpdateData<T>(T data)
        {
        }

        public void Clear()
        {
            room = null;
            match = null;
            player.Clear();
        }
    }
}
