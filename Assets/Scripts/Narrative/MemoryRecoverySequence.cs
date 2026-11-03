using UnityEngine;

public enum MemoryRecoveryStage { Signal, Trace, Fragment, Restored }

public sealed class MemoryRecoverySequence
{
    public readonly struct Page
    {
        public readonly string Header, Main, Secondary;
        public readonly float Duration;
        public Page(string header, string main, string secondary, float duration)
        { Header = header; Main = main; Secondary = secondary; Duration = duration; }
    }
    public readonly Color Background, Accent, Foreground;
    public readonly Page[] Pages;
    public readonly string Label;
    MemoryRecoverySequence(string label, string background, string accent, string foreground, params Page[] pages)
    {
        Label = label; ColorUtility.TryParseHtmlString(background, out Background);
        ColorUtility.TryParseHtmlString(accent, out Accent); ColorUtility.TryParseHtmlString(foreground, out Foreground);
        Pages = pages;
    }
    public static MemoryRecoverySequence For(MemoryRecoveryStage stage) => stage switch
    {
        MemoryRecoveryStage.Signal => new("THE SIGNAL", "#061016", "#6BC7DF", "#E3F5FA",
            new("BACKUP TERMINAL / EMERGENCY ACCESS", "Emergency interface... online.", "", 3.5f),
            new("BACKUP TERMINAL / ARCHIVE ERROR", "Unidentified memory signature detected.", "Source unknown.", 4),
            new("BACKUP TERMINAL / PRIMARY DIRECTIVE", "Restore main power to access the station's final transmission.", "", 4.5f)),
        MemoryRecoveryStage.Trace => new("THE TRACE", "#100F20", "#B5A0FF", "#F0EBFF",
            new("STATION ARCHIVE / PARTIAL RECOVERY", "Main power restored.", "Archived personnel records detected.", 3.5f),
            new("STATION ARCHIVE / IDENTITY MATCH", "Memory signature match found.", "Record dated: 2026.", 4),
            new("STATION ARCHIVE / TRANSFER INTERRUPTED", "Archive transfer route obstructed.", "Restore cargo access to recover the missing data.", 4.5f)),
        MemoryRecoveryStage.Fragment => new("THE FRAGMENT", "#1C130D", "#E8AE69", "#FFF0DB",
            new("MEMORY ARCHIVE / DATA RECOVERED", "Memory fragment recovered.", "Origin: Station Research Division.", 3.5f),
            new("PERSONNEL ARCHIVE / CORRUPTED", "Last recorded presence: 2026.", "Current memory signature: identical.", 4),
            new("CORE ARCHIVE / THERMAL WARNING", "Core archive overheating.", "Restore cooling to complete memory recovery.", 4.5f)),
        _ => new("MEMORY RESTORED", "#17121B", "#E9A9D3", "#FFF0FA",
            new("CORE ARCHIVE / RECOVERY COMPLETE", "Memory archive restored.", "Reconstructing final personnel record...", 4),
            new("PERSONNEL RECORD / 2026", "Research personnel evacuated.", "No biological crew remained aboard.", 4.5f),
            new("IDENTITY VERIFICATION / MISMATCH", "Memory origin: Human.", "Current host: Synthetic.", 5),
            new("", "You were never the researcher.", "You were carrying what they left behind.", 5.5f),
            new("", "The station remembers.", "And now, so do you.", 8))
    };
}
