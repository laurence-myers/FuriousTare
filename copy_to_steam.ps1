Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Copy-Item -Path "FuriousTareIL2CPP\bin\Release\net6.0\FuriousTareIL2CPP.dll" -Destination "C:\Program Files (x86)\Steam\steamapps\common\Disco Elysium\BepInEx\plugins\FuriousTare\"

echo "Copied!"
