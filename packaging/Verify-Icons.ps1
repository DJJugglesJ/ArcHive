param([string]$Executable, [string]$Installer)
$ErrorActionPreference='Stop'
if(-not $Executable){$Executable=Join-Path $PSScriptRoot '..\artifacts\publish\win-x64\ArcHive.exe'}
$ico=[IO.File]::ReadAllBytes((Join-Path $PSScriptRoot '..\ArcHive.App\Assets\ArcHive.ico'))
$count=[BitConverter]::ToUInt16($ico,4)
$sizes=for($i=0;$i -lt $count;$i++){$v=$ico[6+16*$i];if($v -eq 0){256}else{[int]$v}}
if(($sizes -join ',') -ne '16,20,24,32,40,48,64,128,256'){throw 'Unexpected ICO frames'}
Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class IconResources {
 public delegate bool EnumNames(IntPtr h, IntPtr type, IntPtr name, IntPtr data);
 [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
 [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] static extern bool EnumResourceNames(IntPtr h, IntPtr type, EnumNames callback, IntPtr data);
 [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] static extern IntPtr FindResource(IntPtr h, IntPtr name, IntPtr type);
 [DllImport("kernel32.dll")] static extern uint SizeofResource(IntPtr h, IntPtr resource);
 [DllImport("kernel32.dll")] static extern IntPtr LoadResource(IntPtr h, IntPtr resource);
 [DllImport("kernel32.dll")] static extern IntPtr LockResource(IntPtr resource);
 [DllImport("kernel32.dll")] static extern bool FreeLibrary(IntPtr h);
 public static byte[][] Read(string path) {
  var h=LoadLibraryEx(path,IntPtr.Zero,2);if(h==IntPtr.Zero)throw new Exception("Cannot read executable resources");
  try {var result=new List<byte[]>();
   EnumNames callback=(module,t,name,data)=>{var r=FindResource(module,name,t);var bytes=new byte[SizeofResource(module,r)];Marshal.Copy(LockResource(LoadResource(module,r)),bytes,0,bytes.Length);result.Add(bytes);return true;};
   EnumResourceNames(h,new IntPtr(3),callback,IntPtr.Zero);return result.ToArray();
  } finally {FreeLibrary(h);}
 }
}
"@
$resources=[IconResources]::Read((Resolve-Path $Executable).Path)
if($resources.Count -ne $count){throw 'Executable icon frame count mismatch'}
for($i=0;$i -lt $count;$i++){
 $entry=6+16*$i;$length=[BitConverter]::ToUInt32($ico,$entry+8);$offset=[BitConverter]::ToUInt32($ico,$entry+12)
 $encoded=[Convert]::ToBase64String($ico,$offset,$length)
 if(-not ($resources|Where-Object {[Convert]::ToBase64String($_) -eq $encoded})){throw "Executable icon differs at $($sizes[$i])px"}
}
Write-Output 'PASS: all nine executable icon resources exactly match the approved ICO frames.'
if($Installer){
 $engine=New-Object -ComObject WindowsInstaller.Installer
 $database=$engine.OpenDatabase((Resolve-Path $Installer).Path,0)
 $view=$database.OpenView('SELECT `Value` FROM `Property` WHERE `Property` = ''ARPPRODUCTICON''')
 $view.Execute();$record=$view.Fetch()
 if($record.StringData(1) -ne 'ArcHive.ico'){throw 'Installer icon property mismatch'}
 $view.Close()
 $view=$database.OpenView('SELECT `Data` FROM `Icon` WHERE `Name` = ''ArcHive.ico''')
 $view.Execute();$record=$view.Fetch()
 if($record.DataSize(1) -ne $ico.Length){throw 'Installer icon size mismatch'}
 $view.Close()
 Write-Output 'PASS: MSI Installed Apps icon references the packaged ICO stream.'
}
