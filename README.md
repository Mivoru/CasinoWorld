# Casino World

A tModLoader (Terraria 1.4.4) mod that adds two working casinos, a complete **Gambler** class, casino enemies, two bosses and a hardmode raid. Walk through the door, bet vanilla coins at real tables, and carry your luck into combat.

> **Status:** version 0.5, in development. Balance and art are still changing.

## Highlights

- **Two casinos** in their own dimension: one you build on the surface, and the High Rollers casino that opens underground in hardmode
- **Games:** European roulette, six-deck blackjack, Sic Bo, classic slots, four video slots with their own bonus games, scratch cards and a daily Fortune Wheel
- **The Gambler class:** no critical hits. Every hit rolls for a Jackpot (triple damage) or a Bust (half damage), and Luck tips the odds
- **All In:** hits fill the Pot. Cash it in and the dealer deals you a poker hand that sets your odds
- **23 weapons** made of cards, dice, chips, gold bars and reels
- **5 armour sets** from pre-hardmode to post-Moon Lord, and an Upgrade Machine that takes gear to +10 and turns each set into the next
- **8 enemies, 3 bosses and the High Stakes Night raid** with its Guard Captain; Fortuna waits after the Moon Lord
- **Crates** with a spinning reel, loot that follows your progress, and pity counters
- **The club:** lifetime statistics, 30 achievements, VIP levels, casino furniture and a penthouse of your own
- **Player games:** Texas Hold'em with bots in the free seats, and coin duels between players
- **Multiplayer:** server-rolled roulette, server-run poker and duels, and a shared leaderboard of the biggest wins

## Documentation

The full reference is in [docs](docs/README.md), written to become the wiki:

| Topic | Pages |
|---|---|
| Start here | [Getting started](docs/getting-started.md) |
| Gambling | [Casinos](docs/casinos.md), [Games](docs/games.md), [Video slots](docs/video-slots.md), [Crates](docs/crates.md), [The club](docs/club.md) |
| Class | [The Gambler](docs/gambler-class.md), [Weapons](docs/weapons.md), [Armour](docs/armor.md), [Upgrade Machine](docs/upgrade-machine.md), [Accessories](docs/accessories.md) |
| World | [Enemies](docs/enemies.md), [Bosses](docs/bosses.md), [High Stakes Night](docs/high-stakes-night.md), [NPCs](docs/npcs.md) |
| Reference | [Items](docs/items.md), [Settings and multiplayer](docs/settings.md), [Development](docs/development.md) |

## Quick start

1. A new character starts with a **Casino Blueprint** and a **Gambler's Handbook**. Existing characters get the blueprint when they enter a world.
2. Point the blueprint at flat ground and use it to build the surface casino.
3. Stand at the door and press Up (W by default).
4. Every bet pays **Loyalty Tokens**, win or lose. Spend them at the Prize Cashier.
5. Pick up a Gambler weapon, fill the **Pot** and press **V** to go All In. Change the key in Settings > Controls > Mod Controls.

## Progression

| Stage | What opens |
|---|---|
| Pre-hardmode | Surface casino, first Gambler weapons and armour, casino enemies |
| After Skeletron | The Wild Card (Golden Chip) and its three weapons |
| Hardmode | High Rollers casino, Upgrade Machine, video slots, High Roller set, hardmode enemies |
| After a mechanical boss | The Pit Boss (Vault Key), Pit Boss set, High Stakes Night |
| After Plantera | Spectral set and tier 3 weapons |
| After the Moon Lord | Celestial set, tier 4 weapons and Fortuna |

## Requirements

- tModLoader 1.4.4+
- [SubworldLibrary](https://steamcommunity.com/sharedfiles/filedetails/?id=2785100219)
- Optional: Boss Checklist (all bosses are registered)

## Building

Run `dotnet build` in this folder with the game closed, or use Workshop > Develop Mods > Build + Reload in game. It needs the .NET 8 SDK, which `global.json` pins. See [Development](docs/development.md).

## A note on gambling

Everything uses in-game currency only. Server hosts can switch gambling off, and every player can switch off screen shake and flashes. See [Settings](docs/settings.md).
