using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// Luban <c>TbArcheryConfig</c>(맵마다 한 행)과 <c>TbArcheryTarget</c>(종류 목록)을
    /// LOP-Shared <see cref="ArcheryConfig"/>로 옮기는 사이드 로컬 어댑터
    /// (Shared는 MasterData 패키지 비참조 → 여기서 변환. <see cref="SkydiveConfigProvider"/> 대칭).
    ///
    /// <para><b>서버에 같은 이름의 쌍둥이가 있고, 둘이 같은 값을 내야 한다.</b> 다르면 웨이브 계산이
    /// 갈려 서로 다른 과녁을 본다.</para>
    /// </summary>
    public class ArcheryConfigProvider
    {
        private readonly LOP.MasterData.LOPMasterData md;
        private readonly IRoomDataStore roomDataStore;

        public ArcheryConfigProvider(LOP.MasterData.LOPMasterData md, IRoomDataStore roomDataStore)
        {
            this.md = md;
            this.roomDataStore = roomDataStore;
        }

        public ArcheryConfig Get()
        {
            //  설정은 맵마다 다르다 — 같은 활쏘기라도 사거리 맵과 원형 맵은 과녁이 다르게 뜬다.
            //  이번 라운드가 가리키는 맵을 그대로 쓴다(씬을 고를 때와 같은 출처).
            int mapId = CurrentMapId();
            var r = md.Tables.TbArcheryConfig.GetOrDefault(mapId);
            if (r == null)
            {
                throw new System.InvalidOperationException(
                    $"TbArcheryConfig에 맵 {mapId}의 행이 없음 — 활쏘기 맵을 추가했으면 설정 행도 같이 넣어야 한다");
            }

            var kinds = new List<ArcheryTargetKind>();
            //  뽑기가 이 목록 순서에 기대므로 id로 정렬해 클·서가 같은 순서를 보게 못박는다
            //  (DataList는 테이블에 적힌 순서라 지금도 맞지만, 누가 엑셀 줄을 옮기면 조용히 갈린다).
            //  타입 이름을 쓰지 않고 정렬한다 — 생성된 LOP.MasterData.ArcheryTargetKind가 Shared의
            //  같은 이름과 겹쳐서, 이름을 적는 순간 늘 풀네임으로 구분해야 한다.
            foreach (var row in System.Linq.Enumerable.OrderBy(md.Tables.TbArcheryTarget.DataList, x => x.Id))
            {
                kinds.Add(new ArcheryTargetKind(row.Radius, row.Points, row.Weight, row.IsTrap,
                    (ArcheryTargetShape)row.Shape, BandsOf(md, row.Id)));
            }
            if (kinds.Count == 0)
            {
                throw new System.InvalidOperationException(
                    "TbArcheryTarget이 비어 있음 — 과녁 종류가 없으면 웨이브가 영원히 빈다");
            }

            var courseKind = (ArcheryCourseKind)r.CourseKind;
            //  모르는 값을 그냥 두면 캐스트가 조용히 통과해 웨이브로 굴러간다 — 사거리 맵이
            //  원형처럼 돌면서 에러는 하나도 안 난다. 여기서 크게 터뜨린다.
            if (courseKind != ArcheryCourseKind.Wave && courseKind != ArcheryCourseKind.Range
                                                      && courseKind != ArcheryCourseKind.ShootOff)
            {
                throw new System.InvalidOperationException(
                    $"맵 {mapId}의 course_kind({r.CourseKind})를 모른다 — 0(웨이브)·1(사거리)·2(한 발 승부)여야 한다");
            }

            var range = ArcheryRangeSettings.None;
            if (courseKind == ArcheryCourseKind.Range || courseKind == ArcheryCourseKind.ShootOff)
            {
                var faceRow = md.Tables.TbArcheryTarget.GetOrDefault(r.RangeTargetId);
                if (faceRow == null)
                {
                    throw new System.InvalidOperationException(
                        $"맵 {mapId}의 range_target_id({r.RangeTargetId})가 TbArcheryTarget에 없다");
                }

                //  자리 줄 순서 — 한 발 승부는 Id 오름차순(같은 자리를 라운드마다 다시 쓰므로
                //  줄 순서 = 라운드 순서다), 사거리는 자리 번호 오름차순(코스가 이 차례를 그대로 쓴다).
                var rows = System.Linq.Enumerable.Where(md.Tables.TbArcheryRange.DataList,
                                                        x => x.MapId == mapId);
                var ordered = courseKind == ArcheryCourseKind.ShootOff
                    ? System.Linq.Enumerable.OrderBy(rows, x => x.Id)
                    : System.Linq.Enumerable.OrderBy(rows, x => x.StandIndex);

                var stands = new List<ArcheryRangeStand>();
                foreach (var row in ordered)
                {
                    stands.Add(new ArcheryRangeStand(row.StandIndex, row.DistanceM, row.ExposureTicks,
                                                     row.LateralSpanM, row.LateralPeriodS, row.FaceRadiusM,
                                                     row.WindMps2, row.PointsMultiplier));
                }
                if (stands.Count == 0)
                {
                    throw new System.InvalidOperationException(
                        $"맵 {mapId}은 사거리/한 발 승부 코스인데 TbArcheryRange에 줄이 없다 — 과녁이 영영 안 뜬다");
                }

                range = new ArcheryRangeSettings(
                    new ArcheryTargetKind(faceRow.Radius, faceRow.Points, faceRow.Weight, faceRow.IsTrap,
                                          (ArcheryTargetShape)faceRow.Shape, BandsOf(md, faceRow.Id)),
                    stands, r.StepGapTicks, r.ArrowsPerStand,
                    r.BoxHalfWidthM, r.BoxHalfDepthM, r.MoveSpeedMps);
            }

            return new ArcheryConfig(
                r.WavePeriodTicks, r.MinTargets, r.MaxTargets,
                r.SpawnRadius, r.SpawnMinY, r.SpawnMaxY, r.MinSeparation,
                r.TrapRatioMin, r.TrapRatioMax,
                r.ShakeFreeSeconds, r.ShakeRampSeconds, r.ShakeMaxDegrees,
                r.RiseHeightMin, r.RiseHeightMax, r.StaggerTicks, r.RestTicks,
                kinds,
                courseKind, r.MatchDurationTicks, range,
                //  shakeBaseRatio는 ArcheryConfig 생성자에서 기본값 있는 매개변수 맨 끝에 있다
                //  (C# 제약 — 위 주석 참고) — 여기서도 그 자리에 맞춰 마지막에 넘긴다.
                r.ShakeBaseRatio,
                //  0이면 예전 화살 속도(ArcheryAimSystem.MinSpeed/MaxSpeed)다 — 한 발 승부만 느리게 적는다.
                r.ArrowMinSpeedMps, r.ArrowMaxSpeedMps);
        }

        //  그 과녁 종류의 띠를 중심에서 바깥 순서로 모은다. 순서가 뒤집히면 바깥 띠가 먼저
        //  걸려서 한가운데를 맞혀도 낮은 점수가 나온다.
        private static List<ArcheryRingBand> BandsOf(LOP.MasterData.LOPMasterData md, int targetId)
        {
            var bands = new List<ArcheryRingBand>();
            foreach (var row in System.Linq.Enumerable.OrderBy(
                         System.Linq.Enumerable.Where(md.Tables.TbArcheryRing.DataList,
                                                      x => x.TargetId == targetId),
                         x => x.OuterRatio))
            {
                bands.Add(new ArcheryRingBand(row.OuterRatio, row.Points));
            }
            return bands;
        }

        //  씬을 고를 때와 같은 출처를 쓴다 — 두 곳이 다른 라운드를 보면 맵과 설정이 어긋난다.
        private int CurrentMapId()
        {
            var rounds = roomDataStore.match?.rounds;
            int index = MatchSceneResolver.CurrentRoundIndex(rounds?.Length ?? 0);
            return rounds[index].mapId;
        }
    }
}
