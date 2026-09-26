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

        //  화살을 처음 본 순간 이미 흘러 있던 시간(초). 남의 화살은 소식이 늦어 0이 아니다.
        private readonly Dictionary<(string shooterId, long fireTick), float> firstSeenSeconds =
            new Dictionary<(string, long), float>();
        private readonly HashSet<(string, long)> seenKeys = new HashSet<(string, long)>();

        //  꼬리선. 빠른 화살(20m에 0.2초)은 몇 프레임만 그려져 눈에 안 걸린다 — 지나온 길을 선으로
        //  남겨 궤적이 보이게 한다(총알의 예광탄과 같은 역할).
        private const float TrailSeconds = 0.18f;

        //  재접속처럼 아주 늦게 본 화살까지 처음부터 다시 날리진 않는다.
        private const float MaxDisplayDelaySeconds = 0.5f;
        private const int TrailPoints = 12;
        private readonly Dictionary<(string shooterId, long fireTick), LineRenderer> trails =
            new Dictionary<(string, long), LineRenderer>();
        private readonly HashSet<(string, long)> trailLive = new HashSet<(string, long)>();
        private Material _trailMaterial;

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
            UpdateStuckArrows(renderTick, (float)interval);

            var shots = world.Shots;
            var alive = new HashSet<(string, long)>();
            trailLive.Clear();
            seenKeys.Clear();

            for (int i = 0; i < shots.Count; i++)
            {
                var key = (shots[i].ShooterId, shots[i].FireTick);
                seenKeys.Add(key);

                float trueSeconds = (float)((renderTick - shots[i].FireTick) * interval);
                if (trueSeconds < 0f)
                {
                    trueSeconds = 0f;   // 아직 떠나기 전 프레임 — 출발점에 둔다
                }
                float flight = FlightSecondsOf(shots[i]);
                float firstSeen = FirstSeenSecondsOf(key, trueSeconds);

                //  늦게 본 화살은 늦게 본 만큼 늦게 그린다 — 처음 보는 순간 활에서 출발한다.
                //  아래 그림은 전부 이 시각(seconds)으로 그린다.
                float delay = ArcheryArrowDisplay.DisplayDelaySeconds(firstSeen, MaxDisplayDelaySeconds);
                float seconds = Mathf.Max(0f, trueSeconds - delay);

                //  꽂혔는지는 틱 시스템이 정한다 — 뷰는 그 결과를 읽어 과녁에 붙여 그릴 뿐이다.
                bool hasImpact = stickSystem.TryGetImpact(shots[i].ShooterId, shots[i].FireTick, out var impact);
                ArcheryTarget target = default;
                bool hasTarget = hasImpact && stickSystem.TryGetTarget(impact.Wave, impact.Slot, out target);
                bool consumedOnHit = hasTarget == false || ArcheryHitRules.ConsumedOnHit(target);
                //  늦게 그리는 화살은 실제로 꽂힌 뒤에도 잠깐 더 날아간다 — 그림이 과녁에 닿아야 꽂는다.
                bool arrived = hasImpact && seconds >= impact.Seconds;

                //  늦게 그리는 사이 과녁은 더 움직였다 — 실제로 꽂힌 자리가 아니라 지금 과녁 자리에
                //  닿도록, 그 차이를 비행 동안 조금씩 얹는다(늦추지 않으면 0이다). 꽂힌 뒤에도
                //  꼬리가 과녁으로 빨려 들어가는 동안 같은 값을 써야 꼬리가 화살에서 안 떨어진다.
                Vector3 targetShift = Vector3.zero;
                if (hasTarget)
                {
                    double impactTick = shots[i].FireTick + impact.Seconds / interval;
                    targetShift = ArcheryTargetMotion.PositionAt(target, renderTick, (float)interval)
                                - ArcheryTargetMotion.PositionAt(target, impactTick, (float)interval);
                }
                float shiftUntil = hasTarget ? impact.Seconds : 0f;
                if (ArcheryArrowDisplay.HideConfirmedHit(
                        consumed.IsArrowGone(shots[i].ShooterId, shots[i].FireTick), hasImpact, consumedOnHit))
                {
                    continue;   // 과녁과 함께 사라졌다 — 계속 날아가는 그림은 거짓말이다
                }

                if (landed.ContainsKey(key))
                {
                    continue;   // 이미 땅·벽에 꽂혔다 — 그 그림은 landed가 따로 들고 있다
                }

                //  꼬리선: 꽂힌 뒤에도 꼬리가 과녁까지 빨려 들어갈 때까지 그린다.
                UpdateTrail(key, shots[i], seconds, arrived ? impact.Seconds : seconds,
                            Mathf.Max(0f, firstSeen - delay), flight, targetShift, shiftUntil);

                if (stuck.ContainsKey(key))
                {
                    continue;   // 안 사라지는 과녁에 꽂혔다 — 그 그림은 stuck이 따로 들고 있다
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

                if (arrived)
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
                    RemoveTrail(key);
                    continue;
                }

                arrow.transform.position = DisplayPositionAt(shots[i], seconds, flight, targetShift, shiftUntil);
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

            stale.Clear();
            foreach (var key in trails.Keys)
            {
                if (trailLive.Contains(key) == false)
                {
                    stale.Add(key);
                }
            }
            foreach (var key in stale)
            {
                RemoveTrail(key);
            }

            stale.Clear();
            foreach (var key in firstSeenSeconds.Keys)
            {
                if (seenKeys.Contains(key) == false)
                {
                    stale.Add(key);
                }
            }
            foreach (var key in stale)
            {
                firstSeenSeconds.Remove(key);
            }
        }

        //  한 발 승부: 남의 화살은 화면 속 그 캐릭터의 활에서 떠나 실제 꽂힐 점으로 모인다.
        //  꽂히는 자리는 진짜고, 날아가는 모양만 연출이다(판정도, 땅 충돌 검사도 이 값을 안 본다).
        private Vector3 DisplayPositionAt(in ArcheryShot shot, float seconds, float flight,
                                          Vector3 targetShift, float shiftUntil)
        {
            Vector3 position = ArcheryTrajectory.PositionAt(shot, seconds);
            Vector3 displayOffset = lineupView.DisplayOffsetOf(shot.ShooterId);
            if (displayOffset != Vector3.zero)
            {
                position += displayOffset * ArcheryShootOffLineup.ArrowBlend(seconds, flight);
            }
            if (shiftUntil > 0f)
            {
                position += targetShift * Mathf.Clamp01(seconds / shiftUntil);
            }
            return position;
        }

        //  남의 화살은 발사 소식이 늦게 와서 비행 중간에 처음 보인다. 그때 이미 흘러 있던 시간을
        //  적어 두고, 꼬리선이 그 뒤로 본 길만 그리게 한다.
        private float FirstSeenSecondsOf((string, long) key, float seconds)
        {
            if (firstSeenSeconds.TryGetValue(key, out float first))
            {
                return first;
            }
            firstSeenSeconds[key] = seconds;
            return seconds;
        }

        //  꼬리는 화살이 지나온 실제 궤적 위의 점들이다 — 머리(지금 또는 꽂힌 순간)부터
        //  꼬리 끝(TrailTailSeconds)까지. 꼬리 끝이 머리를 따라잡으면 선을 치운다.
        private void UpdateTrail((string, long) key, in ArcheryShot shot, float seconds, float headSeconds,
                                 float firstSeen, float flight, Vector3 targetShift, float shiftUntil)
        {
            float tail = ArcheryArrowDisplay.TrailTailSeconds(seconds, firstSeen, TrailSeconds);
            if (tail >= headSeconds - 1e-4f)
            {
                return;   // 다 빨려 들어갔다 — 이번 프레임 목록에 안 넣으면 끝에서 치운다
            }
            trailLive.Add(key);

            if (trails.TryGetValue(key, out var line) == false || line == null)
            {
                var go = new GameObject("ArrowTrail");
                line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = TrailMaterial();
                line.positionCount = TrailPoints;
                line.useWorldSpace = true;
                line.startWidth = 0.02f;   // 꼬리 끝은 가늘게
                line.endWidth = 0.09f;     // 화살 쪽은 굵게
                line.numCapVertices = 2;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trails[key] = line;
            }

            for (int j = 0; j < TrailPoints; j++)
            {
                float t = Mathf.Lerp(tail, headSeconds, j / (float)(TrailPoints - 1));
                line.SetPosition(j, DisplayPositionAt(shot, t, flight, targetShift, shiftUntil));
            }
        }

        private void RemoveTrail((string, long) key)
        {
            if (trails.TryGetValue(key, out var line))
            {
                if (line != null)
                {
                    Object.Destroy(line.gameObject);
                }
                trails.Remove(key);
            }
        }

        private Material TrailMaterial()
        {
            if (_trailMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                _trailMaterial = new Material(shader) { color = new Color(1f, 0.6f, 0.35f) };
            }
            return _trailMaterial;
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

            foreach (var pair in trails)
            {
                if (pair.Value != null)
                {
                    Object.Destroy(pair.Value.gameObject);
                }
            }
            trails.Clear();

            if (_trailMaterial != null)
            {
                Object.Destroy(_trailMaterial);
                _trailMaterial = null;
            }

            if (_arrowMaterial != null)
            {
                Object.Destroy(_arrowMaterial);
                _arrowMaterial = null;
            }
        }
    }
}
