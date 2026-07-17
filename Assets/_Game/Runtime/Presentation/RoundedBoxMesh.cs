using System.Collections.Generic;
using UnityEngine;

namespace ColorBlocks.Presentation
{
    /// <summary>
    /// Builds one unit-sized rounded box shared by every runtime presentation object.
    /// Geometry is allocated only on the first Get call and retained for the process lifetime.
    /// </summary>
    public static class RoundedBoxMesh
    {
        public const float CornerRadius = 0.15f;
        public const int BevelSegments = 5;

        private const float HalfExtent = 0.5f;
        private const int ExpectedVertexCount = 336;
        private const int ExpectedIndexCount = 996;

        private static readonly Dictionary<int, Mesh> MeshesByRadius = new();

        public static Mesh Get() => Get(CornerRadius);

        /// <summary>
        /// Returns a cached unit rounded box with the requested normalized radius. Blocks and
        /// compact controls use the reference 0.15 radius, while large non-uniformly scaled
        /// panels need a smaller radius so their bevel does not turn into a screen-wide arch.
        /// </summary>
        public static Mesh Get(float cornerRadius)
        {
            float safeRadius = Mathf.Clamp(cornerRadius, 0.001f, 0.499f);
            int radiusKey = Mathf.RoundToInt(safeRadius * 10000f);
            if (MeshesByRadius.TryGetValue(radiusKey, out Mesh cached) && cached != null) return cached;

            safeRadius = radiusKey / 10000f;
            float innerExtent = HalfExtent - safeRadius;

            List<Vector3> vertices = new(ExpectedVertexCount);
            List<Vector3> normals = new(ExpectedVertexCount);
            List<int> triangles = new(ExpectedIndexCount);

            AddFlatFaces(vertices, normals, triangles, innerExtent);
            AddRoundedEdges(vertices, normals, triangles, innerExtent, safeRadius);
            AddRoundedCorners(vertices, normals, triangles, innerExtent, safeRadius);

            Mesh mesh = new()
            {
                name = $"Runtime_RoundedBox_R{radiusKey:D4}_S{BevelSegments}",
                hideFlags = HideFlags.DontSave
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0, false);
            // All six extrema are authored at exactly +/-0.5. Assigning the bounds explicitly
            // avoids platform-dependent floating-point drift in RecalculateBounds.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one);
            // Keep the tiny CPU copy readable for deterministic geometry validation. No runtime
            // code reads it, and subsequent Get calls return the already-built shared mesh.
            mesh.UploadMeshData(false);
            MeshesByRadius[radiusKey] = mesh;
            return mesh;
        }

        private static void AddFlatFaces(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> triangles,
            float innerExtent)
        {
            AddFlatFace(vertices, normals, triangles, Vector3.right, Vector3.up, Vector3.forward, innerExtent);
            AddFlatFace(vertices, normals, triangles, Vector3.left, Vector3.up, Vector3.forward, innerExtent);
            AddFlatFace(vertices, normals, triangles, Vector3.up, Vector3.right, Vector3.forward, innerExtent);
            AddFlatFace(vertices, normals, triangles, Vector3.down, Vector3.right, Vector3.forward, innerExtent);
            AddFlatFace(vertices, normals, triangles, Vector3.forward, Vector3.right, Vector3.up, innerExtent);
            AddFlatFace(vertices, normals, triangles, Vector3.back, Vector3.right, Vector3.up, innerExtent);
        }

        private static void AddFlatFace(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> triangles,
            Vector3 normal,
            Vector3 tangentA,
            Vector3 tangentB,
            float innerExtent)
        {
            Vector3 center = normal * HalfExtent;
            int a = AddVertex(vertices, normals, center - tangentA * innerExtent - tangentB * innerExtent, normal);
            int b = AddVertex(vertices, normals, center + tangentA * innerExtent - tangentB * innerExtent, normal);
            int c = AddVertex(vertices, normals, center + tangentA * innerExtent + tangentB * innerExtent, normal);
            int d = AddVertex(vertices, normals, center - tangentA * innerExtent + tangentB * innerExtent, normal);
            AddOrientedTriangle(vertices, triangles, a, b, c, normal);
            AddOrientedTriangle(vertices, triangles, a, c, d, normal);
        }

        private static void AddRoundedEdges(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> triangles,
            float innerExtent,
            float cornerRadius)
        {
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    AddRoundedEdge(
                        vertices, normals, triangles,
                        Vector3.right, sx, Vector3.up, sy, Vector3.forward,
                        innerExtent, cornerRadius);
                }
            }

            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    AddRoundedEdge(
                        vertices, normals, triangles,
                        Vector3.right, sx, Vector3.forward, sz, Vector3.up,
                        innerExtent, cornerRadius);
                }
            }

            for (int sy = -1; sy <= 1; sy += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    AddRoundedEdge(
                        vertices, normals, triangles,
                        Vector3.up, sy, Vector3.forward, sz, Vector3.right,
                        innerExtent, cornerRadius);
                }
            }
        }

        private static void AddRoundedEdge(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> triangles,
            Vector3 sideAxisA,
            int sideSignA,
            Vector3 sideAxisB,
            int sideSignB,
            Vector3 lengthAxis,
            float innerExtent,
            float cornerRadius)
        {
            Vector3 signedAxisA = sideAxisA * sideSignA;
            Vector3 signedAxisB = sideAxisB * sideSignB;
            Vector3 center = (signedAxisA + signedAxisB) * innerExtent;
            int firstVertex = vertices.Count;

            for (int segment = 0; segment <= BevelSegments; segment++)
            {
                // Normalized linear interpolation matches the spherical corner-patch boundary
                // exactly, eliminating cracks while retaining five visible bevel segments.
                Vector3 normal = (
                    signedAxisA * (BevelSegments - segment) +
                    signedAxisB * segment).normalized;
                Vector3 radialPosition = center + normal * cornerRadius;
                AddVertex(
                    vertices,
                    normals,
                    radialPosition - lengthAxis * innerExtent,
                    normal);
                AddVertex(
                    vertices,
                    normals,
                    radialPosition + lengthAxis * innerExtent,
                    normal);
            }

            for (int segment = 0; segment < BevelSegments; segment++)
            {
                int lowA = firstVertex + segment * 2;
                int highA = lowA + 1;
                int lowB = lowA + 2;
                int highB = lowA + 3;
                Vector3 expectedNormal = (normals[lowA] + normals[lowB]).normalized;
                AddOrientedTriangle(vertices, triangles, lowA, highA, highB, expectedNormal);
                AddOrientedTriangle(vertices, triangles, lowA, highB, lowB, expectedNormal);
            }
        }

        private static void AddRoundedCorners(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> triangles,
            float innerExtent,
            float cornerRadius)
        {
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        AddRoundedCorner(
                            vertices,
                            normals,
                            triangles,
                            sx,
                            sy,
                            sz,
                            innerExtent,
                            cornerRadius);
                    }
                }
            }
        }

        private static void AddRoundedCorner(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> triangles,
            int sx,
            int sy,
            int sz,
            float innerExtent,
            float cornerRadius)
        {
            Vector3 axisX = Vector3.right * sx;
            Vector3 axisY = Vector3.up * sy;
            Vector3 axisZ = Vector3.forward * sz;
            Vector3 center = (axisX + axisY + axisZ) * innerExtent;
            int firstVertex = vertices.Count;

            // A normalized barycentric octant gives a compact, watertight spherical corner.
            // Its three boundaries use the same interpolation as the rounded edge strips.
            for (int row = 0; row <= BevelSegments; row++)
            {
                for (int column = 0; column <= BevelSegments - row; column++)
                {
                    int weightX = BevelSegments - row - column;
                    int weightY = row;
                    int weightZ = column;
                    Vector3 normal = (
                        axisX * weightX +
                        axisY * weightY +
                        axisZ * weightZ).normalized;
                    AddVertex(vertices, normals, center + normal * cornerRadius, normal);
                }
            }

            for (int row = 0; row < BevelSegments; row++)
            {
                int trianglesInRow = BevelSegments - row;
                for (int column = 0; column < trianglesInRow; column++)
                {
                    int a = CornerVertexIndex(firstVertex, row, column);
                    int b = CornerVertexIndex(firstVertex, row + 1, column);
                    int c = CornerVertexIndex(firstVertex, row, column + 1);
                    Vector3 expectedNormal = (normals[a] + normals[b] + normals[c]).normalized;
                    AddOrientedTriangle(vertices, triangles, a, b, c, expectedNormal);

                    if (column >= trianglesInRow - 1) continue;
                    int d = CornerVertexIndex(firstVertex, row + 1, column + 1);
                    expectedNormal = (normals[b] + normals[d] + normals[c]).normalized;
                    AddOrientedTriangle(vertices, triangles, b, d, c, expectedNormal);
                }
            }
        }

        private static int CornerVertexIndex(int firstVertex, int row, int column)
        {
            int rowOffset = row * (BevelSegments + 1) - row * (row - 1) / 2;
            return firstVertex + rowOffset + column;
        }

        private static int AddVertex(
            List<Vector3> vertices,
            List<Vector3> normals,
            Vector3 position,
            Vector3 normal)
        {
            int index = vertices.Count;
            vertices.Add(position);
            normals.Add(normal);
            return index;
        }

        private static void AddOrientedTriangle(
            IReadOnlyList<Vector3> vertices,
            List<int> triangles,
            int a,
            int b,
            int c,
            Vector3 expectedNormal)
        {
            Vector3 geometricNormal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (Vector3.Dot(geometricNormal, expectedNormal) < 0f) (b, c) = (c, b);
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }
    }
}
