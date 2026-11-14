#!/usr/bin/env sh
# Baut den FF6A-Patcher für Windows (x64), macOS (Apple Silicon) und Linux (x64).
# Voraussetzung: .NET SDK 10 (https://dotnet.microsoft.com/download)
set -e
root="$(cd "$(dirname "$0")" && pwd)"
for rid in win-x64 osx-arm64 linux-x64; do
  echo "== $rid =="
  dotnet publish "$root/src/FF6APatcher.csproj" -c Release -r "$rid" -o "$root/dist/$rid" --nologo
done
echo "Fertig. Ausgabe in $root/dist"
