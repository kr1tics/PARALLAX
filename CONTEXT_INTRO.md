# Context Intro integration

Branch: `intro-ui-integration`, based on team main `8ecfb72`. Original Intro remains on `context-intro` at `4ae40fd` and is pushed to origin. Main is not modified by the integration.

## Game flow

Open **Assets/MainMenu.unity** and enter Play, then select **Play**. The existing menu loads the current tutorial StartScene. ContextIntroFlow adds the story overlay before gameplay begins, plays the three captions, then reveals the Future laboratory. The menu is never replaced with the old standalone Start Game screen.

Esc or the top-right text button skips the story and keeps the gentle final reveal. Movement, look, interactions, tutorial objectives and pause shortcuts remain locked until the overlay disappears. Loading StartScene directly in the editor bypasses the story for convenient level testing. Reloading MainMenu then selecting Play starts a fresh story.

## Visual and audio settings

Select **Assets/Resources/UI/ContextIntro.prefab** to edit fonts, sizes, colours, fade/hold timings and optional AudioClips. Main font is Oxanium-Regular SDF; metadata is SpaceMono-Regular SDF, matching the archived final settings. Sizes: year 80, caption 38, Archive 14, Signal 18, Skip 15. Archive top-left; Skip top-right; Signal below centre. Main text #DCE4E8; secondary #8B9298; signal #B9C6CD.

Default total duration is approximately 13.6 seconds, including .5 seconds of final black and a 1.75-second reveal. The third caption has one subtle signal flicker. Empty audio references are safe; real clip listening is still required after assignment.

Dynamic font assets and their TTF sources are retained; licences are in Documentation/FontLicenses.

## Changes and preservation

- Added ContextIntro.cs (reused original implementation), ContextIntroFlow.cs, and the configurable ContextIntro prefab.
- MainMenuController only queues the story before its existing scene load and cancels the queue if loading throws.
- GameSettingsMenu adds a startup input lock, retaining its main-menu context, settings and controls behavior.
- Adapted the existing menu gameplay test to skip the story before checking gameplay, retaining its original assertions. Added dedicated integration tests for full playback, glyph font, keyboard/button skip and menu replay.

MainMenu.unity, StartScene.unity, EditorBuildSettings, the tutorial/Hologram scripts, GlobalState, player, time and gravity scripts are unchanged from main. No cherry-pick or scene merge was used, and no conflict was resolved by overwriting team files.

## Verification

Full PlayMode regression: **37/37 passed**, including the existing menu, tutorial, settings and gameplay tests, plus three dedicated integration tests. Result: Verification/intro-integration-playmode.xml. Tested on Unity 6000.3.20f1. No gameplay scene or tutorial prefab was replaced.

The separate ContextIntroProject working copy has not been edited by this integration; its uncommitted files were archived locally before starting. Continue development from this repository and integration branch.
