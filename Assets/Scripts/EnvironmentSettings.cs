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

        [SerializeField] private string lobbyServerBaseUrl;
        [SerializeField] private string matchmakingServerBaseUrl;
        [SerializeField] private string roomServerBaseUrl;

        [SerializeField] private bool useLocalRoomInstance;
        [SerializeField] private string localRoomHost = "localhost";
        [SerializeField] private ushort localRoomPort = 7777;

        public string lobbyBaseURL => lobbyServerBaseUrl;
        public string matchmakingBaseURL => matchmakingServerBaseUrl;
        public string roomBaseURL => roomServerBaseUrl;

        public bool UseLocalRoomInstance => useLocalRoomInstance;
        public string LocalRoomHost => localRoomHost;
        public ushort LocalRoomPort => localRoomPort;
    }
}
