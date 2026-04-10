@echo off
dotnet build AstroFinder.App\AstroFinder.App.csproj -f net8.0-windows10.0.19041.0
dotnet run --project AstroFinder.App\AstroFinder.App.csproj -f net8.0-windows10.0.19041.0
