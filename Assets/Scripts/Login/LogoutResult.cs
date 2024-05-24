using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class LogoutResult
    {
        public bool success;
        public string reason;

        public LogoutResult(bool success, string reason)
        {
            this.success = success;
            this.reason = reason;
        }
    }
}
