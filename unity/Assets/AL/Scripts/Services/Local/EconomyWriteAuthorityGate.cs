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

        private EconomyWriteAuthorityGate(
            ISaveGameService saveGameService,
            IProfileWriteAuthorityProvider provider)
        {
            _saveGameService = saveGameService;
            _provider = provider;
        }

        internal static EconomyWriteAuthorityGate FromSaveService(
            ISaveGameService saveGameService)
        {
            var canonical = saveGameService as LocalSaveGameService;
            return new EconomyWriteAuthorityGate(
                saveGameService,
                canonical);
        }

        internal bool TryGetWritableSave(out SaveGameData save)
        {
            save = null;
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
            if (expectedPublishedSave == null ||
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
