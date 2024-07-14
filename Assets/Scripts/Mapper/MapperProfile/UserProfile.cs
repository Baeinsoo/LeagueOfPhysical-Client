using AutoMapper;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class UserProfile : Profile
    {
        public UserProfile()
        {
            CreateMap<User, UserDto>();
            CreateMap<UserDto, User>();
        }
    }
}
