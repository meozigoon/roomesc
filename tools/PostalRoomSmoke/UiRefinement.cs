using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using ThirteenthBell.Core;

namespace ThirteenthBell;

internal static partial class PostalRoomSmoke
{
    private static readonly string[] HiddenShortcutNames = ["F1", "F11", "Esc", "Ctrl", "Tab", "Enter"];
    private static readonly PuzzleId[] CompletedDeskPuzzles = [PuzzleId.LetterAcrostic, PuzzleId.ToyCipher, PuzzleId.StarChart];
    private static readonly string[] RefinementWindowSizes = ["1400x820", "1000x600", "1680x1000"];
    private static bool _clipboardUnavailable;
    private static void CheckUiRefinement(GameForm form)
    {
        Set(form, "_postal", PostalRoomProgress.CompletedLegacyRoom());
        Set(form, "_state", new GameState());
        string[] puzzles = ["ShowLanternPuzzle", "ShowSnowglobePuzzle", "ShowStockingPuzzle", "ShowLetterAcrosticPuzzle", "ShowToyCipherPuzzle", "ShowStarChartPuzzle"];
        foreach (Size size in new[] { new Size(1400, 820), new Size(1000, 600), new Size(1680, 1000) })
        {
            form.ClientSize = size;
            Call(form, "ShowRoom");
            SceneCanvas scene = Get<SceneCanvas>(form, "_scene");
            float scale = scene.Width / 1400f;
            (string Id, Point Center, GameScreen Screen)[] hotspots =
            [
                ("hotspot_lantern", new Point(240, 550), GameScreen.Lanterns),
                ("hotspot_desk", new Point(250, 400), GameScreen.Desk),
                ("hotspot_melody", new Point(485, 650), GameScreen.Melody),
                ("hotspot_clock", new Point(750, 185), GameScreen.Clock),
                ("hotspot_loom", new Point(1060, 420), GameScreen.RibbonLoom)
            ];
            foreach (var hotspot in hotspots)
            {
                Call(form, "ShowRoom");
                Point point = new((int)Math.Round(hotspot.Center.X * scale), (int)Math.Round(hotspot.Center.Y * scale));
                Check(scene.GetChildAtPoint(point)?.Name == hotspot.Id, $"artwork point hits {hotspot.Id} at {size.Width}");
                Click(form, hotspot.Id);
                Check(Get<GameScreen>(form, "_screen") == (hotspot.Screen == GameScreen.Clock ? GameScreen.Room : hotspot.Screen), hotspot.Id + " opens correct scene");
                Check(Get<Button>(form, "_roomButton").Visible, "return button always visible " + hotspot.Id);
            }
            Call(form, "ShowDesk");
            Check(scene.GetChildAtPoint(new Point((int)(715 * scale), (int)(540 * scale)))?.Name == "desk_letter", "envelope hit region " + size.Width);
            Check(scene.GetChildAtPoint(new Point((int)(330 * scale), (int)(520 * scale)))?.Name == "desk_chart", "chart hit region " + size.Width);
            foreach (string puzzle in puzzles)
            {
                Call(form, puzzle);
                string instructions = Get<Label>(form, "_notebookText").Text;
                Call(form, "ShowHint");
                Check(Get<Label>(form, "_notebookText").Text == instructions, puzzle + " hint preserves instructions");
                Call(form, "ShowNarrativeMessage", "오답입니다. 다시 확인하세요.");
                Check(Get<Label>(form, "_notebookText").Text == instructions, puzzle + " feedback preserves instructions");
                Set(form, "_narrativeShownAt", DateTimeOffset.UtcNow.AddMinutes(-1));
                Call(form, "HandleNarrativeFadeTick", form, EventArgs.Empty);
                Check(Get<Control>(form, "_sidebar").Visible && !Get<System.Windows.Forms.Timer>(form, "_narrativeFadeTimer").Enabled,
                    puzzle + " instructions stay visible after a minute");
                CheckVisibleText(form, puzzle + " refinement " + size.Width);
                foreach (AtmosphereCard card in scene.Controls.OfType<AtmosphereCard>())
                {
                    Check(card.ClientRectangle.Contains(card.TitleLabel.Bounds) && card.ClientRectangle.Contains(card.BodyLabel.Bounds), card.Name + " text bounds inside card");
                    Check(!card.TitleLabel.Bounds.IntersectsWith(card.BodyLabel.Bounds), card.Name + " title and body separated");
                }
                TextBox? input = scene.Controls.OfType<TextBox>().FirstOrDefault();
                if (input is not null)
                {
                    Check(input.ImeMode == ImeMode.Disable, input.Name + " IME disabled");
                    if (input.Name is "FrostVaultCode" or "OrnamentEquationCode")
                    {
                        input.Text = "한1２2a3";
                        Check(input.Text == "123", input.Name + " allows ASCII digits only");
                    }
                    else
                    {
                        input.Text = "한a글b!c3";
                        Check(input.Text == "ABC", input.Name + " filters Korean and uppercases");
                        input.SelectAll();
                        if (!_clipboardUnavailable)
                        {
                            try
                            {
                                Clipboard.SetText("한글midnight!");
                                input.Paste();
                            }
                            catch (System.Runtime.InteropServices.ExternalException)
                            {
                                _clipboardUnavailable = true;
                                Console.WriteLine("CLIPBOARD_UNAVAILABLE: system clipboard denied access; verifying replacement text filtering");
                            }
                        }
                        if (_clipboardUnavailable)
                        {
                            input.SelectedText = "한글midnight!";
                        }
                        if (input.Text != "MIDNIGHT")
                        {
                            Console.WriteLine($"PASTE_DIAGNOSTIC control={input.Name} actualLength={input.Text.Length} selectionLength={input.SelectionLength} maxLength={input.MaxLength}");
                        }
                        Check(input.Text == "MIDNIGHT", input.Name + (_clipboardUnavailable ? " replacement text filtered and uppercased" : " paste filtered and uppercased"));
                    }
                }
                Capture(form, "refinement-" + puzzle + "-" + size.Width);
            }
        }
        CheckStockingResize(form);
        form.ClientSize = new Size(1400, 820);
        Set(form, "_state", GameState.Restore(CompletedDeskPuzzles, 0, 0, false));
        Call(form, "ShowDesk");
        Call(form, "ShowHint");
        Check(Get<Label>(form, "_hintText").Text == "제공할 힌트가 없습니다", "completed desk has no hints despite unresolved room puzzles");
        Check(Get<GameState>(form, "_state").HintCount == 0, "no available hint does not increase hint count");
        Call(form, "ShowRoom");
        Call(form, "ShowHint");
        Check(Get<Label>(form, "_hintText").Text.Contains("황동 금고", StringComparison.Ordinal), "room hints only clickable room objects");
        Check(!Get<Label>(form, "_inventoryTitle").Text.Contains("눌러서 보기", StringComparison.Ordinal), "memory title removes click instruction");
        Check(Get<Control>(form, "_inventory").Width < 250, "collapsed memory box fits title");
        Call(form, "ToggleInventoryExpanded");
        CheckTextFits(form, "InventoryText");
        Check(Get<Control>(form, "_inventory").Width < 720 && Get<Control>(form, "_inventory").Height < 180, "expanded inventory fits acquired content");
        Capture(form, "refinement-workshop-memory");
        Check(Get<Button>(form, "_hintButton").Text == "힌트", "hint button removes F1");
        Call(form, "ShowGuideMenu");
        Check(!Get<SceneCanvas>(form, "_menuScene").Controls.OfType<Label>().Any(label => HiddenShortcutNames.Any(label.Text.Contains)), "game guide omits shortcut explanations");
        foreach (EndingChoice ending in Enum.GetValues<EndingChoice>())
        {
            if (ending == EndingChoice.None)
            {
                continue;
            }
            Set(form, "_state", GameState.Restore(Enum.GetValues<PuzzleId>(), 0, 0, true));
            Call(form, "ShowEnding", ending);
            Check(!Get<SceneCanvas>(form, "_scene").Controls.OfType<AtmosphereCard>().Any(card => card.TitleLabel.Text.Contains("진엔딩", StringComparison.Ordinal)), "ending omits true-ending label " + ending);
        }
        CheckLeaderboardUi(form);
        Console.WriteLine("PHASE full leaderboard paging");
        Task paging = CheckFullLeaderboardPagingAsync();
        PumpUntil(() => paging.IsCompleted, TimeSpan.FromSeconds(10));
        paging.GetAwaiter().GetResult();
        Console.WriteLine("PHASE letter animation");
        CheckLetterAnimation();
        File.WriteAllText(Path.Combine(_output, "ui-refinement-result.json"), JsonSerializer.Serialize(new { passed = true, checks = _checks, windows = RefinementWindowSizes, liveLeaderboard = "offline fixture; live outcome verification stored separately", input = "IME property and text filtering", clipboardUnavailable = _clipboardUnavailable, animation = "timed frames and completion" }, ResultJsonOptions));
        Console.WriteLine($"UI_REFINEMENT_OK checks={_checks}");
    }

    private static void CheckStockingResize(GameForm form)
    {
        Set(form, "_state", new GameState());
        form.ClientSize = new Size(1400, 820);
        Call(form, "ShowStockingPuzzle");
        Call(form, "PlaceStockingInSlot", 0, 0);
        Call(form, "PlaceStockingInSlot", 1, 1);
        foreach (Size size in new[] { new Size(1000, 600), new Size(1400, 820) })
        {
            form.ClientSize = size;
            Pump();
            StockingPiece[] pieces = Get<StockingPiece[]>(form, "_stockingPieces");
            StockingHookSlot[] slots = Get<StockingHookSlot[]>(form, "_stockingSlots");
            float scale = Get<SceneCanvas>(form, "_scene").Width / 1400f;
            for (int pieceId = 0; pieceId < 2; pieceId++)
            {
                Check(Math.Abs(pieces[pieceId].Left + pieces[pieceId].Width / 2 - slots[pieceId].Left - slots[pieceId].Width / 2) <= 1,
                    "placed stocking stays aligned after resize " + size.Width + " piece " + pieceId);
                Check(pieces[pieceId].Top == slots[pieceId].Top + (int)Math.Round(19 * scale), "stocking hanging offset scales " + size.Width);
            }
            DropPiece(pieces[0], null);
            Check(Get<int[]>(form, "_stockingPieceSlots")[0] == -1 && Math.Abs(pieces[0].Top - (int)Math.Round(pieces[0].HomeBounds.Top * scale)) <= 1,
                "stocking outside drop returns home at current scale " + size.Width);
            Call(form, "PlaceStockingInSlot", 0, 0);
        }
    }

    private static void CheckLeaderboardUi(GameForm form)
    {
        OfflineLeaderboard service = (OfflineLeaderboard)Get<ILeaderboardService>(form, "_leaderboardService");
        foreach (int count in new[] { 0, 5, 6, 60 })
        {
            service.EntryCount = count;
            Call(form, "ShowMainMenu", false);
            Label label = Get<SceneCanvas>(form, "_menuScene").Controls.OfType<Label>().Single();
            Check(label.Controls.Cast<Control>().Count(cell => cell.Name.StartsWith("RankingRow", StringComparison.Ordinal) && cell.Name.EndsWith("Column0", StringComparison.Ordinal)) == Math.Min(5, count), "menu shows at most five entries " + count);
            bool full = Get<Dictionary<string, Button>>(form, "_actions").ContainsKey("leaderboard_all");
            Check(full == (count > 5), "full-list button threshold " + count);
            if (full)
            {
                Click(form, "leaderboard_all");
                ListView list = (ListView)form.Controls.Find("FullLeaderboard", true).Single();
                Check(list.Items.Count == count, "full list contains all entries " + count);
                list.EnsureVisible(count - 1);
                Check(list.Items[count - 1].SubItems[1].Text == $"Tester{count}", "last leaderboard row accessible " + count);
                Console.WriteLine("CAPTURE leaderboard " + count);
                Capture(form, "refinement-leaderboard-" + count);
                Console.WriteLine("RETURN leaderboard " + count);
                Click(form, "menu_back");
                Console.WriteLine("RETURNED leaderboard " + count);
            }
        }
        service.EntryCount = 0;
    }

    private static async Task CheckFullLeaderboardPagingAsync()
    {
        Console.WriteLine("PAGING start");
        using PagedLeaderboardHandler handler = new();
        using SupabaseLeaderboardService service = new(new PlayerData(), handler);
        LeaderboardLoadResult result = await service.LoadAllAsync(CancellationToken.None);
        Check(result.Succeeded && result.Entries.Count == 503 && handler.Offsets.SequenceEqual([0, 250, 500]), "full leaderboard reads beyond server first page");
        Check(result.Entries[502].Nickname == "Player503", "paged leaderboard includes final row");
        handler.Invalid = true;
        Check(!(await service.LoadAllAsync(CancellationToken.None)).Succeeded, "full leaderboard rejects malformed response");
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        bool canceled = false;
        try
        {
            await service.LoadAllAsync(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }
        Check(canceled, "full leaderboard honors cancellation");
    }

    private sealed class PagedLeaderboardHandler : HttpMessageHandler
    {
        internal List<int> Offsets { get; } = [];
        internal bool Invalid { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string offsetPart = request.RequestUri!.Query.Split('&').Single(part => part.StartsWith("offset=", StringComparison.Ordinal));
            int offset = int.Parse(offsetPart[7..], System.Globalization.CultureInfo.InvariantCulture);
            Offsets.Add(offset);
            string json = Invalid ? "{}" : JsonSerializer.Serialize(Enumerable.Range(offset + 1, Math.Min(250, 503 - offset)).Select(index => new { nickname = "Player" + index, clear_time_ms = index * 1000 }));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }

    private static void CheckLetterAnimation()
    {
        using GameForm animated = new(new OfflineLeaderboard(), animationsEnabled: true, audioEnabled: false);
        animated.WindowState = FormWindowState.Normal;
        animated.ClientSize = new Size(1000, 600);
        animated.Show();
        PumpUntil(() => Get<bool>(animated, "_startupSequenceCompleted"), TimeSpan.FromSeconds(30));
        Set(animated, "_postal", PostalRoomProgress.CompletedLegacyRoom());
        Call(animated, "ShowMarieLetter");
        EnvelopeLetterAnimation reveal = (EnvelopeLetterAnimation)animated.Controls.Find("EnvelopeReveal", true).Single();
        float previous = 0;
        Stopwatch elapsed = Stopwatch.StartNew();
        for (int frame = 0; frame < 4; frame++)
        {
            PumpUntil(() => elapsed.ElapsedMilliseconds >= (frame + 1) * 230, TimeSpan.FromSeconds(3));
            Check(reveal.RevealProgress >= previous, "letter reveal progresses monotonically " + frame);
            previous = reveal.RevealProgress;
            Check(reveal.DocumentImage is not null, "animation uses actual document " + frame);
            Capture(animated, "refinement-letter-frame-" + frame);
        }
        PumpUntil(() => !reveal.Visible, TimeSpan.FromSeconds(5));
        Control letter = animated.Controls.Find("MarieLetterDocument", true).Single();
        Check(letter.Visible && Get<Dictionary<string, Button>>(animated, "_actions")["letter_decipher"].Visible, "letter animation ends with readable document and action");
        CheckVisibleText(animated, "animated letter completion");
        Capture(animated, "refinement-letter-complete");
        Call(animated, "ShowMarieLetter");
        Get<Button>(animated, "_roomButton").PerformClick();
        Pump();
        Check(Get<GameScreen>(animated, "_screen") == GameScreen.Desk, "return cancels letter animation safely");
        Set(animated, "_allowClose", true);
        animated.Close();
    }
}
