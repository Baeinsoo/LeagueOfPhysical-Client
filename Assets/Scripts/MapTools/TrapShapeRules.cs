using System.Collections.Generic;
using UnityEngine;

namespace LOP.MapTools
{
    /// <summary>
    /// 지형 표면의 한 점과 그 자리 법선. 콜라이더 종류를 모른다.
    /// 각도 문턱값(<see cref="ForwardOverhangRule"/>)이 성분 비교로 성립하려면 법선 길이가
    /// 1이어야 하므로, 생성자에서 항상 정규화해 저장한다 — 호출부가 정규화를 잊어도 안전하다.
    /// </summary>
    public readonly struct SurfaceSample
    {
        public readonly Vector3 Point;
        public readonly Vector3 Normal;

        public SurfaceSample(Vector3 point, Vector3 normal)
        {
            Point = point;
            Normal = normal.normalized;
        }
    }

    /// <summary>
    /// "이 모양은 덫이 될 만한가"를 표면 <b>하나</b>로 판단한다. <b>확정하지 않는다</b> — 여기서
    /// 걸린 자리는 굴려 보기의 씨앗이 될 뿐이고, 낌인지 아닌지는 기존 판정이 정한다.
    /// 규칙을 더하려면 이 인터페이스를 구현해 <see cref="TrapShapeRules.Default"/>에 넣는다.
    ///
    /// <para><b>열려 있는 범위 — 단일 표면 규칙까지다.</b> <see cref="IsSuspect"/>가 보는 건
    /// 표면 하나뿐이라, "이 면 혼자 어떤 모양인가"로 판단하는 새 규칙은 클래스 하나 추가 +
    /// <see cref="TrapShapeRules.Default"/>에 등록만으로 끝난다(훑기·씨앗 합치기·판정 불변).
    /// 반대로 <b>두 면 이상을 함께 봐야 하는 모양</b>(예: 바닥과 그 위로 기운 천장이 만드는
    /// 쐐기꼴)은 이 인터페이스로 표현할 수 없다 — 이웃 표면을 볼 방법이 없다. 그런 규칙이
    /// 필요해지면 이 인터페이스와, 이걸 호출하는 훑기 루프를 함께 바꿔야 한다. 다만 그 비용은
    /// 크지 않다 — 증거가 있는 두-면 모양이 아직 없어 지금 넓히지 않을 뿐, 나오면 손볼 곳은
    /// 이 파일과 훑기 루프 딱 둘이다.</para>
    /// </summary>
    public interface ITrapShapeRule
    {
        string Name { get; }
        bool IsSuspect(in SurfaceSample sample);
    }

    /// <summary>
    /// 머리 위를 <b>앞으로 기울어</b> 덮은 면. 이 프로젝트가 실측으로 찾아낸 덫 모양이다.
    ///
    /// <para>이동 커널은 수평으로 미끄러진 뒤 세로를 따로 한 번 쓸어 올린다. 위가 덮여 있으면 그
    /// sweep이 몇 cm에서 막혀 세로 속도가 0이 되고, 아무리 눌러도 오르지 못한다. 전진은 상수라
    /// 계속 밀어붙이고 뒤로 갈 수단은 없다. 뒤로 기운 끝면은 위로 열려 있어 안전하다 —
    /// 실측에서 1.68m 계단은 멀쩡했고 0.55m 계단이 덫이었다. 크기가 아니라 기울기다.</para>
    /// </summary>
    public sealed class ForwardOverhangRule : ITrapShapeRule
    {
        private readonly float minDownward;
        private readonly float minBackward;

        /// <param name="minDownward">법선이 이만큼은 아래를 봐야 덮개로 친다. 벽을 걸러낸다.</param>
        /// <param name="minBackward">법선이 이만큼은 뒤를 봐야 "앞으로 기운" 것이다. 평평한 천장을 걸러낸다.</param>
        public ForwardOverhangRule(float minDownward = 0.3f, float minBackward = 0.1f)
        {
            this.minDownward = minDownward;
            this.minBackward = minBackward;
        }

        public string Name => "앞으로 기운 덮개";

        //  전진이 +x이므로, 뒤를 향하는 법선은 x가 음수다.
        public bool IsSuspect(in SurfaceSample sample)
            => sample.Normal.y <= -minDownward && sample.Normal.x <= -minBackward;
    }

    public static class TrapShapeRules
    {
        /// <summary>오늘 쓰는 규칙. 새 모양이 드러나면 여기에 더한다.</summary>
        public static readonly IReadOnlyList<ITrapShapeRule> Default =
            new ITrapShapeRule[] { new ForwardOverhangRule() };
    }
}
