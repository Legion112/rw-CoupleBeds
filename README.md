# Couple Beds

Partners (spouses, and optionally lovers/fiancés) automatically move into a shared
double bed — preferring a private, impressive bedroom — so they avoid the
"Want to sleep with partner" mood penalty.

## Development (needs Go and the .NET SDK)

    go run ./tools/rwmod build   # compile Assemblies/CoupleBeds.dll
    go run ./tools/rwmod link    # symlink this repo into RimWorld/Mods
    go run ./tools/rwmod log     # CoupleBeds lines from Player.log (-all for every error)

`build` keeps `Source/obj` so IDEs can resolve symbols; pass `-clean` to remove it.
Set RIMWORLD_DIR if the game isn't in ~/Games/RimWorld/game.

### IDEs

Open `CoupleBeds.sln` in the repo root (not `Source/`) so the Go tool is visible
too. The project finds the game's DLLs on its own, in this order: an explicit
`-p:RimWorldManaged=`, then `$RIMWORLD_DIR`, then the repo's own location inside
`Mods/`, then `~/Games/RimWorld/game`. Building from the IDE works with no extra
configuration; if the game is somewhere else, set `RIMWORLD_DIR` in the
environment the IDE inherits.
Then enable "Couple Beds" in the mod list. Settings: Options > Mod settings > Couple Beds.

## License

MIT — see [LICENSE](LICENSE).
