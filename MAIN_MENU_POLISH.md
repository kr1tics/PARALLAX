# Main Menu visual polish

The existing left-aligned title/menu composition and all four actions are retained. PARALLAX uses Oxanium-Regular; buttons use the same face. Metadata, supporting copy and status use SpaceMono-Regular. MainMenu.unity holds explicit references to the existing SDF assets; UI Toolkit uses their associated source Font via TextCore. No font assets, TMP defaults or shared Settings styles are modified.

The near-black background replaces the instantiated orbital/starfield art. The original OrbitalMenuBackdrop source remains untouched for teammates. A live laboratory is intentionally not loaded: keeping this simple screen avoids duplicating tutorial cameras, lights, physics and puzzle initialization. No new textures or paid dependencies are added.

The title remains the largest element. Metadata is muted grey, text is soft cool-white, actions have 280×44 reference-pixel proportions and a subtle one-pixel border. Hover, focus and pressed states use restrained charcoal/grey changes with a 0.15-second transition; keyboard focus does not shift button text. Existing narrow/short layouts and scroll access are preserved. Copy describes the laboratory rather than a station adrift in space. The primary action is labelled START GAME; its existing Play method and Intro request are unchanged.

Files changed:
- Assets/MainMenu.unity — existing font references only.
- Assets/Resources/UI/MainMenu.uxml — remove orbital visual element, restrained archival labels/laboratory copy, uppercase action labels.
- Assets/Resources/UI/MainMenu.uss — typography hierarchy, spacing, neutral palette, button states and responsive sizing.
- Assets/Scripts/UI/MainMenuController.cs — apply source faces from the serialized SDF refs instead of instantiating orbital art; restore START GAME label on loading failure.
- Assets/Tests/MainMenu/MainMenuPlayModeTests.cs — assert actual regular font assignments, absence of orbital art and title width at the existing test resolutions.

Unity 6000.3.20f1 compiled successfully. 7/7 Main Menu PlayMode tests passed, including keyboard navigation, mouse return from Settings, persistent settings/preferences, focus restoration, missing-scene recovery, editor Quit behaviour and Start Game → Intro → Tutorial with Skip. Six rendered sizes checked: 1280×720, 1920×1080, 1024×768, 2560×1080, 640×360 and 480×800. PARALLAX title measurement fits each viewport; all four actions remain accessible. These are automated PlayMode input tests and actual UI Toolkit renders, not a manual native-window walkthrough or a standalone/WebGL build test. Report: Verification/main-menu-polish-playmode.xml.

Intro, tutorial scene, objective HUD, completion screen, gameplay mechanics and shared Settings assets are unchanged. All work stays on scene01-visual-refinement; main is untouched.
