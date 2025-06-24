using AutoMapper;
using UnityEngine;

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

            CreateMap<global::EntitySnap, EntitySnap>();
        }
    }
}
