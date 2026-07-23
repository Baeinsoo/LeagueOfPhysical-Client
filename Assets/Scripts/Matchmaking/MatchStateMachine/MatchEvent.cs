
namespace LOP
{
    public enum MatchEvent
    {
        //  External (UI)
        PlayClicked,
        CancelClicked,

        //  Internal — matchmaking request result
        MatchRequestSucceeded,
        MatchRequestFailed,

        //  Internal — resolved user location (shared by several states)
        LocationIsGameRoom,
        LocationIsWaitingRoom,
        LocationIsNone,

        //  Internal — need to re-resolve location (after an error / cancel)
        RecheckRequested,
    }
}
