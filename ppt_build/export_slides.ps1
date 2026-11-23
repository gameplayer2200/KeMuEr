$ErrorActionPreference = "Stop"
$src = "D:\FYP\KeMuEr\ppt_build\deck.pptx"
$outDir = "D:\FYP\KeMuEr\ppt_build\render"
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }

$app = New-Object -ComObject PowerPoint.Application
$pres = $app.Presentations.Open($src, $true, $false, $false)
Write-Output ("slides: " + $pres.Slides.Count)
foreach ($i in 5, 6, 11, 12, 13) {
    $slide = $pres.Slides.Item($i)
    $path = Join-Path $outDir ("slide{0:d2}.png" -f $i)
    $slide.Export($path, "PNG", 1600, 900)
    Write-Output ("exported " + $path)
}
$pres.Close()
$app.Quit()
