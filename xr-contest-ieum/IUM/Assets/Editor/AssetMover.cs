using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// docs/move_table.csv 를 읽어 AssetDatabase.MoveAsset 으로만 이동한다.
/// 탐색기/셸 이동을 쓰지 않으므로 GUID와 참조가 보존된다.
/// 배치모드: -executeMethod AssetMover.Run -moveTable &lt;csv경로&gt; -moveLog &lt;출력경로&gt;
/// </summary>
public static class AssetMover
{
    public static void Run()
    {
        string table = null, logOut = "MoveResult.txt";
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-moveTable") table = args[i + 1];
            if (args[i] == "-moveLog") logOut = args[i + 1];
        }
        if (table == null || !File.Exists(table))
        {
            Debug.LogError("AssetMover: -moveTable 경로가 없습니다: " + table);
            EditorApplication.Exit(2);
            return;
        }

        var sb = new StringBuilder();
        int ok = 0, skipped = 0, failed = 0, made = 0;

        var rows = new List<(int order, string src, string dst, string note)>();
        foreach (var raw in File.ReadAllLines(table, Encoding.UTF8).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var c = SplitCsv(raw);
            if (c.Count < 3) continue;
            if (!int.TryParse(c[0].Trim(), out var order)) continue;
            rows.Add((order, c[1].Trim(), c[2].Trim(), c.Count > 3 ? c[3] : ""));
        }
        rows = rows.OrderBy(r => r.order).ToList();

        // ── Phase 1: 필요한 폴더를 먼저 전부 만든다 ──
        // CreateFolder 는 StartAssetEditing 배치 안에서는 AssetDatabase 에 즉시 등록되지 않아
        // 뒤따르는 ValidateMoveAsset 이 "Parent directory is not in asset database" 로 실패한다.
        // 그래서 배치로 묶지 않고, 폴더 생성을 별도 단계로 분리한 뒤 Refresh 한다.
        foreach (var r in rows)
        {
            if (string.IsNullOrEmpty(r.dst))
            {
                if (EnsureFolder(r.src)) { made++; sb.AppendLine($"MKDIR\t{r.src}"); }
                else sb.AppendLine($"MKDIR-EXISTS\t{r.src}");
            }
            else
            {
                var parentDir = Path.GetDirectoryName(r.dst).Replace('\\', '/');
                if (EnsureFolder(parentDir)) { made++; sb.AppendLine($"MKDIR-PARENT\t{parentDir}"); }
            }
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

        // ── Phase 2: 이동 ──
        try
        {
            foreach (var r in rows)
            {
                if (string.IsNullOrEmpty(r.dst)) continue;   // MKDIR rows already handled

                bool srcIsFolder = AssetDatabase.IsValidFolder(r.src);
                bool srcExists = srcIsFolder || File.Exists(r.src);
                if (!srcExists)
                {
                    skipped++;
                    sb.AppendLine($"SKIP-NOSRC\t{r.src}\t{r.dst}");
                    continue;
                }
                if (AssetDatabase.IsValidFolder(r.dst) || File.Exists(r.dst))
                {
                    skipped++;
                    sb.AppendLine($"SKIP-DSTEXISTS\t{r.src}\t{r.dst}");
                    continue;
                }

                var err = AssetDatabase.ValidateMoveAsset(r.src, r.dst);
                if (!string.IsNullOrEmpty(err))
                {
                    failed++;
                    sb.AppendLine($"FAIL-VALIDATE\t{r.src}\t{r.dst}\t{err}");
                    continue;
                }
                var res = AssetDatabase.MoveAsset(r.src, r.dst);
                if (string.IsNullOrEmpty(res)) { ok++; sb.AppendLine($"MOVED\t{r.src}\t{r.dst}"); }
                else { failed++; sb.AppendLine($"FAIL-MOVE\t{r.src}\t{r.dst}\t{res}"); }
            }
        }
        finally
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }

        var header = $"moved={ok} skipped={skipped} failed={failed} foldersCreated={made}";
        File.WriteAllText(logOut, header + Environment.NewLine + sb);
        Debug.Log("AssetMover: " + header);
        if (failed > 0) Debug.LogError("AssetMover: 실패 " + failed + "건 — " + logOut + " 확인");
    }

    static bool EnsureFolder(string folder)
    {
        folder = folder.TrimEnd('/');
        if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return false;
        var parts = folder.Split('/');
        var cur = parts[0];               // "Assets"
        bool created = false;
        for (int i = 1; i < parts.Length; i++)
        {
            var next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(cur, parts[i]);
                created = true;
            }
            cur = next;
        }
        return created;
    }

    static List<string> SplitCsv(string line)
    {
        var outp = new List<string>();
        var sb = new StringBuilder();
        bool q = false;
        foreach (var ch in line)
        {
            if (ch == '"') { q = !q; continue; }
            if (ch == ',' && !q) { outp.Add(sb.ToString()); sb.Clear(); continue; }
            sb.Append(ch);
        }
        outp.Add(sb.ToString());
        return outp;
    }
}
