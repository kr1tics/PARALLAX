# Cold Storage (Level 3, final level)

Scene: `Assets/Scenes/Level3_ColdStorage.unity` (GUID kept from the original Puzzle 3 scene). Build order:
Main Menu → Tutorial → Power Restoration → Cargo Bay → Cold Storage. Cargo Bay's **Continue** loads Cold Storage;
Cold Storage ends with **Restart Level** and **Main Menu** only.

Cold Storage redesigns the original Puzzle 3, Coolant Exchange. It keeps that level's machinery meshes, physical
carrying, gravity seating and jetpack traversal.

## Story

Power Restoration restarted the station reactor in 2176. With no working coolant loop it is overheating. This room is
the **primary coolant plant** next to the reactor hall: a heat exchanger, its two cartridge sockets, the plant's
storage and the loop to the reactor. There is no second reactor here. Starting the exchanger stabilises cooling;
with power and cooling online, the station can sustain itself.

## Rules the player discovers

Cells **A** (amber) and **B** (cyan) are time-linked pairs (`Entangler` with spatial causality, as in Cargo Bay).
A 2176 cell's condition is the record of where its 2026 copy spent the last 150 years:

| 2026 copy rests in | Usable in 2176 when | Otherwise (tag reason) |
|---|---|---|
| S1 cartridge bay | valve on LOOP **and** flange F3 repaired | `BAY VENTED` / `LOOP LEAKED AT F3` |
| R radiator sink | its hatch is sealed: nothing on balance pan P, nothing in the opening | `HATCH LEFT OPEN` |
| Vault | never (active chill needed power) | `ACTIVE CHILL FAILED` |
| S2 hot socket | never (open cradle) | `UNSEALED SOCKET` |
| Balance pan | never | `LEFT ON BALANCE PAN` |
| Anywhere else | never | `UNSTORED` |

- **History is per cell.** On every arrival in 2176, each cell's outcome is re-read from its own 2026 site and that
  site's own conditions only.
  - Unchanged: its 2176 copy stays exactly where the player left it, in the same condition.
  - Changed: the 2176 copy is re-placed where its 2026 copy rests, in the new condition.
  - Consequences: a valve or F3 change touches only a cell in S1; the pan or the hatch opening touch only a cell in R.
- **Present-day handling never changes condition.** Carrying, borrowing or opening R in 2176 leaves a cell usable or
  boiled off.
- **Switching time while carrying** releases the cell in the era being left.
- **R's plug hatch** lifts only while a cartridge rests on balance pan P. The pan is unpowered, so it works in both
  eras.
  - The hatch never closes through the player or a cell.
  - If anything holds it open when 2026 is left, R is not sealed for those 150 years.
- **Plant valve** (2026 only; seized and iced in 2176):
  - `SERVICE` vents the loop, makes the vault hydraulics live and allows the F3 repair.
  - `LOOP` closes and seals the loop.
- **Flange F3** sits on the riser at hover height. It can be repaired only in 2026, with the line vented and empty
  hands. Unrepaired, it has burst under ice by 2176.
- **Completion (2176 only).** Needs a usable cell resting in S1 and a usable cell resting in S2, the valve on `LOOP`
  and F3 repaired. Then press E at the exchanger console. In 2026 the plant is running normally.

## Solution (one valid order)

1. **2176:** both cells are boiled off. R is frosted and shut, F3 has burst, the reactor loop is critical.
2. **2026:**
   1. Set the valve to `SERVICE`.
   2. Carry A from the vault onto pan P; R's hatch lifts.
   3. Fly B from S2 into R.
   4. Take A off P; the hatch seals R.
   5. Put A in S1.
   6. Hover to F3 and repair it.
   7. Set the valve to `LOOP`.
3. **2176:**
   1. A is usable in S1 and B is usable in R.
   2. Borrow A onto P to open R.
   3. Carry B to S2.
   4. Return A to S1.
   5. Start the exchanger.

Variants that also work:

- Swap the two cells' roles.
- Repair F3 earlier.
- Staged: preserve B first and use the still-boiled-off A′ as the 2176 counterweight before sealing A in S1.

## Recovery and safety

- `R` resets the plant: cells, both causality baselines, valve, F3, sockets, hatch, vault, timeline and player.
- A cell that leaves the room returns alone to its start.
- The 2026 vault door and R's hatch hold open for an occupant or a cell in their openings.
- `Esc` / `P` opens the shared settings overlay and blocks all plant interaction.

## Editing and validation

- `ColdStorageBuilder.Build` (menu **PARALLAX/Build Cold Storage Scene**) rebuilds the whole scene:
  - It starts from Power Restoration's player, settings and EventSystem.
  - It uses main's modular kit, `IndustrialCasing`, the hologram lamp materials and Future Environment Decay.
  - It sets Cargo Bay's completion destination and Build Settings.
- `ColdStorageBuilder.Audit` checks scripts, references and the gameplay set.
- Runtime: `ColdStorageController`, `ColdStorageControl`.
- Meshes are in `Assets/Models/CoolantExchange`; finishes are in `Assets/Material/CoolantExchange`.
- `TutorialCompletionScreen.finalLevel` shows Restart Level and Main Menu with the final message, and never Continue
  or the unavailable-level notice. Intermediate levels leave it off.
- **PlayMode tests:** `ColdStorageTests`.
  - Rule tests set up 2026 arrangements directly and say so.
  - The full solve uses only movement, jetpack, carrying, interaction and time input.
- **Built player:** `--parallax-verify-cold` solves the level with input only and saves screenshots under
  `Verification/`.
- Editor PlayMode runs rewrite the TMP font assets and `Assets/Shaders/TimeTravelFxMat.mat`; do not stage those.

## Implementation checks (Phase 3, 4 November 2026)

These ran in an isolated copy of this worktree, using Unity 6000.3.20f1 on Windows.

- **PlayMode, 53/53 passed:**
  - `ColdStorageTests` 15/15;
  - `CargoBayPlayModeTests` 15/15;
  - `Level1PlayModeTests` 17/17;
  - `TutorialCompletionTests` 6/6.
- **Scene audit:** `ColdStorageBuilder.Audit` passed (448 objects).
- **Windows development player:** built with 0 errors, using the committed font assets. `--parallax-verify-cold`
  passed: the level was solved with input only, and the final completion led to Main Menu. Gameplay HUD screenshots
  at 1280×720 and 1920×1080 showed complete text.
- **Not yet done:** the full suite, alternative routes, visual comparison against the other levels, and human
  playtesting.
