param(
    # The game's install directory
    [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\Disco Elysium"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Copy-Item -Path "FuriousTareIL2CPP\bin\Release\net6.0\FuriousTareIL2CPP.dll" -Destination "$GameDir\BepInEx\plugins\FuriousTare\"

echo "Copied!"
