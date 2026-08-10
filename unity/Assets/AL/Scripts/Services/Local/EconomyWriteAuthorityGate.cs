using AL.Core.Interfaces;
using AL.Core.SaveAuthority;
using AL.Data.Runtime;

namespace AL.Services.Local
{
    /// <summary>
    /// Re-reads and validates live save authority at every economy boundary.
    /// Production construction can only derive authority from the supplied
    /// sealed local save service. No runtime-accessible test bypass exists.
    /// </summary>
    internal sealed class EconomyWriteAuthorityGate
    {
        private readonly ISaveGameService _saveGameService;
        private readonly IProfileWriteAuthorityProvider _provider;
        private readonly bool _requiresProductionContainment;

        private EconomyWriteAuthorityGate(
            ISaveGameService saveGameService,
            IProfileWriteAuthorityProvider provider)
            : this(saveGameService, provider, false)
        {
        }

        private EconomyWriteAuthorityGate(
            ISaveGameService saveGameService,
            IProfileWriteAuthorityProvider provider,
            bool requiresProductionContainment)
        {
            _saveGameService = saveGameService;
            _provider = provider;
            _requiresProductionContainment = requiresProductionContainment;
        }

        internal static EconomyWriteAuthorityGate FromSaveService(
            ISaveGameService saveGameService)
        {
            var canonical = saveGameService as LocalSaveGameService;
            return new EconomyWriteAuthorityGate(
                saveGameService,
                canonical,
                true);
        }

        internal bool TryGetWritableSave(out SaveGameData save)
        {
            save = null;
            if (_requiresProductionContainment &&
                !ProfileMutationContainment.ProductionWriteActivationEnabled)
            {
                return false;
            }

            try
            {
                save = _saveGameService?.CurrentSave;
            }
            catch
            {
                return false;
            }

            return IsWritableFor(save);
        }

        internal bool IsWritableFor(SaveGameData expectedPublishedSave)
        {
            if (_requiresProductionContainment &&
                    !ProfileMutationContainment.ProductionWriteActivationEnabled ||
                expectedPublishedSave == null ||
                !ProfileWriteAuthorityProviderGuard
                    .IsCurrentWritable(_provider))
            {
                return false;
            }

            try
            {
                return ReferenceEquals(
                    _saveGameService.CurrentSave,
                    expectedPublishedSave);
            }
            catch
            {
                return false;
            }
        }
    }
}
