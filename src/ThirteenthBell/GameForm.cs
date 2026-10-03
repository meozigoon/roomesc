using System.Diagnostics;
using ThirteenthBell.Core;

namespace ThirteenthBell;

internal enum GameScreen
{
    Menu,
    Intro,
    Room,
    Desk,
    Letter,
    Lanterns,
    Melody,
    RibbonLoom,
    LetterAcrostic,
    ToyCipher,
    StarChart,
    Clock,
    Choice,
    Ending
}

internal sealed class GameForm : Form
{
    private const int BaseWidth = 1400;
    private const int BaseHeight = 820;
    private const string WorkshopImage = "workshop-desk-updated.png";
    private static readonly string[] SceneImagesToPreload =
    [
        "desk-closeup.png",
        "lanterns.png",
        "ribbon-loom.png",
        "snowglobe.png",
        "stocking-logic.png",
        "stocking-sprites.png"
    ];
    private static readonly TimeSpan NarrativeVisibleDuration = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan InitialNarrativeVisibleDuration = TimeSpan.FromSeconds(5);

    private readonly record struct LayoutSnapshot(Rectangle Bounds, Padding Padding, float FontSize, FontStyle FontStyle);

    private readonly ImageBank _images = new();
    private Panel _stage = null!;
    private SceneCanvas _menuScene = null!;
    private SceneCanvas _scene = null!;
    private readonly Panel _header;
    private readonly NarrativePanel _sidebar;
    private readonly MemoryStripPanel _inventory = new();
    private readonly Label _progressLabel;
    private readonly Label _timerLabel;
    private readonly Label _chapterLabel;
    private readonly Label _notebookText;
    private readonly Label _hintText;
    private readonly Label _inventoryTitle;
    private readonly Label _inventoryText;
    private readonly Button _hintButton;
    private readonly Button _soundButton;
    private readonly Button _roomButton;
    private readonly Panel _topHoverZone;
    private readonly System.Windows.Forms.Timer _elapsedTimer = new() { Interval = 1000 };
    private readonly Stopwatch _gameStopwatch = new();
    private readonly Stopwatch _screenStopwatch = new();
    private readonly SoundBank _sounds = new();
    private readonly BackgroundMusicPlayer _music;
    private readonly PlayerDataStore _playerDataStore;
    private readonly ProgressBackupStore _progressBackupStore;
    private readonly ILeaderboardService _leaderboardService;
    private readonly Dictionary<string, Button> _actions = new(StringComparer.Ordinal);
    private readonly Dictionary<GameScreen, int> _hintSteps = [];
    private readonly Dictionary<Control, LayoutSnapshot> _baseLayout = [];
    private readonly System.Windows.Forms.Timer _narrativeFadeTimer = new() { Interval = 40 };
    private readonly System.Windows.Forms.Timer _transitionTimer = new() { Interval = 16 };
    private readonly System.Windows.Forms.Timer _chromeHoverTimer = new() { Interval = 80 };
    private readonly System.Windows.Forms.Timer _startupFadeTimer = new() { Interval = 33 };
    private readonly bool _animationsEnabled;
    private GameState _state = new();
    private PlayerData _playerData;
    private CancellationTokenSource _sceneCancellation = new();
    private CancellationTokenSource _menuCancellation = new();
    private Panel? _confirmationOverlay;
    private FadeTransitionOverlay? _transitionOverlay;
    private StartupTitleOverlay? _startupTitleOverlay;
    private ProgressBackup? _pendingBackup;
    private GameScreen _screen;
    private string? _dataWarning;
    private TimeSpan _elapsedBeforeSession;
    private DateTimeOffset _lastBackupWriteUtc = DateTimeOffset.MinValue;
    private bool _soundEnabled = true;
    private bool _fullscreen = true;
    private bool _hintAvailable;
    private bool _gameInProgress;
    private bool _allowClose;
    private bool _resumePromptHandled;
    private int _treeEasterEggClicks;
    private bool _treeEasterEggFound;
    private float _currentScale = 1f;
    private DateTimeOffset _narrativeShownAt;
    private TimeSpan _narrativeVisibleDuration = NarrativeVisibleDuration;
    private bool _narrativePointerInside;
    private DateTimeOffset _transitionStartedAt;
    private DateTimeOffset _startupFadeStartedAt;
    private Action? _firstRunContinuation;
    private bool _gameChromeVisible;
    private bool _startupSequenceCompleted;
    private bool _shutdownPrepared;
    private bool _resourcesDisposed;
    private bool _inventoryExpanded;
    private StockingPiece[]? _stockingPieces;
    private StockingHookSlot[]? _stockingSlots;
    private int[]? _stockingPlacement;
    private int[]? _stockingPieceSlots;
    private string? _lastFailedStockingOrder;

    public GameForm(ILeaderboardService? leaderboardService = null, bool animationsEnabled = true, bool audioEnabled = true)
    {
        InitializeComponent();
        _animationsEnabled = animationsEnabled;
        _music = new BackgroundMusicPlayer(audioEnabled);

        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app-icon.ico");
        if (File.Exists(iconPath))
        {
            Icon = new Icon(iconPath);
        }

        _header = BuildHeader();
        _stage.Controls.Add(_header);

        _sidebar = BuildSidebar();

        _inventory.Bounds = new Rectangle(30, 78, 300, 58);
        _inventory.BackColor = Color.Transparent;
        _inventory.Padding = new Padding(16, 8, 16, 8);

        _scene.MouseClick += HandleSceneClick;
        _stage.Controls.Add(_scene);
        _scene.Controls.Add(_inventory);
        _scene.Controls.Add(_sidebar);

        _menuScene.Bounds = new Rectangle(0, 0, BaseWidth, BaseHeight);
        _menuScene.AccessibleName = "메인 메뉴";
        _stage.Controls.Add(_menuScene);

        _progressLabel = Theme.CreateLabel(string.Empty, 10.5f, FontStyle.Bold);
        _progressLabel.Name = "ProgressLabel";
        _progressLabel.Bounds = new Rectangle(1020, 8, 245, 46);
        _progressLabel.TextAlign = ContentAlignment.MiddleRight;
        _progressLabel.BackColor = Color.Transparent;
        _scene.Controls.Add(_progressLabel);

        _timerLabel = Theme.CreateLabel("시간 00:00", 10.5f, FontStyle.Bold);
        _timerLabel.Name = "TimerLabel";
        _timerLabel.Bounds = new Rectangle(1260, 8, 120, 46);
        _timerLabel.TextAlign = ContentAlignment.MiddleRight;
        _timerLabel.BackColor = Color.Transparent;
        _scene.Controls.Add(_timerLabel);

        _topHoverZone = new Panel
        {
            Name = "TopHoverZone",
            Bounds = new Rectangle(0, 0, BaseWidth, 22),
            BackColor = Color.Transparent,
            AccessibleName = "상단 메뉴 열기 영역"
        };
        _topHoverZone.MouseEnter += (_, _) => SetHeaderRevealed(true);
        _scene.Controls.Add(_topHoverZone);

        _chapterLabel = (Label)_sidebar.Controls.Find("ChapterLabel", false)[0];
        _notebookText = (Label)_sidebar.Controls.Find("NotebookText", false)[0];
        _hintText = (Label)_sidebar.Controls.Find("HintText", false)[0];
        _hintButton = (Button)_sidebar.Controls.Find("HintButton", false)[0];
        _soundButton = (Button)_header.Controls.Find("SoundButton", false)[0];
        _roomButton = (Button)_header.Controls.Find("RoomButton", false)[0];
        (_inventoryTitle, _inventoryText) = BuildInventoryContents();
        _inventory.Click += (_, _) => ToggleInventoryExpanded();
        _inventoryTitle.Click += (_, _) => ToggleInventoryExpanded();
        _inventoryText.Click += (_, _) => ToggleInventoryExpanded();

        _playerDataStore = PlayerDataStore.CreateDefault();
        if (!_playerDataStore.TryLoad(out _playerData, out _dataWarning))
        {
            _playerData = new PlayerData();
        }

        _progressBackupStore = new ProgressBackupStore(_playerDataStore.DataDirectory);
        if (!_progressBackupStore.TryLoad(out _pendingBackup, out string? backupError))
        {
            AppendDataWarning(backupError);
            _progressBackupStore.TryDiscard(out _);
        }

        _leaderboardService = leaderboardService ?? new SupabaseLeaderboardService(_playerData);

        _elapsedTimer.Tick += (_, _) =>
        {
            UpdateHeader();
            UpdateHintAvailability();
            TryPeriodicProgressBackup();
        };
        _narrativeFadeTimer.Tick += HandleNarrativeFadeTick;
        _transitionTimer.Tick += HandleTransitionTick;
        _chromeHoverTimer.Tick += (_, _) => UpdateHeaderRevealFromPointer();
        _startupFadeTimer.Tick += HandleStartupFadeTick;
        FormClosing += HandleFormClosing;
        FormClosed += (_, _) => PrepareForShutdown();
        Resize += (_, _) => ApplyResponsiveLayout();
        Shown += async (_, _) => await RunStartupSequenceAsync();
        ShowStartupTitle();
    }

    private void InitializeComponent()
    {
        _stage = new Panel();
        _menuScene = new SceneCanvas();
        _scene = new SceneCanvas();
        SuspendLayout();

        _stage.Name = "Stage";
        _stage.Size = new Size(BaseWidth, BaseHeight);
        _stage.BackColor = Theme.Night;
        _stage.AccessibleName = "게임 화면";

        _scene.Name = "GameScene";
        _scene.Bounds = new Rectangle(0, 0, BaseWidth, BaseHeight);
        _scene.AccessibleName = "게임 장면";

        _menuScene.Name = "MenuScene";
        _menuScene.Bounds = new Rectangle(0, 0, BaseWidth, BaseHeight);
        _menuScene.AccessibleName = "메인 메뉴";

        AutoScaleMode = AutoScaleMode.None;
        BackColor = Theme.Night;
        ClientSize = new Size(BaseWidth, BaseHeight);
        Controls.Add(_stage);
        DoubleBuffered = true;
        Font = Theme.Font(10);
        ForeColor = Theme.Snow;
        FormBorderStyle = FormBorderStyle.None;
        KeyPreview = true;
        Name = "GameForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "13번째 종: 잊힌 선물";
        WindowState = FormWindowState.Maximized;
        AccessibleName = "13번째 종 크리스마스 방탈출 게임";
        ResumeLayout(false);
    }

    private Panel BuildHeader()
    {
        Panel header = new()
        {
            Name = "TopToolbar",
            Bounds = new Rectangle(24, 8, 890, 62),
            BackColor = Theme.Night,
            Padding = new Padding(14, 7, 14, 7),
            BorderStyle = BorderStyle.FixedSingle,
            Visible = false,
            AccessibleName = "상단 게임 메뉴"
        };

        WallClockLogo logo = new()
        {
            Name = "HeaderLogo",
            Bounds = new Rectangle(12, 3, 56, 54),
            LogoImage = _images["wall-clock-logo.png"]
        };
        header.Controls.Add(logo);

        Button sound = Theme.CreateButton("소리 켬", (_, _) => ToggleSound(), 90);
        sound.Name = "SoundButton";
        sound.Bounds = new Rectangle(86, 8, 145, 44);
        sound.AccessibleDescription = "효과음을 켜거나 끕니다";
        header.Controls.Add(sound);

        Button room = Theme.CreateButton("공방으로", (_, _) => ShowRoom(), 91);
        room.Name = "RoomButton";
        room.Bounds = new Rectangle(247, 8, 180, 44);
        room.Visible = false;
        room.AccessibleDescription = "현재 퍼즐을 나가 공방으로 돌아갑니다";
        header.Controls.Add(room);

        Button restart = Theme.CreateButton("새 게임", (_, _) => RequestNewGame(), 92);
        restart.Bounds = new Rectangle(443, 8, 170, 44);
        restart.AccessibleDescription = "처음부터 새 게임을 시작합니다. 단축키 Ctrl+N";
        header.Controls.Add(restart);

        Button menu = Theme.CreateButton("메뉴", (_, _) => RequestReturnToMainMenu(), 93);
        menu.Name = "MenuButton";
        menu.Bounds = new Rectangle(629, 8, 235, 44);
        menu.AccessibleDescription = "메인 메뉴로 돌아갑니다";
        header.Controls.Add(menu);
        return header;
    }

    private NarrativePanel BuildSidebar()
    {
        NarrativePanel sidebar = new()
        {
            Name = "NarrativePanel",
            Bounds = new Rectangle(70, 535, 1260, 195),
            Padding = new Padding(28, 16, 28, 18),
            VisualStyle = NarrativeVisualStyle.Narration
        };

        Label chapter = Theme.CreateLabel(string.Empty, 12.5f, FontStyle.Bold);
        chapter.Name = "ChapterLabel";
        chapter.Bounds = new Rectangle(34, 17, 760, 36);
        chapter.ForeColor = Theme.Gold;
        sidebar.Controls.Add(chapter);

        Label notebook = Theme.CreateLabel(string.Empty, 10.5f);
        notebook.Name = "NotebookText";
        notebook.Bounds = new Rectangle(34, 55, 770, 112);
        notebook.TextAlign = ContentAlignment.MiddleLeft;
        sidebar.Controls.Add(notebook);

        Label hint = Theme.CreateLabel("힌트는 현재 화면을 3분 30초 동안 살펴본 뒤 열립니다.", 9.5f);
        hint.Name = "HintText";
        hint.Bounds = new Rectangle(815, 22, 280, 148);
        hint.ForeColor = Theme.PaleGold;
        hint.TextAlign = ContentAlignment.MiddleLeft;
        sidebar.Controls.Add(hint);

        Button hintButton = Theme.CreateButton("힌트 (F1)", (_, _) => ShowHint(), 80);
        hintButton.Name = "HintButton";
        hintButton.Bounds = new Rectangle(1105, 67, 130, 58);
        hintButton.AccessibleDescription = "현재 퍼즐의 단계별 힌트를 표시합니다";
        hintButton.Visible = false;
        hintButton.Enabled = false;
        sidebar.Controls.Add(hintButton);
        return sidebar;
    }

    private (Label Title, Label Contents) BuildInventoryContents()
    {
        Label title = Theme.CreateLabel("획득한 기억, 눌러서 보기", 10.5f, FontStyle.Bold);
        title.Name = "InventoryTitle";
        title.Bounds = new Rectangle(18, 8, 250, 42);
        title.ForeColor = Theme.Gold;
        title.TextAlign = ContentAlignment.MiddleLeft;
        title.Cursor = Cursors.Hand;
        _inventory.Controls.Add(title);

        Label contents = Theme.CreateLabel(string.Empty, 9.5f);
        contents.Name = "InventoryText";
        contents.Bounds = new Rectangle(18, 48, 1000, 58);
        contents.TextAlign = ContentAlignment.MiddleLeft;
        contents.Cursor = Cursors.Hand;
        contents.Visible = false;
        _inventory.Controls.Add(contents);
        _inventory.Cursor = Cursors.Hand;
        _inventory.AccessibleName = "획득한 기억, 눌러서 펼치거나 접기";
        return (title, contents);
    }

    private void ShowStartupTitle()
    {
        ShowMainMenu(animateTransition: false);
        SetStartupMenuControlsVisible(false);

        StartupTitleOverlay overlay = new()
        {
            Name = "StartupTitle",
            Bounds = new Rectangle(0, 0, BaseWidth, BaseHeight),
            FadeProgress = 0f
        };
        _startupTitleOverlay = overlay;
        _menuScene.Controls.Add(overlay);
        overlay.BringToFront();
    }

    private void SetStartupMenuControlsVisible(bool visible)
    {
        foreach (Control control in _menuScene.Controls)
        {
            if (!ReferenceEquals(control, _startupTitleOverlay))
            {
                control.Visible = visible;
            }
        }
    }

    private async Task RunStartupSequenceAsync()
    {
        ApplyResponsiveLayout();
        try
        {
            await Task.WhenAll(
                Task.Delay(650),
                Task.Run(() => _images.Preload(SceneImagesToPreload)));
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or ObjectDisposedException)
        {
            ErrorReporter.Report(exception, "Preloading scene images", false);
        }

        if (IsDisposed || Disposing || _shutdownPrepared)
        {
            return;
        }

        _startupTitleOverlay?.BringToFront();
        if (_animationsEnabled)
        {
            _startupFadeStartedAt = DateTimeOffset.UtcNow;
            _startupFadeTimer.Start();
        }
        else
        {
            CompleteStartupFade();
        }
    }

    private void HandleStartupFadeTick(object? sender, EventArgs eventArgs)
    {
        if (_startupTitleOverlay is null)
        {
            _startupFadeTimer.Stop();
            return;
        }

        float progress = (float)((DateTimeOffset.UtcNow - _startupFadeStartedAt).TotalMilliseconds / 1150d);
        _startupTitleOverlay.FadeProgress = Math.Clamp(progress, 0f, 1f);
        if (progress >= 0.72f)
        {
            SetStartupMenuControlsVisible(true);
            _startupTitleOverlay.BringToFront();
        }

        if (progress >= 1f)
        {
            CompleteStartupFade();
        }
    }

    private void CompleteStartupFade()
    {
        _startupFadeTimer.Stop();
        SetStartupMenuControlsVisible(true);
        StartupTitleOverlay? overlay = _startupTitleOverlay;
        _startupTitleOverlay = null;
        if (overlay is not null)
        {
            overlay.Parent?.Controls.Remove(overlay);
            overlay.Dispose();
        }

        _startupSequenceCompleted = true;
        _music.Play(BackgroundMusicKind.Menu);
        ShowResumePromptIfAvailable();
    }

    private void StartGameFromMainMenu()
    {
        if (_playerData.TutorialSeen)
        {
            ShowNicknameSetup();
            return;
        }

        _firstRunContinuation = ShowNicknameSetup;
        ShowFirstRunGuide();
    }

    private void ShowMainMenu(bool animateTransition = true)
    {
        if (_startupSequenceCompleted)
        {
            _music.Play(BackgroundMusicKind.Menu);
        }

        Bitmap? previousFrame = animateTransition ? CaptureTransitionFrame() : null;
        CloseConfirmationOverlay();
        CancelSceneWork();
        CancelMenuWork();
        _elapsedTimer.Stop();
        _gameStopwatch.Stop();
        _screenStopwatch.Reset();
        _screen = GameScreen.Menu;
        _hintAvailable = false;
        _hintButton.Visible = false;
        _hintButton.Enabled = false;
        _actions.Clear();
        DisposeChildren(_menuScene);
        SetGameChromeVisible(false);
        _menuScene.Visible = true;
        _menuScene.BringToFront();
        _menuScene.SceneImage = _images[WorkshopImage];

        WallClockLogo logo = new()
        {
            Name = "MainClockLogo",
            Bounds = new Rectangle(555, 20, 290, 320),
            LogoImage = _images["wall-clock-logo.png"]
        };
        _menuScene.Controls.Add(logo);

        AddMenuAction("start", "게임 시작", new Rectangle(205, 375, 400, 66), (_, _) => StartGameFromMainMenu(), 1);
        AddMenuAction("menu_guide", "게임 방법", new Rectangle(205, 455, 400, 66), (_, _) => ShowGuideMenu(), 2);
        AddMenuAction("menu_credits", "제작자 보기", new Rectangle(205, 535, 400, 66), (_, _) => ShowCreditsMenu(), 3);
        AddMenuAction("menu_exit", "게임 종료", new Rectangle(205, 615, 400, 66), (_, _) => Close(), 4);

        Label leaderboard = CreateCard("온라인 순위", "불러오는 중...");
        leaderboard.Name = "LeaderboardText";
        leaderboard.Bounds = new Rectangle(705, 375, 500, 306);
        leaderboard.Font = Theme.Font(12, FontStyle.Regular);
        leaderboard.TextAlign = ContentAlignment.TopCenter;
        _menuScene.Controls.Add(leaderboard);
        _ = LoadLeaderboardAsync(leaderboard, _menuCancellation.Token);

        UpdateWindowCloseAvailability();
        RefreshResponsiveLayout();
        BeginScreenTransition(previousFrame);
    }

    private void ShowGuideMenu()
    {
        CancelMenuWork();
        ShowMenuPage(
            "게임 방법",
            "게임 시작 시 중복되지 않는 온라인 닉네임을 설정합니다.\n공방과 책상의 단서를 풀면 마지막 별시계가 열립니다.\n클리어하면 서버 시각 기준 최고 기록과 등수가 저장됩니다.\n\n마우스 또는 Tab과 Enter: 배경 사물 조사와 입력\n마우스 끌기: 양말을 고리에 직접 걸기\nF1: 같은 화면에서 3분 30초 뒤 단계별 힌트\nEsc: 책상이나 퍼즐에서 공방으로 복귀\nCtrl+N: 경고 확인 뒤 새 게임\nF11: 전체 화면과 창 모드 전환\n\n게임 중 메뉴 이동과 종료에는 경고가 표시됩니다. 진행은 자동 백업되며, 다음 실행의 선택 화면에서 한 번만 복구할 수 있습니다.",
            12f);
    }

    private void ShowFirstRunGuide()
    {
        Bitmap? previousFrame = CaptureTransitionFrame();
        CancelMenuWork();
        _actions.Clear();
        DisposeChildren(_menuScene);
        _menuScene.SceneImage = _images[WorkshopImage];

        string warning = string.IsNullOrWhiteSpace(_dataWarning)
            ? string.Empty
            : $"\n\n저장 안내: {_dataWarning}";
        Label card = CreateCard(
            "처음 오셨군요",
            "게임 시작 시 고유한 온라인 닉네임을 정합니다.\n클리어 기록과 등수는 메인 메뉴에 표시됩니다.\n\n마우스 또는 Tab과 Enter로 조사합니다.\n힌트는 같은 화면을 3분 30초 살핀 뒤 나타나고, 그전에는 F1도 작동하지 않습니다.\nEsc는 퍼즐에서 공방으로 돌아가며, F11은 화면 모드를 바꿉니다.\n메뉴 이동과 종료 전에는 경고가 표시됩니다. 진행은 자동 백업되며, 다음 실행에서 불러오기를 선택한 경우 한 번만 복구됩니다." + warning);
        card.Bounds = new Rectangle(275, 90, 850, 535);
        card.Font = Theme.Font(12, FontStyle.Regular);
        _menuScene.Controls.Add(card);
        string continueText = _firstRunContinuation is null ? "확인하고 메인 메뉴로" : "확인하고 닉네임 설정으로";
        AddMenuAction("tutorial_continue", continueText, new Rectangle(500, 660, 400, 70), (_, _) => CompleteFirstRunGuide(), 1);
        RefreshResponsiveLayout();
        BeginScreenTransition(previousFrame);
    }

    private void CompleteFirstRunGuide()
    {
        _playerData.TutorialSeen = true;
        if (!_playerDataStore.TrySave(_playerData, out string? error))
        {
            _dataWarning = error;
        }
        else
        {
            _dataWarning = null;
        }

        Action? continuation = _firstRunContinuation;
        _firstRunContinuation = null;
        if (continuation is null)
        {
            ShowMainMenu();
            ShowResumePromptIfAvailable();
        }
        else
        {
            continuation();
        }
    }

    private void ShowResumePromptIfAvailable()
    {
        if (_resumePromptHandled || _pendingBackup is null)
        {
            return;
        }

        _music.Play(BackgroundMusicKind.Menu);
        Bitmap? previousFrame = CaptureTransitionFrame();
        _resumePromptHandled = true;
        ProgressBackup backup = _pendingBackup;
        CancelMenuWork();
        _actions.Clear();
        DisposeChildren(_menuScene);
        SetGameChromeVisible(false);
        _menuScene.Visible = true;
        _menuScene.BringToFront();
        _menuScene.SceneImage = _images[WorkshopImage];

        string savedTime = backup.SavedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.CurrentCulture);
        Label card = CreateCard(
            "중단된 진행을 발견했습니다",
            $"플레이어: {backup.Nickname}\n완료한 기억 조각: {backup.SolvedPuzzles.Count}/{GameState.RequiredPuzzleCount}\n기록된 진행 시간: {FormatElapsed(TimeSpan.FromMilliseconds(backup.ElapsedMilliseconds))}\n백업 시각: {savedTime}\n\n이 백업은 한 번만 불러올 수 있습니다. 불러오기를 선택하면 즉시 소비되며, 불러오지 않으면 영구 삭제됩니다.");
        card.Name = "ResumePrompt";
        card.Bounds = new Rectangle(275, 105, 850, 430);
        card.Font = Theme.Font(15, FontStyle.Regular);
        _menuScene.Controls.Add(card);

        Label status = Theme.CreateLabel("불러올지 선택하세요.", 10.5f);
        status.Name = "ResumeStatus";
        status.Bounds = new Rectangle(350, 555, 700, 42);
        status.TextAlign = ContentAlignment.MiddleCenter;
        status.ForeColor = Theme.PaleGold;
        _menuScene.Controls.Add(status);

        AddMenuAction("resume_load", "한 번 불러오기", new Rectangle(350, 625, 320, 70), (_, _) => LoadPendingProgress(status), 1);
        AddMenuAction("resume_discard", "불러오지 않고 삭제", new Rectangle(730, 625, 320, 70), (_, _) => DiscardPendingProgress(status), 2);
        RefreshResponsiveLayout();
        BeginScreenTransition(previousFrame);
    }

    private void LoadPendingProgress(Label status)
    {
        if (!_progressBackupStore.TryConsume(out ProgressBackup? backup, out string? error) || backup is null)
        {
            status.Text = error ?? "진행 백업을 불러오지 못했습니다.";
            status.ForeColor = Color.LightSalmon;
            return;
        }

        _pendingBackup = null;
        try
        {
            RestoreProgress(backup);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            AppendDataWarning($"진행 백업을 복원하지 못했습니다: {exception.Message}");
            ShowMainMenu();
        }
    }

    private void DiscardPendingProgress(Label status)
    {
        if (!_progressBackupStore.TryDiscard(out string? error))
        {
            status.Text = error ?? "진행 백업을 삭제하지 못했습니다.";
            status.ForeColor = Color.LightSalmon;
            return;
        }

        _pendingBackup = null;
        ShowMainMenu();
    }

    private void RestoreProgress(ProgressBackup backup)
    {
        _state = GameState.Restore(backup.SolvedPuzzles, backup.HintCount, backup.FailedAttempts, backup.ClockRestored);
        _inventoryExpanded = false;
        _playerData.Nickname = backup.Nickname;
        _elapsedBeforeSession = TimeSpan.FromMilliseconds(backup.ElapsedMilliseconds);
        _gameStopwatch.Restart();
        _hintSteps.Clear();
        _treeEasterEggClicks = 0;
        _treeEasterEggFound = false;
        _gameInProgress = true;
        _lastBackupWriteUtc = DateTimeOffset.UtcNow;
        _elapsedTimer.Start();
        UpdateWindowCloseAvailability();

        GameScreen restoredScreen = Enum.TryParse(backup.Screen, ignoreCase: false, out GameScreen parsed)
            ? parsed
            : GameScreen.Room;
        switch (restoredScreen)
        {
            case GameScreen.Intro:
                ShowIntro();
                break;
            case GameScreen.Lanterns:
                ShowLanternPuzzle();
                break;
            case GameScreen.Melody:
                ShowSnowglobePuzzle();
                break;
            case GameScreen.RibbonLoom:
                ShowStockingPuzzle();
                break;
            case GameScreen.Desk:
            case GameScreen.Letter:
                ShowDesk();
                break;
            case GameScreen.LetterAcrostic:
                ShowLetterAcrosticPuzzle();
                break;
            case GameScreen.ToyCipher:
                ShowToyCipherPuzzle();
                break;
            case GameScreen.StarChart:
                ShowStarChartPuzzle();
                break;
            case GameScreen.Clock when _state.CanOpenClock:
                ShowClockPuzzle();
                break;
            case GameScreen.Choice when _state.ClockRestored:
                ShowChoice();
                break;
            default:
                ShowRoom();
                break;
        }
    }

    private void ShowCreditsMenu()
    {
        CancelMenuWork();
        ShowMenuPage(
            "제작자 보기",
            "기획 및 방향 제시: 사용자\n스토리, 퍼즐 설계, C# WinForms 구현, QA: Codex\n배경 원화: 이 프로젝트를 위해 신규 제작\n\n게임 글꼴: Noto Serif KR\nGoogle, Adobe 공동 개발\nSIL Open Font License 1.1");
    }

    private void ShowMenuPage(string heading, string body, float fontSize = 16f)
    {
        _music.Play(BackgroundMusicKind.Menu);
        Bitmap? previousFrame = CaptureTransitionFrame();
        _actions.Clear();
        DisposeChildren(_menuScene);
        _menuScene.SceneImage = _images[WorkshopImage];

        Label card = CreateCard(heading, body);
        card.Bounds = new Rectangle(300, 115, 800, 480);
        card.Font = Theme.Font(fontSize, FontStyle.Regular);
        _menuScene.Controls.Add(card);
        AddMenuAction("menu_back", "메인 메뉴로 돌아가기", new Rectangle(500, 635, 400, 70), (_, _) => ShowMainMenu(), 1);
        RefreshResponsiveLayout();
        BeginScreenTransition(previousFrame);
    }

    private async Task LoadLeaderboardAsync(Label leaderboard, CancellationToken cancellationToken)
    {
        try
        {
            LeaderboardLoadResult result = await _leaderboardService.LoadAsync(cancellationToken).ConfigureAwait(true);
            if (cancellationToken.IsCancellationRequested || leaderboard.IsDisposed)
            {
                return;
            }

            if (!result.Succeeded)
            {
                leaderboard.Text = $"온라인 순위\n\n{result.Error}\n\n인터넷 연결을 확인한 뒤\n메인 메뉴로 다시 들어오세요.";
                return;
            }

            if (result.Entries.Count == 0)
            {
                leaderboard.Text = "온라인 순위\n\n아직 등록된 클리어 기록이 없습니다.\n첫 번째 기록의 주인공이 되어 보세요.";
                return;
            }

            IEnumerable<string> lines = result.Entries
                .Take(7)
                .Select((entry, index) => $"{index + 1}위   {entry.Nickname}   {FormatOnlineTime(entry.ClearTimeMilliseconds)}");
            leaderboard.Text = $"온라인 순위\n\n{string.Join("\n", lines)}";
        }
        catch (OperationCanceledException)
        {
            // Leaving the menu intentionally cancels its network request.
        }
        catch (Exception exception)
        {
            if (!leaderboard.IsDisposed)
            {
                leaderboard.Text = "온라인 순위\n\n순위를 표시하는 중 문제가 발생했습니다.";
            }

            ErrorReporter.Report(exception, "Loading online leaderboard", false);
        }
    }

    private void ShowNicknameSetup()
    {
        _music.Play(BackgroundMusicKind.Menu);
        Bitmap? previousFrame = CaptureTransitionFrame();
        CancelSceneWork();
        CancelMenuWork();
        _elapsedTimer.Stop();
        _gameStopwatch.Stop();
        _screenStopwatch.Reset();
        _screen = GameScreen.Menu;
        _hintAvailable = false;
        _hintButton.Visible = false;
        _hintButton.Enabled = false;
        _actions.Clear();
        DisposeChildren(_menuScene);
        SetGameChromeVisible(false);
        _menuScene.Visible = true;
        _menuScene.BringToFront();
        _menuScene.SceneImage = _images[WorkshopImage];

        Label card = CreateCard(
            "온라인 닉네임 설정",
            "2자 이상 16자 이하의 닉네임을 입력하세요.\n대소문자와 연속 공백만 다른 닉네임도 동일하게 처리됩니다.\n다른 사용자가 이미 등록한 닉네임은 사용할 수 없습니다.");
        card.Bounds = new Rectangle(300, 85, 800, 285);
        card.Font = Theme.Font(15, FontStyle.Regular);
        _menuScene.Controls.Add(card);

        TextBox editor = new()
        {
            Name = "NicknameEditor",
            Bounds = new Rectangle(430, 400, 540, 62),
            Font = Theme.Font(18, FontStyle.Bold),
            BackColor = Theme.Night,
            ForeColor = Theme.Snow,
            BorderStyle = BorderStyle.FixedSingle,
            MaxLength = NicknameRules.MaximumLength,
            Text = _playerData.Nickname,
            TextAlign = HorizontalAlignment.Center,
            AccessibleName = "온라인 닉네임 입력란",
            TabIndex = 1
        };
        _menuScene.Controls.Add(editor);

        Label status = Theme.CreateLabel("닉네임은 온라인에서 중복 여부를 확인합니다.", 10.5f);
        status.Name = "NicknameStatus";
        status.Bounds = new Rectangle(350, 475, 700, 44);
        status.TextAlign = ContentAlignment.MiddleCenter;
        status.ForeColor = Theme.PaleGold;
        _menuScene.Controls.Add(status);

        Button confirm = AddMenuAction("nickname_confirm", "닉네임 확인 후 시작", new Rectangle(365, 535, 320, 66), (_, _) => { }, 2);
        confirm.Click += (_, _) => _ = ConfirmNicknameAsync(editor, status, confirm, _menuCancellation.Token);
        AddMenuAction("nickname_back", "메인 메뉴로", new Rectangle(715, 535, 320, 66), (_, _) => ShowMainMenu(), 3);
        editor.KeyDown += (_, eventArgs) =>
        {
            if (eventArgs.KeyCode == Keys.Enter && confirm.Enabled)
            {
                eventArgs.SuppressKeyPress = true;
                confirm.PerformClick();
            }
        };

        Label privacy = Theme.CreateLabel("닉네임과 클리어 시간만 공개 순위표에 표시됩니다.", 9.5f);
        privacy.Bounds = new Rectangle(350, 635, 700, 36);
        privacy.TextAlign = ContentAlignment.MiddleCenter;
        privacy.ForeColor = Theme.PaleGold;
        _menuScene.Controls.Add(privacy);
        RefreshResponsiveLayout();
        editor.SelectAll();
        editor.Focus();
        BeginScreenTransition(previousFrame);
    }

    private async Task ConfirmNicknameAsync(TextBox editor, Label status, Button confirm, CancellationToken cancellationToken)
    {
        if (!NicknameRules.TryNormalize(editor.Text, out string normalized, out string? validationError))
        {
            status.Text = validationError;
            status.ForeColor = Color.LightSalmon;
            return;
        }

        confirm.Enabled = false;
        editor.Enabled = false;
        status.ForeColor = Theme.PaleGold;
        status.Text = "온라인에서 닉네임 중복 여부를 확인하는 중입니다...";
        try
        {
            NicknameReservationResult result = await _leaderboardService
                .ReserveNicknameAsync(normalized, cancellationToken)
                .ConfigureAwait(true);
            if (cancellationToken.IsCancellationRequested || status.IsDisposed)
            {
                return;
            }

            if (result.Status != NicknameReservationStatus.Reserved || string.IsNullOrWhiteSpace(result.Nickname))
            {
                status.Text = result.Error ?? "닉네임을 사용할 수 없습니다.";
                status.ForeColor = Color.LightSalmon;
                return;
            }

            _playerData.Nickname = result.Nickname;
            if (!_playerDataStore.TrySave(_playerData, out string? saveError))
            {
                _dataWarning = saveError;
                ErrorReporter.Report(new IOException(saveError), "Saving online player identity", false);
            }

            StartNewGame();
        }
        catch (OperationCanceledException)
        {
            // Leaving this screen intentionally cancels nickname verification.
        }
        catch (Exception exception)
        {
            if (!status.IsDisposed)
            {
                status.Text = "닉네임 확인 중 문제가 발생했습니다. 다시 시도하세요.";
                status.ForeColor = Color.LightSalmon;
            }

            ErrorReporter.Report(exception, "Reserving online nickname", false);
        }
        finally
        {
            if (!confirm.IsDisposed)
            {
                confirm.Enabled = true;
            }

            if (!editor.IsDisposed)
            {
                editor.Enabled = true;
            }
        }
    }

    private static string FormatOnlineTime(long milliseconds)
    {
        TimeSpan elapsed = TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
        return $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}.{elapsed.Milliseconds:000}";
    }

    private void RequestReturnToMainMenu()
    {
        if (!_gameInProgress)
        {
            ShowMainMenu();
            return;
        }

        ShowConfirmationOverlay(
            "메인 메뉴로 나가기",
            "현재 게임 진행을 포기하고 메인 메뉴로 이동합니다.\n완료하지 않은 진행 백업도 함께 삭제되며 되돌릴 수 없습니다.",
            "진행 포기하고 나가기",
            () => AbandonCurrentProgress(() => ShowMainMenu()));
    }

    private void RequestNewGame()
    {
        if (!_gameInProgress)
        {
            ShowNicknameSetup();
            return;
        }

        ShowConfirmationOverlay(
            "새 게임 시작",
            "현재 게임 진행을 포기하고 새 닉네임 설정으로 이동합니다.\n완료하지 않은 진행 백업도 함께 삭제되며 되돌릴 수 없습니다.",
            "진행 포기하고 새 게임",
            () => AbandonCurrentProgress(ShowNicknameSetup));
    }

    private void AbandonCurrentProgress(Action nextScreen)
    {
        if (!_progressBackupStore.TryDiscard(out string? error))
        {
            ShowConfirmationError(error ?? "진행 백업을 삭제하지 못했습니다.");
            return;
        }

        CloseConfirmationOverlay();
        _pendingBackup = null;
        _gameInProgress = false;
        _elapsedTimer.Stop();
        _gameStopwatch.Stop();
        _elapsedBeforeSession = TimeSpan.Zero;
        ClearSessionMemo();
        UpdateWindowCloseAvailability();
        nextScreen();
    }

    private void ShowConfirmationOverlay(
        string title,
        string body,
        string confirmText,
        Action onConfirm,
        string? alternateText = null,
        Action? onAlternate = null)
    {
        if (_confirmationOverlay is not null)
        {
            return;
        }

        _header.Enabled = false;
        _sidebar.Enabled = false;
        _inventory.Enabled = false;
        _scene.Enabled = false;
        _menuScene.Enabled = false;

        Panel overlay = new()
        {
            Name = "ConfirmationOverlay",
            Bounds = new Rectangle(0, 0, BaseWidth, BaseHeight),
            BackColor = Theme.Night,
            AccessibleName = title
        };
        _confirmationOverlay = overlay;
        _stage.Controls.Add(overlay);
        overlay.BringToFront();

        AtmosphereCard card = CreateAtmosphereCard(
            title,
            body,
            AtmosphereCardStyle.Letter,
            new Rectangle(260, 125, 880, 430),
            new Rectangle(60, 48, 760, 60),
            new Rectangle(80, 135, 720, 165),
            20,
            14);
        card.Name = "ConfirmationCard";
        overlay.Controls.Add(card);

        Label errorLabel = Theme.CreateLabel(string.Empty, 10.5f);
        errorLabel.Name = "ConfirmationError";
        errorLabel.Bounds = new Rectangle(60, 340, 760, 45);
        errorLabel.ForeColor = Color.LightSalmon;
        errorLabel.TextAlign = ContentAlignment.MiddleCenter;
        card.Controls.Add(errorLabel);

        if (alternateText is not null && onAlternate is not null)
        {
            AddActionTo(overlay, "confirmation_accept", confirmText, new Rectangle(155, 610, 330, 72), (_, _) => onConfirm(), 1);
            AddActionTo(overlay, "confirmation_alternate", alternateText, new Rectangle(535, 610, 330, 72), (_, _) => onAlternate(), 2);
            AddActionTo(overlay, "confirmation_cancel", "계속 플레이", new Rectangle(915, 610, 330, 72), (_, _) => CloseConfirmationOverlay(), 3);
        }
        else
        {
            AddActionTo(overlay, "confirmation_accept", confirmText, new Rectangle(330, 610, 350, 72), (_, _) => onConfirm(), 1);
            AddActionTo(overlay, "confirmation_cancel", "계속 플레이", new Rectangle(720, 610, 350, 72), (_, _) => CloseConfirmationOverlay(), 2);
        }
        ApplyResponsiveLayout();
        overlay.BringToFront();
    }

    private void ShowConfirmationError(string message)
    {
        if (_confirmationOverlay?.Controls.Find("ConfirmationError", true).FirstOrDefault() is Label label)
        {
            label.Text = message;
        }
    }

    private void CloseConfirmationOverlay()
    {
        Panel? overlay = _confirmationOverlay;
        if (overlay is null)
        {
            return;
        }

        _actions.Remove("confirmation_accept");
        _actions.Remove("confirmation_alternate");
        _actions.Remove("confirmation_cancel");
        _confirmationOverlay = null;
        _stage.Controls.Remove(overlay);
        overlay.Dispose();
        _header.Enabled = true;
        _sidebar.Enabled = true;
        _inventory.Enabled = true;
        _scene.Enabled = true;
        _menuScene.Enabled = true;
        ApplyResponsiveLayout();
    }

    private void HandleFormClosing(object? sender, FormClosingEventArgs eventArgs)
    {
        if (!_gameInProgress || _allowClose)
        {
            return;
        }

        if (eventArgs.CloseReason is CloseReason.WindowsShutDown or CloseReason.TaskManagerClosing)
        {
            SaveProgressBackup(reportFailure: false);
            _allowClose = true;
            return;
        }

        eventArgs.Cancel = true;
        ShowConfirmationOverlay(
            "게임 종료",
            "게임이 아직 끝나지 않았습니다.\n백업 후 종료하면 다음 실행에서 한 번만 불러올 수 있습니다. 백업 없이 종료하면 현재 진행 기록을 삭제합니다.",
            "백업 후 종료",
            ConfirmCloseWithBackup,
            "백업 없이 종료",
            ConfirmCloseWithoutBackup);
    }

    private void ConfirmCloseWithBackup()
    {
        if (!SaveProgressBackup(reportFailure: true))
        {
            return;
        }

        CloseConfirmationOverlay();
        _allowClose = true;
        Close();
    }

    private void ConfirmCloseWithoutBackup()
    {
        if (!_progressBackupStore.TryDiscard(out string? error))
        {
            ShowConfirmationError(error ?? "진행 백업을 삭제하지 못했습니다.");
            return;
        }

        _pendingBackup = null;
        ClearSessionMemo();
        CloseConfirmationOverlay();
        _allowClose = true;
        Close();
    }

    private void TryPeriodicProgressBackup()
    {
        if (!_gameInProgress || DateTimeOffset.UtcNow - _lastBackupWriteUtc < TimeSpan.FromSeconds(5))
        {
            return;
        }

        SaveProgressBackup(reportFailure: false);
    }

    private bool SaveProgressBackup(bool reportFailure)
    {
        if (!_gameInProgress || _state.Ending != EndingChoice.None)
        {
            return true;
        }

        ProgressBackup backup = new()
        {
            Nickname = _playerData.Nickname,
            SolvedPuzzles = [.. _state.SolvedPuzzles.OrderBy(puzzle => puzzle)],
            HintCount = _state.HintCount,
            FailedAttempts = _state.FailedAttempts,
            ClockRestored = _state.ClockRestored,
            ElapsedMilliseconds = Math.Max(0, (long)CurrentElapsed().TotalMilliseconds),
            Screen = _screen.ToString(),
            SavedAtUtc = DateTimeOffset.UtcNow
        };
        if (_progressBackupStore.TrySave(backup, out string? error))
        {
            _lastBackupWriteUtc = DateTimeOffset.UtcNow;
            return true;
        }

        AppendDataWarning(error);
        ErrorReporter.Report(new IOException(error), "Saving progress backup", false);
        if (reportFailure)
        {
            ShowConfirmationError(error ?? "진행 백업을 저장하지 못했습니다.");
        }

        return false;
    }

    private void UpdateWindowCloseAvailability()
    {
        ControlBox = !_gameInProgress;
    }

    private void AppendDataWarning(string? warning)
    {
        if (string.IsNullOrWhiteSpace(warning))
        {
            return;
        }

        _dataWarning = string.IsNullOrWhiteSpace(_dataWarning)
            ? warning
            : $"{_dataWarning} / {warning}";
    }

    private void StartNewGame()
    {
        if (string.IsNullOrWhiteSpace(_playerData.Nickname))
        {
            ShowNicknameSetup();
            return;
        }

        CancelMenuWork();
        CloseConfirmationOverlay();
        if (!_progressBackupStore.TryDiscard(out string? discardError))
        {
            AppendDataWarning(discardError);
        }

        _pendingBackup = null;
        _state = new GameState();
        _inventoryExpanded = false;
        _elapsedBeforeSession = TimeSpan.Zero;
        _gameStopwatch.Restart();
        _hintSteps.Clear();
        _gameInProgress = true;
        _allowClose = false;
        _lastBackupWriteUtc = DateTimeOffset.MinValue;
        _elapsedTimer.Start();
        UpdateWindowCloseAvailability();
        PlayClick();
        ShowIntro();
        SaveProgressBackup(reportFailure: false);
    }

    private void ShowIntro()
    {
        SetScreen(GameScreen.Intro, string.Empty, string.Empty, string.Empty);
        _sidebar.Visible = false;
        _inventory.Visible = false;
        _scene.SceneImage = _images[WorkshopImage];
        AtmosphereCard letter = CreateAtmosphereCard(
            "시계공 마리 벨의 긴급 기록",
            "현재 위치는 루미에르 마을 북쪽 끝, 성 니콜라스 골목 13번 시계공방이다.\n크리스마스이브 23시 47분, 문은 잠겼고 중앙 별시계와 마을의 시간이 함께 멈췄다.\n\n나는 이 공방의 시계공 마리 벨이다. 배달부 엘리아스가 가져온 명단에서\n어린 노엘 애스터의 선물과 기다렸다는 기억이 동시에 지워지는 것을 발견했다.\n붉은 선물을 태엽으로 쓰면 모두의 시간은 돌아오지만 노엘의 기다림은 영원히 사라진다.\n\n공방에 기억의 흔적을 남겼다. 장치의 단서를 따라 마지막 별시계를 열고\n누구의 시간을 지킬지 네가 결정해라. 책상 봉투에는 내가 남긴 원문과 암호가 있다.\n\n시계공 마리 벨",
            AtmosphereCardStyle.Letter,
            new Rectangle(165, 30, 1070, 555),
            new Rectangle(65, 34, 900, 58),
            new Rectangle(75, 96, 920, 415),
            19,
            11.2f);
        letter.Name = "LetterDocument";
        _scene.Controls.Add(letter);
        AddAction("continue", "기록을 접고 공방 조사하기", new Rectangle(525, 610, 350, 62), (_, _) => ShowRoom(), 1);
    }

    private void ShowRoom()
    {
        SetScreen(GameScreen.Room, "멈춘 공방", "방 안의 물건을 직접 눌러 조사하세요. 눈에 띄는 장치와 물건을 차례로 살펴보세요.", "장치의 단서를 풀어 기억을 되찾으면 중앙 별시계의 봉인이 열립니다.");
        _scene.SceneImage = _images[WorkshopImage];
        AddHotspot("hotspot_lantern", PuzzleDone(PuzzleId.Lanterns) ? "금고 완료" : "별등 금고", new Rectangle(5, 265, 150, 125), (_, _) => ShowLanternPuzzle(), 1, PuzzleDone(PuzzleId.Lanterns));
        AddHotspot("hotspot_desk", "마리의 책상 가까이 보기", new Rectangle(160, 315, 270, 145), (_, _) => ShowDesk(), 2, false);
        AddHotspot("hotspot_melody", PuzzleDone(PuzzleId.Melody) ? "계산대 완료" : "설구 계산대", new Rectangle(425, 450, 225, 64), (_, _) => ShowSnowglobePuzzle(), 2, PuzzleDone(PuzzleId.Melody));
        AddHotspot("hotspot_clock", _state.CanOpenClock ? "별시계 열기" : "별시계 잠김", new Rectangle(590, 175, 225, 180), (_, _) => ShowClockPuzzle(), 4, false);
        AddHotspot("hotspot_loom", PuzzleDone(PuzzleId.RibbonLoom) ? "양말 장치 완료" : "양말 정렬 장치", new Rectangle(1060, 325, 225, 150), (_, _) => ShowStockingPuzzle(), 5, PuzzleDone(PuzzleId.RibbonLoom));
    }

    private void ShowDesk()
    {
        SetScreen(
            GameScreen.Desk,
            "마리의 책상",
            "책상 정면에는 봉인된 편지, 장난감 선반, 황동 나침반이 놓인 별자리 도면이 있습니다.",
            "봉투와 선반의 장난감, 왼쪽 별자리 도면을 직접 눌러 조사하세요.");
        _scene.SceneImage = _images["desk-closeup.png"];
        AddHotspot("desk_letter", PuzzleDone(PuzzleId.LetterAcrostic) ? "마리의 편지 다시 읽기" : "봉인된 편지 열기", new Rectangle(575, 425, 275, 130), (_, _) => ShowMarieLetter(), 1, PuzzleDone(PuzzleId.LetterAcrostic));
        AddHotspot("desk_toys", PuzzleDone(PuzzleId.ToyCipher) ? "장난감 암호 완료" : "장난감 선반 조사", new Rectangle(230, 35, 1000, 210), (_, _) => ShowToyCipherPuzzle(), 2, PuzzleDone(PuzzleId.ToyCipher));
        AddHotspot("desk_chart", PuzzleDone(PuzzleId.StarChart) ? "별자리 암호 완료" : "별자리 도면 조사", new Rectangle(120, 405, 455, 205), (_, _) => ShowStarChartPuzzle(), 3, PuzzleDone(PuzzleId.StarChart));
    }

    private void ShowMarieLetter()
    {
        SetScreen(GameScreen.Letter, string.Empty, string.Empty, string.Empty);
        _sidebar.Visible = false;
        _inventory.Visible = false;
        _scene.SceneImage = _images["desk-closeup.png"];

        EnvelopeLetterAnimation reveal = new()
        {
            Name = "EnvelopeReveal",
            Bounds = new Rectangle(310, 25, 780, 600)
        };
        _scene.Controls.Add(reveal);

        AtmosphereCard letter = CreateAtmosphereCard(
            "마리 벨이 남긴 편지",
            "Candle 빛 아래서 지워진 이름을 처음 보았다.\nHearth의 재 속에는 엘리아스가 떨어뜨린 배달 명단이 남아 있었다.\nIcicle처럼 차가운 잉크는 노엘 애스터의 줄만 삼켰다.\nMidnight 전에 흩어진 기억을 되찾아야 한다.\nNorth 창가의 별시계는 23시 47분에서 기다린다.\nEnvelope의 봉인은 내가 직접 찍었다.\nYear의 마지막 밤, 첫 글자들이 마지막 통로를 말해 줄 것이다.\n\n네가 이 글을 읽는다면 노엘의 기다림까지 함께 기억해 줘.\n시계공 마리 벨",
            AtmosphereCardStyle.Letter,
            new Rectangle(170, 20, 1060, 535),
            new Rectangle(60, 30, 940, 55),
            new Rectangle(68, 92, 924, 390),
            18,
            11.4f);
        letter.Name = "MarieLetterDocument";
        letter.Visible = false;
        _scene.Controls.Add(letter);

        Button decipher = AddAction("letter_decipher", PuzzleDone(PuzzleId.LetterAcrostic) ? "해독 기록 보기" : "편지의 첫 글자 해독하기", new Rectangle(485, 575, 430, 62), (_, _) => ShowLetterAcrosticPuzzle(), 1);
        decipher.Visible = false;
        _ = AnimateMarieLetterAsync(reveal, letter, decipher, _sceneCancellation.Token);
    }

    private async Task AnimateMarieLetterAsync(EnvelopeLetterAnimation reveal, Control letter, Control decipher, CancellationToken cancellationToken)
    {
        if (!_animationsEnabled)
        {
            reveal.Visible = false;
            letter.Visible = true;
            decipher.Visible = true;
            return;
        }

        try
        {
            const int frameCount = 36;
            for (int frame = 0; frame <= frameCount; frame++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                reveal.RevealProgress = frame / (float)frameCount;
                await Task.Delay(24, cancellationToken).ConfigureAwait(true);
            }

            reveal.Visible = false;
            letter.Visible = true;
            decipher.Visible = true;
            ScheduleResponsiveLayout();
        }
        catch (OperationCanceledException)
        {
            // Leaving the letter screen intentionally stops the reveal.
        }
    }

    private void ShowLetterAcrosticPuzzle()
    {
        if (PuzzleDone(PuzzleId.LetterAcrostic))
        {
            ShowSolvedMessage(PuzzleId.LetterAcrostic, "네 번째 기억 조각", "편지의 일곱 시작 글자가 굴뚝을 가리키자 배달부 엘리아스의 잃어버린 경로가 되살아났다.");
            return;
        }

        SetScreen(GameScreen.LetterAcrostic, "퍼즐 4: 편지의 숨은 통로", "마리의 편지에는 영어 단어로 시작하는 일곱 문장이 있습니다. 각 문장의 첫 글자를 위에서부터 이어 통로의 영어 이름을 찾으세요.", "공백 없이 영어 단어를 입력하세요. 한국어 뜻도 정답으로 인정됩니다.");
        _scene.SceneImage = _images["desk-closeup.png"];
        AtmosphereCard record = CreateAtmosphereCard(
            "편지 가장자리의 연필 메모",
            "Candle\nHearth\nIcicle\nMidnight\nNorth\nEnvelope\nYear\n\nFIRST LETTERS SHOW THE WAY",
            AtmosphereCardStyle.Letter,
            new Rectangle(350, 55, 700, 380),
            new Rectangle(55, 30, 590, 50),
            new Rectangle(75, 80, 550, 270),
            17,
            10.5f);
        record.Name = "LetterAcrosticRecord";
        _scene.Controls.Add(record);

        TextBox editor = CreatePuzzleCodeEditor("LetterAcrosticCode", new Rectangle(470, 440, 280, 62), 12, "편지의 숨은 통로 정답", digitsOnly: false);
        _scene.Controls.Add(editor);
        void SubmitCode()
        {
            if (PuzzleRules.MatchesLetterAcrostic(editor.Text))
            {
                CompletePuzzle(PuzzleId.LetterAcrostic, "네 번째 기억 조각", "조각의 각인: ‘굴뚝, 지워진 배달 경로’");
                return;
            }

            RejectTextPuzzle(editor, "일곱 영어 단어의 첫 글자를 위에서부터 정확히 이어 보세요.");
        }

        AddAction("letter_acrostic_confirm", "통로 확인", new Rectangle(770, 440, 190, 62), (_, _) => SubmitCode(), 2);
        editor.KeyDown += (_, eventArgs) => SubmitOnEnter(eventArgs, SubmitCode);
        editor.Focus();
    }

    private void ShowToyCipherPuzzle()
    {
        if (PuzzleDone(PuzzleId.ToyCipher))
        {
            ShowSolvedMessage(PuzzleId.ToyCipher, "다섯 번째 기억 조각", "열세 칸을 되돌리자 장난감들이 가리킨 시각이 MIDNIGHT, 자정으로 읽혔다.");
            return;
        }

        SetScreen(GameScreen.ToyCipher, "퍼즐 5: 열세 번째 종의 장난감 암호", "장난감 뒤의 여덟 블록에는 ZVQAVTUG가 적혀 있습니다. ‘열세 번째 종은 알파벳도 같은 수만큼 되돌린다’는 문장을 이용해 해독하세요.", "영어 알파벳 26자는 13칸 이동하면 같은 방식으로 되돌릴 수 있습니다.");
        _scene.SceneImage = _images["desk-closeup.png"];
        AtmosphereCard record = CreateAtmosphereCard(
            "장난감 선반 뒤의 블록",
            "Z V Q A V T U G\n\nA ↔ N    B ↔ O    C ↔ P    D ↔ Q\nE ↔ R    F ↔ S    G ↔ T    H ↔ U\nI ↔ V    J ↔ W    K ↔ X    L ↔ Y    M ↔ Z",
            AtmosphereCardStyle.GiftTag,
            new Rectangle(285, 90, 830, 310),
            new Rectangle(55, 30, 720, 50),
            new Rectangle(70, 90, 690, 195),
            18,
            13);
        record.Name = "ToyCipherRecord";
        _scene.Controls.Add(record);

        TextBox editor = CreatePuzzleCodeEditor("ToyCipherCode", new Rectangle(470, 425, 280, 62), 12, "장난감 ROT13 암호 정답", digitsOnly: false);
        _scene.Controls.Add(editor);
        void SubmitCode()
        {
            if (PuzzleRules.MatchesToyCipher(editor.Text))
            {
                CompletePuzzle(PuzzleId.ToyCipher, "다섯 번째 기억 조각", "조각의 각인: ‘MIDNIGHT, 열세 칸 뒤의 자정’");
                return;
            }

            RejectTextPuzzle(editor, "각 글자를 알파벳에서 정확히 13칸 이동해 보세요.");
        }

        AddAction("toy_cipher_confirm", "암호 해독", new Rectangle(770, 425, 190, 62), (_, _) => SubmitCode(), 2);
        editor.KeyDown += (_, eventArgs) => SubmitOnEnter(eventArgs, SubmitCode);
        editor.Focus();
    }

    private void ShowStarChartPuzzle()
    {
        if (PuzzleDone(PuzzleId.StarChart))
        {
            ShowSolvedMessage(PuzzleId.StarChart, "여섯 번째 기억 조각", "암호문의 각 글자를 한 발자국 뒤로 옮기자 AURORA, 북쪽 하늘의 오로라가 나타났다.");
            return;
        }

        SetScreen(GameScreen.StarChart, "퍼즐 6: 한 발자국 뒤의 카이사르 암호", "별자리 도면에는 ‘북쪽 하늘을 밝히려면 한 발자국 뒤로 가야 한다’는 문장과 암호문 BVSPSB가 적혀 있습니다. 각 알파벳을 한 칸 뒤로 옮겨 원래 단어를 찾으세요.", "예: B는 한 칸 뒤의 A가 됩니다. A 앞은 Z로 이어집니다.");
        _scene.SceneImage = _images["desk-closeup.png"];
        AtmosphereCard record = CreateAtmosphereCard(
            "북쪽 별자리 도면",
            "북쪽 하늘을 밝히려면\n한 발자국 뒤로 가야 한다.\n\nB  V  S  P  S  B\n\n각 글자를 알파벳에서 한 칸 뒤로",
            AtmosphereCardStyle.Letter,
            new Rectangle(300, 70, 800, 330),
            new Rectangle(55, 30, 690, 50),
            new Rectangle(75, 86, 650, 235),
            18,
            13.5f);
        record.Name = "StarChartRecord";
        _scene.Controls.Add(record);

        TextBox editor = CreatePuzzleCodeEditor("StarChartCode", new Rectangle(470, 425, 280, 62), 12, "별자리 카이사르 암호 정답", digitsOnly: false);
        _scene.Controls.Add(editor);
        void SubmitCode()
        {
            if (PuzzleRules.MatchesStarChart(editor.Text))
            {
                CompletePuzzle(PuzzleId.StarChart, "여섯 번째 기억 조각", "조각의 각인: ‘AURORA, 북쪽 창의 빛’");
                return;
            }

            RejectTextPuzzle(editor, "BVSPSB의 각 글자를 알파벳에서 한 칸 뒤로 옮기세요. B는 A가 됩니다.");
        }

        AddAction("star_chart_confirm", "별빛 복원", new Rectangle(770, 425, 190, 62), (_, _) => SubmitCode(), 2);
        editor.KeyDown += (_, eventArgs) => SubmitOnEnter(eventArgs, SubmitCode);
        editor.Focus();
    }

    private void RejectTextPuzzle(TextBox editor, string message)
    {
        _state.RecordFailure();
        editor.Clear();
        editor.Focus();
        ShowNarrativeMessage(message);
        PlaySound(GameSound.Wrong);
        UpdateHeader();
        SaveProgressBackup(reportFailure: false);
    }

    private void HandleSceneClick(object? sender, MouseEventArgs eventArgs)
    {
        if (eventArgs.Button != MouseButtons.Left
            || _screen != GameScreen.Room
            || _confirmationOverlay is not null
            || _scene.ClientSize.Width <= 0
            || _scene.ClientSize.Height <= 0)
        {
            return;
        }

        Point roomPoint = new(
            (int)Math.Round(eventArgs.X * 1400d / _scene.ClientSize.Width),
            (int)Math.Round(eventArgs.Y * 750d / _scene.ClientSize.Height));
        InspectRoomPoint(roomPoint);
    }

    private void InspectRoomPoint(Point point)
    {
        string observation;

        if (new Rectangle(1260, 35, 95, 140).Contains(point))
        {
            InspectShelfTree();
            return;
        }

        if (new Rectangle(1180, 35, 80, 195).Contains(point))
        {
            observation = "나무 병정 인형이다. 한쪽 부츠에 오래된 톱밥이 붙어 있다.";
        }
        else if (new Rectangle(1260, 160, 120, 105).Contains(point))
        {
            observation = "작은 흔들목마다. 금방이라도 선반 끝까지 달려갈 듯 앞을 보고 있다.";
        }
        else if (new Rectangle(1350, 235, 50, 175).Contains(point))
        {
            observation = "황동 종이다. 흔들어 보아도 소리는 나지 않고 차가운 금속 감촉만 남는다.";
        }
        else if (new Rectangle(1160, 245, 105, 125).Contains(point))
        {
            observation = "곰 인형이다. 주인을 기다린 시간이 긴지 털이 조금 바래 있다.";
        }
        else if (new Rectangle(1010, 360, 220, 165).Contains(point))
        {
            observation = "붉은 선물 상자다. 명찰은 없고, 리본만 단단히 묶여 있다.";
        }
        else if (new Rectangle(860, 135, 290, 390).Contains(point))
        {
            observation = "벽난로의 불은 따뜻하지만 장작은 줄어들지 않는다. 이 공방의 시간만 멈춘 모양이다.";
        }
        else if (new Rectangle(920, 100, 205, 190).Contains(point))
        {
            observation = "촛불 여러 개가 바람도 없이 같은 방향으로 기울어 있다.";
        }
        else if (new Rectangle(245, 80, 355, 315).Contains(point))
        {
            observation = "서리 낀 창밖으로 눈 덮인 마을이 보인다. 발자국 하나 없이 고요하다.";
        }
        else if (new Rectangle(0, 75, 120, 325).Contains(point))
        {
            observation = "낡은 장난감들이 빼곡하다. 모두 수리되었지만 찾아간 사람은 없는 듯하다.";
        }
        else if (new Rectangle(780, 75, 170, 145).Contains(point))
        {
            observation = "전나무 가지와 붉은 열매 장식이다. 마른 잎 사이에 금빛 실 한 가닥이 걸려 있다.";
        }
        else if (point.Y < 190)
        {
            observation = "높은 선반에는 완성된 장난감들이 놓여 있다. 먼지가 없어 누군가 계속 돌본 듯하다.";
        }
        else if (point.X < 410)
        {
            observation = "작업대에는 작은 공구 자국이 겹겹이 남아 있다. 서둘러 자리를 비운 흔적은 아니다.";
        }
        else if (point.X > 1030)
        {
            observation = "색실과 포장지가 가지런하다. 마지막 작업만 끝내지 못한 채 시간이 멈췄다.";
        }
        else
        {
            observation = "공방의 나무 바닥이다. 희미한 톱밥 자국이 별시계 쪽으로 이어진다.";
        }

        ShowRoomEvent(observation);
        PlayClick();
    }

    private void InspectShelfTree()
    {
        if (_treeEasterEggFound)
        {
            ShowRoomEvent("작은 트리의 별이 희미하게 반짝인다. 숨겨진 인사는 아직 그 자리에 남아 있다.");
            PlaySound(GameSound.NoteStar);
            return;
        }

        _treeEasterEggClicks++;
        ShowRoomEvent(_treeEasterEggClicks switch
        {
            1 => "작은 크리스마스 트리 장식이다. 누가 두고 간 듯하다.",
            2 => "트리를 다시 건드리자 꼭대기의 별이 아주 조금 흔들렸다.",
            3 => "별 아래에 접힌 종잇조각이 보인다. 손끝에는 닿지 않는다.",
            4 => "한 번만 더 건드리면 종잇조각이 떨어질 것 같다.",
            _ => "[숨겨진 기록] 별 장식에서 작은 쪽지가 떨어졌다. ‘찾아줘서 고마워. 열세 번째 종은 호기심 많은 사람에게만 들린단다.’"
        });

        if (_treeEasterEggClicks >= 5)
        {
            _treeEasterEggFound = true;
            PlaySound(GameSound.NoteStar);
        }
        else
        {
            PlayClick();
        }
    }

    private void ShowLanternPuzzle()
    {
        if (PuzzleDone(PuzzleId.Lanterns))
        {
            ShowSolvedMessage(PuzzleId.Lanterns, "첫 번째 기억 조각", "12월과 25일에서 시작된 별빛은 앞선 두 기억을 더하며 다음 길을 밝혔다.");
            return;
        }

        SetScreen(GameScreen.Lanterns, "퍼즐 1: 얼어붙은 별빛 금고", "금고는 크리스마스의 두 숫자에서 시작해 앞선 기억을 이어 붙였습니다. 마지막 빈칸의 세 자리 수를 입력하세요.", "정답 칸에는 숫자 세 자리만 들어갑니다.");
        _scene.SceneImage = _images["lanterns.png"];
        AtmosphereCard record = CreateAtmosphereCard(
            "서리 금고의 기록",
            "12     25     37     62     99     ?\n앞의 수들이 남긴 규칙으로 마지막 수를 찾으시오.",
            AtmosphereCardStyle.Letter,
            new Rectangle(260, 115, 880, 255),
            new Rectangle(55, 32, 770, 50),
            new Rectangle(70, 88, 740, 105),
            18,
            15);
        record.Name = "FrostVaultRecord";
        _scene.Controls.Add(record);

        TextBox editor = CreatePuzzleCodeEditor("FrostVaultCode", new Rectangle(485, 395, 225, 62), 3, "별빛 금고의 세 자리 암호");
        _scene.Controls.Add(editor);

        void SubmitCode()
        {
            if (PuzzleRules.MatchesFrostVaultCode(editor.Text))
            {
                CompletePuzzle(PuzzleId.Lanterns, "첫 번째 기억 조각", "조각의 각인: ‘12, 자정의 시작’");
                return;
            }

            _state.RecordFailure();
            editor.Clear();
            editor.Focus();
            ShowNarrativeMessage("금고가 다시 얼어붙었습니다. 각 항이 앞선 두 항과 어떤 관계인지 확인하세요.");
            PlaySound(GameSound.Wrong);
            SaveProgressBackup(reportFailure: false);
        }

        AddAction("frost_vault_confirm", "금고 열기", new Rectangle(730, 395, 190, 62), (_, _) => SubmitCode(), 2);
        editor.KeyDown += (_, eventArgs) => SubmitOnEnter(eventArgs, SubmitCode);
        editor.Focus();
    }

    private void ShowSnowglobePuzzle()
    {
        if (PuzzleDone(PuzzleId.Melody))
        {
            ShowSolvedMessage(PuzzleId.Melody, "두 번째 기억 조각", "설구의 장식마다 숨은 값이 드러나자 멈춘 마을의 불빛이 다시 계산된 박자로 켜졌다.");
            return;
        }

        SetScreen(GameScreen.Melody, "퍼즐 2: 요정들의 장식 계산식", "세 식으로 눈사람, 트리, 선물의 값을 찾은 뒤 마지막 식을 계산하세요. 곱셈은 덧셈보다 먼저입니다.", "정답 칸에는 마지막 식의 숫자만 입력합니다.");
        _scene.SceneImage = _images["snowglobe.png"];
        AtmosphereCard calculation = CreateAtmosphereCard(
            "설구 받침의 계산판",
            "눈사람 + 눈사람 = 16\n눈사람 + 트리 = 15\n트리 + 선물 × 눈사람 = 39\n\n선물 + 눈사람 × 트리 = ?",
            AtmosphereCardStyle.Letter,
            new Rectangle(250, 100, 900, 320),
            new Rectangle(55, 28, 790, 50),
            new Rectangle(75, 82, 750, 210),
            18,
            13.5f);
        calculation.Name = "OrnamentEquationRecord";
        _scene.Controls.Add(calculation);

        TextBox editor = CreatePuzzleCodeEditor("OrnamentEquationCode", new Rectangle(485, 440, 225, 62), 3, "장식 계산식의 숫자 정답");
        _scene.Controls.Add(editor);

        void SubmitCode()
        {
            if (PuzzleRules.MatchesOrnamentEquationCode(editor.Text))
            {
                CompletePuzzle(PuzzleId.Melody, "두 번째 기억 조각", "조각의 각인: ‘00, 멈춤 뒤의 첫 박자’");
                return;
            }

            _state.RecordFailure();
            editor.Clear();
            editor.Focus();
            ShowNarrativeMessage("계산판의 불빛이 꺼졌습니다. 장식 값을 차례로 구하고 곱셈을 먼저 처리하세요.");
            PlaySound(GameSound.Wrong);
            SaveProgressBackup(reportFailure: false);
        }

        AddAction("ornament_equation_confirm", "계산판 작동", new Rectangle(730, 440, 190, 62), (_, _) => SubmitCode(), 2);
        editor.KeyDown += (_, eventArgs) => SubmitOnEnter(eventArgs, SubmitCode);
        editor.Focus();
    }

    private void ShowStockingPuzzle()
    {
        if (PuzzleDone(PuzzleId.RibbonLoom))
        {
            ShowSolvedMessage(PuzzleId.RibbonLoom, "세 번째 기억 조각", "네 양말이 제자리를 찾자 1, 2, 2, 5가 이어져 기다림의 날짜를 가리켰다.");
            return;
        }

        SetScreen(
            GameScreen.RibbonLoom,
            "퍼즐 3: 벽난로 앞의 양말들",
            "명찰 숫자: 노랑 1, 초록 2, 빨강 2, 파랑 5\n① 초록은 양끝이 아니다. ② 파랑은 빨강보다 오른쪽이다.\n③ 노랑과 파랑은 이웃하지 않는다. ④ 초록은 빨강 바로 왼쪽이다.",
            "아래의 양말을 마우스로 끌어 벽난로 위 네 고리에 직접 거세요.");
        _scene.SceneImage = _images["stocking-logic.png"];
        _stockingPlacement = [-1, -1, -1, -1];
        _stockingPieceSlots = [-1, -1, -1, -1];
        _lastFailedStockingOrder = null;
        _stockingSlots = new StockingHookSlot[4];
        _stockingPieces = new StockingPiece[4];

        for (int index = 0; index < _stockingSlots.Length; index++)
        {
            StockingHookSlot slot = new()
            {
                Name = $"StockingHook{index + 1}",
                Bounds = new Rectangle(446 + (index * 142), 98, 122, 188),
                AccessibleName = $"왼쪽에서 {index + 1}번째 벽난로 고리"
            };
            _stockingSlots[index] = slot;
            _scene.Controls.Add(slot);
        }

        Color[] colors =
        [
            Color.FromArgb(196, 139, 35),
            Color.FromArgb(21, 105, 60),
            Color.FromArgb(174, 31, 39),
            Color.FromArgb(24, 82, 164)
        ];
        string[] tags = ["1", "2", "2", "5"];
        string[] names = ["노랑", "초록", "빨강", "파랑"];
        int[] initialOrder = [0, 1, 2, 3];
        do
        {
            Random.Shared.Shuffle(initialOrder);
        }
        while (initialOrder.SequenceEqual([0, 1, 2, 3]));

        Image stockingSprites = _images["stocking-sprites.png"];
        for (int position = 0; position < initialOrder.Length; position++)
        {
            int index = initialOrder[position];
            StockingPiece piece = new(index, colors[index], tags[index], stockingSprites)
            {
                Name = $"StockingPiece{index}",
                Bounds = new Rectangle(285 + (position * 210), 355, 118, 154),
                TabIndex = position + 1,
                AccessibleName = $"{names[index]} 양말 {tags[index]}, 벽난로 고리에 끌어 놓으세요"
            };
            piece.HomeBounds = piece.Bounds;
            piece.Dropped += HandleStockingDrop;
            _stockingPieces[index] = piece;
            _scene.Controls.Add(piece);
            piece.BringToFront();
        }

        _inventory.BringToFront();
        _sidebar.BringToFront();
    }

    private void HandleStockingDrop(object? sender, StockingDroppedEventArgs eventArgs)
    {
        if (sender is not StockingPiece piece
            || _stockingSlots is null
            || _stockingPlacement is null
            || _stockingPieceSlots is null)
        {
            return;
        }

        int targetSlot = Array.FindIndex(_stockingSlots, slot => slot.Bounds.Contains(eventArgs.Center));
        if (targetSlot < 0)
        {
            int previousSlot = _stockingPieceSlots[piece.StockingId];
            if (previousSlot >= 0)
            {
                _stockingPlacement[previousSlot] = -1;
                _stockingSlots[previousSlot].Occupied = false;
                _stockingSlots[previousSlot].Invalidate();
                _stockingPieceSlots[piece.StockingId] = -1;
            }

            piece.ReturnHome();
            return;
        }

        PlaceStockingInSlot(piece.StockingId, targetSlot);
        PlayMechanismTone(targetSlot);
        EvaluateStockingArrangement();
    }

    private void PlaceStockingInSlot(int pieceId, int targetSlot)
    {
        if (_stockingSlots is null || _stockingPieces is null || _stockingPlacement is null || _stockingPieceSlots is null)
        {
            return;
        }

        int previousSlot = _stockingPieceSlots[pieceId];
        int displacedPiece = _stockingPlacement[targetSlot];
        if (previousSlot >= 0)
        {
            _stockingPlacement[previousSlot] = -1;
            _stockingSlots[previousSlot].Occupied = false;
        }

        if (displacedPiece >= 0 && displacedPiece != pieceId)
        {
            if (previousSlot >= 0)
            {
                _stockingPlacement[previousSlot] = displacedPiece;
                _stockingPieceSlots[displacedPiece] = previousSlot;
                SnapStockingToSlot(displacedPiece, previousSlot);
                _stockingSlots[previousSlot].Occupied = true;
            }
            else
            {
                _stockingPieceSlots[displacedPiece] = -1;
                _stockingPieces[displacedPiece].ReturnHome();
            }
        }

        _stockingPlacement[targetSlot] = pieceId;
        _stockingPieceSlots[pieceId] = targetSlot;
        _stockingSlots[targetSlot].Occupied = true;
        SnapStockingToSlot(pieceId, targetSlot);
        foreach (StockingHookSlot slot in _stockingSlots)
        {
            slot.Invalidate();
        }
    }

    private void SnapStockingToSlot(int pieceId, int slotIndex)
    {
        if (_stockingSlots is null || _stockingPieces is null)
        {
            return;
        }

        StockingHookSlot slot = _stockingSlots[slotIndex];
        StockingPiece piece = _stockingPieces[pieceId];
        piece.Location = new Point(slot.Left + ((slot.Width - piece.Width) / 2), slot.Top + 19);
        piece.BringToFront();
    }

    private void EvaluateStockingArrangement()
    {
        if (_stockingPlacement is null || _stockingPlacement.Any(value => value < 0))
        {
            return;
        }

        if (PuzzleRules.MatchesStockingOrder(_stockingPlacement))
        {
            CompletePuzzle(PuzzleId.RibbonLoom, "세 번째 기억 조각", "조각의 각인: ‘25, 이름 없는 선물의 날’");
            return;
        }

        string signature = string.Join(',', _stockingPlacement);
        if (signature == _lastFailedStockingOrder)
        {
            return;
        }

        _lastFailedStockingOrder = signature;
        _state.RecordFailure();
        ShowNarrativeMessage("네 양말이 모두 걸렸지만 장치가 잠겼습니다. 조건을 다시 확인해 양말의 위치를 바꾸세요.");
        PlaySound(GameSound.Wrong);
        UpdateHeader();
        SaveProgressBackup(reportFailure: false);
    }

    private void ShowClockPuzzle()
    {
        if (!_state.CanOpenClock)
        {
            ShowRoomEvent("별시계의 홈이 비어 있습니다. 다른 장치의 단서를 따라 기억을 더 되찾으세요.");
            PlaySound(GameSound.Locked);
            return;
        }

        if (_state.ClockRestored)
        {
            ShowChoice();
            return;
        }

        SetScreen(GameScreen.Clock, "최종 장치: 별시계", "세 기억 조각의 숫자를 시, 분, 날짜 다이얼에 맞추세요.", "각인 기록: 자정의 시작 12, 멈춤 뒤의 첫 박자 00, 이름 없는 선물의 날 25");
        _scene.SceneImage = _images[WorkshopImage];
        Panel clockPanel = new()
        {
            Bounds = new Rectangle(425, 100, 550, 330),
            BackColor = Theme.Night,
            Padding = new Padding(30)
        };
        _scene.Controls.Add(clockPanel);

        Label title = Theme.CreateLabel("별시계 설정", 20, FontStyle.Bold);
        title.Bounds = new Rectangle(30, 18, 490, 66);
        title.TextAlign = ContentAlignment.MiddleCenter;
        title.ForeColor = Theme.Gold;
        clockPanel.Controls.Add(title);

        NumericUpDown hour = CreateDial("시", 1, 12, 11, 35, clockPanel, "ClockHour");
        NumericUpDown minute = CreateDial("분", 0, 59, 47, 205, clockPanel, "ClockMinute");
        NumericUpDown date = CreateDial("날짜", 1, 31, 24, 375, clockPanel, "ClockDate");

        Button confirm = Theme.CreateButton("13번째 종 울리기", (_, _) =>
        {
            if (_state.RestoreClock((int)hour.Value, (int)minute.Value, (int)date.Value))
            {
                PlaySound(GameSound.ClockRestored);
                ShowChoice();
            }
            else
            {
                ShowNarrativeMessage("별시계가 한 번 떨리고 멈췄습니다. 여섯 조각 중 12, 00, 25의 숫자 각인을 다시 대조하세요.");
                PlaySound(GameSound.Wrong);
                UpdateHeader();
                SaveProgressBackup(reportFailure: false);
            }
        }, 4);
        confirm.Name = "ClockConfirm";
        confirm.Bounds = new Rectangle(125, 235, 300, 60);
        clockPanel.Controls.Add(confirm);
        _actions.Add("clock_confirm", confirm);
    }

    private static NumericUpDown CreateDial(string labelText, int minimum, int maximum, int value, int x, Control parent, string name)
    {
        Label label = Theme.CreateLabel(labelText, 10, FontStyle.Bold);
        label.Bounds = new Rectangle(x, 92, 140, 30);
        label.TextAlign = ContentAlignment.MiddleCenter;
        parent.Controls.Add(label);

        NumericUpDown dial = new()
        {
            Name = name,
            Minimum = minimum,
            Maximum = maximum,
            Value = value,
            Bounds = new Rectangle(x, 126, 140, 58),
            Font = Theme.Font(20, FontStyle.Bold),
            ForeColor = Theme.Night,
            BackColor = Theme.PaleGold,
            TextAlign = HorizontalAlignment.Center,
            TabIndex = parent.Controls.Count,
            AccessibleName = $"별시계 {labelText}"
        };
        parent.Controls.Add(dial);
        return dial;
    }

    private void ShowChoice()
    {
        SetScreen(GameScreen.Choice, "마지막 선택", "별시계 안에서 붉은 상자와 비어 있는 태엽 홈이 나란히 드러났습니다.", "선물은 한 사람의 기다림을 되돌리고, 태엽은 시간을 즉시 되돌리지만 그 기다림을 지웁니다.");
        _inventory.Visible = false;
        _scene.SceneImage = _images["ribbon-loom.png"];
        AtmosphereCard choice = CreateAtmosphereCard(
            "마리가 어린 시절 쓴 카드",
            "상자 안에는 장난감도 보석도 없었다.\n\n“미래의 나에게. 기다리는 마음을 잊지 않기를.”",
            AtmosphereCardStyle.Speech,
            new Rectangle(260, 55, 880, 320),
            new Rectangle(55, 38, 770, 54),
            new Rectangle(70, 105, 740, 135),
            18,
            14);
        choice.Name = "ChoiceSpeech";
        _scene.Controls.Add(choice);
        AddAction("choice_gift", "선물을 마리에게 전달한다", new Rectangle(220, 420, 420, 78), (_, _) => ShowEnding(EndingChoice.DeliverTheGift), 1);
        AddAction("choice_clock", "선물을 시계의 태엽으로 쓴다", new Rectangle(760, 420, 420, 78), (_, _) => ShowEnding(EndingChoice.FeedTheClock), 2);
        SaveProgressBackup(reportFailure: false);
    }

    private void ShowEnding(EndingChoice choice)
    {
        if (!_state.ChooseEnding(choice))
        {
            throw new InvalidOperationException("별시계를 복구하기 전에는 결말을 선택할 수 없습니다.");
        }

        bool trueEnding = choice == EndingChoice.DeliverTheGift;
        string title = trueEnding ? "진엔딩: 기다림의 수취인" : "엔딩: 정확한 크리스마스";
        string body = trueEnding
            ? "선물이 과거의 마리에게 닿자 공방의 열세 번째 종이 울렸다. 시간은 1분 늦게 흐르기 시작했지만, 그 1분 동안 세상의 모든 잊힌 편지에 새 주소가 생겼다.\n\n시계는 시간을 맞췄고, 사람은 기다림을 기억했다."
            : "붉은 상자는 완벽한 태엽이 되어 별시계를 움직였다. 모든 선물은 정확히 자정에 도착했다. 그러나 마리의 빈 카드에는 끝내 이름이 돌아오지 않았다.\n\n시간은 정확했지만, 누군가의 기다림은 기록되지 않았다.";
        _elapsedTimer.Stop();
        _gameStopwatch.Stop();
        _gameInProgress = false;
        _progressBackupStore.TryDiscard(out _);
        _pendingBackup = null;
        ClearSessionMemo();
        UpdateWindowCloseAvailability();
        SetScreen(
            GameScreen.Ending,
            "기록 저장",
            "당신의 마지막 선택이 시계공의 기록에 남았습니다.",
            $"플레이어: {_playerData.Nickname}\n완료 시간: {ElapsedText()}\n힌트 사용: {_state.HintCount}회\n실패 시도: {_state.FailedAttempts}회\n\n온라인 기록을 저장하는 중입니다...");
        _music.Play(BackgroundMusicKind.Menu);
        _inventory.Visible = false;
        _scene.SceneImage = _images[trueEnding ? "snowglobe.png" : WorkshopImage];
        AtmosphereCard ending = CreateAtmosphereCard(
            title,
            body,
            AtmosphereCardStyle.Letter,
            new Rectangle(190, 25, 1020, 410),
            new Rectangle(65, 34, 890, 58),
            new Rectangle(78, 105, 864, 220),
            19,
            13.5f);
        ending.Name = "EndingDocument";
        _scene.Controls.Add(ending);
        AddAction("restart", "새 닉네임으로 다시 시작", new Rectangle(525, 465, 350, 64), (_, _) => ShowNicknameSetup(), 1);
        _ = SubmitOnlineClearAsync(choice, _sceneCancellation.Token);
        PlaySound(GameSound.EndingBell);
    }

    private async Task SubmitOnlineClearAsync(EndingChoice choice, CancellationToken cancellationToken)
    {
        try
        {
            ScoreSubmissionResult result = await _leaderboardService
                .SubmitClearAsync(choice, cancellationToken)
                .ConfigureAwait(true);
            if (cancellationToken.IsCancellationRequested || IsDisposed || _screen != GameScreen.Ending)
            {
                return;
            }

            if (result.Succeeded)
            {
                _hintText.Text =
                    $"플레이어: {_playerData.Nickname}\n" +
                    $"온라인 최고 기록: {FormatOnlineTime(result.ClearTimeMilliseconds)}\n" +
                    $"현재 순위: {result.Rank}위\n" +
                    $"힌트 사용: {_state.HintCount}회\n" +
                    $"실패 시도: {_state.FailedAttempts}회";
                ShowNarrativeBox();
            }
            else
            {
                _hintText.Text += $"\n\n온라인 저장 실패: {result.Error}";
                ShowNarrativeBox();
            }
        }
        catch (OperationCanceledException)
        {
            // Starting another game intentionally cancels the pending submission display.
        }
        catch (Exception exception)
        {
            if (!IsDisposed && _screen == GameScreen.Ending)
            {
                _hintText.Text += "\n\n온라인 기록 저장 중 문제가 발생했습니다.";
                ShowNarrativeBox();
            }

            ErrorReporter.Report(exception, "Submitting online clear time", false);
        }
    }

    private void CompletePuzzle(PuzzleId puzzle, string title, string description)
    {
        _state.Solve(puzzle);
        PlaySound(GameSound.PuzzleItem);
        UpdateHeader();
        UpdateInventory();
        ShowSolvedMessage(puzzle, title, description);
        SaveProgressBackup(reportFailure: false);
    }

    private void ShowSolvedMessage(PuzzleId puzzle, string title, string description)
    {
        string imageName = puzzle switch
        {
            PuzzleId.Lanterns => "lanterns.png",
            PuzzleId.Melody => "snowglobe.png",
            PuzzleId.RibbonLoom => "stocking-logic.png",
            PuzzleId.LetterAcrostic or PuzzleId.ToyCipher or PuzzleId.StarChart => "desk-closeup.png",
            _ => throw new ArgumentOutOfRangeException(nameof(puzzle))
        };
        SetScreen(puzzle switch
        {
            PuzzleId.Lanterns => GameScreen.Lanterns,
            PuzzleId.Melody => GameScreen.Melody,
            PuzzleId.RibbonLoom => GameScreen.RibbonLoom,
            PuzzleId.LetterAcrostic => GameScreen.LetterAcrostic,
            PuzzleId.ToyCipher => GameScreen.ToyCipher,
            PuzzleId.StarChart => GameScreen.StarChart,
            _ => throw new ArgumentOutOfRangeException(nameof(puzzle))
        }, string.Empty, string.Empty, string.Empty);
        _narrativeFadeTimer.Stop();
        _sidebar.Visible = false;
        _scene.SceneImage = _images[imageName];
        AtmosphereCard card = CreateAtmosphereCard(
            title,
            description,
            AtmosphereCardStyle.GiftTag,
            new Rectangle(390, 145, 620, 275),
            new Rectangle(45, 36, 530, 55),
            new Rectangle(58, 105, 504, 105),
            18,
            13);
        card.Name = "MemoryTag";
        _scene.Controls.Add(card);
        AddAction("completion_return", "기억 조각을 챙기고 돌아가기", new Rectangle(505, 450, 390, 64), (_, _) => ShowRoom(), 1);
    }

    private void SetScreen(GameScreen screen, string chapter, string notebook, string hint)
    {
        _music.Play(BackgroundMusicKind.Game);
        Bitmap? previousFrame = CaptureTransitionFrame();
        CancelMenuWork();
        CancelSceneWork();
        _screen = screen;
        _scene.Cursor = screen is GameScreen.Room or GameScreen.Desk ? Cursors.Hand : Cursors.Default;
        _screenStopwatch.Restart();
        _hintAvailable = false;
        _actions.Clear();
        _menuScene.Visible = false;
        SetGameChromeVisible(true);
        DisposeSceneContent();
        _chapterLabel.Text = chapter;
        _notebookText.Text = notebook;
        _hintText.Text = hint;
        ApplyNarrativeStyle(screen);
        ShowNarrativeBox(initialExplanation: true);
        _hintButton.Visible = false;
        _hintButton.Enabled = false;
        UpdateInventory();
        UpdateHeader();
        _scene.BringToFront();
        _inventory.BringToFront();
        _sidebar.BringToFront();
        BringGameChromeToFront();
        ScheduleResponsiveLayout();
        BeginScreenTransition(previousFrame);
    }

    private Bitmap? CaptureTransitionFrame()
    {
        if (!_animationsEnabled || !IsHandleCreated || _stage.ClientSize.Width <= 0 || _stage.ClientSize.Height <= 0)
        {
            return null;
        }

        CancelTransitionOverlay();
        try
        {
            Bitmap frame = new(_stage.ClientSize.Width, _stage.ClientSize.Height);
            _stage.DrawToBitmap(frame, _stage.ClientRectangle);
            return frame;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private void BeginScreenTransition(Bitmap? previousFrame)
    {
        if (previousFrame is null)
        {
            return;
        }

        Bitmap nextFrame;
        try
        {
            ApplyResponsiveLayout();
            nextFrame = new Bitmap(_stage.ClientSize.Width, _stage.ClientSize.Height);
            _stage.DrawToBitmap(nextFrame, _stage.ClientRectangle);
        }
        catch (ArgumentException)
        {
            previousFrame.Dispose();
            return;
        }

        FadeTransitionOverlay overlay = new(previousFrame, nextFrame)
        {
            Name = "ScreenFadeOverlay",
            Bounds = _stage.ClientRectangle,
            Progress = 0f
        };
        _transitionOverlay = overlay;
        _stage.Controls.Add(overlay);
        overlay.BringToFront();
        _transitionStartedAt = DateTimeOffset.UtcNow;
        _transitionTimer.Start();
    }

    private void HandleTransitionTick(object? sender, EventArgs eventArgs)
    {
        if (_transitionOverlay is null)
        {
            _transitionTimer.Stop();
            return;
        }

        float progress = (float)((DateTimeOffset.UtcNow - _transitionStartedAt).TotalMilliseconds / 520d);
        _transitionOverlay.Progress = progress;
        if (progress >= 1f)
        {
            CancelTransitionOverlay();
        }
    }

    private void CancelTransitionOverlay()
    {
        _transitionTimer.Stop();
        FadeTransitionOverlay? overlay = _transitionOverlay;
        _transitionOverlay = null;
        if (overlay is null)
        {
            return;
        }

        _stage.Controls.Remove(overlay);
        overlay.Dispose();
    }

    private void ApplyNarrativeStyle(GameScreen screen)
    {
        _sidebar.VisualStyle = screen switch
        {
            GameScreen.Lanterns or GameScreen.Melody or GameScreen.RibbonLoom
                or GameScreen.LetterAcrostic or GameScreen.ToyCipher or GameScreen.StarChart => NarrativeVisualStyle.Clue,
            GameScreen.Clock => NarrativeVisualStyle.Mechanism,
            GameScreen.Choice => NarrativeVisualStyle.Dialogue,
            GameScreen.Ending => NarrativeVisualStyle.Result,
            _ => NarrativeVisualStyle.Narration
        };
        _chapterLabel.ForeColor = _sidebar.TitleColor;
        _notebookText.ForeColor = _sidebar.BodyColor;
        _hintText.ForeColor = _sidebar.SecondaryColor;
    }

    private void ShowRoomEvent(string message)
    {
        ShowNarrativeMessage(message);
    }

    private void ShowNarrativeMessage(string message)
    {
        _notebookText.Text = message;
        ShowNarrativeBox();
    }

    private void ShowNarrativeBox(bool initialExplanation = false)
    {
        if (_screen == GameScreen.Menu)
        {
            return;
        }

        _sidebar.Visible = true;
        _narrativeShownAt = DateTimeOffset.UtcNow;
        _narrativeVisibleDuration = initialExplanation
            ? InitialNarrativeVisibleDuration
            : NarrativeVisibleDuration;
        _narrativePointerInside = false;
        SetNarrativeOpacity(1f);
        _narrativeFadeTimer.Start();
        _sidebar.BringToFront();
    }

    private void HandleNarrativeFadeTick(object? sender, EventArgs eventArgs)
    {
        if (!_sidebar.Visible)
        {
            _narrativeFadeTimer.Stop();
            return;
        }

        bool pointerInside = _sidebar.RectangleToScreen(_sidebar.ClientRectangle).Contains(Cursor.Position);
        if (pointerInside)
        {
            _narrativePointerInside = true;
            SetNarrativeOpacity(1f);
            return;
        }

        if (_narrativePointerInside)
        {
            _narrativePointerInside = false;
        }

        double elapsed = (DateTimeOffset.UtcNow - _narrativeShownAt).TotalSeconds;
        if (elapsed <= _narrativeVisibleDuration.TotalSeconds)
        {
            return;
        }

        float opacity = 1f - (float)((elapsed - _narrativeVisibleDuration.TotalSeconds) / 0.65d);
        SetNarrativeOpacity(Math.Max(0f, opacity));
        if (opacity <= 0f)
        {
            _sidebar.Visible = false;
            _narrativeFadeTimer.Stop();
        }
    }

    private void SetNarrativeOpacity(float opacity)
    {
        int alpha = Math.Clamp((int)Math.Round(opacity * 255f), 0, 255);
        _sidebar.VisualOpacity = opacity;
        _chapterLabel.ForeColor = Color.FromArgb(alpha, _sidebar.TitleColor);
        _notebookText.ForeColor = Color.FromArgb(alpha, _sidebar.BodyColor);
        _hintText.ForeColor = Color.FromArgb(alpha, _sidebar.SecondaryColor);
        _hintButton.ForeColor = Color.FromArgb(alpha, Theme.Snow);
    }

    private static Label CreateCard(string heading, string body)
    {
        Label card = Theme.CreateLabel($"{heading}\n\n{body}", 13);
        card.Font = Theme.Font(13, FontStyle.Regular);
        card.ForeColor = Theme.Snow;
        card.BackColor = Color.FromArgb(230, Theme.Night);
        card.Padding = new Padding(34, 26, 34, 26);
        card.TextAlign = ContentAlignment.MiddleCenter;
        return card;
    }

    private static AtmosphereCard CreateAtmosphereCard(
        string heading,
        string body,
        AtmosphereCardStyle style,
        Rectangle bounds,
        Rectangle titleBounds,
        Rectangle bodyBounds,
        float titleSize,
        float bodySize)
    {
        AtmosphereCard card = new(heading, body, style)
        {
            Bounds = bounds
        };
        Font previousTitleFont = card.TitleLabel.Font;
        card.TitleLabel.Font = Theme.Font(titleSize, FontStyle.Bold);
        previousTitleFont.Dispose();
        Font previousBodyFont = card.BodyLabel.Font;
        card.BodyLabel.Font = Theme.Font(bodySize);
        previousBodyFont.Dispose();
        card.Arrange(titleBounds, bodyBounds);
        return card;
    }

    private static TextBox CreatePuzzleCodeEditor(
        string name,
        Rectangle bounds,
        int maxLength,
        string accessibleName,
        bool digitsOnly = true)
    {
        TextBox editor = new()
        {
            Name = name,
            Bounds = bounds,
            MaxLength = maxLength,
            Font = Theme.Font(20, FontStyle.Bold),
            ForeColor = Theme.Night,
            BackColor = Theme.PaleGold,
            BorderStyle = BorderStyle.FixedSingle,
            TextAlign = HorizontalAlignment.Center,
            TabIndex = 1,
            AccessibleName = accessibleName
        };
        if (digitsOnly)
        {
            editor.KeyPress += (_, eventArgs) =>
            {
                if (!char.IsControl(eventArgs.KeyChar) && !char.IsDigit(eventArgs.KeyChar))
                {
                    eventArgs.Handled = true;
                }
            };
        }
        return editor;
    }

    private static void SubmitOnEnter(KeyEventArgs eventArgs, Action submit)
    {
        if (eventArgs.KeyCode != Keys.Enter)
        {
            return;
        }

        eventArgs.Handled = true;
        eventArgs.SuppressKeyPress = true;
        submit();
    }

    private Button AddAction(string id, string text, Rectangle bounds, EventHandler click, int tabIndex)
    {
        return AddActionTo(_scene, id, text, bounds, click, tabIndex);
    }

    private Button AddMenuAction(string id, string text, Rectangle bounds, EventHandler click, int tabIndex)
    {
        return AddActionTo(_menuScene, id, text, bounds, click, tabIndex);
    }

    private Button AddActionTo(Control owner, string id, string text, Rectangle bounds, EventHandler click, int tabIndex)
    {
        Button button = Theme.CreateButton(text, click, tabIndex);
        button.Name = id;
        button.Bounds = bounds;
        owner.Controls.Add(button);
        if (ReferenceEquals(owner, _scene))
        {
            _inventory.BringToFront();
            _sidebar.BringToFront();
        }

        _actions.Add(id, button);
        return button;
    }

    private void SetGameChromeVisible(bool visible)
    {
        _gameChromeVisible = visible;
        _header.Visible = false;
        _progressLabel.Visible = visible;
        _timerLabel.Visible = visible;
        _topHoverZone.Visible = visible;
        _sidebar.Visible = visible;
        _scene.Visible = visible;
        if (visible)
        {
            _chromeHoverTimer.Start();
            UpdateInventory();
            BringGameChromeToFront();
        }
        else
        {
            _chromeHoverTimer.Stop();
            _inventory.Visible = false;
        }
    }

    private void BringGameChromeToFront()
    {
        _header.BringToFront();
        _progressLabel.BringToFront();
        _timerLabel.BringToFront();
        _topHoverZone.BringToFront();
    }

    private void SetHeaderRevealed(bool revealed)
    {
        if (!_gameChromeVisible)
        {
            return;
        }

        _header.Visible = revealed;
        if (revealed)
        {
            _header.BringToFront();
            _topHoverZone.BringToFront();
        }
    }

    private void UpdateHeaderRevealFromPointer()
    {
        if (!_gameChromeVisible || _confirmationOverlay is not null || IsDisposed || Disposing)
        {
            return;
        }

        Point pointer = _stage.PointToClient(Cursor.Position);
        int revealHeight = Math.Max(8, (int)Math.Round(22 * _currentScale));
        Rectangle keepOpenBounds = _header.Bounds;
        keepOpenBounds.Inflate(8, 8);
        bool reveal = pointer.Y >= 0 && pointer.Y <= revealHeight
            || _header.Visible && (keepOpenBounds.Contains(pointer) || _header.ContainsFocus);
        SetHeaderRevealed(reveal);
    }

    private static void DisposeChildren(Control owner)
    {
        Control[] children = owner.Controls.Cast<Control>().ToArray();
        owner.Controls.Clear();
        foreach (Control child in children)
        {
            child.Dispose();
        }
    }

    private void DisposeSceneContent()
    {
        Control[] children = _scene.Controls
            .Cast<Control>()
            .Where(control => control != _inventory
                && control != _sidebar
                && control != _progressLabel
                && control != _timerLabel
                && control != _topHoverZone)
            .ToArray();
        foreach (Control child in children)
        {
            _scene.Controls.Remove(child);
            child.Dispose();
        }
    }

    private void ScheduleResponsiveLayout()
    {
        if (IsHandleCreated && !IsDisposed && !Disposing)
        {
            try
            {
                BeginInvoke((Action)(() =>
                {
                    if (!IsDisposed && !Disposing)
                    {
                        RefreshResponsiveLayout();
                    }
                }));
            }
            catch (InvalidOperationException)
            {
                // The form can lose its handle while a screen transition is closing it.
            }
        }
    }

    private void RefreshResponsiveLayout()
    {
        ApplyResponsiveLayout();
    }

    private void CaptureNewControls(Control parent, ICollection<Control> captured)
    {
        foreach (Control control in parent.Controls)
        {
            if (!_baseLayout.ContainsKey(control))
            {
                _baseLayout.Add(control, new LayoutSnapshot(control.Bounds, control.Padding, control.Font.SizeInPoints, control.Font.Style));
                captured.Add(control);
            }

            if (control.HasChildren)
            {
                CaptureNewControls(control, captured);
            }
        }
    }

    private void ApplyResponsiveLayout()
    {
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return;
        }

        List<Control> capturedNewControls = [];
        CaptureNewControls(_stage, capturedNewControls);
        float nextScale = Math.Min(ClientSize.Width / (float)BaseWidth, ClientSize.Height / (float)BaseHeight);
        bool scaleChanged = Math.Abs(nextScale - _currentScale) > 0.01f;
        _currentScale = nextScale;
        int stageWidth = Math.Max(1, (int)Math.Round(BaseWidth * _currentScale));
        int stageHeight = Math.Max(1, (int)Math.Round(BaseHeight * _currentScale));
        _stage.Bounds = new Rectangle((ClientSize.Width - stageWidth) / 2, (ClientSize.Height - stageHeight) / 2, stageWidth, stageHeight);

        Control[] disposed = _baseLayout.Keys.Where(control => control.IsDisposed).ToArray();
        foreach (Control control in disposed)
        {
            _baseLayout.Remove(control);
        }

        _stage.SuspendLayout();
        IEnumerable<Control> controlsToUpdate = scaleChanged
            ? _baseLayout.Keys
            : capturedNewControls;
        foreach (Control control in controlsToUpdate)
        {
            LayoutSnapshot snapshot = _baseLayout[control];
            control.Bounds = ScaleRectangle(snapshot.Bounds, _currentScale);
            control.Padding = ScalePadding(snapshot.Padding, _currentScale);
            if (control is Label or Button or NumericUpDown or TextBoxBase)
            {
                float scaledSize = Math.Max(7f, snapshot.FontSize * _currentScale);
                if (Math.Abs(control.Font.SizeInPoints - scaledSize) > 0.05f)
                {
                    Font oldFont = control.Font;
                    control.Font = Theme.Font(scaledSize, snapshot.FontStyle);
                    oldFont.Dispose();
                }
            }

            if (control is Button button)
            {
                button.FlatAppearance.BorderSize = Math.Max(1, (int)Math.Round(_currentScale));
            }
        }

        _stage.ResumeLayout(true);
        _stage.Invalidate(true);
    }

    private static Rectangle ScaleRectangle(Rectangle rectangle, float scale)
    {
        return new Rectangle(
            (int)Math.Round(rectangle.X * scale),
            (int)Math.Round(rectangle.Y * scale),
            Math.Max(1, (int)Math.Round(rectangle.Width * scale)),
            Math.Max(1, (int)Math.Round(rectangle.Height * scale)));
    }

    private static Padding ScalePadding(Padding padding, float scale)
    {
        return new Padding(
            (int)Math.Round(padding.Left * scale),
            (int)Math.Round(padding.Top * scale),
            (int)Math.Round(padding.Right * scale),
            (int)Math.Round(padding.Bottom * scale));
    }

    private void AddHotspot(string id, string text, Rectangle bounds, EventHandler click, int tabIndex, bool completed)
    {
        InvisibleHotspotButton button = new()
        {
            Name = id,
            Text = text,
            Bounds = bounds,
            TabIndex = tabIndex,
            AccessibleName = text
        };
        button.Click += click;
        button.AccessibleDescription = completed ? "완료한 퍼즐을 다시 확인합니다" : "이 물건을 조사합니다";
        _scene.Controls.Add(button);
        _actions.Add(id, button);
        _inventory.BringToFront();
        _sidebar.BringToFront();
    }

    private void UpdateInventory()
    {
        List<string> acquired = [];
        if (PuzzleDone(PuzzleId.Lanterns))
        {
            acquired.Add("서리 금고: 숫자 각인 12");
        }

        if (PuzzleDone(PuzzleId.Melody))
        {
            acquired.Add("설구 계산대: 숫자 각인 00");
        }

        if (PuzzleDone(PuzzleId.RibbonLoom))
        {
            acquired.Add("양말 장치: 숫자 각인 25");
        }

        if (PuzzleDone(PuzzleId.LetterAcrostic))
        {
            acquired.Add("마리의 편지: 굴뚝 통로 CHIMNEY");
        }

        if (PuzzleDone(PuzzleId.ToyCipher))
        {
            acquired.Add("장난감 선반: 자정 MIDNIGHT");
        }

        if (PuzzleDone(PuzzleId.StarChart))
        {
            acquired.Add("별자리 도면: 북쪽 하늘 AURORA");
        }

        _inventoryText.Text = string.Join("\n", acquired.Chunk(2).Select(row => string.Join("     ", row)));
        _inventory.SolvedCount = acquired.Count;
        UpdateInventoryPresentation();
        _inventory.Visible = _gameChromeVisible
            && acquired.Count > 0
            && _screen is not GameScreen.Choice and not GameScreen.Ending;
        _roomButton.Visible = _screen is GameScreen.Desk or GameScreen.Letter
            or GameScreen.Lanterns or GameScreen.Melody or GameScreen.RibbonLoom
            or GameScreen.LetterAcrostic or GameScreen.ToyCipher or GameScreen.StarChart or GameScreen.Clock;
    }

    private void ToggleInventoryExpanded()
    {
        if (_inventory.SolvedCount <= 0 || !_inventory.Visible)
        {
            return;
        }

        _inventoryExpanded = !_inventoryExpanded;
        UpdateInventoryPresentation();
        _inventory.BringToFront();
        _sidebar.BringToFront();
    }

    private void UpdateInventoryPresentation()
    {
        Rectangle panelBounds = _inventoryExpanded
            ? new Rectangle(30, 78, 1040, 116)
            : new Rectangle(30, 78, 300, 58);
        SetBaseBounds(_inventory, panelBounds);
        SetBaseBounds(_inventoryTitle, new Rectangle(18, 8, _inventoryExpanded ? 300 : 264, 42));
        SetBaseBounds(_inventoryText, new Rectangle(18, 48, 1000, 58));
        _inventoryTitle.Text = _inventoryExpanded
            ? "획득한 기억, 눌러서 접기"
            : "획득한 기억, 눌러서 보기";
        _inventoryText.Visible = _inventoryExpanded;
        _inventory.Invalidate();
    }

    private void SetBaseBounds(Control control, Rectangle bounds)
    {
        if (_baseLayout.TryGetValue(control, out LayoutSnapshot snapshot))
        {
            _baseLayout[control] = snapshot with { Bounds = bounds };
            control.Bounds = ScaleRectangle(bounds, _currentScale);
            return;
        }

        control.Bounds = bounds;
    }

    private void UpdateHeader()
    {
        _progressLabel.Text = $"힌트 {_state.HintCount}  |  실패 {_state.FailedAttempts}";
        _timerLabel.Text = $"시간 {ElapsedText()}";
    }

    private string ElapsedText()
    {
        return FormatElapsed(CurrentElapsed());
    }

    private TimeSpan CurrentElapsed()
    {
        return _elapsedBeforeSession + _gameStopwatch.Elapsed;
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        return $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";
    }

    private bool PuzzleDone(PuzzleId puzzle)
    {
        return _state.SolvedPuzzles.Contains(puzzle);
    }

    private void ClearSessionMemo()
    {
        _hintSteps.Clear();
        _treeEasterEggClicks = 0;
        _treeEasterEggFound = false;
        _lastFailedStockingOrder = null;
    }

    private void ShowHint()
    {
        if (!_hintAvailable || !_hintButton.Enabled)
        {
            return;
        }

        int step = _hintSteps.GetValueOrDefault(_screen);
        string[] hints = GetHints(_screen);
        if (hints.Length == 0)
        {
            return;
        }

        int shownStep = Math.Min(step, hints.Length - 1);
        _hintText.Text = $"힌트 {shownStep + 1}/{hints.Length}\n{hints[shownStep]}";
        _hintSteps[_screen] = Math.Min(step + 1, hints.Length - 1);
        _state.RecordHint();
        UpdateHeader();
        ShowNarrativeBox();
        PlayClick();
        SaveProgressBackup(reportFailure: false);
    }

    private void UpdateHintAvailability()
    {
        if (_screen == GameScreen.Menu || _hintAvailable || GetHints(_screen).Length == 0)
        {
            return;
        }

        if (!HintRules.IsUnlocked(_screenStopwatch.Elapsed))
        {
            return;
        }

        _hintAvailable = true;
        _hintButton.Visible = true;
        _hintButton.Enabled = true;
        _hintText.Text += "\n\n힌트를 사용할 수 있습니다.";
        ShowNarrativeBox();
    }

    private void CancelSceneWork()
    {
        CancellationTokenSource cancellation = _sceneCancellation;
        _sceneCancellation = new CancellationTokenSource();
        try
        {
            cancellation.Cancel();
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    private void CancelMenuWork()
    {
        CancellationTokenSource cancellation = _menuCancellation;
        _menuCancellation = new CancellationTokenSource();
        try
        {
            cancellation.Cancel();
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    private static string[] GetHints(GameScreen screen)
    {
        return screen switch
        {
            GameScreen.Room => ["눈에 띄는 장치뿐 아니라 왼쪽 책상도 눌러 가까이 볼 수 있습니다.", "공방과 책상의 단서를 충분히 풀면 중앙 별시계가 열립니다."],
            GameScreen.Desk => ["봉투, 위쪽 장난감 선반, 왼쪽 별자리 도면이 각각 상호작용 지점입니다.", "책상의 세 문제는 마리의 편지와 영어 암호를 서로 연결합니다."],
            GameScreen.Lanterns => ["37은 12와 25를 더한 수입니다. 다음 항도 같은 방식인지 확인하세요.", "62와 99를 더하면 마지막 세 자리 수를 얻습니다."],
            GameScreen.Melody => ["첫 두 식에서 눈사람과 트리의 값을 먼저 구하세요. 세 번째 식으로 선물 값을 찾을 수 있습니다.", "눈사람 8, 트리 7, 선물 4입니다. 마지막에는 곱셈을 먼저 계산하세요."],
            GameScreen.RibbonLoom => ["‘초록 바로 오른쪽에 빨강’을 한 묶음으로 보고, 그 오른쪽에 파랑을 놓을 수 있는지 확인하세요.", "왼쪽부터 노랑, 초록, 빨강, 파랑입니다. 명찰 숫자는 1, 2, 2, 5가 됩니다."],
            GameScreen.LetterAcrostic => ["각 영어 단어를 번역하기보다 첫 글자만 순서대로 적으세요.", "C, H, I, M, N, E, Y를 이어 CHIMNEY로 입력하세요."],
            GameScreen.ToyCipher => ["제목의 13은 ROT13을 뜻합니다. A와 N, B와 O처럼 서로 바꿉니다.", "ZVQAVTUG를 ROT13으로 바꾸면 MIDNIGHT입니다."],
            GameScreen.StarChart => ["BVSPSB의 각 글자를 알파벳에서 한 칸 뒤로 옮기세요. B는 A, V는 U가 됩니다.", "여섯 글자를 모두 한 칸 뒤로 옮기면 AURORA가 됩니다."],
            GameScreen.Clock => ["여섯 조각 중 숫자가 새겨진 세 조각은 시, 분, 날짜에 대응합니다.", "12시 00분, 25일로 맞추세요."],
            GameScreen.Choice => ["두 선택 모두 결말이 있지만, 이야기의 질문은 ‘정확한 시간’과 ‘기억되는 기다림’ 중 무엇을 지킬지입니다."],
            _ => []
        };
    }

    private void ToggleSound()
    {
        _soundEnabled = !_soundEnabled;
        _music.Enabled = _soundEnabled;
        _soundButton.Text = _soundEnabled ? "소리 켬" : "소리 끔";
        if (_soundEnabled)
        {
            PlayClick();
        }
    }

    private void PlayClick()
    {
        PlaySound(GameSound.Click);
    }

    private void PlayMechanismTone(int position)
    {
        GameSound sound = position switch
        {
            0 => GameSound.NoteMoon,
            1 => GameSound.NoteTree,
            2 => GameSound.NoteBell,
            3 => GameSound.NoteStar,
            _ => throw new ArgumentOutOfRangeException(nameof(position))
        };
        PlaySound(sound);
    }

    private void PlaySound(GameSound sound)
    {
        if (_soundEnabled)
        {
            _sounds.Play(sound);
        }
    }

    private void ToggleFullscreen()
    {
        SuspendLayout();
        if (_fullscreen)
        {
            WindowState = FormWindowState.Normal;
            FormBorderStyle = FormBorderStyle.Sizable;
            UpdateWindowCloseAvailability();
            ClientSize = new Size(BaseWidth, BaseHeight);
            StartPosition = FormStartPosition.CenterScreen;
            CenterToScreen();
            _fullscreen = false;
        }
        else
        {
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            _fullscreen = true;
        }

        ResumeLayout(true);
        ApplyResponsiveLayout();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Alt | Keys.F4))
        {
            Close();
            return true;
        }

        if (keyData == Keys.F1)
        {
            ShowHint();
            return true;
        }

        if (keyData == (Keys.Control | Keys.N))
        {
            RequestNewGame();
            return true;
        }

        if (keyData == Keys.F11)
        {
            ToggleFullscreen();
            return true;
        }

        if (keyData == Keys.Escape && _screen is GameScreen.Desk or GameScreen.Letter
            or GameScreen.Lanterns or GameScreen.Melody or GameScreen.RibbonLoom
            or GameScreen.LetterAcrostic or GameScreen.ToyCipher or GameScreen.StarChart or GameScreen.Clock)
        {
            ShowRoom();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void PrepareForShutdown()
    {
        if (_shutdownPrepared)
        {
            return;
        }

        _shutdownPrepared = true;
        ErrorReporter.BeginShutdown();
        _elapsedTimer.Stop();
        _narrativeFadeTimer.Stop();
        _transitionTimer.Stop();
        _chromeHoverTimer.Stop();
        _startupFadeTimer.Stop();
        _gameStopwatch.Stop();
        _screenStopwatch.Stop();
        _sceneCancellation.Cancel();
        _menuCancellation.Cancel();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_resourcesDisposed)
        {
            _resourcesDisposed = true;
            PrepareForShutdown();
            _elapsedTimer.Dispose();
            _narrativeFadeTimer.Dispose();
            _transitionTimer.Dispose();
            _chromeHoverTimer.Dispose();
            _startupFadeTimer.Dispose();
            _sceneCancellation.Dispose();
            _menuCancellation.Dispose();
            _sounds.Dispose();
            _music.Dispose();
            _leaderboardService.Dispose();
            _images.Dispose();
        }

        base.Dispose(disposing);
    }
}
