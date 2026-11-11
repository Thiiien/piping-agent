# Chay dung cac buoc cua dung tu dong tren may ca nhan.
# -SkipAutoCad: bo qua buoc nap plug-in vao AutoCAD qua accoreconsole.
# -Dwg: ban ve dung de thu plug-in. Mac dinh la ban ve that trong docs\dataTemplate neu co.
param(
    [switch]$SkipAutoCad,
    [string]$Dwg,
    [string]$AcadDir = "C:\Program Files\Autodesk\AutoCAD 2025"
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
dotnet restore "$root\PipingAgent.sln"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build "$root\PipingAgent.sln" -c Release --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet test "$root\PipingAgent.sln" -c Release --no-build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Buoc nap plug-in that chi chay duoc tren may co AutoCAD. May chu dung tu dong khong co, nen bo qua im lang.
if ($SkipAutoCad -or -not (Test-Path (Join-Path $AcadDir "accoreconsole.exe"))) {
    Write-Host "Bo qua buoc nap plug-in vao AutoCAD."
    exit 0
}
if (-not $Dwg) {
    $Dwg = Join-Path $root "docs\dataTemplate\A410B4001.dwg"
    if (-not (Test-Path $Dwg)) { $Dwg = Join-Path $root "prototype\data\piping_plan_2d.dxf" }
}
Write-Host "Nap plug-in vao AutoCAD voi ban ve $Dwg"
& (Join-Path $PSScriptRoot "test_plugin.ps1") -Dwg $Dwg -AcadDir $AcadDir
exit $LASTEXITCODE
