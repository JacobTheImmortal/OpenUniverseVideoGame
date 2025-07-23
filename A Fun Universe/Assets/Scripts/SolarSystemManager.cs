using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SolarSystemManager : MonoBehaviour
{
    [Header("Star Settings")]
    [SerializeField, Range(50f, 200f)] float minStarRadius = 50f;
    [SerializeField, Range(50f, 400f)] float maxStarRadius = 300f;

    [SerializeField] Light starLight;

    // Light intensity range that matches the size range
    [SerializeField] float minStarIntensity = 800000f;
    [SerializeField] float maxStarIntensity = 1000000f;
    [SerializeField] Gradient starColor;

    [Header("Planet Settings")]
    [SerializeField, Range(1, 9)] int minPlanets = 1;
    [SerializeField, Range(1, 9)] int maxPlanets = 9;

    [SerializeField, Range(200f, 1000f)] float minOrbitRadius = 300f;
    [SerializeField, Range(200f, 1000f)] float orbitStep = 400f;
    [SerializeField] PlanetGenerator planetPrefab;
    float currentStarRadius;
    Light mainStar;

    List<Transform> spawnedPlanets = new List<Transform>();

    void Start()
    {
        /* ── 1.  Star size & brightness ───────────────────────────── */
        float starRadius = Random.Range(minStarRadius, maxStarRadius);
        currentStarRadius = starRadius;

        float t = (starRadius - minStarRadius) / (maxStarRadius - minStarRadius);
        float starLightIntensity = Mathf.Lerp(minStarIntensity, maxStarIntensity, t);

        starLight = SpawnStar(starRadius, starLightIntensity);


        /* ── 2.  Random planet count ──────────────────────────────── */
        int planetCount = Random.Range(minPlanets, maxPlanets + 1);

        /* ── 3.  Spawn planets with safe spacing ──────────────────── */
        float nextOrbitRadius = 0f;
        float farthestSurface = 0f;          // for light.range later


        for (int i = 0; i < planetCount; i++)
        {
            /* Planet size: 10 … ½× star radius */
            float planetRadius = Random.Range(starRadius * 0.2f, starRadius * 0.8f);

            float orbitRadius;
            if (i == 0)
            {
                float minCentre = minOrbitRadius + ((starRadius + planetRadius)*2f);
                float maxCentre = minOrbitRadius * 3 + ((starRadius + planetRadius)*2f);
                orbitRadius = Random.Range(minCentre, maxCentre);
            }
            else
            {
                orbitRadius = nextOrbitRadius + Random.Range((orbitStep + planetRadius*2f) * 1.5f, (orbitStep + planetRadius*2f) * 3f);
            }

            nextOrbitRadius = orbitRadius;
            farthestSurface = Mathf.Max(farthestSurface, orbitRadius + planetRadius);

            SpawnPlanet(i, orbitRadius, planetRadius);
        }

        /* ── 4.  Adjust star-light range so every planet is lit ───── */
        starLight.range = farthestSurface + 50f;   // 50-unit safety margin
        Physics.SyncTransforms();
        SpawnPlayerInSpace();
        SpawnStarFillLight(starLight.transform,
                   starLight.color,
                   farthestSurface,
                   starLightIntensity);



    }

    Light SpawnStar(float radius, float intensity)
    {
        GameObject star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        star.name = "Star";
        star.transform.SetParent(transform);
        star.transform.localScale = Vector3.one * radius * 2f;

        Light light = star.AddComponent<Light>();
        light.type = LightType.Point;
        light.intensity = intensity;
        light.color = starColor.Evaluate(Random.value);

        var mat = new Material(Shader.Find("Unlit/Color")) { color = light.color };
        star.GetComponent<MeshRenderer>().material = mat;
        mainStar = light;
        return light;
    }

    void SpawnPlanet(int index, float orbitRadius, float planetRadius)
    {
        var planet = Instantiate(planetPrefab, transform);
        planet.name = $"Planet_{index}";

        /* ---------- size-dependent noise tuning ------------------- */
        float t = Mathf.InverseLerp(currentStarRadius * 0.1f,
                                    currentStarRadius * 0.8f,
                                    planetRadius);                // 0 = small, 1 = big

        Color low = Random.ColorHSV(0f, 1f, 0.4f, 1f, 0.2f, 0.9f); // darker base
        Color high = Random.ColorHSV(0f, 1f, 0.8f, 1f, 0.2f, 0.9f);     // bright peaks
        planet.lowColor = low;
        planet.highColor = high;

        planet.baseAmp = Mathf.Lerp(5f, 30f, t);                 // height scale 10-25
        planet.octaves = Random.Range(1, 4);                      // 2,3,4,5
        planet.gain = Random.Range(0.1f, 0.3f);                // roughness
        /* ---------------------------------------------------------- */
        planet.surfaceGravity = Mathf.Lerp(4f, 20f, t);

        planet.SetRadius(planetRadius);

        float sizeRatio = planetRadius / currentStarRadius;
        planet.mountainCount = Random.Range(1, 6);              // 1 – 6

        if (sizeRatio >= 0.20f)          // ≥ 20 % of star
            planet.craterCount = Random.Range(0, 4);            // 0 – 4
        else
            planet.craterCount = Random.Range(1, 20);           // 1 – 20

        planet.starRadius = currentStarRadius;   // pass for any future use

        planet.Generate(Random.Range(0, 1_000_000));

        var orbit = planet.gameObject.AddComponent<OrbitMotion>();
        orbit.origin = transform;
        orbit.orbitRadius = orbitRadius;

        // ----- NEW: random orbital plane & direction -----------------
        float inclDeg = Random.Range(-30f, 30f);          // tilt ±30 °
        float lonDeg = Random.Range(0f, 360f);           // random heading
        Quaternion planeRot = Quaternion.Euler(inclDeg, lonDeg, 0f);

        orbit.orbitAxis = planeRot * Vector3.up;         // supply to the script
        float dir = Random.value < 0.5f ? 1f : -1f; // 50 % retrograde
        orbit.orbitSpeed = dir * Random.Range(0.1f, 0.3f);

        orbit.startPhase = Random.Range(0f, 360f);
        orbit.selfSpin = Random.Range(-0.6f, 0.6f);
        // -------------------------------------------------------------

        spawnedPlanets.Add(planet.transform);
    }

    void SpawnPlayerInSpace()
    {
        // pick a random point in a hollow sphere around the star
        float minDist = minOrbitRadius * 1.5f;
        float maxDist = minOrbitRadius * 2.5f;
        Vector3 pos = Random.onUnitSphere * Random.Range(minDist, maxDist);

        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.transform.position = pos;
        player.transform.rotation = Quaternion.LookRotation(-pos.normalized); // face star

        // attach main camera
        Camera.main.transform.SetParent(player.transform);
        Camera.main.transform.localPosition = new Vector3(0, 1.2f, 0);
        Camera.main.transform.localRotation = Quaternion.identity;

        Rigidbody rb = player.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.angularDamping = 0;
        rb.linearDamping = 0;

        player.AddComponent<FreeFlyController>();
    }
    Light SpawnStarFillLight(Transform starTf, Color tint,
                         float starRange, float starIntensity)
    {
        GameObject g = new GameObject("StarFill");
        g.transform.SetParent(starTf, false);          // stick to the star

        Light l = g.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = tint;

        /* Make it reach *farther* than the main star light */
        l.range = starRange * 50f;

        /* Very low power – try 1-3 % of the main light */
        l.intensity = starIntensity * 0.9f;

        /* Optional: no shadows – keeps it cheap */
        l.shadows = LightShadows.None;
        return l;
    }
    void LateUpdate()
    {
        if (starLight == null) return;

        Vector3 dir = (starLight.transform.position - Camera.main.transform.position).normalized;
        Shader.SetGlobalVector("_SunDirWS", dir);               // points **to** the star
        Shader.SetGlobalFloat("_SunIntensity", starLight.intensity);
    }

}

