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
        public int flapCombo { get; set; }        // Flappy: 기절 없이 이어진, 시뮬이 받은 날갯짓 수 — 고비 보상의 근거라 정확히 맞춘다

        /// <summary>결승선 등수(1부터). 0 = 아직 안 들어옴.</summary>
        public int finishPlacement { get; set; }
        public int score { get; set; }
        public long knockbackEndTick { get; set; } // Knockback: 날림이 끝나는 절대 틱. 0 = 날리는 중 아님
        public int damagePercent { get; set; }     // Knockback: 누적 피해 %
        public int knockbackAmmo { get; set; }     // Knockback: 남은 탄
        public long knockbackNextReloadTick { get; set; }   // Knockback: 다음 탄이 차는 틱. 가득이면 long.MaxValue
        public long knockbackCooldownEndTick { get; set; }  // Knockback: 다시 쏠 수 있는 틱
        public bool knockbackHasBomb { get; set; }          // Knockback: 다음 탄이 충격파 폭탄인가
        public long cardStunEndTick { get; set; }       // CardRush: 기절이 끝나는 틱. 0 = 없음
        public long cardSpinStartTick { get; set; }     // CardRush: 카드가 (다시) 도는 틱. 0 = 아직 안 씀
        public int cardPosMm { get; set; }              // CardRush: 출발선에서 간 거리(mm)
        public int cardUseCount { get; set; }           // CardRush: 카드 쓴 횟수(첫 장 씨앗)
        public long cardDashStartTick { get; set; }     // CardRush: 대시를 쓴 틱. 0 = 대시 중 아님
        public int cardObstacleIndex { get; set; }      // CardRush: 지금 상대하는 장애물 번호
        public long cardObstacleTick { get; set; }      // CardRush: 바리케이드 닿은 틱 / 굴러오기 시작한 틱. 0 = 아직
        public int cardZoneIndex { get; set; }          // CardRush: 지금 상대하는 구역 번호
        public long cardZoneTick { get; set; }          // CardRush: 구역 경고가 켜진 틱. 0 = 아직
        public int cardObstacleEvent { get; set; }      // CardRush: 마지막 장애물 결과(CardRushObstacleEvent)
        public long cardObstacleEventTick { get; set; }
        public int cardZoneEvent { get; set; }          // CardRush: 마지막 구역 결과(CardRushZoneEvent)
        public long cardZoneEventTick { get; set; }
        public long cardFinishTick { get; set; }        // CardRush: 완주 틱. 0 = 아직
        public int cardCleanStreak { get; set; }        // CardRush: 지금 연속 무사 통과
        public long cardWhiffTick { get; set; }         // CardRush: 투사체가 꺼진 틱. 0 = 없음
        public long cardLockTick { get; set; }          // CardRush: 카드를 잡은 틱. 0 = 돈다
        public long cardExpireTick { get; set; }        // CardRush: 잡은 카드가 사라진 틱. 0 = 없음
        public long cardJumpStartTick { get; set; }     // CardRush: 점프 시작 틱. 0 = 땅
        public long cardShotTick { get; set; }          // CardRush: 날고 있는 투사체를 던진 틱. 0 = 없음
        public int cardShotFromMm { get; set; }         // CardRush: 투사체 출발 자리(mm)
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
