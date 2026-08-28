using System;
using UnityEngine;

namespace LOP
{
    [CreateAssetMenu(fileName = "EnvironmentSettings", menuName = "LOP/Internal/Environment Settings")]
    public class EnvironmentSettings : ScriptableObject
    {
        /// <summary>플레이어 빌드에 구워지는 환경 자산의 이름. 빌드가 선택한 환경을 이 이름으로 복사한다.</summary>
        public const string ActiveEnvironment = "active";

        /// <summary>환경 자산들이 사는 Resources 하위 폴더.</summary>
        public const string ResourceDirectory = "EnvironmentSettings";

        /// <summary>에디터에서 아직 환경을 고른 적 없을 때 쓰는 값. 플레이어 빌드와는 무관하다.</summary>
        public const string EditorDefaultEnvironment = "local-k8s";

        public const string EditorPrefsKey = "LOP.Environment";

        public static EnvironmentSettings _active;
        public static EnvironmentSettings active
        {
            get
            {
                if (_active == null)
                {
                    _active = Load();
                }
                return _active;
            }
        }

        public static void Reload()
        {
            _active = null;
        }

        public static string ResourcePathFor(string environment)
        {
            return $"{ResourceDirectory}/EnvironmentSettings.{environment}";
        }

        /// <summary>
        /// 지금 어느 환경으로 도는가("dev"·"local-k8s"…). 플레이어 빌드는 환경이 하나로 구워져
        /// 있어 늘 같은 값이고, 에디터에서만 고른 값을 따라 바뀐다.
        /// <para>계정 저장 칸을 환경별로 나누는 데 쓴다 — 환경마다 계정 DB가 달라서, 한 칸을
        /// 나눠 쓰면 환경을 오갈 때 앞 환경 계정이 지워진다.</para>
        /// </summary>
        public static string ActiveEnvironmentName
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.EditorPrefs.GetString(EditorPrefsKey, EditorDefaultEnvironment);
#else
                return ActiveEnvironment;
#endif
            }
        }

        private static EnvironmentSettings Load()
        {
            //  플레이어 빌드에는 빌드 시점에 고른 환경 하나가 ActiveEnvironment 이름으로 구워져 있다.
            var environment = ActiveEnvironmentName;
            var path = ResourcePathFor(environment);
            var loaded = Resources.Load<EnvironmentSettings>(path);
            if (loaded == null)
            {
                //  틀린 서버에 조용히 붙느니 여기서 죽는다.
                throw new InvalidOperationException(
                    $"환경 설정을 찾을 수 없다: Resources/{path}. " +
                    "플레이어 빌드라면 빌드 시 -buildEnv 인자가 누락된 것이다.");
            }

            Debug.Log($"[LOP] environment={environment} lobby={loaded.lobbyServerBaseUrl}");
            return loaded;
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
