using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameFramework;

namespace LOP
{
    //  user와 같이 따로 model 정의하여 사용하도록 수정 필요 (dto 사용 x)
    public partial class RoomDataContext : IDataContext
    {
        public Type[] subscribedTypes => new Type[] { typeof(MatchDto) };

        public RoomDto room;
        public MatchDto match;
        public Player player;

        public RoomDataContext()
        {
            player = new Player();
        }

        public void UpdateData<T>(T data)
        {
            if (data is MatchDto matchDto)
            {
                match = matchDto;
            }
        }

        public void Clear()
        {
            room = null;
            match = null;
            player.Clear();
        }
    }
}
