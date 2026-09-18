param([string]$Configuration = "Release")

$ErrorActionPreference = "Stop"
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$projectPath = Join-Path $projectRoot "src\NovaLite.Desktop\NovaLite.Desktop.csproj"
$manifestPath = Join-Path $PSScriptRoot "AppxManifest.xml"
$assetsPath = Join-Path $PSScriptRoot "Assets"
$toolsProject = Join-Path $PSScriptRoot "MsixTools.csproj"
$toolsPackages = Join-Path $projectRoot ".tools\msix"
$nugetConfig = Join-Path $projectRoot "NuGet.Config"
$artifactsPath = Join-Path $projectRoot "artifacts"
$stagingPath = Join-Path $artifactsPath "msix-layout"
$projectDocument = [xml](Get-Content -LiteralPath $projectPath -Raw)
$appVersion = [string]$projectDocument.Project.PropertyGroup.Version
$manifestDocument = [xml](Get-Content -LiteralPath $manifestPath -Raw)
$manifestVersion = [string]$manifestDocument.Package.Identity.Version
if ($manifestVersion -ne "$appVersion.0") {
    throw "A versão do manifesto ($manifestVersion) não corresponde à versão do aplicativo ($appVersion)."
}
$packagePath = Join-Path $artifactsPath "NovaHub-$appVersion-x64.msix"
$checksumPath = "$packagePath.sha256"

$makeAppx = Get-Command makeappx.exe -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $makeAppx) {
    $makeAppx = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Filter makeappx.exe -Recurse -ErrorAction SilentlyContinue |
        Where-Object FullName -Match "\\x64\\makeappx\.exe$" |
        Sort-Object FullName -Descending |
        Select-Object -First 1
}
if (-not $makeAppx) {
    & dotnet restore $toolsProject --packages $toolsPackages --configfile $nugetConfig -p:NuGetAudit=false --nologo
    if ($LASTEXITCODE -ne 0) { throw "Falha ao restaurar as ferramentas MSIX." }
    $makeAppx = Get-ChildItem $toolsPackages -Filter makeappx.exe -Recurse -ErrorAction SilentlyContinue |
        Where-Object FullName -Match "\\x64\\makeappx\.exe$" |
        Sort-Object FullName -Descending |
        Select-Object -First 1
}
if (-not $makeAppx) {
    throw "MakeAppx.exe não encontrado nas ferramentas oficiais da Microsoft."
}

foreach ($path in @($stagingPath, $packagePath, $checksumPath)) {
    if (-not (Test-Path -LiteralPath $path)) { continue }
    $resolved = (Resolve-Path -LiteralPath $path).Path
    if (-not $resolved.StartsWith($artifactsPath + [IO.Path]::DirectorySeparatorChar)) {
        throw "Destino fora da pasta de artefatos: $resolved"
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}

& dotnet publish $projectPath -c $Configuration -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $stagingPath
if ($LASTEXITCODE -ne 0) { throw "Falha ao publicar o NovaHub." }

Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $stagingPath "AppxManifest.xml")
Copy-Item -LiteralPath $assetsPath -Destination (Join-Path $stagingPath "Assets") -Recurse
Copy-Item -LiteralPath (Join-Path $projectRoot "LICENSE") -Destination $stagingPath
$dotnetRoot = Split-Path -Parent (Get-Command dotnet -ErrorAction Stop).Source
$thirdPartyNotices = Join-Path $dotnetRoot "ThirdPartyNotices.txt"
if (Test-Path -LiteralPath $thirdPartyNotices) {
    Copy-Item -LiteralPath $thirdPartyNotices -Destination $stagingPath
}

& $makeAppx.FullName pack /d $stagingPath /p $packagePath /o /h SHA256
if ($LASTEXITCODE -ne 0) { throw "Falha ao gerar o pacote MSIX." }

$hash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath $checksumPath -Value "$hash  $([IO.Path]::GetFileName($packagePath))" -Encoding ascii
Remove-Item -LiteralPath $stagingPath -Recurse -Force
Write-Output "Pacote MSIX disponível em $packagePath"
