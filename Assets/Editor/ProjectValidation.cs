using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Read-only asset audit and repeatable Windows build verification.</summary>
public static class ProjectValidation
{
    public static void AuditAndBuild()
        => Build("Builds/Review/Parallax.exe", BuildOptions.Development);

    public static void BuildFinal()
        => Build("Builds/Final/Parallax.exe", BuildOptions.None);

    static void Build(string output, BuildOptions options)
    {
        var findings = new List<string>();
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        foreach (string path in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath))
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects()) Inspect(root, path, findings);
        }
        foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath))
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root != null) Inspect(root, path, findings);
        }
        foreach (string path in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath))
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                findings.Add(path + ": missing shader");
        }
        foreach (string path in AssetDatabase.FindAssets("t:Shader", new[] { "Assets/Shaders" }).Select(AssetDatabase.GUIDToAssetPath))
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            foreach (var message in ShaderUtil.GetShaderMessages(shader))
                findings.Add($"{path}:{message.line}: {message.severity}: {message.message}");
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/review-unity-assets.txt", findings.Distinct());
        Debug.Log($"Asset audit: {findings.Distinct().Count()} findings; see Logs/review-unity-assets.txt.");
        if (scenes.Length != 3 || scenes[0] != "Assets/MainMenu.unity" || scenes[1] != "Assets/StartScene.unity" || scenes[2] != "Assets/Scenes/Level1_PowerRestoration.unity")
            throw new InvalidOperationException("Expected MainMenu, tutorial StartScene, then Level1_PowerRestoration.");
        EditorSceneManager.OpenScene(scenes[0], OpenSceneMode.Single);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = options
        });
        File.WriteAllText("Logs/review-build-summary.txt",
            $"{report.summary.result}: {report.summary.totalErrors} errors, {report.summary.totalWarnings} warnings, {report.summary.totalSize} bytes\n");
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("Windows build failed; inspect the build log.");
    }

    static void Inspect(GameObject root, string path, List<string> findings)
    {
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            string location = path + " / " + AnimationUtility.CalculateTransformPath(transform, null);
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                findings.Add(location + ": missing script");
            foreach (var component in transform.GetComponents<Component>())
            {
                if (component == null) continue;
                using var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference &&
                        property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                        findings.Add(location + " / " + component.GetType().Name + "." + property.propertyPath + ": broken reference");
            }
        }
    }
}
