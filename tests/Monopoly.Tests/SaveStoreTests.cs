using Monopoly.Core;
using Monopoly.Net;

namespace Monopoly.Tests
{
    // Сохранения хоста: список незаконченных партий и удаление.
    public class SaveStoreTests : IDisposable
    {
        private readonly SaveStore store = new(Path.Combine(Path.GetTempPath(), "monopoly-tests", Guid.NewGuid().ToString("N")));

        private static SaveFile Save(bool finished = false, string version = "") => new(
            Guid.NewGuid(),
            version.Length > 0 ? version : NetDefaults.GameVersion,
            42,
            new[] { new SavedSeat("Хост", 0, true, false), new SavedSeat("Бот 1", 1, false, true) },
            new GameAction[] { new RollDice(0) },
            DateTime.UtcNow,
            finished,
            PlayedSeconds: 90);

        [Fact]
        public void Write_ThenList_ReturnsSameSave()
        {
            var save = Save();
            store.Write(save);

            var listed = Assert.Single(store.ListUnfinished());

            Assert.Equal(save.GameId, listed.GameId);
            Assert.Equal("Хост", listed.HostName);
            Assert.Equal(90, listed.PlayedSeconds);
            Assert.Equal(new RollDice(0), Assert.Single(listed.Actions));
        }

        [Fact]
        public void List_SkipsFinishedAndOtherVersions()
        {
            store.Write(Save(finished: true));
            store.Write(Save(version: "0.0.1"));

            Assert.Empty(store.ListUnfinished());
        }

        [Fact]
        public void Delete_RemovesSave()
        {
            var keep = Save();
            var remove = Save();
            store.Write(keep);
            store.Write(remove);

            store.Delete(remove.GameId);

            Assert.Equal(keep.GameId, Assert.Single(store.ListUnfinished()).GameId);
        }

        public void Dispose()
        {
            if (Directory.Exists(store.Directory))
                Directory.Delete(store.Directory, recursive: true);
        }
    }
}
