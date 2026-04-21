#!/usr/bin/env bash
set -euo pipefail

dotnet --version >/dev/null
dotnet restore
dotnet build --configuration Release
