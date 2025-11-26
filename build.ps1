if (Test-Path -Path .\publish\win-x64) {
    Remove-Item -Recurse -Force .\publish\win-x64
}

Remove-Item -Path $PSScriptRoot\bin -Recurse -Force -Confirm:$false -ErrorAction SilentlyContinue
Remove-Item -Path $PSScriptRoot\obj -Recurse -Force -Confirm:$false -ErrorAction SilentlyContinue

dotnet publish .\src\WebsiteMonitor.csproj -c Release -r win-x64 /p:PublishAot=true -o .\publish\win-x64

Set-Location -Path $PSScriptRoot\publish\win-x64

.\WebsiteMonitor.exe --generate-yaml-config

.\WebsiteMonitor.exe