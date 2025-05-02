using UnityEngine;
using GameFramework;

namespace LOP.Static
{
    public static class GameContext
    {
        public static IGame game => SceneLifetimeScope.Resolve<IGame>();
    }
}
