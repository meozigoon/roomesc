namespace ThirteenthBell;

internal sealed partial class GameForm
{
    private void ShowCreatorEasterEgg()
    {
        bool rewarded = _state.TryClaimCreatorHint();
        ShowNarrativeMessage("제작자 정보\n기획 및 방향 제시: 사용자\n스토리, 퍼즐 설계와 구현: Codex\n배경 원화: 이 프로젝트를 위해 제작\n"
            + (rewarded ? "숨겨진 기록을 발견하셨습니다. 힌트 1개를 받았습니다." : "이 게임에서 힌트 보상을 이미 받았습니다."));
        ShowNarrativeBox(progression: true);
        UpdateHeader();
        UpdateHintAvailability();
        SaveProgressBackup(reportFailure: false);
    }
    private static readonly string[] RankingHeadings = ["순위", "닉네임", "시간 / 결과"];
    private static readonly string[] AcrosticHeadings = ["조각 번호", "영어 단어", "바늘땀 수"];
    private static readonly string[][] AcrosticRows =
    [
        ["6", "ENVELOPE", "4"], ["2", "NORTH", "5"], ["7", "TOY", "3"],
        ["1", "CLOCK", "1"], ["5", "WINTER", "3"], ["3", "ICICLE", "3"], ["4", "CHIMNEY", "4"]
    ];
    private static void AddAlignedTable(Control parent, string prefix, string[] headings,
        string[][] rows, Rectangle bounds, int firstWidth, int secondWidth, Color color,
        bool ellipsizeName = false)
    {
        int rowHeight = bounds.Height / (rows.Length + 1);
        for (int row = -1; row < rows.Length; row++)
        {
            string[] values = row < 0 ? headings : rows[row];
            int left = bounds.Left;
            for (int column = 0; column < 3; column++)
            {
                int width = column == 0 ? firstWidth : column == 1 ? secondWidth : bounds.Width - firstWidth - secondWidth;
                AlignedTextLabel label = new()
                {
                    Name = row < 0 ? $"{prefix}Heading{column}" : $"{prefix}Row{row}Column{column}",
                    Text = values[column],
                    Bounds = new Rectangle(left, bounds.Top + (row + 1) * rowHeight, width, rowHeight),
                    Font = Theme.Font(row < 0 ? 10 : 12, row < 0 ? FontStyle.Bold : FontStyle.Regular),
                    ForeColor = color,
                    BackColor = Color.Transparent,
                    AutoSize = false,
                    AutoEllipsis = column == 1 && ellipsizeName && row >= 0,
                    TextAlign = column == 1 && ellipsizeName ? ContentAlignment.MiddleLeft
                        : column == 2 && ellipsizeName ? ContentAlignment.MiddleRight : ContentAlignment.MiddleCenter,
                    AccessibleName = $"{headings[column]}: {values[column]}"
                };
                parent.Controls.Add(label);
                left += width;
            }
        }
    }
}
