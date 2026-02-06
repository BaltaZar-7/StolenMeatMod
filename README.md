# Stolen Meat Mod

MelonLoader mod for The Long Dark. Drop meat outside, wildlife comes and takes it - then sticks around.

## How It Works

### Meat Tracking

Drop meat, fish, or qualifying food outdoors and the mod starts a timer on it. Meat inside buildings, indoor spaces, or containers is safe. Pick it up and tracking stops. Meat near a burning fire is protected for the fire's remaining duration.

Timers tick even in scenes you're not in - that venison you forgot in Mystery Lake is counting down while you're in Pleasant Valley.

### Meat Theft

When the timer hits the despawn threshold (default 8h), it rolls the dice (default 25%) to steal the meat. Hit = item destroyed, calories recorded. Miss = timer resets, try again next cycle. This also runs on scene load for items that expired while you were away.

### Predator Packs

Stolen meat attracts wolves. Calories from stolen items feed into predator regions - nearby existing ones or new ones at the theft location.

Every time meat disappears in a new location, the dice are rolled again (default: 10% chance). The **first time** this roll passes successfully, the location will begin to accumulate calories.

As calories pile up, the pack grows. Each wolf costs a calorie threshold (default 1000 cal), capped at the max pack size.

Wolves migrate, they don't magically appear - the mod redistributes existing wolves from around the region. If no wolves are nearby, the pack will not expand.

Only normal wolves are eligible, but newly created spawn regions can steal from each other! (This can result in packs "following you" if you arent careful... *evil laughter gif*) 

Packs have a base lifespan (default 24h), extended by stolen calories. Kill all wolves and the pack disperses immediately, preventing more calorie accumulation until a new pack forms.

When packs expire, they return any remaining wolves to where they originally came from. In most situations this will mean immediate respawn of all remaining wolves, but if pack duration is long enough vanilla regions may respawn and some remaining wolves will disperse harmlessly into the wild.

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
| **Max Pack Size** | 3 | 1 - 10 | Max wolves per pack. |
| **Pack Radius (meters)** | 250 | 50 - 500 | Min distance between packs. Stolen meat within radius feeds the existing pack instead. |
| **Max Packs per Scene** | 3 | 1 - 25 | Cap on simultaneous packs per scene. |
| **Pack Duration (hours)** | 24 | 8 - 72 | Base lifespan before the pack despawns. |
| **Calories per Predator** | 1000 | 500 - 2500 | Calorie threshold per wolf. 1000 cal = first wolf, 2000 = second, etc. |
| **Calories per Additional Hour** | 500 | 100 - 2500 | Stolen calories extend pack life. 500 cal = +1 hour. 
