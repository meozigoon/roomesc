using System.Drawing.Imaging;
using System.Text.Json;
using ThirteenthBell.Core;

namespace ThirteenthBell;

internal static partial class PostalRoomSmoke
{
    private static void CheckArtworkRefinement(GameForm form)
    {
        OfflineLeaderboard service = (OfflineLeaderboard)Get<ILeaderboardService>(form, "_leaderboardService");
        service.EntryCount = 8;
        service.NicknamePrefix = "아주긴닉네임가나다라마바사";
        Set(form, "_postal", PostalRoomProgress.CompletedLegacyRoom());
        foreach (Size size in new[] { new Size(1400, 820), new Size(1000, 600), new Size(1680, 1000) })
        {
            form.ClientSize = size;
            Call(form, "ShowRoom");
            Control scene = Get<Control>(form, "_scene");
            float scale = scene.Width / 1400f;
            Button safe = Get<Dictionary<string, Button>>(form, "_actions")["hotspot_lantern"];
            Button desk = Get<Dictionary<string, Button>>(form, "_actions")["hotspot_desk"];
            Check(safe.Top > desk.Bottom && !safe.Bounds.IntersectsWith(desk.Bounds), "safe region below desktop " + size.Width);
            Check(scene.GetChildAtPoint(new Point((int)(240 * scale), (int)(550 * scale))) == safe, "safe artwork clicks safe " + size.Width);
            Capture(form, "artwork-room-" + size.Width);
            Click(form, "hotspot_lantern");
            Check(Get<GameScreen>(form, "_screen") == GameScreen.Lanterns, "safe opens combination puzzle");
            Capture(form, "artwork-safe-" + size.Width);
            Call(form, "ShowDesk");
            Capture(form, "artwork-desk-" + size.Width);
            Check(scene.GetChildAtPoint(new Point((int)(715 * scale), (int)(540 * scale)))?.Name == "desk_letter", "restored envelope is clickable");
            Check(scene.GetChildAtPoint(new Point((int)(330 * scale), (int)(520 * scale)))?.Name == "desk_chart", "restored chart is clickable");
            Call(form, "ShowLetterAcrosticPuzzle");
            AtmosphereCard record = (AtmosphereCard)form.Controls.Find("LetterAcrosticRecord", true).Single();
            for (int column = 0; column < 3; column++)
            {
                Label[] cells = Enumerable.Range(0, 7).Select(row => (Label)record.Controls.Find($"AcrosticRow{row}Column{column}", false).Single()).ToArray();
                Check(cells.Select(cell => cell.Left).Distinct().Count() == 1 && cells.Select(cell => cell.Width).Distinct().Count() == 1, "stitch column aligned " + column);
                Check(cells.All(cell => cell.TextAlign == ContentAlignment.MiddleCenter), "stitch column centered " + column);
                Check(cells.Zip(cells.Skip(1)).All(pair => Math.Abs(pair.First.Bottom - pair.Second.Top) <= 1), "stitch row spacing equal within pixel rounding " + column);
            }
            Check(Get<Label>(form, "_notebookText").Text == "조각마다 번호와 영어 단어, 바늘땀이 남아 있습니다. 각 단어의 왼쪽에서 바늘땀 수만큼 세어 글자 하나를 골라 주세요. 조각 번호를 맞추면 엘리아스가 남긴 배달 통로를 찾을 수 있습니다.", "exact requested stitch instructions");
            CheckVisibleText(form, "stitch alignment");
            Capture(form, "artwork-stitches-" + size.Width);
            Call(form, "ShowIntro");
            Control letter = form.Controls.Find("LetterDocument", true).Single();
            Check(letter.Top > Get<Label>(form, "_progressLabel").Bottom, "intro letter corner below status line");
            Check(scene.ClientRectangle.Contains(letter.Bounds), "intro letter within scene");
            CheckVisibleText(form, "intro letter");
            Capture(form, "artwork-intro-" + size.Width);
            Call(form, "ShowMainMenu", false);
            Label ranking = (Label)form.Controls.Find("LeaderboardText", true).Single();
            for (int column = 0; column < 3; column++)
            {
                Label[] cells = Enumerable.Range(0, 5).Select(row => (Label)ranking.Controls.Find($"RankingRow{row}Column{column}", false).Single()).ToArray();
                Check(cells.Select(cell => cell.Left).Distinct().Count() == 1, "ranking column aligned " + column);
                Check(cells.All(cell => cell.Top >= 0 && ranking.ClientRectangle.Contains(cell.Bounds)), "ranking cells inside table " + column);
            }
            Label name = (Label)ranking.Controls.Find("RankingRow0Column1", false).Single();
            Check(name.AutoEllipsis && !name.AutoSize && name.TextAlign == ContentAlignment.MiddleLeft, "nickname left aligned with ellipsis");
            Check(TextRenderer.MeasureText(name.Text, name.Font).Width > name.Width, "long nickname fixture actually exceeds column width");
            Capture(form, "artwork-ranking-" + size.Width);
            Click(form, "leaderboard_all");
            ListView full = (ListView)form.Controls.Find("FullLeaderboard", true).Single();
            Check(full.Columns[1].TextAlign == HorizontalAlignment.Left && full.Columns[2].TextAlign == HorizontalAlignment.Right, "full ranking name and result column alignments");
            Call(form, "ShowGuideMenu");
            AtmosphereCard guide = (AtmosphereCard)form.Controls.Find("GuideDocument", true).Single();
            Check(guide.TitleLabel.Text == "게임 방법" && guide.TitleLabel.Font.Size > guide.BodyLabel.Font.Size && guide.TitleLabel.Font.Bold, "guide separate large bold heading");
            Check(guide.BodyLabel.Text.Split("\n\n").Length == 2 && !guide.BodyLabel.Text.Contains("배경의 사물을 눌러", StringComparison.Ordinal), "guide second paragraph removed");
            CheckGuideButton(form);
            CheckVisibleText(form, "new guide");
            Capture(form, "artwork-guide-" + size.Width);
            Call(form, "ShowMainMenu", false);
            Click(form, "menu_exit");
            SceneCanvas overlay = (SceneCanvas)Get<Panel>(form, "_confirmationOverlay");
            Check(ReferenceEquals(overlay.SceneImage, Get<ImageBank>(form, "_images")["exit-background.png"]), "exit uses generated background");
            Control card = form.Controls.Find("ConfirmationCard", true).Single();
            Check(form.Controls.Find("ConfirmationError", true).Single().Parent == card, "confirmation error keeps paper parent");
            foreach (Button button in overlay.Controls.OfType<Button>())
            {
                Check(button.Top > card.Bottom && overlay.ClientRectangle.Contains(button.Bounds), "exit buttons below card and inside screen");
            }
            CheckVisibleText(form, "new exit");
            Capture(form, "artwork-exit-" + size.Width);
            form.Activate();
            Pump();
            if (Form.ActiveForm == form && size.Width == 1000)
            {
                using Bitmap desktop = new(form.ClientSize.Width, form.ClientSize.Height);
                using Graphics graphics = Graphics.FromImage(desktop);
                graphics.CopyFromScreen(form.PointToScreen(Point.Empty), Point.Empty, desktop.Size);
                desktop.Save(Path.Combine(_output, "artwork-exit-desktop.png"), ImageFormat.Png);
            }
            Click(form, "confirmation_cancel");
            Check(Get<Panel?>(form, "_confirmationOverlay") is null && Get<Control>(form, "_menuScene").Enabled, "exit cancel restores menu");
        }
        service.NicknamePrefix = "Tester";
        service.EntryCount = 0;
        File.WriteAllText(Path.Combine(_output, "artwork-result.json"), JsonSerializer.Serialize(new { passed = true, checks = _checks }, ResultJsonOptions));
        Console.WriteLine($"ARTWORK_REFINEMENT_OK checks={_checks}");
    }
}
