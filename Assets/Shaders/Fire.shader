﻿Shader "Custom/Fire"
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
            #pragma enable_d3d11_debug_symbols
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
            Texture3D<float> Ember;
            SamplerState samplerEmber;
            float4 _MainTex_ST; // x,y contains texture scale, and z,w contains translation
            // Container
            float3 boundsMin;
            float3 boundsMax;
            // Ray marching
            int marchSteps;
            // Light
            float3 lightDirection;
            float4 lightColor;
            // Colors
            float4 fireColor0;
            float4 fireColor1;

            float HenyeyGreenstein ( float3 inLightVector , float3 inViewVector, float inG )
            {
                float cosAngle = dot(normalize(inLightVector), normalize(inViewVector));
                return (1-inG*inG)/(4*3.1415)/pow(1 + inG*inG - 2*inG*cosAngle, 3.0/2.0);
            }

            // Ray box dst
            // https://github.com/SebLague/Clouds/blob/44e81a483504817e859d8e1b654a952f8a978a1a/Assets/Scripts/Clouds/Shaders/Clouds.shader
            // Returns (dstToBox, dstInsideBox). If ray misses box, dstInsideBox will be zero
            float2 rayBoxDst(float3 boundsMin, float3 boundsMax, float3 rayOrigin, float3 dir) {
                // Adapted from: http://jcgt.org/published/0007/03/04/
                float3 t0 = (boundsMin - rayOrigin) / dir;
                float3 t1 = (boundsMax - rayOrigin) / dir;
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

            float4 flameColor(float strength) {
                float4 yellow = float4(1.0f, .72f, .15f, 0);
                float4 blue = float4(.141f, .651f, .98f, 0);
                float4 red = float4(.71f, .17f, .11f, 0);
                float4 orangeRed = float4(1, .635f, .365f, 0) * 0.7;
                float4 darkYellow = yellow * 0.7;
                // float4 orange = 
                // Defualt
                // return yellow * strength;
                // Experimental
                if (strength < 0.1) {
                    return float4(0, 0, 0, 0);
                } else if (strength < 0.9) {
                    return fireColor0 * (strength - 0.1) / 0.9;
                } else if (strength < 0.95) {
                    return lerp(fireColor0, fireColor1, (strength - 0.9) * 20);
                } else {
                    return fireColor1 * (1 - (strength - 0.95) * 20);
                }
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
                float distToBox = hit.x;
                float distDelta = hit.y; // Delta t inside the box starting from distToBox
                // Didn't hit
                // Also this is a very crude solution for solving z-fighting
                if (distDelta <= 0 || distToBox > depth_linear + 0.001) {
                    return col;
                }

                // Hit
                float dstTravelled = 0;
                float stepSize = distDelta / marchSteps;
                float totalDensity = 0;
                float totalEnergy = 0; // Light contribution from the sun
                float totalFuel = 0;
                float totalDebug = 0;
                float totalEmber = 0;
                while (dstTravelled < distDelta) {
                    float3 rayPos = origin + dir * (dstTravelled + distToBox);
                    float3 rayPosObject = (rayPos - boundsMin) / (boundsMax - boundsMin);
                    float d = max(Density.SampleLevel(samplerDensity, rayPosObject, 0) * distDelta, 0);
                    totalDensity += d;
                    totalEnergy += 2.0 * exp(-d) * (1-exp(-2*d)) * HenyeyGreenstein(lightDirection, dir, 0.05);
                    totalFuel += max(Fuel.SampleLevel(samplerFuel, rayPosObject, 0) * distDelta, 0);
                    totalEmber += max(Ember.SampleLevel(samplerEmber, rayPosObject, 0).x * distDelta, 0);
                    totalDebug += max(Debug.SampleLevel(samplerDebug, rayPosObject, 0) * distDelta, 0);
                    dstTravelled += stepSize;
                }
                float transmittance = min(1, exp(-totalDensity) + 0);
                totalEnergy = exp(-totalEnergy);

                float4 smokeColor = float4(.5, .5, .5, 0);
                lightColor = float4(lightColor.rgb, 0);
                float lightColorContribution = 0.2;
                smokeColor = lerp(smokeColor, lightColor, lightColorContribution);
                // smokeColor = lightColor * lightColorContribution + smokeColor * (1-lightColorContribution);
                float strength = exp(-totalFuel*0.07);
                // return col*transmittance + (1-transmittance) * totalEnergy * smokeColor + strength * flameColor;
                if (strength < 0.95) { // without smoke
                    return col + flameColor(strength);
                }
                if (totalEmber > 0.5) {
                    return float4(1, .3686278, 0, 0) * totalEmber * 2.5 + col * 0.5;
                }
                return lerp(totalEnergy * smokeColor, col, transmittance) * strength + flameColor(strength) + float4(1, .3686278, 0, 0) * totalEmber * 0.3; // with smoke
            }

            ENDCG
        }
    }
    FallBack "Diffuse"
}
