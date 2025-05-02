using UnityEngine;
using GameFramework;

namespace LOP
{
    public static class GameLocator
    {
        public static IGame game => SceneLifetimeScope.Resolve<IGame>();
    }
}
