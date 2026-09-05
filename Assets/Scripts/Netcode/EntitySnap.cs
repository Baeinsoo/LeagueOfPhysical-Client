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
        public int teleportCount { get; set; }    // 이어지지 않는 이동(레이저 피격 등)이 일어날 때마다 늘어난다
        public long stunEndTick { get; set; }     // Flappy: 멈춤이 풀리는 절대 틱. 0 = 안 멈춤
        public long invulnEndTick { get; set; }   // Flappy: 다시 안 걸리는 구간이 끝나는 절대 틱
        public long dashEndTick { get; set; }     // Flappy: 대시가 끝나는 절대 틱. 0 = 대시 중 아님
        public float dashCharge { get; set; }     // Flappy: 대시 게이지 0~1. 발동 자격의 권위다
        
        /// <summary>결승선 등수(1부터). 0 = 아직 안 들어옴.</summary>
        public int finishPlacement { get; set; }
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
