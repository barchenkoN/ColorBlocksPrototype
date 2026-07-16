using System;
using System.Collections.Generic;
using System.Reflection;
using ColorBlocks.Core;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlocks.Tests
{
    public sealed class LevelValidatorTests
    {
        [Test]
        public void EmptyBoardAndWrongLaneCount_AreRejected()
        {
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.Configure(
                1,
                2,
                2,
                Array.Empty<CellDefinition>(),
                new[] { Lane(), Lane(), Lane(), Lane() });

            string report = string.Join("\n", LevelValidator.Validate(level));

            Assert.That(report, Does.Contain("Board must contain at least one non-empty cell"));
            Assert.That(report, Does.Contain("Exactly five unit lanes"));
            UnityEngine.Object.DestroyImmediate(level);
        }

        [Test]
        public void NullCollectionsAndNestedEntries_AreReportedWithoutThrowing()
        {
            LevelDefinition nullCollections = ScriptableObject.CreateInstance<LevelDefinition>();
            SetField(nullCollections, "cells", null);
            SetField(nullCollections, "lanes", null);

            string rootReport = string.Join("\n", LevelValidator.Validate(nullCollections));
            Assert.That(rootReport, Does.Contain("Board cell collection is null"));
            Assert.That(rootReport, Does.Contain("lane collection is null"));

            CellDefinition nullLayers = new(0, 0, new[] { BlockColorId.Red });
            SetField(nullLayers, "layers", null);
            UnitLaneDefinition nullUnits = Lane();
            SetField(nullUnits, "units", null);
            LevelDefinition nested = ScriptableObject.CreateInstance<LevelDefinition>();
            nested.Configure(
                1,
                2,
                2,
                new CellDefinition[] { null, nullLayers },
                new[]
                {
                    null,
                    nullUnits,
                    new UnitLaneDefinition(new UnitDefinition[] { null }),
                    Lane(),
                    Lane()
                });

            string nestedReport = string.Join("\n", LevelValidator.Validate(nested));
            Assert.That(nestedReport, Does.Contain("Cell entry 1 is null"));
            Assert.That(nestedReport, Does.Contain("null layer collection"));
            Assert.That(nestedReport, Does.Contain("Lane 1 is null"));
            Assert.That(nestedReport, Does.Contain("Lane 2 has a null unit collection"));
            Assert.That(nestedReport, Does.Contain("Lane 3, unit 1 is null"));

            UnityEngine.Object.DestroyImmediate(nullCollections);
            UnityEngine.Object.DestroyImmediate(nested);
        }

        [Test]
        public void InvalidCoordinatesStacksEnumsAndAmmo_AreRejected()
        {
            BlockColorId invalidColor = (BlockColorId)99;
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.Configure(
                1,
                2,
                2,
                new[]
                {
                    new CellDefinition(0, 0, Array.Empty<BlockColorId>()),
                    new CellDefinition(0, 0, new[] { invalidColor }),
                    new CellDefinition(2, -1, new[] { BlockColorId.Red })
                },
                new[]
                {
                    Lane(new UnitDefinition(invalidColor, 0)),
                    Lane(),
                    Lane(),
                    Lane(),
                    Lane()
                });

            string report = string.Join("\n", LevelValidator.Validate(level));

            Assert.That(report, Does.Contain("has no layers"));
            Assert.That(report, Does.Contain("Duplicate cell"));
            Assert.That(report, Does.Contain("outside the board"));
            Assert.That(report, Does.Contain("invalid color value 99"));
            Assert.That(report, Does.Contain("must have positive charges"));
            UnityEngine.Object.DestroyImmediate(level);
        }

        [Test]
        public void AuthoringValidation_RejectsAChargeBalancedButUnsolvableQueue()
        {
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.Configure(
                1,
                1,
                1,
                new[] { new CellDefinition(0, 0, new[] { BlockColorId.Red }) },
                new[]
                {
                    Lane(
                        Unit(BlockColorId.Blue),
                        Unit(BlockColorId.Blue),
                        Unit(BlockColorId.Blue),
                        Unit(BlockColorId.Blue),
                        Unit(BlockColorId.Blue),
                        Unit(BlockColorId.Red)),
                    Lane(),
                    Lane(),
                    Lane(),
                    Lane()
                });

            Assert.That(LevelValidator.Validate(level), Is.Empty,
                "Fast runtime validation should remain structural only.");
            string authoringReport = string.Join("\n", LevelValidator.ValidateForAuthoring(level));
            Assert.That(authoringReport, Does.Contain("No legal sequence"));
            UnityEngine.Object.DestroyImmediate(level);
        }

        private static UnitLaneDefinition Lane(params UnitDefinition[] units)
        {
            return new UnitLaneDefinition(units);
        }

        private static UnitDefinition Unit(BlockColorId color, int charges = 1)
        {
            return new UnitDefinition(color, charges);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field {fieldName}.");
            field.SetValue(target, value);
        }
    }
}
