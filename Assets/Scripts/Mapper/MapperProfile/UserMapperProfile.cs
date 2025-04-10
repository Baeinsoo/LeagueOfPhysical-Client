using AutoMapper;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class UserMapperProfile : Profile
    {
        public UserMapperProfile()
        {
            CreateMap<User, UserDto>();
            CreateMap<UserDto, User>();

            CreateMap<UserProfile, UserProfileDto>();
            CreateMap<UserProfileDto, UserProfile>();

            CreateMap<UserLocation, UserLocationDto>();
            CreateMap<UserLocationDto, UserLocation>();

            CreateMap<UserStats, UserStatsDto>();
            CreateMap<UserStatsDto, UserStats>();
        }
    }
}
