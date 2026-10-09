using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Parallax.Gameplay.Tests
{
    public class InteractionDistortionTests
    {
        private GameObject root;
        private Material source;
        private static Type RuntimeType(string name) => Array.Find(AppDomain.CurrentDomain.GetAssemblies(),
            a => a.GetName().Name == "Assembly-CSharp").GetType(name);
        private static void Play(GameObject target) => RuntimeType("InteractionDistortion")
            .GetMethod("Play").Invoke(null, new object[] { target });

        [TearDown]
        public void Cleanup()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (source != null) Object.DestroyImmediate(source);
        }

        private GameObject Cube()
        {
            root = new GameObject("Interacted object");
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(root.transform);
            source = new Material(Shader.Find("Standard")) { color = Color.cyan };
            cube.GetComponent<Renderer>().sharedMaterial = source;
            return cube;
        }

        [UnityTest]
        public IEnumerator TimedDistortionClearsWhenObjectIsDisabledAndCanRestart()
        {
            var cube = Cube();
            var effect = cube.AddComponent(RuntimeType("DistortableObject"));
            var renderer = cube.GetComponent<Renderer>();
            var block = new MaterialPropertyBlock();
            effect.GetType().GetMethod("ActivateDistortion").Invoke(effect, null);
            yield return new WaitForSeconds(.3f);
            renderer.GetPropertyBlock(block);
            Assert.That(block.GetFloat("_EffectAmount"), Is.GreaterThan(.9f));
            cube.SetActive(false);
            cube.SetActive(true);
            renderer.GetPropertyBlock(block);
            Assert.That(block.GetFloat("_EffectAmount"), Is.Zero, "Deactivation stops the coroutine; its temporary effect must not remain stuck.");
            effect.GetType().GetMethod("ActivateDistortion").Invoke(effect, null);
            yield return new WaitForSeconds(1.9f);
            renderer.GetPropertyBlock(block);
            Assert.That(block.GetFloat("_EffectAmount"), Is.Zero);
        }

        [UnityTest]
        public IEnumerator ChildMeshesPulseThenRestoreWithoutChangingColliderOrSource()
        {
            var cube = Cube();
            var filter = cube.GetComponent<MeshFilter>();
            var original = filter.sharedMesh;
            var collider = cube.GetComponent<BoxCollider>();
            var size = collider.size;
            Play(root);
            var renderer = cube.GetComponent<Renderer>();
            Assert.AreEqual("COMP30019/DistortableObject", renderer.sharedMaterial.shader.name);
            Assert.AreEqual(Color.cyan, renderer.sharedMaterial.color);
            Assert.IsNotNull(renderer.sharedMaterial.mainTexture,
                "Flat-colour objects need a pattern for visible chromatic splitting.");
            Assert.GreaterOrEqual(filter.sharedMesh.vertexCount, 32);
            Assert.AreEqual(filter.sharedMesh.vertexCount, filter.sharedMesh.uv.Length);
            Assert.AreEqual(original.bounds, filter.sharedMesh.bounds);
            Assert.AreEqual(size, collider.size);
            Assert.AreEqual("Standard", source.shader.name);
            yield return new WaitForSecondsRealtime(.3f);
            var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
            Assert.Greater(block.GetFloat("_EffectAmount"), .9f);
            Play(root); // repeated interaction restarts the pulse without retaining old instances
            yield return new WaitForSecondsRealtime(1.9f);
            Assert.AreSame(source, renderer.sharedMaterial);
            Assert.AreSame(original, filter.sharedMesh);
            renderer.GetPropertyBlock(block);
            Assert.AreEqual(0, block.GetFloat("_EffectAmount"));
        }

        [UnityTest]
        public IEnumerator DisableRestoresAndPuzzleMaterialChangesArePreserved()
        {
            var cube = Cube();
            var renderer = cube.GetComponent<Renderer>();
            var mesh = cube.GetComponent<MeshFilter>().sharedMesh;
            Play(root);
            root.SetActive(false);
            Assert.AreSame(source, renderer.sharedMaterial);
            Assert.AreSame(mesh, cube.GetComponent<MeshFilter>().sharedMesh);
            root.SetActive(true);
            Play(root);
            renderer.sharedMaterial = source; // time-state/puzzle logic updates appearance
            yield return new WaitForSecondsRealtime(1.9f);
            Assert.AreSame(source, renderer.sharedMaterial);
        }

        [UnityTest]
        public IEnumerator InteractionPickUpAndDropBothApplyPulse()
        {
            var cube = Cube();
            root.transform.position = Vector3.forward * 2;
            cube.AddComponent<Rigidbody>().useGravity = false;
            var movable = cube.AddComponent(RuntimeType("MovableObject"));
            var owner = new GameObject("Interaction owner");
            owner.transform.SetParent(root.transform);
            owner.transform.position = Vector3.zero;
            var interact = owner.AddComponent(RuntimeType("Interact"));
            var type = interact.GetType();
            type.GetField("rayOrigin").SetValue(interact, owner.transform);
            type.GetField("reachDistance").SetValue(interact, 3f);
            type.GetField("interactibleLayers", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(interact, (LayerMask)(1 << cube.layer));
            Physics.SyncTransforms();
            type.GetMethod("TryInteract").Invoke(interact, null);
            Assert.AreSame(movable, type.GetProperty("HeldObject").GetValue(interact));
            Assert.AreEqual("COMP30019/DistortableObject", cube.GetComponent<Renderer>().sharedMaterial.shader.name);
            yield return new WaitForSecondsRealtime(1.9f);
            Assert.AreEqual("COMP30019/DistortableObject", cube.GetComponent<Renderer>().sharedMaterial.shader.name,
                "The shader stays applied beyond the original pulse duration while held.");
            var block = new MaterialPropertyBlock();
            cube.GetComponent<Renderer>().GetPropertyBlock(block);
            Assert.AreEqual(1f, block.GetFloat("_EffectAmount"));
            owner.transform.rotation = Quaternion.Euler(0, 180, 0);
            type.GetMethod("TryInteract").Invoke(interact, null);
            Assert.IsNull(type.GetProperty("HeldObject").GetValue(interact));
            Assert.AreEqual("COMP30019/DistortableObject", cube.GetComponent<Renderer>().sharedMaterial.shader.name);
            yield return new WaitForSecondsRealtime(.7f);
            Assert.AreSame(source, cube.GetComponent<Renderer>().sharedMaterial,
                "Dropping finishes the effect and restores the original material.");
        }

        [UnityTest]
        public IEnumerator SwitchInteractionPulsesItsChildRenderer()
        {
            var cube = Cube();
            root.transform.position = Vector3.forward * 2;
            var target = root.AddComponent(RuntimeType("Switch"));
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            target.GetType().GetField("triggers", flags).SetValue(target,
                Array.CreateInstance(RuntimeType("Trigger"), 0));
            target.GetType().GetField("sideEffects", flags).SetValue(target,
                new UnityEngine.Events.UnityEvent[0]);
            var owner = new GameObject("Interaction owner");
            owner.transform.SetParent(root.transform);
            owner.transform.position = Vector3.zero;
            var interact = owner.AddComponent(RuntimeType("Interact"));
            var type = interact.GetType();
            type.GetField("rayOrigin").SetValue(interact, owner.transform);
            type.GetField("reachDistance").SetValue(interact, 3f);
            type.GetField("interactibleLayers", flags).SetValue(interact, (LayerMask)(1 << cube.layer));
            Physics.SyncTransforms();
            type.GetMethod("TryInteract").Invoke(interact, null);
            Assert.IsTrue((bool)target.GetType().GetField("on", flags).GetValue(target));
            Assert.AreEqual("COMP30019/DistortableObject", cube.GetComponent<Renderer>().sharedMaterial.shader.name);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FlatColourObjectsProduceVisibleChromaticSplitting()
        {
            var cube = Cube();
            source.color = Color.white;
            Play(root);
            var cameraObject = new GameObject("Effect test camera");
            cameraObject.transform.SetParent(root.transform);
            cameraObject.transform.position = new Vector3(0, 0, -3);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = .8f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var target = new RenderTexture(128, 128, 24);
            var pixels = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                var block = new MaterialPropertyBlock();
                block.SetFloat("_EffectAmount", 1);
                cube.GetComponent<Renderer>().SetPropertyBlock(block);
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); pixels.Apply();
                int splitPixels = 0;
                foreach (var pixel in pixels.GetPixels())
                    if (Mathf.Max(pixel.r, pixel.g, pixel.b) - Mathf.Min(pixel.r, pixel.g, pixel.b) > .08f)
                        splitPixels++;
                Assert.Greater(splitPixels, 40, "The existing shader must visibly split RGB, not just wobble.");
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null;
                Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
            yield return null;
        }
    }
}
