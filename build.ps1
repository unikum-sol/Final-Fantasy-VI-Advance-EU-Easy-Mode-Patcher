# Baut den FF6A-Patcher für Windows (x64), macOS (Apple Silicon) und Linux (x64).
# Voraussetzung: .NET SDK 10 (https://dotnet.microsoft.com/download)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$targets = @('win-x64', 'osx-arm64', 'linux-x64')
foreach ($rid in $targets) {
    Write-Host "== $rid =="
    dotnet publish "$root/src/FF6APatcher.csproj" -c Release -r $rid -o "$root/dist/$rid" --nologo
    if ($LASTEXITCODE -ne 0) { throw "Build für $rid fehlgeschlagen" }
}
Write-Host "Fertig. Ausgabe in $root/dist"
