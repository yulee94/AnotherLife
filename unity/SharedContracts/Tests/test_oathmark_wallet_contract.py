"""Wallet installation policy is a catalog contract, not a balance grant."""
import copy
import json
from pathlib import Path
import unittest
from jsonschema import Draft202012Validator

ROOT = Path(__file__).resolve().parents[3]
CATALOG = ROOT / 'unity/Assets/AL/StreamingAssets/GameData/al_oathmark_wallet_policy.json'
SCHEMA = ROOT / 'unity/SharedContracts/Schemas/al-oathmark-wallet.schema.json'


class OathmarkWalletContractTests(unittest.TestCase):
    def test_wallet_policy_exists_and_fails_closed(self):
        self.assertTrue(CATALOG.is_file(), 'Wallet runtime needs an explicit installation policy')
        catalog = json.loads(CATALOG.read_text(encoding='utf-8'))
        schema = json.loads(SCHEMA.read_text(encoding='utf-8'))
        Draft202012Validator.check_schema(schema)
        validator = Draft202012Validator(schema)
        self.assertEqual([], list(validator.iter_errors(catalog)))
        for field, value in [('initialBalance', 500), ('currencyCatalogId', 'gold'),
                             ('accountBinding', 'caller_selected'), ('saveSchemaVersion', 1),
                             ('earningSourcesEnabled', True), ('maximumReceipts', 0)]:
            with self.subTest(field=field):
                broken = copy.deepcopy(catalog)
                broken[field] = value
                self.assertTrue(list(validator.iter_errors(broken)))


if __name__ == '__main__':
    unittest.main()
