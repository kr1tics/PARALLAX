# Project 2 Report

Read the [project 2
specification](https://github.com/feit-comp30019/project-2-specification) for
details on what needs to be covered here. You may modify this template as you
see fit, but please keep the same general structure and headings.

Remember that you should maintain the Game Design Document (GDD) in the
`README.md` file (as discussed in the specification). We've provided a
placeholder for it [here](README.md).

## Table of Contents

- [Evaluation Plan](#evaluation-plan)
- [Evaluation Report](#evaluation-report)
- [Shaders and Special Effects](#shaders-and-special-effects)
- [Summary of Contributions](#summary-of-contributions)
- [References and External Resources](#references-and-external-resources)

## Evaluation Plan

- Goals: Find out how easily players learn PARALLAX's mechanics. Players should be able to navigate the station, interact with objects, switch between past and future, and complete puzzles without excessive assistance. Testing will identify confusing controls or feedback and puzzles that are too difficult to follow.
  - Scope: Test the implemented Engineering Access level (`StartScene`): two cube-and-pressure-plate relays, time switching, gravity-controlled storage access and the exit. The GDD's planned power-budget, cooling and networking systems are outside this evaluation.
- Techniques and tasks:
  - Observational (Think-Aloud Playtesting): Five participants will play the Engineering Access level while explaining their thoughts and decisions. Observers will note where players become confused, record unsuccessful interactions, and track task times and assistance. This lets us see where and why players struggle.
  - Querying (Post-Play Questionnaire): Five different participants will attempt the same tasks without thinking aloud, then answer the questionnaire even if they do not finish. Their ratings and written answers will describe how the controls felt, whether puzzles and time switching made sense, how easily they found their way, and whether they enjoyed playing.

  Give both groups the task prompts. Keep the success checks on the evaluator's sheet so participants work out the solutions themselves.

  | Task prompt (in order) | Observable success check |
  |---|---|
  | Explore the arrival area, find the first relay and compare the two time periods. | Reach plate 01, switch between Past and Future, and identify the current period when asked once after switching. |
  | Restore the access relay and enter engineering. | Grab, move and release the cube onto plate 01 in the Past; return to the Future with gravity on and pass through the opened access door. |
  | Release the engineering cube and restore the engineering relay. | Turn station gravity off using the Past console, move the cube out of storage to plate 02, restore gravity and activate the relay in the Future. |
  | Find and reach the level exit. | Follow the available route and reach the exit in the Future after both relays are solved, triggering tutorial completion. |

- Participants: We will recruit ten people through classmates, friends and gaming communities, with five in each group and no overlap. Participants should have basic experience with first-person PC games and an interest in puzzle-solving or exploration, matching the GDD's target audience. Include casual and experienced players who are unfamiliar with PARALLAX's puzzles, and record their gaming experience. Participation will be voluntary, with no financial incentives.
- Procedure: Use the same WebGL build, computer, browser and default settings for all sessions. Log any settings adjusted for comfort. Verify the complete route and disable the developer completion shortcut before testing. Start each session by selecting Play from the Main Menu, with fresh puzzle state and the same intro and in-game guidance.
  - Allow approximately 30 minutes: 5 for consent, experience questions and controls; up to 20 for play; 5 for feedback. Read the same brief: “Restore access and engineering, then reach the exit. We are testing the game; you may stop at any time.” Supply the in-game controls reference without demonstrating solutions. Only the observational group receives the request to explain what they are trying and expecting, with neutral reminders to keep talking.
  - If a player asks for help or makes no progress for two minutes, say: “Check the current objective and time/gravity indicators.” If they are still stuck two minutes later, give a task-specific hint prepared during the pilot. After two further minutes, record the task as incomplete and demonstrate only what is needed to attempt the next task. Log when each hint was given and mark later attempts that followed a demonstration.
  - Stop when the player finishes, reaches the play limit or chooses to withdraw. For bugs or soft locks, note the action, time period and symptom; pause timing and try `R` or a fresh launch. Record recovery time and lost progress separately, and distinguish restarted attempts from first attempts. If recovery fails, end play and collect feedback on what the player experienced.
- Questionnaire: Use **1 = strongly disagree, 2 = disagree, 3 = neutral, 4 = agree, 5 = strongly agree**, plus **not experienced** where appropriate. Ask participants to rate:
  - “Movement and looking responded as I expected.”
  - “Grabbing, moving and releasing cubes was easy.”
  - “I could tell whether I was in the Past or Future.”
  - “I understood how moving a cube in the Past affected the Future.”
  - “The gravity and storage-door feedback helped me solve the engineering puzzle.”
  - “I could tell where to go next.”
  - “The puzzles were challenging but manageable.”
  - “I enjoyed playing this level.”
  - Open-ended: “Where did you get stuck, and what did you expect to happen?”; “What helped you understand time switching or gravity?”; “What one change would most improve the experience, and why?”
- Collection: Use a stopwatch and shared spreadsheet, with one row for each participant's attempt at a task. Record their ID, technique, build, gaming experience, start and end times, outcome (independent, assisted, incomplete or unreached), hints, resets and issue notes. Independent success means completing a task without facilitator hints or demonstrations; normal in-game guidance is allowed. Time each task from its prompt until success or stopping. Include player resets and hints, but exclude technical recovery and breaks.
  - During think-aloud sessions, count unsuccessful attempts to grab or use objects and note what happened and what the player said. Distinguish control problems from attempts to work out a puzzle. Store questionnaire answers by participant ID. With consent, we may record gameplay using OBS Studio; written notes are enough if a participant declines. Use anonymous IDs O1 to O5 and Q1 to Q5 in reports and restrict recordings to the team.
- Analysis: For each group of five, count independent and assisted completions separately and include tasks left incomplete or unreached. Report the median and range of successful task times, with assisted times and time spent on incomplete attempts reported separately. For each questionnaire item, report the median, the number choosing each rating and the total valid responses. Exclude “not experienced” answers.
  - Our initial targets are for four of five players in each group to complete each task independently, and for four of five questionnaire players to finish within the local milestone's ten-minute playtime goal. The first six rating items should have medians of at least 4. The 20-minute limit gives us time to observe difficulties. Think-aloud and silent-play times should not be compared directly.
  - Group comments under controls, navigation, time switching, puzzle clarity and visual feedback, and link them to tasks and ratings. Fix problems that block completion first, then recurring issues. Record how many players each issue affected and whether it blocked completion, required help or caused a minor difficulty. Use these findings to choose changes to mechanics, the interface and level design. Compare themes across the two groups while keeping their results separate. These small samples of available volunteers cannot represent all players.
- Timeline:
  - 12 to 14 October 2026: Prepare materials, run a teammate pilot (excluded from the ten participants), verify WebGL and recruit participants. Check these proposed dates against the course deadline before recruitment.
  - 15 to 18 October: Run five observational and five questionnaire sessions. Allow five hours plus setup and spare slots for rescheduling. If a blocking bug requires a build change, record the version and analyse affected attempts separately.
  - 19 to 20 October: Analyse results and decide which usability issues to address first.
  - 21 to 23 October: Make the highest-priority changes to gameplay, controls and puzzle clarity that fit the time available. Defer larger changes.
  - 24 to 25 October: Retest changed tasks with available returning participants and check that the changes have not broken other gameplay. Report retests separately from the original ten sessions, noting that returning players already know the puzzles. Identify any fixes that have not been retested with participants.
  - 26 October: Finalise evaluation findings and documentation ahead of submission.
- Responsibilities: All team members will help with recruitment, testing, data collection and analysis. By 14 October, decide who will handle scheduling and build preparation, who will manage materials and data, and who will write the report. Two pairs, each with a facilitator and recorder, will cover five sessions apiece (five person-hours per pair, plus preparation). Swap roles while using the same script, and check notes after each session. Assign fixes according to existing programming, level design and user interface responsibilities. Track tasks, progress and individual contributions in GitHub Issues.

## Evaluation Report

TODO - see specification for details

## Shaders and Special Effects

TODO - see specification for details

## Summary of Contributions

TODO - see specification for details

## References and External Resources

- **OpenAI Codex:** used to implement the prototype settings menu, integrate it with player input and camera preferences, implement the space backdrop and procedural Earth textures using built-in Unity materials, repair exterior station faces and collision, scope artificial gravity to station decks, repair walking/jumping and object interactions, fix scene teardown, add the settings controls reference, and limit menu scrolling. Automated regression tests were added for these gameplay and settings paths. The team specified the living-past / barren-future Earth appearance.
