using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    [CreateAssetMenu(fileName = "EnvironmentSettings", menuName = "LOP/Internal/Environment Settings")]
    public class EnvironmentSettings : ScriptableObject
    {
        public const string DefaultEnvironment = "local-k8s";
        public const string EditorPrefsKey = "LOP.Environment";

        public static EnvironmentSettings _active;
        public static EnvironmentSettings active
        {
            get
            {
                if (_active == null)
                {
                    _active = Resources.Load<EnvironmentSettings>($"EnvironmentSettings/EnvironmentSettings.{GetSelectedEnvironment()}");
                }
                return _active;
            }
        }

        public static void Reload()
        {
            _active = null;
        }

        private static string GetSelectedEnvironment()
        {
#if UNITY_EDITOR
            return UnityEditor.EditorPrefs.GetString(EditorPrefsKey, DefaultEnvironment);
#else
            return DefaultEnvironment;
#endif
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

        [SerializeField] private bool useLocalRoomInstance;
        [SerializeField] private string localRoomHost = "localhost";
        [SerializeField] private ushort localRoomPort = 7777;

        public string lobbyBaseURL => lobbyServerSetting.baseUrl;
        public string matchmakingBaseURL => matchmakingServerSetting.baseUrl;
        public string roomBaseURL => roomServerSetting.baseUrl;

        public bool UseLocalRoomInstance => useLocalRoomInstance;
        public string LocalRoomHost => localRoomHost;
        public ushort LocalRoomPort => localRoomPort;
    }
}
