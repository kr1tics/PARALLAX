# Scene 01 contextual controls

Settings launcher is moved from the top-right timeline status area to the bottom-right (24 px margin, 32 px height) only while Scene 01 is active. Its original placement and size are restored on leaving the scene. The shared Settings stylesheet and controller are unchanged.

A small Space Mono footer always shows `ESC / P — Settings / Resume`. Additional controls appear when relevant:

- `Q — Toggle jetpack` while the player's artificial gravity is inactive.
- `R — Reset checkpoint` while the delayed recovery hint is shown.
- `R — Replay` after the level is complete.

The full controls remain in Settings. Q toggles the existing jetpack/player-gravity mode; it does not toggle station gravity. Ctrl supplies downward thrust, Space upward thrust. Station gravity is controlled at the Past console.

## Hover recovery

Scene01Guidance observes the existing player Rigidbody, ArtificialGravity and Level1Controller. During Objective 02 (access relay solved, engineering relay not yet solved), the player must remain airborne without active personal gravity, with speed below 1.5 m/s and no floor within the existing ground-check distance. After 8 continuous gameplay seconds it shows:

`CTRL — Thrust down · SPACE — Thrust up`

With station gravity off, a second line reminds the player to restore it at the Past console; otherwise it suggests returning to walking with Q. Vertical input, landing, movement above the speed threshold or finishing the engineering relay resets the timer and removes the hint. Pause/Intro time does not count; all labels are hidden by the existing gameplay HUD group during Settings/Intro.

The delay and speed threshold are adjustable on **Level 1 - Engineering Access → Scene01Guidance**. No input bindings, gravity, physics or puzzle logic changed.

## Verification

Scene01GuidanceTests checks Objective 01 suppression, delayed Objective 02 detection, Ctrl dismissal, pause hiding, stage completion dismissal, the actual bottom-right launcher placement and restoration on returning to MainMenu. Typography checks now cover 16 scene TMP labels (nine authored, seven runtime).

Unity 6000.3.20f1: **44/44 PlayMode tests passed**, no compilation errors or unhandled runtime exceptions. Full report: `Verification/scene01-guidance-playmode.xml`.
