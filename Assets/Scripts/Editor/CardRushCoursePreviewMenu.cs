using UnityEditor;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// 재생 없이 맵 씬에서 칸 배치·색을 본다(spec §13.3). 열린 씬의 <see cref="CardRushMapRules"/> 마커와 고정 씨앗으로
    /// 레인 8개에 실행 중과 같은 그림(<see cref="CardRushLaneView"/>)을 깐다. 저장되지 않는 숨은 오브젝트라 씬을
    /// 더럽히지 않고, 메뉴를 다시 누르면 지운다.
    /// </summary>
    public static class CardRushCoursePreviewMenu
    {
        public const string MenuPath = "LOP/CardRush/코스 미리보기";
        public const string RootName = "__CardRushCoursePreview";

        /// <summary>미리보기 씨앗 — 고정이라 같은 맵이면 늘 같은 색 줄이 나온다.</summary>
        public const ulong PreviewSeed = 20261102UL;

        private static CardRushCoursePalette palette;

        [MenuItem(MenuPath)]
        public static void Toggle()
        {
            if (Clear())
            {
                Debug.Log("[CardRushPreview] 미리보기를 지웠다");
                return;
            }
            Show();
        }

        /// <summary>깔려 있던 미리보기를 지운다. 지운 게 있으면 true.</summary>
        public static bool Clear()
        {
            bool any = false;
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go != null && go.name == RootName && go.transform.parent == null && EditorUtility.IsPersistent(go) == false)
                {
                    Object.DestroyImmediate(go);
                    any = true;
                }
            }
            palette?.Dispose();
            palette = null;
            return any;
        }

        /// <summary>열린 씬의 마커로 레인 8개를 깐다. 만든 뿌리를 돌려준다(마커가 없거나 틀리면 null).</summary>
        public static GameObject Show()
        {
            var markers = Object.FindObjectsByType<CardRushMapRules>(FindObjectsSortMode.None);
            if (markers.Length != 1)
            {
                Debug.LogWarning($"[CardRushPreview] 열린 씬에 CardRushMapRules 마커가 하나여야 한다(지금 {markers.Length}개)");
                return null;
            }
            var marker = markers[0];

            var field = new CardRushMapRulesField();
            field.Set(marker.theme, marker.stepCount, marker.stepSpacing, marker.laneSpacing, marker.sections,
                      marker.transform.position, marker.transform.forward);
            if (field.Validate(out var error) == false)
            {
                Debug.LogWarning($"[CardRushPreview] 마커 값이 틀렸다: {error}");
                return null;
            }

            Clear();
            palette = new CardRushCoursePalette();
            var course = new CardRushCourse(PreviewSeed, field.StepCount, field.Sections);
            const HideFlags flags = HideFlags.HideAndDontSave;
            var root = new GameObject(RootName) { hideFlags = flags };
            for (int lane = 0; lane < CardRushLanes.MaxLanes; lane++)
            {
                new CardRushLaneView(root.transform, "Lane " + lane, field.Theme, field.LaneOrigin(lane), field.Forward,
                                     field.StepSpacing, course, palette, flags);
            }
            SceneView.RepaintAll();
            Debug.Log($"[CardRushPreview] {field.Theme} 레인 {CardRushLanes.MaxLanes} × 칸 {field.StepCount} (씨앗 {PreviewSeed}) — 메뉴를 다시 누르면 지운다");
            return root;
        }
    }
}
