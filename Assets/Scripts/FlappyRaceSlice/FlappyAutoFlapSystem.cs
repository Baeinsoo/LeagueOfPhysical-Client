using FlappyRace;
using GameFramework.Runner;
using LOP.Event.LOPRunner.Update;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// [진단용 임시 — 관찰이 끝나면 지운다] 손을 떼고 남의 새를 볼 수 있게 하는 자동 비행.
    ///
    /// 남의 새 렌더 보정이 어떻게 보이는지는 두 클라를 동시에 조종하면서 볼 수가 없다. 게다가 매번
    /// 다르게 날면 상수를 바꿔 가며 비교하는 것 자체가 안 된다 — 규칙이 같아야 같은 장면이 반복된다.
    ///
    /// 규칙은 옛 <c>FlappyAutoPilot</c>과 같다: <b>앞을 보고, 지나갈 수 있는 틈의 아래쪽을 겨냥해,
    /// 거기보다 낮으면 날갯짓.</b> 다른 것은 틈을 찾는 경로뿐 — 옛 스캔은 <c>FlappyObstacle</c>이
    /// 붙은 콜라이더를 읽었는데 실제 맵에는 그 컴포넌트가 없어(씬의 MonoBehaviour는 SpawnPoint·
    /// FinishLine뿐) 쓸 수 없다. 그래서 여기서는 물리로 직접 훑는다.
    ///
    /// 사람 입력을 덮지 않는다 — <see cref="PlayerInputManager.SetJump"/>를 부를 뿐이라 사람이
    /// 동시에 눌러도 그대로 먹는다.
    /// </summary>
    public class FlappyAutoFlapSystem : ITickSystem, VContainer.Unity.IStartable
    {
        public const string EditorPrefsKey = "LOP.Debug.AutoFlap";

        //  앞을 보는 시간. 거리가 아니라 시간으로 잡는다 — 날갯짓은 정점까지 0.32초가 걸리므로,
        //  그보다 가까운 것만 보면 보고 나서 쳐도 이미 늦는다(1.5m=0.14초로 보다가 계속 박았다).
        //  여러 지점을 보고 그 틈들이 겹치는 구간을 목표로 삼는다 — 앞이 뚫렸어도 그다음이 막혔으면
        //  거기로 가면 안 되기 때문이다.
        //  맨 앞 0.05초(0.55m)는 "코앞"이다. 나머지가 2.2m 앞부터라 <b>이미 몸이 닿아 있는 벽은
        //  아예 안 보였고</b>, 그래서 기둥에 붙은 채 계속 눌러 그 자리에 서 있었다(x=16·48·82·166에서
        //  재현). 코앞을 겹침 계산에 넣으면 "그 벽의 어느 틈으로 지나갈지"가 목표에 들어온다.
        private static readonly float[] LookAheadSeconds = { 0.05f, 0.20f, 0.40f, 0.60f };
        //  겨냥은 "이만큼 뒤에 어디 있을까"로 계산한다. 코앞 열이 생겼다고 이 시점까지 당기면
        //  안 된다 — 당기면 먼 기둥에 늦게 반응해 도로 박는다. 코앞은 목표를 좁힐 뿐이다.
        private const float PlanAheadSeconds = 0.20f;
        //  훑는 간격. 새 몸(0.9m)보다 훨씬 작아 틈을 놓치지 않는다.
        private const float ScanStep = 0.25f;
        //  훑는 범위(현재 높이 기준 위아래). 코스의 통로 높이(약 18m)를 덮는다.
        private const float ScanRange = 20f;
        //  연속 탭 방지. 없으면 봇이 틈 바닥에서 매 프레임 눌러 호버링하는데, 그러면 사람이 나는
        //  모양이 아니라서 정작 관찰하려는 "남의 새가 어떻게 보이나"가 실제 플레이와 달라진다.
        //  세는 단위는 틱이다 — 벽시계(Time.time)로 재면 그 에디터가 그 순간 몇 프레임을 뽑았느냐에
        //  따라 날갯짓이 허용되기도, 막히기도 한다. 그러면 두 클라가 같은 높이에서 출발해도 갈리고
        //  같은 판을 두 번 돌려도 달라져, 이 도구의 존재 이유인 "같은 장면 반복"이 무너진다.
        private const float FlapCooldownSeconds = 0.12f;

        //  대시로 나아가는 거리(전진 2배 × 0.2초). 이만큼 앞이 뚫려 있을 때만 지른다 —
        //  대시 중엔 날갯짓이 안 먹어 궤도를 못 고치므로, 막힌 데로 지르면 그대로 박는다.
        private const float DashReachSeconds = 0.2f;
        //  대시 경로를 훑는 간격. 몸 지름보다 촘촘해야 사이로 빠지는 기둥을 놓치지 않는다.
        private const float DashProbeStep = 0.4f;

        private readonly IRunner runner;
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly PlayerInputManager playerInputManager;
        private readonly FlappyConfig config;
        private readonly int mapLayerMask;

        private readonly bool[] blocked = new bool[(int)(ScanRange * 2f / ScanStep) + 1];
        private long lastFlapTick = long.MinValue;

        public FlappyAutoFlapSystem(IRunner runner, IPlayerContext playerContext,
                                    GameFramework.World.EntityRegistry entityRegistry,
                                    PlayerInputManager playerInputManager, FlappyConfig config)
        {
            this.runner = runner;
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
            this.playerInputManager = playerInputManager;
            this.config = config;
            //  맵만 본다 — 새는 Character 레이어라 서로를 장애물로 오인하지 않는다.
            this.mapLayerMask = LayerMask.GetMask("Default");
        }

        //  틱 등록은 Start에서 한다 — 이 클래스는 아무도 의존하지 않아서 엔트리포인트로 등록해야
        //  만들어지고, VContainer는 IStartable을 그 신호로 쓴다.
        public void Start()
        {
            runner.RegisterSystem<ProcessInput>(this);
        }

        public void Tick(long tick, float deltaTime)
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorPrefs.GetBool(EditorPrefsKey, false) == false)
            {
                return;
            }
            if (playerContext.entityId == null)
            {
                return;
            }
            var entity = entityRegistry.Get(playerContext.entityId);
            if (entity == null)
            {
                return;
            }

            Vector3 position = GameFramework.World.EntityMotionExtensions.GetPosition(entity);
            Vector3 velocity = GameFramework.World.EntityMotionExtensions.GetVelocity(entity);
            float bottomY = position.y - ScanRange;

            //  가장 가까운 기둥의 틈에서 시작해, 뒤쪽 기둥들과 겹치는 데만 남긴다.
            //  겹치는 데가 사라지면 거기서 멈춘다 — 더 먼 기둥까지 맞추려다 눈앞을 놓치면 안 된다.
            bool hasGap = false;
            float low = 0f;
            float high = 0f;
            int ticksToGap = Mathf.RoundToInt(PlanAheadSeconds / deltaTime);
            for (int s = 0; s < LookAheadSeconds.Length; s++)
            {
                float aheadX = position.x + config.ForwardSpeed * LookAheadSeconds[s];
                for (int i = 0; i < blocked.Length; i++)
                {
                    Vector3 probe = new Vector3(aheadX, bottomY + i * ScanStep, position.z);
                    blocked[i] = Physics.CheckSphere(probe, config.BodyRadius, mapLayerMask,
                                                     QueryTriggerInteraction.Ignore);
                }

                if (FlappyGapAiming.TryFindGap(blocked, bottomY, ScanStep, position.y, config.BodyRadius,
                                               out float columnLow, out float columnHigh) == false)
                {
                    break;   // 이 기둥은 온통 막혔다 — 앞에서 찾은 것까지만 쓴다
                }

                if (hasGap == false)
                {
                    low = columnLow;
                    high = columnHigh;
                    hasGap = true;
                    continue;
                }
                if (FlappyGapAiming.TryIntersect(low, high, columnLow, columnHigh,
                                                 out float mergedLow, out float mergedHigh) == false)
                {
                    break;   // 여기서부터는 같은 높이로 못 지난다 — 앞쪽만 맞춘다
                }
                low = mergedLow;
                high = mergedHigh;
            }

            if (hasGap == false)
            {
                return;   // 앞이 온통 막혔다 — 날갯짓해도 소용없으니 그냥 둔다
            }

            TryDash(entity, position);

            //  몸 반지름만큼은 가장자리에서 떨어져 지나간다.
            if (FlappyGapAiming.ShouldFlap(position.y, velocity.y, low, high, ticksToGap, deltaTime,
                                           config.Gravity, config.MaxFallSpeed, config.FlapImpulse,
                                           margin: config.BodyRadius) == false)
            {
                return;
            }
            //  long.MinValue에 쿨다운을 더해도 넘치지 않으므로 첫 날갯짓은 그냥 통과한다.
            if (tick < lastFlapTick + Mathf.RoundToInt(FlapCooldownSeconds / deltaTime))
            {
                return;
            }

            lastFlapTick = tick;
            playerInputManager.SetJump(true);
#endif
        }

        /// <summary>
        /// 게이지가 가득이고 대시 거리만큼 앞이 뚫려 있으면 지른다. 아끼지 않는 이유는 게이지가
        /// 다시 차기 때문이다 — 쓸 수 있을 때 쓰는 것이 곧 최선이고, 사람도 그렇게 친다.
        /// </summary>
        private void TryDash(GameFramework.World.Entity entity, Vector3 position)
        {
            if ((entity.Get<FlappyDash>()?.Charge ?? 0f) < 1f)
            {
                return;
            }

            float reach = config.ForwardSpeed * config.DashMult * DashReachSeconds;
            for (float ahead = DashProbeStep; ahead <= reach; ahead += DashProbeStep)
            {
                var probe = new Vector3(position.x + ahead, position.y, position.z);
                if (Physics.CheckSphere(probe, config.BodyRadius, mapLayerMask, QueryTriggerInteraction.Ignore))
                {
                    return;   // 대시 경로가 막혔다 — 지르면 2배 속도로 박는다
                }
            }

            playerInputManager.SetDash(true);
        }
    }
}
