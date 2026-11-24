# Development

## Building
`dotnet build` with the game closed, or Workshop > Develop Mods > Build + Reload. .NET 8 SDK, pinned by `global.json`. `lib/SubworldLibrary.dll` is a compile-time reference only and is not packed into the mod.

## Layout
| Folder | Contents |
|---|---|
| `Common/Gambler` | Damage class, Jackpot / Bust roll, Pot, All In, meter |
| `Common/Games` | Roulette, blackjack, slots, video slots, Sic Bo, scratch cards, challenges, leaderboard |
| `Common/Crates` | Crate loot, opening, world generation, fishing |
| `Common/Upgrades` | Upgrade Machine and armour evolution |
| `Common/Systems` | Travel, world entrances, config, High Stakes Night, Boss Checklist |
| `Common/Effects` | Shared particles, boss titles, Triple Jackpot |
| `Content/Items`, `NPCs`, `Projectiles`, `Tiles` | Game content; each sprite sits next to its class |
| `Content/Subworlds` | Casino interiors |
| `Localization` | All text |

## Commands
| Command | Effect |
|---|---|
| `/casinohere` | Builds the surface casino where you stand |
| `/casinovip` | Builds the High Rollers entrance where you stand |

## Keeping these docs correct
Numbers are copied from the code. When you change a value, update the matching page: weapons and armour in `Content/Items`, odds in `GamblerPlayer` and `GamblerPot`, crates in `CrateLoot`, upgrades in `GamblerUpgrades` and `ArmorEvolution`, bosses in `Content/NPCs/Boss`, the raid in `HighStakesNight`.
