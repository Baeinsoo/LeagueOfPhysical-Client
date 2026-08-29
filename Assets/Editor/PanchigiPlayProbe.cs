using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// 플레이 중인 판치기를 밖에서 몰기 위한 손잡이. 두 에디터(메인 + MPPM 클론)를 CLI로
    /// 몰아 낙·탈락 같은 흐름을 사람 손 없이 재현할 때 쓴다.
    ///
    /// <para><b>왜 "치기 전에 기다리나".</b> 클라 화면이 "내 차례"라고 해도 서버는 아직 동전이
    /// 구르는 중일 수 있다. 그 상태로 보낸 타격은 서버가 <i>조용히</i> 버리고(로그만 남는다)
    /// 화면에는 아무 일도 안 일어난다 — 실제로 이걸 모르고 한참을 헤맸다. 그래서 여기서는
    /// <b>동전이 실제로 멎었는지</b>를 직접 확인하고 나서 친다.</para>
    /// </summary>
    public static class PanchigiPlayProbe
    {
        //  판 위에 있는 동전만 센다 — 플레이어 신원 엔티티는 판 아래(-10)에 있다.
        private const float BelowBoard = -5f;

        //  이 값보다 덜 움직였으면 "멎었다"로 본다. 서버 기준(RestSpeedEpsilon)과 같을 필요는
        //  없다 — 우리는 두 번 읽은 위치를 비교하는 것뿐이고, 판정은 어차피 서버가 한다.
        private const float StillEpsilon = 0.002f;

        private static Vector3[] lastPositions;

        //  마지막 Status가 본 "멎었나". Strike가 다시 재면 방금 Status가 갱신한 위치와
        //  자기 자신을 비교하게 되어 늘 "멎었다"가 된다 — 판정은 한 번만 하고 나눠 쓴다.
        private static bool lastStill;

        //  PressAndHold가 눌러 둔 자리 — Release가 이 자리로 뗀다. Strike처럼 한 호출 안에서
        //  Begin·End를 다 끝내면 그 사이에 프레임이 지나가지 않아 게이지가 절대 안 보인다.
        //  그래서 뗄 자리만 기억해 두고, 실제로 손을 떼는 시점(Release)은 호출부가 정하게 둔다.
        private static bool hasPendingHold;
        private static Vector3 pendingEnd;

        /// <summary>지금 판이 어떤 상태인지 한 줄로. 호출부(CLI)가 이걸 보고 다음 행동을 정한다.</summary>
        public static string Status()
        {
            if (TryGetWorld(out var world) == false)
            {
                return "판 없음";
            }
            if (TryGetStrikeInput(out var input, out var store, out string myEntityId) == false)
            {
                return "타격 입력 없음";
            }

            Vector3[] positions = CoinPositions(world);
            bool still = IsStill(positions);
            lastStill = still;

            var sb = new StringBuilder();
            sb.Append("국면=").Append(store.Phase.CurrentValue == 1 ? "조준" : "정산");
            sb.Append(" 차례=").Append(string.IsNullOrEmpty(store.CurrentEntityId.CurrentValue)
                ? "없음" : store.CurrentEntityId.CurrentValue);
            sb.Append(" 나=").Append(myEntityId);
            sb.Append(" 동전=").Append(positions.Length);
            sb.Append(" 멎음=").Append(still);
            sb.Append(" 낙=").Append(store.GetDropOutCount(myEntityId));
            sb.Append(" 탈락=").Append(store.IsEliminated(myEntityId));
            sb.Append(" 게이지=").Append(input.Charge.ToString("F2"));
            sb.Append(" z=[").Append(string.Join(",", positions.Select(v => v.z.ToString("F2")))).Append("]");
            return sb.ToString();
        }

        /// <summary>
        /// 칠 수 있으면 치고, 아니면 왜 못 치는지 돌려준다. <see cref="Status"/>와 따로 부르면
        /// 그 사이에 차례가 넘어가 헛치게 된다(20초마다 넘어간다) — 한 번에 끝내는 쪽을 쓴다.
        /// </summary>
        public static string StrikeWhenReady(float targetZ = 0.75f, float power = 3f, float holdSeconds = 1f)
        {
            string before = Status();   // 여기서 "멎었나"가 정해진다
            string result = Strike(targetZ, power, holdSeconds);
            return result + " | " + before;
        }

        /// <summary>
        /// 지금 칠 수 있으면 친다. 없으면 왜 못 치는지 돌려준다.
        /// </summary>
        /// <param name="targetZ">판의 세로축에서 어느 자리를 칠지. 동전은 이 축을 따라 놓인다.</param>
        /// <param name="power">끄는 세기. 설정 상한을 넘으면 서버가 거절하므로 상한에서 자른다.</param>
        /// <param name="holdSeconds">누른 시간. 위와 같이 상한에서 자른다.</param>
        public static string Strike(float targetZ = 0.75f, float power = 3f, float holdSeconds = 1f)
        {
            if (TryGetStrikeInput(out var input, out var store, out string myEntityId) == false)
            {
                return "타격 입력 없음";
            }
            if (store.Phase.CurrentValue != 1 || store.CurrentEntityId.CurrentValue != myEntityId)
            {
                return "내 차례가 아니다";
            }
            if (TryGetWorld(out var world) == false)
            {
                return "판 없음";
            }
            if (lastStill == false)
            {
                //  화면은 "내 차례"인데 동전이 아직 구르는 중이다. 여기서 보내면 서버가 버린다.
                //  판정은 Status가 한다 — 여기서 다시 재면 방금 읽은 값과 자기를 비교하게 된다.
                return "동전이 아직 안 멎었다 (Status를 먼저 두 번 부를 것)";
            }

            var config = MasterDataOf(input);
            if (config == null)
            {
                return "TbPanchigiConfig(1)이 없다";
            }

            var collector = CollectorOf(input);
            if (collector == null)
            {
                return "수집기가 아직 없다";
            }

            float clampedPower = Mathf.Min(power, config.StrikePowerMax);
            float clampedHold = Mathf.Min(holdSeconds, config.HoldTimeMax);

            //  판 윗면을 손가락이 짚은 것처럼 만든다 — 시작점에서 목표 자리까지 끌고 뗀다.
            float surfaceY = BoardSurfaceY();
            var start = new Vector3(0f, surfaceY, targetZ - clampedPower);
            var end = new Vector3(0f, surfaceY, targetZ);

            collector.Clear();
            float now = Time.time;
            collector.Begin(-1, start, now);
            collector.Update(-1, end);
            collector.End(-1, end, now + clampedHold, config.HoldTimeMax, config.StrikePowerMax);

            //  수집기를 채워 두면 다음 프레임의 Update가 알아서 보낸다 — 사람이 손을 뗐을 때와
            //  같은 경로를 지나가므로, 전송 배선까지 함께 검증된다.
            return $"쳤다 z={targetZ} 세기={clampedPower} 누름={clampedHold}";
        }

        /// <summary>
        /// 판을 누르고 <b>계속</b> 누르고 있는다(뗴지 않는다) — <see cref="Strike"/>는 한 호출
        /// 안에서 눌렀다 떼버려 게이지가 절대 안 보인다. 이 사이 <see cref="Status"/>를 여러
        /// 번 불러 게이지가 오르는 것을 눈으로 확인한 뒤 <see cref="Release"/>로 뗀다.
        /// </summary>
        /// <param name="targetZ">뗄 때 판 세로축 자리. 시작점은 여기서 <paramref name="power"/>만큼 당겨 잡는다.</param>
        /// <param name="power">끌 세기. 뗄 때 상한에서 잘린다.</param>
        public static string PressAndHold(float targetZ = 0.75f, float power = 3f)
        {
            if (TryGetStrikeInput(out var input, out var store, out string myEntityId) == false)
            {
                return "타격 입력 없음";
            }
            if (store.IsAimingTurnOf(myEntityId) == false)
            {
                return "내 차례가 아니다";
            }

            var config = MasterDataOf(input);
            if (config == null)
            {
                return "TbPanchigiConfig(1)이 없다";
            }

            var collector = CollectorOf(input);
            if (collector == null)
            {
                return "수집기가 아직 없다";
            }

            float surfaceY = BoardSurfaceY();
            var start = new Vector3(0f, surfaceY, targetZ - power);
            var end = new Vector3(0f, surfaceY, targetZ);

            collector.Clear();
            collector.Begin(-1, start, Time.time);
            collector.Update(-1, end);

            hasPendingHold = true;
            pendingEnd = end;

            return $"눌렀다 z={targetZ} 세기={power} — Status로 게이지를 지켜본 뒤 Release로 뗄 것";
        }

        /// <summary>
        /// <see cref="PressAndHold"/>가 눌러 둔 손가락을 뗀다. 다음 프레임의 기존 입력 경로가
        /// 그대로 타격을 내보낸다 — 사람이 손을 뗐을 때와 같은 길이다.
        /// </summary>
        public static string Release()
        {
            if (hasPendingHold == false)
            {
                return "누르고 있는 것이 없다 (PressAndHold를 먼저 부를 것)";
            }
            if (TryGetStrikeInput(out var input, out var store, out string myEntityId) == false)
            {
                hasPendingHold = false;
                return "타격 입력 없음";
            }

            var config = MasterDataOf(input);
            var collector = CollectorOf(input);
            if (config == null || collector == null)
            {
                hasPendingHold = false;
                return "TbPanchigiConfig(1) 또는 수집기가 없다";
            }

            float charge = input.Charge;
            collector.End(-1, pendingEnd, Time.time, config.HoldTimeMax, config.StrikePowerMax);
            hasPendingHold = false;

            return $"뗐다 (누르고 있던 동안 게이지={charge:F2})";
        }

        /// <summary>연속 호출로 "멎었나"를 보려면 직전 위치가 필요하다. 새 판을 시작할 때 지운다.</summary>
        public static string Reset()
        {
            lastPositions = null;
            lastStill = false;
            hasPendingHold = false;
            return "직전 위치 지움";
        }

        private static bool IsStill(Vector3[] positions)
        {
            Vector3[] previous = lastPositions;
            lastPositions = positions;

            if (previous == null || previous.Length != positions.Length || positions.Length == 0)
            {
                return false;   // 비교할 짝이 없다 — 한 번 더 불러야 안다
            }

            for (int i = 0; i < positions.Length; i++)
            {
                if ((positions[i] - previous[i]).sqrMagnitude > StillEpsilon * StillEpsilon)
                {
                    return false;
                }
            }
            return true;
        }

        private static Vector3[] CoinPositions(GameFramework.World.IWorld world)
        {
            return world.EntityRegistry.All
                .Select(e => e.Get<GameFramework.World.Transform>())
                .Where(t => t != null && t.Position.Y > BelowBoard)
                .Select(t => new Vector3(t.Position.X, t.Position.Y, t.Position.Z))
                .ToArray();
        }

        private static float BoardSurfaceY()
        {
            var board = Object.FindFirstObjectByType<PanchigiBoard>();
            return board != null ? board.Bounds.max.y : 0f;
        }

        private static bool TryGetWorld(out GameFramework.World.IWorld world)
        {
            world = null;
            var runner = Object.FindFirstObjectByType<LOPRunner>();
            if (runner == null) { return false; }

            world = FieldOf<GameFramework.World.IWorld>(runner, "world");
            return world != null;
        }

        private static bool TryGetStrikeInput(out PanchigiStrikeInput input,
            out PanchigiStateStore store, out string myEntityId)
        {
            input = Object.FindFirstObjectByType<PanchigiStrikeInput>();
            store = null;
            myEntityId = null;
            if (input == null) { return false; }

            store = FieldOf<PanchigiStateStore>(input, "stateStore");
            object playerContext = FieldOf<object>(input, "playerContext");
            myEntityId = playerContext?.GetType().GetProperty("entityId")?.GetValue(playerContext) as string;
            return store != null;
        }

        private static PanchigiContactCollector CollectorOf(PanchigiStrikeInput input)
            => FieldOf<PanchigiContactCollector>(input, "collector");

        private static LOP.MasterData.PanchigiConfig MasterDataOf(PanchigiStrikeInput input)
            => FieldOf<LOP.MasterData.LOPMasterData>(input, "masterData")?.Tables.TbPanchigiConfig.GetOrDefault(1);

        //  Public도 함께 본다 — 필드 접근자는 우리가 정하는 게 아니고, 빠뜨리면 조용히 null이 된다.
        private static T FieldOf<T>(object target, string name) where T : class
        {
            var field = target.GetType().GetField(name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(target) as T;
        }
    }
}
