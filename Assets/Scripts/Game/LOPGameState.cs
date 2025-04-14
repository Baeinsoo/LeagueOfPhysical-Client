using GameFramework;
using UnityEngine;

namespace LOP
{
    public class None : IGameState
    {
        public static readonly None State = new None();
        private None() { }
    }

    public class Initializing : IGameState
    {
        public static readonly Initializing State = new Initializing();
        private Initializing() { }
    }

    public class Initialized : IGameState
    {
        public static readonly Initialized State = new Initialized();
        private Initialized() { }
    }
    
    public class Preparing : IGameState
    {
        public static readonly Preparing State = new Preparing();
        private Preparing() { }
    }

    public class Prepared : IGameState
    {
        public static readonly Prepared State = new Prepared();
        private Prepared() { }
    }

    public class Playing : IGameState
    {
        public static readonly Playing State = new Playing();
        private Playing() { }
    }

    public class Paused : IGameState
    {
        public static readonly Paused State = new Paused();
        private Paused() { }
    }

    public class GameOver : IGameState
    {
        public static readonly GameOver State = new GameOver();
        private GameOver() { }
    }

    public class Error : IGameState
    {
        public static readonly Error State = new Error();
        private Error() { }
    }
}
