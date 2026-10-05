using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ThirteenthBell.Core;

namespace ThirteenthBell;

internal sealed record LeaderboardEntry(string Nickname, long ClearTimeMilliseconds, bool Failed = false);

internal sealed record LeaderboardLoadResult(IReadOnlyList<LeaderboardEntry> Entries, string? Error, long TotalCount = 0)
{
    public bool Succeeded => Error is null;
}

internal enum NicknameReservationStatus
{
    Reserved,
    Taken,
    Invalid,
    Unavailable
}

internal sealed record NicknameReservationResult(NicknameReservationStatus Status, string? Nickname, string? Error);

internal sealed record ScoreSubmissionResult(bool Succeeded, long ClearTimeMilliseconds, long Rank, string? Error);

internal interface ILeaderboardService : IDisposable
{
    Task<LeaderboardLoadResult> LoadAsync(CancellationToken cancellationToken);

    Task<LeaderboardLoadResult> LoadAllAsync(CancellationToken cancellationToken)
    {
        return LoadAsync(cancellationToken);
    }

    Task<NicknameReservationResult> ReserveNicknameAsync(string nickname, CancellationToken cancellationToken);

    Task<ScoreSubmissionResult> SubmitClearAsync(EndingChoice ending, CancellationToken cancellationToken);
    Task<ScoreSubmissionResult> SubmitClearAsync(EndingChoice ending, long? elapsedMilliseconds,
        int failedAttempts, int hintCount, CancellationToken cancellationToken)
    {
        return SubmitClearAsync(ending, cancellationToken);
    }

    Task<ScoreSubmissionResult> SubmitFailureAsync(string claimToken, CancellationToken cancellationToken)
    {
        return Task.FromResult(new ScoreSubmissionResult(false, 0, 0, "실패 기록 서버를 사용할 수 없습니다."));
    }
}

internal sealed class SupabaseLeaderboardService : ILeaderboardService
{
    private static readonly JsonSerializerOptions ConfigJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class OnlineConfig
    {
        public string FunctionUrl { get; set; } = string.Empty;

        public string PublishableKey { get; set; } = string.Empty;
    }

    private readonly HttpClient _httpClient;
    private readonly PlayerData _playerData;
    private readonly Uri? _functionUri;
    private readonly string _publishableKey = string.Empty;
    private readonly string? _configurationError;

    public SupabaseLeaderboardService(PlayerData playerData, HttpMessageHandler? handler = null)
    {
        _playerData = playerData ?? throw new ArgumentNullException(nameof(playerData));
        _httpClient = handler is null ? new HttpClient() : new HttpClient(handler);
        _httpClient.Timeout = TimeSpan.FromSeconds(12);
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Assets", "online-config.json");
            string json = File.ReadAllText(path, Encoding.UTF8);
            OnlineConfig? config = JsonSerializer.Deserialize<OnlineConfig>(json, ConfigJsonOptions);

            if (config is null
                || !Uri.TryCreate(config.FunctionUrl, UriKind.Absolute, out Uri? uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || !config.PublishableKey.StartsWith("sb_publishable_", StringComparison.Ordinal))
            {
                _configurationError = "온라인 순위 설정이 올바르지 않습니다.";
                return;
            }

            _functionUri = uri;
            _publishableKey = config.PublishableKey;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            _configurationError = $"온라인 순위 설정을 읽지 못했습니다: {exception.Message}";
        }
    }

    public async Task<LeaderboardLoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        if (_functionUri is null)
        {
            return new LeaderboardLoadResult([], _configurationError ?? "온라인 순위를 사용할 수 없습니다.");
        }

        try
        {
            using HttpRequestMessage request = CreateRequest(HttpMethod.Get);
            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new LeaderboardLoadResult([], $"온라인 순위를 불러오지 못했습니다. HTTP {(int)response.StatusCode}");
            }

            using JsonDocument document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("entries", out JsonElement entriesElement)
                || entriesElement.ValueKind != JsonValueKind.Array)
            {
                return new LeaderboardLoadResult([], "온라인 순위 응답 형식이 올바르지 않습니다.");
            }

            long totalCount = document.RootElement.TryGetProperty("totalCount", out JsonElement countElement)
                && countElement.ValueKind == JsonValueKind.Number && countElement.TryGetInt64(out long count) && count >= 0
                ? count : entriesElement.GetArrayLength();
            return new LeaderboardLoadResult(ParseEntries(entriesElement), null, totalCount);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new LeaderboardLoadResult([], "온라인 순위 서버의 응답 시간이 초과되었습니다.");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or IOException)
        {
            return new LeaderboardLoadResult([], $"온라인 순위를 불러오지 못했습니다: {exception.Message}");
        }
    }

    public async Task<LeaderboardLoadResult> LoadAllAsync(CancellationToken cancellationToken)
    {
        if (_functionUri is null)
        {
            return new LeaderboardLoadResult([], _configurationError ?? "온라인 순위를 사용할 수 없습니다.");
        }
        const int pageSize = 250;
        List<LeaderboardEntry> entries = [];
        try
        {
            int offset = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Uri uri = new(_functionUri.GetLeftPart(UriPartial.Authority)
                    + "/rest/v1/thirteenth_bell_leaderboard?select=nickname,clear_time_ms,failed&completed_at=not.is.null"
                    + $"&order=failed.asc,clear_time_ms.asc.nullslast,ranking_penalty.asc,completed_at.asc,nickname.asc&offset={offset}&limit={pageSize}");
                using HttpRequestMessage request = new(HttpMethod.Get, uri);
                request.Headers.Add("apikey", _publishableKey);
                using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return new LeaderboardLoadResult([], $"전체 순위를 불러오지 못했습니다. HTTP {(int)response.StatusCode}");
                }
                using JsonDocument document = await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return new LeaderboardLoadResult([], "전체 순위 응답 형식이 올바르지 않습니다.");
                }
                entries.AddRange(ParseEntries(document.RootElement));
                int count = document.RootElement.GetArrayLength();
                if (count < pageSize)
                {
                    return new LeaderboardLoadResult(entries, null);
                }
                offset = checked(offset + count);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new LeaderboardLoadResult([], "전체 순위 서버의 응답 시간이 초과되었습니다.");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or IOException)
        {
            return new LeaderboardLoadResult([], $"전체 순위를 불러오지 못했습니다: {exception.Message}");
        }
    }

    private static List<LeaderboardEntry> ParseEntries(JsonElement entriesElement)
    {
        List<LeaderboardEntry> entries = [];
        foreach (JsonElement entry in entriesElement.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.Object
                && entry.TryGetProperty("failed", out JsonElement failedElement)
                && failedElement.ValueKind == JsonValueKind.True
                && entry.TryGetProperty("nickname", out JsonElement failedNickname)
                && failedNickname.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(failedNickname.GetString()))
            {
                entries.Add(new LeaderboardEntry(failedNickname.GetString()!, 0, true));
                continue;
            }
            if (entry.ValueKind == JsonValueKind.Object
                && entry.TryGetProperty("nickname", out JsonElement nicknameElement)
                && entry.TryGetProperty("clear_time_ms", out JsonElement timeElement)
                && nicknameElement.ValueKind == JsonValueKind.String
                && timeElement.ValueKind == JsonValueKind.Number
                && nicknameElement.GetString() is string nickname
                && timeElement.TryGetInt64(out long milliseconds)
                && milliseconds > 0)
            {
                entries.Add(new LeaderboardEntry(nickname, milliseconds));
            }
        }

        return entries;
    }

    public async Task<NicknameReservationResult> ReserveNicknameAsync(string nickname, CancellationToken cancellationToken)
    {
        if (!NicknameRules.TryNormalize(nickname, out string normalized, out string? validationError))
        {
            return new NicknameReservationResult(NicknameReservationStatus.Invalid, null, validationError);
        }

        if (_functionUri is null)
        {
            return new NicknameReservationResult(
                NicknameReservationStatus.Unavailable,
                null,
                _configurationError ?? "온라인 닉네임 서버를 사용할 수 없습니다.");
        }

        string candidateToken = EnsureClaimToken();
        string tokenHash = ComputeClaimHash(candidateToken);
        try
        {
            using HttpRequestMessage request = CreateJsonRequest(new
            {
                action = "reserve",
                nickname = normalized,
                claimTokenHash = tokenHash
            });
            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                return new NicknameReservationResult(
                    NicknameReservationStatus.Taken,
                    null,
                    "이미 사용 중인 닉네임입니다. 다른 닉네임을 입력하세요.");
            }

            string responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new NicknameReservationResult(
                    NicknameReservationStatus.Unavailable,
                    null,
                    $"닉네임을 확인하지 못했습니다. HTTP {(int)response.StatusCode}");
            }

            using JsonDocument document = JsonDocument.Parse(responseText);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("result", out JsonElement resultElement)
                || resultElement.ValueKind != JsonValueKind.String
                || resultElement.GetString() != "reserved")
            {
                return new NicknameReservationResult(NicknameReservationStatus.Unavailable, null, "닉네임 서버의 등록 승인을 확인하지 못했습니다.");
            }
            string? reservedNickname = document.RootElement.TryGetProperty("nickname", out JsonElement nicknameElement)
                && nicknameElement.ValueKind == JsonValueKind.String
                ? nicknameElement.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(reservedNickname)
                || !string.Equals(reservedNickname, normalized, StringComparison.Ordinal))
            {
                return new NicknameReservationResult(
                    NicknameReservationStatus.Unavailable,
                    null,
                    "닉네임 서버 응답 형식이 올바르지 않습니다.");
            }

            _playerData.Nickname = reservedNickname;
            _playerData.OnlineClaimToken = candidateToken;
            return new NicknameReservationResult(NicknameReservationStatus.Reserved, reservedNickname, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new NicknameReservationResult(NicknameReservationStatus.Unavailable, null, "닉네임 확인 시간이 초과되었습니다.");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or IOException)
        {
            return new NicknameReservationResult(
                NicknameReservationStatus.Unavailable,
                null,
                $"닉네임 서버에 연결하지 못했습니다: {exception.Message}");
        }
    }

    public Task<ScoreSubmissionResult> SubmitClearAsync(EndingChoice ending, CancellationToken cancellationToken)
    {
        return SubmitClearAsync(ending, null, 0, 0, cancellationToken);
    }

    public async Task<ScoreSubmissionResult> SubmitClearAsync(EndingChoice ending, long? elapsedMilliseconds,
        int failedAttempts, int hintCount, CancellationToken cancellationToken)
    {
        if (_functionUri is null || string.IsNullOrWhiteSpace(_playerData.OnlineClaimToken))
        {
            return new ScoreSubmissionResult(false, 0, 0, "온라인 닉네임 예약 정보가 없습니다.");
        }

        string endingValue = ending switch
        {
            EndingChoice.DeliverTheGift => "deliver_gift",
            EndingChoice.FeedTheClock => "feed_clock",
            _ => throw new ArgumentOutOfRangeException(nameof(ending))
        };

        try
        {
            using HttpRequestMessage request = CreateJsonRequest(new
            {
                action = "submit",
                claimTokenHash = ComputeClaimHash(_playerData.OnlineClaimToken),
                ending = endingValue,
                elapsedMilliseconds,
                failedAttempts,
                hintCount
            });
            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            string responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new ScoreSubmissionResult(false, 0, 0, $"클리어 기록을 저장하지 못했습니다. HTTP {(int)response.StatusCode}");
            }

            using JsonDocument document = JsonDocument.Parse(responseText);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("result", out JsonElement resultElement)
                || resultElement.ValueKind != JsonValueKind.String
                || resultElement.GetString() != "saved"
                || !document.RootElement.TryGetProperty("clearTimeMs", out JsonElement timeElement)
                || timeElement.ValueKind != JsonValueKind.Number
                || !timeElement.TryGetInt64(out long milliseconds)
                || milliseconds <= 0
                || !document.RootElement.TryGetProperty("rank", out JsonElement rankElement)
                || rankElement.ValueKind != JsonValueKind.Number
                || !rankElement.TryGetInt64(out long rank)
                || rank <= 0)
            {
                return new ScoreSubmissionResult(false, 0, 0, "클리어 기록 응답 형식이 올바르지 않습니다.");
            }

            return new ScoreSubmissionResult(true, milliseconds, rank, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ScoreSubmissionResult(false, 0, 0, "클리어 기록 저장 시간이 초과되었습니다.");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or IOException)
        {
            return new ScoreSubmissionResult(false, 0, 0, $"클리어 기록 서버에 연결하지 못했습니다: {exception.Message}");
        }
    }

    public async Task<ScoreSubmissionResult> SubmitFailureAsync(string claimToken, CancellationToken cancellationToken)
    {
        if (_functionUri is null || string.IsNullOrWhiteSpace(claimToken))
        {
            return new ScoreSubmissionResult(false, 0, 0, "온라인 닉네임 예약 정보가 없습니다.");
        }
        try
        {
            using HttpRequestMessage request = CreateJsonRequest(new
            {
                action = "fail",
                claimTokenHash = ComputeClaimHash(claimToken)
            });
            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new ScoreSubmissionResult(false, 0, 0, $"실패 기록 저장 오류: HTTP {(int)response.StatusCode}");
            }
            using JsonDocument document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("result", out JsonElement result) || result.ValueKind != JsonValueKind.String
                || result.GetString() != "saved"
                || !root.TryGetProperty("rank", out JsonElement rank) || rank.ValueKind != JsonValueKind.Number
                || !rank.TryGetInt64(out long value) || value <= 0)
            {
                return new ScoreSubmissionResult(false, 0, 0, "실패 기록 응답 형식이 올바르지 않습니다.");
            }
            return new ScoreSubmissionResult(true, 0, value, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ScoreSubmissionResult(false, 0, 0, "실패 기록 저장 시간이 초과되었습니다.");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or IOException)
        {
            return new ScoreSubmissionResult(false, 0, 0, $"실패 기록 서버 연결 오류: {exception.Message}");
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method)
    {
        HttpRequestMessage request = new(method, _functionUri);
        request.Headers.Add("apikey", _publishableKey);
        return request;
    }

    private HttpRequestMessage CreateJsonRequest<T>(T payload)
    {
        HttpRequestMessage request = CreateRequest(HttpMethod.Post);
        request.Content = JsonContent.Create(payload);
        return request;
    }

    private static string EnsureClaimToken()
    {
        byte[] randomBytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(randomBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string ComputeClaimHash(string token)
    {
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(digest);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
