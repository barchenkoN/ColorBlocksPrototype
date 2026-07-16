using System;
using System.Collections.Generic;
using ColorBlocks.Core;
using UnityEngine;

namespace ColorBlocks.Presentation
{
    public sealed class VisualTheme : IDisposable
    {
        private readonly Dictionary<BlockColorId, Material> _body = new();
        private readonly Dictionary<BlockColorId, Material> _face = new();
        private readonly Dictionary<BlockColorId, Material> _projectile = new();
        private readonly List<Material> _owned = new();
        private readonly Material _litTemplate;
        private readonly Material _unlitTemplate;

        public VisualTheme(PresentationAssets assets)
        {
            if (assets == null) throw new ArgumentNullException(nameof(assets));
            if (!assets.IsValid(out string reason))
            {
                throw new InvalidOperationException($"Presentation assets are invalid: {reason}");
            }

            Assets = assets;
            _litTemplate = assets.LitTemplate;
            _unlitTemplate = assets.UnlitTemplate;

            foreach (BlockColorId id in Enum.GetValues(typeof(BlockColorId)))
            {
                _body[id] = CreateLit($"{id}_Body", ColorPalette.Get(id), 0.28f);
                _face[id] = CreateLit($"{id}_Face", ColorPalette.GetLight(id), 0.40f);
                _projectile[id] = CreateUnlit($"{id}_Projectile", ColorPalette.GetLight(id));
            }

            Board = CreateLit("Board", new Color(0.045f, 0.10f, 0.20f), 0.30f);
            BoardInner = CreateLit("BoardInner", new Color(0.075f, 0.16f, 0.29f), 0.18f);
            Slot = CreateLit("Slot", new Color(0.045f, 0.10f, 0.20f), 0.34f);
            SlotInner = CreateLit("SlotInner", new Color(0.29f, 0.39f, 0.57f), 0.16f);
            WhiteUnlit = CreateUnlit("WhiteUnlit", Color.white);
        }

        public PresentationAssets Assets { get; }
        public Material Board { get; }
        public Material BoardInner { get; }
        public Material Slot { get; }
        public Material SlotInner { get; }
        public Material WhiteUnlit { get; }

        public Material Body(BlockColorId id) => _body[id];
        public Material Face(BlockColorId id) => _face[id];
        public Material Projectile(BlockColorId id) => _projectile[id];

        public void Dispose()
        {
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i] != null) UnityEngine.Object.Destroy(_owned[i]);
            }
        }

        private Material CreateLit(string materialName, Color color, float smoothness)
        {
            Material material = new(_litTemplate) { name = materialName, color = color, hideFlags = HideFlags.DontSave };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            material.enableInstancing = true;
            _owned.Add(material);
            return material;
        }

        private Material CreateUnlit(string materialName, Color color)
        {
            Material material = new(_unlitTemplate) { name = materialName, color = color, hideFlags = HideFlags.DontSave };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            material.enableInstancing = true;
            _owned.Add(material);
            return material;
        }

    }
}
