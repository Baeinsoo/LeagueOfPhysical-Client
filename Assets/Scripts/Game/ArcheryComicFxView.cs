using System.Collections.Generic;
using GameFramework;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 한 발 승부 만화 이펙트 — 사건마다 그 사람 머리 위(관중석은 맞은 자리)에 그림을 띄운다. 화면을 덮지 않아 조준 중에도 뜬다.
    /// 로빈 후드·닭 소동은 얼굴도 잠깐 놀람으로(<see cref="IsSurprised"/>를 <see cref="ArcheryChibiView"/>가 읽는다).
    /// </summary>
    public class ArcheryComicFxView : IStartable, ILateTickable, System.IDisposable
    {
        private const float HeadClearance = 0.55f;
        private const float FallbackHeight = 1.9f;

        private readonly ArcheryCourse course;
        private readonly ActorRegistry actorRegistry;
        private readonly ISubscriber<WorldEventBatchToC> batchSubscriber;
        private readonly ArcheryArrowLandings landings;
        private readonly ArcheryChickenView chickenView;
        private readonly CameraController cameraController;
        private readonly Dictionary<string, float> surprisedUntil = new Dictionary<string, float>();

        private ArcheryComicFxPool pool;
        private System.IDisposable subscription;
        private int crowdSerial;

        public ArcheryComicFxView(ArcheryCourse course, ActorRegistry actorRegistry,
                                  ISubscriber<WorldEventBatchToC> batchSubscriber,
                                  ArcheryArrowLandings landings, ArcheryChickenView chickenView,
                                  CameraController cameraController)
        {
            this.course = course;
            this.actorRegistry = actorRegistry;
            this.batchSubscriber = batchSubscriber;
            this.landings = landings;
            this.chickenView = chickenView;
            this.cameraController = cameraController;
        }

        public void Start()
        {
            if (course.IsShootOff == false)
            {
                return;
            }
            var atlas = Resources.Load<Texture2D>("Comic/FxAtlas");
            if (atlas == null)
            {
                Debug.LogWarning("[ArcheryComicFxView] Resources/Comic/FxAtlas가 없다 — 이펙트 없이 간다");
                return;
            }
            pool = new ArcheryComicFxPool(atlas);
            subscription = batchSubscriber.Subscribe(OnWorldEventBatch);
            landings.Landed += OnLanded;
            chickenView.Scared += OnChickenScared;
        }

        public void LateTick()
        {
            pool?.Tick(Time.time, cameraController != null ? cameraController.MainCamera : null);
        }

        public bool IsSurprised(string entityId, float now)
            => entityId != null && surprisedUntil.TryGetValue(entityId, out float until) && now < until;

        public void Dispose()
        {
            if (pool == null)
            {
                return;
            }
            subscription?.Dispose();
            landings.Landed -= OnLanded;
            chickenView.Scared -= OnChickenScared;
            pool.Dispose();
            pool = null;
        }

        private void OnWorldEventBatch(WorldEventBatchToC msg)
        {
            foreach (var rec in msg.Events)
            {
                if (rec.EventCase == WorldEventToC.EventOneofCase.ArcheryHit)
                {
                    var hit = (ArcheryTargetHitEvent)WorldEventWire.FromWire(rec);
                    if (hit.points == 10)
                    {
                        OnShooter(hit.shooterId, ArcheryComicEvent.Bull);
                    }
                }
                else if (rec.EventCase == WorldEventToC.EventOneofCase.ArcheryRoundResult)
                {
                    var result = (ArcheryRoundResultEvent)WorldEventWire.FromWire(rec);
                    foreach (var p in result.placements)
                    {
                        if (p.Hit == false)
                        {
                            OnShooter(p.ShooterId, ArcheryComicEvent.Miss);
                        }
                    }
                }
            }
        }

        private void OnLanded(ArcheryArrowLanding landing)
        {
            if (landing.Kind == ArcheryLandingKind.Crowd)
            {
                //  관중마다 따로 — 한 사람 키로 묶지 않는다(여러 발이 관중석에 들어가도 각자 뜬다).
                pool.Show("crowd:" + (crowdSerial++), ArcheryComicFx.For(ArcheryComicEvent.CrowdHit), landing.Point + Vector3.up * 0.4f, Time.time);
            }
            else if (landing.Kind == ArcheryLandingKind.Target && landing.SplitArrow)
            {
                OnShooter(landing.ShooterId, ArcheryComicEvent.RobinHood);
            }
        }

        private void OnChickenScared(string shooterId) => OnShooter(shooterId, ArcheryComicEvent.Chicken);

        private void OnShooter(string shooterId, ArcheryComicEvent e)
        {
            if (pool == null || TryHead(shooterId, out var head) == false)
            {
                return;
            }
            var cue = ArcheryComicFx.For(e);
            float now = Time.time;
            pool.Show(shooterId, cue, head, now);
            if (cue.SurpriseSeconds > 0f)
            {
                surprisedUntil[shooterId] = now + cue.SurpriseSeconds;
            }
        }

        private bool TryHead(string entityId, out Vector3 position)
        {
            position = default;
            if (entityId == null || actorRegistry.TryGet(entityId, out var actor) == false || actor == null || actor.visualGameObject == null)
            {
                return false;
            }
            var visual = actor.visualGameObject;
            var animator = visual.GetComponent<Animator>();
            var head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            position = head != null ? head.position + Vector3.up * HeadClearance : visual.transform.position + Vector3.up * FallbackHeight;
            return true;
        }
    }
}
