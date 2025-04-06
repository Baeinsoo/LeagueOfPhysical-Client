using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    [Serializable]
    public class UserLocationDto
    {
        public string userId;
        public Location location;
        public LocationDetail locationDetail;
    }
}
