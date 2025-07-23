using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class IcoSphere
{
    struct Triangle { public int v1, v2, v3; public Triangle(int a, int b, int c) { v1 = a; v2 = b; v3 = c; } }

    // Public entry point
    public static Mesh Create(int recursionLevel, float radius)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<Triangle>();

        // --- 1  create 12-vertex icosahedron ---
        float t = (1f + Mathf.Sqrt(5f)) * .5f;

        void Add(Vector3 v) => vertices.Add(v.normalized);
        Add(new Vector3(-1, t, 0)); Add(new Vector3(1, t, 0));
        Add(new Vector3(-1, -t, 0)); Add(new Vector3(1, -t, 0));
        Add(new Vector3(0, -1, t)); Add(new Vector3(0, 1, t));
        Add(new Vector3(0, -1, -t)); Add(new Vector3(0, 1, -t));
        Add(new Vector3(t, 0, -1)); Add(new Vector3(t, 0, 1));
        Add(new Vector3(-t, 0, -1)); Add(new Vector3(-t, 0, 1));

        triangles.AddRange(new[]{
            new Triangle(0,11,5), new Triangle(0,5,1),  new Triangle(0,1,7),  new Triangle(0,7,10), new Triangle(0,10,11),
            new Triangle(1,5,9),  new Triangle(5,11,4), new Triangle(11,10,2),new Triangle(10,7,6), new Triangle(7,1,8),
            new Triangle(3,9,4),  new Triangle(3,4,2),  new Triangle(3,2,6),  new Triangle(3,6,8),  new Triangle(3,8,9),
            new Triangle(4,9,5),  new Triangle(2,4,11), new Triangle(6,2,10), new Triangle(8,6,7),  new Triangle(9,8,1)
        });

        // --- 2  subdivide ---
        var midCache = new Dictionary<long, int>();
        int GetMid(int a, int b)
        {
            long key = ((long)Mathf.Min(a, b) << 32) + Mathf.Max(a, b);
            if (midCache.TryGetValue(key, out int idx)) return idx;
            Vector3 mid = (vertices[a] + vertices[b]).normalized;
            vertices.Add(mid);
            midCache[key] = vertices.Count - 1;
            return midCache[key];
        }

        for (int i = 0; i < recursionLevel; i++)
        {
            var newTris = new List<Triangle>(triangles.Count * 4);
            foreach (var tri in triangles)
            {
                int a = GetMid(tri.v1, tri.v2);
                int b = GetMid(tri.v2, tri.v3);
                int c = GetMid(tri.v3, tri.v1);
                newTris.Add(new Triangle(tri.v1, a, c));
                newTris.Add(new Triangle(tri.v2, b, a));
                newTris.Add(new Triangle(tri.v3, c, b));
                newTris.Add(new Triangle(a, b, c));
            }
            triangles = newTris;
        }

        // --- 3  build mesh ---
        var mesh = new Mesh { name = $"IcoSphere_{recursionLevel}" };
        mesh.SetVertices(vertices.ConvertAll(v => v * radius));
        var triIndices = new List<int>(triangles.Count * 3);
        foreach (var t3 in triangles) { triIndices.Add(t3.v1); triIndices.Add(t3.v2); triIndices.Add(t3.v3); }
        mesh.SetTriangles(triIndices, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
