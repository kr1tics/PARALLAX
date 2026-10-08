# Scene 01 contextual controls

Settings launcher is moved from the top-right timeline status area to the bottom-right (24 px margin, 32 px height) only while Scene 01 is active. Its original placement and size are restored on leaving the scene. The shared Settings stylesheet and controller are unchanged.

A small Space Mono footer always shows `ESC / P — Settings / Resume`. Additional controls appear when relevant:

- `Q — Toggle jetpack` while the player's artificial gravity is inactive.
- `R — Reset checkpoint` while the Objective 02 flight help is shown.
- `R — Replay` after the level is complete.

The full controls remain in Settings. Q toggles the existing jetpack/player-gravity mode; it does not toggle station gravity. Ctrl supplies downward thrust, Space upward thrust. Station gravity is controlled at the Past console.

## Hover recovery

Scene01Guidance observes the existing player Rigidbody, ArtificialGravity and Level1Controller. During Objective 02, flight help appears immediately when the player is airborne without active personal gravity. It sits directly below the top-left objective panel with a dark background:

`Hold CTRL to descend while floating.`

`SPACE — Thrust up`

It remains visible while practicing vertical movement and disappears on landing or completing the engineering relay. Settings and Intro hide the gameplay HUD. There is no delay or pause timer.

After placing the engineering cube while station gravity is off, the objective explicitly says:

`Return to the Past console.`

`Turn station gravity ON.`

Only after gravity is restored does it ask the player to switch to the Future. No input bindings, gravity, physics or puzzle logic changed; Intro typography is unchanged.

## Verification

Scene01GuidanceTests checks Objective 01 suppression, immediate Objective 02 detection, visibility during descent, pause hiding, stage completion dismissal, the actual bottom-right launcher placement and restoration on returning to MainMenu. Typography checks now cover 16 scene TMP labels (nine authored, seven runtime).

Unity 6000.3.20f1: **8/8 focused PlayMode tests passed** for final guidance, objective progression and typography; no compilation errors or unhandled runtime exceptions. Actual Unity captures checked at 960×540, 1280×720 and 1920×1080. Report: `Verification/scene01-objective02-playmode.xml`. The preceding full-suite run passed 43/44; its text overflow failure was corrected and passed in the focused rerun.

Scene01PresentationTests additionally verifies cube placement with gravity OFF, the explicit ON instruction, no text overflow, and the subsequent Future instruction only after gravity is restored.
