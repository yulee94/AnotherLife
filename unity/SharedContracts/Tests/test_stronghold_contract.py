"""Inert stronghold contract: no production balance, territory conversion or activation."""
import json
import pathlib
import unittest

from jsonschema import Draft202012Validator

ROOT = pathlib.Path(__file__).resolve().parents[3]
DATA = ROOT / 'unity/Assets/AL/StreamingAssets/GameData/al_stronghold_catalog.json'
SCHEMA = ROOT / 'unity/SharedContracts/Schemas/al-stronghold.schema.json'
FIXTURES = pathlib.Path(__file__).parent / 'fixtures'


class StrongholdContractTests(unittest.TestCase):
    def test_schema_rejects_cross_wired_profile_types(self):
        validator = Draft202012Validator(json.loads(SCHEMA.read_text(encoding='utf-8')))
        data = json.loads(DATA.read_text(encoding='utf-8'))
        data['levels'][0]['gateProfileId'] = data['levels'][0]['visualProfileId']
        self.assertTrue(list(validator.iter_errors(data)))

    def test_schema_valid_fixture_and_each_negative_fixture(self):
        self.assertTrue(SCHEMA.is_file(), 'Missing stronghold schema')
        schema = json.loads(SCHEMA.read_text(encoding='utf-8'))
        Draft202012Validator.check_schema(schema)
        validator = Draft202012Validator(schema)
        data = json.loads(DATA.read_text(encoding='utf-8'))
        self.assertEqual([], list(validator.iter_errors(data)))
        self.assertEqual(data, json.loads((FIXTURES / 'valid/al-stronghold.valid.json').read_text(encoding='utf-8')))
        negatives = sorted((FIXTURES / 'invalid').glob('al-stronghold.invalid.*.json'))
        self.assertEqual(12, len(negatives))
        for path in negatives:
            with self.subTest(path=path.name):
                self.assertTrue(list(validator.iter_errors(json.loads(path.read_text(encoding='utf-8')))))

    def test_catalog_has_exact_legacy_mapping_and_ten_source_slots(self):
        self.assertTrue(DATA.is_file(), 'Missing stronghold source catalog')
        data = json.loads(DATA.read_text(encoding='utf-8'))
        self.assertFalse(data['productionEligible'])
        self.assertEqual([('T1', 'stronghold_t1'), ('T2', None), ('T3', None),
                          ('T4', 'stronghold_t4'), ('T5', None)],
                         [(x['territoryId'], x['strongholdProfileId']) for x in data['territories']])
        self.assertEqual(list(range(1, 11)), [x['level'] for x in data['levels']])
        self.assertEqual(180000, data['takeoverDurationMilliseconds'])
        self.assertEqual({'stonehold': 'DeepOre', 'eldergrove': 'WorldSap',
                          'crownlands': 'RoyalSigil', 'umbral': 'DarkCrystal'}, data['ownerRareResources'])
        for row in data['levels']:
            self.assertIsNone(row['balance'])
            self.assertEqual(row['level'] >= 5, row['commandNpcRequired'])
            self.assertEqual(row['level'] >= 5, row['mageGuardsRequired'])
        for key in ('visualProfileId', 'gateProfileId', 'guardRosterProfileId', 'upgradeCostProfileId'):
            self.assertEqual(10, len({x[key] for x in data['levels']}))
        self.assertEqual('MajorGateMilestone', data['levels'][4]['milestone'])
        self.assertEqual('CapstoneMilestone', data['levels'][9]['milestone'])
        for key in ('guardStatsProfileId', 'survivorRegenerationProfileId', 'reinforcementTimingProfileId'):
            self.assertNotEqual(data['levels'][8][key], data['levels'][9][key])


if __name__ == '__main__':
    unittest.main()
