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

- Goals: Evaluate how effectively players understand and interact with PARALLAX's core gameplay mechanics. Players should be able to navigate the station, interact with objects, switch between past and future, and complete puzzles without excessive assistance. Testing will identify issues with controls, puzzle difficulty, visual feedback and overall gameplay clarity.
- Techniques and tasks:
  - Observational – Think-Aloud Playtesting: Five participants will play the Engineering Access level while explaining their thoughts and decisions. They will be tasked with exploring the environment, interacting with objects, switching time periods and completing puzzles. Observers will record confusion, incorrect interactions, completion times and instances where assistance is needed. This technique helps identify usability problems as they occur.
  - Querying – Post-Play Questionnaire: Five different participants will complete the same level before answering a questionnaire. Questions will assess control responsiveness, puzzle difficulty, time-switching clarity, environmental navigation and overall enjoyment. Participants will provide ratings from 1–5 and written suggestions. This technique provides measurable feedback and allows players to explain their experiences.
- Participants: Ten participants will be recruited through classmates, friends and gaming communities, with five assigned to each technique. Participants should have basic experience with first-person PC games and an interest in puzzle-solving or exploration, matching the GDD's target audience. Both casual and experienced players will be included to provide varied perspectives. Participation will be voluntary, with no financial incentives.
- Collection: Quantitative data will include task completion times, success rates, incorrect interactions, puzzle resets and questionnaire ratings. Qualitative data will include player comments, observed difficulties and improvement suggestions. Gameplay may be recorded using OBS Studio with participant consent. Results will be organised in a shared spreadsheet, with participants identified anonymously.
- Analysis: Completion times, success rates and questionnaire ratings will be averaged to identify overall usability trends. Qualitative feedback will be grouped into categories such as controls, navigation, time switching, puzzle clarity and visual feedback. Recurring problems will be prioritised based on their frequency and impact on gameplay. Findings will guide improvements to the game's mechanics, interface and level design.
- Timeline:
  - 12–14 October 2026: Prepare testing materials, conduct a pilot test and recruit participants.
  - 15–18 October: Conduct five observational and five questionnaire-based sessions.
  - 19–20 October: Analyse results and identify priority usability issues.
  - 21–23 October: Implement improvements to gameplay, controls and puzzle clarity.
  - 24–25 October: Retest updated gameplay and assess improvements.
  - 26 October: Finalise evaluation findings and documentation ahead of submission.
- Responsibilities: All team members will contribute to recruitment, testing, data collection and analysis. Members will rotate between facilitating sessions, observing gameplay and recording feedback to distribute the workload fairly. Improvements will be assigned according to existing development responsibilities, including programming, level design and user interface development. GitHub Issues will be used to track tasks, progress and individual contributions.

## Evaluation Report

TODO - see specification for details

## Shaders and Special Effects

TODO - see specification for details

## Summary of Contributions

TODO - see specification for details

## References and External Resources

- **OpenAI Codex:** used to implement the prototype settings menu, integrate it with player input and camera preferences, implement the space backdrop and procedural Earth textures using built-in Unity materials, repair exterior station faces and collision, scope artificial gravity to station decks, repair walking/jumping and object interactions, fix scene teardown, add the settings controls reference, and limit menu scrolling. Automated regression tests were added for these gameplay and settings paths. The team specified the living-past / barren-future Earth appearance.
