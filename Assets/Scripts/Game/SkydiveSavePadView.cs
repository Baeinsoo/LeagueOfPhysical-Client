using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 내가 저장한 세이브 발판을 빛나게 한다(다른 발판은 그대로). 저장 판정은 시뮬(<see cref="SkydiveSaveSystem"/>)이 하고,
    /// 여기서는 내 캐릭터의 <see cref="SkydiveSave"/>를 읽어 그릴 뿐이다.
    /// <para>맵 씬에 클라 전용 컴포넌트를 붙이지 않으려고 진입점으로 둔다(<see cref="SkydiveLaserView"/>와 같은 이유).</para>
    /// </summary>
    public class SkydiveSavePadView : ILateTickable, IDisposable
    {
        public static readonly Color SavedColor = new Color(0.35f, 1f, 0.75f);

        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        private readonly Dictionary<int, (Renderer renderer, Material original)> pads = new Dictionary<int, (Renderer, Material)>();
        private Material savedMaterial;
        private int lit = SkydiveSave.None;
        private float nextSearch;

        public SkydiveSavePadView(IPlayerContext playerContext, GameFramework.World.EntityRegistry entityRegistry)
        {
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
        }

        public void LateTick()
        {
            if (pads.Count == 0)
            {
                //  맵이 늦게 뜬다 — 발판이 보일 때까지 찾되, 발판 없는 맵에서 매 프레임 씬을 훑지 않게 1초마다.
                if (Time.unscaledTime < nextSearch)
                {
                    return;
                }
                nextSearch = Time.unscaledTime + 1f;
                Collect();
                if (pads.Count == 0)
                {
                    return;
                }
            }

            var entity = string.IsNullOrEmpty(playerContext.entityId) ? null : entityRegistry.Get(playerContext.entityId);
            int saved = entity?.Get<SkydiveSave>()?.PadId ?? SkydiveSave.None;
            if (saved == lit)
            {
                return;
            }
            if (pads.TryGetValue(lit, out var before) && before.renderer != null)
            {
                before.renderer.sharedMaterial = before.original;
            }
            if (pads.TryGetValue(saved, out var after) && after.renderer != null)
            {
                savedMaterial ??= MakeSaved(after.original);
                after.renderer.sharedMaterial = savedMaterial;
            }
            lit = saved;
        }

        private void Collect()
        {
            foreach (var pad in UnityEngine.Object.FindObjectsByType<SavePad>(FindObjectsSortMode.None))
            {
                var r = pad.GetComponentInChildren<Renderer>();
                if (r != null)
                {
                    pads[pad.Id] = (r, r.sharedMaterial);
                }
            }
        }

        private static Material MakeSaved(Material source)
        {
            var m = source != null ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", SavedColor);
            return m;
        }

        public void Dispose()
        {
            foreach (var pair in pads)
            {
                if (pair.Value.renderer != null)
                {
                    pair.Value.renderer.sharedMaterial = pair.Value.original;
                }
            }
            pads.Clear();
            if (savedMaterial != null)
            {
                UnityEngine.Object.Destroy(savedMaterial);
            }
        }
    }
}
