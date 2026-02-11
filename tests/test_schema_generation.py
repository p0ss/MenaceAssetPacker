#!/usr/bin/env python3
"""
Tests for schema generation, validation, and diff tools.

Uses synthetic dump.cs fixtures to verify correctness without needing
the actual game dump.

Usage:
  python -m pytest tests/test_schema_generation.py -v
  python tests/test_schema_generation.py  # standalone
"""

import json
import os
import sys
import tempfile
import unittest
from pathlib import Path

# Add tools directory to path
sys.path.insert(0, str(Path(__file__).parent.parent / "tools"))

import generate_schema
import validate_extraction
import diff_schemas


# ---------------------------------------------------------------------------
# Synthetic dump fixture
# ---------------------------------------------------------------------------

SYNTHETIC_DUMP = r"""
// Namespace:
public class SerializedScriptableObject : ScriptableObject // TypeDefIndex: 100
{
}

// Namespace:
public abstract class DataTemplate : SerializedScriptableObject // TypeDefIndex: 200
{
	// Fields
	private string m_GameDesignComment; // 0x58
	private string m_ID; // 0x68
	private bool m_IsGarbage; // 0x70
	private bool m_IsInitialized; // 0x71

	// Methods
	public string GetID() { }
}

// Namespace:
public abstract class BaseItemTemplate : DataTemplate // TypeDefIndex: 300
{
	// Fields
	public string Title; // 0x78
	public int Weight; // 0x80
	public float Value; // 0x84

	// Methods
	public void .ctor() { }
}

// Namespace:
public class WeaponTemplate : BaseItemTemplate // TypeDefIndex: 301
{
	// Fields
	public int MinRange; // 0x88
	public int MaxRange; // 0x8C
	public float Damage; // 0x90
	public float Accuracy; // 0x94
	public int Ammo; // 0x98
	public bool IsAutomatic; // 0x9C
	public WeaponType WeaponClass; // 0xA0
	public TagTemplate Tags; // 0xA8

	// Methods
	public void .ctor() { }
}

// Namespace:
public class ArmorTemplate : BaseItemTemplate // TypeDefIndex: 302
{
	// Fields
	public int Defense; // 0x88
	public float Coverage; // 0x8C
	public ArmorSlot Slot; // 0x90

	// Methods
	public void .ctor() { }
}

// Namespace:
public abstract class SkillBaseTemplate : DataTemplate // TypeDefIndex: 400
{
	// Fields
	public int ActionPointCost; // 0x78
	public bool IsPassive; // 0x7C

	// Methods
	public void .ctor() { }
}

// Namespace:
public class SkillTemplate : SkillBaseTemplate // TypeDefIndex: 401
{
	// Fields
	public int Cooldown; // 0x80
	public float Range; // 0x84
	public Sprite Icon; // 0x88

	// Methods
	public void .ctor() { }
}

// Namespace:
public class TagTemplate : DataTemplate // TypeDefIndex: 500
{
	// Fields
	public int Priority; // 0x78

	// Methods
	public void .ctor() { }
}

// Namespace:
public enum WeaponType // TypeDefIndex: 600
{
	// Fields
	public int value__; // 0x0
	public const WeaponType Rifle = 0;
	public const WeaponType Pistol = 1;
	public const WeaponType Shotgun = 2;
	public const WeaponType Sniper = 3;
	public const WeaponType Heavy = 4;
}

// Namespace:
public enum ArmorSlot // TypeDefIndex: 601
{
	// Fields
	public int value__; // 0x0
	public const ArmorSlot Head = 0;
	public const ArmorSlot Body = 1;
	public const ArmorSlot Legs = 2;
}

// Namespace:
public struct DamageInfo // TypeDefIndex: 700
{
	// Fields
	public int MinDamage; // 0x0
	public int MaxDamage; // 0x4
	public float CritChance; // 0x8
}
"""


def create_temp_dump(content=None):
    """Create a temp dump.cs file."""
    if content is None:
        content = SYNTHETIC_DUMP
    fd, path = tempfile.mkstemp(suffix=".cs")
    with os.fdopen(fd, "w") as f:
        f.write(content)
    return Path(path)


def create_temp_schema(schema):
    """Write schema to temp file."""
    fd, path = tempfile.mkstemp(suffix=".json")
    with os.fdopen(fd, "w") as f:
        json.dump(schema, f)
    return Path(path)


# ---------------------------------------------------------------------------
# Tests
# ---------------------------------------------------------------------------

class TestSchemaGeneration(unittest.TestCase):

    def setUp(self):
        self.dump_path = create_temp_dump()
        self.schema = generate_schema.build_schema(self.dump_path)

    def tearDown(self):
        self.dump_path.unlink(missing_ok=True)

    def test_enums_parsed(self):
        self.assertIn("WeaponType", self.schema["enums"])
        self.assertIn("ArmorSlot", self.schema["enums"])
        wt = self.schema["enums"]["WeaponType"]
        self.assertEqual(wt["values"]["Rifle"], 0)
        self.assertEqual(wt["values"]["Heavy"], 4)
        self.assertEqual(wt["underlying_type"], "int")

    def test_structs_parsed(self):
        self.assertIn("DamageInfo", self.schema["structs"])
        di = self.schema["structs"]["DamageInfo"]
        field_names = [f["name"] for f in di["fields"]]
        self.assertIn("MinDamage", field_names)
        self.assertIn("CritChance", field_names)

    def test_templates_parsed(self):
        self.assertIn("WeaponTemplate", self.schema["templates"])
        self.assertIn("ArmorTemplate", self.schema["templates"])
        self.assertIn("TagTemplate", self.schema["templates"])

    def test_abstract_detected(self):
        self.assertTrue(self.schema["templates"]["DataTemplate"]["is_abstract"])
        self.assertTrue(self.schema["templates"]["BaseItemTemplate"]["is_abstract"])
        self.assertFalse(self.schema["templates"]["WeaponTemplate"]["is_abstract"])

    def test_inherited_fields(self):
        """WeaponTemplate should have fields from BaseItemTemplate."""
        wt = self.schema["templates"]["WeaponTemplate"]
        field_names = [f["name"] for f in wt["fields"]]
        # From BaseItemTemplate
        self.assertIn("Weight", field_names)
        self.assertIn("Value", field_names)
        # Own fields
        self.assertIn("MinRange", field_names)
        self.assertIn("Damage", field_names)

    def test_field_categories(self):
        wt = self.schema["templates"]["WeaponTemplate"]
        field_map = {f["name"]: f for f in wt["fields"]}

        self.assertEqual(field_map["MinRange"]["category"], "primitive")
        self.assertEqual(field_map["Damage"]["category"], "primitive")
        self.assertEqual(field_map["IsAutomatic"]["category"], "primitive")
        self.assertEqual(field_map["WeaponClass"]["category"], "enum")
        self.assertEqual(field_map["Tags"]["category"], "reference")

    def test_inheritance_chains(self):
        self.assertIn("WeaponTemplate", self.schema["inheritance"])
        chain = self.schema["inheritance"]["WeaponTemplate"]
        # Should include DataTemplate -> BaseItemTemplate -> WeaponTemplate
        self.assertIn("DataTemplate", chain)
        self.assertIn("BaseItemTemplate", chain)
        self.assertIn("WeaponTemplate", chain)

    def test_dump_hash_present(self):
        self.assertIn("dump_hash", self.schema)
        self.assertEqual(len(self.schema["dump_hash"]), 64)  # SHA-256

    def test_version_present(self):
        self.assertEqual(self.schema["version"], "1.0.0")


class TestSchemaFromFile(unittest.TestCase):
    """Test the full generate -> write -> read cycle."""

    def test_roundtrip(self):
        dump_path = create_temp_dump()
        schema = generate_schema.build_schema(dump_path)

        fd, schema_path = tempfile.mkstemp(suffix=".json")
        with os.fdopen(fd, "w") as f:
            json.dump(schema, f)

        with open(schema_path) as f:
            loaded = json.load(f)

        self.assertEqual(loaded["version"], schema["version"])
        self.assertEqual(set(loaded["enums"].keys()), set(schema["enums"].keys()))
        self.assertEqual(set(loaded["templates"].keys()), set(schema["templates"].keys()))

        dump_path.unlink(missing_ok=True)
        Path(schema_path).unlink(missing_ok=True)


class TestSchemaDiff(unittest.TestCase):

    def setUp(self):
        self.dump_path = create_temp_dump()
        self.schema = generate_schema.build_schema(self.dump_path)

    def tearDown(self):
        self.dump_path.unlink(missing_ok=True)

    def test_identical_schemas_no_changes(self):
        enum_results = diff_schemas.diff_enums(
            self.schema["enums"], self.schema["enums"])
        template_results = diff_schemas.diff_templates(
            self.schema["templates"], self.schema["templates"])
        self.assertEqual(len(enum_results), 0)
        self.assertEqual(len(template_results), 0)

    def test_added_enum_detected(self):
        new_enums = dict(self.schema["enums"])
        new_enums["NewEnum"] = {"underlying_type": "int", "values": {"A": 0}}

        results = diff_schemas.diff_enums(self.schema["enums"], new_enums)
        levels = [r[0] for r in results]
        self.assertIn("ADD", levels)

    def test_removed_template_detected(self):
        old_templates = dict(self.schema["templates"])
        new_templates = {k: v for k, v in old_templates.items()
                        if k != "TagTemplate"}

        results = diff_schemas.diff_templates(old_templates, new_templates)
        levels = [r[0] for r in results]
        self.assertIn("DEL", levels)

    def test_offset_change_is_critical(self):
        import copy
        old_templates = copy.deepcopy(self.schema["templates"])
        new_templates = copy.deepcopy(self.schema["templates"])

        # Change an offset
        for f in new_templates["WeaponTemplate"]["fields"]:
            if f["name"] == "Damage":
                f["offset"] = "0xFF"
                break

        results = diff_schemas.diff_templates(old_templates, new_templates)
        levels = [r[0] for r in results]
        self.assertIn("CRIT", levels)


class TestValidation(unittest.TestCase):

    def setUp(self):
        self.dump_path = create_temp_dump()
        self.schema = generate_schema.build_schema(self.dump_path)
        self.data_dir = Path(tempfile.mkdtemp())

    def tearDown(self):
        self.dump_path.unlink(missing_ok=True)
        import shutil
        shutil.rmtree(self.data_dir, ignore_errors=True)

    def _write_data(self, name, data):
        with open(self.data_dir / f"{name}.json", "w") as f:
            json.dump(data, f)

    def test_coverage_pass_with_all_concrete(self):
        # Write data for all concrete templates
        for tname, tinfo in self.schema["templates"].items():
            if not tinfo["is_abstract"]:
                self._write_data(tname, [{"name": "test_instance"}])

        results, found = validate_extraction.check_template_coverage(
            self.schema, self.data_dir)
        levels = [r[0] for r in results]
        self.assertIn("PASS", levels)

    def test_coverage_fail_with_missing(self):
        # Write data for only one template
        self._write_data("WeaponTemplate", [{"name": "test"}])

        results, found = validate_extraction.check_template_coverage(
            self.schema, self.data_dir)
        # Should report missing types
        self.assertTrue(len(results) > 1)

    def test_instance_names_warn_on_unknown(self):
        self._write_data("WeaponTemplate", [
            {"name": "unknown_0"},
            {"name": "unknown_1"},
            {"name": "unknown_2"},
        ])

        results = validate_extraction.check_instance_names(
            self.data_dir, {"WeaponTemplate"})
        levels = [r[0] for r in results]
        self.assertIn("FAIL", levels)

    def test_type_validation_catches_garbage_float(self):
        self._write_data("WeaponTemplate", [
            {"name": "test_weapon", "Damage": 2.03e32, "MinRange": 5}
        ])

        results = validate_extraction.check_type_validation(
            self.schema, self.data_dir, {"WeaponTemplate"})
        levels = [r[0] for r in results]
        self.assertIn("FAIL", levels)

    def test_type_validation_passes_good_data(self):
        self._write_data("WeaponTemplate", [
            {"name": "test_weapon", "MinRange": 5, "MaxRange": 20,
             "Damage": 15.5, "Accuracy": 0.85, "Ammo": 30,
             "IsAutomatic": True, "WeaponClass": 2, "Weight": 10, "Value": 50.0}
        ])

        results = validate_extraction.check_type_validation(
            self.schema, self.data_dir, {"WeaponTemplate"})
        levels = [r[0] for r in results]
        self.assertNotIn("FAIL", levels)


if __name__ == "__main__":
    unittest.main(verbosity=2)
