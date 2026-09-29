using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 머리 위 만화 그림(빌보드 스프라이트). 같은 키(사람)는 한 장만 — 새 것이 옛 것을 바꾼다. 시간은 밖에서 받는다(시험이 시간을 준다).
    /// </summary>
    public sealed class ArcheryComicFxPool : System.IDisposable
    {
        private const float Size = 0.5f;   // 머리 위 0.5m 사각
        private const int CellPixels = 256;
        private const int Columns = 4;

        private readonly Texture2D atlas;
        private readonly Sprite[] cells;
        private readonly Dictionary<string, (GameObject root, SpriteRenderer[] renderers, float bornAt)> active =
            new Dictionary<string, (GameObject, SpriteRenderer[], float)>();
        private readonly List<string> expired = new List<string>();

        public int ActiveCount => active.Count;

        public ArcheryComicFxPool(Texture2D atlas)
        {
            this.atlas = atlas;
            int rows = Mathf.Max(1, atlas.height / CellPixels);
            cells = new Sprite[Columns * rows];
            for (int i = 0; i < cells.Length; i++)
            {
                int col = i % Columns, row = i / Columns;
                var rect = new Rect(col * CellPixels, atlas.height - (row + 1) * CellPixels, CellPixels, CellPixels);
                cells[i] = Sprite.Create(atlas, rect, new Vector2(0.5f, 0f), CellPixels / Size);
            }
        }

        public void Show(string key, ArcheryComicCue cue, Vector3 worldPosition, float now)
        {
            Remove(key);
            var root = new GameObject("ComicFx_" + key);
            root.transform.position = worldPosition;
            var renderers = new SpriteRenderer[cue.Cells.Length];
            for (int i = 0; i < cue.Cells.Length; i++)
            {
                var go = new GameObject("Cell" + cue.Cells[i]);
                go.transform.SetParent(root.transform, false);
                //  첫 칸은 가운데 크게, 나머지는 오른쪽 위에 작게 곁들인다.
                go.transform.localPosition = i == 0 ? Vector3.zero : new Vector3(0.28f, 0.22f, 0f);
                go.transform.localScale = Vector3.one * (i == 0 ? 1f : 0.55f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = cells[cue.Cells[i]];
                sr.sortingOrder = 10 + i;
                renderers[i] = sr;
            }
            active[key] = (root, renderers, now);
        }

        public void Tick(float now, Camera camera)
        {
            expired.Clear();
            foreach (var pair in active)
            {
                float t = now - pair.Value.bornAt;
                if (t >= ArcheryComicFx.Life || pair.Value.root == null)
                {
                    expired.Add(pair.Key);
                    continue;
                }
                var root = pair.Value.root.transform;
                root.localScale = Vector3.one * ArcheryComicFx.ScaleAt(t);
                if (camera != null)
                {
                    root.rotation = camera.transform.rotation;   // 카메라를 본다
                }
                float a = ArcheryComicFx.AlphaAt(t);
                foreach (var sr in pair.Value.renderers)
                {
                    var c = sr.color;
                    c.a = a;
                    sr.color = c;
                }
            }
            foreach (var key in expired)
            {
                Remove(key);
            }
        }

        public void Dispose()
        {
            foreach (var key in new List<string>(active.Keys))
            {
                Remove(key);
            }
            foreach (var s in cells)
            {
                if (s != null) { Kill(s); }
            }
        }

        private void Remove(string key)
        {
            if (active.TryGetValue(key, out var entry))
            {
                if (entry.root != null) { Kill(entry.root); }
                active.Remove(key);
            }
        }

        private static void Kill(Object o)
        {
            if (UnityEngine.Application.isPlaying) { Object.Destroy(o); } else { Object.DestroyImmediate(o); }
        }
    }
}
