using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 코리도 경계를 일괄 마킹하고, 마킹 전후를 눈으로 확인할 인벤토리를 뽑는다.
///
/// 가르는 기준은 이름이 아니라 <b>크기</b>다. ComposedMap이 절차 생성이라 코리도 벽과 내부 장애물이
/// 죄다 "Cube"라는 같은 이름을 갖는다. 대신 크기가 확연히 갈린다 —
/// 실측 분포에서 최대변 7.8과 26.0 사이에 아무것도 없다(장애물 68개 / 경계 50개).
/// 코리도 벽은 두께 26짜리 긴 기울어진 박스이고, 내부 장애물은 폭 1.6의 얇은 수직 박스다.
/// </summary>
public static class FlappyBoundaryMarker
{
    /// <summary>최대변이 이 값 이상이면 코리도 경계로 본다(실측 간극 7.8~26.0의 중간).</summary>
    const float BoundaryMinSize = 15f;

    // 이름으로도 명백한 경계들(Gauntlet 구간). 크기 기준과 OR로 묶는다.
    static readonly string[] BoundaryRoots = { "CorridorTop", "CorridorBot", "FunnelTop", "FunnelBot" };

    [MenuItem("Flappy/장애물 인벤토리 출력")]
    public static void Inventory()
    {
        var byPath = new Dictionary<string, int>();
        foreach (var ob in Object.FindObjectsByType<FlappyObstacle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string key = TopPath(ob.transform) + (IsBoundary(ob) ? "  [경계]" : "  [장애물]");
            byPath[key] = byPath.TryGetValue(key, out int n) ? n + 1 : 1;
        }
        var sb = new System.Text.StringBuilder("[FlappyObstacle 인벤토리]\n");
        foreach (var kv in byPath) sb.AppendLine($"  {kv.Key} × {kv.Value}");
        Debug.Log(sb.ToString());
    }

    [MenuItem("Flappy/코리도 경계 마킹")]
    public static void Mark()
    {
        int added = 0;
        foreach (var ob in Object.FindObjectsByType<FlappyObstacle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!IsBoundary(ob)) continue;
            if (ob.GetComponent<FlappyBoundary>() != null) continue;
            Undo.AddComponent<FlappyBoundary>(ob.gameObject);
            added++;
        }
        Debug.Log($"[FlappyBoundary] {added}개 마킹됨");
        if (added > 0) UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
    }

    // 이름이 붙은 하자드. 조리개·핀치는 화면 밖까지 덮으려고 파이프를 길게 뻗어서
    // 코리도 벽만큼 크다 — 그래서 크기 기준보다 이름 기준이 먼저 와야 한다.
    static readonly string[] HazardNames = { "Windmill", "Iris", "Pinch", "Donut", "MovingPipe" };

    static bool IsBoundary(FlappyObstacle ob)
    {
        if (IsHazardByName(ob.transform)) return false;      // 이름 있는 하자드는 크기 무관하게 장애물
        if (IsUnderBoundaryRoot(ob.transform)) return true;  // 이름이 명백한 경계
        return IsBigWall(ob);                                // 나머지 익명 Cube는 크기로
    }

    static bool IsHazardByName(Transform t)
    {
        for (var p = t; p != null; p = p.parent)
            foreach (var n in HazardNames)
                if (p.name.Contains(n)) return true;
        return false;
    }

    // 코리도 벽은 플레이 영역을 통째로 감싸는 거대한 박스다.
    [MenuItem("Flappy/코리도 경계 마킹 해제")]
    public static void Clear()
    {
        int removed = 0;
        foreach (var b in Object.FindObjectsByType<FlappyBoundary>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Undo.DestroyObjectImmediate(b);
            removed++;
        }
        Debug.Log($"[FlappyBoundary] {removed}개 해제됨");
        if (removed > 0) UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
    }

    // 코리도 벽은 플레이 영역을 통째로 감싸는 거대한 박스다.
    static bool IsBigWall(FlappyObstacle ob)
    {
        var col = ob.GetComponentInChildren<Collider>();
        if (col == null) return false;
        var s = col.bounds.size;
        return Mathf.Max(s.x, s.y) >= BoundaryMinSize;
    }

    static bool IsUnderBoundaryRoot(Transform t)
    {
        for (var p = t; p != null; p = p.parent)
            foreach (var n in BoundaryRoots)
                if (p.name == n) return true;
        return false;
    }

    // 조상 경로를 최상위 3단계까지만(로그가 폭발하지 않게)
    static string TopPath(Transform t)
    {
        var chain = new List<string>();
        for (var p = t; p != null; p = p.parent) chain.Add(p.name);
        chain.Reverse();
        int take = Mathf.Min(3, chain.Count);
        return string.Join("/", chain.GetRange(0, take));
    }
}
