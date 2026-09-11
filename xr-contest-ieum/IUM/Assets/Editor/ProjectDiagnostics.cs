using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 재배치 전후 비교용 진단. 빌드 씬을 하나씩 열어 Missing Script / Missing Prefab / 로그 에러를 센다.
/// 배치모드: -executeMethod ProjectDiagnostics.Run
/// 출력 파일명은 -diagOut 인자로 바꾼다 (기본 Diagnostics.txt).
/// </summary>
public static class ProjectDiagnostics
{
    class SceneStat
    {
        public string path;
        public bool opened;
        public int gameObjects;
        public int missingScripts;
        public int missingPrefabs;
        public int errors;
        public List<string> missingScriptPaths = new List<string>();
        public List<string> missingPrefabPaths = new List<string>();
        public List<string> errorLines = new List<string>();
    }

    static int s_errors;
    static List<string> s_errorLines = new List<string>();

    static void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            s_errors++;
            if (s_errorLines.Count < 40) s_errorLines.Add(type + ": " + condition);
        }
    }

    public static void Run()
    {
        var outName = "Diagnostics.txt";
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-diagOut") outName = args[i + 1];

        var sb = new StringBuilder();
        var scenes = EditorBuildSettings.scenes;
        var stats = new List<SceneStat>();

        Application.logMessageReceived += OnLog;

        foreach (var s in scenes)
        {
            var st = new SceneStat { path = s.path };
            stats.Add(st);
            if (!s.enabled) continue;
            if (!File.Exists(s.path)) { st.opened = false; continue; }

            s_errors = 0;
            s_errorLines.Clear();
            Scene scene;
            try
            {
                scene = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
                st.opened = true;
            }
            catch (Exception e)
            {
                st.opened = false;
                st.errorLines.Add("OpenScene 실패: " + e.Message);
                continue;
            }

            foreach (var root in scene.GetRootGameObjects())
                Walk(root, st, root.name);

            st.errors = s_errors;
            st.errorLines.AddRange(s_errorLines);
        }

        Application.logMessageReceived -= OnLog;

        // ---- 프로젝트 전역: 프리팹 에셋 안의 Missing Script ----
        int prefabAssetMissing = 0;
        var prefabMissingList = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            if (!p.StartsWith("Assets/")) continue;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (go == null) continue;
            int n = CountMissingInHierarchy(go);
            if (n > 0) { prefabAssetMissing += n; if (prefabMissingList.Count < 60) prefabMissingList.Add(n + "\t" + p); }
        }

        sb.AppendLine("# ProjectDiagnostics");
        sb.AppendLine("generatedAt\t" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("unityVersion\t" + Application.unityVersion);
        sb.AppendLine("buildScenesTotal\t" + scenes.Length);
        sb.AppendLine("buildScenesEnabled\t" + scenes.Count(x => x.enabled));
        sb.AppendLine();
        sb.AppendLine("## TOTALS");
        sb.AppendLine("scenesOpened\t" + stats.Count(x => x.opened));
        sb.AppendLine("missingScripts\t" + stats.Sum(x => x.missingScripts));
        sb.AppendLine("missingPrefabs\t" + stats.Sum(x => x.missingPrefabs));
        sb.AppendLine("sceneErrors\t" + stats.Sum(x => x.errors));
        sb.AppendLine("prefabAssetMissingScripts\t" + prefabAssetMissing);
        sb.AppendLine();
        sb.AppendLine("## PER SCENE");
        sb.AppendLine("path\topened\tgameObjects\tmissingScripts\tmissingPrefabs\terrors");
        foreach (var st in stats)
            sb.AppendLine($"{st.path}\t{st.opened}\t{st.gameObjects}\t{st.missingScripts}\t{st.missingPrefabs}\t{st.errors}");

        sb.AppendLine();
        sb.AppendLine("## DETAIL missingScripts (scene\tobjectPath)");
        foreach (var st in stats)
            foreach (var d in st.missingScriptPaths)
                sb.AppendLine(st.path + "\t" + d);

        sb.AppendLine();
        sb.AppendLine("## DETAIL missingPrefabs (scene\tobjectPath)");
        foreach (var st in stats)
            foreach (var d in st.missingPrefabPaths)
                sb.AppendLine(st.path + "\t" + d);

        sb.AppendLine();
        sb.AppendLine("## DETAIL prefabAssetMissingScripts (count\tpath)");
        foreach (var d in prefabMissingList) sb.AppendLine(d);

        sb.AppendLine();
        sb.AppendLine("## DETAIL sceneErrors");
        foreach (var st in stats)
            foreach (var e in st.errorLines)
                sb.AppendLine(st.path + "\t" + e.Replace("\n", " ").Replace("\r", " "));

        File.WriteAllText(outName, sb.ToString());
        Debug.Log($"ProjectDiagnostics: wrote {outName} | missingScripts={stats.Sum(x => x.missingScripts)} missingPrefabs={stats.Sum(x => x.missingPrefabs)} sceneErrors={stats.Sum(x => x.errors)} prefabAssetMissing={prefabAssetMissing}");
    }

    static void Walk(GameObject go, SceneStat st, string path)
    {
        st.gameObjects++;

        var comps = go.GetComponents<Component>();
        for (int i = 0; i < comps.Length; i++)
        {
            if (comps[i] == null)
            {
                st.missingScripts++;
                if (st.missingScriptPaths.Count < 60) st.missingScriptPaths.Add(path);
            }
        }

        var status = PrefabUtility.GetPrefabInstanceStatus(go);
        if (status == PrefabInstanceStatus.MissingAsset)
        {
            st.missingPrefabs++;
            if (st.missingPrefabPaths.Count < 60) st.missingPrefabPaths.Add(path);
        }

        foreach (Transform c in go.transform)
            Walk(c.gameObject, st, path + "/" + c.name);
    }

    static int CountMissingInHierarchy(GameObject go)
    {
        int n = 0;
        var comps = go.GetComponents<Component>();
        for (int i = 0; i < comps.Length; i++) if (comps[i] == null) n++;
        foreach (Transform c in go.transform) n += CountMissingInHierarchy(c.gameObject);
        return n;
    }
}
