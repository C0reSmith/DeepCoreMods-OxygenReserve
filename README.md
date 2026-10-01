# DeepCore Mods – Oxygen Reserve

**Engineering Better Gameplay**

Oxygen Reserve automatically protects your suit's oxygen supply in **Stationeers** by refilling the installed air canister when its learned oxygen reserve reaches **10% or less**.

Each physical canister is learned individually, allowing Oxygen Reserve to work with canisters that have different capacities or preferred fill levels.

## Features

- Automatic oxygen refill at **10% reserve**
- Learns each physical oxygen canister individually
- Remembers learned canisters between game sessions
- Supports canisters with different oxygen capacities
- Automatically recognises previously learned canisters
- Prevents automatic refilling of unknown canisters
- Current-pressure safety guard
- Predictive pressure safety guard before oxygen is added
- Automatic refill pressure limited to **90% of structural maximum pressure**
- Native Stationeers keybinding
- Default learning key: **F7**
- Learning key can be rebound through the Stationeers Controls menu

## Learning a New Canister

Oxygen Reserve needs to learn what **100%** means for each new physical canister.

1. Fill the oxygen canister to the level you normally consider full.
2. Install the canister in your suit.
3. Press **F7** once.
4. Oxygen Reserve records the current oxygen amount as that canister's trusted full-tank reference.
5. From then on, Oxygen Reserve automatically refills that canister when its oxygen reserve reaches **10% or less**.

> **Important:** Fill the canister to your desired full level before pressing F7.

Pressing F7 on an already learned canister will **not** overwrite its existing trusted reference.

## Pressure Safety

Oxygen Reserve performs two safety checks before an automatic refill.

### Current Pressure Guard

If the canister is already at or above the automatic refill pressure limit, the refill is blocked.

### Predictive Pressure Guard

Before changing the canister atmosphere, Oxygen Reserve calculates the expected pressure after the proposed refill.

If the predicted pressure would exceed the safety limit, the refill is cancelled.

Automatic refilling is limited to **90% of the canister's structural maximum pressure**.

## Installation

Requires **BepInEx 5**.

1. Download the latest Oxygen Reserve release.
2. Extract the ZIP directly into your Stationeers installation folder.
3. The plugin should be installed at:

   `BepInEx\plugins\DeepCoreMods.OxygenReserve\DeepCoreMods.OxygenReserve.dll`

4. Launch Stationeers normally.

## Keybinding

**Control Group:** DeepCore Mods - Oxygen Reserve  
**Control:** Learn Full Tank Reference  
**Default Key:** F7

The key can be changed through the normal Stationeers Controls menu.

## Saved Canister References

Learned canister references are stored at:

`BepInEx\config\DeepCoreMods.OxygenReserve.canisters.txt`

This allows Oxygen Reserve to recognise previously learned physical canisters after restarting Stationeers.

Deleting this file resets all learned canister references. Each canister will then need to be learned again.

## Version

**DeepCore Mods – Oxygen Reserve v1.0.0**

## Author

**C0reSmith**

---

### DeepCore Mods

**Engineering Better Gameplay**
