using GameFramework;
using LOP.Event.LOPGameEngine.Update;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LOP
{
    public class SnapReconciler : MonoBehaviour
    {
        public LOPEntity entity { get; set; }

        private CircularBuffer<EntitySnap> serverEntitySnaps;
        private List<LocalEntitySnap> localEntitySnaps;
        private CircularBuffer<InputSequence> serverInputSequences;
        private CircularBuffer<InputSequence> localInputSequences;
        private BoundedDictionary<long, EntityTransformSnap> entityTransformSnaps;

        private Vector3 beginPosition;
        private Vector3 beginRotation;
        private Vector3 beginVelocity;

        private Vector3 positionForSmoothDamp;
        private Vector3 velocityForSmoothDamp;

        private void Awake()
        {
            serverEntitySnaps = new CircularBuffer<EntitySnap>(5);
            localEntitySnaps = new List<LocalEntitySnap>(100);
            serverInputSequences = new CircularBuffer<InputSequence>(50);
            localInputSequences = new CircularBuffer<InputSequence>(50);
            entityTransformSnaps = new BoundedDictionary<long, EntityTransformSnap>(20);

            SceneLifetimeScope.Resolve<IGameEngine>().AddListener(this);  //  addto(this);
        }

        private void OnDestroy()
        {
            SceneLifetimeScope.Resolve<IGameEngine>().RemoveListener(this);
        }

        [GameEngineListen(typeof(Begin))]
        private void OnBegin()
        {
            beginPosition = entity.position;
            beginRotation = entity.rotation;
            beginVelocity = entity.velocity;

        }

        [GameEngineListen(typeof(End))]
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
                tick = GameEngine.Time.tick,
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

            var pos = entity.position;
            var rot = entity.rotation;
            var vel = entity.velocity;

            var lastServerEntitySnap = serverEntitySnaps.Last();

            Vector3 position = lastServerEntitySnap.position;
            Vector3 rotation = lastServerEntitySnap.rotation;
            Vector3 velocity = lastServerEntitySnap.velocity;

            //  서버에서 처리한 인풋 Sequence를 기점으로, 서버의 값에 클라의 인풋 처리 값(로컬 Diff)를 적용하여 보정.
            long baseLocalTick = 0;
            if (serverInputSequences.Count == 0)
            {
                baseLocalTick = localEntitySnaps[0].tick - 1;
            }
            else
            {
                var serverTick = lastServerEntitySnap.tick;

                var serverInputSequence = serverInputSequences.Last(x => x.Tick <= serverTick);
                long offset = serverTick - serverInputSequence.Tick;

                var localSyncSequence = localInputSequences.First(x => x.Sequence == serverInputSequence.Sequence);
                baseLocalTick = localSyncSequence.Tick + offset;
            }

            foreach (var localEntitySnap in localEntitySnaps)
            {
                if (localEntitySnap.tick > baseLocalTick)
                {
                    position += localEntitySnap.positionDiff;
                    rotation += localEntitySnap.rotationDiff;
                    velocity += localEntitySnap.velocityDiff;
                }
            }
            localEntitySnaps.RemoveAll(x => x.tick <= baseLocalTick);


            float threshold = 0.1f;
            float distance = (pos - position).magnitude;

            float lerpFactor = Mathf.Clamp01(distance / threshold);
            float smoothTime = Mathf.Lerp(0.5f, 0.1f, lerpFactor);

            entity.position = Vector3.SmoothDamp(pos, position, ref positionForSmoothDamp, smoothTime, entity.velocity.magnitude * 2f, (float)GameEngine.Time.tickInterval);
            entity.rotation = Quaternion.Slerp(Quaternion.Euler(rot), Quaternion.Euler(rotation), (float)GameEngine.Time.tickInterval * (2f + lerpFactor * 3f)).eulerAngles;
            entity.velocity = Vector3.SmoothDamp(vel, velocity, ref velocityForSmoothDamp, smoothTime, entity.velocity.magnitude * 2f, (float)GameEngine.Time.tickInterval);

            if (distance > threshold * 5)
            {
                entity.position = position;
                entity.rotation = rotation;
                entity.velocity = velocity;

                positionForSmoothDamp = Vector3.zero;
                velocityForSmoothDamp = Vector3.zero;
            }

            //  To DO:
            //  충돌 체크.. 벽이나 지면 뚫지 않도록..
        }

        private void SaveEntityTransformSnap()
        {
            entityTransformSnaps[GameEngine.Time.tick] = new EntityTransformSnap
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

        public void AddServerInputSequence(InputSequence inputSequence)
        {
            serverInputSequences.Add(inputSequence);
        }

        public void AddLocalInputSequence(InputSequence inputSequence)
        {
            localInputSequences.Add(inputSequence);
        }

        private void LateUpdate()
        {
            if (entity.visualGameObject == null || entityTransformSnaps.Count < 2)
            {
                return;
            }

            double tickInterval = GameEngine.Time.tickInterval;
            double renderTime = GameEngine.Time.elapsedTime - tickInterval;

            long tickPrev = (long)(renderTime / tickInterval);
            long tickNext = tickPrev + 1;

            float t = (float)((renderTime % tickInterval) / tickInterval);

            entity.visualGameObject.transform.position = Vector3.Lerp(
                entityTransformSnaps[tickPrev].position,
                entityTransformSnaps[tickNext].position,
                t);

            entity.visualGameObject.transform.rotation = Quaternion.Slerp(
                Quaternion.Euler(entityTransformSnaps[tickPrev].rotation),
                Quaternion.Euler(entityTransformSnaps[tickNext].rotation),
                t);
        }
    }
}
