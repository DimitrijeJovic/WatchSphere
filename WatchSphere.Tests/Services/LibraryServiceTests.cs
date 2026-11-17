using WatchSphere.API.DTOs;
using WatchSphere.API.Services;
using WatchSphere.Tests.Infrastructure;

namespace WatchSphere.Tests.Services;

public class LibraryServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private LibraryService NewService() => new(_db.CreateContext());

    [Fact]
    public async Task Adding_the_same_title_twice_is_refused()
    {
        var user = _db.AddUser("dup");
        var media = _db.AddMedia("Twice");
        await NewService().AddAsync(user.Id, new AddToLibraryDto { MediaId = media.Id });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewService().AddAsync(user.Id, new AddToLibraryDto { MediaId = media.Id }));
    }

    [Fact]
    public async Task Adding_an_unknown_title_is_refused()
    {
        var user = _db.AddUser("ghost");

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => NewService().AddAsync(user.Id, new AddToLibraryDto { MediaId = 9999 }));
    }

    [Fact]
    public async Task First_watched_episode_moves_a_planned_title_to_watching()
    {
        var user = _db.AddUser("starter");
        var entry = await NewService().AddAsync(user.Id, new AddToLibraryDto { MediaId = _db.AddMedia("Fresh", totalEpisodes: 12).Id });

        var updated = await NewService().IncrementEpisodeAsync(user.Id, entry.Id);

        Assert.NotNull(updated);
        Assert.Equal("Watching", updated.Status);
        Assert.Equal(1, updated.EpisodesWatched);
        Assert.NotNull(updated.StartedAt);
    }

    [Fact]
    public async Task Watching_the_last_episode_completes_the_title()
    {
        var user = _db.AddUser("finisher");
        var entry = await NewService().AddAsync(user.Id, new AddToLibraryDto { MediaId = _db.AddMedia("Short", totalEpisodes: 2).Id });
        await NewService().IncrementEpisodeAsync(user.Id, entry.Id);

        var updated = await NewService().IncrementEpisodeAsync(user.Id, entry.Id);

        Assert.Equal("Completed", updated!.Status);
        Assert.Equal(2, updated.EpisodesWatched);
        Assert.NotNull(updated.FinishedAt);
    }

    [Fact]
    public async Task Progress_is_clamped_to_the_number_of_episodes()
    {
        var user = _db.AddUser("overshoot");
        var entry = await NewService().AddAsync(user.Id, new AddToLibraryDto { MediaId = _db.AddMedia("Ten", totalEpisodes: 10).Id });

        var updated = await NewService().UpdateAsync(user.Id, entry.Id, new UpdateLibraryDto { EpisodesWatched = 50 });

        Assert.Equal(10, updated!.EpisodesWatched);
        Assert.Equal("Completed", updated.Status);
    }

    [Fact]
    public async Task Setting_status_back_to_planned_clears_progress_and_dates()
    {
        var user = _db.AddUser("resetter");
        var entry = await NewService().AddAsync(user.Id,
            new AddToLibraryDto { MediaId = _db.AddMedia("Restart", totalEpisodes: 6).Id, Status = "Completed" });

        var updated = await NewService().UpdateAsync(user.Id, entry.Id, new UpdateLibraryDto { Status = "Planned" });

        Assert.Equal("Planned", updated!.Status);
        Assert.Equal(0, updated.EpisodesWatched);
        Assert.Null(updated.StartedAt);
        Assert.Null(updated.FinishedAt);
    }

    [Fact]
    public async Task A_movie_counts_as_one_episode_when_completed()
    {
        var user = _db.AddUser("moviegoer");
        var movie = _db.AddMedia("Feature", type: "Movie", totalEpisodes: null);

        var entry = await NewService().AddAsync(user.Id, new AddToLibraryDto { MediaId = movie.Id, Status = "Completed" });

        Assert.Equal(1, entry.EpisodesWatched);
        Assert.NotNull(entry.FinishedAt);
    }

    [Fact]
    public async Task Another_users_entry_cannot_be_changed_or_removed()
    {
        var owner = _db.AddUser("owner");
        var intruder = _db.AddUser("intruder");
        var entry = await NewService().AddAsync(owner.Id, new AddToLibraryDto { MediaId = _db.AddMedia("Private").Id });

        Assert.Null(await NewService().UpdateAsync(intruder.Id, entry.Id, new UpdateLibraryDto { Score = 1 }));
        Assert.Null(await NewService().IncrementEpisodeAsync(intruder.Id, entry.Id));
        Assert.False(await NewService().RemoveAsync(intruder.Id, entry.Id));

        using var context = _db.CreateContext();
        var stored = context.UserLibraries.Single(e => e.Id == entry.Id);
        Assert.Null(stored.Score);
        Assert.Equal(0, stored.EpisodesWatched);
    }
}
