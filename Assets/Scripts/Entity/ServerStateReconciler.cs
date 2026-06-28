using GameFramework;
using LOP.Event.LOPRunner.Update;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class ServerStateReconciler : MonoBehaviour, ICleanup
    {
        [Inject]
        private IRunner runner;

        public LOPEntity entity { get; set; }
        public LOPEntityView entityView { get; set; }

        private CircularBuffer<EntitySnap> serverEntitySnaps;
        private List<LocalEntitySnap> localEntitySnaps;
        private BoundedDictionary<long, EntityTransform> entityTransformSnaps;

        private Vector3 beginPosition;
        private Vector3 beginRotation;
        private Vector3 beginVelocity;

        private Vector3 positionForSmoothDamp;
        private Vector3 velocityForSmoothDamp;

        private void Awake()
        {
            serverEntitySnaps = new CircularBuffer<EntitySnap>(5);
            localEntitySnaps = new List<LocalEntitySnap>(100);
            entityTransformSnaps = new BoundedDictionary<long, EntityTransform>(20);
        }

        private void Start()
        {
            runner.AddListener(this);  //  addto(this);
        }

        public void Cleanup()
        {
            runner.RemoveListener(this);
        }

        [RunnerListen(typeof(Begin))]
        private void OnBegin()
        {
            beginPosition = entity.position;
            beginRotation = entity.rotation;
            beginVelocity = entity.velocity;
        }

        [RunnerListen(typeof(End))]
        private void OnEnd()
        {
            SaveLocalEntitySnap();

            Reconcile();

            SaveEntityTransformSnap();
        }

        private void SaveLocalEntitySnap()
        {
            localEntitySnaps.Add(new LocalEntitySnap
            {
                tick = Runner.Time.tick,
                positionDiff = entity.position - beginPosition,
                rotationDiff = entity.rotation - beginRotation,
                velocityDiff = entity.velocity - beginVelocity,
            });
        }

        private void Reconcile()
        {
            if (serverEntitySnaps.Count == 0)
            {
                return;
            }

            var position = entity.position;
            var rotation = entity.rotation;
            var velocity = entity.velocity;

            var lastServerEntitySnap = serverEntitySnaps.Last();

            Vector3 targetPosition = lastServerEntitySnap.position;
            Vector3 targetRotation = lastServerEntitySnap.rotation;
            Vector3 targetVelocity = lastServerEntitySnap.velocity;

            long baseLocalTick = lastServerEntitySnap.tick;

            foreach (var localEntitySnap in localEntitySnaps)
            {
                if (localEntitySnap.tick > baseLocalTick)
                {
                    targetPosition += localEntitySnap.positionDiff;
                    targetRotation += localEntitySnap.rotationDiff;
                    targetVelocity += localEntitySnap.velocityDiff;
                }
            }
            localEntitySnaps.RemoveAll(x => x.tick <= baseLocalTick);


            float threshold = 0.06f;
            float distance = (position - targetPosition).magnitude;

            float lerpFactor = Mathf.Clamp01(distance / threshold);
            float smoothTime = Mathf.Lerp(0.4f, 0.08f, lerpFactor);
            float maxSpeed = Mathf.Lerp(8f, 25f, lerpFactor);

            entity.position = Vector3.SmoothDamp(position, targetPosition, ref positionForSmoothDamp, smoothTime, maxSpeed, (float)Runner.Time.tickInterval);
            entity.rotation = Quaternion.Slerp(Quaternion.Euler(rotation), Quaternion.Euler(targetRotation), (float)Runner.Time.tickInterval * (2f + lerpFactor * 3f)).eulerAngles;
            entity.velocity = Vector3.SmoothDamp(velocity, targetVelocity, ref velocityForSmoothDamp, smoothTime, maxSpeed, (float)Runner.Time.tickInterval);

            if (distance > threshold * 8)
            {
                entity.position = targetPosition;
                entity.rotation = targetRotation;
                entity.velocity = targetVelocity;

                positionForSmoothDamp = Vector3.zero;
                velocityForSmoothDamp = Vector3.zero;
            }

            //  To DO:
            //  충돌 체크.. 벽이나 지면 뚫지 않도록..
        }

        private void SaveEntityTransformSnap()
        {
            entityTransformSnaps[Runner.Time.tick] = new EntityTransform
            {
                position = entity.position,
                rotation = entity.rotation,
                velocity = entity.velocity,
            };
        }

        public void AddServerEntitySnap(EntitySnap entitySnap)
        {
            serverEntitySnaps.Add(entitySnap);
        }

        private void LateUpdate()
        {
            if (entityView.visualGameObject == null || entityTransformSnaps.Count < 2)
            {
                return;
            }

            //  렌더링과 물리 시뮬레이션의 주기가 달라 움직임이 튀어 보일 수 있는 부분을 지연 렌더링을 사용하여 부드럽게 렌더링한다.
            double tickInterval = Runner.Time.tickInterval;
            double renderTime = Runner.Time.elapsedTime - tickInterval;

            long tickPrev = (long)(renderTime / tickInterval);
            long tickNext = tickPrev + 1;

            float t = (float)((renderTime % tickInterval) / tickInterval);

            // 렌더 시간으로 역산한 틱이 저장 버퍼에 없을 수 있다(틱 카운터 vs 경과시간 어긋남/갭).
            // 없으면 이번 프레임 보간을 건너뛰고 직전 위치 유지 — 다음 프레임에 다시 맞춰진다.
            if (entityTransformSnaps.TryGetValue(tickPrev, out var prev) == false ||
                entityTransformSnaps.TryGetValue(tickNext, out var next) == false)
            {
                return;
            }

            entityView.visualGameObject.transform.position = Vector3.Lerp(
                prev.position,
                next.position,
                t);

            entityView.visualGameObject.transform.rotation = Quaternion.Slerp(
                Quaternion.Euler(prev.rotation),
                Quaternion.Euler(next.rotation),
                t);
        }
    }
}
