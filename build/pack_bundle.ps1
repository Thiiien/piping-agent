<#
.SYNOPSIS
Đóng gói plug-in AutoCAD thành thư mục PipingAgent.bundle. Việc S1-09, mục 11.3 của tài liệu dự án.

.DESCRIPTION
Lấy bản dựng Release của hai nền .NET, ghép với PackageContents.xml, ghi ra dist\PipingAgent.bundle.
-Install chép gói vào %APPDATA%\Autodesk\ApplicationPlugins của người dùng hiện tại; AutoCAD và accoreconsole
tự nạp khi khởi động, không cần NETLOAD hay TRUSTEDPATHS.
-Uninstall xoá gói đã cài.

.EXAMPLE
build\pack_bundle.ps1
build\pack_bundle.ps1 -Install
build\pack_bundle.ps1 -Uninstall
#>
param(
    [switch]$Install,
    [switch]$Uninstall,
    [switch]$Zip,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$bundleName = "PipingAgent.bundle"
$installRoot = Join-Path $env:APPDATA "Autodesk\ApplicationPlugins"
$installPath = Join-Path $installRoot $bundleName

if ($Uninstall) {
    if (Test-Path $installPath) {
        Remove-Item $installPath -Recurse -Force -Confirm:$false
        Write-Host "Đã gỡ $installPath"
    } else {
        Write-Host "Chưa cài, không có gì để gỡ."
    }
    exit 0
}

$plugin = Join-Path $repo "src\PipingAgent.AutoCAD"
$net48 = Join-Path $plugin "bin\$Configuration\net48"
$net8 = Join-Path $plugin "bin\$Configuration\net8.0-windows"
foreach ($dir in @($net48, $net8)) {
    if (-not (Test-Path (Join-Path $dir "PipingAgent.AutoCAD.dll"))) { throw "Chưa có bản dựng ở $dir. Chạy build\build.ps1 trước." }
}

$version = ([xml](Get-Content (Join-Path $repo "Directory.Build.props"))).Project.PropertyGroup.Version
if (-not $version) { $version = "0.1.0" }

$dist = Join-Path $repo "dist\$bundleName"
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force -Confirm:$false }
New-Item -ItemType Directory -Path (Join-Path $dist "Contents\net48") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $dist "Contents\net8") -Force | Out-Null

Copy-Item (Join-Path $net48 "*.dll") (Join-Path $dist "Contents\net48")
Copy-Item (Join-Path $net8 "*.dll") (Join-Path $dist "Contents\net8")
Copy-Item (Join-Path $net8 "*.deps.json") (Join-Path $dist "Contents\net8") -ErrorAction SilentlyContinue
# Hồ sơ quy ước và danh mục đi kèm, plug-in tìm ở thư mục profiles và catalog cạnh tệp DLL.
foreach ($sub in @("profiles", "catalog", "equipment", "agenthost")) {
    foreach ($target in @("net48", "net8")) {
        $src = Join-Path (Get-Variable $target -ValueOnly) $sub
        if (Test-Path $src) { Copy-Item $src (Join-Path $dist "Contents\$target\$sub") -Recurse }
    }
}

$manifest = Get-Content (Join-Path $plugin "Bundle\PackageContents.xml") -Raw -Encoding UTF8
$manifest = $manifest.Replace("@VERSION@", "$version")
[System.IO.File]::WriteAllText((Join-Path $dist "PackageContents.xml"), $manifest, (New-Object System.Text.UTF8Encoding($true)))

$readme = @"
Piping Agent $version cho AutoCAD
Chép cả thư mục $bundleName vào %APPDATA%\Autodesk\ApplicationPlugins rồi khởi động lại AutoCAD.
Lệnh: PA_ISO rồi PA_BUILD cho tờ isometric; PA_SCAN, PA_CONVERT, PA_CHECK, PA_BUILD cho mặt bằng; PA_PANEL, PA_HELLO.
Nhật ký: %LOCALAPPDATA%\PipingAgent\logs
"@
[System.IO.File]::WriteAllText((Join-Path $dist "Contents\README.txt"), $readme, (New-Object System.Text.UTF8Encoding($true)))

Write-Host "Đã tạo $dist"
Get-ChildItem $dist -Recurse -File | ForEach-Object { "  " + $_.FullName.Substring($dist.Length + 1) + "  " + $_.Length }

# -Zip: gói cài thủ công, S6-11: tệp nén chứa bundle, hồ sơ mẫu và hướng dẫn cài hai trang.
if ($Zip) {
    $zipPath = Join-Path $repo "dist\PipingAgent-$version.zip"
    $stage = Join-Path $env:TEMP ("pa_zip_" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Force $stage | Out-Null
    Copy-Item $dist (Join-Path $stage $bundleName) -Recurse
    Copy-Item (Join-Path $repo "data\profiles") (Join-Path $stage "profiles") -Recurse
    Copy-Item (Join-Path $PSScriptRoot "HUONG_DAN_CAI.md") (Join-Path $stage "HUONG_DAN_CAI.md")
    if (Test-Path $zipPath) { Remove-Item $zipPath }
    Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zipPath
    Remove-Item $stage -Recurse -Force
    Write-Host "Đã tạo $zipPath"
}

if ($Install) {
    New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
    if (Test-Path $installPath) { Remove-Item $installPath -Recurse -Force -Confirm:$false }
    Copy-Item $dist $installPath -Recurse
    Write-Host "Đã cài vào $installPath"
}
exit 0
