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

### Goals

Evaluate how effectively players understand and interact with PARALLAX's core gameplay mechanics. Players should be able to navigate the station, interact with objects, switch between past and future, and complete puzzles without excessive assistance. Testing will identify issues with controls, puzzle difficulty, visual feedback and overall gameplay clarity.

  
### Evaluation techniques

We will use two evaluation techniques to assess the usability and overall player experience of our game: **Think Aloud (observational)** and **Questionnaire (querying)**. Each technique will involve **_five_** different participants, with a total of _**ten**_ participants.  


**1. Observational Technique: Think Aloud (5 participants)**  

We will use the Think Aloud method to observe how players interact with the game and identify any difficulties they encounter. Participants will be asked to share and explain their thoughts and decisions while playing, without receiving instructions or assistance unless necessary.  

Participants will complete the following tasks:  

    1. Understand the objective: Watch the introduction and identify the theme/concept of the game and also player's goal.
    2. Basic navigation: Explore the environment using the movement and camera controls.
    3. Gravity switching: Use gravity switching to navigate the environment and overcome an obstacle.
    4. Time switching: Switch between time periods and observe how the environment changes.
    5. Puzzle completion: Apply the game mechanics to complete the available puzzles.

We will observe whether players understand the instructions, use the mechanics correctly, and know how to progress through the game.  

This method will help us identify usability issues that may not be obvious to the developers, particularly confusing controls, unclear objectives and puzzle-solving difficulties.  


**2. Querying Technique: Questionnaire (5 participants)**  

We will use a post-game questionnaire to collect feedback on the overall gameplay experience. Five different participants will play the same demo before completing a short questionnaire.  

The questionnaire will include 1–5 rating-scale questions covering:  

    - Clarity of the game objectives and instructions.
    - Ease of using movement and switching mechanics.
    - Difficulty of the puzzle.
    - Enjoyment of the gameplay.
    - Overall satisfaction with the experience.
  
  We will also include open-ended questions:  
  
    1. Which part of the game did you find most confusing?
    2. Which part of the game did you find most interesting?
    3. What would you improve about the game?
  
This method will allow us to compare feedback across participants and identify areas for improvement, particularly regarding usability, difficulty and player engagement.    


**Evaluation Tasks**
  Give both groups the task prompts. Keep the success checks on the evaluator's sheet so participants work out the solutions themselves.

  | Task prompt (in order) | Observable success check |
  |---|---|
  | Explore the arrival area, find the first relay and compare the two time periods. | Reach plate 01, switch between Past and Future, and identify the current period when asked once after switching. |
  | Restore the access relay and enter engineering. | Grab, move and release the cube onto plate 01 in the Past; return to the Future with gravity on and pass through the opened access door. |
  | Release the engineering cube and restore the engineering relay. | Turn station gravity off using the Past console, move the cube out of storage to plate 02, restore gravity and activate the relay in the Future. |
  | Find and reach the level exit. | Follow the available route and reach the exit in the Future after both relays are solved, triggering tutorial completion. |

#### Participants
Ten participants will be recruited through classmates, friends and gaming communities, with five assigned to each technique. Participants should have basic experience with first-person PC games and an interest in puzzle-solving or exploration, matching the GDD's target audience. Both casual and experienced players will be included to provide varied perspectives. Participation will be voluntary, with no financial incentives.

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

#### Data Collection
Use a stopwatch and shared spreadsheet, with one row for each participant's attempt at a task. Record their ID, technique, build, gaming experience, start and end times, outcome (independent, assisted, incomplete or unreached), hints, resets and issue notes. Independent success means completing a task without facilitator hints or demonstrations; normal in-game guidance is allowed. Time each task from its prompt until success or stopping. Include player resets and hints, but exclude technical recovery and breaks.
  - During think-aloud sessions, count unsuccessful attempts to grab or use objects and note what happened and what the player said. Distinguish control problems from attempts to work out a puzzle. Store questionnaire answers by participant ID. With consent, we may record gameplay using OBS Studio; written notes are enough if a participant declines. Use anonymous IDs O1 to O5 and Q1 to Q5 in reports and restrict recordings to the team.
- Analysis: For each group of five, count independent and assisted completions separately and include tasks left incomplete or unreached. Report the median and range of successful task times, with assisted times and time spent on incomplete attempts reported separately. For each questionnaire item, report the median, the number choosing each rating and the total valid responses. Exclude “not experienced” answers.
  - Our initial targets are for four of five players in each group to complete each task independently, and for four of five questionnaire players to finish within the local milestone's ten-minute playtime goal. The first six rating items should have medians of at least 4. The 20-minute limit gives us time to observe difficulties. Think-aloud and silent-play times should not be compared directly.
  - Group comments under controls, navigation, time switching, puzzle clarity and visual feedback, and link them to tasks and ratings. Fix problems that block completion first, then recurring issues. Record how many players each issue affected and whether it blocked completion, required help or caused a minor difficulty. Use these findings to choose changes to mechanics, the interface and level design. Compare themes across the two groups while keeping their results separate. These small samples of available volunteers cannot represent all players.
  - 
#### Participants
Ten participants will be recruited through classmates, friends and gaming communities, with five assigned to each technique. Participants should have basic experience with first-person PC games and an interest in puzzle-solving or exploration, matching the GDD's target audience. Both casual and experienced players will be included to provide varied perspectives. Participation will be voluntary, with no financial incentives.


#### Data Collection
Quantitative data will include task completion times, success rates, incorrect interactions, puzzle resets and questionnaire ratings. Qualitative data will include player comments, observed difficulties and improvement suggestions. Gameplay may be recorded using OBS Studio with participant consent. Results will be organised in a shared spreadsheet, with participants identified anonymously.


#### Data Analysis
Completion times, success rates and questionnaire ratings will be averaged to identify overall usability trends. Qualitative feedback will be grouped into categories such as controls, navigation, time switching, puzzle clarity and visual feedback. Recurring problems will be prioritised based on their frequency and impact on gameplay. Findings will guide improvements to the game's mechanics, interface and level design.


#### Timeline
  - 12–14 October 2026: Prepare testing materials, conduct a pilot test and recruit participants.
  - 15–18 October: Conduct five observational and five questionnaire-based sessions.
  - 19–20 October: Analyse results and identify priority usability issues.
  - 21–23 October: Implement improvements to gameplay, controls and puzzle clarity.
  - 24–25 October: Retest updated gameplay and assess improvements.
  - 26 October: Finalise evaluation findings and documentation ahead of submission.
 

#### Responsibilities
All team members will contribute to recruitment, testing, data collection and analysis. Members will rotate between facilitating sessions, observing gameplay and recording feedback to distribute the workload fairly. Improvements will be assigned according to existing development responsibilities, including programming, level design and user interface development. GitHub Issues will be used to track tasks, progress and individual contributions.

## Evaluation Report

TODO - see specification for details

## Shaders and Special Effects

TODO - see specification for details

## Summary of Contributions

TODO - see specification for details

## References and External Resources

- **OpenAI Codex:** used to implement the prototype settings menu, integrate it with player input and camera preferences, implement the space backdrop and procedural Earth textures using built-in Unity materials, repair exterior station faces and collision, scope artificial gravity to station decks, repair walking/jumping and object interactions, fix scene teardown, add the settings controls reference, and limit menu scrolling. Automated regression tests were added for these gameplay and settings paths. The team specified the living-past / barren-future Earth appearance.
