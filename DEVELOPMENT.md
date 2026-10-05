# Development setup

## Steam or GOG

- Install Disco Elysium, version `2024-04-23` or later.
- Install .NET SDK v6
  - https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-6.0.428-windows-x64-installer
  - This version was required to generate the plugin from a template, but perhaps a different version will work for you.
- Download BepInEx v6 (4901521, build date 2024-02-10T05:53:59), for IL2CPP, Windows, x64
  - https://builds.bepinex.dev/projects/bepinex_be
  - https://builds.bepinex.dev/projects/bepinex_be/688/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.688%2B4901521.zip
- Extract BepInEx into the game directory (e.g. in `Steam\steamapps\common\Disco Elysium`)
- Run the game once. You should see the BepInEx console.
- From the directory `Disco Elysium\BepInEx\interop`, copy these files:
  - `Assembly-CSharp.dll`
  - `DialogueSystem.dll`
  - `IL2Cppmscorlib.dll`
  - `Unity.TextMeshPro.dll`
  - `UnityEngine.AnimationModule.dll`
  - `UnityEngine.CoreModule.dll`
  - `UnityEngine.UI.dll`
- Create the directory `lib\` in project `FuriousTareIL2CPP\`. Paste the DLL files.
- Open a console in `FuriousTareIL2CPP`
- Install dependencies: `dotnet restore`
- Build the plugin DLL: `dotnet build`.
- Copy the built DLL from `bin\Debug\net6.0\FuriousTareIL2CPP.dll"`.
- Paste the built DLL into `Disco Elysium\BepInEx\plugins\FuriousTareIL2CPP`
- Run the game and test your changes!

## Formatting

File encoding, final newlines and whitespace are set in `.editorconfig`, which Rider and Visual Studio follow
automatically. Pull requests are checked by the "Lint" GitHub workflow.

To check C# files locally: `dotnet format whitespace --folder --verify-no-changes --exclude FuriousTareIL2CPP/obj FuriousTareIL2CPP/bin`

To fix them, run the same command without `--verify-no-changes`.

## Packaging a release

- Copy the BepInEx IL2CPP zip into the project directory `BepInEx`
- Ensure you can run unsigned PowerShell scripts: `Set-ExecutionPolicy -ExecutionPolicy Unrestricted -Scope Process`
- Run `package.ps1`. This will create an archive in a `temp\` directory.

## Reverse Engineering

Disco Elysium GOG version `2023-03-16` was built using Mono, not IL2CPP. This makes it easy to read the decompiled
code. Grab a copy, and open `Assembly-CSharp.dll` in something like `dnSpyEx` or `JetBrains dotPeek`.

FuriousTare provides a logger class, `DebugTypeLogger`, that will try to intercept and log all method calls on a
class/instance. Use it like so:

```csharp
DebugTypeLogger.RegisterPatches(typeof(FlashlightBehaviour));
```
