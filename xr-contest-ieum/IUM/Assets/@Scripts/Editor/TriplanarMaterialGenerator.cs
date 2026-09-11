using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class TriplanarMaterialGenerator : EditorWindow
{
    private string materialName = "Triplanar_MultiFace_Material";
    
    // 세 면 텍스쳐로 확장
    private Texture2D albedoTextureX;
    private Texture2D albedoTextureY;
    private Texture2D albedoTextureZ;
    private Texture2D normalTexture;

    private float textureScale = 0.3f;
    private float blendSharpness = 5.0f;
    private float smoothness = 0.15f;
    private float metallic = 0.0f;

    private bool applyToSelected = true;

    [MenuItem("Tools/Triplanar Material Generator")]
    public static void ShowWindow()
    {
        GetWindow<TriplanarMaterialGenerator>("Triplanar Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("트라이플래너 3면 독립 매핑 생성기", EditorStyles.boldLabel);
        GUILayout.Space(10);

        // 1. 머티리얼 설정
        GUILayout.Label("1. 머티리얼 이름 및 각 면 텍스쳐 설정", EditorStyles.boldLabel);
        materialName = EditorGUILayout.TextField("머티리얼 이름", materialName);
        
        GUILayout.Space(5);
        albedoTextureX = (Texture2D)EditorGUILayout.ObjectField("X축 텍스쳐 (옆면 X)", albedoTextureX, typeof(Texture2D), false);
        albedoTextureY = (Texture2D)EditorGUILayout.ObjectField("Y축 텍스쳐 (윗면/아랫면 - 나이테)", albedoTextureY, typeof(Texture2D), false);
        albedoTextureZ = (Texture2D)EditorGUILayout.ObjectField("Z축 텍스쳐 (앞면/뒷면 Z)", albedoTextureZ, typeof(Texture2D), false);
        
        EditorGUILayout.HelpBox("꿀팁 💡: Y축이나 Z축 텍스쳐를 비워두시면, X축 텍스쳐가 빈 면들에 자동으로 공통 적용됩니다.", MessageType.Info);
        
        GUILayout.Space(5);
        normalTexture = (Texture2D)EditorGUILayout.ObjectField("공용 노멀 맵 (선택)", normalTexture, typeof(Texture2D), false);

        GUILayout.Space(10);

        // 2. 조절 값
        GUILayout.Label("2. 세부 조절", EditorStyles.boldLabel);
        textureScale = EditorGUILayout.Slider("텍스쳐 크기 (Scale)", textureScale, 0.01f, 50.0f);
        blendSharpness = EditorGUILayout.Slider("블렌딩 부드러움 (Sharpness)", blendSharpness, 1.0f, 20.0f);
        smoothness = EditorGUILayout.Slider("매끄러움 (Smoothness)", smoothness, 0.0f, 1.0f);
        metallic = EditorGUILayout.Slider("금속성 (Metallic)", metallic, 0.0f, 1.0f);

        GUILayout.Space(15);

        // 3. 편의 기능
        applyToSelected = EditorGUILayout.Toggle("생성 시 선택한 가구에 바로 적용", applyToSelected);

        GUILayout.Space(15);

        // 4. 실행 버튼
        if (GUILayout.Button("각 면 독립 트라이플래너 머티리얼 생성 및 저장", GUILayout.Height(30)))
        {
            CreateAndSaveMaterial();
        }

        GUILayout.Space(25);

        // 5. 공유용 패키지 추출 기능
        GUILayout.Label("3. 다른 개발자 공유용 패키징 (Library Export)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("아래 버튼을 누르면 이 커스텀 3면 셰이더와 현재 생성한 머티리얼 및 텍스쳐 세트를 유니티 패키지로 묶어 내보냅니다.", MessageType.Info);
        
        if (GUILayout.Button("공유용 유니티 패키지 즉시 추출 (Export)", GUILayout.Height(30)))
        {
            ExportTriplanarPackage();
        }
    }

    private void CreateAndSaveMaterial()
    {
        if (albedoTextureX == null)
        {
            EditorUtility.DisplayDialog("알림", "최소한 X축 텍스쳐(옆면 X)는 필수로 지정해 주셔야 합니다!", "확인");
            return;
        }

        Shader triplanarShader = Shader.Find("Custom/URPLocalTriplanar");
        if (triplanarShader == null)
        {
            EditorUtility.DisplayDialog("오류", "Custom/URPLocalTriplanar 셰이더를 찾을 수 없습니다. 셰이더 파일이 프로젝트에 존재하는지 확인해 주세요.", "확인");
            return;
        }

        // 비어있는 Y축/Z축 텍스쳐는 X축 텍스쳐로 자동 대체
        Texture2D finalTexY = (albedoTextureY != null) ? albedoTextureY : albedoTextureX;
        Texture2D finalTexZ = (albedoTextureZ != null) ? albedoTextureZ : albedoTextureX;

        Material newMat = new Material(triplanarShader);
        
        // 각 면 텍스쳐 설정 주입
        newMat.SetTexture("_MainTexX", albedoTextureX);
        newMat.SetTexture("_MainTexY", finalTexY);
        newMat.SetTexture("_MainTexZ", finalTexZ);

        if (normalTexture != null)
        {
            newMat.SetTexture("_NormalMap", normalTexture);
        }
        newMat.SetFloat("_TextureScale", textureScale);
        newMat.SetFloat("_BlendSharpness", blendSharpness);
        newMat.SetFloat("_Smoothness", smoothness);
        newMat.SetFloat("_Metallic", metallic);

        // 3. 사용자가 직접 프로젝트 내부에서 저장 위치와 이름을 정하도록 탐색기 창 띄우기
        string savePath = EditorUtility.SaveFilePanelInProject(
            "트라이플래너 머티리얼 저장 위치 지정",
            materialName,
            "mat",
            "머티리얼을 저장할 프로젝트 폴더와 이름을 결정해 주세요."
        );

        if (string.IsNullOrEmpty(savePath))
        {
            Debug.Log("머티리얼 생성이 취소되었습니다.");
            return;
        }

        // 사용자가 수정한 파일명으로 머티리얼 이름 동기화
        materialName = Path.GetFileNameWithoutExtension(savePath);

        AssetDatabase.CreateAsset(newMat, savePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[3면 머티리얼 라이브러리화 완료] {savePath}", newMat);

        if (applyToSelected)
        {
            GameObject selectedGo = Selection.activeGameObject;
            if (selectedGo != null)
            {
                Renderer renderer = selectedGo.GetComponent<Renderer>();
                if (renderer == null)
                {
                    renderer = selectedGo.GetComponentInChildren<Renderer>();
                }

                if (renderer != null)
                {
                    Undo.RecordObject(renderer, "Apply Triplanar Material");
                    renderer.material = newMat;
                    Debug.Log($"선택한 오브젝트({selectedGo.name})에 머티리얼을 자동 할당했습니다.");
                }
            }
        }
    }

    private void ExportTriplanarPackage()
    {
        List<string> assetPaths = new List<string>();

        string[] shaderGuids = AssetDatabase.FindAssets("URPLocalTriplanar t:Shader");
        if (shaderGuids.Length > 0)
        {
            string shaderPath = AssetDatabase.GUIDToAssetPath(shaderGuids[0]);
            assetPaths.Add(shaderPath);
        }
        else
        {
            EditorUtility.DisplayDialog("오류", "URPLocalTriplanar 셰이더 원본 파일을 찾지 못해 패키지를 생성할 수 없습니다.", "확인");
            return;
        }

        // 에디터 툴 스크립트 자체도 패키지에 함께 묶어서 전송 (중요!)
        string[] editorGuids = AssetDatabase.FindAssets("TriplanarMaterialGenerator t:Script");
        if (editorGuids.Length > 0)
        {
            string editorPath = AssetDatabase.GUIDToAssetPath(editorGuids[0]);
            assetPaths.Add(editorPath);
        }

        string folderPath = "Assets/@Art/Environment/Workshop/Materials";
        string targetMatPath = $"{folderPath}/{materialName}.mat";
        
        if (File.Exists(targetMatPath))
        {
            assetPaths.Add(targetMatPath);
        }
        else
        {
            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { folderPath });
            foreach (var guid in matGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("Triplanar"))
                {
                    assetPaths.Add(path);
                }
            }
        }

        if (assetPaths.Count == 0)
        {
            EditorUtility.DisplayDialog("알림", "패키지에 묶을 머티리얼을 먼저 [생성 및 저장] 해주세요!", "확인");
            return;
        }

        string exportDir = "upload";
        if (!Directory.Exists(exportDir))
        {
            Directory.CreateDirectory(exportDir);
        }

        string exportPath = $"{exportDir}/TriplanarLibrary.unitypackage";

        AssetDatabase.ExportPackage(
            assetPaths.ToArray(), 
            exportPath, 
            ExportPackageOptions.Interactive | ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies
        );

        Debug.Log($"[3면 패키징 완료] 패키지 생성 완료: {exportPath}");
        EditorUtility.RevealInFinder(exportPath);
    }
}
