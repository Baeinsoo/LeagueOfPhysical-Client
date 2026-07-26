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
        public int activeAbilityId { get; set; }
        public long abilityEndTick { get; set; }

        // AutoMapper 대상 아님 — 핸들러가 수동으로 채운다(contributions와 같은 이유).
        public List<ActiveEffect> statusEffects { get; set; } = new List<ActiveEffect>();

        public double timestamp { get; set; }

        // 서버 권위 외부 이동 기여(넉백 등). AutoMapper 대상 아님 — 핸들러가 수동으로 채운다.
        public List<MotionContribution> contributions { get; set; } = new List<MotionContribution>();
    }
}
