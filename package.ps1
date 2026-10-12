Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

dotnet build --configuration "Release"
if (-not $?) { throw "Build failed" }

Remove-Item -Force -Recurse -ErrorAction "Continue" -Path "temp/"
mkdir -Path "temp\package\IL2CPP\BepInEx\config","temp\package\IL2CPP\BepInEx\plugins\FuriousTare"
Expand-Archive "BepInEx\BepInEx-Unity.IL2CPP-*.zip" "temp\package\IL2CPP"

Copy-Item -Path "BepInEx\config\IL2CPP\BepInEx.cfg" -Destination "temp\package\IL2CPP\BepInEx\config\"
Copy-Item -Path "FuriousTareIL2CPP\bin\Release\net6.0\FuriousTareIL2CPP.dll" -Destination "temp\package\IL2CPP\BepInEx\plugins\FuriousTare"

$Today = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd')
Compress-Archive -Path "temp\package\IL2CPP\*" -DestinationPath "temp\FuriousTareIL2CPP_$Today.zip"

mkdir -Path "temp\package\PluginOnly\BepInEx\plugins\FuriousTare"
Copy-Item -Path "FuriousTareIL2CPP\bin\Release\net6.0\FuriousTareIL2CPP.dll" -Destination "temp\package\PluginOnly\BepInEx\plugins\FuriousTare"
Compress-Archive -Path "temp\package\PluginOnly\*" -DestinationPath "temp\FuriousTareIL2CPP_PluginOnly_$Today.zip"

Remove-Item -Force -Recurse -ErrorAction "Continue" -Path "temp/package"

echo "Done!"
