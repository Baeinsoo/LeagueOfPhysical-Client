using UnityEditor;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>사람 판·캡처용: 재생 중 투석기 판에서 누를 때마다 다음 표적의 터짐을 카메라 앞 35 m에 띄운다. 남의 적중 판은 작고 옅게·흔들림 없이.</summary>
    public static class CatapultFxPreviewMenu
    {
        private static int next;

        [MenuItem("LOP/Debug/투석기 연출 미리보기(다음)")]
        public static void Next() => Play(mine: true);

        [MenuItem("LOP/Debug/투석기 연출 미리보기(다음, 남의 적중)")]
        public static void NextOthers() => Play(mine: false);

        private static void Play(bool mine)
        {
            var scope = EditorApplication.isPlaying ? Object.FindFirstObjectByType<ArcheryLifetimeScope>() : null;
            var view = scope != null && scope.Container != null
                ? scope.Container.Resolve(typeof(ArcheryCatapultHitFxView)) as ArcheryCatapultHitFxView
                : null;
            if (view == null)
            {
                Debug.LogWarning("[FxPreview] 재생 중 투석기 판에서만 된다");
                return;
            }
            string code = ArcheryCatapultLooks.Codes[next++ % ArcheryCatapultLooks.Codes.Length];
            view.Preview(code, mine);
            Debug.Log("[FxPreview] " + code + (mine ? "" : " (남의 적중)"));
        }
    }
}
