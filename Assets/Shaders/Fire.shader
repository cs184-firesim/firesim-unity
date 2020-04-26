Shader "Custom/Fire"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {} // Source frame buffer
        _Noise("Noise", 3D) = "white" {} // Noise texture
        _Velocity("Velocity", 3D) = "white" {} // Velocity texture
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

            sampler2D _MainTex;
            sampler3D _Noise;
            sampler3D _Velocity;
            float4 _MainTex_ST; // x,y contains texture scale, and z,w contains translation
            float3 boundsMin;
            float3 boundsMax;

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
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                float3 origin = _WorldSpaceCameraPos;
                float3 dir = normalize(i.viewVector);

                float2 hit = rayBoxDst(boundsMin, boundsMax, origin, dir);
                if (hit.y <= 0) { // Didn't hit
                    return col;
                }
                return tex3D(_Velocity, float3(i.uv, 0));
            }

            ENDCG
        }
    }
    FallBack "Diffuse"
}
