using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 테마 물건의 크기·높이·흔들림·거인 자리(클라, 그림만). 크기는 판정 값에서 계산한다. 억울함은 판정이 그림보다
    /// 클 때 생긴다("안 닿았는데 맞았다") — 선수 몸 그림(반지름 0.35)이 몸 판정(0.16)보다 커서, 물건 그림이 판정보다
    /// 조금 작아도 몸과 먼저 겹쳐 보인 뒤에 맞는다. 조건: 물건 그림 + 몸 그림 ≥ 물건 판정 + 몸 판정(테마 스펙 §2).
    /// </summary>
    public static class DodgePropPose
    {
        public const float SlipperHeight = 0.35f;
        public const float SlipperSpinDegreesPerTick = 24f;
        public const float MelonDiameter = 0.9f;
        public const float MelonDropHeight = 9f;
        public const float RopeLiftHeight = 3f;
        public const float RopeThickness = 0.18f;
        public const float JarWobbleDegrees = 7f;
        /// <summary>줄 끝에서 거인이 서는 거리. 줄 끝이 벽 안쪽(±9)이라 벽 바깥(±10)을 넘게.</summary>
        public const float GiantBack = 2.2f;
        public const float GiantScale = 3f;

        public static bool IsBullet(DodgePatternKind k) =>
            k == DodgePatternKind.BulletRain || k == DodgePatternKind.BulletWall || k == DodgePatternKind.BulletAimed;

        /// <summary>바닥 층이 그리나. 탄은 슬리퍼, 굴러가는 장독은 물건 층이 그린다 — 나머지(예고·과즙·줄 자리·온돌)는 바닥이다.</summary>
        public static bool DrawnOnGround(in DodgeShape s) =>
            !IsBullet(s.Kind) && !(s.Kind == DodgePatternKind.Rock && s.Active);

        public static float SlipperLength(float bulletRadius) => bulletRadius * 2f;

        /// <summary>
        /// 슬리퍼 색 번호. 위치로 고르면 틱마다 바뀌어 번쩍인다 — 탄은 직선으로 날아가므로 진행 방향에 수직인 거리
        /// (날아가는 줄의 자리)는 비행 내내 같다. 그 값으로 고른다. 멈춰 있으면(첫 틱) 0.
        /// </summary>
        public static int SlipperPick(Vector2 prev, Vector2 now, int count)
        {
            Vector2 d = now - prev;
            if (d.sqrMagnitude < 1e-8f || count <= 0)
            {
                return 0;
            }
            d.Normalize();
            float lane = prev.x * d.y - prev.y * d.x;
            return Mathf.Abs(Mathf.RoundToInt(lane * 2f)) % count;
        }

        /// <summary>장독 메시는 반지름 0.5 구 안에 든다 → 판정 지름만큼 키운다.</summary>
        public static float JarScale(float rockRadius) => rockRadius * 2f;

        public static float MelonHeight(float progress)
        {
            float p = Mathf.Clamp01(progress);
            return MelonDropHeight * (1f - p * p);   // 중력처럼 — 처음엔 느리게, 바닥에 가까울수록 빨리
        }

        public static float RopeHeight(bool active, float progress) =>
            active ? 0f : RopeLiftHeight * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));

        public static float WobbleDegrees(float progress, double renderTick) =>
            JarWobbleDegrees * Mathf.Clamp01(progress) * Mathf.Sin((float)(renderTick * 0.9));

        public static float RollDegrees(double renderTick, float speed, float radius) =>
            (float)(renderTick / DodgeConfig.TicksPerSecond * speed / Mathf.Max(radius, 0.01f) * Mathf.Rad2Deg);

        public static float HeadingDegrees(Vector2 from, Vector2 to) =>
            Mathf.Atan2(to.x - from.x, to.y - from.y) * Mathf.Rad2Deg;

        public static (Vector2 a, Vector2 b) GiantSpots(Vector2 end0, Vector2 end1)
        {
            Vector2 dir = (end1 - end0).normalized;
            return (end0 - dir * GiantBack, end1 + dir * GiantBack);
        }
    }
}
