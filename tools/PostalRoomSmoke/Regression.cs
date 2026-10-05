using System.Net;
using System.Text;
using System.Text.Json;
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
        Check(data.OnlineClaimToken != "test-token" && data.OnlineClaimToken.Length >= 40, "new reservation has independent run identity");
        string firstToken = data.OnlineClaimToken;
        Check((await service.ReserveNicknameAsync("Tester", CancellationToken.None)).Status == NicknameReservationStatus.Reserved
            && data.OnlineClaimToken != firstToken, "next reservation preserves previous run identity");
        foreach (string json in new[] { "null", "[]", "{\"clearTimeMs\":1000,\"rank\":1}", "{\"result\":\"saved\",\"clearTimeMs\":\"1000\",\"rank\":1}", "{\"result\":\"saved\",\"clearTimeMs\":0,\"rank\":1}", "{\"result\":\"saved\",\"clearTimeMs\":1000,\"rank\":0}" })
        {
            handler.Json = json;
            Check(!(await service.SubmitClearAsync(EndingChoice.DeliverTheGift, CancellationToken.None)).Succeeded, "invalid score response " + json);
        }
        handler.Json = "{\"result\":\"saved\",\"clearTimeMs\":1000,\"rank\":1}";
        Check((await service.SubmitClearAsync(EndingChoice.DeliverTheGift, CancellationToken.None)).Succeeded, "valid score accepted");
        Check((await service.SubmitClearAsync(EndingChoice.DeliverTheGift, 123456L, 3, 2, CancellationToken.None)).Succeeded, "active elapsed and run statistics submission accepted");
        using (JsonDocument submission = JsonDocument.Parse(handler.LastBody))
        {
            JsonElement body = submission.RootElement;
            Check(body.GetProperty("elapsedMilliseconds").GetInt64() == 123456L
                && body.GetProperty("failedAttempts").GetInt32() == 3
                && body.GetProperty("hintCount").GetInt32() == 2, "clear submission transmits elapsed, failures and used hints");
        }
        handler.Json = "{\"entries\":[{\"nickname\":\"Clear\",\"clear_time_ms\":1000,\"failed\":false},{\"nickname\":\"Failed\",\"clear_time_ms\":null,\"failed\":true}],\"totalCount\":17}";
        LeaderboardLoadResult outcomes = await service.LoadAsync(CancellationToken.None);
        Check(outcomes.Succeeded && outcomes.Entries.Count == 2 && outcomes.Entries[1].Failed && outcomes.TotalCount == 17, "failed ranking row and server total parsed");
        foreach (string json in new[] { "null", "[]", "{}", "{\"result\":\"saved\",\"rank\":0}", "{\"result\":\"saved\",\"rank\":\"1\"}" })
        {
            handler.Json = json;
            Check(!(await service.SubmitFailureAsync(firstToken, CancellationToken.None)).Succeeded, "invalid failure response " + json);
        }
        handler.Json = "{\"result\":\"saved\",\"rank\":17,\"failed\":true}";
        ScoreSubmissionResult failed = await service.SubmitFailureAsync(firstToken, CancellationToken.None);
        Check(failed.Succeeded && failed.Rank == 17 && failed.ClearTimeMilliseconds == 0, "valid failed outcome has rank without clear time");
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
        internal string LastBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Json, Encoding.UTF8, "application/json")
            };
        }
    }
}
