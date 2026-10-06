using System.Net;
using System.Text;
using System.Text.Json;
using ThirteenthBell.Core;

namespace ThirteenthBell;

internal static class Program
{
    private static int _checks;

    private static async Task Main()
    {
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        using ResponseHandler handler = new();
        PlayerData player = new() { Nickname = "ExistingPlayer", OnlineClaimToken = "existing-claim" };
        using SupabaseLeaderboardService service = new(player, handler);
        handler.Json = $"[{{\"id\":\"{first}\",\"nickname\":\"Clear\",\"clear_time_ms\":1000}},{{\"id\":\"{second}\",\"nickname\":\"Failed\",\"failed\":true}}]";
        LeaderboardLoadResult entries = await service.LoadAllAsync(CancellationToken.None);
        Check(entries.Succeeded && entries.Entries.Count == 2
            && entries.Entries[0].Id == first && entries.Entries[1].Id == second && entries.Entries[1].Failed, "stable UUIDs for success and failure");
        Check(handler.LastUri!.Query.Contains("select=id,", StringComparison.Ordinal), "all-record query requests IDs");
        handler.Json = "[{\"id\":\"bad\",\"nickname\":\"Legacy\",\"clear_time_ms\":1000}]";
        entries = await service.LoadAllAsync(CancellationToken.None);
        Check(entries.Succeeded && entries.Entries[0].Id is null, "invalid IDs remain non-deletable");
        int calls = handler.Calls;
        foreach (Guid[] ids in new[] { Array.Empty<Guid>(), new[] { Guid.Empty }, Enumerable.Range(0, 1001).Select(_ => Guid.NewGuid()).ToArray() })
        {
            Check(!(await service.DeleteAsync(ids, "fixture-password", CancellationToken.None)).Succeeded, "invalid selection refused");
        }
        Check(!(await service.DeleteAsync([first], string.Empty, CancellationToken.None)).Succeeded, "empty password refused");
        Check(!(await service.DeleteAsync([first], new string('가', 25), CancellationToken.None)).Succeeded, "bcrypt byte limit refused");
        Check(handler.Calls == calls, "invalid requests make no HTTP calls");
        handler.Json = "{\"result\":\"deleted\",\"deletedCount\":2}";
        LeaderboardDeleteResult deleted = await service.DeleteAsync([first, first, second], "fixture-password", CancellationToken.None);
        Check(deleted.Succeeded && deleted.DeletedCount == 2, "bulk deletion success");
        using (JsonDocument request = JsonDocument.Parse(handler.LastBody))
        {
            JsonElement root = request.RootElement;
            Check(root.GetProperty("action").GetString() == "delete"
                && root.GetProperty("password").GetString() == "fixture-password"
                && root.GetProperty("recordIds").GetArrayLength() == 2, "single POST with unique IDs and password");
        }
        Check(player.Nickname == "ExistingPlayer" && player.OnlineClaimToken == "existing-claim", "deletion preserves local run ownership");
        foreach (string json in new[] { "null", "[]", "{}", "{\"result\":\"deleted\",\"deletedCount\":\"1\"}", "{\"result\":\"deleted\",\"deletedCount\":-1}", "{\"result\":\"deleted\",\"deletedCount\":3}", "{\"result\":\"saved\",\"deletedCount\":1}", "{" })
        {
            handler.Json = json;
            Check(!(await service.DeleteAsync([first, second], "fixture-password", CancellationToken.None)).Succeeded, "invalid response rejected");
        }
        handler.Json = "{\"result\":\"deleted\",\"deletedCount\":0}";
        Check((await service.DeleteAsync([first], "fixture-password", CancellationToken.None)).Succeeded, "retry with zero deletions accepted");
        handler.Status = HttpStatusCode.Forbidden;
        Check((await service.DeleteAsync([first], "fixture-password", CancellationToken.None)).Error!.Contains("일치하지", StringComparison.Ordinal), "wrong password explained");
        handler.Status = HttpStatusCode.TooManyRequests;
        Check((await service.DeleteAsync([first], "fixture-password", CancellationToken.None)).Error!.Contains("1분", StringComparison.Ordinal), "throttle explained");
        handler.Status = HttpStatusCode.BadGateway;
        Check(!(await service.DeleteAsync([first], "fixture-password", CancellationToken.None)).Succeeded, "HTTP failure rejected");
        handler.Status = HttpStatusCode.OK;
        handler.ThrowTimeout = true;
        Check((await service.DeleteAsync([first], "fixture-password", CancellationToken.None)).Error!.Contains("삭제 여부", StringComparison.Ordinal), "timeout requires result reconciliation");
        handler.ThrowTimeout = false;
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        bool cancelled = false;
        try
        {
            await service.DeleteAsync([first], "fixture-password", cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }
        Check(cancelled, "caller cancellation propagates");
        Console.WriteLine($"LEADERBOARD_DELETE_CLIENT_OK checks={_checks}, productionRequests=0");
    }

    private static void Check(bool condition, string name)
    {
        if (!condition)
        {
            throw new InvalidOperationException(name);
        }
        _checks++;
    }

    private sealed class ResponseHandler : HttpMessageHandler
    {
        internal string Json { get; set; } = "{}";
        internal HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        internal bool ThrowTimeout { get; set; }
        internal int Calls { get; private set; }
        internal Uri? LastUri { get; private set; }
        internal string LastBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            LastUri = request.RequestUri;
            LastBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (ThrowTimeout)
            {
                throw new TaskCanceledException("simulated timeout");
            }
            return new HttpResponseMessage(Status) { Content = new StringContent(Json, Encoding.UTF8, "application/json") };
        }
    }
}
