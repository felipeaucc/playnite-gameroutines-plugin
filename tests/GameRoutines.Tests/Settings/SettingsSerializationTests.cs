using Microsoft.VisualStudio.TestTools.UnitTesting;
using Playnite.SDK.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;

namespace GameRoutines.Tests
{
    [TestClass]
    public class SettingsSerializationTests
    {
        private static FieldInfo serializerField;
        private static object previousSerializer;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            serializerField = typeof(Serialization).GetField(
                "serializer",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(serializerField, "Playnite SDK serialization field was not found.");
            previousSerializer = serializerField.GetValue(null);
            serializerField.SetValue(null, new DeterministicJsonSerializer());
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            serializerField?.SetValue(null, previousSerializer);
        }

        [TestMethod]
        public void Deserialize_LegacyV1Fixture_MigratesRepresentativeGraph()
        {
            var legacy = Serialization.FromJson<LegacySettingsV1>(ReadFixture("LegacySettingsV1.json"));

            var migrated = SettingsMigrationService.Migrate(legacy);

            Assert.AreEqual(2, migrated.SchemaVersion);
            Assert.IsTrue(migrated.UseTasksAvailableTag);
            Assert.AreEqual(1, migrated.TrackedGames.Count);
            Assert.AreEqual(TestSettingsFactory.GameId, migrated.TrackedGames[0].GameId);
            Assert.AreEqual("Weeklies", migrated.TrackedGames[0].Routines[0].Name);
            Assert.AreEqual(TaskState.INCOMPLETE, migrated.TrackedGames[0].Routines[0].CurrentState);
            Assert.AreEqual(TestSettingsFactory.ChecklistItemOneId, migrated.TrackedGames[0].Routines[0].Checklist[0].Id);
            Assert.IsTrue(migrated.TrackedGames[0].CustomReminderEnabled);
        }

        [TestMethod]
        public void Deserialize_LegacyV0Fixture_MigratesRepresentativeGraph()
        {
            var legacy = Serialization.FromJson<LegacySettingsV0>(ReadFixture("LegacySettingsV0.json"));

            var migrated = SettingsMigrationService.Migrate(legacy);

            Assert.AreEqual(2, migrated.SchemaVersion);
            Assert.IsTrue(migrated.UseTasksAvailableTag);
            Assert.AreEqual(1, migrated.TrackedGames.Count);
            Assert.AreEqual(TestSettingsFactory.GameId, migrated.TrackedGames[0].GameId);
            Assert.AreEqual("Weeklies", migrated.TrackedGames[0].Routines[0].Name);
            Assert.AreEqual(TaskState.COMPLETE, migrated.TrackedGames[0].Routines[0].CurrentState);
            Assert.AreEqual(TestSettingsFactory.ChecklistItemTwoId, migrated.TrackedGames[0].Routines[0].Checklist[0].Id);
            Assert.AreEqual(ReminderCadence.Weekly, migrated.TrackedGames[0].ReminderCadence);
        }

        [TestMethod]
        public void Serialization_CurrentV2Graph_RoundTripsPersistedValuesAndOmitsComputedProperties()
        {
            var original = TestSettingsFactory.CurrentSettings();

            var json = Serialization.ToJson(original, true);
            var restored = Serialization.FromJson<GameRoutinesSettings>(json);

            Assert.IsFalse(json.Contains("DisplayState"));
            Assert.IsFalse(json.Contains("IsComplete"));
            Assert.IsFalse(json.Contains("ParticipatingRoutineCount"));
            Assert.IsFalse(json.Contains("BiWeeklyResetStartingDate"));
            Assert.IsFalse(json.Contains("BiWeeklyReminderStartingDate"));
            Assert.AreEqual(2, restored.SchemaVersion);
            Assert.AreEqual(1, restored.TrackedGames.Count);
            var game = restored.TrackedGames[0];
            Assert.AreEqual(TestSettingsFactory.GameId, game.GameId);
            Assert.AreEqual(ReminderCadence.BiWeekly, game.ReminderCadence);
            Assert.AreEqual("18:45", game.ReminderTime);
            AssertSameLocalWallTime(TestSettingsFactory.ReminderTimestamp, game.LastReminderProcessedLocal);
            AssertSameLocalWallTime(
                DateTime.SpecifyKind(new DateTime(2025, 12, 19, 18, 45, 0), DateTimeKind.Local),
                game.BiWeeklyReminderAnchorLocal);
            var routine = game.Routines[0];
            Assert.AreEqual(TestSettingsFactory.DailyRoutineId, routine.Id);
            Assert.AreEqual(ResetCadence.BiWeekly, routine.ResetCadence);
            AssertSameLocalWallTime(TestSettingsFactory.ResetTimestamp, routine.LastResetProcessedLocal);
            AssertSameLocalWallTime(
                DateTime.SpecifyKind(new DateTime(2025, 12, 17, 6, 30, 0), DateTimeKind.Local),
                routine.BiWeeklyResetAnchorLocal);
            Assert.AreEqual(2, routine.Checklist.Count);
            Assert.AreEqual(TestSettingsFactory.ChecklistItemOneId, routine.Checklist[0].Id);
            Assert.AreEqual(TaskState.INCOMPLETE, game.CurrentState);
            Assert.AreEqual("INCOMPLETE", game.DisplayState);
            Assert.AreEqual(1, game.ParticipatingRoutineCount);
        }

        private static string ReadFixture(string fileName)
        {
            var assemblyDirectory = Path.GetDirectoryName(typeof(SettingsSerializationTests).Assembly.Location);
            var path = Path.Combine(assemblyDirectory, "TestData", fileName);
            return File.ReadAllText(path);
        }

        private static void AssertSameLocalWallTime(DateTime expected, DateTime? actual)
        {
            Assert.IsTrue(actual.HasValue);
            Assert.AreEqual(expected.Ticks, actual.Value.Ticks);
        }

        private sealed class DeterministicJsonSerializer : IDataSerializer
        {
            private readonly JavaScriptSerializer jsonSerializer = new JavaScriptSerializer
            {
                MaxJsonLength = int.MaxValue,
                RecursionLimit = 100
            };

            public string ToJson(object obj, bool formatted)
            {
                return jsonSerializer.Serialize(CreateSerializableGraph(obj));
            }

            public void ToJsonStream(object obj, Stream stream, bool formatted)
            {
                var bytes = Encoding.UTF8.GetBytes(ToJson(obj, formatted));
                stream.Write(bytes, 0, bytes.Length);
            }

            public T FromJson<T>(string json) where T : class
            {
                return jsonSerializer.Deserialize<T>(json);
            }

            public bool TryFromJson<T>(string json, out T content) where T : class
            {
                return TryFromJson(json, out content, out _);
            }

            public bool TryFromJson<T>(string json, out T content, out Exception error) where T : class
            {
                try
                {
                    content = FromJson<T>(json);
                    error = null;
                    return true;
                }
                catch (Exception exception)
                {
                    content = default(T);
                    error = exception;
                    return false;
                }
            }

            public T FromJsonStream<T>(Stream stream) where T : class
            {
                using (var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, true))
                {
                    return FromJson<T>(reader.ReadToEnd());
                }
            }

            public bool TryFromJsonStream<T>(Stream stream, out T content) where T : class
            {
                return TryFromJsonStream(stream, out content, out _);
            }

            public bool TryFromJsonStream<T>(Stream stream, out T content, out Exception error) where T : class
            {
                try
                {
                    content = FromJsonStream<T>(stream);
                    error = null;
                    return true;
                }
                catch (Exception exception)
                {
                    content = default(T);
                    error = exception;
                    return false;
                }
            }

            public T FromJsonFile<T>(string filePath) where T : class
            {
                using (var stream = File.OpenRead(filePath))
                {
                    return FromJsonStream<T>(stream);
                }
            }

            public bool TryFromJsonFile<T>(string filePath, out T content) where T : class
            {
                return TryFromJsonFile(filePath, out content, out _);
            }

            public bool TryFromJsonFile<T>(string filePath, out T content, out Exception error) where T : class
            {
                try
                {
                    content = FromJsonFile<T>(filePath);
                    error = null;
                    return true;
                }
                catch (Exception exception)
                {
                    content = default(T);
                    error = exception;
                    return false;
                }
            }

            public T GetClone<T>(T source) where T : class
            {
                return FromJson<T>(ToJson(source, false));
            }

            public U GetClone<T, U>(T source)
                where T : class
                where U : class
            {
                return FromJson<U>(ToJson(source, false));
            }

            public bool AreObjectsEqual(object object1, object object2)
            {
                return string.Equals(ToJson(object1, false), ToJson(object2, false), StringComparison.Ordinal);
            }

            public string ToYaml(object obj)
            {
                throw new NotSupportedException();
            }

            public T FromYaml<T>(string yaml) where T : class
            {
                throw new NotSupportedException();
            }

            public bool TryFromYaml<T>(string yaml, out T content) where T : class
            {
                content = default(T);
                return false;
            }

            public bool TryFromYaml<T>(string yaml, out T content, out Exception error) where T : class
            {
                content = default(T);
                error = new NotSupportedException();
                return false;
            }

            public T FromYamlFile<T>(string filePath) where T : class
            {
                throw new NotSupportedException();
            }

            public bool TryFromYamlFile<T>(string filePath, out T content) where T : class
            {
                content = default(T);
                return false;
            }

            public bool TryFromYamlFile<T>(string filePath, out T content, out Exception error) where T : class
            {
                content = default(T);
                error = new NotSupportedException();
                return false;
            }

            public T FromToml<T>(string toml) where T : class
            {
                throw new NotSupportedException();
            }

            public bool TryFromToml<T>(string toml, out T content) where T : class
            {
                content = default(T);
                return false;
            }

            public bool TryFromToml<T>(string toml, out T content, out Exception error) where T : class
            {
                content = default(T);
                error = new NotSupportedException();
                return false;
            }

            public T FromTomlFile<T>(string filePath) where T : class
            {
                throw new NotSupportedException();
            }

            public bool TryFromTomlFile<T>(string filePath, out T content) where T : class
            {
                content = default(T);
                return false;
            }

            public bool TryFromTomlFile<T>(string filePath, out T content, out Exception error) where T : class
            {
                content = default(T);
                error = new NotSupportedException();
                return false;
            }

            private static object CreateSerializableGraph(object value)
            {
                if (value == null)
                {
                    return null;
                }

                var type = value.GetType();
                if (value is DateTime dateTime)
                {
                    return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
                }

                if (type.IsPrimitive || type.IsEnum || value is string || value is decimal ||
                    value is Guid || value is TimeSpan)
                {
                    return value;
                }

                if (value is IEnumerable enumerable)
                {
                    return enumerable.Cast<object>().Select(CreateSerializableGraph).ToList();
                }

                var graph = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (!property.CanRead || property.GetIndexParameters().Length != 0 ||
                        property.IsDefined(typeof(DontSerializeAttribute), true))
                    {
                        continue;
                    }

                    graph[property.Name] = CreateSerializableGraph(property.GetValue(value, null));
                }

                return graph;
            }
        }
    }
}
