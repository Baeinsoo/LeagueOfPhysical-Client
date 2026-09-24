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
                                ArcheryCourse course, ArcheryShootOffLineupView lineupView)
        {
            this.runner = runner;
            this.world = world;
            this.consumed = consumed;
            this.stickSystem = stickSystem;
            this.course = course;
            this.lineupView = lineupView;
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

            var shots = world.Shots;
            var alive = new HashSet<(string, long)>();

            for (int i = 0; i < shots.Count; i++)
            {
                if (consumed.IsArrowGone(shots[i].ShooterId, shots[i].FireTick))
                {
                    continue;   // 과녁에 박혔다 — 계속 날아가는 그림은 거짓말이다
                }

                var key = (shots[i].ShooterId, shots[i].FireTick);
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

                //  꽂혔는지는 틱 시스템이 정한다 — 뷰는 그 결과를 읽어 과녁에 붙여 그릴 뿐이다.
                if (stickSystem.TryGetImpact(shots[i].ShooterId, shots[i].FireTick, out var impact))
                {
                    //  꽂힌 과녁이 사라지면(내가 아니라 남이 먹었어도) 화살도 같이 치운다 —
                    //  안 그러면 아무것도 없는 허공에 박힌 채로 남는다.
                    if (consumed.IsTargetGone(impact.Wave, impact.Slot))
                    {
                        continue;
                    }
                    if (stickSystem.TryGetTarget(impact.Wave, impact.Slot, out var target) == false)
                    {
                        continue;
                    }
                    //  수명이 끝난 과녁은 땅 아래로 내려간 것이다 — 따라가면 화살이 바닥을 뚫고 들어간다.
                    if (ArcheryTargetMotion.IsAlive(target, renderTick, (float)interval) == false)
                    {
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
                    continue;
                }

                float seconds = (float)((renderTick - shots[i].FireTick) * interval);
                if (seconds < 0f)
                {
                    seconds = 0f;   // 아직 떠나기 전 프레임 — 출발점에 둔다
                }

                Vector3 position = ArcheryTrajectory.PositionAt(shots[i], seconds);
                var velocity = ArcheryTrajectory.VelocityAt(shots[i], seconds);

                //  이번 프레임에 지나온 구간이 땅·벽을 통과했으면 거기서 멈춰 꽂는다. 예전엔
                //  그냥 지나쳐 땅 밑으로 들어갔다 사라져서 **어디에 빗나갔는지 볼 수가 없었다.**
                float previousSeconds = Mathf.Max(0f, seconds - Time.deltaTime);
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
                arrow.transform.position = position
                    + lineupView.DisplayOffsetOf(shots[i].ShooterId)
                    * ArcheryShootOffLineup.ArrowBlend(seconds, FlightSecondsOf(shots[i]));
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
        }

        //  과녁까지 가는 데 걸리는 시간 = 과녁 거리 ÷ 수평 속도.
        private float FlightSecondsOf(in ArcheryShot shot)
        {
            if (course.SharedLane == null)
            {
                return 0f;
            }
            int step = course.IndexAt(shot.FireTick, world.GameplayStartTick);
            float distance = course.StandDistanceAt(step);
            float speed = new Vector2(shot.Velocity.x, shot.Velocity.z).magnitude;
            return speed > 0.01f ? distance / speed : 0f;
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

            if (_arrowMaterial != null)
            {
                Object.Destroy(_arrowMaterial);
                _arrowMaterial = null;
            }
        }
    }
}
