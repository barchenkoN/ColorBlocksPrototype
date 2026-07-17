using System.Collections.Generic;
using ColorBlocks.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlocks.Tests
{
    public sealed class RoundedBoxMeshTests
    {
        [Test]
        public void SharedRoundedBox_HasReferenceMatchedRadiusAndMobileGeometryBudget()
        {
            Mesh mesh = RoundedBoxMesh.Get();

            Assert.That(mesh, Is.SameAs(RoundedBoxMesh.Get()),
                "Every compact presentation object must reuse the reference-radius mesh.");
            Mesh panelMesh = RoundedBoxMesh.Get(0.08f);
            Assert.That(panelMesh, Is.SameAs(RoundedBoxMesh.Get(0.08f)),
                "Large panels must reuse their smaller-radius mesh variant.");
            Assert.That(panelMesh, Is.Not.SameAs(mesh));
            Assert.That(panelMesh.vertexCount, Is.EqualTo(mesh.vertexCount));
            Assert.That(RoundedBoxMesh.CornerRadius, Is.InRange(0.14f, 0.16f));
            Assert.That(RoundedBoxMesh.BevelSegments, Is.InRange(4, 6));
            Assert.That(mesh.vertexCount, Is.EqualTo(336));
            Assert.That(mesh.triangles.Length / 3, Is.EqualTo(332));
            Assert.That(mesh.subMeshCount, Is.EqualTo(1));
        }

        [Test]
        public void SharedRoundedBox_HasExactUnitBoundsSmoothNormalsAndOutwardWinding()
        {
            Mesh mesh = RoundedBoxMesh.Get();
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            int[] triangles = mesh.triangles;

            Assert.That(mesh.bounds.center, Is.EqualTo(Vector3.zero));
            Assert.That(mesh.bounds.size.x, Is.EqualTo(1f).Within(0.000001f));
            Assert.That(mesh.bounds.size.y, Is.EqualTo(1f).Within(0.000001f));
            Assert.That(mesh.bounds.size.z, Is.EqualTo(1f).Within(0.000001f));
            Assert.That(normals, Has.Length.EqualTo(vertices.Length));
            Assert.That(triangles.Length % 3, Is.Zero);

            Vector3 minimum = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 maximum = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            Dictionary<Vector3Int, Vector3> seamNormals = new();
            bool foundSphericalCornerNormal = false;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 vertex = vertices[i];
                Vector3 normal = normals[i];
                minimum = Vector3.Min(minimum, vertex);
                maximum = Vector3.Max(maximum, vertex);
                Assert.That(normal.magnitude, Is.EqualTo(1f).Within(0.00001f),
                    $"Normal {i} is not normalized.");
                Assert.That(Vector3.Dot(vertex, normal), Is.GreaterThan(0f),
                    $"Normal {i} points into the convex mesh.");

                if (Mathf.Abs(normal.x) > 0.1f &&
                    Mathf.Abs(normal.y) > 0.1f &&
                    Mathf.Abs(normal.z) > 0.1f)
                {
                    foundSphericalCornerNormal = true;
                }

                Vector3Int seamKey = new(
                    Mathf.RoundToInt(vertex.x * 100000f),
                    Mathf.RoundToInt(vertex.y * 100000f),
                    Mathf.RoundToInt(vertex.z * 100000f));
                if (seamNormals.TryGetValue(seamKey, out Vector3 existingNormal))
                {
                    Assert.That(Vector3.Dot(existingNormal, normal), Is.GreaterThan(0.9999f),
                        $"Patch seam at {vertex} has a discontinuous normal.");
                }
                else
                {
                    seamNormals.Add(seamKey, normal);
                }
            }

            Assert.That(minimum.x, Is.EqualTo(-0.5f).Within(0.000001f));
            Assert.That(minimum.y, Is.EqualTo(-0.5f).Within(0.000001f));
            Assert.That(minimum.z, Is.EqualTo(-0.5f).Within(0.000001f));
            Assert.That(maximum.x, Is.EqualTo(0.5f).Within(0.000001f));
            Assert.That(maximum.y, Is.EqualTo(0.5f).Within(0.000001f));
            Assert.That(maximum.z, Is.EqualTo(0.5f).Within(0.000001f));
            Assert.That(foundSphericalCornerNormal, Is.True,
                "Rounded corners must contain genuinely three-axis normals.");

            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];
                Vector3 geometricNormal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                Vector3 expectedNormal = (normals[a] + normals[b] + normals[c]).normalized;
                Assert.That(geometricNormal.sqrMagnitude, Is.GreaterThan(0.00000001f),
                    $"Triangle {i / 3} is degenerate.");
                Assert.That(Vector3.Dot(geometricNormal.normalized, expectedNormal), Is.GreaterThan(0.95f),
                    $"Triangle {i / 3} has inward or inconsistent winding.");
            }
        }
    }
}
