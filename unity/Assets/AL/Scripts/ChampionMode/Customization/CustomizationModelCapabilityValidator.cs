using System;
using System.Collections.Generic;
using System.Linq;
using AL.ChampionMode.Customization.Contracts;

namespace AL.ChampionMode.Customization
{
    public static class CustomizationModelCapabilityValidator
    {
        public static ModelCapabilityValidationResult Validate(
            ModelCapabilityCandidate candidate)
        {
            var diagnostics = new List<CustomizationDiagnostic>();
            if (candidate == null)
            {
                Error(diagnostics, "AL-CUS-MODEL-NULL", "model", string.Empty);
                return Result(null, diagnostics);
            }

            if (!CustomizationCatalogValidator.IsTechnicalId(candidate.CapabilityId))
            {
                Error(diagnostics, "AL-CUS-MODEL-IDENTITY",
                    "model.capabilityId", candidate.CapabilityId);
            }

            if (candidate.Revision <= 0L)
            {
                Error(diagnostics, "AL-CUS-MODEL-REVISION",
                    "model.revision", candidate.CapabilityId);
            }

            if (!IsSourceIdentity(candidate.SourceIdentity))
            {
                Error(diagnostics, "AL-CUS-MODEL-SOURCE",
                    "model.sourceIdentity", candidate.CapabilityId);
            }

            if ((candidate.SupportedFields & ~CustomizationField.All) != 0)
            {
                Error(diagnostics, "AL-CUS-MODEL-FIELDS",
                    "model.supportedFields", candidate.CapabilityId);
            }

            IReadOnlyList<string> capabilities = candidate.Capabilities;
            if (capabilities == null ||
                capabilities.Count > CustomizationTechnicalLimits.MaximumCapabilities)
            {
                Error(diagnostics, "AL-CUS-MODEL-CAPABILITIES",
                    "model.capabilities", candidate.CapabilityId);
            }
            else
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (int index = 0; index < capabilities.Count; index++)
                {
                    string capability = capabilities[index];
                    string path = "model.capabilities[" + index + "]";
                    if (!CustomizationCatalogValidator.IsCapabilityId(capability))
                    {
                        Error(diagnostics, "AL-CUS-MODEL-CAPABILITY-ID",
                            path, capability);
                    }
                    else if (!seen.Add(capability))
                    {
                        Error(diagnostics, "AL-CUS-MODEL-CAPABILITY-DUPLICATE",
                            path, capability);
                    }
                }
            }

            if (diagnostics.Any(item =>
                    item.Severity == CustomizationDiagnosticSeverity.Error))
            {
                return Result(null, diagnostics);
            }

            return Result(new ModelCapabilitySnapshot(
                candidate.CapabilityId,
                candidate.Revision,
                candidate.SourceIdentity,
                candidate.SupportedFields,
                capabilities.OrderBy(item => item, StringComparer.Ordinal)),
                diagnostics);
        }

        private static ModelCapabilityValidationResult Result(
            ModelCapabilitySnapshot snapshot,
            IEnumerable<CustomizationDiagnostic> diagnostics)
        {
            return new ModelCapabilityValidationResult(snapshot, diagnostics);
        }

        private static bool IsSourceIdentity(string value)
        {
            if (string.IsNullOrEmpty(value) ||
                value.Length > CustomizationTechnicalLimits.MaximumContentKeyLength)
            {
                return false;
            }

            foreach (char character in value)
            {
                bool allowed = character >= 'a' && character <= 'z' ||
                               character >= '0' && character <= '9' ||
                               character == '_' || character == '-' ||
                               character == '.' || character == '/' ||
                               character == ':' || character == '@';
                if (!allowed)
                {
                    return false;
                }
            }

            return !value.Contains("..") &&
                   value[0] != '/' && value[value.Length - 1] != '/';
        }

        private static void Error(
            ICollection<CustomizationDiagnostic> diagnostics,
            string code,
            string path,
            string recordId)
        {
            diagnostics.Add(new CustomizationDiagnostic(code, path, recordId));
        }
    }
}
