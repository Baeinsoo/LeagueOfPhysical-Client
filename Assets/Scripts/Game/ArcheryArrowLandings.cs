using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public enum ArcheryLandingKind { Target, Ground, Crowd }

    public readonly struct ArcheryArrowLanding
    {
        public readonly string ShooterId;
        public readonly ArcheryLandingKind Kind;
        public readonly Vector3 Point;
        /// <summary>과녁에 꽂히면서 먼저 꽂힌 화살을 쪼갰다(로빈 후드).</summary>
        public readonly bool SplitArrow;

        public ArcheryArrowLanding(string shooterId, ArcheryLandingKind kind, Vector3 point, bool splitArrow)
        {
            ShooterId = shooterId;
            Kind = kind;
            Point = point;
            SplitArrow = splitArrow;
        }
    }

    /// <summary>
    /// 화살이 과녁·땅·관중석에 꽂히는 순간을 알린다(연출용). 한 스코프 안·한 프레임 안의 알림이라 C# 이벤트로 충분하다.
    /// 관중석 충돌체는 관중석 뷰가 등록한다 — 화살 뷰는 맞은 충돌체가 관중석 것인지로 땅과 가른다.
    /// </summary>
    public sealed class ArcheryArrowLandings
    {
        private readonly HashSet<Collider> crowd = new HashSet<Collider>();

        public event System.Action<ArcheryArrowLanding> Landed;

        public void Publish(in ArcheryArrowLanding landing) => Landed?.Invoke(landing);

        public void AddCrowdCollider(Collider c) => crowd.Add(c);

        public void RemoveCrowdCollider(Collider c) => crowd.Remove(c);

        public bool IsCrowd(Collider c) => c != null && crowd.Contains(c);
    }
}
