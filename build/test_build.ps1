<#
.SYNOPSIS
Thử PA_BUILD không cần giao diện: quét, dựng vào bản vẽ hiện tại, đếm solid, Undo một lần, dựng lại có tiêm lỗi.

Kết quả mong đợi: SOLIDS bằng số phần tử của kế hoạch, AFTERUNDO=none, AFTERFAIL=none.
Dùng build\test_plugin.ps1 để nạp plug-in. Hồ sơ quy ước lấy từ %LOCALAPPDATA%\PipingAgent\settings.json.
#>
param(
    [Parameter(Mandatory = $true)][string]$Dwg,
    [string]$Profile = "",
    [int]$TimeoutSec = 180
)

$ErrorActionPreference = "Stop"
if ($Profile -ne "") {
    $settingsDir = Join-Path $env:LOCALAPPDATA "PipingAgent"
    New-Item -ItemType Directory -Force $settingsDir | Out-Null
    $json = '{"lastProfile":"' + ((Resolve-Path $Profile).Path -replace '\\', '\\') + '"}'
    [System.IO.File]::WriteAllText((Join-Path $settingsDir "settings.json"), $json, [System.Text.Encoding]::UTF8)
}

$count = '(princ (strcat "\nSOLIDS=" (itoa (sslength (ssget "X" (quote ((0 . "3DSOLID"))))))))'
$afterUndo = '(princ (strcat "\nAFTERUNDO=" (if (ssget "X" (quote ((0 . "3DSOLID")))) "some" "none")))'
$afterFail = '(princ (strcat "\nAFTERFAIL=" (if (ssget "X" (quote ((0 . "3DSOLID")))) "some" "none")))'
$command = "PA_SCAN`nPA_BUILD`nHientai`n$count`n_.UNDO`n1`n$afterUndo`nPA_BUILD`nLoithu`n$afterFail"

& (Join-Path $PSScriptRoot "test_plugin.ps1") -Dwg $Dwg -TimeoutSec $TimeoutSec -Command $command
