using System.IO;
using Luban;
using UnityEditor;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// 재생 없이 맵 씬에서 장애물·구역 배치를 본다(spec 2026-11-05 §15). 열린 씬의 <see cref="CardRushMapRules"/> 마커 + 고정 씨앗 +
    /// 마스터데이터의 시작 튜닝으로 레인 8개에 실행 중과 같은 그림(<see cref="CardRushTrackDecor"/>·<see cref="CardRushLaneHazards"/>)을 깐다 —
    /// 장애물은 서 있는 모습, 구역은 경고 중 모습. 저장되지 않는 숨은 오브젝트라 씬을 더럽히지 않고, 메뉴를 다시 누르면 지운다.
    /// </summary>
    public static class CardRushCoursePreviewMenu
    {
        public const string MenuPath = "LOP/CardRush/코스 미리보기";
        public const string RootName = "__CardRushCoursePreview";

        /// <summary>미리보기 씨앗 — 고정이라 같은 맵·같은 표면 늘 같은 줄이 나온다.</summary>
        public const ulong PreviewSeed = 20261105UL;

        private const string MasterDataFolder = "Packages/com.baegames.lop.masterdata.client/Runtime.Generated/StreamingAssets/MasterData/";

        private static CardRushStageMaterials materials;

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
            materials?.Dispose();
            materials = null;
            return any;
        }

        /// <summary>마스터데이터 패키지의 표를 바로 읽는다 — 에디터엔 실행 중 로더(UnityWebRequest)가 없다.</summary>
        public static CardRushConfig StartConfig()
        {
            var tables = new LOP.MasterData.Tables(file => new ByteBuf(File.ReadAllBytes(Path.GetFullPath(MasterDataFolder + file + ".bytes"))));
            var md = new LOP.MasterData.LOPMasterData();
            typeof(LOP.MasterData.LOPMasterData).GetProperty(nameof(LOP.MasterData.LOPMasterData.Tables)).SetValue(md, tables);
            return new CardRushConfigProvider(md).Get();
        }

        /// <summary>열린 씬의 마커로 레인 8개를 깐다. 만든 뿌리를 돌려준다(마커가 없으면 null).</summary>
        public static GameObject Show()
        {
            var markers = Object.FindObjectsByType<CardRushMapRules>(FindObjectsSortMode.None);
            if (markers.Length != 1)
            {
                Debug.LogWarning($"[CardRushPreview] 열린 씬에 CardRushMapRules 마커가 하나여야 한다(지금 {markers.Length}개)");
                return null;
            }
            var marker = markers[0];
            var origin = marker.transform.position;
            var forward = marker.transform.forward;
            float laneSpacing = marker.laneSpacing;

            Clear();
            var tuning = StartConfig().Tuning;
            var track = new CardRushTrack(PreviewSeed, tuning);
            materials = new CardRushStageMaterials();
            const HideFlags flags = HideFlags.HideAndDontSave;
            var root = new GameObject(RootName) { hideFlags = flags };

            CardRushTrackDecor.BuildFinish(root.transform, origin, forward, laneSpacing, track.TrackMm, materials, flags, out _);
            for (int lane = 0; lane < CardRushLanes.MaxLanes; lane++)
            {
                var laneOrigin = CardRushLanes.LaneOrigin(origin, forward, laneSpacing, lane);
                CardRushTrackDecor.BuildLane(root.transform, "Track " + lane, laneOrigin, forward, laneSpacing, track.TrackMm, materials, flags);
                var hazards = new CardRushLaneHazards(root.transform, "Hazards " + lane, laneOrigin, forward, laneSpacing, materials, flags, labels: false);
                for (int i = 0; i < track.Obstacles.Count; i++)
                {
                    var o = track.Obstacles[i];
                    hazards.ShowObstacle(i, o, new CardRushObstacleLook(CardRushObstaclePhase.Standing, 0f, o.AtMm, o.Variant, 0f), null);
                }
                for (int i = 0; i < track.Zones.Count; i++)
                {
                    var z = track.Zones[i];
                    //  경고 중간 모습 — 바닥 투명도 반쯤, 바위는 아직 없다.
                    hazards.ShowZone(i, new CardRushZoneLook(CardRushZonePhase.Warning, z.FromMm, z.ToMm, z.WarnTicks * 0.5f * CardRushTiming.TickInterval, 0.5f, -1f, 0f), 0, null);
                }
            }
            SceneView.RepaintAll();
            Debug.Log($"[CardRushPreview] 레인 {CardRushLanes.MaxLanes} × 장애물 {track.Obstacles.Count} · 구역 {track.Zones.Count} · 길이 {track.TrackMm / 1000f} m"
                      + $" (씨앗 {PreviewSeed}, 지문 {track.Fingerprint:X16}) — 메뉴를 다시 누르면 지운다");
            return root;
        }
    }
}
