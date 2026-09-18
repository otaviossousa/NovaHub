param([switch]$Package, [switch]$Probe)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$sdkPath = (Get-Command dotnet -ErrorAction Stop).Source
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
function Invoke-Dotnet {
    & $sdkPath @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet terminou com código $LASTEXITCODE" }
}
Invoke-Dotnet restore 'src/NovaLite.Desktop/NovaLite.Desktop.csproj' --configfile NuGet.Config '-p:NuGetAudit=false' --nologo
Invoke-Dotnet build 'src/NovaLite.Desktop/NovaLite.Desktop.csproj' -c Release --no-restore '-t:Rebuild' --nologo
Invoke-Dotnet restore 'tests/NovaLite.Checks/NovaLite.Checks.csproj' --configfile NuGet.Config '-p:NuGetAudit=false' --nologo
Invoke-Dotnet run --project 'tests/NovaLite.Checks' -c Release --no-restore
if ($Probe) {
    Invoke-Dotnet 'tests/NovaLite.Checks/bin/Release/net10.0/NovaLite.Checks.dll' --probe 'artifacts/xinput-probe.json'
}
if ($Package) {
    $artifactsRoot = Join-Path $PSScriptRoot 'artifacts'
    [xml]$project = Get-Content 'src/NovaLite.Desktop/NovaLite.Desktop.csproj' -Raw
    $version = [string]$project.Project.PropertyGroup.Version
    if ([string]::IsNullOrWhiteSpace($version)) { throw 'Versão do NovaHub não encontrada no projeto.' }
    $packageName = "NovaHub-$version-win-x64"
    $packagePath = Join-Path $artifactsRoot $packageName
    $zipPath = Join-Path $artifactsRoot "$packageName.zip"
    $checksumPath = "$zipPath.sha256"
    if (Test-Path -LiteralPath $packagePath) {
        Remove-Item -LiteralPath $packagePath -Recurse -Force
    }
    foreach ($oldFile in @($zipPath, $checksumPath)) {
        if (Test-Path -LiteralPath $oldFile) { Remove-Item -LiteralPath $oldFile -Force }
    }
    Invoke-Dotnet restore 'src/NovaLite.Desktop/NovaLite.Desktop.csproj' -r win-x64 --configfile NuGet.Config '-p:NuGetAudit=false' --nologo
    Invoke-Dotnet publish 'src/NovaLite.Desktop/NovaLite.Desktop.csproj' -c Release -r win-x64 --self-contained true --no-restore `
        '-p:PublishSingleFile=true' '-p:IncludeNativeLibrariesForSelfExtract=true' `
        '-p:EnableCompressionInSingleFile=true' '-p:DebugType=None' '-p:DebugSymbols=false' `
        -o $packagePath --nologo
    Copy-Item -LiteralPath 'packaging/README.txt' -Destination $packagePath -Force
    Copy-Item -LiteralPath 'LICENSE' -Destination $packagePath -Force
    $sdkRoot = Split-Path -Parent $sdkPath
    if (Test-Path (Join-Path $sdkRoot 'ThirdPartyNotices.txt')) {
        Copy-Item -LiteralPath (Join-Path $sdkRoot 'ThirdPartyNotices.txt') -Destination $packagePath -Force
    }
    Compress-Archive -Path (Join-Path $packagePath '*') -DestinationPath $zipPath -CompressionLevel Optimal
    $hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath $checksumPath -Value "$hash  $packageName.zip" -Encoding ascii
    Write-Output "Release disponível em $zipPath"
}







