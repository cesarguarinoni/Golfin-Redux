// Assets/Localization/Editor/LocalizationBuildHook.cs
#if UNITY_EDITOR
using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

/// <summary>
/// Regenerates <c>LocalizationTextTable.asset</c> from <c>LocalizationText.csv</c> at the start of
/// every build — the build-time counterpart to <see cref="LocalizationPlaymodeHook"/>, which does
/// the same thing when you enter Play Mode.
///
/// <para>
/// <b>Why both.</b> The play-mode hook fires on <c>PlayModeStateChange.ExitingEditMode</c> and
/// nothing else, so a build made after a CSV edit but without entering Play ships whatever is in
/// the committed <c>.asset</c>. That fails silently: the build succeeds, the strings are just
/// stale, and a missing key renders as the key itself on a tester's device. Editing a CSV row and
/// having it reach the build is the whole point of the CSV being the source.
/// </para>
/// <para>
/// <b>Why it fails the build rather than warning.</b> A missing CSV means the importer would leave
/// the previous table in place and the build would succeed with stale text. There is no useful
/// build to be had from that state, and a warning in a batchmode log is a warning nobody reads.
/// </para>
/// <para>
/// <c>LocalizationTextImporter.ImportCsv</c> ends in <c>EditorUtility.SetDirty</c> +
/// <c>AssetDatabase.SaveAssets</c>, so the regenerated table is on disk before the player data is
/// written. If a future pipeline ever consumes the table before preprocess callbacks run, the
/// deterministic fallback is running <c>Tools → Localization → Import Text CSV</c> as an explicit
/// step ahead of the build.
/// </para>
/// </summary>
public sealed class LocalizationBuildHook : IPreprocessBuildWithReport
{
    private const string CsvPath = "Assets/Localization/LocalizationText.csv";

    /// <summary>Early. Nothing here depends on other callbacks, and being first costs nothing.</summary>
    public int callbackOrder => -100;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (!File.Exists(CsvPath))
        {
            throw new BuildFailedException(
                $"[Localization] {CsvPath} is missing. Refusing to build rather than shipping " +
                "whatever LocalizationTextTable.asset happens to hold.");
        }

        LocalizationTextImporter.ImportCsv(logResult: true);
    }
}
#endif
