using ThirteenthBell.Core;

namespace ThirteenthBell;

internal sealed partial class GameForm
{
    private void ShowFullLeaderboard()
    {
        ShowMenuPage("온라인 순위 전체 보기", "전체 기록을 불러오는 중입니다.", 13);
        Label status = _menuScene.Controls.OfType<Label>().Single();
        status.Name = "LeaderboardStatus";
        _ = LoadFullLeaderboardAsync(status, _menuCancellation.Token);
    }

    private async Task LoadFullLeaderboardAsync(Label status, CancellationToken cancellationToken)
    {
        try
        {
            LeaderboardLoadResult result = await _leaderboardService.LoadAllAsync(cancellationToken).ConfigureAwait(true);
            if (cancellationToken.IsCancellationRequested || status.IsDisposed)
            {
                return;
            }
            if (!result.Succeeded || result.Entries.Count == 0)
            {
                status.Text = result.Error ?? "아직 등록된 클리어 기록이 없습니다.";
                ArrangeAdaptiveText();
                return;
            }
            _textCardBounds.Remove(status);
            _baseLayout.Remove(status);
            _menuScene.Controls.Remove(status);
            status.Dispose();
            ListView list = new()
            {
                Name = "FullLeaderboard",
                Bounds = new Rectangle(285, 145, 830, 455),
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false,
                BackColor = Theme.Night,
                ForeColor = Theme.Snow,
                Font = Theme.Font(13),
                AccessibleName = "전체 온라인 순위 목록, 아래로 스크롤하여 모든 기록 확인"
            };
            list.Columns.Add("순위", 124);
            list.Columns.Add("닉네임", 440);
            list.Columns.Add("시간 / 결과", 230, HorizontalAlignment.Right);
            list.SizeChanged += (_, _) =>
            {
                if (list.Disposing || list.IsDisposed || list.Columns.Count != 3)
                {
                    return;
                }
                list.Columns[0].Width = Math.Max(40, list.ClientSize.Width * 15 / 100);
                list.Columns[1].Width = Math.Max(80, list.ClientSize.Width * 53 / 100);
                list.Columns[2].Width = Math.Max(60, list.ClientSize.Width * 28 / 100);
            };
            list.BeginUpdate();
            for (int index = 0; index < result.Entries.Count; index++)
            {
                LeaderboardEntry entry = result.Entries[index];
                list.Items.Add(new ListViewItem([ $"{(entry.Failed ? result.Entries.Count : index + 1)}위", entry.Nickname, FormatLeaderboardResult(entry) ]));
            }
            list.EndUpdate();
            _menuScene.Controls.Add(list);
            Label heading = Theme.CreateLabel($"온라인 순위 전체 보기 ({result.Entries.Count}명)", 18, FontStyle.Bold);
            heading.Bounds = new Rectangle(285, 85, 830, 45);
            heading.TextAlign = ContentAlignment.MiddleCenter;
            _menuScene.Controls.Add(heading);
            SetBaseBounds(_actions["menu_back"], new Rectangle(500, 635, 400, 70));
            ApplyResponsiveLayout();
        }
        catch (OperationCanceledException)
        {
            // Returning to the menu cancels the read request.
        }
        catch (Exception exception)
        {
            if (!status.IsDisposed)
            {
                status.Text = "전체 순위를 표시하지 못했습니다. 메인 메뉴에서 다시 시도해 주세요.";
                ArrangeAdaptiveText();
            }
            ErrorReporter.Report(exception, "Loading full leaderboard", false);
        }
    }
}
