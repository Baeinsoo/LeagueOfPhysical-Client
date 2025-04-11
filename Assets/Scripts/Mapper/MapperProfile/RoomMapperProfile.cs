using AutoMapper;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class RoomMapperProfile : Profile
    {
        public RoomMapperProfile()
        {
            CreateMap<Room, RoomDto>();
            CreateMap<RoomDto, Room>();
        }
    }
}
