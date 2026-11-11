<#
.SYNOPSIS
Nạp plug-in vào AutoCAD không cần giao diện bằng accoreconsole và chạy PA_HELLO trên một bản vẽ.

.DESCRIPTION
Dùng cho việc S0-10. AutoCAD chỉ nạp DLL nằm trong thư mục tin cậy, biến TRUSTEDPATHS,
và thư mục tin cậy không bao gồm thư mục con trừ khi đường dẫn kết thúc bằng "\...".
Kịch bản này chép bản dựng vào thẳng src\PipingAgent.AutoCAD\bin, là thư mục đã được
thêm vào TRUSTEDPATHS trên máy phát triển, rồi nạp từ đó.

Chưa đổi gì trong cài đặt bảo mật của AutoCAD. Nếu NETLOAD báo "Unable to load", kiểm tra
TRUSTEDPATHS trong AutoCAD.

.EXAMPLE
build\test_plugin.ps1 -Dwg data\private\A410B4001.dwg
build\test_plugin.ps1 -Dwg data\private\A410B4001.dwg -AcadDir "C:\Program Files\Autodesk\AutoCAD 2025" -Target net8.0-windows
#>
param(
    [Parameter(Mandatory = $true)][string]$Dwg,
    [string]$AcadDir = "C:\Program Files\Autodesk\AutoCAD 2025",
    [ValidateSet("net8.0-windows", "net48")][string]$Target = "net8.0-windows",
    [string]$Configuration = "Release",
    [int]$TimeoutSec = 120,
    [string]$Command = "PA_HELLO"
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $AcadDir "accoreconsole.exe"
if (-not (Test-Path $exe)) { throw "Không thấy $exe" }
$Dwg = (Resolve-Path $Dwg).Path

$buildOut = Join-Path $repo "src\PipingAgent.AutoCAD\bin\$Configuration\$Target"
$trusted = Join-Path $repo "src\PipingAgent.AutoCAD\bin"
if (-not (Test-Path (Join-Path $buildOut "PipingAgent.AutoCAD.dll"))) {
    throw "Chưa có bản dựng ở $buildOut. Chạy build\build.ps1 trước."
}
# Chép cả thư mục con profiles và catalog, chúng chỉ chứa JSON nên không cần nằm trong TRUSTEDPATHS.
Copy-Item (Join-Path $buildOut "*") $trusted -Force -Recurse
$dll = Join-Path $trusted "PipingAgent.AutoCAD.dll"

$scr = Join-Path $env:TEMP ("pa_hello_" + [guid]::NewGuid().ToString("N") + ".scr")
@"
(setvar "FILEDIA" 0)
_.NETLOAD "$dll"
$Command
(princ "\nPA-SCRIPT-END")

"@ | Set-Content -Path $scr -Encoding Ascii

$outFile = Join-Path $env:TEMP ("acc_" + [guid]::NewGuid().ToString("N") + ".txt")
$p = Start-Process -FilePath $exe -ArgumentList @("/i", "`"$Dwg`"", "/s", "`"$scr`"", "/l", "en-US") `
    -RedirectStandardOutput $outFile -NoNewWindow -PassThru
$finished = $p.WaitForExit($TimeoutSec * 1000)
if (-not $finished) { $p.Kill(); $p.WaitForExit(); Start-Sleep -Seconds 1; Write-Warning "accoreconsole không tự thoát sau $TimeoutSec giây, đã dừng." }

# accoreconsole ghi UTF-16LE.
$text = [System.IO.File]::ReadAllText($outFile, [System.Text.Encoding]::Unicode)
Remove-Item $outFile, $scr -ErrorAction SilentlyContinue

$lines = $text -split "`r?`n" | Where-Object { $_.Trim() -ne "" }
$start = [Array]::IndexOf($lines, ($lines | Where-Object { $_ -like "*NETLOAD*" } | Select-Object -First 1))
if ($start -lt 0) { $start = 0 }
$end = [Array]::IndexOf($lines, ($lines | Where-Object { $_ -like "*PA-SCRIPT-END*" } | Select-Object -First 1))
if ($end -lt 0) { $end = $lines.Count - 1 }
$lines[$start..$end]

if ($text -match "Unable to load") { Write-Error "NETLOAD thất bại. Kiểm tra TRUSTEDPATHS trong AutoCAD."; exit 1 }
if ($text -notmatch "Piping Agent") { Write-Error "$Command không chạy."; exit 1 }
exit 0
