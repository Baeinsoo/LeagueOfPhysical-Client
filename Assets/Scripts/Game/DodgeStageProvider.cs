using System.Collections.Generic;

namespace LOP
{
    /// <summary>TbDodgeStage → 공용 DodgeStageTable(id 순). 클·서가 같은 표를 읽어야 HUD와 진행기가 같은 스테이지를 가리킨다.</summary>
    public class DodgeStageProvider
    {
        private readonly LOP.MasterData.LOPMasterData md;

        public DodgeStageProvider(LOP.MasterData.LOPMasterData md)
        {
            this.md = md;
        }

        public DodgeStageTable Get()
        {
            var rows = new List<LOP.MasterData.DodgeStage>(md.Tables.TbDodgeStage.DataList);
            rows.Sort((a, b) => a.Id.CompareTo(b.Id));
            var stages = new List<DodgeStage>(rows.Count);
            foreach (var r in rows)
            {
                var kinds = new DodgePatternKind[r.Kinds.Count];
                for (int i = 0; i < kinds.Length; i++) kinds[i] = (DodgePatternKind)r.Kinds[i];
                stages.Add(new DodgeStage(r.Name, r.DurationSeconds, kinds, r.BaseIntensity, r.Tighten, r.IntervalSeconds));
            }
            return new DodgeStageTable(stages);
        }

        /// <summary>스테이지 해설(클라 전용 칸) — <see cref="Get"/>과 같은 id 순.</summary>
        public IReadOnlyList<string> Captions()
        {
            var rows = new List<LOP.MasterData.DodgeStage>(md.Tables.TbDodgeStage.DataList);
            rows.Sort((a, b) => a.Id.CompareTo(b.Id));
            return rows.ConvertAll(r => r.Caption ?? "");
        }
    }
}
