using System;

namespace WeeklyManager
{
    internal sealed class WeeklyManagerUiStateChangedEventArgs : EventArgs
    {
        public Guid? GameId { get; }

        public WeeklyManagerUiStateChangedEventArgs(Guid? gameId)
        {
            GameId = gameId;
        }

        public bool Affects(Guid gameId)
        {
            return !GameId.HasValue || GameId.Value == gameId;
        }
    }
}
