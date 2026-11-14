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
- Siphoning car tanks (can be switched off in the settings; then the hose's recipe is hidden too): make a siphon
  hose at a workbench (2 cured gut, 45 min), open a car's fuel cover and use the filler neck behind it. The transfer
  menu opens with the tank as the source; choose the container and the amount, a liter a game minute. Each tank holds
  5-20 l of lantern fuel (settings), found the first time it is siphoned and kept in the save. This came from the
  Generator Mod; the fuel covers were found by Modelfinder.
- Machines of other mods: the Generator Mod's generator (it requires Liquid Management) opens the transfer menu with
  its drum as the target to refuel it (whale oil or lantern fuel), and as the source to fill the fuel back into your
  containers (once for each fuel of the mix, each into its own containers); container and amount by choice.

Planned:

- Phase 2, water: potable or non-potable water in bottles (one at a time, never mixed); carried water limited by the
  bottles' capacity; the game's water bottles stay bottles.
- Phase 3: refuel the lamp by key, put out fires with water, spawn settings.

## Build

`dotnet build -c Release` (game path: `-p:TheLongDarkPath=...`, copy into Mods: `-p:CopyToMods=true`). The build checks
the texts in `Distributable/localizations`: valid JSON for ModComponent, English and German for every key, and a text
for every `LM_` key used in the code or the item files.

The hose's item shell and icons (`Distributable/bundle`) are built with the Unity project in `../LiquidManagementUnity`
(Unity 6000.0.60f1, `./build.sh`); its look is made at runtime (`src/HoseGeometry.cs`), so no game geometry is shipped.

Requires ModComponent, ModSettings and ModData.

## For mod authors

`LiquidManagement.MachineTankApi` puts a machine's tank into the transfer menu: `OpenFill(name, accepts, liters,
capacityLiters, add)` fills it from the player's containers, `OpenPourOut(name, liquids, liters, take, add)` pours it
back into them. Reference `LiquidManagement.dll` (the Generator Mod does), or find the type by reflection and check
`MachineTankApi.Version` (now 2) to keep Liquid Management optional for your mod.

## Credits

Ideas from the mods before it: Better Water Management (WulfMarius, ds5678, STBlade/dommrogers) and Fuel Manager
(The Illusion). No code of theirs is used.

## License

MIT, see LICENSE.
