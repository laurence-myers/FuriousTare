# Unofficial Patch for Disco Elysium

This mod fixes some outstanding bugs in Disco Elysium - The Final Cut. Initially developed for v2024-04-23 and somewhat
tested up to v2026-01-05.

The mod has been tested on the GOG and Steam releases for Windows. Older versions of the game are not officially
supported (Steam might work, GOG will not, due to the game files being built in a different way).

## How to install

- Download the zip for your version from the "Releases" page.
  - For GOG or Steam, download "FuriousTareIL2CPP.zip"
- Unzip the files into your game directory
  - E.g. `Steam\steamapps\common\Disco Elysium`

## Configuration

You can disable specific patch categories via a config file.

Run the game once to generate the config file, then edit it. For example, to disable the change in flashlight behaviour,
open `Disco Elysium\BepInEx\config\FuriousTareIL2CPP.cfg`, and change the value like so:

```toml
[Patches]
StopWavingThatFlashlight = false
```

The patch names are listed in the `Bugs fixed` section.

Some patches have additional configuration:

```toml
[ScrollSensitivityTweaks]

## Scroll sensitivity for Dialogue. Original value: 20
# Setting type: Single
# Default value: 60
Dialogue = 60

## Scroll sensitivity for InventoryItemDescription. Original value: 10
# Setting type: Single
# Default value: 20
InventoryItemDescription = 20

## Scroll sensitivity for JournalTasksList. Original value: 10
# Setting type: Single
# Default value: 80
JournalTasksList = 80

## Scroll sensitivity for JournalWhiteChecks. Original value: 5
# Setting type: Single
# Default value: 40
JournalWhiteChecks = 40

## Scroll sensitivity for SaveLoadFileList. Original value: 30
# Setting type: Single
# Default value: 80
SaveLoadFileList = 80

## Scroll sensitivity for SkillDescription. Original value: 5
# Setting type: Single
# Default value: 10
SkillDescription = 10

## Scroll sensitivity for ThoughtCabinetDescription. Original value: 1
# Setting type: Single
# Default value: 10
ThoughtCabinetDescription = 10

## Scroll sensitivity for ThoughtCabinetList. Original value: 20
# Setting type: Single
# Default value: 40
ThoughtCabinetList = 40

## Scroll sensitivity for ThoughtInternalised. Original value: 20
# Setting type: Single
# Default value: 40
ThoughtInternalised = 40
```

## Mod features

### Bugs fixed

Some patches for dialogue fixes are marked with the "Articy ID", representing the unique node in the dialogue graph, e.g. `0x0100004500009218`.

- `AbilityLabelOverflow`: The ability label number and pips would overflow onto the next line when you have 10 or more
  points in an ability. This can happen if you have a Physique of 4, internalise "Revacholian Nationhood", and drink
  three different alcohols.
- `DialoguePathFixes`: `0x0100004500009218`: When asking Joyce for 10,000 reals, choosing "Hydrodynamique E40? Sounds fast." would trigger
  the correct response, _and also_ the response for the other dialogue choice ("I like high fidelity *anything*").
- `DisableCollageMode`: Collage mode adds 2 to 6 seconds loading time at game startup. We can disable it for a speed boost.
  (Just disable this specific patch if you want to use Collage mode.)
- `HandHud`: The hand HUD icons wouldn't respond to mouseover or clicks, except for a small area below the icons.
  - This was because the clock's "rectangle" actually overlapped and blocked the icons.
- `HandHudReplaceHeldItem`: When swapping a held consumable item (booze, smokes) with another, the new item would not be usable
  from the HUD until you un-equip & re-equip it.
- `MuzzleKimsBark`: When attempting to open the locked apartment door, Kim's "bark" voice-over clip would trigger multiple
  times, making it much louder.
- `ScrollSensitivityTweaks`: Some scrolling text, like the Thought Cabinet description panel, has very low scrolling sensitivity.
  - The sensitivity is configurable per panel.
- `SkipIncorrectVoiceOver`: `0x0100005800001E34`: The wrong voice-over clip plays when Cindy the Skull looks at Joyce.
  - This is a data problem - the voice-over clips for this dialogue entry were, perhaps, not recorded or imported into
    the game - and the correct clips do not exist in the game files. For now, we just skip playing the voice-over. 
- `StopWavingThatFlashlight`: When entering a conversation while holding a flashlight, the flashlight is supposed to stay still, but you could still
  wave it around.
- `TakeASwig`: Consuming a held substance sometimes doesn't play an animation when you're standing still, but does when
  you're moving, or if you switch to the inventory screen.
- `ThrowAGunLoseAGun`: When throwing a gun, if you had both Villiers and Ruby guns, you would lose the Villiers even if you threw Ruby's.
- `TweakHudWhiteSpace`: The bottom-right HUD UI white space could be improved:
  - The clock's right edge is too close to the day text.
  - The space between hand icons should align with the colon in the clock.
  - The joystick icons are snug against the hand icons.
- `VoiceOverFixAlternatives`: The wrong voice over clip would play when the dialogue entry has "alternative" (conditional) lines, if the dialogue sets a
  variable which changes the "alternative" line to use. Examples:
  - `0x0100004C00004BC7`: Attempting the Saviour Faire jump to get your RCM coat, the white check always plays the
    voice-over as if you've already attempted a jump.
  - `0x010000580001C11B`: "Talking" to the hanging corpse, comparing it to a harlequin, and ending the "chat", the text
    reads "Humour yourself with my harlequin features," but the voice-over is "Amuse yourself with my frank manners
    and my *memento mori* features."

### Faster startup

These patches are in the `StartupTweaks` config section. Set `Enabled = false` to disable all of them.

- `LoadingPriority`: During startup, Unity only spent ~4 ms per frame on loading, and frames were capped by vsync.
  Until the main menu is ready, the loading priority is raised to High, and vsync is turned off.
- `SkipShaderWarmup`: When entering the main menu, every variant of every shader was compiled (~18000 variants, 5 - 25
  seconds). Shaders are now compiled the first time they are drawn, and only the variants that are actually used.
- `SkipSplashMinimum`: The first loading screen always lasted at least 5 seconds, even though its work takes ~0.5
  seconds.

```toml
[StartupTweaks]

## Also turn off vsync and the frame rate cap while the game starts up
# Setting type: Boolean
# Default value: true
DisableVSyncWhileLoading = true

## Speed up the game startup. Set to false to disable every patch in this section
# Setting type: Boolean
# Default value: true
Enabled = true

## Raise Unity's loading priority (and optionally turn off vsync) until the main menu is ready
# Setting type: Boolean
# Default value: true
LoadingPriority = true

## Skip warming up every shader when entering the main menu. Shaders are compiled when first drawn instead
# Setting type: Boolean
# Default value: true
SkipShaderWarmup = true

## Skip the 5 second minimum duration of the first loading screen
# Setting type: Boolean
# Default value: true
SkipSplashMinimum = true
```

### Faster area transitions

These patches are in the `TransitionTweaks` config section. Set `Enabled = false` to disable all of them.

- `FadeSpeed`: The loading screen fades in and out faster. Configurable with `FadeSpeedMultiplier`.
- `SkipGC`: Every area transition ended with a forced garbage collection (0.3 - 0.4 seconds), right after the loading
  screen faded out. This is skipped; the garbage collector still runs by itself when needed.
- `ShowVisibleEntities`: After an area change, the game shows the characters and objects in view over dozens of frames
  (10 - 30 per frame). With faster transitions, characters would pop in after the loading screen faded out. A full
  pass is now done before the fade-out, and another one frame later, once the camera has settled.
- `SkipShaderWarmup`: Every area transition re-ran a full shader warmup (0.2 - 1 seconds, plus more work in the next
  frames), even though the shaders were already warmed up (or compiled when first drawn).
- `TextureWarmupBudget`: Every area transition spent up to 0.5 seconds streaming textures, blocking the game. The budget
  is reduced to 0.1 seconds; textures keep streaming in while the loading screen fades out. Configurable with
  `TextureWarmupSeconds`.

```toml
[TransitionTweaks]

## Speed up area transitions, e.g. when walking through a door. Set to false to disable every patch in this section
# Setting type: Boolean
# Default value: true
Enabled = true

## Fade the loading screen in and out faster. See FadeSpeedMultiplier
# Setting type: Boolean
# Default value: true
FadeSpeed = true

## Loading screen fade speed multiplier. Original value: 1
# Setting type: Single
# Default value: 2
FadeSpeedMultiplier = 2

## Show the characters and objects in view before the loading screen fades out, instead of over the following frames
# Setting type: Boolean
# Default value: true
ShowVisibleEntities = true

## Skip the forced garbage collection at the end of each area transition
# Setting type: Boolean
# Default value: true
SkipGC = true

## Skip warming up every shader again on each area transition
# Setting type: Boolean
# Default value: true
SkipShaderWarmup = true

## Spend less time streaming textures while the loading screen is shown. See TextureWarmupSeconds
# Setting type: Boolean
# Default value: true
TextureWarmupBudget = true

## Maximum time to spend streaming textures when entering an area. Original value: 0.5
# Setting type: Single
# Default value: 0.1
TextureWarmupSeconds = 0.1
```

### Diagnostics

These patches don't change the game. They are in the `Diagnostics` config section, and disabled by default. They write
to the BepInEx console and `BepInEx\LogOutput.log`.

- `StartupTimings`: Logs a timeline of where the time goes between launching the game and reaching the main menu.
- `TransitionTimings`: Logs a timeline of where the time goes when changing area, e.g. when walking through a door.
