using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 이동표 CSV 를 읽어 AssetDatabase.MoveAsset 으로만 이동한다.
/// 탐색기/셸 이동을 쓰지 않으므로 GUID와 참조가 보존된다.
/// 배치모드: -executeMethod AssetMover.Run -moveTable &lt;csv경로&gt; -moveLog &lt;출력경로&gt;
///
/// CSV 열: order,source,destination,note
///   destination 이 비어 있으면 source 폴더를 만든다(MKDIR).
///   destination 이 "DELETE-IF-EMPTY" 이면 source 폴더가 비었을 때만 지운다.
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
        int ok = 0, skipped = 0, failed = 0, made = 0, removed = 0;

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

        // 행 단위로 순차 처리한다.
        // (StartAssetEditing 배치로 묶거나 목적지 폴더를 한꺼번에 미리 만들면,
        //  뒤 행의 폴더 이름 변경과 충돌하거나 "@Art 1" 같은 중복 폴더가 생긴다.)
        foreach (var r in rows)
        {
            try
            {
                if (string.IsNullOrEmpty(r.dst))
                {
                    if (EnsureFolder(r.src, sb)) { made++; sb.AppendLine($"MKDIR\t{r.src}"); }
                    else sb.AppendLine($"MKDIR-EXISTS\t{r.src}");
                    continue;
                }

                if (r.dst == "DELETE-IF-EMPTY")
                {
                    if (!AssetDatabase.IsValidFolder(r.src)) { sb.AppendLine($"SKIP-NOFOLDER\t{r.src}"); skipped++; continue; }
                    var left = AssetDatabase.FindAssets("", new[] { r.src })
                                            .Select(AssetDatabase.GUIDToAssetPath)
                                            .Where(p => !AssetDatabase.IsValidFolder(p)).ToList();
                    if (left.Count > 0) { sb.AppendLine($"SKIP-NOTEMPTY\t{r.src}\t{left.Count} assets"); skipped++; continue; }
                    if (AssetDatabase.DeleteAsset(r.src)) { removed++; sb.AppendLine($"DELETED-EMPTY\t{r.src}"); }
                    else { failed++; sb.AppendLine($"FAIL-DELETE\t{r.src}"); }
                    continue;
                }

                bool srcExists = AssetDatabase.IsValidFolder(r.src) || File.Exists(r.src);
                if (!srcExists) { skipped++; sb.AppendLine($"SKIP-NOSRC\t{r.src}\t{r.dst}"); continue; }
                if (AssetDatabase.IsValidFolder(r.dst) || File.Exists(r.dst) || Directory.Exists(r.dst))
                {
                    skipped++; sb.AppendLine($"SKIP-DSTEXISTS\t{r.src}\t{r.dst}"); continue;
                }

                var parent = Path.GetDirectoryName(r.dst).Replace('\\', '/');
                if (EnsureFolder(parent, sb)) { made++; sb.AppendLine($"MKDIR-PARENT\t{parent}"); }

                var err = AssetDatabase.ValidateMoveAsset(r.src, r.dst);
                if (!string.IsNullOrEmpty(err)) { failed++; sb.AppendLine($"FAIL-VALIDATE\t{r.src}\t{r.dst}\t{err}"); continue; }

                var res = AssetDatabase.MoveAsset(r.src, r.dst);
                if (string.IsNullOrEmpty(res)) { ok++; sb.AppendLine($"MOVED\t{r.src}\t{r.dst}"); }
                else { failed++; sb.AppendLine($"FAIL-MOVE\t{r.src}\t{r.dst}\t{res}"); }
            }
            catch (Exception e)
            {
                failed++;
                sb.AppendLine($"EXCEPTION\t{r.src}\t{r.dst}\t{e.Message}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

        // 빌드 씬 목록을 GUID 기준으로 다시 맞춘다 (이동된 씬의 path 갱신).
        var scenes = EditorBuildSettings.scenes;
        int fixedScenes = 0;
        var updated = scenes.Select(s =>
        {
            var guidPath = AssetDatabase.GUIDToAssetPath(s.guid);
            if (!string.IsNullOrEmpty(guidPath) && guidPath != s.path)
            {
                sb.AppendLine($"BUILDSCENE\t{s.path}\t{guidPath}");
                fixedScenes++;
                return new EditorBuildSettingsScene(guidPath, s.enabled);
            }
            return s;
        }).ToArray();
        if (fixedScenes > 0) EditorBuildSettings.scenes = updated;
        AssetDatabase.SaveAssets();

        var header = $"moved={ok} skipped={skipped} failed={failed} foldersCreated={made} emptyFoldersDeleted={removed} buildScenesUpdated={fixedScenes}";
        File.WriteAllText(logOut, header + Environment.NewLine + sb);
        Debug.Log("AssetMover: " + header);
        if (failed > 0) Debug.LogError("AssetMover: 실패 " + failed + "건 — " + logOut + " 확인");
    }

    static bool EnsureFolder(string folder, StringBuilder sb)
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
                if (Directory.Exists(next))
                {
                    // 디스크에는 있는데 DB 에 없으면 CreateFolder 가 "이름 1" 을 만들어 버린다. 먼저 임포트한다.
                    AssetDatabase.ImportAsset(next, ImportAssetOptions.ForceSynchronousImport);
                    if (!AssetDatabase.IsValidFolder(next))
                        throw new InvalidOperationException("폴더가 디스크에는 있으나 AssetDatabase 에 등록되지 않음: " + next);
                }
                else
                {
                    var guid = AssetDatabase.CreateFolder(cur, parts[i]);
                    var actual = AssetDatabase.GUIDToAssetPath(guid);
                    if (actual != next)
                        throw new InvalidOperationException($"CreateFolder 가 예상과 다른 경로를 만듦: {actual} (기대 {next})");
                    created = true;
                }
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
