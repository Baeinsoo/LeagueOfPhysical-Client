using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    [Serializable]
    public class User
    {
        public string id;
        public string nickname;
        public int masterExp;
        public int friendlyRating;
        public int rankRating;
        public int goldCoin;
        public int gem;
        public Location location;
        public LocationDetail locationDetail;
    }
}
