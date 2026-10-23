using UnityEditor;

namespace LOP.EditorTools
{
    /// <summary>로컬 검증 전용: 다음에 받는 매치의 게임 모드를 바꾼다(RoomDataStore가 에디터에서만 읽는다).</summary>
    public static class ForceGameModeMenu
    {
        private const string ModeKey = "LOP.Debug.ForceGameModeId";
        private const string MapKey = "LOP.Debug.ForceMapId";

        // 모드와 맵은 짝이라 같이 건다(모드만 바꾸면 이전 모드의 맵이 남는다).
        [MenuItem("LOP/Debug/다음 매치 게임 모드 강제/말 활쏘기(10, 맵 14)")]
        public static void MountedArchery()
        {
            EditorPrefs.SetInt(ModeKey, 10);
            EditorPrefs.SetInt(MapKey, 14);
        }

        [MenuItem("LOP/Debug/다음 매치 게임 모드 강제/끄기")]
        public static void Off()
        {
            EditorPrefs.DeleteKey(ModeKey);
            EditorPrefs.DeleteKey(MapKey);
        }
    }
}
