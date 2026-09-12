using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 로컬 사용자 데이터를 다루는 에디터 도구. 지금은 저장 파일 삭제 하나뿐이다.
///
/// 진행 흐름을 처음부터 확인하려면 저장이 없는 상태가 필요한데, 저장 파일은
/// <c>Application.persistentDataPath</c> 아래에 있어 프로젝트 창에서 보이지 않는다. 탐색기에서
/// 경로를 찾아 지우는 일을 매번 반복하지 않도록 메뉴로 만들었다.
/// </summary>
static class UserDataTools
{
    /// <summary>
    /// <see cref="DataManager"/>가 저장 파일을 만드는 규칙과 같은 조합이다. 절대 경로를 박아 두면
    /// 회사명·제품명이 바뀌는 순간 엉뚱한 곳을 지우게 된다.
    /// </summary>
    const string UserDirectoryName = "UserData";

    const string UserFileName = "user.json";

    static string UserFilePath =>
        Path.Combine(Application.persistentDataPath, UserDirectoryName, UserFileName);

    [MenuItem("Tools/PROJECT 이음/저장 데이터 삭제")]
    static void DeleteUserData()
    {
        // 플레이 중에는 DataManager가 진행·옵션을 메모리에 들고 있고, 다음 저장 시점에 그 내용을
        // 그대로 다시 쓴다. 파일만 지우면 삭제된 것처럼 보이다가 곧 되살아나므로 아예 막는다.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "저장 데이터 삭제",
                "플레이 모드에서는 삭제할 수 없습니다.\n\n" +
                "DataManager가 메모리에 들고 있는 진행·옵션이 다음 저장 때 파일을 다시 쓰기 " +
                "때문입니다. 플레이를 멈춘 뒤 다시 실행하십시오.",
                "확인");
            return;
        }

        var path = UserFilePath;

        if (!File.Exists(path))
        {
            EditorUtility.DisplayDialog(
                "저장 데이터 삭제",
                $"저장 데이터가 없습니다.\n\n{path}",
                "확인");
            Debug.Log($"[UserData] 삭제할 저장 데이터가 없습니다: {path}");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "저장 데이터 삭제",
                $"아래 파일을 삭제합니다.\n\n{path}\n\n" +
                "진행과 옵션이 모두 초기화되며 되돌릴 수 없습니다.",
                "삭제", "취소"))
            return;

        try
        {
            // 같은 폴더의 다른 파일(임시 저장본 등)은 건드리지 않는다. 폴더째 지우면 이 도구가
            // 아는 것보다 넓은 범위를 없애게 된다.
            File.Delete(path);
            Debug.Log($"[UserData] 저장 데이터를 삭제했습니다: {path}");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[UserData] 저장 데이터를 삭제하지 못했습니다: {exception.Message}");
            EditorUtility.DisplayDialog(
                "저장 데이터 삭제",
                $"삭제하지 못했습니다.\n\n{exception.Message}",
                "확인");
        }
    }
}
