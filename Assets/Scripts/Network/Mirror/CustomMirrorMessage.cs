using GameFramework;
using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public struct CustomMirrorMessage : NetworkMessage
    {
        public IMessage payload;
    }
}
