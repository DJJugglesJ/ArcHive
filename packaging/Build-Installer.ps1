param(
    [string]$Version = '0.9.0',
    [string]$WixPath,
    [switch]$SkipPublish
)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot
$artifacts = Join-Path $project 'artifacts'
$publish = Join-Path $artifacts 'publish\win-x64'
$output = Join-Path $artifacts 'installer'
New-Item -ItemType Directory -Force $output | Out-Null
function Run([scriptblock]$Command) { & $Command; if ($LASTEXITCODE -ne 0) { throw "Build command failed ($LASTEXITCODE)." } }
if (-not $SkipPublish) {
    Run { powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Export-Icon.ps1') }
    Run { dotnet publish (Join-Path $project 'ArcHive.App\ArcHive.App.csproj') -c Release -r win-x64 --self-contained true -o $publish -p:Version=$Version -p:DebugType=None -p:DebugSymbols=false }
}
if (-not (Test-Path (Join-Path $publish 'ArcHive.exe'))) { throw 'Publish ArcHive before packaging.' }
if (-not $WixPath) {
    $toolFolder = Join-Path $artifacts 'tools'
    $WixPath = Join-Path $toolFolder 'wix.exe'
    if (-not (Test-Path $WixPath)) { Run { dotnet tool install wix --version 4.0.6 --tool-path $toolFolder } }
}
$publishedVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $publish 'ArcHive.exe')).FileVersion
if ([Version]$publishedVersion -ne [Version]($Version + '.0')) { throw 'Installer version must match the published executable. Rebuild without -SkipPublish.' }
$coreVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $publish 'ArcHive.Core.dll')).FileVersion
if ([Version]$coreVersion -ne [Version]($Version + '.0')) { throw 'Core version must match installer version. Rebuild without -SkipPublish.' }
$env:DOTNET_ROLL_FORWARD = 'Major'
function Xml([string]$Value) { return [System.Security.SecurityElement]::Escape($Value) }
function Id([string]$Value) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { return 'F' + ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Value))).Replace('-','').Substring(0,30)) } finally { $sha.Dispose() }
}
function StableGuid([string]$Value) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { $bytes = $sha.ComputeHash([Text.Encoding]::UTF8.GetBytes('ArcHive.Component.v1/' + $Value)); return [Guid]::new([byte[]]$bytes[0..15]).ToString() } finally { $sha.Dispose() }
}
$components = [System.Collections.Generic.List[string]]::new()
function DirectoryXml([string]$Directory) {
    $xml = [Text.StringBuilder]::new()
    foreach ($file in Get-ChildItem -LiteralPath $Directory -File | Sort-Object Name) {
        $relative = $file.FullName.Substring($publish.Length + 1)
        $id = Id $relative
        $components.Add($id)
        [void]$xml.AppendLine('<Component Id="'+$id+'" Guid="'+(StableGuid $relative)+'"><File Id="'+$id+'File" Source="'+(Xml $file.FullName)+'"/><RegistryValue Root="HKCU" Key="Software\ArcHive\Installer\Files" Name="'+$id+'" Type="integer" Value="1" KeyPath="yes"/></Component>')
    }
    foreach ($dir in Get-ChildItem -LiteralPath $Directory -Directory | Sort-Object Name) {
        $relative = $dir.FullName.Substring($publish.Length+1)
        $id = Id ('dir/'+$relative)
        $components.Add($id+'Cleanup')
        [void]$xml.AppendLine('<Directory Id="'+$id+'" Name="'+(Xml $dir.Name)+'">'+(DirectoryXml $dir.FullName)+'<Component Id="'+$id+'Cleanup" Guid="*"><RemoveFolder Id="'+$id+'Remove" On="uninstall"/><RegistryValue Root="HKCU" Key="Software\ArcHive\Installer\Folders" Name="'+$id+'" Type="integer" Value="1" KeyPath="yes"/></Component></Directory>')
    }
    return $xml.ToString()
}
$filesXml = DirectoryXml $publish
$refs = ($components | ForEach-Object { '<ComponentRef Id="'+$_+'"/>' }) -join "`n"
$icon = Xml (Join-Path $project 'ArcHive.App\Assets\ArcHive.ico')
$versionXml = Xml $Version
$source = @"
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
 <Package Name="ArcHive" Manufacturer="ArcHive" Version="$versionXml" UpgradeCode="93D19C3A-D94D-4908-98A8-EBEC4EF93778" Scope="perUser" InstallerVersion="500">
  <MajorUpgrade DowngradeErrorMessage="A newer version of ArcHive is already installed."/>
  <MediaTemplate EmbedCab="yes" CompressionLevel="high"/>
  <Icon Id="ArcHive.ico" SourceFile="$icon"/>
  <Property Id="ARPPRODUCTICON" Value="ArcHive.ico"/>
  <Property Id="INSTALLFOLDER"><RegistrySearch Id="PreviousInstallFolder" Root="HKCU" Key="Software\ArcHive\Installer" Name="InstallDir" Type="raw"/></Property>
  <Property Id="ARPURLINFOABOUT" Value="https://github.com/DJJugglesJ/ArcHive"/>
  <StandardDirectory Id="LocalAppDataFolder"><Directory Id="ProgramsFolder" Name="Programs"><Directory Id="INSTALLFOLDER" Name="ArcHive">
   $filesXml
   <Component Id="InstallationFolderCleanup" Guid="*"><RemoveFolder Id="RemoveInstallFolder" On="uninstall"/><RegistryValue Root="HKCU" Key="Software\ArcHive\Installer" Name="InstallDir" Type="string" Value="[INSTALLFOLDER]" KeyPath="yes"/></Component>
  </Directory></Directory></StandardDirectory>
  <StandardDirectory Id="ProgramMenuFolder"><Directory Id="ArcHiveMenu" Name="ArcHive"><Component Id="StartMenuShortcut" Guid="*">
   <Shortcut Id="ArcHiveShortcut" Name="ArcHive" Target="[INSTALLFOLDER]ArcHive.exe" WorkingDirectory="INSTALLFOLDER" Icon="ArcHive.ico"/>
   <RemoveFolder Id="RemoveMenuFolder" On="uninstall"/>
   <RegistryValue Root="HKCU" Key="Software\ArcHive\Installer" Name="Shortcut" Type="integer" Value="1" KeyPath="yes"/>
  </Component></Directory></StandardDirectory>
  <Feature Id="Complete" Title="ArcHive" Level="1">$refs<ComponentRef Id="InstallationFolderCleanup"/><ComponentRef Id="StartMenuShortcut"/></Feature>
 </Package>
</Wix>
"@
$wxs = Join-Path $output 'ArcHive.generated.wxs'
[IO.File]::WriteAllText($wxs, $source, [Text.UTF8Encoding]::new($false))
$msi = Join-Path $output "ArcHive-$Version-win-x64.msi"
Run { & $WixPath build $wxs -arch x64 -o $msi }
Get-FileHash -Algorithm SHA256 $msi | Format-List
Write-Output "Installer: $msi"
