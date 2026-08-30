using System.Collections.Generic;
using System.Numerics;
using UnityEngine;

namespace LOP
{
    public class EntitySnap
    {
        public long tick { get; set; }
        public string entityId { get; set; }
        public UnityEngine.Vector3 position { get; set; }
        public UnityEngine.Vector3 rotation { get; set; }
        public UnityEngine.Vector3 velocity { get; set; }
        public bool grounded { get; set; }
        public long stunEndTick { get; set; }     // Flappy: 멈춤이 풀리는 절대 틱. 0 = 안 멈춤
        public long invulnEndTick { get; set; }   // Flappy: 다시 안 걸리는 구간이 끝나는 절대 틱
        public float postureAxis { get; set; }    // Skydive: 0 = 대자, 1 = 다이브
        public bool gliding { get; set; }         // Skydive: 패러세일을 폈나
        public float stamina { get; set; }        // Skydive: 남은 활공 자원
        public float emergencyRemaining { get; set; } // Skydive: 잔고 0에서 쓴 구제 창의 남은 초
        public int activeAbilityId { get; set; }
        public long abilityEndTick { get; set; }

        // AutoMapper 대상 아님 — 핸들러가 수동으로 채운다(contributions와 같은 이유).
        public List<ActiveEffect> statusEffects { get; set; } = new List<ActiveEffect>();

        public double timestamp { get; set; }

        // 서버 권위 외부 이동 기여(넉백 등). AutoMapper 대상 아님 — 핸들러가 수동으로 채운다.
        public List<MotionContribution> contributions { get; set; } = new List<MotionContribution>();
    }
}
