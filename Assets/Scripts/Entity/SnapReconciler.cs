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

        private BoundedList<EntitySnap> serverEntitySnaps;
        private List<LocalEntitySnap> localEntitySnaps;
        private BoundedList<InputSequnce> serverInputSequnces;
        private BoundedList<InputSequnce> localInputSequnces;

        private Vector3 beginPosition;
        private Vector3 beginRotation;

        private Vector3 velocityForSmoothDamp;

        private void Awake()
        {
            serverEntitySnaps = new BoundedList<EntitySnap>(5);
            localEntitySnaps = new List<LocalEntitySnap>(100);
            serverInputSequnces = new BoundedList<InputSequnce>(50);
            localInputSequnces = new BoundedList<InputSequnce>(50);

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
        }

        [GameEngineListen(typeof(End))]
        private void OnEnd()
        {
            SaveLocalEntitySnap();

            Reconcile();
        }

        private void SaveLocalEntitySnap()
        {
            localEntitySnaps.Add(new LocalEntitySnap
            {
                tick = GameEngine.Time.tick,
                positionDiff = entity.position - beginPosition,
                rotationDiff = entity.rotation - beginRotation,
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

            var lastServerEntitySnap = serverEntitySnaps.Last();

            Vector3 position = lastServerEntitySnap.position;
            Vector3 rotation = lastServerEntitySnap.rotation;

            //  서버에서 처리한 인풋 Sequnce를 기점으로, 서버의 값에 클라의 인풋 처리 값(로컬 Diff)를 적용하여 보정.
            long baseLocalTick = 0;
            if (serverInputSequnces.Count == 0)
            {
                baseLocalTick = localEntitySnaps[0].tick - 1;
            }
            else
            {
                var serverTick = lastServerEntitySnap.tick;

                var serverInputSequnce = serverInputSequnces.Last(x => x.Tick <= serverTick);
                long offset = serverTick - serverInputSequnce.Tick;

                var localSyncSequence = localInputSequnces.First(x => x.Sequence == serverInputSequnce.Sequence);
                baseLocalTick = localSyncSequence.Tick + offset;
            }

            foreach (var localEntitySnap in localEntitySnaps)
            {
                if (localEntitySnap.tick > baseLocalTick)
                {
                    position += localEntitySnap.positionDiff;
                    rotation += localEntitySnap.rotationDiff;
                }
            }
            localEntitySnaps.RemoveAll(x => x.tick <= baseLocalTick);


            float threshold = 0.1f;
            float distance = (pos - position).magnitude;

            float lerpFactor = Mathf.Clamp01(distance / threshold);
            float smoothTime = Mathf.Lerp(0.5f, 0.1f, lerpFactor);

            entity.position = Vector3.SmoothDamp(pos, position, ref velocityForSmoothDamp, smoothTime, entity.velocity.magnitude * 2f, (float)GameEngine.Time.tickInterval);
            entity.rotation = Quaternion.Slerp(Quaternion.Euler(rot), Quaternion.Euler(rotation), (float)GameEngine.Time.tickInterval * (2f + lerpFactor * 3f)).eulerAngles;

            if (distance > threshold * 5)
            {
                entity.position = position;
                entity.rotation = rotation;
                velocityForSmoothDamp = Vector3.zero;
            }

            //  To DO:
            //  충돌 체크.. 벽이나 지면 뚫지 않도록..
        }

        public void AddServerEntitySnap(EntitySnap entitySnap)
        {
            serverEntitySnaps.Add(entitySnap);
        }

        public void AddServerInputSequnce(InputSequnce inputSequnce)
        {
            serverInputSequnces.Add(inputSequnce);
        }

        public void AddLocalInputSequnce(InputSequnce inputSequnce)
        {
            localInputSequnces.Add(inputSequnce);
        }

        private void LateUpdate()
        {
            //  To Do:
            //  visualObject만 따로 빼서 (elapsedTime - tickTime) * velocity 로 보정해 주기
        }
    }
}
