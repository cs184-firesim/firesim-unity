Shader "Custom/Fire"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {} // Source frame buffer
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 viewVector : TEXCOORD1;
            };

            // Textures
            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;
            Texture3D<float4> Noise;
            SamplerState samplerNoise;
            Texture3D<float4> Velocity;
            SamplerState samplerVelocity;
            Texture3D<float> Density;
            SamplerState samplerDensity;
            Texture3D<float> Debug;
            SamplerState samplerDebug;
            Texture3D<float> Temperature;
            SamplerState samplerTemperature;
            Texture3D<float> Fuel;
            SamplerState samplerFuel;
            float4 _MainTex_ST; // x,y contains texture scale, and z,w contains translation
            // Container
            float3 boundsMin;
            float3 boundsMax;
            // Ray marching
            int marchSteps;

            // Ray box dst
            // https://github.com/SebLague/Clouds/blob/44e81a483504817e859d8e1b654a952f8a978a1a/Assets/Scripts/Clouds/Shaders/Clouds.shader
            // Returns (dstToBox, dstInsideBox). If ray misses box, dstInsideBox will be zero
            float2 rayBoxDst(float3 boundsMin, float3 boundsMax, float3 rayOrigin, float3 invRaydir) {
                // Adapted from: http://jcgt.org/published/0007/03/04/
                float3 t0 = (boundsMin - rayOrigin) / invRaydir;
                float3 t1 = (boundsMax - rayOrigin) / invRaydir;
                float3 tmin = min(t0, t1);
                float3 tmax = max(t0, t1);
                
                float dstA = max(max(tmin.x, tmin.y), tmin.z);
                float dstB = min(tmax.x, min(tmax.y, tmax.z));

                // CASE 1: ray intersects box from outside (0 <= dstA <= dstB)
                // dstA is dst to nearest intersection, dstB dst to far intersection

                // CASE 2: ray intersects box from inside (dstA < 0 < dstB)
                // dstA is the dst to intersection behind the ray, dstB is dst to forward intersection

                // CASE 3: ray misses box (dstA > dstB)

                float dstToBox = max(0, dstA);
                float dstInsideBox = max(0, dstB - dstToBox);
                return float2(dstToBox, dstInsideBox);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                // Camera space matches OpenGL convention where cam forward is -z. In unity forward is positive z.
                // (https://docs.unity3d.com/ScriptReference/Camera-cameraToWorldMatrix.html)
                float3 viewVector = mul(unity_CameraInvProjection, float4(v.uv * 2 - 1, 0, -1));
                o.viewVector = mul(unity_CameraToWorld, float4(viewVector,0));
                // I don't really know why the version below does not work
                // o.viewVector = float3(WorldSpaceViewDir(v.vertex).x,WorldSpaceViewDir(v.vertex).y,-WorldSpaceViewDir(v.vertex).z);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target {
                // Sample the source frame buffer
                fixed4 col = tex2D(_MainTex, i.uv);
                // Calculate depth
                float depth_non_linear = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                float depth_linear = LinearEyeDepth(depth_non_linear) * length(i.viewVector);
                // Generate ray
                float3 origin = _WorldSpaceCameraPos;
                float3 dir = normalize(i.viewVector);
                // Test intersection
                float2 hit = rayBoxDst(boundsMin, boundsMax, origin, dir);

                // Didn't hit
                // Also this is a very crude solution for solving z-fighting
                if (hit.y <= 0 || hit.x > depth_linear + 0.001) {
                    return col;
                }

                // Hit
                // Sample velocity

                float dstTravelled = 0;
                float stepSize = hit.y / marchSteps; //(hit.y - hit.x) / marchSteps;
                float totalDensity = 0;
                float totalDebug = 0;
                float totalFuel = 0;
                float distLimit = min(depth_linear - hit.x, hit.y);
                float totalVelocity = 0;
                while (dstTravelled < distLimit) {
                    float3 rayPos = origin + dir * (dstTravelled + hit.x);
                    totalDensity += max(Density.SampleLevel(samplerDensity, rayPos, 0), 0);
                    totalFuel += max(Fuel.SampleLevel(samplerFuel, rayPos, 0), 0);
                    totalDebug += max(Debug.SampleLevel(samplerDebug, rayPos, 0), 0);
                    totalVelocity += max(length(Velocity.SampleLevel(samplerVelocity, rayPos, 0)), 0);
                    dstTravelled += stepSize;
                }
                float transmittance = exp(-totalDensity);
                col = transmittance * col;
                // float red = exp(-totalVelocity);
                // col.r = red;
                //if (totalDebug > 0.01)
                //    col = float4(1, 1, 1, 0);
                // return Velocity.SampleLevel(samplerVelocity, float3(i.uv, 5), 0);
                float strength = 1 - exp(-totalFuel*0.5);
                return col + float4(1.0f, .72f, .15f, 0) * strength;
            }

            ENDCG
        }
    }
    FallBack "Diffuse"
}
