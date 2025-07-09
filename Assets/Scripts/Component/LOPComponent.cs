using GameFramework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public abstract class LOPComponent : MonoComponent
    {
        public new LOPEntity entity => base.entity as LOPEntity;
    }
}
