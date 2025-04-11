using AutoMapper;
using UnityEngine;

namespace LOP
{
    public class MatchMapperProfile : Profile
    {
        public MatchMapperProfile()
        {
            CreateMap<Match, MatchDto>();
            CreateMap<MatchDto, Match>();
        }
    }
}
