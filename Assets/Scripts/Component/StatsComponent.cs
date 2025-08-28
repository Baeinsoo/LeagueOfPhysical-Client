using GameFramework;
using LOP.Event.Entity;
using System.ComponentModel;

namespace LOP
{
    public class StatsComponent : LOPComponent
    {
        //  기본 능력치 (Primary Stats)
        private int _strength;  //  (힘) 물리 공격력 증가, 장비 착용 조건(무기, 갑옷 등)
        public int strength
        {
            get => _strength;
            set
            {
                this.SetProperty(ref _strength, value, RaisePropertyChanged);
            }
        }

        private int _dexterity; //  (민첩) 공격 정확도, 크리티컬, 회피율, 원거리 데미지 증가, 공격속도
        public int dexterity
        {
            get => _dexterity;
            set
            {
                this.SetProperty(ref _dexterity, value, RaisePropertyChanged);
            }
        }

        private int _intelligence; //  (지능) 마법 공격력 증가, 마나/자원 관련 보너스, 마나량
        public int intelligence
        {
            get => _intelligence;
            set
            {
                this.SetProperty(ref _intelligence, value, RaisePropertyChanged);
            }
        }

        private int _vitality; //  (체력) HP 증가, 생존력 강화, 체력 회복
        public int vitality
        {
            get => _vitality;
            set
            {
                this.SetProperty(ref _vitality, value, RaisePropertyChanged);
            }
        }

        //  전투 관련 스탯 (Combat Stats)
        public int attackPower { get; set; } //  기본 공격력(물리, 원소 포함)
        public int criticalChance { get; set; } //  치명타 확률
        public float criticalDamage { get; set; } //  치명타 데미지 배율
        public float attackSpeed { get; set; } //  초당 공격 횟수
        public float castSpeed { get; set; } //  마법 시전 속도
        public float accuracy { get; set; } //  명중률
        public float dodge { get; set; } //  회피율

        //  방어 관련 스탯 (Defense Stats)
        public int armor { get; set; } //  물리 피해 감소
        public int blockChance { get; set; } //  방패 등으로 공격을 막을 확률
        public int resistancesFire { get; set; } //  화염 저항
        public int resistancesCold { get; set; } //  냉기 저항
        public int resistancesLightning { get; set; } //  번개 저항
        public int resistancesPoison { get; set; } //  독 저항
        public float damageReduction { get; set; } //  받는 피해 일정 % 감소 (DR, capped 등)

        //  자원 관련 스탯 (Resource Stats)
        public int resourceRegeneration { get; set; } //  자원 회복 속도 (마나, 에너지 등)

        //  상태 이상 및 저항 관련
        public int stunChance { get; set; } //  기절 확률
        public float stunDuration { get; set; } //  기절 지속시간
        public int slowChance { get; set; } //  느려지는 효과 확률
        public float slowDuration { get; set; } //  느려지는 효과 지속시간
        public float freezeChance { get; set; } //  얼어붙는 효과 확률
        public float chillChance { get; set; } //  Chill 효과 확률
        public int crowdControlResistance { get; set; } //  군중 제어 저항력
        public int dotResistance { get; set; } //  지속 피해 저항 (출혈, 중독 등)

        //기타 고급 스탯 (Advanced Stats)
        public int lifeLeech { get; set; } //  타격 시 생명 흡수
        public int manaLeech { get; set; } //  타격 시 마나 흡수
        public int lifeOnHit { get; set; } //  히트 시 생명 회복
        public int manaOnHit { get; set; } //  히트 시 마나 회복
        public int areaDamage { get; set; } //  광역 피해 증가
        public float cooldownReduction { get; set; } //  스킬 재사용 시간 감소 (CDR)
        public float resourceCostReduction { get; set; } //  스킬 자원 소모량 감소
        public int elementalDamageBonus { get; set; } //  특정 속성 데미지 증가 (불, 냉기 등)
        public int pierce { get; set; } //  방어력, 저항력 관통
        public int thorns { get; set; } //  피격 시 반사 데미지

        public void RaisePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            EventBus.Default.Publish(EventTopic.EntityId<LOPEntity>(entity.entityId), new PropertyChange(e.PropertyName));
        }

        public void Initialize(string characterCode)
        {
        }
    }
}
