Shader "Custom/BoidProcedural"
{
    Properties
    {
        _Color ("Material Base Color", Color) = (1,1,1,1)
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct BoidGPUData
            {
                float3 position;
                float3 velocity;
                float4 color;
                float scale;
                int isPredator;
            };

            #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                StructuredBuffer<BoidGPUData> BoidBuffer;
            #endif

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
                uint instanceID     : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 normalWS     : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                float4 color        : COLOR;
            };

            float4 _Color;

            void setup()
            {
                // Instancing setup (needed for Graphics.DrawMeshInstancedProcedural to activate procedural path)
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                float3 posOS = input.positionOS.xyz;
                float3 normOS = input.normalOS;
                float4 boidColor = _Color;

                #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                    BoidGPUData boid = BoidBuffer[input.instanceID];
                    float3 posWS = boid.position;
                    float3 vel = boid.velocity;
                    float scale = boid.scale;
                    boidColor = boid.color;

                    float3 forward = length(vel) > 0.001 ? normalize(vel) : float3(0, 0, 1);
                    float3 up = float3(0, 1, 0);
                    if (abs(dot(forward, up)) > 0.999)
                    {
                        up = float3(1, 0, 0);
                    }
                    float3 right = normalize(cross(up, forward));
                    up = cross(forward, right);

                    // Transform position OS to WS
                    float3 transformedPos = (right * posOS.x * scale) + (up * posOS.y * scale) + (forward * posOS.z * scale) + posWS;
                    output.positionWS = transformedPos;
                    output.positionCS = TransformWorldToHClip(transformedPos);
                    
                    // Transform normal
                    output.normalWS = mul(float3x3(right, up, forward), normOS);
                #else
                    output.positionWS = TransformObjectToWorld(posOS);
                    output.positionCS = TransformObjectToHClip(posOS);
                    output.normalWS = TransformObjectToWorldNormal(normOS);
                #endif

                output.normalWS = normalize(output.normalWS);
                output.color = boidColor;

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // Get main light
                Light mainLight = GetMainLight();
                
                // Calculate shadows
                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                    float shadowAtten = MainLightRealtimeShadow(shadowCoord);
                #else
                    float shadowAtten = 1.0;
                #endif

                float3 normalWS = normalize(input.normalWS);
                float3 lightDir = normalize(mainLight.direction);
                
                // Diffuse lighting
                float NdotL = saturate(dot(normalWS, lightDir));
                float3 diffuse = NdotL * shadowAtten * mainLight.color;
                
                // Ambient lighting
                float3 ambient = float3(0.25, 0.25, 0.3) * (normalWS.y * 0.3 + 0.7); // soft hemisphere ambient
                
                float3 finalColor = input.color.rgb * (diffuse + ambient);
                
                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct BoidGPUData
            {
                float3 position;
                float3 velocity;
                float4 color;
                float scale;
                int isPredator;
            };

            #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                StructuredBuffer<BoidGPUData> BoidBuffer;
            #endif

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                uint instanceID     : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
            };

            void setup()
            {
            }

            float3 ApplyShadowBias(float3 positionWS, float3 normalWS, float3 lightDirection)
            {
                // URP specific shadow bias
                return positionWS;
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 posOS = input.positionOS.xyz;

                #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                    BoidGPUData boid = BoidBuffer[input.instanceID];
                    float3 posWS = boid.position;
                    float3 vel = boid.velocity;
                    float scale = boid.scale;

                    float3 forward = length(vel) > 0.001 ? normalize(vel) : float3(0, 0, 1);
                    float3 up = float3(0, 1, 0);
                    if (abs(dot(forward, up)) > 0.999)
                    {
                        up = float3(1, 0, 0);
                    }
                    float3 right = normalize(cross(up, forward));
                    up = cross(forward, right);

                    float3 transformedPos = (right * posOS.x * scale) + (up * posOS.y * scale) + (forward * posOS.z * scale) + posWS;
                    
                    // Simple bias approximation or direct projection
                    float3 normalWS = mul(float3x3(right, up, forward), input.normalOS);
                    normalWS = normalize(normalWS);
                    
                    // We project to light space
                    output.positionCS = TransformWorldToHClip(transformedPos);
                #else
                    float3 posWS = TransformObjectToWorld(posOS);
                    output.positionCS = TransformWorldToHClip(posWS);
                #endif

                return output;
            }

            float4 frag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
