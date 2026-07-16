using System.Collections;
using System.Collections.Generic;
using ColorBlocks.Core;
using UnityEngine;

namespace ColorBlocks.Presentation
{
    /// <summary>
    /// Small pooled, deterministic impact burst. A block is replaced by a one-frame flash and
    /// colored shards, keeping the hit readable without allocating ParticleSystems per shot.
    /// </summary>
    public sealed class ImpactFxPool
    {
        private sealed class Burst
        {
            public GameObject Root;
            public GameObject Flash;
            public MeshRenderer[] FragmentRenderers;
            public Transform[] Fragments;
            public Vector3[] Velocities;
            public float[] Spins;
            public Vector3[] BaseScales;
            public bool Busy;
        }

        private readonly List<Burst> _bursts = new();
        private readonly Transform _root;
        private readonly VisualTheme _theme;
        private readonly GameFeelProfile _feel;
        private int _sequence;

        public ImpactFxPool(VisualTheme theme, GameFeelProfile feel, int capacity = 16)
        {
            _theme = theme;
            _feel = feel;
            _root = new GameObject("ImpactFxPool").transform;
            for (int i = 0; i < capacity; i++) _bursts.Add(Create(i));
        }

        public IEnumerator Play(BlockColorId color, Vector3 position)
        {
            Burst burst = Acquire();
            burst.Busy = true;
            burst.Root.transform.position = position + new Vector3(0f, 0f, -0.38f);
            burst.Root.SetActive(true);
            burst.Flash.SetActive(true);

            int sequence = _sequence++;
            int count = Mathf.Min(_feel.FragmentCount, burst.Fragments.Length);
            for (int i = 0; i < burst.Fragments.Length; i++)
            {
                bool active = i < count;
                burst.Fragments[i].gameObject.SetActive(active);
                if (!active) continue;

                float normalized = (i + 0.37f * (sequence % 5)) / Mathf.Max(1f, count);
                float angle = normalized * Mathf.PI * 2f;
                float speedVariation = 0.72f + ((i * 37 + sequence * 17) % 41) / 100f;
                float speed = _feel.FragmentSpeed * speedVariation;
                burst.Velocities[i] = new Vector3(
                    Mathf.Cos(angle) * speed,
                    Mathf.Sin(angle) * speed + 0.42f,
                    0f);
                burst.Spins[i] = ((i & 1) == 0 ? 1f : -1f) * (180f + (i % 4) * 75f);
                float size = 0.075f + (i % 3) * 0.022f;
                burst.BaseScales[i] = new Vector3(size * 1.35f, size, size);
                burst.Fragments[i].localPosition = Vector3.zero;
                burst.Fragments[i].localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
                burst.Fragments[i].localScale = burst.BaseScales[i];
                burst.FragmentRenderers[i].sharedMaterial = (i % 3 == 0)
                    ? _theme.Face(color)
                    : _theme.Body(color);
            }

            float elapsed = 0f;
            float duration = Mathf.Max(_feel.FragmentDuration, _feel.ImpactFlashDuration);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float fragmentT = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, _feel.FragmentDuration));
                for (int i = 0; i < count; i++)
                {
                    float fragmentTime = fragmentT * _feel.FragmentDuration;
                    burst.Fragments[i].localPosition =
                        burst.Velocities[i] * fragmentTime +
                        Vector3.down * (0.5f * _feel.FragmentGravity * fragmentTime * fragmentTime);
                    burst.Fragments[i].localRotation = Quaternion.Euler(
                        0f,
                        0f,
                        burst.Spins[i] * fragmentTime);
                    float fadeScale = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 1f, fragmentT));
                    burst.Fragments[i].localScale = burst.BaseScales[i] * fadeScale;
                }

                float flashT = elapsed / Mathf.Max(0.001f, _feel.ImpactFlashDuration);
                if (flashT < 1f)
                {
                    float flashScale = Mathf.Sin(flashT * Mathf.PI) * 0.76f;
                    burst.Flash.transform.localScale = new Vector3(flashScale, flashScale, 0.08f);
                }
                else
                {
                    burst.Flash.SetActive(false);
                }

                yield return null;
            }

            burst.Root.SetActive(false);
            burst.Busy = false;
        }

        public void Destroy()
        {
            if (_root != null) Object.Destroy(_root.gameObject);
        }

        private Burst Acquire()
        {
            for (int i = 0; i < _bursts.Count; i++)
            {
                if (!_bursts[i].Busy) return _bursts[i];
            }

            Burst extra = Create(_bursts.Count);
            _bursts.Add(extra);
            return extra;
        }

        private Burst Create(int index)
        {
            GameObject root = new($"Impact_{index}");
            root.transform.SetParent(_root, false);

            GameObject flash = CreatePart("Flash", root.transform, _theme.WhiteUnlit);
            int capacity = Mathf.Max(12, _feel.FragmentCount);
            Transform[] fragments = new Transform[capacity];
            MeshRenderer[] renderers = new MeshRenderer[capacity];
            for (int i = 0; i < capacity; i++)
            {
                GameObject fragment = CreatePart($"Fragment_{i}", root.transform, _theme.WhiteUnlit);
                fragments[i] = fragment.transform;
                renderers[i] = fragment.GetComponent<MeshRenderer>();
            }

            root.SetActive(false);
            return new Burst
            {
                Root = root,
                Flash = flash,
                Fragments = fragments,
                FragmentRenderers = renderers,
                Velocities = new Vector3[capacity],
                Spins = new float[capacity],
                BaseScales = new Vector3[capacity]
            };
        }

        private static GameObject CreatePart(string name, Transform parent, Material material)
        {
            GameObject part = new(name);
            part.transform.SetParent(parent, false);
            MeshFilter filter = part.AddComponent<MeshFilter>();
            filter.sharedMesh = ChamferedCubeMesh.Get();
            MeshRenderer renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return part;
        }
    }
}
