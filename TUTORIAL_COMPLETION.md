# Tutorial completion

`TutorialCompletionScreen` is attached to the existing Level 1 - Engineering Access object in StartScene. It observes `Level1Controller.Completed`; it never sets puzzle completion flags. The existing exit invokes TryComplete, which requires both solved relays and the Future timeline. Entering the exit without those prerequisites does not show the screen.

The component acquires the existing StartupInputBlocked lock and pauses Time.timeScale as soon as completion is observed, preserving the final camera/environment. It uses unscaled time for a 1-second wait and staged 0.8-second fades. The existing Settings shell and HUD already respect that lock; their scripts/layout are unchanged. A sorting-order 100 uGUI Canvas reuses the existing EventSystem and Input System UI module. Cursor is unlocked and visible; buttons accept mouse/keyboard selection after the fade. Lock, time scale and cursor are restored on navigation or component disable.

All five text strings exactly match the request. Oxanium-Regular SDF is used for title/buttons and SpaceMono-Regular SDF for the description/final message. The existing scene typography observer skips only this new Canvas, so its title font/secondary colours cannot be overwritten. No fonts or global defaults change.

Replay loads the existing StartScene without queuing ContextIntro. This rebuilds puzzle states, cubes, doors, objectives, player position and the initial Future timeline/station gravity through the existing initialization code. No reset mechanics are duplicated.

Continue returns to the existing MainMenu while the next scene is absent. To connect a future section, set Next Scene on Level 1 - Engineering Access > TutorialCompletionScreen and enable that scene in the build scene list. An empty or unavailable destination safely falls back to MainMenu. No new level is created.

Files changed: new TutorialCompletionScreen.cs and .meta; StartScene.unity (one component and references); Scene01Typography.cs (exclude new Canvas only); new TutorialCompletionTests.cs and .meta; Level1PlayModeTests.cs (adapt completion/reset test for the new pause modal). Intro, menu, Settings, HUD, objective logic and time/gravity scripts are unchanged.

Verification: Unity 6000.3.20f1 compiled successfully; 48/48 PlayMode tests passed. Existing physics puzzles, Intro/menu, Settings, typography, contextual guidance and objective progression regressions passed. New tests cover incomplete-objective suppression, exact copy/fonts/no overflow, Settings rejection, one modal only, a second completion after Replay, initial state recovery, Continue fallback and three actual Unity render resolutions. These are automated PlayMode tests and rendered previews, not a manual native-window mouse walkthrough. Report: Verification/tutorial-completion-playmode.xml.
