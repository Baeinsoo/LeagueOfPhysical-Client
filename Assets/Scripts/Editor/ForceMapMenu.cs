using UnityEditor;

namespace LOP.EditorTools
{
    /// <summary>로컬 검증 전용: 다음에 받는 매치의 맵을 바꾼다(RoomDataStore가 에디터에서만 읽는다).</summary>
    public static class ForceMapMenu
    {
        private const string Key = "LOP.Debug.ForceMapId";

        [MenuItem("LOP/Debug/다음 매치 맵 강제/투석기 사냥(13)")]
        public static void Catapult() => EditorPrefs.SetInt(Key, 13);

        [MenuItem("LOP/Debug/다음 매치 맵 강제/끄기")]
        public static void Off() => EditorPrefs.DeleteKey(Key);
    }
}
