using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LOP.EditorTools
{
    //  🚪 문지기만 빠르게 다시 재는 모드. 갈림길 증명(전수 탐색, 몇 분)은 지형에만 달렸으므로 전체 검사가
    //  남긴 날갯짓·출발점·기본 길을 캐시해 두고, 문지기만 고쳤을 때는 🚪 절만 다시 돈다.
    public static partial class FlappyMapPlayabilityCheck
    {
        [System.Serializable]
        private sealed class GuardCache
        {
            public string Fingerprint;
            public Vector3 Start;
            public bool SafeVerified;
            public List<Vector3> SafePath = new List<Vector3>();
            public List<int> BranchX0 = new List<int>();
            //  날갯짓은 '0'/'1' 문자열로 — JsonUtility가 중첩 리스트를 못 쓴다.
            public List<string> BranchFlaps = new List<string>();
        }

        private static string LogsPath(string file)
            => Path.Combine(Path.GetDirectoryName(UnityEngine.Application.dataPath), "Logs", file);

        private const string GuardCacheFile = "FlappyGuardCache.json";
        private const string GuardCheckFile = "FlappyGuardCheck.txt";

        //  지형 지문 — 씬 경로 + ComposedMap 아래 콜라이더 수·경계 합(0.01 반올림). 문지기(Guard_ 루트)는 빼서
        //  문지기를 고쳐도 캐시가 살고, 지형을 고치면 죽는다.
        private static string TerrainFingerprint()
        {
            var composed = GameObject.Find("ComposedMap");
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            if (composed == null) { return scene + "|no ComposedMap"; }
            int count = 0;
            double sum = 0;
            foreach (Collider c in composed.GetComponentsInChildren<Collider>(includeInactive: true))
            {
                Transform root = c.transform;
                while (root.parent != null && root.parent != composed.transform) { root = root.parent; }
                if (root.name.StartsWith(LOP.MapTools.GuardLayout.MarkerPrefix, System.StringComparison.Ordinal)) { continue; }
                //  도는 장애물은 검사 중 자세를 바꾸므로 경계가 매번 달라진다 — 지문에서 뺀다.
                if (c.GetComponentInParent<LOP.FlappyWindmill>(true) != null || c.GetComponentInParent<LOP.FlappyPendulum>(true) != null
                    || c.GetComponentInParent<LOP.FlappyShutter>(true) != null) { continue; }
                Bounds b = c.bounds;
                count++;
                sum += System.Math.Round(b.min.x + b.min.y + b.min.z + b.max.x + b.max.y + b.max.z, 2);
            }
            return $"{scene}|{count}|{sum:F2}";
        }

        private static void SaveGuardCache(Vector3 start, Dictionary<int, IReadOnlyList<bool>> branchFlaps,
                                           List<Vector3> safePath, bool safeVerified)
        {
            try
            {
                var cache = new GuardCache { Fingerprint = TerrainFingerprint(), Start = start, SafeVerified = safeVerified };
                if (safePath != null) { cache.SafePath.AddRange(safePath); }
                var keys = new List<int>(branchFlaps.Keys);
                keys.Sort();   // 같은 입력이면 같은 파일
                foreach (int x0 in keys)
                {
                    var text = new System.Text.StringBuilder(branchFlaps[x0].Count);
                    foreach (bool flap in branchFlaps[x0]) { text.Append(flap ? '1' : '0'); }
                    cache.BranchX0.Add(x0);
                    cache.BranchFlaps.Add(text.ToString());
                }
                File.WriteAllText(LogsPath(GuardCacheFile), JsonUtility.ToJson(cache));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[맵 검사] 🚪 캐시를 못 남겼다: {e.Message}");
            }
        }

        [MenuItem("LOP/Debug/Flappy 문지기만 검사 (빠름)")]
        public static void CheckGuardsOnly() => RunGuards(humanWatching: true);

        /// <summary>백그라운드 잡에서 부른다. 결과는 Logs/FlappyGuardCheck.txt.</summary>
        public static void RunGuardsOnly() => RunGuards(humanWatching: false);

        private static void RunGuards(bool humanWatching)
        {
            FlappyMapPlayabilityCheck.humanWatching = humanWatching;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            string outPath = LogsPath(GuardCheckFile);
            void Stop(string line)
            {
                Debug.LogWarning($"[맵 검사] 🚪만: {line}");
                File.WriteAllText(outPath, line + "\n");
            }

            int mapMask = CheckMapMask();
            if (TryReadBounds(mapMask, out Bounds bounds) == false) { Stop("맵 층에 콜라이더가 없다 — 맵 씬을 먼저 열어라."); return; }
            SearchMinY = bounds.min.y;
            SearchMaxY = bounds.max.y;
            if (TryReadFullConfig(out LOP.FlappyConfig config) == false) { Stop("MasterData에서 FlappyConfig를 못 읽었다."); return; }
            var shape = ShapeFrom(config);
            ScanAirflows();

            GuardCache cache = null;
            string cachePath = LogsPath(GuardCacheFile);
            if (File.Exists(cachePath))
            {
                try { cache = JsonUtility.FromJson<GuardCache>(File.ReadAllText(cachePath)); }
                catch (System.Exception) { cache = null; }
            }
            if (cache == null || cache.Fingerprint != TerrainFingerprint())
            {
                Stop("지형 경로 캐시가 없거나 지형이 바뀌었다 — 전체 검사를 먼저 한 번 돌릴 것.");
                return;
            }

            Windmills = CollectWindmills(out var windmillPoses, out _, out _);
            CollectGuards();
            SetGuardsActive(false);
            posedTick = long.MinValue;
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool sceneWasDirty = activeScene.isDirty;
            try
            {
                var flaps = new Dictionary<int, IReadOnlyList<bool>>();
                for (int i = 0; i < cache.BranchX0.Count; i++)
                {
                    string text = cache.BranchFlaps[i];
                    var list = new List<bool>(text.Length);
                    foreach (char c in text) { list.Add(c == '1'); }
                    flaps[cache.BranchX0[i]] = list;
                }
                var query = new GameFramework.Physics.UnityCollisionQuery();
                string section = Guards.Count == 0
                    ? "── 🚪 문지기 ─────────────────────\n  문지기 없음"
                    : GuardSection(cache.Start, shape, mapMask, query, ReadBranches(), flaps, cache.SafePath, cache.SafeVerified);
                //  기본 길은 지난 전체 검사 때 문지기 자리로 찾은 것이다 — 문지기를 옮겼으면 그 거리는 옛 길 기준이다.
                section = section.Replace("🌀", "🌀(지난 전체 검사의 기본 길 기준)");
                watch.Stop();
                File.WriteAllText(outPath, section + $"\n\n(🚪만 검사 {watch.ElapsedMilliseconds}ms)\n");
                Debug.Log($"[맵 검사] 🚪만 {watch.ElapsedMilliseconds}ms\n{section}");
            }
            finally
            {
                RestoreWindmills(windmillPoses);
                RestoreGuards();
                Windmills = null;
                moversLive = false;
                posedTick = long.MinValue;
                Debug.Log($"[맵 검사] 🚪만 씬 더티: 들어올 때 {sceneWasDirty} → 나갈 때 {activeScene.isDirty}");
            }
        }
    }
}
