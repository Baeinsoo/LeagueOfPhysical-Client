using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public partial class RoomContainer : IDataContainer
    {
        public RoomDto room;
        public MatchDto match;

        public RoomContainer() { }

        public void Clear()
        {
            room = null;
            match = null;
        }
    }
}
