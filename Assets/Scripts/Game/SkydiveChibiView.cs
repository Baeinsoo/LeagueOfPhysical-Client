using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 스카이다이브 치비 — 얼굴 판·저지를 입히고, 공중이면 떨어지는 동작(Falling), 표정(다이브 집중 · 레이저 맞음 놀람)을 준다.
    /// 몸 기울기는 <see cref="PostureTiltView"/>가 따로 한다. 치비가 아닌 몸은 건드리지 않는다.
    /// </summary>
    public class SkydiveChibiView : IStartable, ILateTickable, System.IDisposable
    {
        private const string FaceMaterialKey = "Assets/Characters/Chibi/Materials/ChibiFace.mat";

        private readonly ActorRegistry actorRegistry;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly Dictionary<string, GameObject> dressed = new Dictionary<string, GameObject>();
        //  옛 원격 에셋의 치비엔 Falling이 없다 — 없는 파라미터를 매 프레임 부르면 경고가 쌓인다. 몸마다 한 번 확인.
        private readonly Dictionary<GameObject, bool> hasFalling = new Dictionary<GameObject, bool>();
        private readonly Dictionary<string, float> lastY = new Dictionary<string, float>();
        private readonly Dictionary<string, float> hitAt = new Dictionary<string, float>();
        private Material faceMaterial;

        public SkydiveChibiView(ActorRegistry actorRegistry, GameFramework.World.EntityRegistry entityRegistry)
        {
            this.actorRegistry = actorRegistry;
            this.entityRegistry = entityRegistry;
        }

        public void Start()
        {
            UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<Material>(FaceMaterialKey).Completed += h =>
            {
                faceMaterial = h.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded ? h.Result : null;
                if (faceMaterial == null)
                {
                    Debug.LogWarning("[SkydiveChibiView] 얼굴 재질을 못 받았다 — 표정 없이 간다: " + FaceMaterialKey);
                }
                dressed.Clear();   // 재질보다 몸이 먼저 떴으면 얼굴 없이 입었다 — 다시 입힌다
            };
        }

        public void LateTick()
        {
            float now = Time.time;
            foreach (var entity in entityRegistry.All)
            {
                var motion = entity.Get<MotionState>();
                if (motion == null || actorRegistry.TryGet(entity.Id, out var actor) == false || actor == null || actor.visualGameObject == null)
                {
                    continue;
                }
                var visual = actor.visualGameObject;
                if (ChibiDresser.IsChibi(visual) == false)
                {
                    continue;
                }
                if (dressed.TryGetValue(entity.Id, out var last) == false || last != visual)
                {
                    ChibiDresser.Dress(entity.Id, visual, faceMaterial);
                    dressed[entity.Id] = visual;
                }

                var animator = visual.GetComponent<Animator>();
                if (hasFalling.TryGetValue(visual, out bool canFall) == false)
                {
                    canFall = false;
                    var parameters = animator.parameters;
                    foreach (var p in parameters)
                    {
                        canFall |= p.name == "Falling";
                    }
                    if (parameters.Length > 0)
                    {
                        hasFalling[visual] = canFall;   // 아직 초기화 전(빈 목록)이면 다음 프레임에 다시 본다
                    }
                }
                if (canFall)
                {
                    animator.SetBool("Falling", motion.Value != SkydiveMotionState.Walking);
                }

                float y = GameFramework.World.EntityMotionExtensions.GetPosition(entity).y;
                if (lastY.TryGetValue(entity.Id, out float prevY) && SkydiveLookRules.Teleported(prevY, y))
                {
                    hitAt[entity.Id] = now;   // 레이저에 맞아 체크포인트로 올라갔다
                }
                lastY[entity.Id] = y;

                var posture = entity.Get<Posture>();
                bool hitRecently = hitAt.TryGetValue(entity.Id, out float at) && now - at < SkydiveLookRules.SurpriseSeconds;
                var face = visual.GetComponent<ChibiFace>();
                if (face != null)
                {
                    face.SetExpression(SkydiveLookRules.ChibiFace(motion.Value, posture?.Axis ?? 0f, posture?.Gliding ?? false, hitRecently));
                }
            }
        }

        public void Dispose()
        {
            dressed.Clear();
            hasFalling.Clear();
            lastY.Clear();
            hitAt.Clear();
        }
    }
}
