using System;
using System.Collections.Generic;
using System.Linq;
using Parallax.Settings;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Authors the Cold Storage scene in the Editor (no runtime geometry). Re-runnable: rebuilds the whole level from
/// Power Restoration's player, settings and EventSystem, keeping the scene asset and its GUID.</summary>
public static class ColdStorageBuilder
{
    public const string ScenePath = "Assets/Scenes/Level3_ColdStorage.unity";
    public const string SceneName = "Level3_ColdStorage";
    const string Fonts = "Assets/TextMesh Pro/Resources/Fonts & Materials/";
    static Transform shell, plant, dressing;
    static Material casing, steel, rubber, coolant, frost, glass, cable, locked, ready, amberStripe, cyanStripe;
    static TMP_FontAsset title, mono;
    static readonly List<FutureDecayEnvironment.Surface> surfaces = new();
    static readonly List<GameObject> pastOnly = new(), futureOnly = new();
    static readonly List<Light> zoneLights = new();

    [MenuItem("PARALLAX/Build Cold Storage Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level1_PowerRestoration.unity", OpenSceneMode.Single);
        foreach (var existing in scene.GetRootGameObjects())
            if (existing.GetComponent<PlayerController>() == null && existing.GetComponent<GlobalStateCompanion>() == null &&
                existing.GetComponent<GameSettingsMenu>() == null && existing.GetComponent<UnityEngine.EventSystems.EventSystem>() == null)
                Object.DestroyImmediate(existing);
        surfaces.Clear(); pastOnly.Clear(); futureOnly.Clear(); zoneLights.Clear();
        ColdStorageModels.Begin(surfaces);
        title = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Fonts + "Oxanium/Oxanium-Regular SDF.asset");
        mono = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Fonts + "Space_Mono/SpaceMono-Regular SDF.asset");
        casing = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Scene01/IndustrialCasing.mat");
        locked = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/HologramLocked.mat");
        ready = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/HologramReady.mat");
        glass = AssetDatabase.LoadAssetAtPath<Material>(ColdStorageModels.Kit + "Meshes/Materials/Glass.mat");
        steel = ColdStorageModels.Finish("MachinedSteel", new Color(.58f, .66f, .70f), .65f);
        rubber = ColdStorageModels.Finish("CableInsulation", new Color(.045f, .06f, .075f), .05f);
        coolant = ColdStorageModels.Finish("CoolantPipe", new Color(.05f, .48f, .56f), .55f, .12f);
        cable = ColdStorageModels.Finish("SafetyCable", new Color(.86f, .67f, .15f), .1f);
        frost = ColdStorageModels.Finish("Frost", new Color(.78f, .9f, .96f), .1f, .18f);
        amberStripe = ColdStorageModels.Finish("CellStripeA", new Color(1f, .55f, .15f), .2f, .25f);
        cyanStripe = ColdStorageModels.Finish("CellStripeB", new Color(.2f, .8f, 1f), .2f, .25f);

        shell = new GameObject("Cold Storage / station shell").transform;
        plant = new GameObject("Cold Storage / plant").transform;
        dressing = new GameObject("Cold Storage / timeline dressing").transform;
        Shell();
        var root = new GameObject("Level 3 - Cold Storage");
        var level = root.AddComponent<ColdStorageController>();
        Plant(level, out var refs);
        Dressing(refs);
        Lighting(refs);
        Cells(refs);
        var completion = root.AddComponent<TutorialCompletionScreen>();
        Set(completion, "primaryFont", title); Set(completion, "secondaryFont", mono);
        Set(completion, "finalLevel", true); Set(completion, "nextScene", "");
        Set(completion, "completionTitle", "STATION STABLE");
        Set(completion, "completionDescription", "Power and cooling restored. The station can sustain itself again.");
        Set(completion, "finalMessage", "Final level complete.");
        Set(completion, "replayLabel", "RESTART LEVEL"); Set(completion, "allowCompletionShortcut", false);
        refs["completion"] = completion;
        refs["player"] = Object.FindFirstObjectByType<PlayerController>();
        refs["sounds"] = Resources.Load<ParallaxAudioPalette>("Audio/ParallaxAudioPalette");
        refs["titleFont"] = title; refs["supportingFont"] = mono;
        refs["lockedMaterial"] = locked; refs["readyMaterial"] = ready;
        Wire(level, refs);
        root.AddComponent<FutureDecayEnvironment>().Configure(surfaces.ToArray());
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.16f, .19f, .23f);
        RenderSettings.fog = true; RenderSettings.fogColor = new Color(.035f, .05f, .065f); RenderSettings.fogDensity = .01f;
        EditorSceneManager.SaveScene(scene, ScenePath);
        ConnectProgression();
        AssetDatabase.SaveAssets();
        Debug.Log("COLD STORAGE SCENE BUILT: " + ScenePath);
    }

    // Cargo Bay continues here; Cold Storage is last in Build Settings. Only the Cargo Bay completion string changes.
    public static void ConnectProgression()
    {
        var cargo = EditorSceneManager.OpenScene(CargoBaySetup.ScenePath, OpenSceneMode.Single);
        foreach (var screen in Object.FindObjectsByType<TutorialCompletionScreen>(FindObjectsSortMode.None))
            using (var data = new SerializedObject(screen))
                if (data.FindProperty("nextScene").stringValue != SceneName)
                {
                    data.FindProperty("nextScene").stringValue = SceneName; data.ApplyModifiedPropertiesWithoutUndo();
                    EditorSceneManager.SaveScene(cargo);
                }
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ---------------------------------------------------------------- shell
    static void Shell()
    {
        int structure = LayerMask.NameToLayer("Structure");
        void Solid(string name, Vector3 p, Vector3 size, bool visible = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(shell);
            go.transform.position = p; go.transform.localScale = size; go.layer = structure;
            var renderer = go.GetComponent<Renderer>(); renderer.enabled = visible;
            // Bulkheads show above the kit cladding: the kit atlas finish, decayed in 2176 like the panels.
            if (visible) { renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ColdStorageModels.Kit + "Meshes/Materials/Diffuse_01.mat"); ColdStorageModels.Weather(renderer, renderer.sharedMaterial); }
        }
        // StationGravity reads deck volumes from renderers named "Floor_".
        Solid("Floor_PlantDeck", new Vector3(0, -.2f, 0), new Vector3(20, .4f, 22));
        Solid("Ceiling", new Vector3(0, 12.2f, 0), new Vector3(20, .4f, 22), true);
        Solid("Port bulkhead", new Vector3(-10, 6, 0), new Vector3(.4f, 12, 22), true);
        Solid("Starboard bulkhead", new Vector3(10, 6, 0), new Vector3(.4f, 12, 22), true);
        Solid("Reactor-side bulkhead", new Vector3(0, 6, 11), new Vector3(20, 12, .4f), true);
        Solid("Arrival bulkhead", new Vector3(0, 6, -11), new Vector3(20, 12, .4f), true);
        Solid("Floor_UpperDeck", new Vector3(6, 6.8f, 6.3f), new Vector3(6, .4f, 6.6f));
        Solid("Upper deck rear rail", new Vector3(6, 7.5f, 9.45f), new Vector3(6, 1, .12f));
        foreach (var x in new[] { 3.2f, 8.8f }) Solid("Deck column", new Vector3(x, 3.3f, 9.2f), new Vector3(.3f, 6.6f, .3f));
        Solid("Deck column", new Vector3(8.8f, 3.3f, 3.3f), new Vector3(.3f, 6.6f, .3f));
        shell.gameObject.AddComponent<StationGravity>();
        using (var data = new SerializedObject(shell.GetComponent<StationGravity>()))
        { var height = data.FindProperty("heightAboveDeck"); if (height != null) { height.floatValue = 14; data.ApplyModifiedPropertiesWithoutUndo(); } }

        // Main's modular kit: floor tiles, wall cladding, columns and ceiling fixtures (visual; collision is the boxes above).
        for (int ix = 0; ix < 5; ix++) for (int iz = 0; iz < 5; iz++)
                ColdStorageModels.KitPiece("Floor_01", new Vector3(-8 + ix * 4, .018f, -8.8f + iz * 4.4f), new Vector3(4, .04f, 4.4f), 0, shell);
        for (int x = 0; x < 2; x++) for (int z = 0; z < 2; z++)
                ColdStorageModels.KitPiece("Floor_01", new Vector3(4.5f + x * 3, 7.018f, 4.65f + z * 3.3f), new Vector3(3, .04f, 3.3f), 0, shell);
        // One 6 m row of kit cladding per wall, sized and oriented as in Cargo Bay (thin axis z before the yaw).
        for (int i = 0; i < 4; i++)
        {
            float along = -8.25f + i * 5.5f, across = -7.5f + i * 5;
            ColdStorageModels.KitPiece("Wall_Simple_01", new Vector3(-9.6f, 3, along), new Vector3(5.5f, 6, .4f), 90, shell);
            ColdStorageModels.KitPiece("Wall_Simple_01", new Vector3(9.6f, 3, along), new Vector3(5.5f, 6, .4f), -90, shell);
            ColdStorageModels.KitPiece("Wall_Simple_01", new Vector3(across, 3, 10.6f), new Vector3(5, 6, .4f), 180, shell);
            ColdStorageModels.KitPiece("Wall_Simple_01", new Vector3(across, 3, -10.6f), new Vector3(5, 6, .4f), 0, shell);
        }
        foreach (var x in new[] { -9.55f, 9.55f }) foreach (var z in new[] { -8f, -2.7f, 2.7f, 8f })
                ColdStorageModels.KitPiece("Column_01_Top", new Vector3(x, 6, z), new Vector3(.5f, 12, .5f), 0, shell);
        ColdStorageModels.Box("Ceiling liner", new Vector3(0, 12.02f, 0), new Vector3(19.6f, .06f, 21.6f), casing, shell, true);
        ColdStorageModels.Box("Deck structure", new Vector3(6, 6.8f, 6.3f), new Vector3(6, .24f, 6.6f), casing, shell, true);
        ColdStorageModels.Box("Deck front beam", new Vector3(6, 6.8f, 2.99f), new Vector3(6.16f, .40f, .22f), steel, shell, true);
        ColdStorageModels.Box("Deck side beam", new Vector3(2.99f, 6.8f, 6.3f), new Vector3(.22f, .40f, 6.6f), steel, shell, true);
        ColdStorageModels.Box("Deck rear rail", new Vector3(6, 7.5f, 9.45f), new Vector3(6, .08f, .08f), steel, shell, true);
        foreach (var x in new[] { 3.2f, 8.8f }) ColdStorageModels.Box("Deck column casing", new Vector3(x, 3.3f, 9.2f), new Vector3(.32f, 6.6f, .32f), casing, shell, true);
        ColdStorageModels.Box("Deck column casing", new Vector3(8.8f, 3.3f, 3.3f), new Vector3(.32f, 6.6f, .32f), casing, shell, true);
    }

    // ---------------------------------------------------------------- plant
    static void Plant(ColdStorageController level, out Dictionary<string, object> refs)
    {
        refs = new Dictionary<string, object>();
        int interactable = LayerMask.NameToLayer("Interactable"), structure = LayerMask.NameToLayer("Structure");

        // Primary heat exchanger and the trunk to the reactor hall restarted in Power Restoration.
        ColdStorageModels.Exchanger(new Vector3(0, 0, 7.7f), casing, steel, coolant, plant);
        foreach (var x in new[] { -.8f, .8f })
            ColdStorageModels.Route("Primary trunk", new[] { new Vector3(x, 1.2f, 8.6f), new Vector3(x, 1.2f, 10.9f) }, .16f, coolant, plant);
        Label("Trunk sign", "PRIMARY LOOP <-> REACTOR HALL", new Vector3(0, 5.5f, 10.35f), 0, 7, 2.4f);
        refs["reactorLabel"] = Label("Reactor loop repeater", "REACTOR LOOP\nCRITICAL · 612 K", new Vector3(0, 6.9f, 10.7f), 0, 7, 5.5f, title);

        // S1 cartridge bay at the exchanger inlet.
        var s1 = Plate("S1 cartridge bay", new Vector3(0, .1f, 5), casing, structure);
        foreach (var x in new[] { -.68f, .68f }) ColdStorageModels.Box("S1 guide", new Vector3(x, .45f, 5), new Vector3(.08f, .7f, 1.2f), steel, plant, true);
        ColdStorageModels.Box("S1 back plate", new Vector3(0, .6f, 5.68f), new Vector3(1.4f, 1f, .08f), steel, plant, true);
        refs["s1"] = s1; refs["s1Lamp"] = Lamp("S1 lamp", new Vector3(.82f, 1.25f, 4.4f));
        refs["s1Label"] = Label("S1 label", "S1 CARTRIDGE BAY", new Vector3(0, 1.75f, 4.3f), 0, 3.2f, 1.5f);
        var s1Frost = ColdStorageModels.Box("S1 frost ring", new Vector3(0, .24f, 5), new Vector3(1.5f, .06f, 1.5f), frost, plant);
        refs["s1Frost"] = s1Frost;

        // Exchanger console beside S1: start-up happens in 2176.
        var console = Control(level, ColdStorageControl.Kind.Console, "Exchanger console", new Vector3(-2.8f, 1.1f, 4.6f), new Vector3(1.3f, .9f, .6f), interactable);
        ColdStorageModels.Box("Exchanger console base", new Vector3(-2.8f, .35f, 4.6f), new Vector3(.8f, .7f, .5f), steel, plant, true);
        refs["consoleLamp"] = Lamp("Console lamp", new Vector3(-2.35f, 1.62f, 4.28f));
        refs["consoleLabel"] = Label("Console label", "EXCHANGER", new Vector3(-2.8f, 2.05f, 4.25f), 0, 3.2f, 1.5f);

        // Balance pan P lifts R's plug hatch through a cable up the deck edge.
        refs["pan"] = Plate("Balance pan P", new Vector3(2.6f, .1f, 3), steel, structure);
        ColdStorageModels.Box("Pan rim", new Vector3(2.6f, .25f, 3), new Vector3(1.35f, .1f, 1.35f), cable, plant, true);
        refs["panLabel"] = Label("Pan label", "BALANCE PAN", new Vector3(3.4f, 1.5f, 2.25f), 0, 3.2f, 1.4f);
        ColdStorageModels.Route("Hatch balance cable", new[] { new Vector3(2.6f, .3f, 3.62f), new Vector3(2.6f, 11.3f, 3.62f), new Vector3(7.85f, 11.3f, 3.62f), new Vector3(7.85f, 11.3f, 6), new Vector3(7.85f, 10.75f, 6) }, .035f, cable, plant);
        foreach (var p in new[] { new Vector3(2.6f, 11.3f, 3.62f), new Vector3(7.85f, 11.3f, 3.62f), new Vector3(7.85f, 11.3f, 6) })
            ColdStorageModels.Box("Cable pulley", p, Vector3.one * .22f, steel, plant, true);

        // R: a vacuum-insulated alcove on the hull radiator, sealed by a plug hatch.
        void RPart(string name, Vector3 p, Vector3 size, Quaternion rotation)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(plant);
            go.transform.SetPositionAndRotation(p, rotation); go.transform.localScale = size; go.layer = structure;
            go.GetComponent<Renderer>().sharedMaterial = casing; ColdStorageModels.Weather(go.GetComponent<Renderer>(), casing);
        }
        var slide = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Material/CoolantExchange/RackSlide.physicMaterial");
        if (slide == null)
        {
            slide = new PhysicsMaterial("RackSlide") { staticFriction = .02f, dynamicFriction = .02f, frictionCombine = PhysicsMaterialCombine.Minimum };
            AssetDatabase.CreateAsset(slide, "Assets/Material/CoolantExchange/RackSlide.physicMaterial");
        }
        RPart("R rack floor", new Vector3(9.0f, 9.1f, 6), new Vector3(1.75f, .2f, 1.8f), Quaternion.Euler(0, 0, -8));
        plant.Find("R rack floor").GetComponent<Collider>().sharedMaterial = slide;
        RPart("R side wall", new Vector3(9.0f, 9.95f, 5.05f), new Vector3(1.75f, 1.7f, .15f), Quaternion.identity);
        RPart("R side wall", new Vector3(9.0f, 9.95f, 6.95f), new Vector3(1.75f, 1.7f, .15f), Quaternion.identity);
        RPart("R roof", new Vector3(9.0f, 10.8f, 6), new Vector3(1.75f, .15f, 2.05f), Quaternion.identity);
        ColdStorageModels.Box("R hull backing", new Vector3(9.85f, 10, 6), new Vector3(.1f, 4, 4.8f), casing, plant, true);
        ColdStorageModels.Box("R radiator window",new Vector3(9.76f, 9.95f, 6), new Vector3(.04f, 1.2f, 1.6f), glass, plant);
        ColdStorageModels.Box("R permanent frost", new Vector3(9.7f, 9.95f, 6), new Vector3(.05f, 1.3f, 1.7f), frost, plant);
        var hatch = GameObject.CreatePrimitive(PrimitiveType.Cube); hatch.name = "R plug hatch"; hatch.transform.SetParent(plant);
        hatch.transform.position = new Vector3(8.1f, 9.95f, 6); hatch.transform.localScale = new Vector3(.15f, 1.6f, 1.8f); hatch.layer = structure;
        hatch.GetComponent<Renderer>().sharedMaterial = casing; ColdStorageModels.Weather(hatch.GetComponent<Renderer>(), casing);
        ColdStorageModels.Box("R hatch inspection glass", new Vector3(8.02f, 10.1f, 6), new Vector3(.02f, .4f, 1.2f), glass, hatch.transform);
        ColdStorageModels.Box("R hatch balance arm", new Vector3(8.0f, 10.8f, 6), new Vector3(.1f, .1f, 1.9f), steel, hatch.transform, true);
        refs["hatch"] = hatch; refs["rLamp"] = Lamp("R lamp", new Vector3(8.0f, 9.0f, 6.95f));
        refs["rLabel"] = Label("R label", "R RADIATOR SINK", new Vector3(7.95f, 11.5f, 6), 90, 4, 1.6f);

        // S2: the exchanger's open hot-side cradle on the deck.
        refs["s2"] = Plate("S2 hot socket", new Vector3(5, 7.1f, 5.8f), casing, structure);
        foreach (var x in new[] { -.68f, .68f }) ColdStorageModels.Box("S2 lip", new Vector3(5 + x, 7.3f, 5.8f), new Vector3(.08f, .25f, 1.2f), steel, plant, true);
        refs["s2Lamp"] = Lamp("S2 lamp", new Vector3(5.8f, 7.6f, 5.15f));
        refs["s2Label"] = Label("S2 label", "S2 HOT SOCKET", new Vector3(5, 8.4f, 5.1f), 0, 3.2f, 1.5f);
        refs["tankCoolant"] = ColdStorageModels.Tank(new Vector3(4.15f, 7, 8.15f), steel, casing, glass, coolant, plant);

        // Loop riser from S1 to the deck, with flange F3 at hover height.
        ColdStorageModels.Route("Loop riser", new[] { new Vector3(.9f, .4f, 8.15f), new Vector3(2.9f, .4f, 8.15f), new Vector3(2.9f, 7.4f, 8.15f), new Vector3(4.6f, 7.4f, 8.15f), new Vector3(4.6f, 7.4f, 6.5f) }, .12f, coolant, plant);
        ColdStorageModels.Ring("Flange F3", new Vector3(2.9f, 4.6f, 8.15f), .2f, .06f, steel, plant);
        var flange = Control(level, ColdStorageControl.Kind.Flange, "Flange F3", new Vector3(2.9f, 4.6f, 8.15f), new Vector3(.55f, .4f, .55f), interactable);
        flange.GetComponent<Renderer>().enabled = false;
        refs["flange"] = flange.transform; refs["flangeLamp"] = Lamp("F3 lamp", new Vector3(2.9f, 4.05f, 7.85f));
        refs["flangeLabel"] = Label("F3 label", "F3", new Vector3(2.9f, 5.5f, 7.7f), 0, 3, 1.8f);
        var crack = new GameObject("F3 crack vapour (2026)").transform; crack.SetParent(plant);
        for (int i = 0; i < 3; i++) ColdStorageModels.Box("Vapour wisp", new Vector3(2.75f - i * .12f, 4.75f + i * .18f, 7.95f), new Vector3(.25f + i * .08f, .05f, .05f), glass, crack);
        var burst = new GameObject("F3 burst and ice (2176)").transform; burst.SetParent(plant);
        ColdStorageModels.Box("Ice cascade", new Vector3(2.9f, 2.3f, 8.0f), new Vector3(.5f, 4.5f, .35f), frost, burst);
        ColdStorageModels.Box("Ice mound", new Vector3(2.9f, .15f, 7.7f), new Vector3(1.4f, .3f, 1.1f), frost, burst);
        ColdStorageModels.Box("Burst flange half", new Vector3(3.1f, 4.4f, 7.9f), new Vector3(.35f, .08f, .35f), steel, burst).transform.rotation = Quaternion.Euler(30, 20, 15);
        var intact = new GameObject("F3 frost line (2176)").transform; intact.SetParent(plant);
        ColdStorageModels.Box("Riser frost sleeve", new Vector3(2.9f, 3.5f, 8.15f), new Vector3(.3f, 6.2f, .3f), frost, intact);
        refs["flangeCrack"] = crack.gameObject; refs["flangeBurst"] = burst.gameObject; refs["flangeIntact"] = intact.gameObject;

        // Plant valve: LOOP or SERVICE. Seized by 2176.
        var valve = Control(level, ColdStorageControl.Kind.Valve, "Plant valve", new Vector3(-4, 1.1f, -3), new Vector3(1.7f, 1f, .6f), interactable);
        ColdStorageModels.Box("Valve footing", new Vector3(-4, .3f, -3), new Vector3(.8f, .6f, .5f), steel, plant, true);
        ColdStorageModels.Handwheel(new Vector3(-4, 1.05f, -3.42f), cable, steel, plant);
        ColdStorageModels.Route("Service line to vault", new[] { new Vector3(-4.6f, .3f, -2.7f), new Vector3(-6, .3f, -2.7f), new Vector3(-6, .3f, 2.6f) }, .08f, cable, plant);
        refs["valveLamp"] = Lamp("Valve lamp", new Vector3(-3.3f, 1.5f, -3.32f));
        refs["valveLabel"] = Label("Valve label", "PLANT VALVE", new Vector3(-4, 2.05f, -3.35f), 0, 3.2f, 1.4f);
        var ice = ColdStorageModels.Box("Valve ice crust", new Vector3(-4, 1.05f, -3.5f), new Vector3(.75f, .7f, .1f), frost, dressing);
        futureOnly.Add(ice);

        // Active-chill vault: barred hydraulic door in 2026, buckled open by 2176.
        void VaultWall(string name, Vector3 p, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(plant); go.transform.position = p;
            go.transform.localScale = size; go.layer = structure; go.GetComponent<Renderer>().sharedMaterial = casing;
            ColdStorageModels.Weather(go.GetComponent<Renderer>(), casing);
        }
        VaultWall("Vault port wall", new Vector3(-8.5f, 2.25f, 6), new Vector3(.3f, 4.5f, 6));
        VaultWall("Vault starboard wall", new Vector3(-3.5f, 2.25f, 6), new Vector3(.3f, 4.5f, 6));
        VaultWall("Vault rear wall", new Vector3(-6, 2.25f, 9), new Vector3(5, 4.5f, .3f));
        VaultWall("Vault roof", new Vector3(-6, 4.5f, 6), new Vector3(5, .3f, 6));
        var gate = GameObject.CreatePrimitive(PrimitiveType.Cube); gate.name = "Vault door"; gate.transform.SetParent(plant);
        gate.transform.position = new Vector3(-6, 2.25f, 3); gate.transform.localScale = new Vector3(4.8f, 4.5f, .3f); gate.layer = structure;
        gate.GetComponent<Renderer>().enabled = false;
        for (int i = 0; i < 9; i++) ColdStorageModels.Box("Vault door bar", new Vector3(-7.9f + i * .475f, 2.25f, 3), new Vector3(.08f, 4.3f, .08f), steel, gate.transform, true);
        foreach (float y in new[] { .15f, 2.25f, 4.3f }) ColdStorageModels.Box("Vault door rail", new Vector3(-6, y, 3), new Vector3(4.6f, .12f, .12f), casing, gate.transform, true);
        refs["vaultGate"] = gate;
        refs["vaultLabel"] = Label("Vault label", "ACTIVE-CHILL VAULT", new Vector3(-6, 5.35f, 2.8f), 0, 5, 2.4f);
        var vaultLamp = new GameObject("Vault chill lamp", typeof(Light)).GetComponent<Light>(); vaultLamp.transform.SetParent(dressing);
        vaultLamp.transform.position = new Vector3(-6, 3.8f, 6); vaultLamp.type = LightType.Point; vaultLamp.color = new Color(.55f, .85f, 1); vaultLamp.range = 6; vaultLamp.intensity = 1.2f;
        pastOnly.Add(vaultLamp.gameObject);

        // Flow lamps light in loop order during the start-up.
        refs["loopLamps"] = new[] { new Vector3(0, .26f, 5.75f), new Vector3(0, 1.4f, 9.1f), new Vector3(.8f, 1.5f, 10.6f), new Vector3(1.6f, .58f, 8.15f), new Vector3(2.9f, 2f, 7.95f), new Vector3(2.9f, 6.2f, 7.95f), new Vector3(4.6f, 7.65f, 7.4f) }
            .Select(p => (Object)Lamp("Loop flow lamp", p)).ToArray();

        // Topology and storage notes: the rules of the plant, never the steps.
        ColdStorageModels.Box("Plant board", new Vector3(4.5f, 2.35f, -1.5f), new Vector3(3.8f, 2.8f, .15f), casing, plant, true);
        Label("Plant board heading", "PRIMARY COOLANT PLANT", new Vector3(4.5f, 3.45f, -1.59f), 0, 3.6f, 3f, title);
        Label("Plant board body",
            "LOOP:  REACTOR <-> EXCHANGER <-> S1 BAY -> RISER F3 -> S2\n" +
            "LOOP CLOSED:  S1 CARTRIDGES SEALED IN\n" +
            "SERVICE:  LOOP VENTED · VAULT HYDRAULICS LIVE\n" +
            "UNSEALED CARTRIDGES BOIL OFF\n" +
            "R RADIATOR SINK:  PASSIVE COLD, NO POWER",
            new Vector3(4.5f, 2.2f, -1.59f), 0, 3.6f, 1.55f);
    }

    // ---------------------------------------------------------------- dressing and light
    static void Dressing(Dictionary<string, object> refs)
    {
        // 2176: buckled vault door, fallen ceiling panel, coolant stains. 2026: a maintenance cart.
        var door = ColdStorageModels.Box("Buckled vault door", new Vector3(-6.4f, .25f, 1.6f), new Vector3(4.4f, .25f, 2.4f), casing, dressing, true);
        door.transform.rotation = Quaternion.Euler(8, 12, 4); futureOnly.Add(door);
        var panel = ColdStorageModels.KitPiece("Wall_Simple_01", new Vector3(-7.6f, .3f, -7), new Vector3(3.2f, .2f, 3.2f), 25, dressing);
        panel.transform.rotation = Quaternion.Euler(0, 25, 6); futureOnly.Add(panel);
        foreach (var p in new[] { new Vector3(-1.5f, .025f, 6.2f), new Vector3(1.8f, .025f, 1.2f), new Vector3(-5.2f, .025f, -1.5f) })
            futureOnly.Add(ColdStorageModels.Box("Coolant stain", p, new Vector3(1.4f, .01f, 1.1f), rubber, dressing));
        var cart = new GameObject("Maintenance cart").transform; cart.SetParent(dressing);
        ColdStorageModels.Box("Cart body", new Vector3(-8, .7f, -8.5f), new Vector3(1.2f, .7f, .7f), casing, cart, true);
        ColdStorageModels.Box("Cart tray", new Vector3(-8, 1.1f, -8.5f), new Vector3(1.3f, .06f, .8f), steel, cart, true);
        pastOnly.Add(cart.gameObject);
        refs["pastOnly"] = pastOnly.Cast<Object>().ToArray(); refs["futureOnly"] = futureOnly.Cast<Object>().ToArray();
    }

    static void Lighting(Dictionary<string, object> refs)
    {
        foreach (var x in new[] { -5f, 5f }) foreach (var z in new[] { -6f, 0f, 6f })
            {
                ColdStorageModels.KitPiece("Light_01", new Vector3(x, 11.85f, z), new Vector3(2.6f, .15f, .6f), 0, shell);
                zoneLights.Add(PointLight("Zone light", new Vector3(x, 10.8f, z), new Color(.78f, .88f, .95f), 2.2f, 14));
            }
        zoneLights.Add(PointLight("Deck light", new Vector3(6, 10.6f, 6), new Color(.7f, .85f, 1), 1.6f, 9));
        var fill = PointLight("Station fill", new Vector3(0, 10, 0), new Color(.65f, .78f, .86f), .7f, 30);
        fill.type = LightType.Directional; fill.transform.rotation = Quaternion.Euler(50, -25, 0); RenderSettings.sun = fill;
        PointLight("R frost glow", new Vector3(8.9f, 10.1f, 6), new Color(.55f, .85f, 1), .8f, 3.5f);
        var warning = PointLight("Reactor loop warning", new Vector3(0, 6.2f, 9.8f), new Color(1, .5f, .15f), 1.4f, 9);
        var cooling = PointLight("Restored cooling", new Vector3(0, 4.5f, 6.4f), new Color(.3f, 1, .85f), 0, 13); cooling.enabled = false;
        refs["zoneLights"] = zoneLights.Cast<Object>().ToArray(); refs["warningLight"] = warning; refs["coolingLight"] = cooling;
    }

    // ---------------------------------------------------------------- cells
    static void Cells(Dictionary<string, object> refs)
    {
        var entanglers = new Object[2]; var past = new Object[2]; var future = new Object[2]; var windows = new Object[2];
        var pastTags = new List<Object>(); var futureTags = new List<Object>();
        var starts = new[] { new Vector3(-6, .45f, 6.6f), new Vector3(5, 7.65f, 5.8f) };
        for (int i = 0; i < 2; i++)
        {
            string id = i == 0 ? "A" : "B";
            var root = new GameObject("Cell " + id); root.transform.SetParent(plant);
            var causality = root.AddComponent<CausalityController>();
            using (var data = new SerializedObject(causality))
            { var types = data.FindProperty("_backingCausalityTypes"); types.arraySize = 1; types.GetArrayElementAtIndex(0).enumValueIndex = (int)Causality.CausalityType.Spatial; data.ApplyModifiedPropertiesWithoutUndo(); }
            var entangler = root.AddComponent<Entangler>();
            GameObject Copy(string era, List<Object> tags, out Renderer window)
            {
                var cell = GameObject.CreatePrimitive(PrimitiveType.Cube); cell.name = "Cell " + id + " - " + era;
                cell.transform.SetParent(root.transform); cell.transform.position = starts[i]; cell.transform.localScale = Vector3.one * .9f;
                cell.layer = LayerMask.NameToLayer("Interactable"); cell.GetComponent<Renderer>().enabled = false;
                var body = cell.AddComponent<Rigidbody>(); body.mass = 1; body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                cell.AddComponent<ArtificialGravity>(); cell.AddComponent<MovableObject>();
                window = ColdStorageModels.Cell(cell.transform, casing, steel, coolant, i == 0 ? amberStripe : cyanStripe, rubber);
                for (int side = 0; side < 4; side++)
                {
                    var tag = Label("Cell tag", "CELL " + id, Vector3.zero, side * 90, 1, 1.1f, mono);
                    tag.transform.SetParent(cell.transform, false);
                    tag.transform.localRotation = Quaternion.Euler(0, side * 90, 0);
                    tag.transform.localPosition = tag.transform.localRotation * new Vector3(0, .08f, -.5f);
                    tags.Add(tag);
                }
                return cell;
            }
            var pastCopy = Copy("2026", pastTags, out _); var futureCopy = Copy("2176", futureTags, out var futureWindow);
            pastCopy.SetActive(false);
            Set(entangler, "pastObject", pastCopy); Set(entangler, "futureObject", futureCopy); Set(entangler, "causalityController", causality);
            entanglers[i] = entangler; past[i] = pastCopy.GetComponent<Rigidbody>(); future[i] = futureCopy.GetComponent<Rigidbody>(); windows[i] = futureWindow;
        }
        refs["cells"] = entanglers; refs["pastCells"] = past; refs["futureCells"] = future; refs["futureCoolant"] = windows;
        refs["pastTags"] = pastTags.ToArray(); refs["futureTags"] = futureTags.ToArray();
    }

    // ---------------------------------------------------------------- helpers
    static PressureButton Plate(string name, Vector3 p, Material material, int layer)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(plant);
        go.transform.position = p; go.transform.localScale = new Vector3(1.2f, .2f, 1.2f); go.layer = layer;
        go.GetComponent<Renderer>().sharedMaterial = material; ColdStorageModels.Weather(go.GetComponent<Renderer>(), material);
        return go.AddComponent<PressureButton>();
    }
    static GameObject Control(ColdStorageController level, ColdStorageControl.Kind kind, string name, Vector3 p, Vector3 size, int layer)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(plant);
        go.transform.position = p; go.transform.localScale = size; go.layer = layer;
        go.GetComponent<Renderer>().sharedMaterial = casing; ColdStorageModels.Weather(go.GetComponent<Renderer>(), casing);
        go.AddComponent<ColdStorageControl>().Configure(level, kind); return go;
    }
    static Renderer Lamp(string name, Vector3 p) => ColdStorageModels.Box(name, p, new Vector3(.16f, .16f, .05f), locked, plant).GetComponent<Renderer>();
    static Light PointLight(string name, Vector3 p, Color color, float intensity, float range)
    {
        var light = new GameObject(name).AddComponent<Light>(); light.transform.SetParent(dressing);
        light.transform.position = p; light.type = LightType.Point; light.color = color; light.intensity = intensity; light.range = range; return light;
    }
    // World signage in main's style (Oxanium, white; the controller turns satisfied states green), sized for 1080p at ~6 m.
    static TMP_Text Label(string name, string text, Vector3 p, float yaw, float width, float size, TMP_FontAsset font = null)
    {
        var go = new GameObject(name, typeof(TextMeshPro)); go.transform.SetParent(plant);
        go.transform.SetPositionAndRotation(p, Quaternion.Euler(0, yaw, 0));
        var label = go.GetComponent<TextMeshPro>(); label.text = text; label.font = font != null ? font : title; label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center; label.textWrappingMode = TextWrappingModes.NoWrap;
        label.rectTransform.sizeDelta = new Vector2(width, 1.6f); label.color = new Color(.85f, .92f, .96f);
        return label;
    }
    static void Wire(ColdStorageController level, Dictionary<string, object> refs)
    {
        using var data = new SerializedObject(level);
        foreach (var pair in refs)
        {
            var property = data.FindProperty(pair.Key);
            if (property == null) throw new ArgumentException("ColdStorageController has no field " + pair.Key);
            if (pair.Value is Object[] items)
            {
                property.arraySize = items.Length;
                for (int i = 0; i < items.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }
            else property.objectReferenceValue = (Object)pair.Value;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    static void Set(Object owner, string field, object value)
    {
        using var serialized = new SerializedObject(owner); var property = serialized.FindProperty(field);
        if (property == null) throw new ArgumentException(owner.GetType().Name + " has no field " + field);
        if (value is Object reference) property.objectReferenceValue = reference;
        else if (value is bool boolean) property.boolValue = boolean;
        else if (value is string text) property.stringValue = text;
        else throw new ArgumentException(field);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // Scene audit for the rebuilt level: scripts, references and the gameplay set.
    public static void Audit()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var objects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToArray();
        foreach (var go in objects)
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) != 0) throw new Exception("Missing script: " + go.name);
            foreach (var component in go.GetComponents<Component>())
            {
                var iterator = new SerializedObject(component).GetIterator();
                while (iterator.NextVisible(true))
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference && iterator.objectReferenceValue == null && iterator.objectReferenceInstanceIDValue != 0)
                        throw new Exception("Broken reference: " + go.name + "." + iterator.propertyPath);
            }
        }
        if (objects.Count(go => go.GetComponent<PlayerController>() != null) != 1 || objects.Count(go => go.GetComponent<ColdStorageControl>() != null) != 3 ||
            objects.Count(go => go.GetComponent<PressureButton>() != null) != 3 || objects.Count(go => go.GetComponent<Entangler>() != null) != 2)
            throw new Exception("Cold Storage gameplay components are incomplete.");
        if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath && s.enabled)) throw new Exception("Cold Storage missing from Build Settings.");
        Debug.Log("COLD STORAGE AUDIT PASSED: " + objects.Length + " objects.");
    }
}
