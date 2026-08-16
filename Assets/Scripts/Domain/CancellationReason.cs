using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>매칭이 왜 끝났는지. 값 이름·번호는 서버(@lop/server-core)와 일치해야 한다.</summary>
    [Serializable]
    public enum CancellationReason
    {
        None = 0,
        User = 1,
        Timeout = 2,
    }
}
