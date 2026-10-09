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

Evaluate how effectively players understand and interact with PARALLAX's core gameplay mechanics. Players should be able to navigate the station, interact with objects, switch between past and future, and complete puzzles without excessive assistance.  

Testing will identify issues with controls, puzzle difficulty, visual feedback and overall gameplay clarity.

  
### Evaluation techniques

We will use two evaluation techniques to assess the usability and overall player experience of our game: **Think Aloud (observational)** and **Questionnaire (querying)**. Each technique will involve **_five_** different participants, with a total of _**ten**_ participants.  


**1. Observational Technique: Think Aloud (5 participants)**  

We will use the Think Aloud method to observe how players interact with the game and identify any difficulties they encounter. Participants will be asked to share and explain their thoughts and decisions while playing, without receiving instructions or assistance share their thoughts and explain their decisions while playing, without receiving immediate assistance.

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
  
This method will allow us to compare feedback across participants and identify areas for improvement, particularly regarding usability, difficulty and player engagement.    


**3. Evaluation Tasks**. 

  Give both groups the task prompts. Keep the success checks on the evaluator's sheet so participants work out the solutions themselves.

  | Task prompt (in order) | Observable success check |
  |---|---|
  | **Task 1:** Explore the arrival area, find the first relay and compare the two time periods. | Reach plate 01, switch between Past and Future, and identify the current period when asked once after switching. |
  | **Task 2:** Restore the access relay and enter engineering. | Grab, move and release the cube onto plate 01 in the Past; return to the Future with gravity on and pass through the opened access door. |
  | **Task 3:** Release the engineering cube and restore the engineering relay. | Turn station gravity off using the Past console, move the cube out of storage to plate 02, restore gravity and activate the relay in the Future. |
  | **Task 4:** Find and reach the level exit. | Follow the available route and reach the exit in the Future after both relays are solved, triggering tutorial completion. |

These tasks will help us evaluate whether players can understand and apply the game's core mechanics to complete the tutorial.

**4. Testing Procedure** 

All participants will use the same WebGL build, computer, browser and default settings. Any adjustments made for accessibility or comfort will be recorded.  

Before testing, we will verify that the complete level is playable and disable developer shortcuts. Each participant will begin from the Main Menu with a fresh puzzle state.  

Each session will last approximately 30 minutes:  

- **5 minutes:** Consent, gaming experience questions and control information.
- **Up to 20 minutes:** Gameplay and task completion.
- **5 minutes:** Questionnaire or post-test feedback.

Participants will receive the same initial instructions:   
    Restore access and engineering, then reach the exit. We are testing the game; you may stop at any time.

The in-game controls reference will be available, but solutions will not be demonstrated.  

Only participants in the Think Aloud group will be asked to explain their thoughts and expectations while playing.  

**Assistance Rules**  

To ensure consistency across sessions:  

    1. If a participant requests help or makes no progress for two minutes, the evaluator will remind them to check the current objective and time/gravity indicators.
    2. If they remain stuck for another two minutes, a task-specific hint will be provided.
    3. After another two minutes without progress, the task will be recorded as incomplete. The evaluator may then demonstrate the minimum necessary steps to continue.
   
All hints and assistance will be recorded.  

If a bug or soft lock occurs, the evaluator will record the issue, pause the timer and attempt recovery through a reset or restart. Recovery time and lost progress will be recorded separately.  

**5.Questionnaire Design** 

Participants in the querying group will complete a short questionnaire after gameplay.  

Ratings will use a five-point Likert scale:
    **1 = Strongly Disagree | 2 = Disagree | 3 = Neutral | 4 = Agree | 5 = Strongly Agree**
  
A "Not Experienced" option will be available where appropriate.
  
**Rating Questions** 

    1. Movement and looking responded as I expected.
    2. Grabbing, moving and releasing cubes was easy.
    3. I could tell whether I was in the Past or Future.
    4. I understood how moving a cube in the Past affected the Future.
    5. The gravity and storage-door feedback helped me solve the engineering puzzle.
    6. I could tell where to go next.
    7. The puzzles were challenging but manageable.
    8. I enjoyed playing this level.

**Open-ended Questions** 

    1. Which part of the game did you find most interesting?
    2. Where did you get stuck, and what did you expect to happen?
    3. What helped you understand time switching or gravity?
    4. What one change would most improve the experience, and why?

  
### Participants
Ten participants will be recruited through classmates, friends and gaming communities, with five assigned to each evaluation technique.  

Participants should have basic experience with first-person PC games and an interest in puzzle-solving or exploration, matching the GDD's target audience.  

Both casual and experienced players will be included to provide varied perspectives.  

Participation will be voluntary, with no financial incentives.

### Data Collection
We will collect both quantitative and qualitative data during the evaluation sessions.  

**1. Quantitative Data** 

We will record:  

    - Task completion: Whether participants complete each task independently, with assistance or not at all.
    - Completion time: Time taken to complete each task.
    - Hints required: Number of hints provided during gameplay.
    - Failed interactions: Unsuccessful attempts to interact with objects or use mechanics.
    - Resets: Number of puzzle resets or restarts.
    - Questionnaire ratings: Numerical responses to the post-game questionnaire.
  
  Task outcomes will be classified as:  
  
    - Independent: Completed without facilitator hints or demonstrations.
    - Assisted: Completed with facilitator assistance.
    - Incomplete: Attempted but not completed.
    - Unreached: Participant did not reach the task.
    Normal in-game guidance will not count as facilitator assistance.

**2. Qualitative Data**   

We will collect:  

    - Player comments made during Think Aloud sessions.
    - Observations of confusion, hesitation or unexpected behaviour.
    - Difficulties understanding controls, instructions or puzzle mechanics.
    - Open-ended questionnaire responses and improvement suggestions.
Observations will distinguish between difficulties with controls and difficulties understanding the puzzle solutions.  

**3. Collection Tools and Methods** 

A stopwatch and shared spreadsheet will be used to record task performance.  

Each task attempt will have a separate row containing the participant ID, evaluation technique, build version, gaming experience, start and end times, task outcome, hints, resets and observations.  

Task timing will begin when the prompt is given and end when the task is completed or stopped. Time spent on hints and player-initiated resets will be included, while technical recovery time and breaks will be excluded.  

With participant consent, gameplay may also be recorded using OBS Studio to support observation and review. Written notes will be sufficient if participants decline recording.  

Questionnaire responses will be collected through Google Forms and organised in the shared spreadsheet.  

Participants will be identified using anonymous IDs:
    - O1–O5: Think Aloud group.
    - Q1–Q5: Questionnaire group.

Recordings and collected data will only be accessible to team members.


#### Data Analysis
  
  We will analyse quantitative and qualitative results separately to identify usability problems and prioritise improvements.  
  
**1. Quantitative Analysis**. 

For each group of five participants, we will calculate:  

    - Number of independent and assisted task completions.
    - Number of incomplete or unreached tasks.
    - Median and range of successful task completion times.
    - Number of hints, unsuccessful interactions and resets.
    - Median questionnaire ratings and response distributions.
  
  Assisted completion times and incomplete attempts will be reported separately.
  
  "Not Experienced" questionnaire responses will be excluded from rating calculations.
  
  Think Aloud and questionnaire-group completion times will not be compared directly, as verbalising thoughts may influence gameplay speed.
  
  Initial Evaluation Targets. 
  
    - At least 4 out of 5 participants in each group should complete each task independently.
    - At least 4 out of 5 questionnaire participants should complete the level within the ten-minute target.
    - The first six questionnaire items should have median ratings of at least 4.
  
  The 20-minute testing limit will allow additional time to observe difficulties beyond the intended gameplay duration.
  
**2.Qualitative Analysis** 

Player comments and observations will be grouped into the following categories:  

    - Controls and interactions.
    - Navigation and objectives.
    - Time switching.
    - Puzzle clarity.
    - Visual feedback.
  
  Issues will be prioritised based on their frequency and impact on gameplay.
   
  Problems preventing task completion will receive the highest priority, followed by recurring difficulties requiring assistance and minor usability issues.  
  
  The findings will guide improvements to gameplay mechanics, interface design and level structure.  

  Results from both groups will be considered together when identifying common issues, while recognising that the small sample size cannot represent all potential players.


#### Timeline

| Date | Activity |
  |---|---|
  | **12–14 October 2026** | Prepare testing materials, conduct a pilot test and recruit participants. |
  | **15–18 October** | Conduct five observational and five questionnaire-based sessions. |
  | **19–20 October** | Analyse results and identify priority usability issues. |
  | **21–23 October** | Implement improvements to gameplay, controls and puzzle clarity. |
  | **24–25 October** | Retest updated gameplay and assess improvements. |
  | **26 October** | Finalise evaluation findings and documentation ahead of submission. |
 
#### Responsibilities
All team members will contribute to participant recruitment, testing, data collection and analysis.  

Members will rotate between facilitating sessions, observing gameplay and recording feedback to distribute the workload fairly.  

Improvements will be assigned according to existing development responsibilities, including programming, level design and user interface development.
GitHub Issues will be used to track tasks, progress and individual contributions.

## Evaluation Report

TODO - see specification for details

## Shaders and Special Effects

TODO - see specification for details

## Summary of Contributions

TODO - see specification for details

## References and External Resources

- **OpenAI Codex:** Used as a development assistant to support the implementation, debugging and refinement of various game features, including the settings menu, player controls, gravity mechanics, object interactions and environmental visuals. It also assisted with fixing gameplay issues and improving the overall stability of the prototype. The team guided the development process, reviewed the implementations and tested the resulting features.
