using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.Mathf;
using static UnityEngine.Vector3;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PlanetGenerator : MonoBehaviour
{
    /* ─── Base sphere ──────────────────────────────────────── */
    [Header("Shape")]
    [Range(0, 5)] public int recursionLevel = 3;
    [Min(0.1f)] public float radius = 5f;

    /* ─── Global FBM settings  (gentle hills) ──────────────── */
    [Header("Global Noise (FBM)")]
    [Range(1, 6)] public int octaves = 3;
    [Min(.1f)] public float baseAmp = 1f;
    [Min(.1f)] public float baseFreq = 1f;
    [Range(1f, 3f)] public float lacunarity = 2f;
    [Range(0f, 1f)] public float gain = 0.5f;

    /* ─── Stamp parameters  (craters / mountains) ─────────── */
    [Header("Stamps – Craters & Mountains")]
    [Range(0, 50)] public int craterCount = 0;
    [Range(0, 50)] public int mountainCount = 0;

    [Header("Stamp Radius Ranges  (degrees)")]
    [Range(1f, 45f)] public float craterRadiusMinDeg = 5f;
    [Range(1f, 45f)] public float craterRadiusMaxDeg = 15f;
    [Range(1f, 45f)] public float mountainRadiusMinDeg = 10f;
    [Range(1f, 45f)] public float mountainRadiusMaxDeg = 20f;

    [Tooltip("Mountain amplitude is this × baseAmp")]
    public float mountainAmpMul = 2f;
    [Tooltip("Crater depth is negative height × this factor")]
    public float craterDepthMul = 1f;

    /* ─── Vertex-colour visual settings ────────────────────── */
    [Header("Visual")]
    public Material vertexColorMaterial;

    [Header("Tri-Colour Blend")]
    [Range(0f, 1f)] public float cutLowMid = 0.35f;
    [Range(0f, 1f)] public float cutMidHigh = 0.65f;
    [Range(.01f, 1f)] public float blendWidth = 0.25f;
    [Range(-1f, 1f)] public float blendSoftness = 1f;
    [HideInInspector] public Color lowColor = Color.green;
    [HideInInspector] public Color midColor = Color.grey;
    [HideInInspector] public Color highColor = Color.yellow;

    /* ─── Exposed radii / misc. ────────────────────────────── */
    public float OuterRadius { get; private set; }
    public float RealRadius => OuterRadius;          // legacy alias
    [HideInInspector] public float surfaceGravity = 12f;
    [HideInInspector] public float starRadius = 100f; // kept for future use

    /* ------------------------------------------------------- */
    MeshFilter mf;

    public void Generate(int seed)
    {
        Random.InitState(seed);

        mf = GetComponent<MeshFilter>();
        Mesh sphere = IcoSphere.Create(recursionLevel, radius);
        ApplyNoise(sphere, seed);
        mf.sharedMesh = sphere;

        /* collider */
        var col = GetComponent<MeshCollider>() ??
                  gameObject.AddComponent<MeshCollider>();
        col.sharedMesh = sphere;

        gameObject.layer = LayerMask.NameToLayer("Planet");

        const float SHELL_EXPAND = 1.02f;
        const float SHELL_THICK_F = 0.04f;
        MakeAtmosphereShell(OuterRadius * SHELL_EXPAND,
                            OuterRadius * SHELL_THICK_F);
    }

    void MakeAtmosphereShell(float shellRadius, float thickness)
    {
        Mesh m = IcoSphere.Create(recursionLevel, shellRadius);

        var go = new GameObject("Atmosphere");
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = m;

        /* ---------- pick a sky colour -------------------------------- */
        Color sky = Color.HSVToRGB(Random.value, 0.5f, 1f);
        sky.a = 0.5f;                         // tweak overall strength here

        /* ---------- get / clone material ----------------------------- */
        Material src = Resources.Load<Material>("Mat_AtmoRadial");
        if (src == null)
        {
            src = new Material(Shader.Find("Custom/AtmosphereScattering"));
            Debug.LogWarning("Mat_AtmoRadial not found – made a fresh one in RAM.");
        }

        Material mat = new Material(src);                 // unique per-planet

        /* ---------- per-planet scale factors -------------------------- */
        float R = OuterRadius;       // surface radius (world units)
        float A = shellRadius;       // top of atmosphere
        float H_Ray = thickness * 0.5f;   // 50 % of shell thickness
        float H_Mie = thickness * 0.1f;   // 10 % – haze hugs the ground

        /* ---------- scattering coefficients (start bright, tune down) */
        Vector3 betaR = new Vector3(5.8e-4f, 13.5e-4f, 33.1e-4f);   // RGB
        float betaM = 2e-4f;

        // ---------- push constants ------------------------------------
        mat.SetColor("_Tint", sky);
        
        mat.SetFloat("_PlanetRadius", R);
        mat.SetFloat("_AtmosphereRadius", A);
        mat.SetVector("_RayleighCoeff", betaR);
        mat.SetFloat("_MieCoeff", betaM);
        mat.SetFloat("_RayleighHeight", H_Ray);
        mat.SetFloat("_MieHeight", H_Mie);
        mat.SetFloat("_MieG", 0.76f);      //  0 = isotropic, 1 = forward

        go.AddComponent<MeshRenderer>().sharedMaterial = mat;

        /* ---------- controller --------------------------------------- */
        var ctrl = go.AddComponent<AtmosphereShell>();
        ctrl.Configure(transform.position, shellRadius - thickness, thickness);
        ctrl.tint = sky;                      // <<<<<<<<<<  keep in-sync  >>>>>>>>>>
    }

    public void SetRadius(float r) => radius = r;

    /* ───────── helpers ────────────────────────────────────── */
    float FractalNoise(Vector3 p, int seed, float amp, float g, int oct)
    {
        float freq = baseFreq, sum = 0;
        for (int i = 0; i < oct; i++)
        {
            float n = (PerlinNoise(p.x * freq + seed, p.y * freq + seed) +
                       PerlinNoise(p.y * freq + seed * 1.3f, p.z * freq + seed * 1.3f) +
                       PerlinNoise(p.z * freq + seed * 2.1f, p.x * freq + seed * 2.1f))
                      / 3f - .5f;
            sum += n * amp;
            amp *= g; freq *= lacunarity;
        }
        return sum;
    }

    float StampDisplace(Vector3 dir, Vector3 centreDir, float radiusDeg, float height)
    {
        float ang = Acos(Dot(dir, centreDir)) * Mathf.Rad2Deg;   // 0-180 °
        if (ang > radiusDeg) return 0f;

        float t = Cos(ang / radiusDeg * Mathf.PI * .5f); // smooth fall-off
        return height * t;
    }

    /* ───────── mesh deformation & colouring ───────────────── */
    void ApplyNoise(Mesh mesh, int seed)
    {
        Vector3[] v = mesh.vertices;
        float maxR = 0f, minR = float.MaxValue;

        /* ----- choose random stamp centres ------------------- */
        Random.InitState(seed * 3 + 17);

        List<(Vector3 c, float r)> craterS = new();
        List<(Vector3 c, float r)> mountainS = new();

        for (int i = 0; i < craterCount; i++)
            craterS.Add((Random.onUnitSphere,
                           Random.Range(craterRadiusMinDeg, craterRadiusMaxDeg)));
        for (int i = 0; i < mountainCount; i++)
            mountainS.Add((Random.onUnitSphere,
                           Random.Range(mountainRadiusMinDeg, mountainRadiusMaxDeg)));

        /* ----- deform ---------------------------------------- */
        for (int i = 0; i < v.Length; i++)
        {
            Vector3 dir = v[i].normalized;
            float h = FractalNoise(dir, seed, baseAmp, gain, octaves);

            foreach (var (c, r) in craterS)
                h += StampDisplace(dir, c, r, -baseAmp * craterDepthMul);

            foreach (var (c, r) in mountainS)
                h += StampDisplace(dir, c, r, baseAmp * mountainAmpMul);

            v[i] = dir * (radius + h);
            float mag = v[i].magnitude;
            maxR = Max(maxR, mag); minR = Min(minR, mag);
        }
        mesh.vertices = v;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        /* ----- vertex colours -------------------------------- */
        Color[] cols = new Color[v.Length];
        float range = maxR - minR;
        float w = 0.15f * blendSoftness;         // max half-width of a blend zone

        for (int i = 0; i < v.Length; i++)
        {
            float h01 = (v[i].magnitude - minR) / range;
            Color c;

            if (h01 < cutLowMid - w) c = lowColor;
            else if (h01 < cutLowMid + w) c = Color.Lerp(lowColor, midColor,
                                      InverseLerp(cutLowMid - w, cutLowMid + w, h01));
            else if (h01 < cutMidHigh - w) c = midColor;
            else if (h01 < cutMidHigh + w) c = Color.Lerp(midColor, highColor,
                                       InverseLerp(cutMidHigh - w, cutMidHigh + w, h01));
            else c = highColor;

            cols[i] = c;
        }
        mesh.colors = cols;
        OuterRadius = maxR;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying || mf == null) return;
        ApplyNoise(mf.sharedMesh, Random.Range(0, 1_000_000));
    }
#endif
}
