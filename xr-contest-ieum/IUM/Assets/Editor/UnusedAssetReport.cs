using System.IO;
using System.Linq;
using UnityEditor;

public static class UnusedAssetReport {
    [MenuItem("Tools/Unused Asset Report")]
    public static void Run() {
        var all = AssetDatabase.GetAllAssetPaths()
            .Where(p => p.StartsWith("Assets/") && !AssetDatabase.IsValidFolder(p)).ToArray();
        var roots = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path)
            .Concat(all.Where(p => p.Contains("/Resources/")
                                || p.Contains("/StreamingAssets/")
                                || p.Contains("/@AddressableAssets/")))
            .Distinct()
            .Where(File.Exists)
            .ToArray();

        var used = AssetDatabase.GetDependencies(roots, true).ToHashSet();

        var lines = all
            .Where(p => !used.Contains(p) && !p.EndsWith(".cs") && !p.EndsWith(".asmdef"))
            .Where(File.Exists)
            .Select(p => (p, mb: new FileInfo(p).Length / 1048576.0))
            .OrderByDescending(x => x.mb)
            .Select(x => $"{x.mb:F2} MB\t{x.p}")
            .ToArray();

        File.WriteAllLines("UnusedAssets.txt", lines);

        // Extra diagnostics that help interpret the list.
        File.WriteAllLines("UnusedAssets_roots.txt", roots);
        File.WriteAllLines("UnusedAssets_used.txt", used.OrderBy(p => p));
        UnityEngine.Debug.Log($"UnusedAssetReport: roots={roots.Length} all={all.Length} used={used.Count} unused={lines.Length}");
    }
}
