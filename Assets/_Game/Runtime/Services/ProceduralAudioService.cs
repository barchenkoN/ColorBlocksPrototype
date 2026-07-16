using System.Collections.Generic;
using ColorBlocks.Presentation;
using UnityEngine;

namespace ColorBlocks.Services
{
    /// <summary>
    /// Small capped one-shot audio service. The historical class name is retained so scene-facing
    /// code does not churn, but clips are now imported, licensed assets rather than runtime tones.
    /// </summary>
    public sealed class ProceduralAudioService
    {
        private readonly GameObject _root;
        private readonly AudioSource[] _sources;
        private readonly PresentationAssets _assets;
        private int _sourceIndex;
        private int _impactIndex;

        public ProceduralAudioService(PresentationAssets assets)
        {
            _assets = assets != null ? assets : throw new System.ArgumentNullException(nameof(assets));
            _root = new GameObject("GameAudio");
            _sources = new AudioSource[8];
            for (int i = 0; i < _sources.Length; i++)
            {
                AudioSource source = _root.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
                source.loop = false;
                _sources[i] = source;
            }
        }

        public void Tap() => Play(_assets.Select, Random.Range(0.98f, 1.04f), 0.72f);
        public void Shot() => Play(_assets.Shot, Random.Range(0.96f, 1.08f), 0.16f);
        public void Land() => Play(_assets.UnitLand, Random.Range(0.98f, 1.03f), 0.46f);
        public void Exit() => Play(_assets.UnitExit, Random.Range(0.98f, 1.04f), 0.34f);

        public void Impact()
        {
            IReadOnlyList<AudioClip> clips = _assets.Impacts;
            if (clips.Count == 0) return;
            AudioClip clip = clips[_impactIndex++ % clips.Count];
            Play(clip, Random.Range(0.92f, 1.08f), 0.52f);
        }

        // Kept as a compatibility no-op while call sites migrate to the single, cleaner impact voice.
        public void DestroyBlock() { }

        public void Win() => Play(_assets.Win, 1f, 0.78f);
        public void Loss() => Play(_assets.Loss, 0.96f, 0.70f);

        public void Destroy()
        {
            if (_root != null) Object.Destroy(_root);
        }

        private void Play(AudioClip clip, float pitch, float volume)
        {
            if (clip == null) return;

            AudioSource source = AcquireSource();
            source.pitch = pitch;
            source.PlayOneShot(clip, volume);
        }

        private AudioSource AcquireSource()
        {
            for (int i = 0; i < _sources.Length; i++)
            {
                int index = (_sourceIndex + i) % _sources.Length;
                if (!_sources[index].isPlaying)
                {
                    _sourceIndex = (index + 1) % _sources.Length;
                    return _sources[index];
                }
            }

            AudioSource fallback = _sources[_sourceIndex];
            _sourceIndex = (_sourceIndex + 1) % _sources.Length;
            return fallback;
        }
    }
}
