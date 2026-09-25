using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 날아가는 화살을 그린다. <b>시뮬과 같은 식에 같은 시각을 넣으므로</b> 그림과 계산이 어긋나지 않는다.
    /// 화살은 엔티티가 아니라서 뷰가 직접 월드의 발사 목록을 읽는다.
    /// </summary>
    public class ArcheryArrowView : ILateTickable, System.IDisposable
    {
        private readonly GameFramework.Runner.IRunner runner;
        private readonly ArcheryWorld world;
        private readonly ArcheryConsumed consumed;
        private readonly ArcheryArrowStickSystem stickSystem;
        private readonly ArcheryCourse course;
        private readonly ArcheryShootOffLineupView lineupView;
        private readonly IGameDataStore gameDataStore;

        //  남의 화살은 발사 소식이 늦게 온다 — 처음 받았을 때 이미 흘러 있던 시간(초)을 적어 두고
        //  그 순간부터 활에서 출발시켜 따라잡게 그린다(ArcheryArrowDisplay.VisualSeconds).
        private readonly Dictionary<(string shooterId, long fireTick), float> arrivalSeconds =
            new Dictionary<(string, long), float>();
        private readonly List<(string, long)> forgotten = new List<(string, long)>();

        //  안 사라지는 과녁(사거리·한 발 승부)에 꽂힌 화살. 월드의 발사 목록은 3초 뒤 화살을 지우지만
        //  과녁은 그보다 오래 서 있다 — 여러 발이 한 과녁에 꽂혀 있는 것 자체가 보여 줄 내용이라,
        //  과녁이 내려갈 때까지 뷰가 따로 들고 있는다.
        private readonly Dictionary<(string shooterId, long fireTick), StuckArrow> stuck =
            new Dictionary<(string, long), StuckArrow>();
        private readonly List<(string, long)> unstuck = new List<(string, long)>();

        private readonly struct StuckArrow
        {
            public readonly GameObject Arrow;
            public readonly ArcheryTarget Target;
            public readonly int Wave;
            public readonly int Slot;
            public readonly Vector3 OffsetFromTarget;

            public StuckArrow(GameObject arrow, ArcheryTarget target, int wave, int slot, Vector3 offsetFromTarget)
            {
                Arrow = arrow;
                Target = target;
                Wave = wave;
                Slot = slot;
                OffsetFromTarget = offsetFromTarget;
            }
        }

        // 목록의 자리(index)로 화살을 알아보면 안 된다 — 수명이 다한 화살이 빠지면 뒤 화살들의
        // 자리가 앞으로 당겨져서, 남아 있는 화살이 남의 궤적으로 순간이동한다.
        // 쏜 사람과 쏜 틱은 절대 안 바뀌므로 그 둘을 이름으로 쓴다.
        private readonly Dictionary<(string shooterId, long fireTick), GameObject> drawn =
            new Dictionary<(string, long), GameObject>();
        private readonly List<(string, long)> stale = new List<(string, long)>();

        //  빗나가 땅·벽에 꽂힌 화살. 이 목록은 world.Shots의 수명(3초)과 **따로 논다** —
        //  조준선이 없어진 뒤로 "내 화살이 어디 떨어졌나"가 거리별 낙차를 익히는 유일한 단서라,
        //  화살이 사라지기 전에 눈으로 확인할 시간이 있어야 한다.
        private readonly Dictionary<(string shooterId, long fireTick), LandedArrow> landed =
            new Dictionary<(string, long), LandedArrow>();
        private readonly List<(string, long)> expired = new List<(string, long)>();

        //  꽂힌 자리에 이만큼 남는다(초). 90m 과녁이 화면에서 작으므로 넉넉히 준다.
        private const float LandedSeconds = 6f;

        //  Character는 뺀다 — 화살이 사수 눈높이에서 나가므로 넣으면 제 몸에 바로 꽂히고,
        //  옆 레인 사수 몸에도 걸린다. 과녁은 콜라이더가 없어 여기 안 걸린다(판정은 시뮬이 한다).
        private readonly int worldLayerMask = LayerMask.GetMask("Default");

        private readonly struct LandedArrow
        {
            public readonly GameObject Arrow;
            public readonly float RemoveAtTime;

            public LandedArrow(GameObject arrow, float removeAtTime)
            {
                Arrow = arrow;
                RemoveAtTime = removeAtTime;
            }
        }


        public ArcheryArrowView(GameFramework.Runner.IRunner runner, ArcheryWorld world,
                                ArcheryConsumed consumed, ArcheryArrowStickSystem stickSystem,
                                ArcheryCourse course, ArcheryShootOffLineupView lineupView,
                                IGameDataStore gameDataStore)
        {
            this.runner = runner;
            this.world = world;
            this.consumed = consumed;
            this.stickSystem = stickSystem;
            this.course = course;
            this.lineupView = lineupView;
            this.gameDataStore = gameDataStore;
        }


        //  화살마다 material을 새로 만들면 재질 인스턴스가 계속 쌓인다 — 한 장을 돌려 쓴다.
        private Material _arrowMaterial;

        private Material ArrowMaterial()
        {
            if (_arrowMaterial == null)
            {
                //  이름으로 찾는 셰이더는 Graphics ▸ Always Included Shaders에 있어야 폰 빌드에서도
                //  잡힌다 — 없으면 null이 와서 매 프레임 터진다(RuntimeShaderInclusionTests가 지킨다).
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                _arrowMaterial = new Material(shader) { color = Color.red };
            }
            return _arrowMaterial;
        }

        public void LateTick()
        {
            if (runner?.tickUpdater == null)
            {
                return;   // 씬 진입 초기거나 언로드 도중 — 러너가 아직/더 이상 안 물려 있다
            }
            double interval = runner.tickUpdater.interval;
            if (interval <= 0d)
            {
                return;
            }
            double renderTick = (runner.tickUpdater.elapsedTime - interval) / interval;

            //  목록에서 사라진 화살의 "박혔다" 기록을 계속 들고 있을 이유가 없다.
            consumed.ForgetArrowsBefore(
                (long)renderTick - (long)(ArcheryTrajectory.LifetimeSeconds / interval) - 1);

            RemoveExpiredLandedArrows();
            UpdateStuckArrows(renderTick, (float)interval);

            var shots = world.Shots;
            var alive = new HashSet<(string, long)>();

            for (int i = 0; i < shots.Count; i++)
            {
                //  꽂혔는지는 틱 시스템이 정한다 — 뷰는 그 결과를 읽어 과녁에 붙여 그릴 뿐이다.
                bool hasImpact = stickSystem.TryGetImpact(shots[i].ShooterId, shots[i].FireTick, out var impact);
                ArcheryTarget target = default;
                bool hasTarget = hasImpact && stickSystem.TryGetTarget(impact.Wave, impact.Slot, out target);
                bool consumedOnHit = hasTarget == false || ArcheryHitRules.ConsumedOnHit(target);
                if (ArcheryArrowDisplay.HideConfirmedHit(
                        consumed.IsArrowGone(shots[i].ShooterId, shots[i].FireTick), hasImpact, consumedOnHit))
                {
                    continue;   // 과녁과 함께 사라졌다 — 계속 날아가는 그림은 거짓말이다
                }

                var key = (shots[i].ShooterId, shots[i].FireTick);
                if (stuck.ContainsKey(key))
                {
                    continue;   // 안 사라지는 과녁에 꽂혔다 — 그 그림은 stuck이 따로 들고 있다
                }
                if (landed.ContainsKey(key))
                {
                    continue;   // 이미 땅·벽에 꽂혔다 — 그 그림은 landed가 따로 들고 있다
                }
                alive.Add(key);

                if (drawn.TryGetValue(key, out var arrow) == false || arrow == null)
                {
                    arrow = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    //  임시 그림이라 실물 비례보다 눈에 띄는 것이 우선이다 — 흰 5cm 막대는
                    //  12m 밖에서 사실상 안 보여서 "안 나갔나 안 보이나"를 가릴 수 없었다.
                    arrow.transform.localScale = new Vector3(0.15f, 0.15f, 0.8f);
                    var renderer = arrow.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.sharedMaterial = ArrowMaterial();
                    }
                    Object.Destroy(arrow.GetComponent<Collider>());   // 그림일 뿐이다 — 판정은 시뮬이 한다
                    drawn[key] = arrow;
                }

                if (hasImpact)
                {
                    //  꽂힌 과녁이 사라지면(내가 아니라 남이 먹었어도) 화살도 같이 치운다 —
                    //  안 그러면 아무것도 없는 허공에 박힌 채로 남는다.
                    if (consumed.IsTargetGone(impact.Wave, impact.Slot) || hasTarget == false
                        //  수명이 끝난 과녁은 땅 아래로 내려간 것이다 — 따라가면 화살이 바닥을 뚫고 들어간다.
                        || ArcheryTargetMotion.IsAlive(target, renderTick, (float)interval) == false)
                    {
                        alive.Remove(key);
                        continue;
                    }

                    //  과녁이 움직여도 꽂힌 자리(오프셋)는 그대로다 — 과녁을 따라 화살도 같이 움직인다.
                    arrow.transform.position =
                        ArcheryTargetMotion.PositionAt(target, renderTick, (float)interval) + impact.OffsetFromTarget;

                    //  회전은 꽂힌 순간의 방향으로 고정한다 — 과녁을 따라 움직인다고 화살이 돌지는 않는다.
                    var stuckVelocity = ArcheryTrajectory.VelocityAt(shots[i], impact.Seconds);
                    if (stuckVelocity.sqrMagnitude > 1e-6f)
                    {
                        arrow.transform.rotation = Quaternion.LookRotation(stuckVelocity);
                    }

                    if (consumedOnHit == false)
                    {
                        alive.Remove(key);
                        drawn.Remove(key);
                        stuck[key] = new StuckArrow(arrow, target, impact.Wave, impact.Slot, impact.OffsetFromTarget);
                    }
                    continue;
                }

                float trueSeconds = (float)((renderTick - shots[i].FireTick) * interval);
                if (trueSeconds < 0f)
                {
                    trueSeconds = 0f;   // 아직 떠나기 전 프레임 — 출발점에 둔다
                }

                float arrival = ArrivalSecondsOf(key, shots[i].ShooterId, trueSeconds, (float)interval);
                float flight = FlightSecondsOf(shots[i]);
                //  같은 궤적을 시간만 늦춰 그리므로 꽂히는 자리·땅에 떨어지는 자리는 그대로다.
                float seconds = ArcheryArrowDisplay.VisualSeconds(trueSeconds, arrival, flight);

                Vector3 position = ArcheryTrajectory.PositionAt(shots[i], seconds);
                var velocity = ArcheryTrajectory.VelocityAt(shots[i], seconds);

                //  이번 프레임에 지나온 구간이 땅·벽을 통과했으면 거기서 멈춰 꽂는다. 예전엔
                //  그냥 지나쳐 땅 밑으로 들어갔다 사라져서 **어디에 빗나갔는지 볼 수가 없었다.**
                float previousSeconds = ArcheryArrowDisplay.VisualSeconds(
                    Mathf.Max(0f, trueSeconds - Time.deltaTime), arrival, flight);
                Vector3 previous = ArcheryTrajectory.PositionAt(shots[i], previousSeconds);
                if (previousSeconds < seconds
                    && Physics.Linecast(previous, position, out RaycastHit ground,
                                        worldLayerMask, QueryTriggerInteraction.Ignore))
                {
                    //  촉이 표면에 닿고 몸통은 밖에 남게 살짝 앞으로 밀어 넣는다.
                    Vector3 heading = velocity.sqrMagnitude > 1e-6f ? velocity.normalized : Vector3.down;
                    arrow.transform.position = ground.point + heading * 0.2f;
                    arrow.transform.rotation = Quaternion.LookRotation(heading);

                    alive.Remove(key);
                    drawn.Remove(key);
                    landed[key] = new LandedArrow(arrow, Time.time + LandedSeconds);
                    continue;
                }

                //  한 발 승부: 남의 화살은 화면 속 그 캐릭터의 활에서 떠나 실제 꽂힐 점으로 모인다.
                //  꽂히는 자리는 진짜고, 날아가는 모양만 연출이다(판정도, 위의 땅 충돌 검사도 이 값을 안 본다).
                Vector3 displayOffset = lineupView.DisplayOffsetOf(shots[i].ShooterId);
                if (displayOffset != Vector3.zero)
                {
                    position += displayOffset * ArcheryShootOffLineup.ArrowBlend(seconds, flight);
                }
                arrow.transform.position = position;
                if (velocity.sqrMagnitude > 1e-6f)
                {
                    arrow.transform.rotation = Quaternion.LookRotation(velocity);
                }
            }

            stale.Clear();
            foreach (var pair in drawn)
            {
                if (alive.Contains(pair.Key) == false)
                {
                    stale.Add(pair.Key);
                }
            }
            foreach (var key in stale)
            {
                Object.Destroy(drawn[key]);
                drawn.Remove(key);
            }

            forgotten.Clear();
            foreach (var key in arrivalSeconds.Keys)
            {
                if (alive.Contains(key) == false && landed.ContainsKey(key) == false && stuck.ContainsKey(key) == false)
                {
                    forgotten.Add(key);
                }
            }
            foreach (var key in forgotten)
            {
                arrivalSeconds.Remove(key);
            }
        }

        //  내 화살은 예측이라 쏜 틱에 바로 생긴다(0). 남의 것은 처음 본 순간 이미 흘러 있던 시간이다.
        //  한 틱보다 적게 늦은 것은 늦은 게 아니다 — 그대로 그린다.
        private float ArrivalSecondsOf((string, long) key, string shooterId, float trueSeconds, float interval)
        {
            if (arrivalSeconds.TryGetValue(key, out float arrival))
            {
                return arrival;
            }
            arrival = shooterId == gameDataStore.userEntityId || trueSeconds <= interval ? 0f : trueSeconds;
            arrivalSeconds[key] = arrival;
            return arrival;
        }

        //  과녁까지 가는 데 걸리는 시간 = 과녁 거리 ÷ 수평 속도. 레인이 없는 맵(원형)은 모른다(0).
        private float FlightSecondsOf(in ArcheryShot shot)
        {
            if (course.IsLaned == false)
            {
                return 0f;
            }
            int step = course.IndexAt(shot.FireTick, world.GameplayStartTick);
            float distance = course.StandDistanceAt(step);
            float speed = new Vector2(shot.Velocity.x, shot.Velocity.z).magnitude;
            return speed > 0.01f ? distance / speed : 0f;
        }

        //  꽂힌 채 과녁을 따라 움직이다가, 과녁이 내려가거나 치워지면 같이 치운다.
        private void UpdateStuckArrows(double renderTick, float interval)
        {
            if (stuck.Count == 0)
            {
                return;
            }

            unstuck.Clear();
            foreach (var pair in stuck)
            {
                var s = pair.Value;
                if (s.Arrow == null || consumed.IsTargetGone(s.Wave, s.Slot)
                    || ArcheryTargetMotion.IsAlive(s.Target, renderTick, interval) == false)
                {
                    unstuck.Add(pair.Key);
                    continue;
                }
                s.Arrow.transform.position = ArcheryTargetMotion.PositionAt(s.Target, renderTick, interval) + s.OffsetFromTarget;
            }
            foreach (var key in unstuck)
            {
                if (stuck[key].Arrow != null)
                {
                    Object.Destroy(stuck[key].Arrow);
                }
                stuck.Remove(key);
            }
        }

        private void RemoveExpiredLandedArrows()
        {
            if (landed.Count == 0)
            {
                return;
            }

            expired.Clear();
            foreach (var pair in landed)
            {
                if (Time.time >= pair.Value.RemoveAtTime)
                {
                    expired.Add(pair.Key);
                }
            }
            foreach (var key in expired)
            {
                Object.Destroy(landed[key].Arrow);
                landed.Remove(key);
            }
        }

        // 판이 끝나면 그리던 화살과 재질을 같이 치운다 — 재질은 우리가 만든 것이라
        // 아무도 대신 지워 주지 않는다.
        public void Dispose()
        {
            foreach (var pair in drawn)
            {
                Object.Destroy(pair.Value);
            }
            drawn.Clear();

            foreach (var pair in landed)
            {
                Object.Destroy(pair.Value.Arrow);
            }
            landed.Clear();

            foreach (var pair in stuck)
            {
                Object.Destroy(pair.Value.Arrow);
            }
            stuck.Clear();

            if (_arrowMaterial != null)
            {
                Object.Destroy(_arrowMaterial);
                _arrowMaterial = null;
            }
        }
    }
}
