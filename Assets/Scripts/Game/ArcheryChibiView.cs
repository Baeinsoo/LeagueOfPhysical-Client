using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 한 발 승부에서 보이는 몸이 치비면 얼굴 판·저지를 입히고, 리액션 신호에 맞춰 애니·표정을 바꾼다.
    /// 치비가 아닌 몸(기사)은 건드리지 않는다. 몸 튕김은 <see cref="ArcheryShootOffReactionView"/>가 따로 얹는다.
    /// </summary>
    public class ArcheryChibiView : IStartable, ILateTickable, System.IDisposable
    {
        private const string FaceMaterialKey = "Assets/Characters/Chibi/Materials/ChibiFace.mat";

        private readonly ArcheryCourse course;
        private readonly ActorRegistry actorRegistry;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly ArcheryShootOffResultTracker resultTracker;
        private readonly GameFramework.Runner.IRunner runner;
        private readonly ArcheryComicFxView comicFx;

        private readonly Dictionary<string, (GameObject visual, ArcheryReactionCue cue)> dressed =
            new Dictionary<string, (GameObject, ArcheryReactionCue)>();
        private Material faceMaterial;

        public ArcheryChibiView(ArcheryCourse course, ActorRegistry actorRegistry,
                                GameFramework.World.EntityRegistry entityRegistry,
                                ArcheryShootOffResultTracker resultTracker, GameFramework.Runner.IRunner runner,
                                ArcheryComicFxView comicFx)
        {
            this.comicFx = comicFx;
            this.course = course;
            this.actorRegistry = actorRegistry;
            this.entityRegistry = entityRegistry;
            this.resultTracker = resultTracker;
            this.runner = runner;
        }

        public void Start()
        {
            if (course.IsShootOff == false)
            {
                return;
            }
            //  얼굴 재질은 프리팹과 같은 원격 그룹 — 한 번만 받아 둔다.
            UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<Material>(FaceMaterialKey).Completed += h =>
            {
                faceMaterial = h.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded ? h.Result : null;
                if (faceMaterial == null)
                {
                    Debug.LogWarning("[ArcheryChibiView] 얼굴 재질을 못 받았다 — 표정 없이 간다: " + FaceMaterialKey);
                }
                dressed.Clear();   // 재질보다 몸이 먼저 떴으면 얼굴 없이 입었다 — 다음 프레임에 다시 입힌다
            };
        }

        public void LateTick()
        {
            if (course.IsShootOff == false)
            {
                return;
            }
            float now = Time.time;
            double renderTick = RenderTick();
            foreach (var entity in entityRegistry.All)
            {
                if (entity.Has<ArcheryScore>() == false
                    || actorRegistry.TryGet(entity.Id, out var actor) == false || actor == null || actor.visualGameObject == null)
                {
                    continue;
                }
                var visual = actor.visualGameObject;
                if (IsChibi(visual) == false)
                {
                    continue;
                }
                if (dressed.TryGetValue(entity.Id, out var last) == false || last.visual != visual)
                {
                    DressVisual(entity.Id, visual, faceMaterial);
                    last = (visual, ArcheryReactionCue.None);
                }
                var cue = resultTracker.CueOf(entity.Id, now, renderTick);
                bool drawing = entity.Get<ArcheryAim>()?.Drawing ?? false;
                string trigger = ChibiReaction.TriggerOnChange(last.cue, cue);
                if (trigger != null)
                {
                    visual.GetComponent<Animator>().SetTrigger(trigger);
                }
                var face = visual.GetComponent<ChibiFace>();
                if (face != null)
                {
                    face.SetExpression(ChibiReaction.Of(cue, drawing, comicFx.IsSurprised(entity.Id, now)).Expression);
                }
                dressed[entity.Id] = (visual, cue);
            }
        }

        public void Dispose()
        {
            dressed.Clear();
        }

        public static bool IsChibi(GameObject visual)
        {
            var animator = visual != null ? visual.GetComponent<Animator>() : null;
            if (animator == null || animator.isHuman == false || animator.runtimeAnimatorController == null)
            {
                return false;
            }
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip != null && clip.name == "Happy")
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>얼굴 판·저지를 입힌다. 이미 입었으면 얼굴은 새로 붙이지 않는다.</summary>
        public static void DressVisual(string entityId, GameObject visual, Material faceMaterial)
        {
            ChibiOutfit.Apply(visual, ChibiOutfit.ColorsFor(entityId));
            if (faceMaterial == null || visual.GetComponent<ChibiFace>() != null)
            {
                return;
            }
            var face = visual.AddComponent<ChibiFace>();
            face.faceMaterial = faceMaterial;
            face.Build();
        }

        private double RenderTick()
        {
            if (runner?.tickUpdater == null || runner.tickUpdater.interval <= 0d)
            {
                return double.PositiveInfinity;
            }
            double interval = runner.tickUpdater.interval;
            return (runner.tickUpdater.elapsedTime - interval) / interval;
        }
    }
}
