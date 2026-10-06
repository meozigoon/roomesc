using System.Diagnostics;
using System.Drawing.Imaging;
using System.Reflection;
using System.Text.Json;
using ThirteenthBell.Core;

namespace ThirteenthBell;

internal static class Speedrun
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly List<object> Actions = [];
    private static string _output = string.Empty;
    private static int _exitCode = 1;

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: Speedrun <nickname> <output-directory> [--rehearsal]");
            return 2;
        }
        bool rehearsal = args.Contains("--rehearsal", StringComparer.Ordinal);
        _output = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(_output);
        string dataPath = Path.Combine(_output, "player-data");
        Environment.SetEnvironmentVariable("THIRTEENTH_BELL_DATA_DIR", dataPath);
        PlayerDataStore store = new(dataPath);
        if (File.Exists(store.FilePath))
        {
            throw new InvalidOperationException("Use a fresh output directory to preserve any existing online session.");
        }
        if (!store.TrySave(new PlayerData { TutorialSeen = true, Nickname = args[0] }, out string? error))
        {
            throw new IOException(error);
        }
        ApplicationConfiguration.Initialize();
        Theme.Initialize();
        try
        {
            using GameForm form = new(rehearsal ? new RehearsalLeaderboard() : null,
                animationsEnabled: true, audioEnabled: true);
            form.WindowState = FormWindowState.Normal;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.ClientSize = new Size(1400, 820);
            form.Shown += async (_, _) => await RunAsync(form, args[0], rehearsal);
            Application.Run(form);
        }
        finally
        {
            Theme.Shutdown();
        }
        return _exitCode;
    }

    private static async Task RunAsync(GameForm form, string nickname, bool rehearsal)
    {
        try
        {
            await WaitUntilAsync(() => Get<bool>(form, "_startupSequenceCompleted"));
            await StableAsync(form);
            await ClickAsync(form, "start");
            Find<TextBox>(form, "NicknameEditor").Text = nickname;
            await ClickAsync(form, "nickname_confirm");
            await WaitUntilAsync(() => Get<GameScreen>(form, "_screen") == GameScreen.Intro
                || (Find<Label>(form, "NicknameStatus").Text.Contains("사용할 수 없습니다", StringComparison.Ordinal)));
            if (Get<GameScreen>(form, "_screen") != GameScreen.Intro)
            {
                throw new InvalidOperationException(Find<Label>(form, "NicknameStatus").Text);
            }
            await StableAsync(form);
            Console.WriteLine("STARTING " + nickname + (rehearsal ? " offline rehearsal" : " live online run"));

            await ClickAsync(form, "continue");
            await ClickAsync(form, "postal_move_suitcase");
            await ClickAsync(form, "postal_move_parcel");
            await ClickAsync(form, "postal_take_key");
            await ClickAsync(form, "postal_move_blanket");
            await ClickAsync(form, "postal_drawer");
            await EnterAsync(form, "PostalRouteCode", "postal_route_submit", "83614");
            await ClickAsync(form, "completion_return");
            await ClickAsync(form, "postal_bells");
            foreach (int bell in new[] { 0, 1, 3, 2, 2, 3 })
            {
                await ClickAsync(form, $"postal_bell_{bell}");
            }
            await ClickAsync(form, "postal_bell_check");
            await ClickAsync(form, "completion_return");
            await ClickAsync(form, "postal_door");
            await ClickAsync(form, "hotspot_lantern");
            await EnterAsync(form, "FrostVaultCode", "frost_vault_confirm", "161");
            await ClickAsync(form, "completion_return");
            await ClickAsync(form, "hotspot_melody");
            await EnterAsync(form, "OrnamentEquationCode", "ornament_equation_confirm", "44");
            await ClickAsync(form, "completion_return");
            await ClickAsync(form, "hotspot_loom");
            StockingPiece[] pieces = Get<StockingPiece[]>(form, "_stockingPieces");
            StockingHookSlot[] slots = Get<StockingHookSlot[]>(form, "_stockingSlots");
            for (int index = 0; index < 4; index++)
            {
                DropPiece(pieces[index], slots[index]);
                Record(form, "stocking_drop_" + index);
            }
            await StableAsync(form);
            await ClickAsync(form, "completion_return");
            await ClickAsync(form, "hotspot_desk");
            await ClickAsync(form, "desk_letter");
            await ClickAsync(form, "letter_decipher");
            await EnterAsync(form, "LetterAcrosticCode", "letter_acrostic_confirm", "CHIMNEY");
            await ClickAsync(form, "completion_return");
            await ClickAsync(form, "desk_toys");
            await EnterAsync(form, "ToyCipherCode", "toy_cipher_confirm", "MIDNIGHT");
            await ClickAsync(form, "completion_return");
            await ClickAsync(form, "desk_chart");
            await EnterAsync(form, "StarChartCode", "star_chart_confirm", "AURORA");
            await ClickAsync(form, "completion_return");
            Get<Button>(form, "_roomButton").PerformClick();
            Record(form, "room_button");
            await StableAsync(form);
            await ClickAsync(form, "hotspot_clock");
            Find<NumericUpDown>(form, "ClockHour").Value = 12;
            Find<NumericUpDown>(form, "ClockMinute").Value = 0;
            Find<NumericUpDown>(form, "ClockDate").Value = 25;
            await ClickAsync(form, "clock_confirm");
            await ClickAsync(form, "choice_gift");

            GameState state = Get<GameState>(form, "_state");
            PostalRoomProgress postal = Get<PostalRoomProgress>(form, "_postal");
            if (state.SolvedPuzzles.Count != 6 || !state.ClockRestored
                || state.Ending != EndingChoice.DeliverTheGift || !postal.DoorOpened
                || state.FailedAttempts != 0 || state.HintCount != 0)
            {
                throw new InvalidOperationException("The full route did not complete with zero failures and hints.");
            }
            await WaitUntilAsync(() => Get<Label>(form, "_hintText").Text.Contains("현재 순위", StringComparison.Ordinal)
                || Get<Label>(form, "_hintText").Text.Contains("온라인 저장 실패", StringComparison.Ordinal));
            string endingText = Get<Label>(form, "_hintText").Text;
            if (endingText.Contains("온라인 저장 실패", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(endingText);
            }
            long elapsedMilliseconds = (long)Get<Stopwatch>(form, "_gameStopwatch").Elapsed.TotalMilliseconds;
            Capture(form, "ending.png");
            await ClickAsync(form, "ending_menu");
            await WaitUntilAsync(() => Descendants(form).OfType<Label>().Any(label => label.Visible && label.Text == nickname));
            Capture(form, "leaderboard.png");
            string[] leaderboardText = Descendants(Get<Control>(form, "_menuScene")).OfType<Label>()
                .Where(label => label.Visible).Select(label => label.Text).ToArray();
            File.WriteAllText(Path.Combine(_output, "result.json"), JsonSerializer.Serialize(new
            {
                passed = true, nickname, rehearsal, elapsedMilliseconds, endingText,
                solvedPuzzles = state.SolvedPuzzles.Select(puzzle => puzzle.ToString()).ToArray(),
                postal, state.FailedAttempts, state.HintCount, leaderboardText, actions = Actions,
                animationsEnabled = true, audioEnabled = true,
                method = "Actual GameForm button and mouse handlers, unmodified Stopwatch and SupabaseLeaderboardService"
            }, JsonOptions));
            Console.WriteLine($"CLEARED nickname={nickname} milliseconds={elapsedMilliseconds}");
            Console.WriteLine(endingText);
            _exitCode = 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            Capture(form, "error.png");
            File.WriteAllText(Path.Combine(_output, "error.json"), JsonSerializer.Serialize(new
            {
                error = exception.ToString(), screen = Get<GameScreen>(form, "_screen").ToString(), actions = Actions,
                buttons = Get<Dictionary<string, Button>>(form, "_actions").Select(pair => new
                {
                    pair.Key, pair.Value.Visible, pair.Value.Enabled, pair.Value.IsDisposed
                }).ToArray()
            }, JsonOptions));
        }
        finally
        {
            if (!Get<bool>(form, "_gameInProgress"))
            {
                form.Close();
            }
            else
            {
                // Keep an interrupted real run open with its normal backup/exit flow.
                Console.Error.WriteLine("Run remains open; normal game recovery data is preserved.");
            }
        }
    }

    private static async Task ClickAsync(GameForm form, string id)
    {
        await WaitUntilAsync(() => Get<Dictionary<string, Button>>(form, "_actions").TryGetValue(id, out Button? button)
            && button.Visible && button.Enabled);
        Get<Dictionary<string, Button>>(form, "_actions")[id].PerformClick();
        Record(form, id);
        await StableAsync(form);
    }

    private static async Task EnterAsync(GameForm form, string name, string action, string text)
    {
        Find<TextBox>(form, name).Text = text;
        await ClickAsync(form, action);
    }

    private static async Task StableAsync(GameForm form)
    {
        await Task.Delay(1);
        await WaitUntilAsync(() => Get<Control?>(form, "_transitionOverlay") is null);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        Stopwatch timer = Stopwatch.StartNew();
        while (!condition())
        {
            if (timer.Elapsed > TimeSpan.FromSeconds(30))
            {
                throw new TimeoutException("Timed out waiting for the normal game UI.");
            }
            await Task.Delay(1);
        }
    }

    private static void DropPiece(StockingPiece piece, StockingHookSlot target)
    {
        Point center = new(target.Left + target.Width / 2, target.Top + target.Height / 2);
        Point local = piece.PointToClient(piece.Parent!.PointToScreen(center));
        typeof(StockingPiece).GetMethod("OnMouseDown", PrivateInstance)!.Invoke(piece,
            [new MouseEventArgs(MouseButtons.Left, 1, piece.Width / 2, piece.Height / 2, 0)]);
        typeof(StockingPiece).GetMethod("OnMouseMove", PrivateInstance)!.Invoke(piece,
            [new MouseEventArgs(MouseButtons.Left, 1, local.X, local.Y, 0)]);
        typeof(StockingPiece).GetMethod("OnMouseUp", PrivateInstance)!.Invoke(piece,
            [new MouseEventArgs(MouseButtons.Left, 1, piece.Width / 2, piece.Height / 2, 0)]);
    }

    private static T Get<T>(GameForm form, string field)
    {
        return (T)typeof(GameForm).GetField(field, PrivateInstance)!.GetValue(form)!;
    }

    private static T Find<T>(GameForm form, string name) where T : Control
    {
        return (T)form.Controls.Find(name, true).Single();
    }

    private static void Record(GameForm form, string id)
    {
        Actions.Add(new { id, milliseconds = Get<Stopwatch>(form, "_gameStopwatch").Elapsed.TotalMilliseconds });
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            yield return control;
            foreach (Control child in Descendants(control))
            {
                yield return child;
            }
        }
    }

    private static void Capture(GameForm form, string filename)
    {
        form.Refresh();
        using Bitmap bitmap = new(form.ClientSize.Width, form.ClientSize.Height);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(form.PointToScreen(Point.Empty), Point.Empty, form.ClientSize);
        bitmap.Save(Path.Combine(_output, filename), ImageFormat.Png);
    }

    private sealed class RehearsalLeaderboard : ILeaderboardService
    {
        private long _elapsed;

        public Task<LeaderboardLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new LeaderboardLoadResult([new LeaderboardEntry("admin", _elapsed)], null, 1));
        }

        public Task<NicknameReservationResult> ReserveNicknameAsync(string nickname, CancellationToken cancellationToken)
        {
            return Task.FromResult(new NicknameReservationResult(NicknameReservationStatus.Reserved, nickname, null));
        }

        public Task<ScoreSubmissionResult> SubmitClearAsync(EndingChoice ending, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Elapsed time is required for this rehearsal.");
        }

        public Task<ScoreSubmissionResult> SubmitClearAsync(EndingChoice ending, long? elapsedMilliseconds,
            int failedAttempts, int hintCount, CancellationToken cancellationToken)
        {
            _elapsed = elapsedMilliseconds ?? throw new InvalidOperationException("No measured elapsed time.");
            return Task.FromResult(new ScoreSubmissionResult(true, _elapsed, 1, null));
        }

        public void Dispose()
        {
        }
    }
}
