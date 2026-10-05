using System.Diagnostics;
using System.Drawing.Imaging;
using System.Text.Json;

namespace ThirteenthBell;

internal static partial class PostalRoomSmoke
{
    private static readonly Size[] SnowWindowSizes = [new(1000, 600), new(1400, 820), new(1680, 1000)];

    private static void CheckSnowAndLoading(GameForm form, Stopwatch startup, double formCreationMs)
    {
        SceneCanvas menu = Get<SceneCanvas>(form, "_menuScene");
        Check(menu.SnowfallEnabled && !Get<SceneCanvas>(form, "_scene").SnowfallEnabled, "snow enabled only on menu background");
        System.Windows.Forms.Timer animation = (System.Windows.Forms.Timer)typeof(SceneCanvas).GetField("_snowfallTimer", PrivateInstance)!.GetValue(menu)!;
        List<double> heartbeatGaps = [];
        Stopwatch heartbeat = Stopwatch.StartNew();
        double previousTick = 0;
        using System.Windows.Forms.Timer probe = new() { Interval = 16 };
        probe.Tick += (_, _) =>
        {
            double now = heartbeat.Elapsed.TotalMilliseconds;
            heartbeatGaps.Add(now - previousTick);
            previousTick = now;
        };
        Pump();
        Check(animation.Enabled, "snow timer active during title hold");
        Capture(form, "snow-startup-title");
        heartbeat.Restart();
        probe.Start();
        while (!Get<bool>(form, "_startupSequenceCompleted") && startup.Elapsed < TimeSpan.FromSeconds(20))
        {
            Application.DoEvents();
            Thread.Sleep(1);
        }
        Check(Get<bool>(form, "_startupSequenceCompleted"), "startup completes while processing messages");
        probe.Stop();
        double startupMs = startup.Elapsed.TotalMilliseconds;
        Check(heartbeatGaps.Count > 30, "UI keeps processing timer messages throughout startup");
        Check(startupMs >= 4000, "four-second title and creator hold preserved");
        Check(menu.SceneImage is not null, "asynchronously prepared village background applied");

        // Verify actual alpha composition and motion without system timer randomness.
        using SnowfallEffect effect = new();
        using Bitmap first = new(1000, 600);
        using Bitmap second = new(1000, 600);
        using (Graphics graphics = Graphics.FromImage(first))
        {
            graphics.Clear(Color.Black);
            effect.Draw(graphics, first.Size);
        }
        for (int index = 0; index < 20; index++)
        {
            effect.Advance(0.05);
        }
        using (Graphics graphics = Graphics.FromImage(second))
        {
            graphics.Clear(Color.Black);
            effect.Draw(graphics, second.Size);
        }
        int snowPixels = 0;
        int changedPixels = 0;
        int maximumWhite = 0;
        for (int y = 0; y < first.Height; y++)
        {
            for (int x = 0; x < first.Width; x++)
            {
                Color before = first.GetPixel(x, y);
                Color after = second.GetPixel(x, y);
                if (before.B > 0)
                {
                    snowPixels++;
                    maximumWhite = Math.Max(maximumWhite, before.B);
                }
                if (before != after)
                {
                    changedPixels++;
                }
            }
        }
        Check(snowPixels > 100 && maximumWhite > 20 && maximumWhite < 230, "snow is visible and alpha blends below opaque white");
        Check(changedPixels > 100, "snow positions change over time");
        double effectTime = (double)typeof(SnowfallEffect).GetField("_elapsedSeconds", PrivateInstance)!.GetValue(effect)!;
        effect.Advance(10);
        double afterDelay = (double)typeof(SnowfallEffect).GetField("_elapsedSeconds", PrivateInstance)!.GetValue(effect)!;
        Check(Math.Abs(afterDelay - effectTime - 0.065) < 0.0001, "busy frame never produces a ten-second motion jump");
        List<object> paintResults = [];
        foreach (Size size in SnowWindowSizes)
        {
            form.ClientSize = size;
            Pump();
            Check(animation.Enabled && menu.ClientSize.Width > 0, "resizing preserves snow at " + size.Width);
            Capture(form, "snow-menu-first-" + size.Width);
            Thread.Sleep(120);
            Pump();
            Capture(form, "snow-menu-next-" + size.Width);
            using Bitmap frame = new(menu.Width, menu.Height, PixelFormat.Format32bppPArgb);
            using Graphics graphics = Graphics.FromImage(frame);
            menu.PaintBackdrop(graphics);
            double[] timings = new double[30];
            for (int index = 0; index < timings.Length; index++)
            {
                Stopwatch paint = Stopwatch.StartNew();
                menu.PaintBackdrop(graphics);
                timings[index] = paint.Elapsed.TotalMilliseconds;
            }
            Array.Sort(timings);
            paintResults.Add(new { width = size.Width, height = size.Height, medianBackdropMs = timings[15], p95BackdropMs = timings[28] });
            Check(timings[15] < 33, "cached background and snow fit 33ms frame budget at " + size.Width);
            CheckVisibleText(form, "snow menu " + size.Width);
            Check(Get<Dictionary<string, Button>>(form, "_actions").Values.All(button => button.Visible && button.Enabled), "snow preserves menu buttons");
        }
        Click(form, "menu_guide");
        Check(animation.Enabled, "snow continues behind game guide");
        CheckVisibleText(form, "snow guide");
        Click(form, "menu_back");
        Call(form, "StartNewGame");
        Check(!animation.Enabled, "snow timer stops when entering the game");
        Call(form, "ShowMainMenu", false);
        Check(animation.Enabled, "snow resumes when returning to menu");
        menu.Visible = false;
        Check(!animation.Enabled, "hidden menu stops animation work");
        menu.Visible = true;
        Check(animation.Enabled, "visible menu restarts animation");
        menu.SnowfallEnabled = false;
        Check(!animation.Enabled, "disabling snow stops its timer");
        menu.SnowfallEnabled = true;
        Call(form, "PrepareForShutdown");
        Check(!animation.Enabled, "shutdown stops animation timer");
        heartbeatGaps.Sort();
        File.WriteAllText(Path.Combine(_output, "snow-loading-result.json"), JsonSerializer.Serialize(new
        {
            passed = true, checks = _checks, startupMs, formCreationMs,
            startupHeartbeatCount = heartbeatGaps.Count,
            startupHeartbeatP95Ms = heartbeatGaps[(int)(heartbeatGaps.Count * 0.95)],
            startupHeartbeatMaxMs = heartbeatGaps[^1],
            snowParticles = 120, snowAlpha = "48, 78, 112 of 255", snowPixels, changedPixels,
            paintResults, titleHoldMs = 4000,
            note = "Heartbeat is measured after initial title capture and excludes that synchronous test capture. Backdrop timings exclude child controls and do not prove monitor frame rate."
        }, ResultJsonOptions));
        Console.WriteLine($"SNOW_LOADING_OK checks={_checks} startupMs={startupMs:F1} heartbeatMaxMs={heartbeatGaps[^1]:F1}");
    }
}
