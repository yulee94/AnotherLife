using System;
using System.Collections.Generic;
using AL.Core;
using AL.Core.Interfaces;

namespace AL.Services.Local
{
    internal static class EconomyProductionBatchPlanner
    {
        private const double LongUpperExclusive = 9223372036854775808d;

        internal static EconomyMutationStatus Plan(
            IReadOnlyList<EconomyProductionContribution> contributions,
            double[] currentRemainders,
            long[] currentBalances,
            bool[] hasCurrentEntry,
            double[] stagedContributions,
            double[] stagedRemainders,
            long[] stagedBalances,
            bool[] stagedInsertions,
            bool[] stagedBalanceChanges,
            out EconomyDiagnostic diagnostic)
        {
            int resourceCount = ResourceRules.WalletResources.Count;
            ValidateWorkspace(currentRemainders, resourceCount, nameof(currentRemainders));
            ValidateWorkspace(currentBalances, resourceCount, nameof(currentBalances));
            ValidateWorkspace(hasCurrentEntry, resourceCount, nameof(hasCurrentEntry));
            ValidateWorkspace(stagedContributions, resourceCount, nameof(stagedContributions));
            ValidateWorkspace(stagedRemainders, resourceCount, nameof(stagedRemainders));
            ValidateWorkspace(stagedBalances, resourceCount, nameof(stagedBalances));
            ValidateWorkspace(stagedInsertions, resourceCount, nameof(stagedInsertions));
            ValidateWorkspace(stagedBalanceChanges, resourceCount, nameof(stagedBalanceChanges));

            Array.Clear(stagedContributions, 0, resourceCount);
            Array.Clear(stagedRemainders, 0, resourceCount);
            Array.Clear(stagedBalances, 0, resourceCount);
            Array.Clear(stagedInsertions, 0, resourceCount);
            Array.Clear(stagedBalanceChanges, 0, resourceCount);

            if (contributions == null)
            {
                diagnostic = new EconomyDiagnostic(
                    EconomyDiagnosticCodes.ProductionDependency,
                    "Production.Contributions");
                return EconomyMutationStatus.RejectedDependencyUnavailable;
            }

            for (int contributionIndex = 0; contributionIndex < contributions.Count; contributionIndex++)
            {
                EconomyProductionContribution contribution = contributions[contributionIndex];
                if (!ResourceRules.TryGetWalletIndex(contribution.ResourceType, out int resourceIndex) ||
                    double.IsNaN(contribution.Amount) ||
                    double.IsInfinity(contribution.Amount) ||
                    contribution.Amount < 0d)
                {
                    diagnostic = new EconomyDiagnostic(
                        EconomyDiagnosticCodes.ProductionInvalidContribution,
                        $"Production.Contributions[{contributionIndex}]");
                    return EconomyMutationStatus.RejectedDependencyUnavailable;
                }

                double totalContribution = stagedContributions[resourceIndex] + contribution.Amount;
                if (double.IsNaN(totalContribution) ||
                    double.IsInfinity(totalContribution) ||
                    totalContribution < 0d)
                {
                    diagnostic = new EconomyDiagnostic(
                        EconomyDiagnosticCodes.ProductionInvalidContribution,
                        $"Production.Contributions[{contributionIndex}]");
                    return EconomyMutationStatus.RejectedOverflow;
                }

                stagedContributions[resourceIndex] = totalContribution;
            }

            bool changed = false;
            for (int resourceIndex = 0; resourceIndex < resourceCount; resourceIndex++)
            {
                double remainder = currentRemainders[resourceIndex];
                if (double.IsNaN(remainder) ||
                    double.IsInfinity(remainder) ||
                    remainder < 0d ||
                    remainder >= 1d)
                {
                    diagnostic = new EconomyDiagnostic(
                        EconomyDiagnosticCodes.ProductionInvalidRemainder,
                        $"Production.Remainders[{ResourceRules.WalletResources[resourceIndex]}]");
                    return EconomyMutationStatus.RejectedMalformedState;
                }

                double total = remainder + stagedContributions[resourceIndex];
                if (double.IsNaN(total) || double.IsInfinity(total) || total < 0d)
                {
                    diagnostic = new EconomyDiagnostic(
                        EconomyDiagnosticCodes.ProductionInvalidContribution,
                        $"Production.Totals[{ResourceRules.WalletResources[resourceIndex]}]");
                    return EconomyMutationStatus.RejectedOverflow;
                }

                double wholeAsDouble = Math.Floor(total);
                if (wholeAsDouble < 0d || wholeAsDouble >= LongUpperExclusive)
                {
                    diagnostic = new EconomyDiagnostic(
                        EconomyDiagnosticCodes.Overflow,
                        $"Resources[{ResourceRules.WalletResources[resourceIndex]}]");
                    return EconomyMutationStatus.RejectedOverflow;
                }

                long whole = (long)wholeAsDouble;
                double nextRemainder = total - wholeAsDouble;
                if (double.IsNaN(nextRemainder) ||
                    double.IsInfinity(nextRemainder) ||
                    nextRemainder < 0d ||
                    nextRemainder >= 1d)
                {
                    diagnostic = new EconomyDiagnostic(
                        EconomyDiagnosticCodes.ProductionInvalidRemainder,
                        $"Production.Remainders[{ResourceRules.WalletResources[resourceIndex]}]");
                    return EconomyMutationStatus.RejectedOverflow;
                }

                stagedRemainders[resourceIndex] = nextRemainder;
                changed |= nextRemainder != remainder;

                if (!hasCurrentEntry[resourceIndex])
                {
                    if (ResourceRules.IsCoreResource(ResourceRules.WalletResources[resourceIndex]))
                    {
                        diagnostic = new EconomyDiagnostic(
                            EconomyDiagnosticCodes.MissingCoreResource,
                            $"Resources[{ResourceRules.WalletResources[resourceIndex]}]");
                        return EconomyMutationStatus.RejectedMalformedState;
                    }

                    stagedBalances[resourceIndex] = whole;
                    stagedInsertions[resourceIndex] = whole > 0;
                    stagedBalanceChanges[resourceIndex] = whole > 0;
                    changed |= whole > 0;
                    continue;
                }

                try
                {
                    stagedBalances[resourceIndex] = checked(currentBalances[resourceIndex] + whole);
                }
                catch (OverflowException)
                {
                    diagnostic = new EconomyDiagnostic(
                        EconomyDiagnosticCodes.Overflow,
                        $"Resources[{ResourceRules.WalletResources[resourceIndex]}]");
                    return EconomyMutationStatus.RejectedOverflow;
                }

                stagedBalanceChanges[resourceIndex] = whole > 0;
                changed |= whole > 0;
            }

            diagnostic = default;
            return changed ? EconomyMutationStatus.Applied : EconomyMutationStatus.NoChange;
        }

        private static void ValidateWorkspace(Array workspace, int expectedLength, string name)
        {
            if (workspace == null || workspace.Length != expectedLength)
            {
                throw new ArgumentException("Production workspace has an invalid bounded size.", name);
            }
        }
    }
}
