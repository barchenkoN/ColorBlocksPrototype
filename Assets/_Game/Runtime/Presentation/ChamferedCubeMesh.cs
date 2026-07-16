using System.Collections.Generic;
using UnityEngine;

namespace ColorBlocks.Presentation
{
    internal static class ChamferedCubeMesh
    {
        private static Mesh _sharedMesh;

        public static Mesh Get()
        {
            if (_sharedMesh != null)
            {
                return _sharedMesh;
            }

            const float half = 0.5f;
            // A narrow bevel matches the reference's 1-3 px seams at a ~75 px tile pitch.
            const float inset = 0.44f;
            List<Vector3> vertices = new();
            List<Vector3> normals = new();
            List<int> triangles = new();

            AddFace(vertices, normals, triangles,
                new Vector3(half, -inset, -inset), new Vector3(half, inset, -inset),
                new Vector3(half, inset, inset), new Vector3(half, -inset, inset), Vector3.right);
            AddFace(vertices, normals, triangles,
                new Vector3(-half, -inset, inset), new Vector3(-half, inset, inset),
                new Vector3(-half, inset, -inset), new Vector3(-half, -inset, -inset), Vector3.left);
            AddFace(vertices, normals, triangles,
                new Vector3(-inset, half, -inset), new Vector3(-inset, half, inset),
                new Vector3(inset, half, inset), new Vector3(inset, half, -inset), Vector3.up);
            AddFace(vertices, normals, triangles,
                new Vector3(-inset, -half, inset), new Vector3(-inset, -half, -inset),
                new Vector3(inset, -half, -inset), new Vector3(inset, -half, inset), Vector3.down);
            AddFace(vertices, normals, triangles,
                new Vector3(-inset, -inset, half), new Vector3(inset, -inset, half),
                new Vector3(inset, inset, half), new Vector3(-inset, inset, half), Vector3.forward);
            AddFace(vertices, normals, triangles,
                new Vector3(inset, -inset, -half), new Vector3(-inset, -inset, -half),
                new Vector3(-inset, inset, -half), new Vector3(inset, inset, -half), Vector3.back);

            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    Vector3 expected = new(sx, sy, 0f);
                    AddFace(vertices, normals, triangles,
                        new Vector3(sx * half, sy * inset, -inset), new Vector3(sx * half, sy * inset, inset),
                        new Vector3(sx * inset, sy * half, inset), new Vector3(sx * inset, sy * half, -inset), expected);
                }
            }

            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 expected = new(sx, 0f, sz);
                    AddFace(vertices, normals, triangles,
                        new Vector3(sx * half, -inset, sz * inset), new Vector3(sx * inset, -inset, sz * half),
                        new Vector3(sx * inset, inset, sz * half), new Vector3(sx * half, inset, sz * inset), expected);
                }
            }

            for (int sy = -1; sy <= 1; sy += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 expected = new(0f, sy, sz);
                    AddFace(vertices, normals, triangles,
                        new Vector3(-inset, sy * half, sz * inset), new Vector3(inset, sy * half, sz * inset),
                        new Vector3(inset, sy * inset, sz * half), new Vector3(-inset, sy * inset, sz * half), expected);
                }
            }

            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        AddTriangle(vertices, normals, triangles,
                            new Vector3(sx * half, sy * inset, sz * inset),
                            new Vector3(sx * inset, sy * half, sz * inset),
                            new Vector3(sx * inset, sy * inset, sz * half),
                            new Vector3(sx, sy, sz));
                    }
                }
            }

            _sharedMesh = new Mesh
            {
                name = "Runtime_ChamferedCube",
                hideFlags = HideFlags.DontSave
            };
            _sharedMesh.SetVertices(vertices);
            _sharedMesh.SetNormals(normals);
            _sharedMesh.SetTriangles(triangles, 0);
            _sharedMesh.RecalculateBounds();
            _sharedMesh.UploadMeshData(true);
            return _sharedMesh;
        }

        private static void AddFace(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> triangles,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d,
            Vector3 expectedNormal)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            if (Vector3.Dot(normal, expectedNormal) < 0f)
            {
                (b, d) = (d, b);
                normal = Vector3.Cross(b - a, c - a).normalized;
            }

            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            for (int i = 0; i < 4; i++) normals.Add(normal);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private static void AddTriangle(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> triangles,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 expectedNormal)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            if (Vector3.Dot(normal, expectedNormal) < 0f)
            {
                (b, c) = (c, b);
                normal = Vector3.Cross(b - a, c - a).normalized;
            }

            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }
    }
}
