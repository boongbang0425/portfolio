// paaalop의 Custom/WoodTriplanar(Assets/Art/Materials/GameAssets/URPLocalTriplanar.shader) 호환 사본.
//
// 원본은 추가 광원을 per-pixel Forward 키워드(_ADDITIONAL_LIGHTS)로만 지원해서,
// Forward+ 렌더러(PC_Renderer, m_RenderingMode: 2)에서는 _CLUSTER_LIGHT_LOOP만 켜지고
// _ADDITIONAL_LIGHTS는 꺼지므로 추가 광원 기여가 전부 사라진다. Play 씬은 주광 intensity 0 +
// 거의 검은 Flat 앰비언트 구성이라 결과가 순수 검정 실루엣이 된다. Forward 렌더러 프리셋들도
// 추가 광원이 Per Vertex 모드(_ADDITIONAL_LIGHTS_VERTEX)라 같은 이유로 검게 나온다.
//
// 이 사본은 라이팅 계산은 그대로 두고 다음만 보강한다.
//   1. _CLUSTER_LIGHT_LOOP(Forward+/Cluster) 배리언트 추가
//   2. _ADDITIONAL_LIGHTS_VERTEX(정점 추가 광원) 배리언트 + vertexLighting 전달 추가
//
// 원본 셰이더는 paaalop 소유라 수정하지 않는다. 원본이 바뀌면 이 파일도 따라 갱신해야 한다.
// 머티리얼 연결은 PlayWorkshopBuilder.FixForwardPlusIncompatibleMaterials가 이식 시점에
// 원본 머티리얼의 프로퍼티를 복사한 *_Compat.mat을 만들어 수행한다.
Shader "Custom/WoodTriplanarCompat"
{
    Properties
    {
        [Header(Textures)]
        _MainTexX ("Texture X (Right/Left)", 2D) = "white" {}
        _MainTexY ("Texture Y (Top/Bottom)", 2D) = "white" {}
        _MainTexZ ("Texture Z (Front/Back)", 2D) = "white" {}

        [Header(Normal Map)]
        [Normal] _NormalMapX ("Normal Map X", 2D) = "bump" {}
        [Normal] _NormalMapY ("Normal Map Y", 2D) = "bump" {}
        [Normal] _NormalMapZ ("Normal Map Z", 2D) = "bump" {}
        _NormalScale ("Normal Scale", Float) = 1.0

        [Header(Transform)]
        _TextureScale ("Texture Scale (Tiling)", Float) = 1.0
        _Offset ("Texture Offset (X, Y)", Vector) = (0, 0, 0, 0)
        _AngleX ("Rotation X", Range(0, 360)) = 0.0
        _AngleY ("Rotation Y", Range(0, 360)) = 0.0
        _AngleZ ("Rotation Z", Range(0, 360)) = 0.0

        [Header(Blending)]
        _BlendSharpness ("Blend Sharpness", Range(1, 10)) = 3.0

        [Header(Surface Properties)]
        _Smoothness ("Smoothness", Range(0, 1)) = 0.1
        _Metallic ("Metallic", Range(0, 1)) = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            // [호환 보강] 원본의 `multi_compile_fragment _ _ADDITIONAL_LIGHTS`를
            // 정점 광원 배리언트를 포함한 표준 3종으로 바꾸고 Forward+(Cluster) 키워드를 더했다.
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD3;
                float3 tangentWS : TEXCOORD4;
                float3 bitangentWS : TEXCOORD5;
                // [호환 보강] _ADDITIONAL_LIGHTS_VERTEX 모드의 정점 광원 결과 전달용.
                half3 vertexLighting : TEXCOORD6;
            };

            TEXTURE2D(_MainTexX);
            SAMPLER(sampler_MainTexX);
            TEXTURE2D(_MainTexY);
            SAMPLER(sampler_MainTexY);
            TEXTURE2D(_MainTexZ);
            SAMPLER(sampler_MainTexZ);

            TEXTURE2D(_NormalMapX);
            SAMPLER(sampler_NormalMapX);
            TEXTURE2D(_NormalMapY);
            SAMPLER(sampler_NormalMapY);
            TEXTURE2D(_NormalMapZ);
            SAMPLER(sampler_NormalMapZ);

            CBUFFER_START(UnityPerMaterial)
                float _TextureScale;
                float4 _Offset;
                float _BlendSharpness;
                float _Smoothness;
                float _Metallic;
                float _AngleX;
                float _AngleY;
                float _AngleZ;
                float _NormalScale;
            CBUFFER_END

            float2 RotateUV(float2 uv, float angleDegrees)
            {
                float angleRad = angleDegrees * (3.14159265359 / 180.0);
                float c = cos(angleRad);
                float s = sin(angleRad);
                float2 rotatedUV;
                rotatedUV.x = uv.x * c - uv.y * s;
                rotatedUV.y = uv.x * s + uv.y * c;
                return rotatedUV;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);

                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.normalWS = normalInput.normalWS;
                output.tangentWS = normalInput.tangentWS;
                output.bitangentWS = normalInput.bitangentWS;

                // [호환 보강] Per Vertex 추가 광원 모드에서만 실제 값이 계산된다(그 외엔 0).
                output.vertexLighting = VertexLighting(output.positionWS, output.normalWS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. 오브젝트 로컬 공간 기준 좌표 (스케일 보정 추가)
                // 오브젝트의 월드 스케일을 계산하여 로컬 좌표에 곱해줍니다.
                // 이렇게 하면 오브젝트를 늘려도 텍스처는 늘어나지 않고 절대적인 크기를 유지합니다.
                float3 objScale = float3(
                    length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x)),
                    length(float3(UNITY_MATRIX_M[0].y, UNITY_MATRIX_M[1].y, UNITY_MATRIX_M[2].y)),
                    length(float3(UNITY_MATRIX_M[0].z, UNITY_MATRIX_M[1].z, UNITY_MATRIX_M[2].z))
                );
                float3 pos = (input.positionOS * objScale * _TextureScale) + _Offset.xyz;
                float3 normOS = normalize(input.positionOS); // Use local position as normal for Triplanar weights if you want local
                float3 normalWS = normalize(input.normalWS);

                // 2. 축별 가중치 계산 (월드 또는 로컬 노멀 기준)
                // 로컬 노멀을 가져오기 힘들 경우 월드 노멀을 오브젝트 스페이스로 변환
                float3 normalOS = TransformWorldToObjectNormal(normalWS);
                float3 blendWeights = pow(abs(normalOS), _BlendSharpness);
                blendWeights /= (blendWeights.x + blendWeights.y + blendWeights.z + 1e-6);

                // 3. 투사 UV 계산
                float2 uvX = pos.zy; // X축 투사
                float2 uvZ = pos.xy; // Z축 투사
                float2 uvY = pos.xz; // Y축 투사

                // UV 회전 적용
                uvX = RotateUV(uvX, _AngleX);
                uvY = RotateUV(uvY, _AngleY);
                uvZ = RotateUV(uvZ, _AngleZ);

                // 4. 텍스처 샘플링 (Albedo)
                half4 colX = SAMPLE_TEXTURE2D(_MainTexX, sampler_MainTexX, uvX);
                half4 colZ = SAMPLE_TEXTURE2D(_MainTexZ, sampler_MainTexZ, uvZ);
                half4 colY = SAMPLE_TEXTURE2D(_MainTexY, sampler_MainTexY, uvY);
                half4 finalAlbedo = colX * blendWeights.x + colZ * blendWeights.z + colY * blendWeights.y;

                // 5. 노멀맵 샘플링
                half4 normTexX = SAMPLE_TEXTURE2D(_NormalMapX, sampler_NormalMapX, uvX);
                half4 normTexZ = SAMPLE_TEXTURE2D(_NormalMapZ, sampler_NormalMapZ, uvZ);
                half4 normTexY = SAMPLE_TEXTURE2D(_NormalMapY, sampler_NormalMapY, uvY);

                half3 tnormX = UnpackNormalScale(normTexX, _NormalScale);
                half3 tnormZ = UnpackNormalScale(normTexZ, _NormalScale);
                half3 tnormY = UnpackNormalScale(normTexY, _NormalScale);

                // Triplanar Normal Blending (World Space)
                // X, Y, Z 축에 맞게 Tangent Space 노멀을 월드 축으로 변환
                half3 worldNormX = half3(tnormX.xy + normalWS.zy, abs(tnormX.z) * normalWS.x);
                half3 worldNormY = half3(tnormY.xy + normalWS.xz, abs(tnormY.z) * normalWS.y);
                half3 worldNormZ = half3(tnormZ.xy + normalWS.xy, abs(tnormZ.z) * normalWS.z);

                half3 finalNormalWS = normalize(
                    worldNormX.zyx * blendWeights.x +
                    worldNormY.xzy * blendWeights.y +
                    worldNormZ.xyz * blendWeights.z
                );

                // 6. URP 라이팅 계산 (InputData, SurfaceData 세팅)
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = finalNormalWS;
                inputData.viewDirectionWS = normalize(GetCameraPositionWS() - input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);

                // URP 필수 조명 데이터 세팅 (이 중 하나라도 빠지면 까맣게 나올 수 있음)
                inputData.bakedGI = SampleSH(finalNormalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);
                // [호환 보강] 정점 추가 광원 기여 전달.
                inputData.vertexLighting = input.vertexLighting;

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = finalAlbedo.rgb;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.alpha = 1.0;
                // occlusion 0이면 주변광이 전부 차단되므로 1로 고정한다(원본의 수정 유지).
                surfaceData.occlusion = 1.0;

                half4 finalColor = UniversalFragmentPBR(inputData, surfaceData);
                return finalColor;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
