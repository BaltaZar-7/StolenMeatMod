# Stolen Meat Mod

MelonLoader mod for The Long Dark. Drop meat outside, wildlife comes and takes it - then sticks around.

## How It Works

### Meat Tracking

Drop meat, fish, or qualifying food outdoors and the mod starts a timer on it. Meat inside buildings, indoor spaces, or containers is safe. Pick it up and tracking stops. Meat near a burning fire is protected for the fire's remaining duration.

Timers tick even in scenes you're not in - that venison you forgot in Mystery Lake is counting down while you're in Pleasant Valley.

### Meat Theft

When the timer hits the despawn threshold (default: 8 hours), it rolls the dice (default: 10% chance) to steal the meat. Hit = item destroyed, calories recorded. Miss = timer resets, try again next cycle. This also runs on scene load for items that expired while you were away.

### Predator Packs

Stolen meat attracts wolves. Calories from stolen items feed into predator regions - nearby existing ones or new ones at the theft location.

Every time meat disappears in a new location, the dice are rolled again (default: 25% chance). The **first time** this roll passes successfully, the location will begin to accumulate calories.

As calories pile up, the pack grows. Each additional 1000 calories leads to a new wolf wandering in, capped at a maximum of 5 wolves.

Wolves migrate, they don't magically appear - the mod redistributes existing wolves from around the region. If no wolves are nearby, the pack will not expand.

Only normal wolves are eligible, but newly created spawn regions can steal from each other! (This can result in packs "following you" if you arent careful... *evil laughter gif*) 

Packs have a base lifespan (default: 24 hours), extended by stolen calories. Kill all wolves and the pack disperses immediately, preventing more calorie accumulation until a new pack forms.

When packs expire, they return any remaining wolves to where they originally came from. In most situations this will mean immediate respawn of all remaining wolves, but if pack duration is long enough vanilla regions may respawn and some remaining wolves will disperse harmlessly into the wild.

## Installation

Download and install dependancy mods: Moddata and ModSettings  
Download and place the dll file into your \Mods folder.  
Change the settings in ModSettings to your liking!  

## Settings

Configurable through the in-game Mod Settings menu.

### Despawn

| Setting | Default | Range | Description |
|---|---|---|---|
| **Despawn Time (hours)** | 8 | 2 - 24 | Hours before the first steal roll. Resets on miss. |
| **Despawn Chance (%)** | 25 | 10 - 100 | Chance meat gets taken when the timer expires. |

### Meat Types

Raw meat and fish are always tracked. These toggles control extras.

| Setting | Default | Description |
|---|---|---|
| **Include Cured Meat** | Yes | Turn off to safely store cured meat outdoors. |
| **Include Fat** | Yes | Whether animal fat can be stolen. |
| **Include Animal Quarters** | No | Off by default - quarters are a big investment. |

### Predator Spawns

| Setting | Default | Range | Description |
|---|---|---|---|
| **Spawn Chance (%)** | 10 | 0 - 100 | Chance each meat theft in a new area begins accumulating a wolf pack. |
| **Pack Duration (hours)** | 24 | 8 - 72 | Base lifespan before the pack despawns. |
