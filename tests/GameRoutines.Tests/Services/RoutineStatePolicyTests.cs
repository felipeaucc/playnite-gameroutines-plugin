using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.ObjectModel;

namespace GameRoutines.Tests
{
    [TestClass]
    public class RoutineStatePolicyTests
    {
        [TestMethod]
        public void GetChecklistDerivedState_EmptyChecklist_ReturnsComplete()
        {
            Assert.AreEqual(
                TaskState.COMPLETE,
                RoutineStatePolicy.GetChecklistDerivedState(CreateRoutine(false)));
        }

        [TestMethod]
        public void GetChecklistDerivedState_FullyCheckedChecklist_ReturnsComplete()
        {
            var routine = CreateRoutine(true);
            routine.Checklist.Add(TestSettingsFactory.Item(
                TestSettingsFactory.ChecklistItemOneId, "One", true, 0));

            Assert.AreEqual(TaskState.COMPLETE, RoutineStatePolicy.GetChecklistDerivedState(routine));
        }

        [TestMethod]
        public void GetChecklistDerivedState_PartiallyCheckedChecklist_ReturnsIncomplete()
        {
            var routine = CreateRoutine(true);
            routine.Checklist.Add(TestSettingsFactory.Item(
                TestSettingsFactory.ChecklistItemOneId, "One", true, 0));
            routine.Checklist.Add(TestSettingsFactory.Item(
                TestSettingsFactory.ChecklistItemTwoId, "Two", false, 1));

            Assert.AreEqual(TaskState.INCOMPLETE, RoutineStatePolicy.GetChecklistDerivedState(routine));
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(1)]
        public void IsManualStateBlocked_AutoCompletionDisabled_AllowsRequestedState(int state)
        {
            var routine = CreateRoutine(false);

            Assert.IsFalse(RoutineStatePolicy.IsManualStateBlocked(routine, (TaskState)state));
        }

        [TestMethod]
        public void IsManualStateBlocked_AutoCompletionEnabledAndStateConflicts_ReturnsTrue()
        {
            var routine = CreateRoutine(true);
            routine.Checklist.Add(TestSettingsFactory.Item(
                TestSettingsFactory.ChecklistItemOneId, "One", false, 0));

            Assert.IsTrue(RoutineStatePolicy.IsManualStateBlocked(routine, TaskState.COMPLETE));
        }

        [TestMethod]
        public void DoesRequestedStateAgreeWithChecklist_MatchingState_ReturnsTrue()
        {
            var routine = CreateRoutine(true);
            routine.Checklist.Add(TestSettingsFactory.Item(
                TestSettingsFactory.ChecklistItemOneId, "One", false, 0));

            Assert.IsTrue(RoutineStatePolicy.DoesRequestedStateAgreeWithChecklist(
                routine, TaskState.INCOMPLETE));
            Assert.IsFalse(RoutineStatePolicy.IsManualStateBlocked(routine, TaskState.INCOMPLETE));
        }

        [TestMethod]
        public void OwnsAutomaticallyDerivedCompletion_AutomaticCompleteState_ReturnsTrue()
        {
            Assert.IsTrue(RoutineStatePolicy.OwnsAutomaticallyDerivedCompletion(
                CreateRoutine(true), TaskState.COMPLETE));
        }

        [TestMethod]
        public void OwnsAutomaticallyDerivedCompletion_AutomaticIncompleteState_ReturnsFalse()
        {
            Assert.IsFalse(RoutineStatePolicy.OwnsAutomaticallyDerivedCompletion(
                CreateRoutine(true), TaskState.INCOMPLETE));
        }

        [TestMethod]
        public void OwnsAutomaticallyDerivedCompletion_AutoCompletionDisabled_ReturnsFalse()
        {
            Assert.IsFalse(RoutineStatePolicy.OwnsAutomaticallyDerivedCompletion(
                CreateRoutine(false), TaskState.COMPLETE));
        }

        private static RoutineSettings CreateRoutine(bool automatic)
        {
            var routine = TestSettingsFactory.Routine(
                TestSettingsFactory.DailyRoutineId, "Dailies", 0);
            routine.AutomaticallyCompleteFromChecklist = automatic;
            routine.Checklist = new ObservableCollection<ChecklistItemSettings>();
            return routine;
        }
    }
}
