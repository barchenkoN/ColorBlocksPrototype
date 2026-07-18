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
        private readonly Material _projectileLitTemplate;
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
            _projectileLitTemplate = assets.ProjectileLitTemplate;
            _unlitTemplate = assets.UnlitTemplate;

            foreach (BlockColorId id in Enum.GetValues(typeof(BlockColorId)))
            {
                _body[id] = CreateLit($"{id}_Body", ColorPalette.Get(id), 0.42f);
                _face[id] = CreateLit($"{id}_Face", ColorPalette.GetLight(id), 0.54f);
                Color projectileColor = ColorPalette.GetLight(id);
                Material projectile = CreateLit(
                    _projectileLitTemplate,
                    $"{id}_Projectile",
                    projectileColor,
                    0.58f);
                if (projectile.HasProperty("_EmissionColor"))
                {
                    projectile.SetColor("_EmissionColor", projectileColor * 0.32f);
                }
                _projectile[id] = projectile;
            }

            Color neutralProjectileColor = new(0.94f, 0.97f, 1.0f);
            ProjectileNeutral = CreateLit(
                _projectileLitTemplate,
                "Projectile_Neutral",
                neutralProjectileColor,
                0.62f);
            if (ProjectileNeutral.HasProperty("_EmissionColor"))
            {
                ProjectileNeutral.SetColor("_EmissionColor", neutralProjectileColor * 0.42f);
            }

            // Calibrated against the supplied 1170x2532 frame after Mobile URP lighting:
            // backdrop ~99/125/174, frame ~42/64/103, inner field ~48/70/109.
            Backdrop = CreateLit("Backdrop", new Color(0.365f, 0.50f, 0.75f), 0.12f);
            Board = CreateLit("Board", new Color(0.055f, 0.195f, 0.42f), 0.34f);
            BoardInner = CreateLit("BoardInner", new Color(0.085f, 0.23f, 0.45f), 0.22f);
            Slot = CreateLit("Slot", new Color(0.055f, 0.195f, 0.42f), 0.38f);
            SlotInner = CreateLit("SlotInner", new Color(0.27f, 0.41f, 0.68f), 0.20f);
            WhiteUnlit = CreateUnlit("WhiteUnlit", Color.white);
        }

        public PresentationAssets Assets { get; }
        public Material Backdrop { get; }
        public Material Board { get; }
        public Material BoardInner { get; }
        public Material Slot { get; }
        public Material SlotInner { get; }
        public Material WhiteUnlit { get; }
        public Material ProjectileNeutral { get; }

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
            return CreateLit(_litTemplate, materialName, color, smoothness);
        }

        private Material CreateLit(Material template, string materialName, Color color, float smoothness)
        {
            Material material = new(template) { name = materialName, color = color, hideFlags = HideFlags.DontSave };
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
