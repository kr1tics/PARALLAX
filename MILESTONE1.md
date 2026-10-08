# Milestone 1: User Evaluation Launch

## Goal
> Getting a playable build in front of real users

## TODO
### Demo
- [ ] Implement the core gameplay described within the GDD.
- [ ] Ensure the game is playable in a web browser as a WebGL build.

(The game does not need to be polished at this stage, 
    but it should be playable from start to finish.) 
#### Game Environment
- [ ] story lore dump at start (can be a wall of text at the start for now)
- [ ] General like puzzle element design
- [ ] Placement for base puzzles at least
- [ ] Camera and Player feel good
- [ ] General Room Layout even if not all rooms have puzzles yet
#### Gameplay Mechanics
- [x] Player Movement
- [ ] Pick up / Move / Interact with objects
- [x] Physics (incl. Gravity)
- [x] Time Travel (Done but needs fixing)
- [ ] Buttons to open/close doors
- [ ] Power Switching (Maybe de-scope?)
- [ ] Some Puzzle Design (see below)

#### UI and Sound
- [x] Main Menu (Play, shared settings/controls, platform-aware Quit)
- [x] Pause Menu/Control UI
- [x] Settings (volume, graphics quality, fullscreen, mouse sensitivity, inversion)
- [ ] Interaction prompt
- [ ] Feedback for Puzzle completion/progress

#### Puzzle Design
One idea of a intro puzzle would be making the player travel back in time, 
maybe stairs or a ladder that are broken in the future?
do something that turns gravity back on, and then put a cube on a button.

Then a harder puzzle that explores how they can play into eachother, might need brainstorming

We want to keep it to a short number of puzzles, so our data is more meaningful in the sense that people will at least get to each puzzle
We also want to be aware of the 10 minute playtime goal.

We also should make sure that with low-grav we cant like cheese a puzzle

### Evaluation Plan
Goes in REPORT.md
Meet to Discuss...
- [ ] AT LEAST One Querying and One Observational Evaluation Technique
(Basically, Querying techniques are asking the user after play, 
    and Observational Techniques are things specifically to watch for while they're playing)
- [ ] 5+ Participants per technique (Ask Family/Friends if Possible?)

Other things we want to condider:
- What are we evaluating (Intuitiveness, Puzzle Difficulty, etc)
- How should we get participants
- What data do we want/what do we do with it
- What does the timeline for this look like
- Responsibilities, need to discuss.

Once we do this milestone, changes should mostly be on feedback.

### Submission
- [ ] Update `metadata.json`
- [ ] Test on Gradescope at least more than Project 1
- [ ] Double-check the REPORT.md is filled out enough.

## Task Allocation
Plan needs to be done in more detail once we're closer to done game wise.
General idea is observarion technique would be watching the tutorial with no advice and seeing how they go -- idea is to see how intuitive the mechanics are
then before next puzzle give user a brief on anything that confused them and like ley them know that the next puzzle they can actually ask questions
the questionairre can more relate to the second puzzle, where we can talk more and intervene if really stuck

Everyone should think about shader and how that fits into gameplay (okay if it cant, but ideally it does)
### Amy
- Object Interaction (Buttons, picking up items, etc)
- Movement feel?
- Layout tutorial

### Will
- Settings menu?

### Sabrina
- textures & sound for uzzle elements
- context lore dump

### Cache
- assets and room layout
- pause menu & control list
- Main menu
