namespace ThirteenthBell.Core;

public sealed class PostalRoomProgress
{
    public bool SuitcaseMoved { get; set; }

    public bool ParcelMoved { get; set; }

    public bool BlanketMoved { get; set; }

    public bool KeyFound { get; set; }

    public bool DrawerOpened { get; set; }

    public bool BellClueFound { get; set; }

    public bool RouteSolved { get; set; }

    public bool BellsSolved { get; set; }

    public bool DoorOpened { get; set; }

    public bool KeyVisible => SuitcaseMoved && ParcelMoved && !KeyFound;

    public bool CanOpenDoor => KeyFound && RouteSolved && BellsSolved;

    public bool IsValid()
    {
        return (!DrawerOpened || KeyFound)
            && (!RouteSolved || DrawerOpened)
            && (!BellsSolved || BellClueFound)
            && (!DoorOpened || CanOpenDoor);
    }

    public static PostalRoomProgress CompletedLegacyRoom()
    {
        return new PostalRoomProgress
        {
            SuitcaseMoved = true,
            ParcelMoved = true,
            BlanketMoved = true,
            KeyFound = true,
            DrawerOpened = true,
            BellClueFound = true,
            RouteSolved = true,
            BellsSolved = true,
            DoorOpened = true
        };
    }
}

public static class PostalPuzzleRules
{
    public const string RouteCode = "83614";

    public static bool MatchesRouteCode(string? input)
    {
        return string.Equals(input?.Trim(), RouteCode, StringComparison.Ordinal);
    }

    public static bool MatchesBellSequence(IReadOnlyList<int> input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Count != 6)
        {
            return false;
        }

        int bell = 0;
        for (int index = 0; index < input.Count; index++)
        {
            bell = (bell + index) % 4;
            if (input[index] != bell)
            {
                return false;
            }
        }

        return true;
    }
}
