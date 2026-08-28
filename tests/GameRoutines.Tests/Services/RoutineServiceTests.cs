using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.ObjectModel;

namespace GameRoutines.Tests
{
    [TestClass]
    public class RoutineServiceTests
    {
        [TestMethod]
        public void GetOverallState_NullGame_ReturnsComplete()
        {
            Assert.AreEqual(TaskState.COMPLETE, RoutineService.GetOverallState(null));
        }

        [TestMethod]
        public void GetOverallState_EmptyRoutines_ReturnsComplete()
        {
            Assert.AreEqual(TaskState.COMPLETE, RoutineService.GetOverallState(TestSettingsFactory.Game()));
        }

        [TestMethod]
        public void GetOverallState_NoParticipatingRoutines_ReturnsComplete()
        {
            var routine = TestSettingsFactory.Routine(
                TestSettingsFactory.DailyRoutineId, "Ignored", 0, TaskState.INCOMPLETE, false);

            Assert.AreEqual(TaskState.COMPLETE, RoutineService.GetOverallState(TestSettingsFactory.Game(routine)));
        }

        [TestMethod]
        public void GetOverallState_AllParticipatingComplete_ReturnsComplete()
        {
            var first = TestSettingsFactory.Routine(TestSettingsFactory.DailyRoutineId, "Dailies", 0);
            var second = TestSettingsFactory.Routine(TestSettingsFactory.WeeklyRoutineId, "Weeklies", 1);

            Assert.AreEqual(TaskState.COMPLETE, RoutineService.GetOverallState(TestSettingsFactory.Game(first, second)));
        }

        [TestMethod]
        public void GetOverallState_OneParticipatingIncomplete_ReturnsIncomplete()
        {
            var first = TestSettingsFactory.Routine(TestSettingsFactory.DailyRoutineId, "Dailies", 0);
            var second = TestSettingsFactory.Routine(
                TestSettingsFactory.WeeklyRoutineId, "Weeklies", 1, TaskState.INCOMPLETE);

            Assert.AreEqual(TaskState.INCOMPLETE, RoutineService.GetOverallState(TestSettingsFactory.Game(first, second)));
        }

        [TestMethod]
        public void GetOverallState_IncompleteNonParticipatingRoutine_IsIgnored()
        {
            var participating = TestSettingsFactory.Routine(TestSettingsFactory.DailyRoutineId, "Dailies", 0);
            var ignored = TestSettingsFactory.Routine(
                TestSettingsFactory.WeeklyRoutineId, "Optional", 1, TaskState.INCOMPLETE, false);

            Assert.AreEqual(
                TaskState.COMPLETE,
                RoutineService.GetOverallState(TestSettingsFactory.Game(participating, ignored)));
        }

        [TestMethod]
        public void GetOverallState_MixedParticipation_UsesOnlyParticipatingRoutines()
        {
            var participating = TestSettingsFactory.Routine(
                TestSettingsFactory.DailyRoutineId, "Dailies", 2, TaskState.INCOMPLETE);
            var ignored = TestSettingsFactory.Routine(
                TestSettingsFactory.WeeklyRoutineId, "Optional", 0, TaskState.COMPLETE, false);

            Assert.AreEqual(
                TaskState.INCOMPLETE,
                RoutineService.GetOverallState(TestSettingsFactory.Game(ignored, participating)));
        }

        [TestMethod]
        public void GetParticipatingRoutines_NullAndNonParticipatingEntries_ExcludesAndOrdersCanonically()
        {
            var later = TestSettingsFactory.Routine(TestSettingsFactory.DailyRoutineId, "Later", 5);
            var earlier = TestSettingsFactory.Routine(TestSettingsFactory.WeeklyRoutineId, "Earlier", 1);
            var ignored = TestSettingsFactory.Routine(
                TestSettingsFactory.OtherRoutineId, "Ignored", 0, TaskState.COMPLETE, false);
            var game = new TrackedGameSettings
            {
                Routines = new ObservableCollection<RoutineSettings> { later, null, ignored, earlier }
            };

            var result = RoutineService.GetParticipatingRoutines(game);

            CollectionAssert.AreEqual(new[] { earlier, later }, new System.Collections.Generic.List<RoutineSettings>(result));
        }
    }
}
