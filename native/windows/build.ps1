# Nacos Sync Tool - Windows Native Build Script

param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "../../dist/windows-native",
    [switch]$SkipTests,
    [switch]$Portable
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Nacos Sync Tool - Windows Native" -ForegroundColor Cyan
Write-Host "  Build Script v1.0.2" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 检查 .NET SDK
Write-Host "[1/6] Checking .NET SDK..." -ForegroundColor Yellow
try {
    $dotnetVersion = dotnet --version
    Write-Host "  ✓ .NET SDK version: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "  ✗ .NET SDK not found! Please install .NET 10.0 SDK or higher." -ForegroundColor Red
    Write-Host "  Download: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    exit 1
}

# 进入项目目录
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptPath

# 还原依赖
Write-Host ""
Write-Host "[2/6] Restoring dependencies..." -ForegroundColor Yellow
dotnet restore NacosSyncTool.Windows.sln
if ($LASTEXITCODE -ne 0) {
    Write-Host "  ✗ Failed to restore dependencies" -ForegroundColor Red
    exit 1
}
Write-Host "  ✓ Dependencies restored" -ForegroundColor Green

# 构建项目
Write-Host ""
Write-Host "[3/6] Building project ($Configuration)..." -ForegroundColor Yellow
dotnet build NacosSyncTool.Windows.sln --configuration $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "  ✗ Build failed" -ForegroundColor Red
    exit 1
}
Write-Host "  ✓ Build succeeded" -ForegroundColor Green

# 运行测试
if (-not $SkipTests) {
    Write-Host ""
    Write-Host "[4/6] Running tests..." -ForegroundColor Yellow
    dotnet test NacosSyncTool.Windows.sln --configuration $Configuration --no-build --verbosity normal
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ✗ Tests failed" -ForegroundColor Red
        exit 1
    }
    Write-Host "  ✓ All tests passed" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "[4/6] Skipping tests..." -ForegroundColor Yellow
}

# 发布应用
Write-Host ""
Write-Host "[5/6] Publishing application..." -ForegroundColor Yellow

$publishDir = Join-Path $scriptPath $OutputDir
if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

# 发布 x64 版本
Write-Host "  → Publishing Windows x64..." -ForegroundColor Cyan
$x64Dir = Join-Path $publishDir "win-x64"
dotnet publish src/NacosSyncTool.Windows/NacosSyncTool.Windows.csproj `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    --output $x64Dir `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) {
    Write-Host "  ✗ Failed to publish x64 version" -ForegroundColor Red
    exit 1
}
Write-Host "  ✓ Windows x64 published" -ForegroundColor Green

# 发布 ARM64 版本
Write-Host "  → Publishing Windows ARM64..." -ForegroundColor Cyan
$arm64Dir = Join-Path $publishDir "win-arm64"
dotnet publish src/NacosSyncTool.Windows/NacosSyncTool.Windows.csproj `
    --configuration $Configuration `
    --runtime win-arm64 `
    --self-contained true `
    --output $arm64Dir `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) {
    Write-Host "  ✗ Failed to publish ARM64 version" -ForegroundColor Red
    exit 1
}
Write-Host "  ✓ Windows ARM64 published" -ForegroundColor Green

# 创建便携版压缩包
if ($Portable) {
    Write-Host ""
    Write-Host "[6/6] Creating portable packages..." -ForegroundColor Yellow

    # 压缩 x64 版本
    $x64ZipPath = Join-Path $publishDir "NacosSyncTool-Windows-x64-v1.0.2.zip"
    Compress-Archive -Path "$x64Dir\*" -DestinationPath $x64ZipPath -Force
    Write-Host "  ✓ x64 package: $x64ZipPath" -ForegroundColor Green

    # 压缩 ARM64 版本
    $arm64ZipPath = Join-Path $publishDir "NacosSyncTool-Windows-ARM64-v1.0.2.zip"
    Compress-Archive -Path "$arm64Dir\*" -DestinationPath $arm64ZipPath -Force
    Write-Host "  ✓ ARM64 package: $arm64ZipPath" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "[6/6] Skipping portable packages..." -ForegroundColor Yellow
}

# 显示输出信息
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Build Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Output directory: $publishDir" -ForegroundColor Yellow
Write-Host ""
Write-Host "Files:" -ForegroundColor Yellow
Write-Host "  - win-x64/Nacos.Sync.Tool.Native.Windows.exe" -ForegroundColor Cyan
Write-Host "  - win-arm64/Nacos.Sync.Tool.Native.Windows.exe" -ForegroundColor Cyan
if ($Portable) {
    Write-Host "  - NacosSyncTool-Windows-x64-v1.0.2.zip" -ForegroundColor Cyan
    Write-Host "  - NacosSyncTool-Windows-ARM64-v1.0.2.zip" -ForegroundColor Cyan
}
Write-Host ""
Write-Host "Run the application:" -ForegroundColor Yellow
Write-Host "  cd $x64Dir" -ForegroundColor Cyan
Write-Host "  .\Nacos.Sync.Tool.Native.Windows.exe" -ForegroundColor Cyan
Write-Host ""
