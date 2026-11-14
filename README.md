# Liquid Management

A mod for The Long Dark (MelonLoader, game version 2.60). Every liquid container is a real item: it stays when it runs
empty and can be filled again, and liquids can be poured from one container into another by choice of source, target
and amount.

Work in progress. Works so far:

- Empty Lantern Fuel and Whale Oil containers stay in the inventory (a setting for each liquid).
- **Pour** in the inventory (on a fuel container, in place of Examine) opens the game's liquid menu with two more rows,
  *From* and *Into*: up/down chooses the row, left/right changes the container or the amount. Every container keeps its
  own liquid, so lantern fuel goes only into lantern fuel containers and whale oil only into whale oil containers.
  Afterwards you are back in the inventory where you were.

Planned:

- Phase 1, fuel: empty Lantern Fuel and Whale Oil containers stay; a transfer menu (source, target, amount); siphoning
  car tanks with a hose (can be switched off in the settings); refuelling machines of other mods by choice.
- Phase 2, water: potable or non-potable water in bottles (one at a time, never mixed); carried water limited by the
  bottles' capacity; the game's water bottles stay bottles.
- Phase 3: refuel the lamp by key, put out fires with water, spawn settings.

## Build

`dotnet build -c Release` (game path: `-p:TheLongDarkPath=...`, copy into Mods: `-p:CopyToMods=true`). The build checks
the texts in `Distributable/localizations`: valid JSON for ModComponent, English and German for every key, and a text
for every `LM_` key used in the code or the item files.

Requires ModComponent and ModSettings.

## Credits

Ideas from the mods before it: Better Water Management (WulfMarius, ds5678, STBlade/dommrogers) and Fuel Manager
(The Illusion). No code of theirs is used.

## License

MIT, see LICENSE.
