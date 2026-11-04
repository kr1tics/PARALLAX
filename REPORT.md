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


**3. Evaluation Tasks** 

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

**5. Questionnaire Design** 

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


### Data Analysis
  
  We will analyse quantitative and qualitative results separately to identify usability problems and prioritise improvements.  
  
**1. Quantitative Analysis** 

For each group of five participants, we will analyse the following metrics: 

    - Task completion rates: How many participants complete each task independently or with assistance.
    - Completion times: How long participants take to complete each task.
    - Hints and failed attempts: How often participants need assistance or struggle with controls and interactions.
    - Questionnaire ratings: How participants rate the clarity of instructions, ease of controls, puzzle difficulty and overall enjoyment.

  We will summarise task completion results and calculate the median completion times and questionnaire ratings to identify common patterns.

  We will pay particular attention to tasks with low completion rates, longer completion times or frequent requests for assistance, as these may indicate usability issues.
  
**2. Qualitative Analysis** 

Player comments and observations will be grouped into the following categories:  

    - Controls and interactions.
    - Navigation and objectives.
    - Time switching.
    - Puzzle clarity.
    - Visual feedback.
  
  We will identify recurring problems and compare feedback from both evaluation groups.  
  
Issues will be prioritised based on how frequently they occur and how significantly they affect gameplay. Problems that prevent players from progressing will be addressed first, followed by difficulties that cause confusion or reduce enjoyment.  

The findings will guide improvements to game mechanics, user interface and level design, with a focus on improving the overall player experience.


### Timeline

| Date | Activity |
  |---|---|
  | **12–14 October 2026** | Prepare testing materials, conduct a pilot test and recruit participants. |
  | **15–18 October** | Conduct five observational and five questionnaire-based sessions. |
  | **19–20 October** | Analyse results and identify priority usability issues. |
  | **21–23 October** | Implement improvements to gameplay, controls and puzzle clarity. |
  | **24–25 October** | Retest updated gameplay and assess improvements. |
  | **26 October** | Finalise evaluation findings and documentation ahead of submission. |
 
### Responsibilities
All team members will contribute to participant recruitment, testing, data collection and analysis.  

Members will rotate between facilitating sessions, observing gameplay and recording feedback to distribute the workload fairly.  

Improvements will be assigned according to existing development responsibilities, including programming, level design and user interface development.
GitHub Issues will be used to track tasks, progress and individual contributions.

## Evaluation Report

*Note: The Evaluation Report was originally written using OneNote to allow for live collaboration and easier editing.*
TODO: Transfer final Evaluation Report Ver.

## Shaders and Special Effects

### Shader Report

This document is partially copied from Project 1, in order to save time and resources.

#### Table of Contents

- [Time Transition Shader](#time-transition-shader)
- [Simple Hologram Shader](#simple-hologram-shader)
- [Future Decay Shader](#future-decay-shader)
- [Object Distortion Shader](#object-distortion-shader)

#### Time Transition Shader

_Written by Amy Mauseth [1605871]_

**Code can be found at 
[Assets/Shaders/TimeTravelFx.shader](https://github.com/feit-comp30019/2026s2-project-1-four-sure/blob/main/Assets/Shaders/TimeTravelFx.shader). 
The effect can be triggered by switching time with `T` while in-game.**

##### Behaviour
This shader renders a "blinking" effect to a texture overlaid as UI on the camera.
It uses Unity's provided `_Time` variable and the cosine function 
to create an effect that actually sort of reminds me of a transition from Looney Tunes or something.
It also has calculations to find the actual pixel being evaluated by the fragment shader 
(which was both harder than expected, and ended up being only minimally useful).


##### Use During Gameplay
This shader is used to create the "screen pinch" effect when transitioning between the past and future.
It is important, as it hides any teleporting objects and other quirks of how time travel is implemented.

##### Parameters

The shader exposes 5 parameters to Unity. Two of them (`_MainTex` and `_Color`) are simply just getting things from the 
material definition, and the other three are custom:
- `_Intensity` is unused but was intended to dictate the velocity of the star pattern I didn't end up implementing.
- `_Duration` is the amount of time the effect should take. It should be set in [Assets/Scripts/GlobalState.cs](https://github.com/feit-comp30019/2026s2-project-2-four-sure/blob/main/Assets/Scripts/GlobalState.cs), rather than on the shader directly.
- `_StartTime` is used to effectively 'trigger' the effect. It is set at runtime by [Assets/Scripts/GlobalState.cs](https://github.com/feit-comp30019/2026s2-project-2-four-sure/blob/main/Assets/Scripts/GlobalState.cs), and custom values will be overwritten.

This shader requires an accompanying script to function.
The script is triggered (as described above) by setting `_StartTime` to the current in-game time (`Time.timeSinceLevelLoad`).
In addition, a material can be applied to the shader in order to texture it, which could be used to further enhance the effect, however, there were no textures that i found that would fit the setting.

##### Screenshots / GIFs
TODO add a git of the shader working.

##### External Resources:
- Template & Reference: https://github.com/feit-comp30019/workshop-6/blob/main/Assets/MultiPassShader.shader
- Reference: https://github.com/feit-comp30019/workshop-5
- Reference: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Material.SetFloat.html
- Reference: https://docs.unity3d.com/6000.0/Documentation/Manual/SL-PropertiesInPrograms.html
- Reference: https://docs.unity3d.com/2020.3/Documentation/Manual/SL-UnityShaderVariables.html
- Troubleshooting: https://discussions.unity.com/t/is-_time-y-equal-to-time-time-on-the-cpu-or-are-they-not-in-sync/757063/6
- Reference: https://learn.microsoft.com/en-us/windows/win32/direct3dhlsl/dx-graphics-hlsl-abs
- Reference: https://learn.microsoft.com/en-us/windows/win32/direct3dhlsl/dx-graphics-hlsl-per-component-math
- Reference: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Material-color.html
- Reference: https://www.ronja-tutorials.com/post/039-screenspace-texture/#screenspace-coordinates-in-unlit-shaders
- Referece: https://docs.unity3d.com/2019.3/Documentation/Manual/SL-SubShaderTags.html
- Troubleshooting: https://discussions.unity.com/t/alpha-value-in-fragment-shader/604737/2
- Reference: https://discussions.unity.com/t/unityobjecttoclippos/625278/2
- Reference: https://docs.unity3d.com/2023.2/Documentation/Manual/UICanvas.html

#### Simple Hologram Shader

_Written by Cache Luk [1625615]_ 

**Shader:** `Unlit/SimpleHologram`, implemented in `Assets/Shaders/SimpleHologram.shader`.
**Code can be found at [Assets/Shaders/SimpleHologram.shader](https://github.com/feit-comp30019/2026s2-project-1-four-sure/blob/main/Assets/Shaders/SimpleHologram.shader).**

##### Behaviour

This shader creates a simple animated holographic surface using cyan colour, horizontal scanlines, periodic brightness flicker and transparency. I chose the effect for PARALLAX because the player is a maintenance worker exploring an abandoned space station. A small projected maintenance panel suits the science-fiction setting and suggests imperfect electronic equipment. The current panel is decorative: it does not display live power readings or change the time-travel mechanics.


The shader uses one vertex program and one fragment program in a single ShaderLab pass. The vertex program uses `UnityObjectToClipPos` to transform the vertices into clip space so they can be rendered on screen. It also passes the UV coordinates to the fragment shader. I did not change the geometry of the object in the vertex shader.

In the fragment shader, I created the scanline effect using the vertical UV coordinate (`uv.y`) together with a sine wave. Since only the Y coordinate is used, the brightness changes vertically across the surface and creates horizontal lines. `_Time.y` is included in the calculation so the lines move over time. `_ScanDensity` controls how many scanlines appear, while `_ScanSpeed` controls how quickly they move.

The sine wave normally produces values between -1 and 1, so I remapped it to a range between 0 and 1. I then adjusted this so the scanlines have a brightness between 0.25 and 1. A second sine wave uses only time to create the flickering effect across the whole hologram. `_FlickerSpeed` controls how quickly this happens and `_FlickerStrength` controls how noticeable it is.

Finally, the selected hologram colour is multiplied by the scanline and flicker brightness. `_Alpha` controls the transparency of the hologram. I used alpha blending so the scene behind the hologram can still be seen, and disabled depth writing because the hologram is transparent. The shader is unlit, so its appearance does not depend on the scene lighting.

##### Use During Gameplay

This shader is used in several places and for various purposes. 
As a "Hologram", it is used during the tutorial to explain where to place certain items and how to use them, 
as well as in the main game as a futuristic alternative to a status light.


##### Parameters and integration

`_Color` sets RGB colour; `_ScanDensity` sets line count; `_ScanSpeed` sets movement speed and direction; `_FlickerSpeed` sets pulse frequency; `_FlickerStrength` sets dimming depth; `_Alpha` sets opacity. `MaintenanceHologram.mat` is assigned to the built-in quad named **Maintenance Hologram**, under **Player / CameraPivot / Main Camera** in `StartScene`. It follows the camera in the lower-left view and has no collider or script.

##### Screenshots / GIFs

**Default parameter configuration**

![Default hologram parameter configuration](shader-2-default.png)

*Default hologram configuration with 12 scanlines, 0.5 scan speed, 2 flicker speed, 0.15 flicker strength and 0.65 opacity.*

**Stronger parameter configuration**

![Stronger hologram parameter configuration](shader-2-strong.png)

*Stronger configuration with 20 scanlines, 1.0 scan speed, 4 flicker speed, 0.45 flicker strength and 0.85 opacity. The increased scanline density is clearly visible, while the Inspector shows the increased animation and flicker parameters.*

TODO add a GIF of this shader working


#### Future Decay Shader

_Written by Sabrina Sun [1165949]_

##### Shader Source

**Code can be found at 
[Assets/Shaders/FutureDecay.shader](FutureDecay.shader) The effect can be triggered by switching time with `T` while in-game.**

##### Behaviour

Future Decay represents the space station after 150 years of aging and neglect. It combines faded paint, procedural grime, irregular corrosion, coating loss and small surface pits, preserving the environment’s panel details.

The shader is written using Cg/HLSL vertex and fragment shaders. The vertex shader prepares information such as the object’s position, texture coordinates and surface direction, then passes it to the fragment shader.

The fragment shader creates the damaged and aged appearance of the future environment. It uses procedural noise at different sizes to generate uneven dirt, corrosion, worn areas and small surface damage. This makes the effect look more natural instead of using a repeated pattern.

Different noise values are used to control where stronger corrosion and lighter surface wear appear. A random seed also gives repeated room objects slightly different patterns, so they do not all look exactly the same. The shader can also apply different effects depending on whether a surface is a wall or a floor.

Small changes to the surface normal create the appearance of shallow bumps, chipped paint and surface damage without actually changing the shape of the object or its collision. Basic lighting is kept so that objects are still easy to recognise, while a subtle flickering glow helps create the feeling of old and deteriorating technology.

##### Use during gameplay
This shader is used constantly throughout the game, as it is the primary way in which the user is fed back to about the current time period they are in.
This is key because the main premise of the game is travelling between the clean past 150 year ago and the decaying future.
The effect can be toggled using **T** to travel through time.

##### Exposed Parameters

- **`_DecayAmount`** controls overall aging, from clean at **0** to fully aged at **1**.
- **`_DecayColor`** controls the grime tint; corrosion uses a separate internal brown palette.
- **`_GrimeStrength`** controls the intensity of dirt and weathering stains.
- **`_DamageStrength`** controls corrosion, coating loss and surface relief.
- **`_FlickerStrength`** controls subtle emission on damaged coating edges.

##### Screenshots

###### Past / Clean

<img width="1512" height="931" alt="Screenshot 2026-09-13 at 3 26 56 pm" src="https://github.com/user-attachments/assets/2022738f-835f-4772-bea3-aa635a8556b0" />

*The Past environment provides the clean gameplay comparison.*

###### Low Decay

<img width="1512" height="876" alt="Screenshot 2026-09-13 at 3 29 59 pm" src="https://github.com/user-attachments/assets/dbe8fc52-3910-42c2-a24d-ce51feb48895" />


*`_DecayAmount = 0.25`: lighter aging with more of the original appearance retained.*

###### High Decay

<img width="1463" height="917" alt="Screenshot 2026-09-13 at 9 06 56 pm" src="https://github.com/user-attachments/assets/d9f8fcad-e224-4edf-b396-54a804872237" />

*`_DecayAmount = 1`: stronger fading, grime and corrosion. Both parameter comparisons use the same viewpoint, with `_GrimeStrength = 0.8`, `_DamageStrength = 0.7` and `_FlickerStrength = 0.1`.*

##### External Resources

- [Unity Built-in diffuse-lighting example](https://docs.unity3d.com/6000.0/Documentation/Manual/built-in-shader-examples-simple-diffuse-lighting.html): reference for the lighting pass and built-in lighting variables.
- The project’s existing **3D Free Modular Kit** supplies the environment meshes and base texture. Corrosion masks are generated procedurally.
- **AI assistance:** ChatGPT assisted with shader refinement, integration and automated checks.

#### Object Distortion Shader

_Written by William Ames [1472360]_

**Shader Source:** [DistortableObject.shader](Assets/Shaders/DistortableObject.shader)

##### Behaviour

This shader creates a temporary spatial distortion effect on a target object.
The shader gradually activates, causing the object's geometry to deform while its surface texture develops animated chromatic aberration. 
After remaining active briefly, the effect fades back to the object's normal appearance.

The main effect is implemented in the **vertex shader**. 
Each vertex is displaced away from the centre of the object using its position and a combination of animated sine waves. 
The displacement varies across the mesh using `_Frequency` and changes over time using Unity's `_Time` value and `_Speed`. 
`_Amplitude` controls the maximum displacement distance.  
The distortion is multiplied by `_EffectAmount`, allowing the effect to smoothly transition between its normal and distorted states. 
A subdivided ProBuilder mesh is used so that there are enough vertices for the deformation to appear smooth rather than only moving the corners of the object.

The **fragment shader** adds chromatic aberration while preserving the object's original texture. 
The red, green and blue channels are sampled from slightly different UV positions before being recombined. 
Rather than using a constant horizontal RGB separation, additional animated sine and cosine functions alter the direction and magnitude of the offsets across the surface. 
This produces a less predictable, unstable appearance that visually supports the idea that the object is being affected by a spatial or gravitational anomaly.

The distortion can operate either as a temporary effect that automatically fades out or as a persistent toggle. 
In persistent mode, player interaction switches `_EffectAmount` between 0 and 1, allowing objects to remain visually distorted until interacted with again.

##### Use During Gameplay
This effect is used when the player picks up an object. 
The idea is for the effect to feel like Zero-Point-Energy or similar, 
as well as providing feedback to the player that they are still holding the object.

This is enabled when the player looks at a compatible object and presses the interact button (E or Left Mouse)
The shader also becomes more intense over time, as if the longer the object is held, the more the physics-defying energy is destabilising it.

In the final game, this effect is toned down and barely noticable.

##### Parameters

* `_EffectAmount` — controls the overall activation of the effect from 0 to 1.
* `_Amplitude` — controls how far the object's vertices are displaced.
* `_Frequency` — controls the spatial frequency of the deformation.
* `_Speed` — controls how quickly the deformation animates.
* `_AberrationStrength` — controls the base amount of RGB channel separation.
* `_ChaosStrength` — controls the irregularity of the chromatic aberration.
* `_ChaosSpeed` — controls how quickly the chromatic distortion changes.
* `_MainTex` and `_Color` — preserve and tint the object's original surface appearance.
* The interaction component also exposes options for persistent toggle behaviour and whether an object begins in the distorted state.

##### Screenshots / GIFs

**Normal state (`_EffectAmount = 0`):**

![alt text](image.png)

**Hologram Shader**


**Activated distortion (`_EffectAmount = 1`):**

![alt text](image-1.png)

TODO get a gif of the thing in use

##### External Resources

- Unity documentation was consulted for ShaderLab properties, vertex/fragment shader structure, built-in shader variables such as `_Time`, and per-renderer material property control.
- **AI assistance**: ChatGPT was used to assist with understanding Unity/HLSL concepts, debugging the shader, and refining the report wording.

### Particle Effect Report

There is a Particle System implemented in Level 1, among other places later into the game.
The object can be found in the editor as "Route 1 / upper connection sparks"
or in the scene `https://github.com/feit-comp30019/2026s2-project-2-four-sure/blob/200fdc0dbf62afb30d135c5dacce064174eb0d75/Assets/Scenes/Level1_PowerRestoration.unity#L16300`

It models Electrical Damage, in the form of sparks, coming from a damaged wire that the player must bypass.
It is based on the Legacy Particles/Alpha-Blended-Premultiply Shader and has had duration, shape, color and gravity modified to feel more like a spark.
<img width="1138" height="632" alt="saprks" src="https://github.com/user-attachments/assets/7d7cf96c-f39d-4610-8929-7e3112821909" />

It also uses [RouteOneSparks.cs](https://github.com/feit-comp30019/2026s2-project-2-four-sure/blob/main/Assets/Scripts/Level1/RouteOneSparks.cs) to control its behaviour, including how it is affected by the global gravity, and if it should be enabled.
Finally, It uses a Random Number Generator to influence its timing and make it look more natural than it being on fixed intervals.

## Summary of Contributions

**Amy**: Core Mechanics and Technology (eg. Player Controller, Interaction System, Custom Physics, Time Travel Causality), Time Travel "Screen Pinch" Effect
**Cache**: Project Management, Puzzle Design, Hologram Shader, HUD, 3D Visual Design, Several Fixes
**Sabrina**: Tutorial, Visual and Sound Design, Story, Puzzle Design and fixes for MacOS, Decay Shader
**Will**: Interaction Shader Effect, Documentation, Settings Menu, Several fixes to gravity and other behaviour.


## References and External Resources
The sections of the Shader Report contain references for the development of individual shaders.

- **OpenAI Codex:** Used as a development assistant to support the implementation, debugging and refinement of various game features, including the settings menu, player controls, gravity mechanics, object interactions and environmental visuals. It also assisted with fixing gameplay issues and improving the overall stability of the prototype. The team guided the development process, reviewed the implementations and tested the resulting features.
