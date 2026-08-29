namespace GameRoutines
{
    internal static class RoutineStatePolicy
    {
        internal static TaskState GetChecklistDerivedState(RoutineSettings routine)
        {
            return ChecklistService.GetProgress(routine).IsComplete
                ? TaskState.COMPLETE
                : TaskState.INCOMPLETE;
        }

        internal static bool DoesRequestedStateAgreeWithChecklist(
            RoutineSettings routine,
            TaskState requestedState)
        {
            return GetChecklistDerivedState(routine) == requestedState;
        }

        internal static bool IsManualStateBlocked(
            RoutineSettings routine,
            TaskState requestedState)
        {
            return routine != null &&
                   routine.AutomaticallyCompleteFromChecklist &&
                   !DoesRequestedStateAgreeWithChecklist(routine, requestedState);
        }

        internal static bool OwnsAutomaticallyDerivedCompletion(
            RoutineSettings routine,
            TaskState derivedState)
        {
            return routine != null &&
                   routine.AutomaticallyCompleteFromChecklist &&
                   derivedState == TaskState.COMPLETE;
        }
    }
}
