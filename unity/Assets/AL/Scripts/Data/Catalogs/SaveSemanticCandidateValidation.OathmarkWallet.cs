using System;

namespace AL.Data.Catalogs
{
    public static partial class SaveSemanticCandidateValidator
    {
        private static void ValidateOathmarkWallet(StrictJsonObject root,
            DiagnosticCollector collector, ValidationState state)
        {
            const string path = "$.OathmarkWallet";
            if (!root.TryGet("OathmarkWallet", out var value) || value is StrictJsonNull) return;
            try
            {
                var obj = WalletJson.Object(value);
                if (WalletJson.Long(obj, "Version") > 1)
                {
                    MarkPreservedUnknown(state, collector, "SAVE_OATHMARK_VERSION_FORWARD", path,
                        SaveSemanticDomain.Envelope, rawOnly: true);
                    return;
                }
                var wallet = OathmarkWalletValidation.Read(obj);
                if (OathmarkWalletValidation.IsEmpty(wallet)) return;
                if (WalletJson.Long(root, "SaveSchemaVersion") != 2 ||
                    !OathmarkWalletValidation.IsValid(wallet, ReadProfileId(root))) throw new FormatException();
            }
            catch (Exception ex) when (ex is FormatException || ex is OverflowException)
            {
                MarkMalformed(state, collector, "SAVE_OATHMARK_INVALID", path, SaveSemanticDomain.Envelope);
            }
        }
    }
}
