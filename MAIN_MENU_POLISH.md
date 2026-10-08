# PARALLAX Main Menu — Temporal Fracture

The left title/action composition now balances an actual laboratory view on the right. Copy is reduced to PARALLAX, ONE LABORATORY. / TWO TIMELINES., and What happened between them? Metadata remains ARCHIVE_2176 and 150 YEARS OF SILENCE; the footer now says A FOUR SURE PROJECT.

START GAME → remains the existing Play action, with a 280×48 reference-pixel button. SETTINGS / CONTROLS / QUIT are a horizontal text-navigation row with hover brightness and a thin keyboard-focus underline. Narrow layouts reduce the background's prominence and retain scroll access. Existing mouse, keyboard, Settings, Quit and Intro transition behaviour are unchanged. Oxanium-Regular and SpaceMono-Regular are reused via the existing serialized TMP SDF/source-font references; no font or global defaults were edited.

## Laboratory visual

MenuLaboratoryPast.png and MenuLaboratoryFuture.png are Unity-rendered 1440×1080 captures from the existing StartScene, with the same camera at (-2, 1.55, 11), looking at (-7, 1.3, 5), FOV 65. They preserve the existing clean Past surfaces and Future decay shader. HUD/world instructions were hidden only in the disposable capture project, and the green route marker was muted through a temporary property block. The real tutorial scene and materials are unchanged.

TemporalMenuBackdrop draws the Future architecture with an irregular region of the Past over it, plus a narrow six-reference-pixel displaced architectural echo at the seam. This is a single continuous composition, with no side-by-side panels or comparison headings. Edges fade into the near-black menu. It uses two imported textures and static UI Toolkit geometry grouped by texture. There is no update timer, live dual-scene rendering, extra camera, RenderTexture, animated shader or post-processing in the shipped menu. Existing button colour transitions remain 0.15 seconds. The optional title glitch/flicker was omitted to keep typography clear.

The static capture prioritises reliability and the submission schedule. The visual will need new captures if the actual laboratory architecture changes. Runtime performance on target hardware/WebGL has not been profiled; the implementation avoids running tutorial simulation or rendering two environments in the menu. Texture Read/Write and mipmaps are disabled; maximum import dimension is 2048.

## Modified files

- Assets/Resources/UI/MainMenu.uxml — concise copy, visual host and primary/secondary action structure.
- Assets/Resources/UI/MainMenu.uss — asymmetric composition, background visibility and text-navigation states.
- Assets/Scripts/UI/MainMenuController.cs — attach the background to the menu document and retain the new primary label on loading recovery.
- Assets/Scripts/UI/TemporalMenuBackdrop.cs and .meta — static dual-texture geometry.
- Assets/Resources/UI/MenuLaboratoryPast.png and .meta, MenuLaboratoryFuture.png and .meta — captured existing environment.
- Assets/Tests/MainMenu/MainMenuPlayModeTests.cs — verify background references, primary label and transparent secondary actions alongside existing interaction/size tests.
- This report and Verification/temporal-menu-playmode.xml.

MainMenu.unity font references remain unchanged. Intro, tutorial HUD, objective system, completion screen, Settings assets and time/gravity/puzzle scripts are unchanged. The teammate's original OrbitalMenuBackdrop source is preserved.

## Verification

Unity 6000.3.20f1: 7/7 Main Menu PlayMode tests passed; no new compiler errors or warnings. Tested six render sizes: 1280×720, 1920×1080, 1024×768, 2560×1080, 640×360 and 480×800. Assertions cover font family/regular weight, title width, action access, mouse return from Settings, keyboard navigation and focus restoration, persistent preferences, editor Quit policy, missing-scene recovery and Start Game → Intro → Tutorial with Skip. Actual rendered images were inspected for composition and readability. Testing was automated in a disposable Unity project; no standalone/WebGL build or native-window manual walkthrough was performed.

Changes are local to scene01-visual-refinement. No push or merge to main.
