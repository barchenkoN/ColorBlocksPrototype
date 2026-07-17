using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ColorBlocks.Presentation
{
    [CreateAssetMenu(fileName = "PresentationAssets", menuName = "Color Blocks/Presentation Assets")]
    public sealed class PresentationAssets : ScriptableObject
    {
        [Header("Serialized shader anchors")]
        [SerializeField] private Material litTemplate;
        [SerializeField] private Material projectileLitTemplate;
        [SerializeField] private Material unlitTemplate;

        [Header("Typography")]
        [SerializeField] private TMP_FontAsset primaryFont;

        [Header("Reference-matched layout and feel")]
        [SerializeField] private GameFeelProfile feelProfile = new();

        [Header("Audio")]
        [SerializeField] private AudioClip select;
        [SerializeField] private AudioClip shot;
        [SerializeField] private AudioClip unitLand;
        [SerializeField] private AudioClip unitExit;
        [SerializeField] private AudioClip win;
        [SerializeField] private AudioClip loss;
        [SerializeField] private AudioClip[] impacts = Array.Empty<AudioClip>();

        public Material LitTemplate => litTemplate;
        public Material ProjectileLitTemplate => projectileLitTemplate;
        public Material UnlitTemplate => unlitTemplate;
        public TMP_FontAsset PrimaryFont => primaryFont;
        public GameFeelProfile FeelProfile => feelProfile;
        public AudioClip Select => select;
        public AudioClip Shot => shot;
        public AudioClip UnitLand => unitLand;
        public AudioClip UnitExit => unitExit;
        public AudioClip Win => win;
        public AudioClip Loss => loss;
        public IReadOnlyList<AudioClip> Impacts => impacts;

        public bool IsValid(out string reason)
        {
            if (litTemplate == null || litTemplate.shader == null || !litTemplate.shader.isSupported)
            {
                reason = "Lit material template is missing or unsupported.";
                return false;
            }

            if (unlitTemplate == null || unlitTemplate.shader == null || !unlitTemplate.shader.isSupported)
            {
                reason = "Unlit material template is missing or unsupported.";
                return false;
            }

            if (projectileLitTemplate == null ||
                projectileLitTemplate.shader == null ||
                !projectileLitTemplate.shader.isSupported ||
                !projectileLitTemplate.IsKeywordEnabled("_EMISSION"))
            {
                reason = "Projectile Lit material template is missing, unsupported, or lacks its serialized emission variant.";
                return false;
            }

            if (primaryFont == null)
            {
                reason = "Primary TMP font asset is missing.";
                return false;
            }

            if (feelProfile == null)
            {
                reason = "Game feel profile is missing.";
                return false;
            }

            if (feelProfile.BlockDepth <= 0f || feelProfile.HiddenLayerDepth <= 0f)
            {
                reason = "Physical block depth must be positive.";
                return false;
            }

            if (Mathf.Abs(feelProfile.HiddenLayerDepth - feelProfile.BlockDepth) > 0.0001f)
            {
                reason = "Physical stack step must equal block depth so layers neither float nor overlap.";
                return false;
            }

            if (Mathf.Abs(feelProfile.StackLayerStep) > 0.0001f ||
                Mathf.Abs(feelProfile.StackLayerHorizontalStep) > 0.0001f)
            {
                reason = "Physical stack layers must share board-space X/Y; only their Z height may differ.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void Configure(
            Material lit,
            Material projectileLit,
            Material unlit,
            TMP_FontAsset font,
            AudioClip selectClip,
            AudioClip shotClip,
            AudioClip landClip,
            AudioClip exitClip,
            AudioClip winClip,
            AudioClip lossClip,
            AudioClip[] impactClips)
        {
            litTemplate = lit;
            projectileLitTemplate = projectileLit;
            unlitTemplate = unlit;
            primaryFont = font;
            // Canonical rebuilds intentionally refresh every measured layout/feel value.
            feelProfile = new GameFeelProfile();
            select = selectClip;
            shot = shotClip;
            unitLand = landClip;
            unitExit = exitClip;
            win = winClip;
            loss = lossClip;
            impacts = impactClips ?? Array.Empty<AudioClip>();
        }
#endif
    }
}
