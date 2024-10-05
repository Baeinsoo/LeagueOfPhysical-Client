using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    [CreateAssetMenu(fileName = "EnvironmentSettings", menuName = "LOP/Internal/Environment Settings")]
    public class EnvironmentSettings : ScriptableObject
    {
        public static EnvironmentSettings _active;
        public static EnvironmentSettings active
        {
            get
            {
                if (_active == null)
                {
                    _active = Resources.Load<EnvironmentSettings>($"EnvironmentSettings/EnvironmentSettings.local-k8s");
                }
                return _active;
            }
        }

        [Serializable]
        public class BaseURLSetting
        {
            public string scheme;
            public string host;

            public string baseUrl => $"{scheme}://{host}";
        }

        [SerializeField] private BaseURLSetting lobbyServerSetting;
        [SerializeField] private BaseURLSetting matchmakingServerSetting;
        [SerializeField] private BaseURLSetting roomServerSetting;

        public string lobbyBaseURL => lobbyServerSetting.baseUrl;
        public string matchmakingBaseURL => matchmakingServerSetting.baseUrl;
        public string roomBaseURL => roomServerSetting.baseUrl;
    }
}
