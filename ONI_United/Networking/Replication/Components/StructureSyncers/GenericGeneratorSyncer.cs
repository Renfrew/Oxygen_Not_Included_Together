using KSerialization;
using Shared.Replication;
using Shared.Replication.Attributes;

namespace ONI_United.Networking.Replication.Components.StructureSyncers
{
    [SkipSaveFileSerialization]
    public class GenericGeneratorSyncer : NetworkBehaviour
    {
        private Generator _generator;

        [SyncVar(Hook = nameof(OnJoulesChanged))]
        private float _joulesAvailable;

        public override void OnSpawn()
        {
            base.OnSpawn();
            _generator = GetComponent<Generator>();
        }

        private void Update()
        {
            if (!isServer || _generator == null) return;
            _joulesAvailable = _generator.JoulesAvailable;
        }

        private void OnJoulesChanged(float oldValue, float newValue)
        {
            if (_generator == null) return;
            _generator.AssignJoulesAvailable(newValue);
        }
    }
}
