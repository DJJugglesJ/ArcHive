# Optional integrations

## Locale Emulator

Choose an existing `LEProc.exe` through **Settings → Integrations → Manage Integrations…**. Detection checks the selected location, common installation folders and PATH; it does not scan game directories or install software. A complete folder needs LEProc.exe, LoaderDll.dll, LocaleEmulator.dll and LEConfig.xml.

In a game's Launch tab, choose a global Locale Emulator profile. ArcHive displays its locale, derived ANSI code page and time zone. Edit the profile in Locale Emulator itself, then read profiles again. Configuration is bounded XML with DTD/external entities disabled. Native launch remains the default.

The adapter supports x86 games on x86/x64 Windows. x64, unconstrained AnyCPU and suspended debug profiles are rejected. LEProc uses the executable directory, so a different custom working directory is rejected. A profile's Parameter overrides passed game arguments in LEProc; ArcHive rejects configurations that populate both sources. Missing/disabled adapters do not silently fall back to native launching. Profiles can request elevation or start Locale Emulator's own updater.

Implementation follows the upstream [LEProc command-line code](https://github.com/xupefei/Locale-Emulator/blob/master/LEProc/Program.cs), [profile configuration](https://github.com/xupefei/Locale-Emulator/blob/master/LECommonLibrary/LEConfig.cs) and [profile model](https://github.com/xupefei/Locale-Emulator/blob/master/LECommonLibrary/LEProfile.cs). Tests use PE and XML fixtures; they do not claim execution of a real game under Locale Emulator.

## Plugins

Install a distribution folder explicitly in Integrations. Scanned game folders never load plugins. Installation copies files into a generated managed folder and starts disabled. Review capabilities and trust the publisher before enabling. Plugins execute as your Windows account in separate processes: **they are not sandboxed**. Capability declarations control ArcHive calls, not the process's Windows access. ArcHive sends no provider credentials in its request and passes only basic Windows/TEMP environment values.

The bounded `plugin.json` manifest uses these property names:

```json
{
  "ApiVersion": 1,
  "Id": "example.provider",
  "Name": "Example Provider",
  "Version": "1.0.0",
  "MinimumAppVersion": "1.0.0",
  "Executable": "ExamplePlugin.exe",
  "Capabilities": ["metadata", "launch"]
}
```

IDs use lowercase letters, digits, dots and hyphens, beginning with a letter (3–60 characters). Capabilities are `metadata` and/or `launch`. Installation limits: 32 plugins, 1,024 files, 512 directories and 256 MB per distribution; links and path escapes are rejected. SHA-256 hashes pin the installed file set; changed files require reinstalling. Write plugin caches outside the managed distribution. Removal unregisters first and removes only ArcHive's managed copy.

One invocation receives one JSON request on stdin followed by EOF. It must write one JSON response to stdout and exit successfully. Use stderr for bounded diagnostics, never stdout logging. Requests/responses are limited to approximately 2 MB and stderr to 16 KB; the default deadline is 15 seconds. Cancellation/timeouts terminate the process tree. No library changes are applied from failed responses.

Metadata request:

```json
{"ApiVersion":1,"Operation":"metadata.search","Query":{"Title":"Example","Year":null,"Engine":null},"Launch":null}
```

Response: `{"ApiVersion":1,"Candidates":[],"Plan":null}`. Candidate objects use the Core `MetadataCandidate` contract, must use Provider `plugin:example.provider`, and enter normal confidence/field reconciliation. Up to 500 candidates are accepted; manual choices remain protected.

Launch request uses `Operation: "launch.prepare"` and a `Launch` object containing `Executable`, `WorkingDirectory` and `Arguments`. Return `Plan` with the same shape. Executables must be the validated selected game or an executable in the installed plugin's entry directory; working directory must remain unchanged. Argument count/length and absolute paths are validated. Launch profiles retain `plugin:<id>` as their AdapterId even if that plugin becomes unavailable.

The protocol's exact public types are in `ArcHive.Core/Plugins.cs` and `ArcHive.Core/LaunchProfiles.cs`. Plugin distribution, updates and publisher verification remain user-managed.
