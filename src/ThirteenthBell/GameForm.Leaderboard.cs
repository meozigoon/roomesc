using ThirteenthBell.Core;

namespace ThirteenthBell;

internal sealed partial class GameForm
{
    private void ShowFullLeaderboard(string? notice = null)
    {
        CancelMenuWork();
        ShowMenuPage("온라인 순위 전체 보기", "전체 기록을 불러오는 중입니다.", 13);
        Label status = _menuScene.Controls.OfType<Label>().Single();
        status.Name = "LeaderboardStatus";
        _ = LoadFullLeaderboardAsync(status, _menuCancellation.Token, notice);
    }

    private async Task LoadFullLeaderboardAsync(Label status, CancellationToken cancellationToken, string? notice = null)
    {
        try
        {
            LeaderboardLoadResult result = await _leaderboardService.LoadAllAsync(cancellationToken).ConfigureAwait(true);
            if (cancellationToken.IsCancellationRequested || status.IsDisposed)
            {
                return;
            }
            if (!result.Succeeded)
            {
                status.Text = string.Join("\n", new[] { notice, result.Error }.Where(text => !string.IsNullOrEmpty(text)));
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
                Bounds = new Rectangle(285, 145, 830, 350),
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = true,
                CheckBoxes = true,
                HideSelection = false,
                BackColor = Theme.Night,
                ForeColor = Theme.Snow,
                Font = Theme.Font(13),
                TabIndex = 1,
                AccessibleName = "전체 온라인 순위 목록",
                AccessibleDescription = "삭제할 기록의 체크박스를 선택하세요. 스페이스 키로 선택을 바꿀 수 있습니다."
            };
            list.Columns.Add("선택 / 순위", 124);
            list.Columns.Add("닉네임", 440);
            list.Columns.Add("시간 / 결과", 230, HorizontalAlignment.Right);
            list.SizeChanged += (_, _) =>
            {
                if (list.Disposing || list.IsDisposed || list.Columns.Count != 3)
                {
                    return;
                }
                list.Columns[0].Width = Math.Max(40, list.ClientSize.Width * 20 / 100);
                list.Columns[1].Width = Math.Max(80, list.ClientSize.Width * 48 / 100);
                list.Columns[2].Width = Math.Max(60, list.ClientSize.Width * 28 / 100);
            };
            list.BeginUpdate();
            for (int index = 0; index < result.Entries.Count; index++)
            {
                LeaderboardEntry entry = result.Entries[index];
                list.Items.Add(new ListViewItem([$"{(entry.Failed ? result.Entries.Count : index + 1)}위", entry.Nickname, FormatLeaderboardResult(entry)])
                {
                    Tag = entry
                });
            }
            list.EndUpdate();
            _menuScene.Controls.Add(list);
            Label heading = Theme.CreateLabel($"온라인 순위 전체 보기 ({result.Entries.Count}명)", 18, FontStyle.Bold);
            heading.Bounds = new Rectangle(285, 85, 830, 45);
            heading.TextAlign = ContentAlignment.MiddleCenter;
            _menuScene.Controls.Add(heading);
            Label selection = Theme.CreateLabel(string.Empty, 11);
            selection.Name = "LeaderboardSelectionStatus";
            selection.Bounds = new Rectangle(285, 505, 830, 55);
            selection.TextAlign = ContentAlignment.MiddleCenter;
            _menuScene.Controls.Add(selection);
            bool changingSelection = false;
            Button selectAll = AddMenuAction("leaderboard_select_all", "전체 선택", new Rectangle(285, 570, 175, 44), (_, _) => SetChecked(true), 2);
            Button clear = AddMenuAction("leaderboard_clear_selection", "선택 해제", new Rectangle(470, 570, 175, 44), (_, _) => SetChecked(false), 3);
            Button delete = AddMenuAction("leaderboard_delete", "선택 기록 삭제", new Rectangle(655, 570, 250, 44), (_, _) => DeleteCheckedLeaderboardRecords(list), 4);
            AddMenuAction("leaderboard_refresh", "새로고침", new Rectangle(915, 570, 200, 44), (_, _) => ShowFullLeaderboard(), 5);
            _actions["menu_back"].TabIndex = 6;
            selectAll.Click += (_, _) => UpdateSelection();
            clear.Click += (_, _) => UpdateSelection();

            void SetChecked(bool check)
            {
                changingSelection = true;
                list.BeginUpdate();
                foreach (ListViewItem item in list.Items)
                {
                    item.Checked = check && item.Tag is LeaderboardEntry { Id: not null };
                }
                list.EndUpdate();
                changingSelection = false;
            }

            void UpdateSelection()
            {
                int count = list.CheckedItems.Count;
                string summary = result.Entries.Count == 0
                    ? "등록된 순위 기록이 없습니다."
                    : $"{count}개 선택됨. 삭제할 기록의 체크박스를 선택하세요. (최대 1,000개)";
                selection.Text = notice is null ? summary : notice + "\n" + summary;
                delete.Enabled = count is > 0 and <= 1000;
                clear.Enabled = count > 0;
                selectAll.Enabled = result.Entries.Any(entry => entry.Id is not null);
            }

            list.ItemCheck += (_, e) =>
            {
                if (list.Items[e.Index].Tag is not LeaderboardEntry { Id: not null })
                {
                    e.NewValue = CheckState.Unchecked;
                }
            };
            list.ItemChecked += (_, _) =>
            {
                if (!changingSelection)
                {
                    UpdateSelection();
                }
            };
            UpdateSelection();
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

    private void DeleteCheckedLeaderboardRecords(ListView list)
    {
        Guid[] ids = list.CheckedItems.Cast<ListViewItem>()
            .Select(item => (item.Tag as LeaderboardEntry)?.Id)
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        if (ids.Length is < 1 or > 1000)
        {
            return;
        }
        using LeaderboardDeleteDialog dialog = new(ids.Length,
            (password, token) => _leaderboardService.DeleteAsync(ids, password, token));
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            ShowFullLeaderboard(dialog.DeletedCount == 0
                ? "선택한 기록은 이미 삭제되었습니다. 최신 목록을 불러왔습니다."
                : $"선택한 기록 중 {dialog.DeletedCount}개를 삭제했습니다.");
        }
    }
}
