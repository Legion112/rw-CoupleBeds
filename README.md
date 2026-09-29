# Couple Beds

Partners (spouses, and optionally lovers/fiancés) automatically move into a shared
double bed — preferring a private, impressive bedroom — so they avoid the
"Want to sleep with partner" mood penalty.

## Development (needs Go and the .NET SDK)

    go run ./tools/rwmod build   # compile Assemblies/CoupleBeds.dll
    go run ./tools/rwmod test    # run the unit tests
    go run ./tools/rwmod mutate  # check the tests would catch a broken rule
    go run ./tools/rwmod art     # re-render About/*.png from art/*.svg
    go run ./tools/rwmod link    # symlink this repo into RimWorld/Mods
    go run ./tools/rwmod log     # CoupleBeds lines from Player.log (-all for every error)

`build` keeps `Source/obj` so IDEs can resolve symbols; pass `-clean` to remove it.
Set RIMWORLD_DIR if the game isn't in ~/Games/RimWorld/game.

### Layout

`Source/Core` holds every decision the mod makes — who counts as a couple, which
beds are candidates, how beds are scored, when a couple is worth moving — as
plain C# with no reference to RimWorld. `Source/CoupleBeds.cs` is the adapter:
it turns a `Map` into a `ColonySnapshot`, asks `CoupleBedPlanner` for a list of
assignments, and carries them out.

RimWorld's `Pawn` and `Building_Bed` cannot be constructed outside a running
game, so `Tests/` compiles the same `Source/Core` files for a modern runtime and
drives them with fakes. `IBedAccess` keeps the expensive per-(pawn, bed) checks
— reachability above all — behind an interface, which is both what keeps them
lazy in game and what lets the tests count them.

`art/` holds the SVG sources for the mod icon and preview image; the rendered
PNGs in `About/` are committed, so `rwmod art` is only needed after editing one.

`rwmod mutate` is the guard against tests that pass without asserting anything:
it breaks one rule at a time and fails if the suite stays green.

The game itself is only needed for `build`, `link` and `log`; `test` and
`mutate` run without RimWorld installed.

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
