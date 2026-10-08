# Station gameplay testing

## Local branches

| Branch | Based on | Contents |
| --- | --- | --- |
| `feature/space-station-environment` | `main` | Starfield, living/dead Earth, exterior hull faces and collision, station deck gravity, modular-floor ground layer wiring |
| `fix/player-movement-interactions` | `feature/space-station-environment` | Walking/jumping, automatic open-space flight, stable pickup/drop, wall occlusion, scene cleanup, gameplay regression tests |
| `fix/settings-scroll-controls` | `main` | Controls reference, menu layout, slower bounded wheel/trackpad scrolling, scrolling regression test |
| `test/station-gameplay` | Gameplay branch plus settings branch | Combined build for acceptance testing |

These branches are local. `main` is unchanged. Test the combined branch before merging or publishing.

## Automated checks

Open the project in Unity, then **Window > General > Test Runner > PlayMode**. Run `Parallax.Gameplay.Tests.GameplayRegressionTests` and `Parallax.Settings.Tests.GameSettingsPlayModeTests` together (19 tests). Tests use the real `StartScene` and synthetic keyboard/mouse input. Gameplay tests access the default Unity assembly through reflection because test asmdefs cannot reference `Assembly-CSharp` directly.

Coverage includes modular-deck walking with the jetpack off, stopping/reversing/strafe, one jump per press, Q transitions, open-space thrust, pause/resume, E and click pickup/drop, carrying while walking, rotated/scaled parents, child colliders, wall blocking, repeated grabs, dropping during a real time switch, gravity restoration, scene reload and menu preferences, wheel/trackpad deltas in both directions, and footer visibility.

## Manual acceptance pass

Use `test/station-gameplay` in Unity, or refresh the rebuilt local web player at <http://127.0.0.1:8765/>. Click the game to capture the mouse; **P** opens settings reliably when the browser uses Escape for pointer lock.

1. Start with the jetpack off. Walk and strafe with WASD, release the keys, turn the camera and repeat. Walk across tile seams. Check that the player stops rather than continuing to slide.
2. Jump with Space. Hold it through landing: it should not cause repeated jumps. Release and press again to jump again.
3. Toggle Q, ascend with Space and descend with Ctrl, then toggle Q off inside the station. Walking should resume after landing. Leave the station: space should remain weightless, and thrusters should work even with Q off. Re-enter and check that gravity returns. Test roof decks as well as the interior.
4. Aim at a cube and press E or click once to grab it. Walk, strafe and turn while holding it. Press E/click again while looking away: it should drop once. Repeat pickup/drop several times.
5. Carry a cube against a wall. It should collide rather than pass through. Try grabbing a cube behind a wall: the wall should block the interaction.
6. While holding a cube, open settings, scroll and click menu buttons. The cube and player should stay paused. Resume and release the cube. Grab the time-linked cube and switch time with F/T: it should release when its version disappears.
7. Open settings and scroll with a wheel and trackpad. Small inputs should move a small amount; a large event should not jump a full page. Reach the controls list, scroll back up, and check that Resume stays visible. Repeat with a narrow window.
8. Check the exterior from outside in both time states. The hull should remain visible; Earth should be alive in the past and barren in the future.

For feedback, note the branch/build, time state, jetpack state, object and the exact keys/actions that reproduce an issue. Automated checks establish repeatable behavior; this manual pass checks the feel of movement, carrying and scrolling.
