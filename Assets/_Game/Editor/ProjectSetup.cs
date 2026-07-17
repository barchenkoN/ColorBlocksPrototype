using System;
using System.Collections.Generic;
using System.IO;
using ColorBlocks.Bootstrap;
using ColorBlocks.Core;
using ColorBlocks.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColorBlocks.Editor
{
    public static class ProjectSetup
    {
        private const string GameRoot = "Assets/_Game";
        private const string ResourcesPath = GameRoot + "/Resources";
        private const string LevelsPath = ResourcesPath + "/Levels";
        private const string ScenePath = GameRoot + "/Scenes/Game.unity";
        private const string MaterialsPath = GameRoot + "/Art/Materials";
        private const string FredokaFontPath = GameRoot + "/Art/Fonts/Fredoka-Variable.ttf";
        private const string FredokaTmpPath = GameRoot + "/Art/Fonts/Fredoka-SDF.asset";
        private const string AudioPath = GameRoot + "/Audio/Kenney";

        [MenuItem("Color Blocks/Prepare Primary Font Asset")]
        public static void PreparePrimaryFontAsset()
        {
            TMP_FontAsset tmpFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FredokaTmpPath);
            if (tmpFont == null)
            {
                throw new InvalidOperationException(
                    $"Required TMP font asset is missing at {FredokaTmpPath}. Rebuild prototype content first.");
            }

            BakeHudCharacters(tmpFont);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Fredoka TMP asset prepared with persistent printable ASCII HUD glyphs.");
        }

        [MenuItem("Color Blocks/Rebuild Prototype Content")]
        public static void RebuildPrototypeContent()
        {
            EnsureFolder(GameRoot);
            EnsureFolder(ResourcesPath);
            EnsureFolder(LevelsPath);
            EnsureFolder(GameRoot + "/Scenes");
            EnsureFolder(MaterialsPath);

            CreateOrUpdatePresentationAssets();

            List<LevelDefinition> levels = new()
            {
                CreateOrUpdateLevel(1, 8, 7, CreateLevelOne(), 10),
                CreateOrUpdateLevel(2, 9, 7, CreateLevelTwo(), 10, true),
                CreateOrUpdateLevel(3, 9, 8, CreateLevelThree(), 10, true),
                CreateOrUpdateLevel(4, 10, 8, CreateLevelFour(), 10),
                CreateOrUpdateLevel(5, 10, 10, CreateLevelFive(), 10)
            };

            LevelCatalog catalog = LoadOrCreate<LevelCatalog>(ResourcesPath + "/LevelCatalog.asset");
            catalog.Configure(levels);
            EditorUtility.SetDirty(catalog);

            ValidateAll(levels);
            CreateGameplayScene();
            ConfigureProjectSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Color Blocks prototype content rebuilt successfully: 5 valid levels and Game scene.");
        }

        [MenuItem("Color Blocks/Log Level Diagnostics")]
        public static void LogLevelDiagnostics()
        {
            LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(ResourcesPath + "/LevelCatalog.asset");
            if (catalog == null) throw new InvalidOperationException("LevelCatalog has not been generated.");

            for (int i = 0; i < catalog.Levels.Count; i++)
            {
                LevelDefinition level = catalog.Levels[i];
                LevelSolveResult solve = LevelSolver.Solve(level);
                int unitCount = 0;
                for (int lane = 0; lane < level.Lanes.Count; lane++) unitCount += level.Lanes[lane].Units.Count;
                Debug.Log(
                    $"[LevelDiagnostics] L{level.LevelNumber}: {level.Width}x{level.Height}, " +
                    $"{CountBlocks(level)} block layers, {unitCount} units, {solve.ExploredStateCount} states, " +
                    $"win=[{string.Join(",", solve.WinningLaneChoices)}], " +
                    $"losingPath={(solve.HasLosingPath ? $"[{string.Join(",", solve.FirstLosingLaneChoices)}]" : "none discovered")}.");
            }
        }

        private static void CreateOrUpdatePresentationAssets()
        {
            TMP_Settings tmpSettings = EnsureTmpEssentialResources();

            Material lit = LoadOrCreateMaterial(
                MaterialsPath + "/RuntimeLitTemplate.mat",
                "Universal Render Pipeline/Lit");
            Material projectileLit = LoadOrCreateMaterial(
                MaterialsPath + "/RuntimeProjectileLitTemplate.mat",
                "Universal Render Pipeline/Lit");
            projectileLit.EnableKeyword("_EMISSION");
            if (projectileLit.HasProperty("_EmissionColor"))
            {
                projectileLit.SetColor("_EmissionColor", Color.white * 0.32f);
            }
            EditorUtility.SetDirty(projectileLit);
            Material unlit = LoadOrCreateMaterial(
                MaterialsPath + "/RuntimeUnlitTemplate.mat",
                "Universal Render Pipeline/Unlit");

            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FredokaFontPath);
            if (sourceFont == null)
            {
                throw new InvalidOperationException(
                    $"Required OFL font is missing at {FredokaFontPath}. Restore the project asset before rebuilding content.");
            }

            TMP_FontAsset tmpFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FredokaTmpPath);
            if (tmpFont == null)
            {
                tmpFont = TMP_FontAsset.CreateFontAsset(sourceFont);
                if (tmpFont == null) throw new InvalidOperationException("Unable to create the Fredoka TMP font asset.");
                tmpFont.name = "Fredoka SDF";
                AssetDatabase.CreateAsset(tmpFont, FredokaTmpPath);
                if (tmpFont.atlasTextures != null && tmpFont.atlasTextures.Length > 0 && tmpFont.atlasTextures[0] != null)
                {
                    AssetDatabase.AddObjectToAsset(tmpFont.atlasTextures[0], tmpFont);
                }
                if (tmpFont.material != null) AssetDatabase.AddObjectToAsset(tmpFont.material, tmpFont);
                EditorUtility.SetDirty(tmpFont);
            }

            BakeHudCharacters(tmpFont);

            SerializedObject settingsObject = new(tmpSettings);
            SerializedProperty version = settingsObject.FindProperty("assetVersion");
            if (version != null) version.stringValue = "2";
            SerializedProperty defaultFont = settingsObject.FindProperty("m_defaultFontAsset");
            if (defaultFont != null) defaultFont.objectReferenceValue = tmpFont;
            SerializedProperty defaultPath = settingsObject.FindProperty("m_defaultFontAssetPath");
            if (defaultPath != null) defaultPath.stringValue = string.Empty;
            settingsObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tmpSettings);

            PresentationAssets assets = LoadOrCreate<PresentationAssets>(ResourcesPath + "/PresentationAssets.asset");
            assets.Configure(
                lit,
                projectileLit,
                unlit,
                tmpFont,
                LoadAudio("select_006.ogg"),
                LoadAudio("tick_002.ogg"),
                LoadAudio("drop_003.ogg"),
                LoadAudio("close_002.ogg"),
                LoadAudio("confirmation_002.ogg"),
                LoadAudio("error_006.ogg"),
                new[]
                {
                    LoadAudio("impactSoft_medium_000.ogg"),
                    LoadAudio("impactSoft_medium_001.ogg"),
                    LoadAudio("impactSoft_medium_002.ogg")
                });
            EditorUtility.SetDirty(assets);
        }

        private static void BakeHudCharacters(TMP_FontAsset tmpFont)
        {
            // A dynamic TMP asset normally clears its generated glyph data during a player
            // build. The HUD must render on the very first iPhone frame, so persist the full
            // printable ASCII set used by every current and diagnostic UI label.
            SerializedObject fontObject = new(tmpFont);
            SerializedProperty clearDynamicData = fontObject.FindProperty("m_ClearDynamicDataOnBuild");
            if (clearDynamicData != null) clearDynamicData.boolValue = false;
            fontObject.ApplyModifiedPropertiesWithoutUndo();

            char[] printableAscii = new char[95];
            for (int index = 0; index < printableAscii.Length; index++)
            {
                printableAscii[index] = (char)(32 + index);
            }

            if (!tmpFont.TryAddCharacters(new string(printableAscii), out string missingCharacters) &&
                !string.IsNullOrEmpty(missingCharacters))
            {
                throw new InvalidOperationException(
                    $"Fredoka TMP asset cannot provide required HUD characters: {missingCharacters}");
            }

            EditorUtility.SetDirty(tmpFont);
            if (tmpFont.atlasTextures != null)
            {
                for (int index = 0; index < tmpFont.atlasTextures.Length; index++)
                {
                    if (tmpFont.atlasTextures[index] != null)
                    {
                        EditorUtility.SetDirty(tmpFont.atlasTextures[index]);
                    }
                }
            }

            if (tmpFont.material != null) EditorUtility.SetDirty(tmpFont.material);
        }

        private static TMP_Settings EnsureTmpEssentialResources()
        {
            const string canonicalSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
            bool missingSettings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(canonicalSettingsPath) == null;
            bool missingShader = Shader.Find("TextMeshPro/Mobile/Distance Field") == null;
            if (missingSettings || missingShader)
            {
                // Remove the temporary fallback created by earlier prototype iterations so only one
                // Resources/TMP Settings asset can ever exist.
                AssetDatabase.DeleteAsset(ResourcesPath + "/TMP Settings.asset");

                UnityEditor.PackageManager.PackageInfo package =
                    UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_Settings).Assembly);
                if (package == null) throw new InvalidOperationException("Unable to locate the installed uGUI/TMP package.");
                string essentials = Path.Combine(
                    package.resolvedPath,
                    "Package Resources",
                    "TMP Essential Resources.unitypackage");
                if (!File.Exists(essentials))
                {
                    throw new FileNotFoundException("TMP Essential Resources package is missing.", essentials);
                }

                AssetDatabase.ImportPackage(essentials, false);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            TMP_Settings settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(canonicalSettingsPath);
            if (settings == null || Shader.Find("TextMeshPro/Mobile/Distance Field") == null)
            {
                throw new InvalidOperationException("TMP Essential Resources failed to import.");
            }

            return settings;
        }

        private static Material LoadOrCreateMaterial(string path, string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException($"Required shader was not found: {shaderName}");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.enableInstancing = true;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static AudioClip LoadAudio(string fileName)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioPath}/{fileName}");
            if (clip == null) throw new InvalidOperationException($"Required audio clip is missing: {fileName}");
            return clip;
        }

        private static LevelDefinition CreateOrUpdateLevel(
            int number,
            int width,
            int height,
            List<CellDefinition> cells,
            int maxChargesPerUnit,
            bool stagedRedOpening = false)
        {
            LevelDefinition level = LoadOrCreate<LevelDefinition>($"{LevelsPath}/Level{number:00}.asset");
            List<UnitLaneDefinition> lanes = stagedRedOpening
                ? BuildStagedOpeningLanes(cells, maxChargesPerUnit, BlockColorId.Red, 9)
                : BuildBalancedLanes(cells, maxChargesPerUnit);
            level.Configure(number, width, height, cells, lanes);
            EditorUtility.SetDirty(level);
            return level;
        }

        private static List<CellDefinition> CreateLevelOne()
        {
            BlockColorId[] colors = { BlockColorId.Red, BlockColorId.Blue, BlockColorId.Yellow };
            List<CellDefinition> cells = new();
            int[] heights = { 5, 6, 7, 7, 7, 7, 6, 5 };
            for (int x = 0; x < heights.Length; x++)
            {
                for (int y = 0; y < heights[x]; y++)
                {
                    cells.Add(Cell(x, y, colors[(x + y * 2) % colors.Length]));
                }
            }
            return cells;
        }

        private static List<CellDefinition> CreateLevelTwo()
        {
            List<CellDefinition> cells = new();
            BlockColorId[] nonOpening = { BlockColorId.Blue, BlockColorId.Yellow, BlockColorId.Green };
            int[] heights = { 5, 6, 7, 7, 7, 7, 7, 6, 5 };
            for (int x = 0; x < heights.Length; x++)
            {
                for (int y = 0; y < heights[x]; y++)
                {
                    BlockColorId color = y == 0
                        ? BlockColorId.Red
                        : nonOpening[(x + y + y * y) % nonOpening.Length];
                    cells.Add(Cell(x, y, color));
                }
            }
            return cells;
        }

        private static List<CellDefinition> CreateLevelThree()
        {
            BlockColorId[] colors =
            {
                BlockColorId.Red, BlockColorId.Blue, BlockColorId.Yellow, BlockColorId.Green
            };
            List<CellDefinition> cells = new();
            int[] heights = { 5, 7, 8, 8, 8, 8, 8, 7, 5 };
            for (int x = 0; x < heights.Length; x++)
            {
                for (int y = 0; y < heights[x]; y++)
                {
                    bool isOpeningFrontier = y == 0;
                    BlockColorId top = isOpeningFrontier
                        ? BlockColorId.Red
                        : colors[1 + (x + y * 2) % 3];
                    bool doubleLayer = (y == 0 && x >= 1 && x <= 7) ||
                                       (y == 2 && (x == 2 || x == 4 || x == 6)) ||
                                       (y == 4 && x >= 3 && x <= 5);
                    if (!doubleLayer)
                    {
                        cells.Add(Cell(x, y, top));
                        continue;
                    }

                    BlockColorId bottom = y == 0
                        ? colors[1 + (x + 2) % 3]
                        : colors[1 + ((int)top - 1 + 1) % 3];
                    cells.Add(Cell(x, y, bottom, top));
                }
            }
            return cells;
        }

        private static List<CellDefinition> CreateLevelFour()
        {
            BlockColorId[] colors =
            {
                BlockColorId.Red, BlockColorId.Blue, BlockColorId.Yellow,
                BlockColorId.Green, BlockColorId.Purple
            };
            List<CellDefinition> cells = new();
            int[] heights = { 5, 7, 8, 8, 8, 8, 8, 8, 7, 5 };
            for (int x = 0; x < heights.Length; x++)
            {
                for (int y = 0; y < heights[x]; y++)
                {
                    BlockColorId top = colors[(x * 2 + y * 3) % colors.Length];
                    bool doubleLayer = y == 0 ||
                                       (y == 2 && (x == 2 || x == 4 || x == 6 || x == 8)) ||
                                       (y == 4 && x >= 3 && x <= 6);
                    cells.Add(doubleLayer
                        ? Cell(x, y, colors[((int)top + 1 + y % 4) % colors.Length], top)
                        : Cell(x, y, top));
                }
            }
            return cells;
        }

        private static List<CellDefinition> CreateLevelFive()
        {
            BlockColorId[] colors =
            {
                BlockColorId.Red, BlockColorId.Blue, BlockColorId.Yellow,
                BlockColorId.Green, BlockColorId.Purple
            };
            List<CellDefinition> cells = new();
            int[] heights = { 7, 9, 10, 10, 10, 10, 10, 10, 9, 7 };
            for (int x = 0; x < heights.Length; x++)
            {
                for (int y = 0; y < heights[x]; y++)
                {
                    BlockColorId top = colors[(x * 3 + y * 2) % colors.Length];
                    bool doubleLayer = y == 0 ||
                                       (y == 2 && x >= 1 && x <= 8) ||
                                       (y == 4 && x >= 2 && x <= 7) ||
                                       (y == 6 && x >= 3 && x <= 6) ||
                                       (y == 8 && x >= 4 && x <= 5);
                    cells.Add(doubleLayer
                        ? Cell(x, y, colors[((int)top + 1 + (y + 1) % 4) % colors.Length], top)
                        : Cell(x, y, top));
                }
            }
            return cells;
        }

        private static CellDefinition Cell(int x, int y, params BlockColorId[] bottomToTop)
        {
            return new CellDefinition(x, y, bottomToTop);
        }

        private static void AddColumn(List<CellDefinition> cells, int x, params BlockColorId[] bottomToTopByHeight)
        {
            for (int y = 0; y < bottomToTopByHeight.Length; y++)
            {
                cells.Add(Cell(x, y, bottomToTopByHeight[y]));
            }
        }

        private static List<UnitLaneDefinition> BuildStagedOpeningLanes(
            IReadOnlyList<CellDefinition> cells,
            int maxChargesPerUnit,
            BlockColorId openingColor,
            int openingCharges)
        {
            Dictionary<BlockColorId, int> remaining = CountColors(cells);
            if (!remaining.TryGetValue(openingColor, out int openingAvailable) || openingAvailable < openingCharges)
            {
                throw new InvalidOperationException($"Staged opening needs {openingCharges} {openingColor} blocks.");
            }

            List<UnitDefinition>[] lanes = new List<UnitDefinition>[5];
            for (int lane = 0; lane < lanes.Length; lane++) lanes[lane] = new List<UnitDefinition>();
            lanes[0].Add(new UnitDefinition(openingColor, openingCharges));
            remaining[openingColor] -= openingCharges;

            List<BlockColorId> blockers = new();
            foreach (BlockColorId color in Enum.GetValues(typeof(BlockColorId)))
            {
                if (color != openingColor && remaining.TryGetValue(color, out int count) && count > 0) blockers.Add(color);
            }
            if (blockers.Count == 0) throw new InvalidOperationException("Staged opening requires at least one blocker color.");

            for (int lane = 1; lane < lanes.Length; lane++)
            {
                BlockColorId color = blockers[(lane - 1) % blockers.Count];
                int charges = Math.Min(maxChargesPerUnit, remaining[color]);
                if (charges <= 0)
                {
                    color = FindColorWithRemaining(remaining, openingColor);
                    charges = Math.Min(maxChargesPerUnit, remaining[color]);
                }
                lanes[lane].Add(new UnitDefinition(color, charges));
                remaining[color] -= charges;
            }

            int destinationLane = 0;
            foreach (BlockColorId color in Enum.GetValues(typeof(BlockColorId)))
            {
                if (!remaining.TryGetValue(color, out int count)) continue;
                while (count > 0)
                {
                    int charges = Math.Min(maxChargesPerUnit, count);
                    lanes[destinationLane % lanes.Length].Add(new UnitDefinition(color, charges));
                    destinationLane++;
                    count -= charges;
                }
                remaining[color] = 0;
            }

            List<UnitLaneDefinition> definitions = new(5);
            for (int lane = 0; lane < lanes.Length; lane++) definitions.Add(new UnitLaneDefinition(lanes[lane]));
            return definitions;
        }

        private static List<UnitLaneDefinition> BuildBalancedLanes(
            IReadOnlyList<CellDefinition> cells,
            int maxChargesPerUnit)
        {
            Dictionary<BlockColorId, int> counts = CountColors(cells);

            List<Queue<UnitDefinition>> byColor = new();
            foreach (BlockColorId color in Enum.GetValues(typeof(BlockColorId)))
            {
                if (!counts.TryGetValue(color, out int remaining)) continue;
                Queue<UnitDefinition> units = new();
                while (remaining > 0)
                {
                    int charges = Math.Min(maxChargesPerUnit, remaining);
                    units.Enqueue(new UnitDefinition(color, charges));
                    remaining -= charges;
                }
                byColor.Add(units);
            }

            List<UnitDefinition> interleaved = new();
            bool added;
            do
            {
                added = false;
                for (int color = 0; color < byColor.Count; color++)
                {
                    if (byColor[color].Count == 0) continue;
                    interleaved.Add(byColor[color].Dequeue());
                    added = true;
                }
            } while (added);

            List<UnitDefinition>[] lanes = new List<UnitDefinition>[5];
            for (int lane = 0; lane < lanes.Length; lane++) lanes[lane] = new List<UnitDefinition>();
            for (int unit = 0; unit < interleaved.Count; unit++) lanes[unit % lanes.Length].Add(interleaved[unit]);

            List<UnitLaneDefinition> definitions = new(5);
            for (int lane = 0; lane < lanes.Length; lane++)
            {
                definitions.Add(new UnitLaneDefinition(lanes[lane]));
            }
            return definitions;
        }

        private static Dictionary<BlockColorId, int> CountColors(IReadOnlyList<CellDefinition> cells)
        {
            Dictionary<BlockColorId, int> counts = new();
            for (int cell = 0; cell < cells.Count; cell++)
            {
                for (int layer = 0; layer < cells[cell].Layers.Count; layer++)
                {
                    BlockColorId color = cells[cell].Layers[layer].Color;
                    counts.TryGetValue(color, out int current);
                    counts[color] = current + 1;
                }
            }
            return counts;
        }

        private static BlockColorId FindColorWithRemaining(
            IReadOnlyDictionary<BlockColorId, int> remaining,
            BlockColorId excluded)
        {
            foreach (BlockColorId color in Enum.GetValues(typeof(BlockColorId)))
            {
                if (color != excluded && remaining.TryGetValue(color, out int count) && count > 0) return color;
            }
            throw new InvalidOperationException("No blocker color charges remain.");
        }

        private static void ValidateAll(IReadOnlyList<LevelDefinition> levels)
        {
            for (int i = 0; i < levels.Count; i++)
            {
                List<string> errors = LevelValidator.ValidateForAuthoring(levels[i]);
                if (errors.Count > 0)
                {
                    throw new InvalidOperationException(
                        $"Generated level {levels[i].LevelNumber} is invalid:\n{string.Join("\n", errors)}");
                }
            }
        }

        private static int CountBlocks(LevelDefinition level)
        {
            int count = 0;
            for (int cell = 0; cell < level.Cells.Count; cell++) count += level.Cells[cell].Layers.Count;
            return count;
        }

        private static void CreateGameplayScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject bootstrap = new("Game");
            bootstrap.AddComponent<GameBootstrapper>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static void ConfigureProjectSettings()
        {
            PlayerSettings.companyName = "Nazar Prototype Studio";
            PlayerSettings.productName = "Color Blocks Prototype";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.nazar.colorblocksprototype");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.nazar.colorblocksprototype");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.buildNumber = "1";
            EditorSettings.serializationMode = SerializationMode.ForceText;
            UnityEditor.VersionControlSettings.mode = "Visible Meta Files";
        }

        private static T LoadOrCreate<T>(string assetPath) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset != null) return asset;
            AssetDatabase.DeleteAsset(assetPath);
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);
            return asset;
        }

        private static void EnsureFolder(string assetPath)
        {
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            if (!Directory.Exists(fullPath)) Directory.CreateDirectory(fullPath);
        }
    }
}
