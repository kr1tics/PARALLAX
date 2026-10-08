# Scene 01 typography replacement

Branch: `scene01-visual-refinement`.

## Actual replacements

- All nine authored TMP components in StartScene use the existing Oxanium-Regular SDF or SpaceMono-Regular SDF assets. Their saved shared materials and world MeshRenderer material references now also point to the matching atlas materials. There are **zero LiberationSans GUID references** left in StartScene.
- The 24 prefab instances resolve to four prefab assets (modular room/wall pieces and the distortion cube). None contains TMP text, including nested content.
- The seven generated HUD labels use Oxanium for objective titles/numbers and Space Mono for instructions, timeline status and interaction prompts. All 16 authored/generated TMP labels are checked for actual glyph font identity, overflow, Regular weight and Normal style.
- Scene01Typography applies the same assignments to newly created scene text. Authored major signs are explicitly assigned as headings; runtime objective titles/numbers and button labels use the heading face; other new text defaults to the supporting face.
- The existing Settings/Controls UI was using system default fonts because it is UI Toolkit, not TMP. Scene01Typography now assigns Oxanium to headings, field labels and buttons; Space Mono to values, descriptions, controls and status. Quality dropdown text is covered by the Settings panel root. Bold simulation is removed; heading letter spacing is 0.5 and supporting spacing is 0.
- TMP colour is #DCE4E8 (approximately), with #8B9298 for the secondary objective identifier. Settings supporting status/value text uses the same muted grey. Button contrast, layout, existing sizes, alignment and scrolling are retained.

## Scope and compatibility

Only StartScene, the new scene-scoped component, test files and verification records change. The Intro prefab/script, shared font assets, source TTF files, global TMP defaults, shared Settings stylesheet and Settings controller are unchanged.

The scene component skips all text under ContextIntro. Its temporary Settings overrides are restored when the scene unloads, including returning to MainMenu, and reapplied if the Settings document rebuilds. It does not modify text content, objectives, input, puzzle state or gameplay.

UI Toolkit's FontDefinition accepts a Unity Font or a TextCore FontAsset, rather than a TMP_FontAsset. Settings therefore reuses the exact existing Regular source files already referenced by the two TMP SDF assets; no new family or shared asset is created. See [Unity FontDefinition](https://docs.unity3d.com/cn/6000.0/ScriptReference/UIElements.FontDefinition.html) and [FromFont](https://docs.unity3d.com/cn/6000.0/ScriptReference/UIElements.FontDefinition.FromFont.html).

## Verification

Unity 6000.3.20f1: **44/44 PlayMode tests passed**; no compilation errors or unhandled runtime exceptions. Full report: `Verification/scene01-guidance-playmode.xml`.

Scene01TypographyTests checks all 16 TMP labels and every visible glyph against the intended asset, so merely assigning a reference while rendering with a fallback cannot pass. A dynamically created label is also checked. Settings fonts, normal weights, control scroll reachability and rollback on returning to MainMenu are tested at 960×540, 1280×720 and 1920×1080. PlayMode renders of scene HUD/signage and Settings are inspected for overlap and clipping.

Edit the saved font references on **StartScene → Level 1 - Engineering Access → Scene01Typography**. Intro settings remain separate. Review final font appearance in the normal Game view as well; UI Toolkit renders Settings from the same source face using its own text renderer.
