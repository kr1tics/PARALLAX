# Station gameplay testing

## Branches

| Branch | Based on | Contents |
| --- | --- | --- |
| `feature/space-station-environment` | `main` | Starfield, living/dead Earth, exterior hull faces and collision, station deck gravity, modular-floor ground layer wiring |
| `fix/player-movement-interactions` | `feature/space-station-environment` | Walking/jumping, automatic open-space flight, stable pickup/drop, wall occlusion, scene cleanup, gameplay regression tests |
| `fix/settings-scroll-controls` | `main` | Controls reference, menu layout, slower bounded wheel/trackpad scrolling, scrolling regression test |
| `test/station-gameplay` | Gameplay branch plus settings branch | Combined build for acceptance testing |

The feature and fix branches are published on `origin` and merged into `main`. `test/station-gameplay` remains local. Both `main` and the local test branch contain the combined gameplay and settings changes.

## Automated checks

Open the project in Unity, then **Window > General > Test Runner > PlayMode**. Run `Parallax.Gameplay.Tests.GameplayRegressionTests`, `Parallax.Settings.Tests.GameSettingsPlayModeTests` and `Parallax.MainMenu.Tests.MainMenuPlayModeTests` together (26 tests). Tests use the real scenes and synthetic keyboard/mouse input. Gameplay and main-menu tests access the default Unity assembly through reflection because test asmdefs cannot reference `Assembly-CSharp` directly.

Coverage includes modular-deck walking with the jetpack off, stopping/reversing/strafe, one jump per press, Q transitions, open-space thrust, pause/resume, E and click pickup/drop, carrying while walking, rotated/scaled parents, child colliders, wall blocking, repeated grabs, dropping during a real time switch, gravity restoration, scene reload and menu preferences, wheel/trackpad deltas in both directions, and footer visibility.

## Manual acceptance pass

### Main menu

1. Open `Assets/MainMenu.unity` and enter Play Mode. Check Play, Settings, Controls and Quit using mouse and keyboard (arrows/Tab and Enter). Escape closes an open shared panel; it does not start gameplay from the title screen.
2. Edit settings and return. Open Controls, scroll to the last hint and return. Confirm focus returns to the button that opened the panel.
3. Press Play. Verify the game starts in Future with station gravity enabled, the cursor captured, and movement/time switching working. Open settings with Esc/P and resume.
4. Repeat at 1280×720, 1920×1080, 1024×768, 2560×1080, 640×360 and 480×800. The main content can scroll in constrained windows; the settings footer must stay reachable.
5. In a standalone build, Quit exits the player. In the Editor it shows a Stop instruction. In WebGL it explains how to close the tab. Test browser pointer lock and fullscreen with a real click; browser security may require clicking the game again after focus loss.

The main-menu tests cover launch/build scene order, fresh gameplay state, preference preservation, duplicate-menu prevention, shared controls, return focus, real keyboard/mouse input, missing-scene recovery, Editor Quit and layout at the six sizes above. Pass `-mainMenuArtifacts <absolute-directory>` to a Unity PlayMode test run to export actual UI Toolkit renders. Build and browser acceptance still require the corresponding platform module.

Validation on 8 October 2026 with Unity 6000.3.20f1: all 26 PlayMode tests passed; the final seven-test main-menu pass also passed after styling adjustments. UI Toolkit renders were checked, and the Windows x64 player build succeeded. Results, logs and screenshots are local under `.utmp/main-menu-validation`; the player is under `.utmp/main-menu-player`. WebGL support is not installed on this machine, so browser pointer lock/fullscreen and WebGL Quit remain manual acceptance checks. Standalone Quit also needs a player click-through. Existing negative-scale BoxCollider warnings remain in `EntangledGravSwitch`; one intermediate run failed the existing wall/carry test's exact paused-position comparison, then passed on rerun. Those unrelated gameplay objects and assertions were not changed.

### Station gameplay

Use `main` or `test/station-gameplay` in Unity, or refresh the rebuilt local web player at <http://127.0.0.1:8765/>. Click the game to capture the mouse; **P** opens settings reliably when the browser uses Escape for pointer lock.

1. Start with the jetpack off. Walk and strafe with WASD, release the keys, turn the camera and repeat. Walk across tile seams. Check that the player stops rather than continuing to slide.
2. Jump with Space. Hold it through landing: it should not cause repeated jumps. Release and press again to jump again.
3. Toggle Q, ascend with Space and descend with Ctrl, then toggle Q off inside the station. Walking should resume after landing. Leave the station: space should remain weightless, and thrusters should work even with Q off. Re-enter and check that gravity returns. Test roof decks as well as the interior.
4. Aim at a cube and press E or click once to grab it. Walk, strafe and turn while holding it. Press E/click again while looking away: it should drop once. Repeat pickup/drop several times.
5. Carry a cube against a wall. It should collide rather than pass through. Try grabbing a cube behind a wall: the wall should block the interaction.
6. While holding a cube, open settings, scroll and click menu buttons. The cube and player should stay paused. Resume and release the cube. Grab the time-linked cube and switch time with F/T: it should release when its version disappears.
7. Open settings and scroll with a wheel and trackpad. Small inputs should move a small amount; a large event should not jump a full page. Reach the controls list, scroll back up, and check that Resume stays visible. Repeat with a narrow window.
8. Check the exterior from outside in both time states. The hull should remain visible; Earth should be alive in the past and barren in the future.

For feedback, note the branch/build, time state, jetpack state, object and the exact keys/actions that reproduce an issue. Automated checks establish repeatable behavior; this manual pass checks the feel of movement, carrying and scrolling.
