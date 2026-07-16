using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColorBlocks.Core
{
    [CreateAssetMenu(fileName = "LevelCatalog", menuName = "Color Blocks/Level Catalog")]
    public sealed class LevelCatalog : ScriptableObject
    {
        [SerializeField] private List<LevelDefinition> levels = new();

        public IReadOnlyList<LevelDefinition> Levels => levels;

        public LevelDefinition GetLevel(int levelNumber)
        {
            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i] != null && levels[i].LevelNumber == levelNumber)
                {
                    return levels[i];
                }
            }

            throw new InvalidOperationException($"Level {levelNumber} is missing from the catalog.");
        }

#if UNITY_EDITOR
        public void Configure(IEnumerable<LevelDefinition> definitions)
        {
            levels = new List<LevelDefinition>(definitions);
        }
#endif
    }
}
