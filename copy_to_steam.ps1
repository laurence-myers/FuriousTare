param(
    # The game's install directory. Defaults to the library inside Steam's own install directory
    [string]$GameDir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not $GameDir) {
    $SteamPath = (Get-ItemProperty -Path "HKCU:\Software\Valve\Steam" -Name "SteamPath").SteamPath
    $GameDir = Join-Path $SteamPath "steamapps\common\Disco Elysium"
}

Copy-Item -Path "FuriousTareIL2CPP\bin\Release\net6.0\FuriousTareIL2CPP.dll" -Destination "$GameDir\BepInEx\plugins\FuriousTare\"

echo "Copied!"
