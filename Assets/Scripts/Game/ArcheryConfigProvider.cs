using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// Luban <c>TbArcheryConfig</c>(전역 단일 행, id=1)과 <c>TbArcheryTarget</c>(종류 목록)을
    /// LOP-Shared <see cref="ArcheryConfig"/>로 옮기는 사이드 로컬 어댑터
    /// (Shared는 MasterData 패키지 비참조 → 여기서 변환. <see cref="SkydiveConfigProvider"/> 대칭).
    ///
    /// <para><b>서버에 같은 이름의 쌍둥이가 있고, 둘이 같은 값을 내야 한다.</b> 다르면 웨이브 계산이
    /// 갈려 서로 다른 과녁을 본다.</para>
    /// </summary>
    public class ArcheryConfigProvider
    {
        private readonly LOP.MasterData.LOPMasterData md;

        public ArcheryConfigProvider(LOP.MasterData.LOPMasterData md)
        {
            this.md = md;
        }

        public ArcheryConfig Get()
        {
            var r = md.Tables.TbArcheryConfig.GetOrDefault(1);
            if (r == null)
            {
                throw new System.InvalidOperationException(
                    "TbArcheryConfig id=1 행을 찾을 수 없음 — MasterData 미로드 또는 ArcheryConfig 데이터 누락");
            }

            var kinds = new List<ArcheryTargetKind>();
            //  뽑기가 이 목록 순서에 기대므로 id로 정렬해 클·서가 같은 순서를 보게 못박는다
            //  (DataList는 테이블에 적힌 순서라 지금도 맞지만, 누가 엑셀 줄을 옮기면 조용히 갈린다).
            //  타입 이름을 쓰지 않고 정렬한다 — 생성된 LOP.MasterData.ArcheryTargetKind가 Shared의
            //  같은 이름과 겹쳐서, 이름을 적는 순간 늘 풀네임으로 구분해야 한다.
            foreach (var row in System.Linq.Enumerable.OrderBy(md.Tables.TbArcheryTarget.DataList, x => x.Id))
            {
                kinds.Add(new ArcheryTargetKind(row.Radius, row.Points, row.Weight, row.IsTrap,
                    ArcheryTargetShape.Sphere, null));
            }
            if (kinds.Count == 0)
            {
                throw new System.InvalidOperationException(
                    "TbArcheryTarget이 비어 있음 — 과녁 종류가 없으면 웨이브가 영원히 빈다");
            }

            return new ArcheryConfig(
                r.WavePeriodTicks, r.MinTargets, r.MaxTargets,
                r.SpawnRadius, r.SpawnMinY, r.SpawnMaxY, r.MinSeparation,
                r.TrapRatioMin, r.TrapRatioMax,
                r.ShakeFreeSeconds, r.ShakeRampSeconds, r.ShakeMaxDegrees,
                r.RiseHeightMin, r.RiseHeightMax, r.StaggerTicks, r.RestTicks,
                kinds);
        }
    }
}
