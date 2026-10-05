using ThirteenthBell.Core;

namespace ThirteenthBell;

internal sealed partial class GameForm
{
    private Task? _failureFlushTask;
    private readonly System.Windows.Forms.Timer _failureRetryTimer = new() { Interval = 30000 };
    private string? _terminalFailureToken;

    private static string FormatLeaderboardResult(LeaderboardEntry entry)
    {
        return entry.Failed ? "실패" : FormatOnlineTime(entry.ClearTimeMilliseconds);
    }

    private bool EndRunIfFailureLimitExceeded()
    {
        if (!_state.ClearFailed)
        {
            return false;
        }
        if (_gameInProgress)
        {
            ShowClearFailure();
        }
        return true;
    }

    private void ShowClearFailure()
    {
        _elapsedTimer.Stop();
        _gameStopwatch.Stop();
        _terminalFailureToken = _playerData.OnlineClaimToken;
        bool queued = QueueOnlineFailure(_terminalFailureToken, flushImmediately: false);
        _gameInProgress = false;
        if (queued)
        {
            _progressBackupStore.TryDiscard(out _);
        }
        _pendingBackup = null;
        CloseConfirmationOverlay();
        ClearSessionMemo();
        UpdateWindowCloseAvailability();
        SetScreen(GameScreen.Ending, "클리어 실패", "실패 횟수가 10회를 초과하여 게임이 끝났습니다.",
            $"플레이어: {_playerData.Nickname}\n결과: 실패\n실패 시도: {_state.FailedAttempts}회\n온라인 실패 기록을 저장하는 중입니다.");
        _scene.SceneImage = _images[WorkshopImage];
        _inventory.Visible = false;
        _music.Play(BackgroundMusicKind.Menu);
        AtmosphereCard card = CreateAtmosphereCard("클리어 실패",
            "열한 번째 시도가 어긋나자 공방의 종소리가 멈췄습니다.\n\n이 게임은 실패로 기록됩니다.\n새 닉네임으로 다시 시작할 수 있습니다.",
            AtmosphereCardStyle.Letter, new Rectangle(310, 125, 780, 300),
            new Rectangle(50, 30, 680, 55), new Rectangle(65, 100, 650, 145), 20, 14);
        card.Name = "FailureDocument";
        _scene.Controls.Add(card);
        AddAction("restart", "다시 시작", new Rectangle(450, 470, 230, 58), (_, _) => ShowNicknameSetup(), 1);
        AddAction("ending_menu", "메인 메뉴", new Rectangle(720, 470, 230, 58), (_, _) => ShowMainMenu(), 2);
        if (!queued)
        {
            _hintText.Text = "결과: 실패\n실패 기록을 저장하지 못했습니다. 저장 폴더를 확인해 주세요.";
            ShowNarrativeBox();
        }
        else
        {
            if (string.IsNullOrWhiteSpace(_terminalFailureToken))
            {
                _hintText.Text = "결과: 실패\n온라인 닉네임 예약 정보가 없어 순위를 등록할 수 없습니다.";
                ShowNarrativeBox();
            }
            else
            {
                _ = FlushPendingFailuresAsync();
            }
        }
        PlaySound(GameSound.Wrong);
    }

    private bool QueueOnlineFailure(string token, bool flushImmediately = true)
    {
        // Older/offline games may have no online reservation to submit.
        if (string.IsNullOrWhiteSpace(token))
        {
            return true;
        }
        bool added = !_playerData.PendingFailureTokens.Contains(token, StringComparer.Ordinal);
        string previousFailedToken = _playerData.LastFailedClaimToken;
        _playerData.LastFailedClaimToken = token;
        if (added)
        {
            _playerData.PendingFailureTokens.Add(token);
        }
        if (!_playerDataStore.TrySave(_playerData, out string? error))
        {
            _playerData.LastFailedClaimToken = previousFailedToken;
            if (added)
            {
                _playerData.PendingFailureTokens.Remove(token);
            }
            AppendDataWarning(error);
            return false;
        }
        if (flushImmediately)
        {
            _ = FlushPendingFailuresAsync();
        }
        return true;
    }

    private Task FlushPendingFailuresAsync()
    {
        if (_failureFlushTask is { IsCompleted: false })
        {
            return _failureFlushTask;
        }
        _failureRetryTimer.Enabled = _playerData.PendingFailureTokens.Count > 0;
        _failureFlushTask = FlushPendingFailuresCoreAsync();
        return _failureFlushTask;
    }

    private async Task FlushPendingFailuresCoreAsync()
    {
        while (!_shutdownPrepared && _playerData.PendingFailureTokens.Count > 0)
        {
            string token = _playerData.PendingFailureTokens[0];
            ScoreSubmissionResult result;
            try
            {
                // A screen change must not cancel a committed outcome.
                result = await _leaderboardService.SubmitFailureAsync(token, CancellationToken.None);
            }
            catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or IOException or ObjectDisposedException)
            {
                result = new ScoreSubmissionResult(false, 0, 0, exception.Message);
            }
            if (_shutdownPrepared || IsDisposed || Disposing)
            {
                return;
            }
            if (result.Succeeded)
            {
                _playerData.PendingFailureTokens.Remove(token);
                if (!_playerDataStore.TrySave(_playerData, out string? error))
                {
                    _playerData.PendingFailureTokens.Insert(0, token);
                    AppendDataWarning(error);
                    return;
                }
            }
            if (_screen == GameScreen.Ending && token == _terminalFailureToken)
            {
                _hintText.Text = result.Succeeded
                    ? $"결과: 실패\n현재 순위: {result.Rank}위 (꼴찌)\n실패 시도: {_state.FailedAttempts}회"
                    : "결과: 실패\n온라인 저장 대기 중입니다.\n연결이 복구되면 다시 저장합니다.";
                ShowNarrativeBox();
            }
            if (result.Succeeded && _screen == GameScreen.Menu
                && _menuScene.Controls.Find("LeaderboardText", false).FirstOrDefault() is Label ranking)
            {
                _ = LoadLeaderboardAsync(ranking, _menuCancellation.Token);
            }
            if (!result.Succeeded)
            {
                return;
            }
        }
        _failureRetryTimer.Stop();
    }

    private async Task CloseAfterPendingFailureAsync()
    {
        await FlushPendingFailuresAsync();
        if (!IsDisposed && !Disposing)
        {
            _allowClose = true;
            Close();
        }
    }
}
