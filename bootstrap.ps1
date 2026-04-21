$ErrorActionPreference = "Stop"

dotnet --version | Out-Null
dotnet restore
dotnet build --configuration Release
