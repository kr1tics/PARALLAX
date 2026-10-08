# Scene 01 visual and UX refinement

Work branch: `scene01-visual-refinement`, based on `intro-ui-integration`.

## Scope

Only StartScene presentation is refined. Main Menu, Intro prefab/settings, puzzle completion logic, player controller, interaction ray, pressure plate logic, gravity and causality scripts are retained.

- Compact TMP objective panel shows one instruction derived from the actual timeline, held cube, cube placement, gravity and existing Level1Controller progression flags. The first required action in the Future remains switching to the Past because the existing Future cube is damaged.
- Oxanium-Regular SDF for objective titles and station signs; SpaceMono-Regular SDF for instructions, status and contextual prompts.
- Environmental tutorial paragraphs become short station signage. Dynamic console/storage signs retain actual status. Projection panels and the floating objective marker are hidden; their original references remain available.
- Cube casing, narrow face insets, muted plate finish and four placement strips replace featureless grey/white presentation. Highlighting follows the existing occlusion-aware interaction ray. Plate feedback follows PressureButton.IsPressed.
- Existing Future cube fault shader and Future Environment Decay shader are preserved. Scene-only property blocks adjust tint, grime and flicker. Past lighting is neutral; Future is dimmer, with restrained warm task lighting at the two relays.
- Presentation details have no enabled colliders or rigidbodies. No object positions, collision shapes, puzzle trigger connections, pressure requirements or entangler references are changed.
- Contextual prompts reuse actual interaction eligibility. Holding displays E — Place; eligible cubes show E — Pick up; relevant objectives show F — Switch Timeline. Controls remain available through Settings.

## Inspector controls

Select **Level 1 - Engineering Access** in StartScene, then **Level1Presentation**.

Typography, title/instruction size, scene-local material references, Past/Future light colour/intensity and Future decay amount are adjustable. Intro typography is separate and unchanged. The compact HUD, visual insets and relay task lights are created when entering Play mode; edit their saved parameters on Level1Presentation outside Play mode. Environmental signs are saved directly in StartScene.

Optional AudioClip slots: Pickup, Placement, Plate Activation, Timeline Switch, Relay Restoration, Door Unlock. Interaction/System volume controls are independent. Unassigned slots remain silent. No suitable audio files were present in the repository and no audio assets were downloaded. The placement hook fires when a held cube is released; activation/restoration hooks wait for actual puzzle events. Spatial sound can be added later without touching gameplay.

## Verification

Final verification: Unity 6000.3.20f1, **40/40 PlayMode tests passed**, zero compilation errors or unhandled runtime exceptions. Report: `Verification/scene01-refinement-playmode.xml`. The complete suite includes Scene01PresentationTests. Focused checks exercise real Past placement → Future activation, objective reset, fonts, pause hiding, absence of physics on new detail geometry and rendered views at 960×540, 1280×720 and 1920×1080.

Existing presentation tests are updated for the shorter console label and the additional R control row. Pause tests now take their baseline after the in-flight fixed step and compare authoritative Rigidbody positions, rather than unsettled render interpolation; gameplay scripts are unchanged.

Manual review: start through MainMenu, skip/finish Intro, aim at a cube, carry/release it, solve relay 01, release the engineering storage, restore relay 02 and exit. Compare Past/Future visibility. Assign suitable clips before listening to and balancing audio. Mouse feel, final loudness and personal readability preferences require a normal interactive play session.
