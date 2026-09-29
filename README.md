# Couple Beds

Partners (spouses, and optionally lovers/fiancés) automatically move into a shared
double bed — preferring a private, impressive bedroom — so they avoid the
"Want to sleep with partner" mood penalty.

## Development (needs Go and the .NET SDK)

    go run ./tools/rwmod build   # compile Assemblies/CoupleBeds.dll
    go run ./tools/rwmod link    # symlink this repo into RimWorld/Mods
    go run ./tools/rwmod log     # CoupleBeds lines from Player.log (-all for every error)

Set RIMWORLD_DIR if the game isn't in ~/Games/RimWorld/game.
Then enable "Couple Beds" in the mod list. Settings: Options > Mod settings > Couple Beds.

## License

MIT — see [LICENSE](LICENSE).
