# Casino World

A tModLoader (Terraria 1.4) mod that adds casinos to the world. You walk in through the front door of a building, enter a separate casino dimension, and gamble with vanilla coins.

## Status

Early development. Done so far (not yet play-tested):

- Surface casino building with a front-view facade, generated near spawn
- Casino interior as a separate dimension (SubworldLibrary)
- Roulette (European, single zero), server-rolled in multiplayer
- Blackjack (6 decks, dealer stands on 17, 3:2, double, one split)

Planned: casino NPCs, gambling rewards, the Gambler class, an underground hardmode casino with slot machines and gear upgrades.

## Requirements

- tModLoader 1.4.4+
- [SubworldLibrary](https://steamcommunity.com/sharedfiles/filedetails/?id=2785100219)

## Building

```
dotnet build
```

Run it in this folder with the game closed (or use Workshop > Develop Mods > Build + Reload in game). It needs the .NET 8 SDK; `global.json` pins it.

`lib/SubworldLibrary.dll` is only a compile-time reference. It is extracted from the SubworldLibrary `.tmod` and is not packed into the mod.

## Dev helpers

- `/casinohere` builds the surface casino where you stand.
- Casino Door (crafted from 1 Wood) places a standalone casino entrance.
