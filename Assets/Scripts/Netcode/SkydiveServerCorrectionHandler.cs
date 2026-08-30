namespace LOP
{
    /// <summary>
    /// Skydive의 서버 보정 — 자세(Axis/Gliding)·스태미나가 서버 권위다.
    ///
    /// 남의 자세를 클라가 예측(외삽)하게 되면서, 그 추측이 서버와 갈리는 순간이 생긴다
    /// (입력이 바뀌었는데 아직 못 받은 경우 등). 그 어긋남을 스냅샷으로 계속 눌러 주지 않으면
    /// 위치는 맞아도 자세만 남을 수 있어, 남 몸이 실제와 다른 낙하 속도로 움직여 보인다.
    /// </summary>
    public class SkydiveServerCorrectionHandler : IServerCorrectionHandler
    {
        private readonly SkydiveWorld world;       // 같은 게임 안이므로 구체를 직접 본다

        public SkydiveServerCorrectionHandler(SkydiveWorld world)
        {
            this.world = world;
        }

        //  비교는 반드시 같은 시점끼리 한다 — 앵커 틱에 "내가 그때 예측했던" 자세 vs 서버가 그
        //  틱에 갖고 있던 자세. 지금 살아있는 값과 비교하면 클라가 앞서 달리는 리드 구간 내내
        //  시점이 어긋나 보여, 자세가 바뀔 때마다 불필요한 되돌리기가 난다.
        public bool Matches(long tick, EntitySnap snap)
        {
            //  앵커 틱 기록이 없으면(정상 경로엔 없는 엣지) 비교 불가 — 불일치로 단정하지 않고
            //  위치 판정에 맡긴다.
            if (!world.TryGetSavedPosture(tick, snap.entityId, out var predicted))
            {
                return true;
            }
            //  Gliding은 켜짐/꺼짐이 곧 다른 항력 계수라 하나라도 어긋나면 되돌린다.
            //  Axis는 연속값이라 정확히 같을 이유가 없다 — SkydiveConfig.PostureRate가 4라 한
            //  틱(0.02초)에 축이 최대 0.08만큼 움직인다. 즉 0.1은 그 한 틱치 움직임(0.08)을 약간
            //  웃도는, 대략 1.25틱만큼의 오차를 봐준다는 뜻이다 — 입력이 한 틱 어긋나 도착했을
            //  때 생기는 차이와 같은 크기라 그 정도는 "같은 자세로 향하고 있다"로 본다. config에서
            //  계산하지 않고 상수로 고정한 건, 이 값이 튜닝값이 바뀔 때마다 같이 흔들려야 할
            //  이유가 없어서다(관용도는 그 자체로 하나의 결정). 스태미나·비상 잔여시간은 여기서
            //  안 본다(연속값이라 매 틱 미세하게 달라서 넣으면 거의 매 틱 되돌린다) —
            //  ApplyAuthoritative에서 덮는 것으로 충분하다.
            const float AxisTolerance = 0.1f;
            return predicted.Gliding == snap.gliding
                && System.Math.Abs(predicted.Axis - snap.postureAxis) < AxisTolerance;
        }

        public void ApplyAuthoritative(GameFramework.World.Entity entity, EntitySnap snap, float deltaTime)
        {
            var posture = entity.Get<Posture>();
            if (posture != null)
            {
                posture.Axis = snap.postureAxis;
                posture.Gliding = snap.gliding;
            }

            var stamina = entity.Get<Stamina>();
            if (stamina != null)
            {
                stamina.Current = snap.stamina;
                //  비상 펼침(잔고 0에서의 마지막 구제 창) 남은 초도 여기서 덮는다. 남에게는
                //  InputBuffer가 없어 TryStartGlide가 절대 안 불리므로, 이 값을 안 실으면
                //  로컬 EmergencyRemaining이 늘 0으로 묶여 있다가 다음 틱 StaminaSystem.Tick이
                //  "잔고도 0, 구제 창도 0"으로 보고 곧바로 접어 버린다 — 서버가 그 1초짜리
                //  구제 구간을 굴리는 동안 남은 자유낙하로 보이는 러버밴딩이 된다.
                //  EmergencyUsed는 안 싣는다 — 남에게는 TryStartGlide가 안 불려 그 값이 예측에
                //  영향을 줄 길이 없다(true를 안 보내도 잘못 접힐 일이 없다는 뜻).
                stamina.EmergencyRemaining = snap.emergencyRemaining;
            }
        }
    }
}
