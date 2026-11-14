using System.Text.Json;
using ArcHive.Core;

internal static class PreferenceTests
{
    public static void Run(string root, Action<bool, string> check)
    {
        var path = Path.Combine(root, "settings", "settings.json"); var store = new AppPreferencesStore(path);
        var settings = store.Load();
        check(settings.ControllerProfile == ControllerProfile.DualSense && settings.GamepadEnabled && !settings.ShowNsfw, "Settings default to DualSense and hidden marked NSFW games");
        var marked = new Game { Title = "Marked game", IsNsfw = true }; var ordinary = new Game { Title = "Other game" };
        check(!settings.Includes(marked) && settings.Includes(ordinary), "Hidden setting excludes only explicitly marked games");
        settings.ShowNsfw = true;
        check(settings.Includes(marked) && settings.Includes(ordinary), "18+ setting includes marked and unmarked games");
        settings.ControllerProfile = ControllerProfile.DualShock4; settings.GamepadEnabled = false;
        store.Save(settings); var loaded = store.Load();
        check(loaded.ControllerProfile == ControllerProfile.DualShock4 && loaded.ShowNsfw && !loaded.GamepadEnabled, "Controller profile visibility and input preference survive restart");
        settings.ControllerProfile = ControllerProfile.Xbox; settings.ShowNsfw = false; store.Save(settings);
        check(store.Load().ControllerProfile == ControllerProfile.Xbox && !store.Load().ShowNsfw && File.Exists(path + ".bak"), "Settings replace atomically with a backup");
        check(!Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.tmp").Any(), "Settings save leaves no staging files");
        settings.ControllerProfile = (ControllerProfile)99;
        try { store.Save(settings); throw new Exception("Invalid settings accepted"); }
        catch (InvalidDataException) { check(store.Load().ControllerProfile == ControllerProfile.Xbox, "Invalid profile save preserves existing preferences"); }
        File.WriteAllText(path, "{\"ControllerProfile\":99}");
        try { store.Load(); throw new Exception("Future profile accepted"); }
        catch (InvalidDataException) { check(true, "Unsupported saved profile rejected"); }
        File.WriteAllText(path, "{invalid");
        try { store.Load(); throw new Exception("Corrupt settings accepted"); }
        catch (JsonException) { check(File.ReadAllText(path) == "{invalid", "Corrupt settings are retained without overwrite"); }
        File.WriteAllText(path, "{}");
        check(store.Load().ControllerProfile == ControllerProfile.DualSense && !store.Load().ShowNsfw, "Missing settings fields use conservative defaults");
        var ps5 = ControllerPrompts.For(ControllerProfile.DualSense); var ps4 = ControllerPrompts.For(ControllerProfile.DualShock4); var xbox = ControllerPrompts.For(ControllerProfile.Xbox);
        check(ps5.Accept == "Cross" && ps5.Cancel == "Circle" && ps5.Alternate == "Square" && ps5.Menu == "Triangle", "PlayStation face button prompts use shapes");
        check(ps4 == ps5 && ps4.Previous == "L1" && ps4.Next == "R1", "DS4 shares positional PlayStation mapping");
        check(xbox.Accept == "A" && xbox.Cancel == "B" && xbox.Alternate == "X" && xbox.Menu == "Y" && xbox.Next == "RB", "Xbox profile uses correct face and shoulder prompts");
        var db = new SqliteLibraryStore(Path.Combine(root, "visibility.db")); db.Load(); db.Save(new Library { Games = [marked, ordinary] });
        var library = db.Load();
        check(library.Games.Single(g => g.Id == marked.Id).IsNsfw && !library.Games.Single(g => g.Id == ordinary.Id).IsNsfw, "SQLite preserves per-game NSFW classification");
        library.Games.Single(g => g.Id == marked.Id).IsNsfw = false; db.Save(library);
        check(!db.Load().Games.Any(g => g.IsNsfw), "Removing NSFW classification persists");
        check(!JsonSerializer.Deserialize<Game>("{\"Title\":\"Legacy\"}")!.IsNsfw, "Legacy games remain unclassified rather than guessed NSFW");
        var reducer = new GamepadNavigation(); reducer.Update(0, 0, 0, true, TimeSpan.Zero);
        check(reducer.Update(PadButtons.A, 0, 0, true, TimeSpan.Zero) == PadAction.Accept && reducer.Update(PadButtons.A, 0, 0, true, TimeSpan.FromSeconds(10)) == PadAction.None, "Direct launch remains edge-triggered with no held-button repeat");
    }
}
