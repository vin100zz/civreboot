@echo off
cd /d "%~dp0server"
dotnet run --urls http://localhost:5001
