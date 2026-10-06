namespace ThirteenthBell;

internal sealed class LeaderboardDeleteDialog : Form
{
    private readonly TextBox _password;
    private readonly Label _status;
    private readonly Button _delete;
    private readonly Button _cancel;
    private readonly Func<string, CancellationToken, Task<LeaderboardDeleteResult>> _deleteRecords;
    private bool _busy;

    public int DeletedCount { get; private set; }

    public LeaderboardDeleteDialog(int selectedCount,
        Func<string, CancellationToken, Task<LeaderboardDeleteResult>> deleteRecords)
    {
        _deleteRecords = deleteRecords;
        Text = "선택한 순위 기록 삭제";
        Name = "LeaderboardDeleteDialog";
        ClientSize = new Size(620, 335);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Theme.Night;
        ForeColor = Theme.Snow;
        Label instruction = Theme.CreateLabel(
            $"선택한 기록 {selectedCount}개를 삭제합니다.\n삭제한 기록은 복구할 수 없습니다.\n계속하려면 삭제 비밀번호를 입력하세요.", 12);
        instruction.Bounds = new Rectangle(24, 20, 572, 100);
        Controls.Add(instruction);
        _password = new TextBox
        {
            Name = "LeaderboardDeletePassword",
            Bounds = new Rectangle(24, 135, 572, 38),
            UseSystemPasswordChar = true,
            MaxLength = 72,
            Font = Theme.Font(13),
            BackColor = Theme.Panel,
            ForeColor = Theme.Snow,
            TabIndex = 0,
            AccessibleName = "순위 기록 삭제 비밀번호"
        };
        Controls.Add(_password);
        _status = Theme.CreateLabel(string.Empty, 10);
        _status.Name = "LeaderboardDeleteStatus";
        _status.Bounds = new Rectangle(24, 183, 572, 66);
        Controls.Add(_status);
        _delete = Theme.CreateButton("삭제", async (_, _) => await DeleteAsync().ConfigureAwait(true), 1);
        _delete.Name = "LeaderboardDeleteConfirm";
        _delete.Bounds = new Rectangle(24, 267, 275, 48);
        Controls.Add(_delete);
        _cancel = Theme.CreateButton("취소", (_, _) => Close(), 2);
        _cancel.DialogResult = DialogResult.Cancel;
        _cancel.Bounds = new Rectangle(321, 267, 275, 48);
        Controls.Add(_cancel);
        AcceptButton = _delete;
        CancelButton = _cancel;
        Shown += (_, _) => _password.Focus();
    }

    private async Task DeleteAsync()
    {
        if (_busy)
        {
            return;
        }
        if (_password.Text.Length == 0)
        {
            _status.Text = "삭제 비밀번호를 입력하세요.";
            _password.Focus();
            return;
        }
        _busy = true;
        _delete.Enabled = false;
        _cancel.Enabled = false;
        _password.Enabled = false;
        _status.Text = "비밀번호를 확인하고 선택한 기록을 삭제하는 중입니다.";
        string password = _password.Text;
        _password.Clear();
        LeaderboardDeleteResult result;
        try
        {
            result = await _deleteRecords(password, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            result = new LeaderboardDeleteResult(false, 0, "삭제 결과를 확인하지 못했습니다. 창을 닫고 새로고침 후 기록을 확인하세요.");
        }
        finally
        {
            password = string.Empty;
            _busy = false;
            _delete.Enabled = true;
            _cancel.Enabled = true;
            _password.Enabled = true;
        }
        if (result.Succeeded)
        {
            DeletedCount = result.DeletedCount;
            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            _status.Text = result.Error;
            _password.Focus();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_busy)
        {
            e.Cancel = true;
        }
        base.OnFormClosing(e);
    }
}
