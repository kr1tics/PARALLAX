using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Parallax.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Parallax.Gameplay.Tests
{
    public class Scene01PresentationTests
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        MonoBehaviour presentation, level;
        object state;
        static object Field(object value, string name) => value.GetType().GetField(name, Fields).GetValue(value);
        static object Property(object value, string name) => value.GetType().GetProperty(name).GetValue(value);
        static void Call(object value, string name, params object[] args) => value.GetType().GetMethod(name).Invoke(value, args);
        string Instruction => (string)Property(presentation, "CurrentInstruction");
        [UnitySetUp] public IEnumerator Setup()
        {
            if (GameSettingsMenu.Instance != null) { Object.Destroy(GameSettingsMenu.Instance.gameObject); yield return null; }
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("StartScene"); yield return null;
            var root = GameObject.Find("Level 1 - Engineering Access");
            presentation = root.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == "Level1Presentation");
            level = root.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == "Level1Controller");
            state = level.GetType().Assembly.GetType("GlobalState").GetMethod("get").Invoke(null, null);
            yield return new WaitForFixedUpdate();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        { if (GameSettingsMenu.Instance != null) { GameSettingsMenu.Instance.Close(); Object.Destroy(GameSettingsMenu.Instance.gameObject); } Time.timeScale = 1; yield return null; }
        IEnumerator Swap() { yield return new WaitForSeconds(1.05f); Call(state, "SwapTime"); yield return new WaitForSeconds(.65f); }

        [UnityTest] public IEnumerator ObjectiveFollowsActualPlacementAndReset()
        {
            Assert.That(Instruction, Is.EqualTo("Switch to the Past."));
            yield return Swap(); Assert.That(Instruction, Is.EqualTo("Locate the power cube."));
            var cube = ((Rigidbody[])Field(level, "introCubes"))[0];
            cube.position = new Vector3(2.5f, .1f, 13.45f); cube.transform.position = cube.position;
            cube.linearVelocity = Vector3.zero; Physics.SyncTransforms();
            yield return new WaitForSeconds(.8f);
            Assert.That(Instruction, Is.EqualTo("Switch to the Future."));
            Assert.That((bool)Property(level, "IntroSolved"), Is.False);
            yield return Swap(); yield return new WaitForSeconds(.8f);
            Assert.That((bool)Property(level, "IntroSolved"), Is.True);
            Assert.That(Instruction, Is.EqualTo("Switch to the Past."));
            Call(level, "ResetCurrentPuzzle"); yield return null;
            Assert.That(Instruction, Does.Contain("Switch to the Past"));
        }

        [UnityTest] public IEnumerator VisualDetailsDoNotAddPhysicsOrBlockPause()
        {
            var details = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(x => x.name == "Industrial inset (visual only)").ToArray();
            Assert.That(details.Length, Is.GreaterThan(8));
            Assert.That(details.All(x => x.GetComponent<Collider>() == null), Is.True);
            Assert.That(details.All(x => x.GetComponent<Rigidbody>() == null), Is.True);
            var hud = GameObject.Find("Scene 01 Objective UI").GetComponent<CanvasGroup>();
            Assert.That(hud.blocksRaycasts, Is.False);
            GameSettingsMenu.Instance.Open(); yield return null;
            Assert.That(hud.alpha, Is.Zero);
            GameSettingsMenu.Instance.Close(); yield return null; yield return null;
            Assert.That(hud.alpha, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator CapturesBothTimelinesAndChecksFontReferences()
        {
            Assert.That(((Object)Field(presentation, "titleFont")).name, Is.EqualTo("Oxanium-Regular SDF"));
            Assert.That(((Object)Field(presentation, "supportingFont")).name, Is.EqualTo("SpaceMono-Regular SDF"));
            var player = GameObject.Find("Player");
            foreach (var behaviour in player.GetComponents<MonoBehaviour>()) if (behaviour.GetType().Name == "PlayerController") behaviour.enabled = false;
            player.GetComponent<Rigidbody>().isKinematic = true;
            var camera = Camera.main;
            foreach (var behaviour in camera.GetComponents<MonoBehaviour>()) if (behaviour.GetType().Name == "Look") behaviour.enabled = false;
            camera.transform.position = new Vector3(4.6f, 1.6f, 10.8f);
            camera.transform.LookAt(new Vector3(2.6f, .5f, 14));
            yield return null; Capture(camera, "future-relay", 1280, 720);
            yield return Swap(); Capture(camera, "past-relay", 1280, 720);
            Capture(camera, "past-relay-wide", 1920, 1080);
            Capture(camera, "past-relay-small", 960, 540);
            Assert.That((bool)Property(level, "IntroSolved"), Is.False);
        }
        static void Capture(Camera camera, string name, int width, int height)
        {
            Directory.CreateDirectory(".utmp/scene01");
            var canvas = GameObject.Find("Scene 01 Objective UI").GetComponent<Canvas>();
            var previousMode = canvas.renderMode; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .8f;
            var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            var rt = new RenderTexture(width, height, 24); camera.targetTexture = rt; Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            File.WriteAllBytes(".utmp/scene01/" + name + ".png", image.EncodeToPNG());
            canvas.renderMode = previousMode; camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            Object.Destroy(image); Object.Destroy(rt);
        }
    }
}
