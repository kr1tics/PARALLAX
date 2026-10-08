using System;
using UnityEngine;

namespace Parallax.Settings
{
    /// <summary>
    /// Shared game preferences. Changes apply immediately; call Save when closing the menu.
    /// Mouse-look controllers can read MouseSensitivity and InvertLook, or listen to Changed.
    /// </summary>
    public static class GameSettings
    {
        public const float DefaultMasterVolume = 1f;
        public const float MinMouseSensitivity = 0.1f;
        public const float MaxMouseSensitivity = 3f;
        public const float DefaultMouseSensitivity = 1f;
        public const bool DefaultInvertLook = false;
        public const bool DefaultFullscreen = true;

        private const string PreferencePrefix = "Parallax.Settings.";
        private const string VolumeKey = PreferencePrefix + "MasterVolume";
        private const string QualityKey = PreferencePrefix + "QualityLevel";
        private const string SensitivityKey = PreferencePrefix + "MouseSensitivity";
        private const string InvertLookKey = PreferencePrefix + "InvertLook";
        private const string FullscreenKey = PreferencePrefix + "Fullscreen";

        private static bool initialized;
        private static bool dirty;
        private static int defaultQualityLevel;
        private static float masterVolume;
        private static int qualityLevel;
        private static float mouseSensitivity;
        private static bool invertLook;
        private static bool fullscreen;

        public static event Action Changed;

        public static string[] QualityNames => QualitySettings.names;

        /// <summary>The current display state, which can differ from the saved browser preference.</summary>
        public static bool IsFullscreen => Screen.fullScreen;

        public static float MasterVolume
        {
            get { Initialize(); return masterVolume; }
            set
            {
                Initialize();
                float sanitized = SanitizeFloat(value, DefaultMasterVolume, 0f, 1f);
                bool changed = masterVolume != sanitized;
                masterVolume = sanitized;
                AudioListener.volume = sanitized;
                PlayerPrefs.SetFloat(VolumeKey, sanitized);
                dirty = true;
                if (changed) Changed?.Invoke();
            }
        }

        public static int QualityLevel
        {
            get { Initialize(); return qualityLevel; }
            set
            {
                Initialize();
                int sanitized = ClampQuality(value);
                bool changed = qualityLevel != sanitized;
                qualityLevel = sanitized;
                ApplyQuality();
                PlayerPrefs.SetInt(QualityKey, sanitized);
                dirty = true;
                if (changed) Changed?.Invoke();
            }
        }

        public static float MouseSensitivity
        {
            get { Initialize(); return mouseSensitivity; }
            set
            {
                Initialize();
                float sanitized = SanitizeFloat(value, DefaultMouseSensitivity,
                    MinMouseSensitivity, MaxMouseSensitivity);
                bool changed = mouseSensitivity != sanitized;
                mouseSensitivity = sanitized;
                PlayerPrefs.SetFloat(SensitivityKey, sanitized);
                dirty = true;
                if (changed) Changed?.Invoke();
            }
        }

        public static bool InvertLook
        {
            get { Initialize(); return invertLook; }
            set
            {
                Initialize();
                bool changed = invertLook != value;
                invertLook = value;
                PlayerPrefs.SetInt(InvertLookKey, value ? 1 : 0);
                dirty = true;
                if (changed) Changed?.Invoke();
            }
        }

        public static bool Fullscreen
        {
            get { Initialize(); return fullscreen; }
            set { SetFullscreen(value, false); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            // Also runs when entering Play mode with domain reload disabled.
            Application.quitting -= Save;
            initialized = false;
            dirty = false;
            Changed = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            if (initialized) return;

            defaultQualityLevel = ClampQuality(QualitySettings.GetQualityLevel());
            initialized = true;
            Application.quitting -= Save;
            Application.quitting += Save;
            LoadPreferences();
            ApplySettings();
        }

        /// <summary>Refreshes preferences after an external change to PlayerPrefs.</summary>
        public static void Reload()
        {
            Initialize();
            LoadPreferences();
            ApplySettings();
            Changed?.Invoke();
        }

        /// <summary>
        /// Pass userInitiated only from a fullscreen button or toggle callback.
        /// Browsers require that gesture; loading preferences never enters fullscreen.
        /// </summary>
        public static void SetFullscreen(bool enabled, bool userInitiated)
        {
            Initialize();
            bool changed = fullscreen != enabled;
            fullscreen = enabled;
            PlayerPrefs.SetInt(FullscreenKey, enabled ? 1 : 0);
            dirty = true;
            ApplyFullscreen(userInitiated);
            if (changed) Changed?.Invoke();
        }

        /// <summary>Restores audio, platform default quality, controls, and fullscreen preference.</summary>
        public static void ResetDefaults()
        {
            Initialize();
            masterVolume = DefaultMasterVolume;
            qualityLevel = defaultQualityLevel;
            mouseSensitivity = DefaultMouseSensitivity;
            invertLook = DefaultInvertLook;
            fullscreen = DefaultFullscreen;

            PlayerPrefs.SetFloat(VolumeKey, masterVolume);
            PlayerPrefs.SetInt(QualityKey, qualityLevel);
            PlayerPrefs.SetFloat(SensitivityKey, mouseSensitivity);
            PlayerPrefs.SetInt(InvertLookKey, invertLook ? 1 : 0);
            PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
            dirty = true;
            ApplySettings();
            Save();
            Changed?.Invoke();
        }

        /// <summary>Flushes edited preferences without creating keys for untouched defaults.</summary>
        public static void Save()
        {
            if (!dirty) return;
            PlayerPrefs.Save();
            dirty = false;
        }

        private static void LoadPreferences()
        {
            masterVolume = SanitizeFloat(PlayerPrefs.GetFloat(VolumeKey, DefaultMasterVolume),
                DefaultMasterVolume, 0f, 1f);
            qualityLevel = ClampQuality(PlayerPrefs.GetInt(QualityKey, defaultQualityLevel));
            mouseSensitivity = SanitizeFloat(PlayerPrefs.GetFloat(SensitivityKey, DefaultMouseSensitivity),
                DefaultMouseSensitivity, MinMouseSensitivity, MaxMouseSensitivity);
            invertLook = ReadBool(InvertLookKey, DefaultInvertLook);
            fullscreen = ReadBool(FullscreenKey, DefaultFullscreen);
        }

        private static void ApplySettings()
        {
            AudioListener.volume = masterVolume;
            ApplyQuality();
            ApplyFullscreen(false);
        }

        private static void ApplyQuality()
        {
            if (QualitySettings.names.Length > 0 && QualitySettings.GetQualityLevel() != qualityLevel)
                QualitySettings.SetQualityLevel(qualityLevel, true);
        }

        private static void ApplyFullscreen(bool userInitiated)
        {
            if (Application.isEditor) return;
            if (Application.platform == RuntimePlatform.WebGLPlayer && !userInitiated) return;
            Screen.fullScreen = fullscreen;
        }

        private static int ClampQuality(int value)
        {
            return Mathf.Clamp(value, 0, Math.Max(0, QualitySettings.names.Length - 1));
        }

        private static float SanitizeFloat(float value, float fallback, float min, float max)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return fallback;
            return Mathf.Clamp(value, min, max);
        }

        private static bool ReadBool(string key, bool fallback)
        {
            int value = PlayerPrefs.GetInt(key, fallback ? 1 : 0);
            return value == 0 ? false : value == 1 ? true : fallback;
        }
    }
}
