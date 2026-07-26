using AutoMapper;
using UnityEngine;
using GameFramework;

namespace LOP
{
    public class ProtoMapperProfile : Profile
    {
        public ProtoMapperProfile()
        {
            CreateMap<ProtoVector3, Vector3>();
            CreateMap<Vector3, ProtoVector3>();

            CreateMap<ProtoTransform, EntityTransform>();
            CreateMap<EntityTransform, ProtoTransform>();

            // statusEffects는 이름이 겹쳐(StatusEffects) AutoMapper가 자동 매핑을 시도하지만
            // ProtoActiveEffect→ActiveEffect 맵이 없어 런타임에 터진다 — 핸들러가 수동으로 채우므로 무시.
            CreateMap<global::EntitySnap, EntitySnap>()
                .ForMember(dest => dest.statusEffects, opt => opt.Ignore());
        }
    }
}
