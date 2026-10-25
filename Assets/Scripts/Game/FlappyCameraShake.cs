using System;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 플래피 카메라 흔들림의 유일한 주인. 대시 시작 흔들림(<see cref="FlappyDashFx"/>가 건네는 몫)과 충돌 트라우마
    /// (<see cref="FlappyCrashFx"/>가 쌓는 몫)를 합쳐 프레임마다 한 번 <see cref="CameraController.ShakeOffset"/>에 쓴다.
    /// <para>주인이 둘이면 같은 프레임에 서로 덮고, 대시가 끝나며 0을 써서 충돌 흔들림을 끊는다. 설정 "흔들림 끄기"(spec §3)도
    /// 여기 한 곳에서 지킨다 — 대시 흔들림도 따른다.</para>
    /// <para>LateTick은 카메라의 LateUpdate(실행 순서 3000)보다 먼저 돈다 — 쓴 값이 그 프레임 카메라에 바로 먹는다.</para>
    /// </summary>
    public class FlappyCameraShake : ILateTickable, IDisposable
    {
        /// <summary>트라우마 1일 때 최대 흔들림(m, 카메라 기준). 시제품 10 px ÷ 30 px/m — 대시 흔들림(0.35 m)과 같은 급.</summary>
        internal const float MaxOffset = 0.33f;

        private readonly CameraTrauma trauma = new CameraTrauma();
        private Vector3 dashOffset;

        //  시험 이음매 — 실제로는 카메라, 시험은 받은 값을 적는다.
        internal Action<Vector3> applyOffset;
        internal Func<float> random = () => UnityEngine.Random.Range(-1f, 1f);

        /// <summary>설정 "흔들림 끄기". 경기 스코프가 지어질 때 한 번 읽는다(설정은 로비에서만 열린다).</summary>
        internal bool ShakeOff { get; set; }

        internal float Trauma => trauma.Value;

        public FlappyCameraShake(CameraController cameraController)
        {
            ShakeOff = FlappyDisplayPrefs.ShakeOff;
            applyOffset = v =>
            {
                if (cameraController != null)
                {
                    cameraController.ShakeOffset = v;
                }
            };
        }

        /// <summary>대시 흔들림 몫(대시 연출이 매 틱 넣고, 끝나면 0을 넣는다).</summary>
        public void SetDashOffset(Vector3 offset)
        {
            dashOffset = offset;
        }

        /// <summary>충돌 한 번의 트라우마(+0.55, 최대 1).</summary>
        public void AddTrauma(float amount)
        {
            trauma.Add(amount);
        }

        public void LateTick()
        {
            LateTick(Time.deltaTime);
        }

        internal void LateTick(float deltaTime)
        {
            trauma.Decay(deltaTime);
            if (ShakeOff)
            {
                applyOffset(Vector3.zero);
                return;
            }
            //  시제품처럼 프레임마다 아무 방향으로 — 위아래·좌우만(앞뒤로 흔들면 거리감이 바뀐다).
            float k = trauma.Offset(MaxOffset);
            Vector3 jolt = k > 0f ? new Vector3(random() * k, random() * k, 0f) : Vector3.zero;
            applyOffset(dashOffset + jolt);
        }

        public void Dispose()
        {
            dashOffset = Vector3.zero;
            applyOffset(Vector3.zero);
        }
    }
}
