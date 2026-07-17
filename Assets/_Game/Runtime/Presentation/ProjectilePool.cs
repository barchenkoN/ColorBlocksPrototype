using System;
using System.Collections;
using System.Collections.Generic;
using ColorBlocks.Core;
using UnityEngine;

namespace ColorBlocks.Presentation
{
    public sealed class ProjectilePool
    {
        private sealed class Projectile
        {
            public GameObject Root;
            public MeshRenderer Renderer;
            public MeshRenderer TracerRenderer;
            public bool Busy;
        }

        private readonly List<Projectile> _items = new();
        private readonly Transform _root;
        private readonly VisualTheme _theme;
        private readonly GameFeelProfile _feel;
        private static Mesh _orbMesh;
        private static Mesh _trailMesh;

        public ProjectilePool(VisualTheme theme, GameFeelProfile feel, int capacity = 30)
        {
            _theme = theme;
            _feel = feel;
            _root = new GameObject("ProjectilePool").transform;
            for (int i = 0; i < capacity; i++) _items.Add(Create(i));
        }

        public IEnumerator Play(
            BlockColorId color,
            Vector3 start,
            Func<Vector3> targetProvider,
            float duration,
            Action completed)
        {
            Projectile projectile = Acquire();
            projectile.Busy = true;
            // Keep the projectile ownership readable at a glance while retaining a neutral
            // high-contrast tracer over every block colour.
            projectile.Renderer.sharedMaterial = _theme.Projectile(color);
            projectile.TracerRenderer.sharedMaterial = _theme.ProjectileNeutral;
            projectile.Root.SetActive(true);
            projectile.Root.transform.position = start;

            float elapsed = 0f;
            Vector3 previous = start;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Vector3 target = targetProvider();
                Vector3 position = Vector3.Lerp(start, target, t);
                position += Vector3.up * (Mathf.Sin(t * Mathf.PI) * _feel.ProjectileArc);
                projectile.Root.transform.position = position;
                Vector3 direction = position - previous;
                if (direction.sqrMagnitude > 0.000001f)
                {
                    projectile.Root.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
                }
                previous = position;
                yield return null;
            }

            projectile.Root.transform.position = targetProvider();
            projectile.Root.SetActive(false);
            projectile.Busy = false;
            completed?.Invoke();
        }

        public void Destroy()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root.gameObject);
        }

        private Projectile Acquire()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (!_items[i].Busy) return _items[i];
            }

            Projectile extra = Create(_items.Count);
            _items.Add(extra);
            return extra;
        }

        private Projectile Create(int index)
        {
            GameObject root = new($"Projectile_{index}");
            root.transform.SetParent(_root, false);
            root.transform.localScale = Vector3.one * _feel.ProjectileScale;
            MeshFilter filter = root.AddComponent<MeshFilter>();
            filter.sharedMesh = GetOrbMesh();
            MeshRenderer renderer = root.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            GameObject tracerObject = new("Tracer");
            tracerObject.transform.SetParent(root.transform, false);
            tracerObject.transform.localPosition = Vector3.zero;
            tracerObject.transform.localScale = Vector3.one;
            MeshFilter tracerFilter = tracerObject.AddComponent<MeshFilter>();
            tracerFilter.sharedMesh = GetTrailMesh();
            MeshRenderer tracerRenderer = tracerObject.AddComponent<MeshRenderer>();
            tracerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tracerRenderer.receiveShadows = false;
            root.SetActive(false);
            return new Projectile { Root = root, Renderer = renderer, TracerRenderer = tracerRenderer };
        }

        private static Mesh GetOrbMesh()
        {
            if (_orbMesh != null) return _orbMesh;

            const int longitudeSegments = 16;
            const int latitudeSegments = 10;
            int stride = longitudeSegments + 1;
            Vector3[] vertices = new Vector3[(latitudeSegments + 1) * stride];
            int[] triangles = new int[latitudeSegments * longitudeSegments * 6];

            int vertexIndex = 0;
            for (int latitude = 0; latitude <= latitudeSegments; latitude++)
            {
                float polar = latitude / (float)latitudeSegments * Mathf.PI;
                float ringRadius = Mathf.Sin(polar) * 0.5f;
                float y = Mathf.Cos(polar) * 0.5f;
                for (int longitude = 0; longitude <= longitudeSegments; longitude++)
                {
                    float azimuth = longitude / (float)longitudeSegments * Mathf.PI * 2f;
                    vertices[vertexIndex++] = new Vector3(
                        Mathf.Cos(azimuth) * ringRadius,
                        y,
                        Mathf.Sin(azimuth) * ringRadius);
                }
            }

            int triangleIndex = 0;
            for (int latitude = 0; latitude < latitudeSegments; latitude++)
            {
                for (int longitude = 0; longitude < longitudeSegments; longitude++)
                {
                    int a = latitude * stride + longitude;
                    int b = a + stride;
                    triangles[triangleIndex++] = a;
                    triangles[triangleIndex++] = a + 1;
                    triangles[triangleIndex++] = b;
                    triangles[triangleIndex++] = a + 1;
                    triangles[triangleIndex++] = b + 1;
                    triangles[triangleIndex++] = b;
                }
            }

            _orbMesh = new Mesh { name = "Runtime_ProjectileSphere", hideFlags = HideFlags.DontSave };
            _orbMesh.vertices = vertices;
            _orbMesh.triangles = triangles;
            _orbMesh.RecalculateNormals();
            _orbMesh.RecalculateBounds();
            _orbMesh.UploadMeshData(true);
            return _orbMesh;
        }

        private static Mesh GetTrailMesh()
        {
            if (_trailMesh != null) return _trailMesh;

            const int segments = 12;
            const float frontY = -0.30f;
            const float frontRadius = 0.28f;
            const float tailY = -1.55f;
            Vector3[] vertices = new Vector3[segments + 2];
            int[] triangles = new int[segments * 6];
            vertices[0] = new Vector3(0f, tailY, 0f);
            vertices[1] = new Vector3(0f, frontY, 0f);
            for (int segment = 0; segment < segments; segment++)
            {
                float angle = segment / (float)segments * Mathf.PI * 2f;
                vertices[segment + 2] = new Vector3(
                    Mathf.Cos(angle) * frontRadius,
                    frontY,
                    Mathf.Sin(angle) * frontRadius);
            }

            int triangle = 0;
            for (int segment = 0; segment < segments; segment++)
            {
                int current = segment + 2;
                int next = (segment + 1) % segments + 2;
                // Cone side, tapered to a point behind the projectile.
                triangles[triangle++] = 0;
                triangles[triangle++] = current;
                triangles[triangle++] = next;
                // Front cap sits inside the orb and prevents a hollow silhouette at oblique aim.
                triangles[triangle++] = 1;
                triangles[triangle++] = next;
                triangles[triangle++] = current;
            }

            _trailMesh = new Mesh { name = "Runtime_ProjectileTrailCone", hideFlags = HideFlags.DontSave };
            _trailMesh.vertices = vertices;
            _trailMesh.triangles = triangles;
            _trailMesh.RecalculateNormals();
            _trailMesh.RecalculateBounds();
            _trailMesh.UploadMeshData(true);
            return _trailMesh;
        }
    }
}
