using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class LoginResult
    {
        public bool success;
        public string reason;
        public string id;

        public LoginResult(bool success, string reason, string id)
        {
            this.success = success;
            this.reason = reason;
            this.id = id;
        }
    }
}
