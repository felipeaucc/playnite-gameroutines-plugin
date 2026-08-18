using System;

namespace GameRoutines
{
    internal sealed class GameRoutinesUiStateChangedEventArgs : EventArgs
    {
        public Guid? GameId { get; }

        public GameRoutinesUiStateChangedEventArgs(Guid? gameId)
        {
            GameId = gameId;
        }

        public bool Affects(Guid gameId)
        {
            return !GameId.HasValue || GameId.Value == gameId;
        }
    }
}
