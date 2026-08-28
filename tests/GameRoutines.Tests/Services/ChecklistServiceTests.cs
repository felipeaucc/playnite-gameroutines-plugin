using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace GameRoutines.Tests
{
    [TestClass]
    public class ChecklistServiceTests
    {
        [TestMethod]
        public void GetProgress_NullOrEmptyChecklist_ReturnsZeroAndComplete()
        {
            var nullProgress = ChecklistService.GetProgress(null);
            var emptyProgress = ChecklistService.GetProgress(TestSettingsFactory.Routine(
                TestSettingsFactory.DailyRoutineId, "Dailies", 0));

            AssertProgress(nullProgress, 0, 0, true);
            AssertProgress(emptyProgress, 0, 0, true);
        }

        [TestMethod]
        public void GetProgress_AllChecked_ReturnsComplete()
        {
            var routine = RoutineWithItems(
                TestSettingsFactory.Item(TestSettingsFactory.ChecklistItemOneId, "One", true, 0),
                TestSettingsFactory.Item(TestSettingsFactory.ChecklistItemTwoId, "Two", true, 1));

            AssertProgress(ChecklistService.GetProgress(routine), 2, 2, true);
        }

        [TestMethod]
        public void GetProgress_OneUnchecked_ReturnsIncomplete()
        {
            var routine = RoutineWithItems(
                TestSettingsFactory.Item(TestSettingsFactory.ChecklistItemOneId, "One", true, 0),
                TestSettingsFactory.Item(TestSettingsFactory.ChecklistItemTwoId, "Two", false, 1));

            AssertProgress(ChecklistService.GetProgress(routine), 1, 2, false);
        }

        [TestMethod]
        public void GetProgress_NullEntries_IgnoresNulls()
        {
            var routine = RoutineWithItems(
                null,
                TestSettingsFactory.Item(TestSettingsFactory.ChecklistItemOneId, "One", true, 0));

            AssertProgress(ChecklistService.GetProgress(routine), 1, 1, true);
        }

        [TestMethod]
        public void Normalize_InvalidItemsDuplicateAndEmptyIds_RemovesRepairsAndOrdersStably()
        {
            var duplicate = TestSettingsFactory.ChecklistItemOneId;
            var laterFirst = TestSettingsFactory.Item(duplicate, " Later first ", false, 5);
            var laterSecond = TestSettingsFactory.Item(duplicate, "Later second", false, 5);
            var emptyId = TestSettingsFactory.Item(Guid.Empty, "Early", false, 1);
            var blank = TestSettingsFactory.Item(TestSettingsFactory.BlankItemId, "   ", false, 0);
            var routine = RoutineWithItems(laterFirst, null, blank, laterSecond, emptyId);

            ChecklistService.Normalize(routine);

            Assert.AreEqual(3, routine.Checklist.Count);
            CollectionAssert.AreEqual(
                new[] { "Early", "Later first", "Later second" },
                routine.Checklist.Select(a => a.Text).ToArray());
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, routine.Checklist.Select(a => a.Order).ToArray());
            Assert.AreEqual(3, routine.Checklist.Select(a => a.Id).Distinct().Count());
            Assert.IsFalse(routine.Checklist.Any(a => a.Id == Guid.Empty));
        }

        [TestMethod]
        public void AddItem_ValidText_AddsNormalizedUncheckedItemAtEnd()
        {
            var routine = RoutineWithItems(
                TestSettingsFactory.Item(TestSettingsFactory.ChecklistItemOneId, "One", true, 3));

            var added = ChecklistService.AddItem(routine, "  New item  ");

            Assert.IsNotNull(added);
            Assert.AreEqual("New item", added.Text);
            Assert.IsFalse(added.IsChecked);
            Assert.AreEqual(1, added.Order);
            Assert.AreEqual(2, routine.Checklist.Count);
        }

        [TestMethod]
        public void AddItem_NullRoutineOrBlankText_ReturnsNullWithoutChange()
        {
            var routine = RoutineWithItems();

            Assert.IsNull(ChecklistService.AddItem(null, "Item"));
            Assert.IsNull(ChecklistService.AddItem(routine, "   "));
            Assert.AreEqual(0, routine.Checklist.Count);
        }

        [TestMethod]
        public void EditItem_ExistingItem_UpdatesNormalizedText()
        {
            var routine = RoutineWithItems(
                TestSettingsFactory.Item(TestSettingsFactory.ChecklistItemOneId, "Old", false, 0));

            Assert.IsTrue(ChecklistService.EditItem(routine, TestSettingsFactory.ChecklistItemOneId, "  New  "));
            Assert.AreEqual("New", routine.Checklist[0].Text);
        }

        [TestMethod]
        public void EditItem_MissingItemOrBlankText_ReturnsFalseWithoutChange()
        {
            var routine = RoutineWithItems(
                TestSettingsFactory.Item(TestSettingsFactory.ChecklistItemOneId, "Old", false, 0));

            Assert.IsFalse(ChecklistService.EditItem(routine, TestSettingsFactory.MissingId, "New"));
            Assert.IsFalse(ChecklistService.EditItem(routine, TestSettingsFactory.ChecklistItemOneId, "   "));
            Assert.AreEqual("Old", routine.Checklist[0].Text);
        }

        [TestMethod]
        public void DeleteItem_ExistingItem_RemovesAndReorders()
        {
            var routine = RoutineWithItems(
                TestSettingsFactory.Item(TestSettingsFactory.ChecklistItemOneId, "One", false, 0),
                TestSettingsFactory.Item(TestSettingsFactory.ChecklistItemTwoId, "Two", false, 1));

            Assert.IsTrue(ChecklistService.DeleteItem(routine, TestSettingsFactory.ChecklistItemOneId));
            Assert.AreEqual(1, routine.Checklist.Count);
            Assert.AreEqual(TestSettingsFactory.ChecklistItemTwoId, routine.Checklist[0].Id);
            Assert.AreEqual(0, routine.Checklist[0].Order);
        }

        [TestMethod]
        public void DeleteItem_MissingItem_ReturnsFalse()
        {
            Assert.IsFalse(ChecklistService.DeleteItem(RoutineWithItems(), TestSettingsFactory.MissingId));
        }

        [TestMethod]
        public void MoveItem_ExistingItem_MovesAndReorders()
        {
            var routine = RoutineWithItems(
                TestSettingsFactory.Item(TestSettingsFactory.ChecklistItemOneId, "One", false, 0),
                TestSettingsFactory.Item(TestSettingsFactory.ChecklistItemTwoId, "Two", false, 1));

            Assert.IsTrue(ChecklistService.MoveItem(routine, TestSettingsFactory.ChecklistItemTwoId, -1));
            CollectionAssert.AreEqual(
                new[] { TestSettingsFactory.ChecklistItemTwoId, TestSettingsFactory.ChecklistItemOneId },
                routine.Checklist.Select(a => a.Id).ToArray());
            CollectionAssert.AreEqual(new[] { 0, 1 }, routine.Checklist.Select(a => a.Order).ToArray());
        }

        [TestMethod]
        public void MoveItem_MissingZeroOrBoundaryMove_ReturnsFalse()
        {
            var routine = RoutineWithItems(
                TestSettingsFactory.Item(TestSettingsFactory.ChecklistItemOneId, "One", false, 0));

            Assert.IsFalse(ChecklistService.MoveItem(routine, TestSettingsFactory.MissingId, 1));
            Assert.IsFalse(ChecklistService.MoveItem(routine, TestSettingsFactory.ChecklistItemOneId, 0));
            Assert.IsFalse(ChecklistService.MoveItem(routine, TestSettingsFactory.ChecklistItemOneId, -1));
            Assert.IsFalse(ChecklistService.MoveItem(routine, TestSettingsFactory.ChecklistItemOneId, 1));
        }

        [TestMethod]
        public void Reset_CheckedItems_UnchecksAndReturnsTrue()
        {
            var routine = RoutineWithItems(
                TestSettingsFactory.Item(TestSettingsFactory.ChecklistItemOneId, "One", true, 0),
                null,
                TestSettingsFactory.Item(TestSettingsFactory.ChecklistItemTwoId, "Two", false, 1));

            Assert.IsTrue(ChecklistService.Reset(routine));
            Assert.IsFalse(routine.Checklist.Where(a => a != null).Any(a => a.IsChecked));
        }

        [TestMethod]
        public void Reset_NullOrAlreadyUnchecked_ReturnsFalse()
        {
            var routine = RoutineWithItems(
                TestSettingsFactory.Item(TestSettingsFactory.ChecklistItemOneId, "One", false, 0));

            Assert.IsFalse(ChecklistService.Reset(null));
            Assert.IsFalse(ChecklistService.Reset(routine));
        }

        [TestMethod]
        public void SetItemChecked_ExistingAndMissingItem_ReturnsExpectedResult()
        {
            var routine = RoutineWithItems(
                TestSettingsFactory.Item(TestSettingsFactory.ChecklistItemOneId, "One", false, 0));

            Assert.IsTrue(ChecklistService.SetItemChecked(routine, TestSettingsFactory.ChecklistItemOneId, true));
            Assert.IsTrue(routine.Checklist[0].IsChecked);
            Assert.IsFalse(ChecklistService.SetItemChecked(routine, TestSettingsFactory.MissingId, true));
        }

        private static RoutineSettings RoutineWithItems(params ChecklistItemSettings[] items)
        {
            var routine = TestSettingsFactory.Routine(
                TestSettingsFactory.DailyRoutineId, "Dailies", 0);
            routine.Checklist = new ObservableCollection<ChecklistItemSettings>(
                items ?? new ChecklistItemSettings[0]);
            return routine;
        }

        private static void AssertProgress(
            ChecklistProgress progress,
            int completed,
            int total,
            bool isComplete)
        {
            Assert.AreEqual(completed, progress.Completed);
            Assert.AreEqual(total, progress.Total);
            Assert.AreEqual(isComplete, progress.IsComplete);
        }
    }
}
