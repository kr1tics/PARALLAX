using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Parallax.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Parallax.Gameplay.Tests
{
    public class Scene01TypographyTests
    {
        [UnitySetUp] public IEnumerator Setup()
        {
            if (GameSettingsMenu.Instance != null) { Object.Destroy(GameSettingsMenu.Instance.gameObject); yield return null; }
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("StartScene"); yield return new WaitForSecondsRealtime(.35f);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        { if (GameSettingsMenu.Instance != null) { GameSettingsMenu.Instance.Close(); Object.Destroy(GameSettingsMenu.Instance.gameObject); } Time.timeScale = 1; yield return null; }

        [UnityTest] public IEnumerator EverySavedAndGeneratedTmpLabelUsesItsAssignedFaceWithoutFallback()
        {
            var texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t.gameObject.scene.name == "StartScene").ToArray();
            Assert.That(texts.Length, Is.EqualTo(16), "Nine authored labels and seven generated HUD labels must all be audited.");
            foreach (var text in texts)
            {
                bool heading = new[] { "Sealed store label", "Main instructions", "Objective number", "Exit instructions",
                    "Arrival / practice", "Intro instructions", "Objective title" }.Contains(text.name);
                Assert.That(text.font.name, Is.EqualTo(heading ? "Oxanium-Regular SDF" : "SpaceMono-Regular SDF"), text.name);
                Assert.That(text.fontStyle, Is.EqualTo(FontStyles.Normal), text.name);
                Assert.That(text.fontWeight, Is.EqualTo(FontWeight.Regular), text.name);
                text.ForceMeshUpdate(true, true);
                Assert.That(text.isTextOverflowing, Is.False, text.name);
                foreach (var character in text.textInfo.characterInfo.Take(text.textInfo.characterCount).Where(c => c.isVisible))
                    Assert.That(character.fontAsset, Is.SameAs(text.font), text.name + " must not silently use a fallback font.");
            }
            var dynamic = new GameObject("Runtime instruction probe"); dynamic.transform.SetParent(GameObject.Find("Level 1 - Engineering Access").transform);
            var label = dynamic.AddComponent<TextMeshPro>(); label.text = "Status probe";
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(label.font.name, Is.EqualTo("SpaceMono-Regular SDF"));
            Object.Destroy(dynamic);
        }

        [UnityTest] public IEnumerator SettingsUsesMatchingRegularFacesAtThreeResolutionsAndRestoresOnUnload()
        {
            var menu = GameSettingsMenu.Instance; menu.Open();
            var document = menu.GetComponent<UIDocument>(); var root = document.rootVisualElement;
            Directory.CreateDirectory(".utmp/scene01-typography");
            foreach (var dimensions in new[] { new Vector2Int(960, 540), new Vector2Int(1280, 720), new Vector2Int(1920, 1080) })
            {
                var rt = new RenderTexture(dimensions.x, dimensions.y, 24); rt.Create(); document.panelSettings.targetTexture = rt;
                for (int i = 0; i < 12; i++) yield return null;
                Assert.That(root.Q<Label>("settings-title").resolvedStyle.unityFontDefinition.font.name, Is.EqualTo("Oxanium-Regular"));
                Assert.That(root.Q<Button>("resume").resolvedStyle.unityFontDefinition.font.name, Is.EqualTo("Oxanium-Regular"));
                Assert.That(root.Q<Label>("settings-status").resolvedStyle.unityFontDefinition.font.name, Is.EqualTo("SpaceMono-Regular"));
                foreach (var label in root.Query<Label>(className: "control-action").ToList())
                    Assert.That(label.resolvedStyle.unityFontDefinition.font.name, Is.EqualTo("SpaceMono-Regular"));
                foreach (var label in root.Query<TextElement>().ToList())
                {
                    Assert.That(label.resolvedStyle.unityFontStyleAndWeight, Is.EqualTo(FontStyle.Normal), label.name);
                    Assert.That(float.IsNaN(label.layout.width), Is.False, label.name);
                }
                Capture(rt, "settings-" + dimensions.x + "x" + dimensions.y);
                var scroll = root.Q<ScrollView>("settings-scroll"); scroll.scrollOffset = new Vector2(0, scroll.verticalScroller.highValue);
                for (int i = 0; i < 6; i++) yield return null;
                var lastAction = root.Query<Label>(className: "control-action").ToList().Last();
                Assert.That(lastAction.worldBound.yMax, Is.LessThanOrEqualTo(scroll.contentViewport.worldBound.yMax + 2), "All controls must be reachable.");
                Capture(rt, "controls-" + dimensions.x + "x" + dimensions.y);
                scroll.scrollOffset = Vector2.zero; document.panelSettings.targetTexture = null; Object.Destroy(rt);
            }
            menu.Close();
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return null;
            Assert.That(root.Q<Label>("settings-title").style.unityFontDefinition.keyword, Is.EqualTo(StyleKeyword.Null), "Scene-only overrides must be removed on returning to the menu.");
        }
        static void Capture(RenderTexture target, string name)
        {
            var old = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            File.WriteAllBytes(".utmp/scene01-typography/" + name + ".png", image.EncodeToPNG());
            RenderTexture.active = old; Object.Destroy(image);
        }
    }
}
