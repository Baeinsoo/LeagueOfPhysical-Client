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
        //  세 지점을 보고 그 틈들이 겹치는 구간을 목표로 삼는다 — 앞이 뚫렸어도 그다음이 막혔으면
        //  거기로 가면 안 되기 때문이다.
        private static readonly float[] LookAheadSeconds = { 0.20f, 0.40f, 0.60f };
        //  훑는 간격. 새 몸(0.9m)보다 훨씬 작아 틈을 놓치지 않는다.
        private const float ScanStep = 0.25f;
        //  훑는 범위(현재 높이 기준 위아래). 코스의 통로 높이(약 18m)를 덮는다.
        private const float ScanRange = 20f;
        //  연속 탭 방지. 없으면 봇이 틈 바닥에서 매 프레임 눌러 호버링하는데, 그러면 사람이 나는
        //  모양이 아니라서 정작 관찰하려는 "남의 새가 어떻게 보이나"가 실제 플레이와 달라진다.
        private const float FlapCooldown = 0.12f;

        private readonly IRunner runner;
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly PlayerInputManager playerInputManager;
        private readonly FlappyConfig config;
        private readonly int mapLayerMask;

        private readonly bool[] blocked = new bool[(int)(ScanRange * 2f / ScanStep) + 1];
        private float lastFlapTime = float.NegativeInfinity;

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
            int ticksToGap = 0;
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
                    ticksToGap = Mathf.RoundToInt(LookAheadSeconds[s] / deltaTime);
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

            //  몸 반지름만큼은 가장자리에서 떨어져 지나간다.
            if (FlappyGapAiming.ShouldFlap(position.y, velocity.y, low, high, ticksToGap, deltaTime,
                                           config.Gravity, config.MaxFallSpeed, config.FlapImpulse,
                                           margin: config.BodyRadius) == false)
            {
                return;
            }
            if (Time.time - lastFlapTime < FlapCooldown)
            {
                return;
            }

            lastFlapTime = Time.time;
            playerInputManager.SetJump(true);
#endif
        }
    }
}
