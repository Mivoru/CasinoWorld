# The Gambler class

Gambler weapons use their own damage class. It takes generic damage bonuses but never crits.

## Jackpot and Bust
| Roll | Base chance | Damage |
|---|---|---|
| Jackpot | 5% | x3 |
| Bust | 20% | x0.5 |
| Normal | 75% | x1 |

At these odds the average multiplier is exactly 1.0. Weapon tooltips show your current odds. Crit from vanilla reforges turns into half as much jackpot chance.

## Luck
Each point of Luck gives +1% jackpot and -2% bust. That is about +3% average damage per point up to Luck 10, where bust reaches zero, then +2% per point. Luck can raise the jackpot chance to at most 50%.

## The Pot
Hits fill the Pot: 1 point, 2 for a bust, 3 for a jackpot. It holds 100, and drains after three seconds without a hit unless it is full. Side hits (coins, embers, wall blasts) do not fill it. Dying empties it.

The meter sits at the top centre of the screen. Drag it with the inventory open, right-click it to reset, or place it under your character in the [settings](settings.md).

## All In
With a full Pot, press the All In key (**V** by default). A shockwave hits everything within 15 tiles for three times your weapon's damage, and the dealer deals a hand:

| Hand | Chance | Jackpot bonus | Extra bust | Duration |
|---|---|---|---|---|
| Pair | 45% | +20% | +10% | 5 s |
| Two Pair | 25% | +25% | +10% | 5 s |
| Three of a Kind | 15% | +25% | +10% | 6 s |
| Full House | 9% | +30% | none | 6 s |
| Four of a Kind | 4.5% | +35% | none | 7 s |
| Royal Flush | 1.5% | +45% | none | 8 s |

When it ends, 10% of the damage you dealt is paid out as a blast (at most six times your weapon's damage, no jackpot roll). The Pot does not fill while All In runs. In the project's model All In adds roughly 15-17% damage over a long fight.

## Jackpots in a row
Consecutive jackpots raise the pitch of the sound. Three in a row trigger the Triple Jackpot: lights around the screen and a rain of coins, at most once per 20 seconds. It is visual only.

## Reforges
Any Gambler weapon can roll these in addition to vanilla prefixes.

| Prefix | Damage | Speed | Knockback | Jackpot chance |
|---|---|---|---|---|
| Lucky | +12% | +6% | | +4% |
| Rigged | +5% | | | +6% |
| Loaded | +15% | | +10% | |
| Shady | | +10% | | +2% |
| Cursed | -10% | | | -3% |
| Unlucky | | | | -4% |

## Fates
After Fortuna, on the Fates page of the Ledger. Each costs 15 Fate Shards once; one at a time is active, switching is free outside boss fights.

| Fate | Gives | Takes |
|---|---|---|
| Control | Normal hits +50% | Jackpots deal x2.25 instead of x3 |
| Dealer | Pot fills 50% faster, All In lasts 50% longer | -10% damage outside All In |
| Fool | Jackpots deal x3.1 | At least 12% busts; a bust costs 12 life, at most once a second |

Change in damage by jackpot chance (`tools/gambler_fates.py`, All In included):

| Jackpot chance | Control | Dealer | Fool |
|---|---|---|---|
| 10% | +17% | +2% | +1% |
| 18% | +11% | +1% | -1% |
| 24% | +5% | 0% | -1% |
| 35% | -4% | 0% | 0% |
| 41% | -7% | 0% | +1% |

Measured in game with Infinity Slot against 50 defense (average of two 30 s runs, about 10% noise each; the model leaves out defense):

| Build | No Fate | Control | Dealer | Fool |
|---|---|---|---|---|
| Celestial set only (18% jackpot, no All In) | 2,350 | 2,910 | 2,320 | 2,460 |
| Everything upgraded, 6 accessories, All In (41% jackpot) | 15,820 | 10,870 | 14,180 | 16,710 |

## Hot Streak
Gambler set bonus: after a jackpot, +10% Gambler damage for four seconds.
