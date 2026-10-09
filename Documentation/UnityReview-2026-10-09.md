# Unity stability review — 9 October 2026

## Scope and repository safety

Started on `main`, tracking `origin/main`, with a clean working tree and no `MERGE_HEAD` or unresolved conflicts. No commits, pushes, branch resets, merges or pull requests were made.

The build contains `Assets/MainMenu.unity` followed by `Assets/StartScene.unity`. StartScene is both Level 1 and the tutorial. There is no implemented Level 2; completion's Continue action returns to MainMenu. The pause overlay has settings, controls and Resume; restart and return-to-menu buttons are not implemented there. R resets the current puzzle checkpoint, and the completion screen provides replay and Continue. These existing mechanics were preserved.

Reviewed the 44 project runtime C# files, editor integration, build/input/physics/render settings, five custom shaders, scene wiring, resources, UI markup/styles and existing test coverage. The asset audit covers all 36 scenes and 27 prefabs, including imported examples. Gameplay validation focuses on the two enabled build scenes. The project uses the built-in render pipeline and the new Input System. No level geometry, prefab layouts, shader source, authored materials or textures were intentionally changed.

## A. Bugs discovered

| Severity | Reproduction / root cause | Result |
| --- | --- | --- |
| High | Open Settings, then reload the gameplay scene. The persistent menu retained `IsOpen`, zero time scale and paused audio from the unloaded scene. An existing regression explicitly expected that old behavior. | Fixed to match the requested fresh-scene behavior. |
| Medium | Toggle the past gravity console for the first time. `ColourSwapper` selected the current material before flipping its state, so the first toggle stayed visually off and subsequent colours lagged one toggle. | Fixed. |
| Medium | Activate a temporary `DistortableObject` pulse, deactivate the object, then reactivate it. Deactivation stopped its coroutine without clearing the renderer property, leaving distortion stuck on. | Fixed; permanent toggle state remains preserved. |

All three issues failed targeted regression checks before their fixes. A fourth suspected issue in the timeline wipe was traced to the test harness: a bare `Graphics.Blit` received a zero built-in shader clock. A camera-based GPU probe confirmed the existing shader uses the correct scene-relative clock. The speculative timestamp change was discarded; `GlobalState.cs` and all shader source remain unchanged. A regression now checks the actual material through camera rendering before, during and after a transition following another scene load.

## B. Fixes implemented

| File | Change and reason |
| --- | --- |
| `Assets/Scripts/Settings/GameSettingsMenu.cs` | Track the scene paused by the overlay. On its unload, close/save the menu and restore normal time/audio. Unsubscribe on destruction; unrelated additive-scene unloads do not dismiss the overlay. Normal Resume still restores the previously saved time/audio state. |
| `Assets/Scripts/Utilities/ColourSwapper.cs` | Select the destination material on each toggle so the console matches gravity state immediately and after checkpoint reset. |
| `Assets/Scripts/Fx/DistortableObject.cs` | Stop/clear the coroutine and restore the authored temporary-effect baseline on disable. Leave permanent toggle mode unchanged. |
| `Assets/Tests/Gameplay/Level1PlayModeTests.cs` | Add wipe rendering, repeated console colour/reset, rapid timeline/pause, and loose-cube hatch safety regressions. Restore the fixture's player to dynamic physics before checkpoint reset to avoid test-induced kinematic-velocity warnings. |
| `Assets/Tests/Gameplay/InteractionDistortionTests.cs` | Add a regression for interrupted temporary distortion and successful reuse. |
| `Assets/Tests/Settings/GameSettingsPlayModeTests.cs` | Update the scene-reload regression to require an unpaused fresh scene while preserving preferences and a single persistent menu. |
| `Assets/Editor/ProjectValidation.cs` and `.meta` | Add a repeatable read-only scene/prefab/material/shader audit and Windows development build entry point. The audit reports findings without rewriting assets. |

The existing hatch is an immediate renderer/collider toggle, with no opening or closing animation. Its existing safety interlock checks the player's entire storage chamber plus rigidbody obstructions in the doorway, and fails open if its overlap buffer fills. No hatch redesign was needed.

## C. Test results

Executed with Unity 6000.3.20f1 on Windows. Local XML, logs and renders are under `Logs/` and `.utmp/` (ignored by Git).

- Original full PlayMode suite: **57 passed, 0 failed, 0 skipped**.
- Final full PlayMode suite: **62 passed, 0 failed, 0 skipped** (`Logs/review-verified-playmode.xml`).
- Pre-fix targeted run: **0 passed, 4 failed**. Three were confirmed gameplay bugs; the fourth was the invalid blit-based rendering check described above.
- Intermediate full run: 61/62 passed while investigating that rendering harness. Corrected camera rendering passes in the final full suite. Both corrected checkpoint fixtures also passed a focused rerun.
- C# compilation: successful. The final PlayMode log contains no C# compiler warnings, runtime exceptions or kinematic-velocity warnings.
- Windows x64 development build: **Succeeded, 0 errors, 0 warnings**, 192,611,658 bytes. Player: `Builds/Review/Parallax.exe`; evidence: `Logs/review-build-summary.txt` and `Logs/review-build.log`. The standalone player was built but not manually played through.
- Five new PlayMode regression tests were added; the existing reload test was strengthened to enforce the requested behavior.
- No separate EditMode test assembly existed in the repository. Synchronous NUnit tests inside the PlayMode assemblies are included in the PlayMode totals.

Existing coverage exercises walking/stopping/strafe, jumping, jetpack/open-space flight, real synthetic keyboard/mouse interaction, carrying against walls, time-linked cube release, both physics puzzles, player-only exit completion, checkpoint recovery, hatch player/carried-cube protection, UI navigation, settings persistence, intro skip, completion replay/Continue, audio integration, fonts and rendering. The end-to-end puzzle tests position objects programmatically and then exercise real physics; they are not a substitute for a human walking the complete route.

Menu layout tests render 1280×720, 1920×1080, 1024×768, 2560×1080, 640×360 and 480×800. Tutorial/completion renders also cover smaller desktop sizes. Static screenshots can establish layout/readability but cannot establish absence of every temporal flicker, collision issue or performance spike.

Reproduce the validation from the repository root with Unity 6000.3.20f1 (run one Editor instance at a time):

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.3.20f1/Editor/Unity.exe' -batchmode -projectPath $PWD -runTests -testPlatform PlayMode -testResults "$PWD/Logs/review-playmode.xml" -logFile "$PWD/Logs/review-playmode.log" -mainMenuArtifacts "$PWD/Logs/review-menu-images"
& 'C:/Program Files/Unity/Hub/Editor/6000.3.20f1/Editor/Unity.exe' -batchmode -quit -projectPath $PWD -executeMethod ProjectValidation.AuditAndBuild -logFile "$PWD/Logs/review-build.log"
```

Keep graphics enabled: some tests render shaders and UI into textures.

## D. Remaining issues and manual acceptance

The static GUID scan found missing references in imported TextMesh Pro examples, old render-pipeline migration metadata/unused material texture properties, modular-kit audio references, and the unused `Assets/Material/GravityField.mat` shader. Their intended original assets cannot be reconstructed reliably from this repository. They were not replaced with arbitrary assets or deleted. Unity's loaded-asset audit and build results below distinguish these from functional build-scene problems.

Unity's serialized-object audit recorded **53 findings** in `Logs/review-unity-assets.txt`. No missing gameplay scripts were found in the build scenes. Two static wall Animators in StartScene have stale Avatar references inherited from the modular kit; their animation controllers are unassigned. Matching references also occur in the source prefabs and old scenes. These remain low-priority asset cleanup, pending confirmation of the intended source model setup. Other findings concern imported demo scripts/materials, kit audio and unused shaders. The five custom shaders produced no shader diagnostic findings. Legacy prototype/backup/demo scenes were inspected for references but not fully playtested.

Manual acceptance still needs:

1. Walk the complete tutorial in the Windows player. Solve both puzzles using only normal controls, then replay and Continue. Include R recovery after losing a cube, a fall, and a pending time swap.
2. With gravity off, carry/drop a cube in the hatch, restore gravity and confirm SAFETY HOLD; remove it and confirm closure. Repeat while standing in the chamber and while switching timelines.
3. Test windowed/fullscreen switching, alt-tab/focus recovery, physical keyboard/mouse navigation, and standalone Quit at 1920×1080. Automated render textures do not verify operating-system fullscreen behavior.
4. Judge mouse feel, movement/carrying, lighting, flicker, text readability during motion, audio balance and performance on target hardware.
5. If WebGL is a target, install its Unity platform module and test browser pointer lock, fullscreen, focus recovery and Quit guidance. Only Windows standalone support is installed here.

The existing Shift+K completion shortcut remains enabled, as authored and covered by an existing test. It opens the completion screen without solving the puzzles; it was preserved as an intentional feature.

## E. Git status

All requested changes remain uncommitted on `main`. There were no teammate changes at the initial inspection. Unity-generated solution, material, font-cache and package-settings changes are reviewed and restored to their initial contents after validation. Scenes, prefabs and shader source remain unchanged. The retained files are those listed in section B plus this report.

Final verification: `git diff --check` passed; no unmerged index entries. Six existing files are modified (three runtime scripts and three test files), and three files are new (the editor audit/build script, its meta file, and this report). Nothing was committed or pushed.
