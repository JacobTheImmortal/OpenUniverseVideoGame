Shader "Custom/AtmosphereScattering"
{
    Properties
    {
        _Tint("Tint (RGB × A)", Color) = (0.3,0.55,1,0.5)

        _PlanetRadius("(set by script)", Float) = 100
        _AtmosphereRadius("(set by script)", Float) = 102

        _RayleighCoeff("Rayleigh coeff (RGB)", Vector) = (0.00058, 0.00135, 0.00331, 0)
        _MieCoeff("Mie coeff",  Float) = 0.0002
        _RayleighHeight("Ray H",      Float) = 4
        _MieHeight("Mie H",      Float) = 1
        _MieG("Mie g",      Float) = 0.76
    }

        SubShader
    {
        Tags { "RenderType" = "Transparent"  "Queue" = "Transparent" }
        ZWrite Off
        ZTest  LEqual
        Blend  SrcAlpha OneMinusSrcAlpha     // <-- standard alpha
        Cull   Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float3 vertex : POSITION; };
            struct v2f {
                float4 pos      : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            /* uniforms */
            float4 _Tint;
            float  _PlanetRadius, _AtmosphereRadius;
            float3 _RayleighCoeff;
            float  _MieCoeff, _RayleighHeight, _MieHeight, _MieG;
            float3 _SunDirWS;
            float  _SunIntensity;

            v2f vert(appdata v)
            {
                v2f o;
                o.worldPos = mul(unity_ObjectToWorld, float4(v.vertex,1)).xyz;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            /* returns 0 if miss, else t0/t1 */
            bool SphereHit(float3 ro, float3 rd, float r, out float t0, out float t1)
            {
                float b = dot(ro, rd);
                float c = dot(ro, ro) - r * r;
                float h = b * b - c;
                if (h < 0) { t0 = t1 = 0; return false; }
                h = sqrt(h);
                t0 = -b - h;
                t1 = -b + h;
                return true;
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 centre = unity_ObjectToWorld[3].xyz;
                float3 camPos = _WorldSpaceCameraPos;
                float3 rd = normalize(i.worldPos - camPos);
                float3 ro = camPos - centre;            // move sphere to origin

                float t0, t1;
                if (!SphereHit(ro, rd, _AtmosphereRadius, t0, t1)) discard;
                if (t1 < 0) return 0;

                t0 = max(t0, 0);
                const int STEPS = 12;
                float dt = (t1 - t0) / STEPS;

                float  odR = 0, odM = 0;
                float3 L = 0;
                float3 betaM = _MieCoeff.xxx;

                float3 sample = ro + rd * (t0 + 0.5 * dt);

                for (int s = 0; s < STEPS; ++s)
                {
                    float h = length(sample) - _PlanetRadius;
                    float dR = exp(-h / _RayleighHeight);
                    float dM = exp(-h / _MieHeight);

                    odR += dR * dt;
                    odM += dM * dt;

                    float3 tau = _RayleighCoeff * odR + betaM * odM;
                    float3 Tr = exp(-tau);                       // sunlight to sample

                    float mu = dot(rd, _SunDirWS);              // cos θ
                    float phaseR = 0.75 * (1 + mu * mu);
                    float phaseM = (1 / (4 * UNITY_PI)) * ((1 - _MieG * _MieG) /
                                   pow(1 + _MieG * _MieG - 2 * _MieG * mu, 1.5));

                    L += Tr * (dR * _RayleighCoeff * phaseR + dM * betaM * phaseM) * dt;

                    sample += rd * dt;
                }

                /* through-atmosphere attenuation toward camera */
                float3 tauCam = _RayleighCoeff * odR + betaM * odM;
                float3 T_cam = exp(-tauCam);

                float alpha = (1 - dot(T_cam, 1.0 / 3.0)) * _Tint.a;
                float3 col = L * _SunIntensity * _Tint.rgb;

                return float4(col, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
