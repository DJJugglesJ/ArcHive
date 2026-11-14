# Deterministic Windows ICO packaging of the approved icon region.
# Requires Windows PowerShell 5.1 (WPF); no redraw or generative conversion.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore,WindowsBase
$assets = Join-Path $PSScriptRoot '..\ArcHive.App\Assets'
$source = [System.Windows.Media.Imaging.BitmapImage]::new([Uri](Join-Path (Resolve-Path $assets) 'ArcHive-brand-approved.png'))
$crop = [System.Windows.Media.Imaging.CroppedBitmap]::new($source, [System.Windows.Int32Rect]::new(963,309,400,400))
$sizes = @(16,20,24,32,40,48,64,128,256)
$frames = @()
foreach ($size in $sizes) {
    $visual = [System.Windows.Media.DrawingVisual]::new()
    [System.Windows.Media.RenderOptions]::SetBitmapScalingMode($visual, [System.Windows.Media.BitmapScalingMode]::HighQuality)
    $context = $visual.RenderOpen()
    $context.DrawImage($crop, [System.Windows.Rect]::new(0,0,$size,$size))
    $context.Close()
    $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new($size,$size,96,96,[System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $memory = [System.IO.MemoryStream]::new()
    $encoder.Save($memory)
    $frames += ,$memory.ToArray()
    $memory.Dispose()
}
$path = Join-Path $assets 'ArcHive.ico'
$stream = [System.IO.File]::Create($path)
$writer = [System.IO.BinaryWriter]::new($stream)
try {
    $writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i=0; $i -lt $sizes.Count; $i++) {
        $dimension = $sizes[$i] % 256
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([UInt16]1); $writer.Write([UInt16]32)
        $writer.Write([UInt32]$frames[$i].Length); $writer.Write([UInt32]$offset)
        $offset += $frames[$i].Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
} finally { $writer.Dispose() }
Write-Output "Packaged $path ($($sizes -join ', ') px)"
