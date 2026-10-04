using System.Diagnostics;
using System.Drawing.Imaging;
using System.Reflection;
using System.Text.Json;
using ThirteenthBell.Core;
using NAudio.Wave;

namespace ThirteenthBell;

internal static partial class PostalRoomSmoke
{
    private static int _checks;
    private static string _output = string.Empty;
    private static readonly JsonSerializerOptions ResultJsonOptions = new() { WriteIndented = true };
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        _output = Path.GetFullPath(args.Length > 0 ? args[0] : "artifacts/postal-smoke");
        Directory.CreateDirectory(_output);
        string dataPath = Path.Combine(_output, "isolated-player-data");
        Environment.SetEnvironmentVariable("THIRTEENTH_BELL_DATA_DIR", dataPath);
        PlayerDataStore playerStore = new(dataPath);
        Check(playerStore.TrySave(new PlayerData { TutorialSeen = true, Nickname = "PostalSmoke" }, out _), "isolated player data");
        ProgressBackupStore backupStore = new(dataPath);
        backupStore.TryDiscard(out _);
        Theme.Initialize();
        try
        {
            if (!args.Contains("--music-only", StringComparer.Ordinal))
            {
                CheckRules();
            }
            bool performance = args.Contains("--performance", StringComparer.Ordinal);
            Stopwatch startup = Stopwatch.StartNew();
            using GameForm form = new(new OfflineLeaderboard(), animationsEnabled: performance, audioEnabled: false);
            form.WindowState = FormWindowState.Normal;
            form.FormBorderStyle = FormBorderStyle.FixedSingle;
            form.ClientSize = performance ? new Size(1000, 600) : new Size(1400, 820);
            form.Location = new Point(20, 20);
            Set(form, "_soundEnabled", false);
            form.Show();
            PumpUntil(() => Get<bool>(form, "_startupSequenceCompleted"), TimeSpan.FromSeconds(30));
            if (performance)
            {
                double startupMs = startup.Elapsed.TotalMilliseconds;
                Call(form, "StartNewGame");
                Click(form, "continue");
                Control stage = Get<Control>(form, "_stage");
                using Bitmap frame = new(stage.Width, stage.Height);
                Stopwatch paint = Stopwatch.StartNew();
                for (int index = 0; index < 8; index++)
                {
                    stage.DrawToBitmap(frame, stage.ClientRectangle);
                }
                double paintMs = paint.Elapsed.TotalMilliseconds / 8;
                Control? transition = Get<Control?>(form, "_transitionOverlay");
                bool overlayFits = transition is null || transition.Bounds == stage.ClientRectangle;
                if (transition is not null)
                {
                    Check(!Get<System.Collections.IDictionary>(form, "_baseLayout").Contains(transition), "transition pixel bounds excluded from logical layout");
                    Call(form, "ApplyResponsiveLayout");
                    Check(transition.Bounds == stage.ClientRectangle, "repeated layout does not zoom transition");
                }
                Check(overlayFits, "transition covers stage at small window size");
                File.WriteAllText(Path.Combine(_output, "performance.json"), JsonSerializer.Serialize(new { startupMs, paintMs, overlayFits }, ResultJsonOptions));
                Console.WriteLine($"PERFORMANCE startupMs={startupMs:F1} paintMs={paintMs:F1} overlayFits={overlayFits}");
                Capture(form, "small-window-transition");
                form.ClientSize = new Size(1120, 680);
                Pump();
                Check(Get<Control?>(form, "_transitionOverlay") is null, "resize cancels stale-size transition");
                Set(form, "_gameInProgress", false);
                Set(form, "_allowClose", true);
                form.Close();
                return 0;
            }
            if (args.Contains("--requirements", StringComparer.Ordinal))
            {
                CheckRequirements(form);
                Set(form, "_gameInProgress", false);
                Set(form, "_allowClose", true);
                form.Close();
                File.WriteAllText(Path.Combine(_output, "requirements-result.json"), JsonSerializer.Serialize(new { passed = true, checks = _checks, rendering = "WinForms DrawToBitmap; 1400x820 and 1000x600" }, ResultJsonOptions));
                Console.WriteLine($"REQUIREMENTS_OK checks={_checks} output={_output}");
                return 0;
            }
            if (args.Contains("--music-only", StringComparer.Ordinal))
            {
                CheckPostalMusic(form);
                Set(form, "_gameInProgress", false);
                Set(form, "_allowClose", true);
                form.Close();
                File.WriteAllText(Path.Combine(_output, "music-result.json"), JsonSerializer.Serialize(new { passed = true, checks = _checks, playback = "MP3 decoding and screen routing; no listening test" }, ResultJsonOptions));
                Console.WriteLine($"POSTAL_MUSIC_OK checks={_checks} output={_output}");
                return 0;
            }
            if (args.Contains("--audio-init", StringComparer.Ordinal))
            {
                CheckAudioInitialization();
                Set(form, "_gameInProgress", false);
                Set(form, "_allowClose", true);
                form.Close();
                File.WriteAllText(Path.Combine(_output, "audio-init-result.json"), JsonSerializer.Serialize(new { passed = true, checks = _checks, playback = "WASAPI initialization, latest request and mute; no listening test" }, ResultJsonOptions));
                Console.WriteLine($"AUDIO_INIT_OK checks={_checks} output={_output}");
                return 0;
            }
            if (args.Contains("--visual-only", StringComparer.Ordinal))
            {
                Set(form, "_postal", new PostalRoomProgress { SuitcaseMoved = true, ParcelMoved = true });
                Call(form, "ShowPostalRoom");
                Check(Get<Dictionary<string, Button>>(form, "_actions").ContainsKey("postal_take_key"), "visible key keyboard action available");
                Call(form, "MovePostalProp", 0);
                Check(!Get<Dictionary<string, Button>>(form, "_actions").ContainsKey("postal_take_key"), "covered key keyboard action removed");
                Call(form, "MovePostalProp", 0);
                Check(Get<Dictionary<string, Button>>(form, "_actions").ContainsKey("postal_take_key"), "revealed key action restored");
                PostalRoomProgress fixture = PostalRoomProgress.CompletedLegacyRoom();
                fixture.RouteSolved = false;
                fixture.BellsSolved = false;
                fixture.DoorOpened = false;
                Set(form, "_postal", fixture);
                Call(form, "ShowPostalLedger");
                CheckTextFits(form, "PostalRouteRecord");
                Capture(form, "ledger-full-text");
                Call(form, "ShowPostalBells");
                CheckTextFits(form, "PostalBellRecord");
                Capture(form, "bells-full-text");
                form.ClientSize = new Size(1000, 600);
                Pump();
                CheckTextFits(form, "PostalBellRecord");
                Capture(form, "bells-small-window");
                Call(form, "ShowPostalLedger");
                CheckTextFits(form, "PostalRouteRecord");
                Capture(form, "ledger-small-window");
                form.ClientSize = new Size(1400, 820);
                fixture.RouteSolved = true;
                fixture.BellsSolved = true;
                Call(form, "ShowPostalRoom");
                Call(form, "ToggleInventoryExpanded");
                CheckTextFits(form, "InventoryText");
                Capture(form, "postal-inventory-expanded");
                Set(form, "_gameInProgress", false);
                Set(form, "_allowClose", true);
                form.Close();
                File.WriteAllText(Path.Combine(_output, "visual-result.json"), JsonSerializer.Serialize(new { passed = true, checks = _checks, screenshots = 5 }, ResultJsonOptions));
                Console.WriteLine($"POSTAL_VISUAL_OK checks={_checks} output={_output}");
                return 0;
            }
            Call(form, "StartNewGame");
            Check(Get<GameScreen>(form, "_screen") == GameScreen.Intro, "intro before new room");
            Capture(form, "01-intro");
            Click(form, "continue");
            Check(Get<GameScreen>(form, "_screen") == GameScreen.PostalRoom, "new room before workshop");
            Check(!Get<Control>(form, "_inventory").Visible, "unfound inventory hidden");
            Capture(form, "02-postal-room");
            Call(form, "TryOpenPostalDoor");
            Check(Get<GameScreen>(form, "_screen") == GameScreen.PostalRoom, "door blocks early entry");
            Check(Get<GameState>(form, "_state").FailedAttempts == 0, "inspection not counted as failure");
            Click(form, "postal_drawer");
            Check(!Get<PostalRoomProgress>(form, "_postal").DrawerOpened, "drawer requires key");

            SceneCanvas scene = Get<SceneCanvas>(form, "_scene");
            Mouse(scene, "OnMouseDown", 585, 640, MouseButtons.Left);
            Mouse(scene, "OnMouseUp", 585, 640, MouseButtons.Left);
            Check(Get<PostalRoomProgress>(form, "_postal").ParcelMoved, "parcel moved with mouse click");
            Check(!Get<PostalRoomProgress>(form, "_postal").KeyVisible, "key still beneath suitcase");
            Mouse(scene, "OnMouseDown", 430, 605, MouseButtons.Left);
            Mouse(scene, "OnMouseMove", 180, 605, MouseButtons.Left);
            Mouse(scene, "OnMouseUp", 180, 605, MouseButtons.Left);
            Check(Get<PostalRoomProgress>(form, "_postal").SuitcaseMoved, "suitcase dragged by mouse");
            Check(Get<PostalRoomProgress>(form, "_postal").KeyVisible, "both props required to reveal key");
            Capture(form, "03-key-revealed", hideNarrative: true);
            Mouse(scene, "OnMouseDown", 560, 645, MouseButtons.Left);
            Mouse(scene, "OnMouseUp", 560, 645, MouseButtons.Left);
            Check(Get<PostalRoomProgress>(form, "_postal").KeyFound, "key collected from rendered location");
            Check(!Get<PostalRoomProgress>(form, "_postal").KeyVisible, "collected key removed");
            Check(Get<Control>(form, "_inventory").Visible, "found item displayed");

            Click(form, "postal_bells");
            Click(form, "postal_bell_0");
            Check(Get<List<int>>(form, "_postalBellInput").Count == 0, "bell clue required");
            Call(form, "ShowPostalRoom");
            Mouse(scene, "OnMouseDown", 1060, 620, MouseButtons.Left);
            Mouse(scene, "OnMouseUp", 1060, 620, MouseButtons.Left);
            Check(Get<PostalRoomProgress>(form, "_postal").BellClueFound, "blanket reveals bell clue");
            Click(form, "postal_drawer");
            Check(Get<GameScreen>(form, "_screen") == GameScreen.PostalLedger, "key unlocks ledger");
            Capture(form, "04-postal-ledger");
            TextBox code = (TextBox)form.Controls.Find("PostalRouteCode", true).Single();
            code.Text = "00000";
            Click(form, "postal_route_submit");
            Check(Get<GameState>(form, "_state").FailedAttempts == 1, "route failure counted once");
            Check(!Get<PostalRoomProgress>(form, "_postal").RouteSolved, "wrong route stays locked");
            code.Text = "83614";
            Click(form, "postal_route_submit");
            Check(Get<PostalRoomProgress>(form, "_postal").RouteSolved, "correct route unlocks first seal");
            Click(form, "postal_door");
            Check(Get<GameScreen>(form, "_screen") == GameScreen.PostalRoom, "second seal still required");
            Click(form, "postal_bells");
            Capture(form, "05-postal-bells");
            Click(form, "postal_bell_check");
            Check(Get<GameState>(form, "_state").FailedAttempts == 1, "incomplete bells not failure");
            for (int index = 0; index < 6; index++)
            {
                Click(form, "postal_bell_0");
            }
            Click(form, "postal_bell_check");
            Check(Get<GameState>(form, "_state").FailedAttempts == 2, "wrong six-note sequence counted");
            Check(Get<List<int>>(form, "_postalBellInput").Count == 0, "wrong bells reset for retry");
            Click(form, "postal_bell_0");
            Click(form, "postal_bell_reset");
            Check(Get<List<int>>(form, "_postalBellInput").Count == 0, "manual reset");
            foreach (int bell in new[] { 0, 1, 3, 2, 2, 3 })
            {
                Click(form, $"postal_bell_{bell}");
            }
            Check(Get<List<int>>(form, "_postalBellInput").Count == 6, "six notes recorded");
            Click(form, "postal_bell_0");
            Check(Get<List<int>>(form, "_postalBellInput").Count == 6, "seventh note blocked");
            Capture(form, "06-six-bell-notes");
            Click(form, "postal_bell_check");
            Check(Get<PostalRoomProgress>(form, "_postal").CanOpenDoor, "all door prerequisites met");

            Call(form, "SaveProgressBackup", false);
            Check(backupStore.TryLoad(out ProgressBackup? saved, out _) && saved?.Postal?.CanOpenDoor == true, "postal prerequisites persist");
            Check(saved!.Postal!.SuitcaseMoved && saved.Postal.ParcelMoved && saved.Postal.BlanketMoved, "prop positions persist");
            Set(form, "_postal", new PostalRoomProgress());
            Call(form, "RestoreProgress", saved);
            Check(Get<PostalRoomProgress>(form, "_postal").CanOpenDoor, "postal restore");
            Check(Get<GameScreen>(form, "_screen") == GameScreen.PostalRoom, "restored to postal room");
            Click(form, "postal_door");
            Check(Get<PostalRoomProgress>(form, "_postal").DoorOpened, "key opens door after two seals");
            Check(Get<GameScreen>(form, "_screen") == GameScreen.Room, "original workshop follows new room");
            Check(Get<GameState>(form, "_state").SolvedPuzzles.Count == 0, "original six puzzles still unsolved");
            Capture(form, "07-workshop-transition");
            Click(form, "hotspot_desk");
            Check(Get<GameScreen>(form, "_screen") == GameScreen.Desk, "original desk navigation works");
            Call(form, "ShowRoom");
            Click(form, "hotspot_lantern");
            Check(form.Controls.Find("FrostVaultCode", true).Length == 1, "original vault works");

            ProgressBackup legacy = new()
            {
                Nickname = "LegacySmoke", Screen = "Room", SolvedPuzzles = [PuzzleId.Lanterns], ElapsedMilliseconds = 1000
            };
            Call(form, "RestoreProgress", legacy);
            Check(Get<GameScreen>(form, "_screen") == GameScreen.Room, "legacy backup resumes old room");
            Check(Get<PostalRoomProgress>(form, "_postal").DoorOpened, "legacy players keep progress");
            Check(Get<GameState>(form, "_state").SolvedPuzzles.Contains(PuzzleId.Lanterns), "legacy solved puzzle preserved");

            Call(form, "StartNewGame");
            Check(!Get<PostalRoomProgress>(form, "_postal").KeyFound, "new game resets postal key");
            Check(!Get<PostalRoomProgress>(form, "_postal").DoorOpened, "new game resets postal door");
            Click(form, "continue");
            form.ClientSize = new Size(1000, 600);
            Pump();
            Capture(form, "08-postal-small-window", hideNarrative: true);
            Mouse(scene, "OnMouseDown", 585, 640, MouseButtons.Left);
            Mouse(scene, "OnMouseUp", 585, 640, MouseButtons.Left);
            Check(Get<PostalRoomProgress>(form, "_postal").ParcelMoved, "mouse coordinates scale to small window");
            Click(form, "postal_move_suitcase");
            Check(Get<PostalRoomProgress>(form, "_postal").KeyVisible, "keyboard prop interaction");
            Click(form, "postal_take_key");
            Check(Get<PostalRoomProgress>(form, "_postal").KeyFound, "keyboard key collection");
            Call(form, "StartNewGame");
            Click(form, "continue");
            form.ClientSize = new Size(1400, 820);
            Pump();
            Capture(form, "09-final-postal-start", hideNarrative: true);
            Set(form, "_gameInProgress", false);
            Set(form, "_allowClose", true);
            form.Close();
            File.WriteAllText(Path.Combine(_output, "result.json"), JsonSerializer.Serialize(new { passed = true, checks = _checks, screenshots = 9, network = "offline stub", data = "isolated" }, ResultJsonOptions));
            Console.WriteLine($"POSTAL_SMOKE_OK checks={_checks} screenshots=9 output={_output}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally
        {
            Theme.Shutdown();
        }
    }

    private static void CheckRules()
    {
        Check(PostalPuzzleRules.MatchesRouteCode("83614"), "route answer");
        Check(!PostalPuzzleRules.MatchesRouteCode("8361"), "route length");
        Check(!PostalPuzzleRules.MatchesRouteCode(null), "null route");
        int accepted = 0;
        for (int mask = 0; mask < 4096; mask++)
        {
            int value = mask;
            int[] sequence = new int[6];
            for (int index = 0; index < 6; index++)
            {
                sequence[index] = value % 4;
                value /= 4;
            }
            if (PostalPuzzleRules.MatchesBellSequence(sequence))
            {
                accepted++;
            }
        }
        Check(accepted == 1, "unique bell sequence among 4096 candidates");
        Check(!PostalPuzzleRules.MatchesBellSequence([0, 1, 3]), "incomplete bell input");
        Check(!PostalPuzzleRules.MatchesBellSequence([0, 1, 3, 2, 2, 4]), "out of range bell input");
        Check(!new PostalRoomProgress { DoorOpened = true }.IsValid(), "invalid door progress rejected");
        Check(PostalRoomProgress.CompletedLegacyRoom().IsValid(), "legacy migration valid");
        using Bitmap sprites = new(Path.Combine(AppContext.BaseDirectory, "Assets", "postal-props.png"));
        Check(sprites.GetPixel(0, 0).A == 0, "sprite atlas has alpha transparency");
    }

    private static void CheckPostalMusic(GameForm form)
    {
        string musicPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Music", "first_game-bgmusic.mp3");
        using (AudioFileReader reader = new(musicPath))
        {
            Check(reader.Length > 0, "provided MP3 opens");
            byte[] samples = new byte[8192];
            Check(reader.Read(samples, 0, samples.Length) > 0, "provided MP3 decodes audio samples");
        }
        BackgroundMusicPlayer music = Get<BackgroundMusicPlayer>(form, "_music");
        Check(music.CurrentKind == BackgroundMusicKind.Menu, "menu keeps original music");
        Call(form, "StartNewGame");
        Check(music.CurrentKind == BackgroundMusicKind.PostalRoom, "intro selects provided music");
        Click(form, "continue");
        Check(music.CurrentKind == BackgroundMusicKind.PostalRoom, "postal room selects provided music");
        PostalRoomProgress fixture = PostalRoomProgress.CompletedLegacyRoom();
        fixture.DoorOpened = false;
        Set(form, "_postal", fixture);
        Call(form, "ShowPostalLedger");
        Check(music.CurrentKind == BackgroundMusicKind.PostalRoom, "ledger keeps postal music");
        Call(form, "ShowPostalBells");
        Check(music.CurrentKind == BackgroundMusicKind.PostalRoom, "bells keep postal music");
        Call(form, "ShowPostalRoom");
        Call(form, "TryOpenPostalDoor");
        Check(music.CurrentKind == BackgroundMusicKind.Game, "opening door selects original workshop music");
        Call(form, "ShowDesk");
        Check(music.CurrentKind == BackgroundMusicKind.Game, "desk keeps original workshop music");
        Call(form, "ShowMainMenu", false);
        Check(music.CurrentKind == BackgroundMusicKind.Menu, "return to menu restores menu music");
        ProgressBackup backup = new()
        {
            Nickname = "MusicSmoke", Screen = "PostalBells", Postal = fixture
        };
        fixture.DoorOpened = false;
        Call(form, "RestoreProgress", backup);
        Check(music.CurrentKind == BackgroundMusicKind.PostalRoom, "restored postal progress selects provided music");
        music.Enabled = false;
        Call(form, "ShowRoom");
        Check(music.CurrentKind == BackgroundMusicKind.PostalRoom, "locked room keeps postal music while muted");
        fixture.DoorOpened = true;
        Call(form, "ShowRoom");
        Check(music.CurrentKind == BackgroundMusicKind.Game, "muted transition records workshop track");
        music.Enabled = true;
        Check(music.CurrentKind == BackgroundMusicKind.Game, "unmute retains destination track");
    }

    private static void CheckAudioInitialization()
    {
        // DoEvents does not keep the context that Application.Run installs in the real game.
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        using BackgroundMusicPlayer music = new();
        Stopwatch timer = Stopwatch.StartNew();
        music.Play(BackgroundMusicKind.Menu);
        double requestMs = timer.Elapsed.TotalMilliseconds;
        Check(requestMs < 250, "audio request returns without waiting for decoder and device");
        music.Play(BackgroundMusicKind.PostalRoom);
        PumpUntil(() => music.IsPlaying || music.LastError is not null, TimeSpan.FromSeconds(15));
        Check(music.LastError is null, "audio device initializes: " + music.LastError);
        Check(music.IsPlaying, "latest requested track plays after background initialization");
        BackgroundMusicKind playing = (BackgroundMusicKind)typeof(BackgroundMusicPlayer).GetField("_playingKind", PrivateInstance)!.GetValue(music)!;
        Check(playing == BackgroundMusicKind.PostalRoom, "stale prepared menu track is discarded");
        music.Enabled = false;
        PumpUntil(() => !music.IsPlaying, TimeSpan.FromSeconds(3));
        Check(!music.IsPlaying, "mute stops asynchronously initialized track");
        using BackgroundMusicPlayer disposed = new();
        disposed.Play(BackgroundMusicKind.Menu);
        disposed.Dispose();
        PumpUntil(() => !(bool)typeof(BackgroundMusicPlayer).GetField("_opening", PrivateInstance)!.GetValue(disposed)!, TimeSpan.FromSeconds(15));
        Check(!disposed.IsPlaying, "closing during audio preparation does not start playback");
        Console.WriteLine($"AUDIO_REQUEST_MS={requestMs:F1}");
    }

    private static void Mouse(Control control, string eventMethod, int x, int y, MouseButtons button)
    {
        int scaledX = (int)Math.Round(x * control.Width / 1400d);
        int scaledY = (int)Math.Round(y * control.Height / 820d);
        typeof(Control).GetMethod(eventMethod, PrivateInstance)!.Invoke(control, [new MouseEventArgs(button, 1, scaledX, scaledY, 0)]);
        Pump();
    }

    private static void Capture(GameForm form, string name, bool hideNarrative = false)
    {
        if (hideNarrative)
        {
            Get<Control>(form, "_sidebar").Visible = false;
        }
        Pump();
        Control stage = Get<Control>(form, "_stage");
        using Bitmap bitmap = new(stage.Width, stage.Height);
        stage.DrawToBitmap(bitmap, stage.ClientRectangle);
        if (Get<Control?>(form, "_confirmationOverlay") is Control overlay)
        {
            // DrawToBitmap can reverse the order of sibling containers. Render the top overlay separately.
            using Bitmap overlayBitmap = new(overlay.Width, overlay.Height);
            overlay.DrawToBitmap(overlayBitmap, overlay.ClientRectangle);
            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.DrawImageUnscaled(overlayBitmap, overlay.Location);
        }
        bitmap.Save(Path.Combine(_output, name + ".png"), ImageFormat.Png);
    }

    private static void CheckTextFits(GameForm form, string name)
    {
        Label label = (Label)form.Controls.Find(name, true).Single();
        Size measured = TextRenderer.MeasureText(label.Text, label.Font, new Size(label.Width, int.MaxValue), TextFormatFlags.WordBreak);
        Console.WriteLine($"TEXT {name}: font={label.Font.Size} needed={measured.Height} available={label.Height}");
        Check(measured.Height <= label.Height, name + " all clue text fits");
    }

    private static void Check(bool condition, string label)
    {
        if (!condition)
        {
            throw new InvalidOperationException("CHECK FAILED: " + label);
        }
        _checks++;
        Console.WriteLine("PASS " + label);
    }

    private static T Get<T>(GameForm form, string field)
    {
        return (T)typeof(GameForm).GetField(field, PrivateInstance)!.GetValue(form)!;
    }

    private static void Set(GameForm form, string field, object value)
    {
        typeof(GameForm).GetField(field, PrivateInstance)!.SetValue(form, value);
    }

    private static void Call(GameForm form, string method, params object[] args)
    {
        typeof(GameForm).GetMethod(method, PrivateInstance)!.Invoke(form, args);
        Pump();
    }

    private static void Click(GameForm form, string id)
    {
        Get<Dictionary<string, Button>>(form, "_actions")[id].PerformClick();
        Pump();
    }

    private static void Pump()
    {
        Application.DoEvents();
        Thread.Sleep(30);
        Application.DoEvents();
    }

    private static void PumpUntil(Func<bool> ready, TimeSpan timeout)
    {
        Stopwatch timer = Stopwatch.StartNew();
        while (!ready() && timer.Elapsed < timeout)
        {
            Pump();
        }
        Check(ready(), "startup completes");
    }

    private sealed class OfflineLeaderboard : ILeaderboardService
    {
        public Task<LeaderboardLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new LeaderboardLoadResult([], null));
        }

        public Task<NicknameReservationResult> ReserveNicknameAsync(string nickname, CancellationToken cancellationToken)
        {
            return Task.FromResult(new NicknameReservationResult(NicknameReservationStatus.Reserved, nickname, null));
        }

        public Task<ScoreSubmissionResult> SubmitClearAsync(EndingChoice ending, CancellationToken cancellationToken)
        {
            return Task.FromResult(new ScoreSubmissionResult(true, 1000, 1, null));
        }

        public void Dispose()
        {
        }
    }
}
