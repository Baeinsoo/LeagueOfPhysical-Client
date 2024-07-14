using AutoMapper;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameFramework;
using System;

namespace LOP
{
    public static class MapperConfig
    {
        private static readonly Lazy<IMapper> lazyMapper = new Lazy<IMapper>(InitializeMapper, true);
        public static IMapper mapper => lazyMapper.Value;

        private static IMapper InitializeMapper()
        {
            var config = new MapperConfiguration(cfg =>
            {
                foreach (var profileType in GetMapperProfileTypes().OrEmpty())
                {
                    cfg.AddProfile(profileType);
                }
            });

            return config.CreateMapper();
        }

        private static IEnumerable<Type> GetMapperProfileTypes()
        {
            return Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => t.IsClass
                    && !t.IsAbstract
                    && typeof(Profile).IsAssignableFrom(t)
                    && !t.IsDefined(typeof(IgnoreMapperProfileAttribute), false)
                );
        }
    }
}
