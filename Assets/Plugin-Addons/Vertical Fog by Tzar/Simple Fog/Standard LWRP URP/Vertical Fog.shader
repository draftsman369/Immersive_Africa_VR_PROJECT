Shader "Tzar/VerticalFog"
{
    Properties
    {
        _Color ("Main Color", Color) = (1, 1, 1, 0.5)
        _Intensity ("Intensity", Float) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO

            #include "UnityCG.cginc"

            // Depth texture declaration (VR-safe)
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos       : SV_POSITION;
                float4 screenPos : TEXCOORD0;   // for screen UV
                float  eyeDepth  : TEXCOORD1;   // our own eye-space depth
                UNITY_FOG_COORDS(2)

                UNITY_VERTEX_OUTPUT_STEREO
            };

            float4 _Color;
            float  _Intensity;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // Clip-space position
                o.pos = UnityObjectToClipPos(v.vertex);

                // Screen position for sampling depth
                o.screenPos = ComputeScreenPos(o.pos);

                // Eye-space depth (positive forward)
                float3 viewPos = UnityObjectToViewPos(v.vertex);
                o.eyeDepth = -viewPos.z;   // Unity's camera looks along -Z

                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // Get normalized screen UV
                float2 uv = i.screenPos.xy / i.screenPos.w;

                // Correct for single-pass stereo layout
                #if defined(UNITY_SINGLE_PASS_STEREO)
                    uv = UnityStereoTransformScreenSpaceTex(uv);
                #endif

                // Sample scene depth from depth texture (non-linear) and convert to linear eye depth
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv);
                float sceneEyeDepth = LinearEyeDepth(rawDepth);

                // Compare scene depth vs the fog plane depth
                float diff = saturate(_Intensity * (sceneEyeDepth - i.eyeDepth));

                // Smoothstep-style easing (same shape you had)
                float t = diff;
                t = t * t * t * (t * (6 * t - 15) + 10);

                fixed4 col = lerp(fixed4(_Color.rgb, 0.0), _Color, t);

                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }

            ENDCG
        }
    }
}