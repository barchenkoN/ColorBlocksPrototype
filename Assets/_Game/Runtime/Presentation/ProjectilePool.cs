using System;
using System.Collections;
using System.Collections.Generic;
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
            Vector3 start,
            Func<Vector3> targetProvider,
            float duration,
            Action completed)
        {
            Projectile projectile = Acquire();
            projectile.Busy = true;
            projectile.Renderer.sharedMaterial = _theme.WhiteUnlit;
            projectile.TracerRenderer.sharedMaterial = _theme.WhiteUnlit;
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
            tracerObject.transform.localPosition = new Vector3(0f, -0.28f, 0.04f);
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

            const int segments = 20;
            Vector3[] vertices = new Vector3[segments + 1];
            Vector3[] normals = new Vector3[segments + 1];
            int[] triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            normals[0] = Vector3.back;
            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 0.5f;
                normals[i + 1] = Vector3.back;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (i + 1) % segments + 1;
                triangles[i * 3 + 2] = i + 1;
            }

            _orbMesh = new Mesh { name = "Runtime_ProjectileOrb", hideFlags = HideFlags.DontSave };
            _orbMesh.vertices = vertices;
            _orbMesh.normals = normals;
            _orbMesh.triangles = triangles;
            _orbMesh.RecalculateBounds();
            _orbMesh.UploadMeshData(true);
            return _orbMesh;
        }

        private static Mesh GetTrailMesh()
        {
            if (_trailMesh != null) return _trailMesh;

            _trailMesh = new Mesh { name = "Runtime_ProjectileTrail", hideFlags = HideFlags.DontSave };
            _trailMesh.vertices = new[]
            {
                new Vector3(-0.24f, 0.12f, 0f),
                new Vector3(0.24f, 0.12f, 0f),
                new Vector3(0f, -1.80f, 0f)
            };
            _trailMesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back };
            _trailMesh.triangles = new[] { 0, 1, 2 };
            _trailMesh.RecalculateBounds();
            _trailMesh.UploadMeshData(true);
            return _trailMesh;
        }
    }
}
