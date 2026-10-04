using System.Text.Json;
using ThirteenthBell.Core;

namespace ThirteenthBell;

internal static partial class PostalRoomSmoke
{
    private static void CheckMenuPresentation(GameForm form)
    {
        Call(form, "ShowMainMenu", false);
        Check(form.Controls.Find("MainClockLogo", true).Length == 0, "main menu large logo removed");
        SceneCanvas menu = Get<SceneCanvas>(form, "_menuScene");
        ImageBank images = Get<ImageBank>(form, "_images");
        Check(ReferenceEquals(menu.SceneImage, images["christmas-village-aerial.png"]), "main menu uses generated village background");
        foreach (Size size in new[] { new Size(1400, 820), new Size(1000, 600) })
        {
            form.ClientSize = size;
            Call(form, "ApplyResponsiveLayout");
            foreach (Button button in Get<Dictionary<string, Button>>(form, "_actions").Values)
            {
                Check(button.Visible && button.Enabled && menu.ClientRectangle.Contains(button.Bounds), "menu action reachable at " + size.Width + ": " + button.Text);
            }
            Capture(form, "main-menu-" + size.Width);
        }
        Click(form, "menu_guide");
        Check(ReferenceEquals(menu.SceneImage, images["christmas-village-aerial.png"]), "guide keeps menu background");
        CheckVisibleText(form, "guide");
        Click(form, "menu_back");
        Click(form, "menu_credits");
        CheckVisibleText(form, "credits");
        Click(form, "menu_back");
        Call(form, "StartNewGame");
        Click(form, "continue");
        Call(form, "MovePostalProp", 0);
        Check(Get<Label>(form, "_notebookText").Text.Contains("밀었습니다.", StringComparison.Ordinal), "postal observation uses polite Korean");
        CheckTextFits(form, "NotebookText");
        Capture(form, "polite-postal-inspection");
        Call(form, "MovePostalProp", 1);
        Call(form, "TakePostalKey");
        Call(form, "MovePostalProp", 2);
        CheckTextFits(form, "NotebookText");
        Set(form, "_postal", PostalRoomProgress.CompletedLegacyRoom());
        Call(form, "ShowRoom");
        Call(form, "InspectRoomPoint", new Point(1370, 300));
        Check(Get<Label>(form, "_notebookText").Text.StartsWith("황동 종입니다.", StringComparison.Ordinal), "workshop observation uses polite Korean");
        CheckTextFits(form, "NotebookText");
        Capture(form, "polite-workshop-inspection");
        Set(form, "_gameInProgress", false);
        Set(form, "_allowClose", true);
        form.Close();
        File.WriteAllText(Path.Combine(_output, "menu-result.json"), JsonSerializer.Serialize(new { passed = true, checks = _checks, screenshots = 4 }, ResultJsonOptions));
    }
}
