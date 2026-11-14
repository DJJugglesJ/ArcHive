namespace ArcHive.Core;

// Presentation only: stored enum names and IDs remain unchanged.
public static class UiText
{
    public const string Library = "Library";
    public const string Archives = "Archives";
    public const string OrganizeLibrary = "Organize Library";
    public const string FindInformation = "Find Game Information";
    public const string ManageArchive = "Manage Archive…";
    public const string ScanGameFiles = "Scan Game Files";
    public const string OpenGameFolder = "Open Game Folder";
    public const string SaveBackups = "Save Backups";
    public const string SaveChanges = "Save Changes";
    public const string ApplyChanges = "Apply Changes";
    public const string Cancel = "Cancel";
    public const string PlayStatus = "Play Status";
    public const string InstallationStatus = "Installation Status";
    public const string UpdateStatus = "Update Status";
    public const string Tagline = "From Archive to Adventure.";
    public const string Attribution = "Bellum Labs · Nicholas Gartin";
    public const string EmptyTitle = "Your library is empty.";
    public const string EmptyDescription = "Add a library folder to discover your games and archives.";
    public const string NoMatches = "No matching games";
    public const string NoMatchesDescription = "Try changing your search or filters.";
    public const string NoDescription = "No description available.\nUse Find Game Information or add your own description.";
    public const string MatchingNotice = "Matches are suggestions, not verified identities. Review each result and choose which fields to update.";
    public const string ControllerHelp = "Cross / A: select or edit text\nCircle / B: cancel or back\nTriangle / Y: context options\nOptions / Start: app options\nL1 / R1 (LB / RB): previous / next control\nD-pad / left stick: navigate lists and controls\nSquare / X: enter text (also in file pickers)\n\nIn native Windows dialogs, Options opens the dialog menu. Use shoulders to reach buttons and fields. External websites and launched games use their own controls.";
    public static string ControllerHint(ControllerProfile profile)
    { var p = ControllerPrompts.For(profile); return $"{p.Accept}: select · {p.Cancel}: back · {p.Menu}: context · Options / Start: menu · {p.Previous}/{p.Next}: focus"; }
    public static string Availability(string state) => state switch { "Ready" => "Ready to Play", "Folder linked" => "Folder Linked", "Not installed" => "Not Installed", "Missing" => "Missing Files", _ => state };
    public static string Update(string state) => state switch { "Update available" => "Update Available", "Up to date" => "Up to Date", _ => state };
    public static string Extraction(ExtractionState state) => state == ExtractionState.Cancelled ? "Canceled" : state.ToString();
    public static string LibraryCount(int visible, int total, int folders) => $"Showing {visible} of {total} games · {folders} library folders";
    public static string RemoveGames(int count) => $"Remove {count} games from your library? Their saved ArcHive information will be removed, but original game folders and archives will remain on disk.";
}
