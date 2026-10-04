using System.Net;
using System.Text;
using NAudio.Wave;
using ThirteenthBell.Core;

namespace ThirteenthBell;

internal static partial class PostalRoomSmoke
{
    private static async Task CheckRegressionsAsync()
    {
        PlayerData data = new() { OnlineClaimToken = "test-token" };
        using JsonResponseHandler handler = new();
        using SupabaseLeaderboardService service = new(data, handler);
        foreach (string json in new[] { "null", "[]", "true", "{\"entries\":null}" })
        {
            handler.Json = json;
            Check(!(await service.LoadAsync(CancellationToken.None)).Succeeded, "invalid leaderboard root " + json);
        }
        handler.Json = "{\"entries\":[null,[],{\"nickname\":1,\"clear_time_ms\":5},{\"nickname\":\"bad\",\"clear_time_ms\":\"5\"},{\"nickname\":\"good\",\"clear_time_ms\":1000}]}";
        LeaderboardLoadResult entries = await service.LoadAsync(CancellationToken.None);
        Check(entries.Succeeded && entries.Entries.Count == 1 && entries.Entries[0].Nickname == "good", "malformed entries skipped without losing valid score");
        foreach (string json in new[] { "null", "[]", "{\"result\":\"reserved\",\"nickname\":1}" })
        {
            handler.Json = json;
            Check((await service.ReserveNicknameAsync("Tester", CancellationToken.None)).Status == NicknameReservationStatus.Unavailable, "invalid nickname response " + json);
        }
        handler.Json = "{\"result\":\"reserved\",\"nickname\":\"Tester\"}";
        Check((await service.ReserveNicknameAsync("Tester", CancellationToken.None)).Status == NicknameReservationStatus.Reserved, "valid nickname response accepted");
        foreach (string json in new[] { "null", "[]", "{\"clearTimeMs\":1000,\"rank\":1}", "{\"result\":\"saved\",\"clearTimeMs\":\"1000\",\"rank\":1}", "{\"result\":\"saved\",\"clearTimeMs\":0,\"rank\":1}", "{\"result\":\"saved\",\"clearTimeMs\":1000,\"rank\":0}" })
        {
            handler.Json = json;
            Check(!(await service.SubmitClearAsync(EndingChoice.DeliverTheGift, CancellationToken.None)).Succeeded, "invalid score response " + json);
        }
        handler.Json = "{\"result\":\"saved\",\"clearTimeMs\":1000,\"rank\":1}";
        Check((await service.SubmitClearAsync(EndingChoice.DeliverTheGift, CancellationToken.None)).Succeeded, "valid score accepted");
    }

    private static void CheckAudioLoopRegression()
    {
        GameState state = GameState.Restore(Enum.GetValues<PuzzleId>(), 0, 0, true);
        Check(!state.ChooseEnding((EndingChoice)999), "undefined ending rejected");
        bool invalidPuzzleRejected = false;
        try
        {
            state.Solve((PuzzleId)999);
        }
        catch (ArgumentOutOfRangeException)
        {
            invalidPuzzleRejected = true;
        }
        Check(invalidPuzzleRejected && state.SolvedPuzzles.Count == GameState.RequiredPuzzleCount, "undefined puzzle cannot corrupt progress");
        Type loopType = typeof(BackgroundMusicPlayer).GetNestedType("LoopingWaveStream", System.Reflection.BindingFlags.NonPublic)!;
        using MemoryStream empty = new();
        using RawSourceWaveStream emptySource = new(empty, new WaveFormat(8000, 16, 1));
        using WaveStream emptyLoop = (WaveStream)Activator.CreateInstance(loopType, emptySource)!;
        byte[] buffer = new byte[12];
        Check(emptyLoop.Read(buffer, 0, buffer.Length) == 0, "empty audio loop returns without hanging");
        using MemoryStream samples = new([1, 2, 3, 4]);
        using RawSourceWaveStream source = new(samples, new WaveFormat(8000, 16, 1));
        using WaveStream loop = (WaveStream)Activator.CreateInstance(loopType, source)!;
        Check(loop.Read(buffer, 0, buffer.Length) == 12 && buffer.SequenceEqual(new byte[] { 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4 }), "audio repeats across source boundaries");
        using BackgroundMusicPlayer player = new(playbackAvailable: false);
        player.Dispose();
        player.Play(BackgroundMusicKind.Game);
        player.Enabled = false;
        Check(player.CurrentKind == BackgroundMusicKind.None, "disposed music ignores playback requests");
    }

    private sealed class JsonResponseHandler : HttpMessageHandler
    {
        internal string Json { get; set; } = "null";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Json, Encoding.UTF8, "application/json")
            });
        }
    }
}
