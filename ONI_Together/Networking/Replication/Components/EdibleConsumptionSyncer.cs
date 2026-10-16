using KSerialization;
using Shared.Replication;
using Shared.Replication.Attributes;

namespace ONI_United.Networking.Replication.Components
{
    [SkipSaveFileSerialization]
    public class EdibleConsumptionSyncer : NetworkBehaviour
    {
        private Edible _edible;

        [SyncVar(Hook = nameof(OnConsumptionChanged))]
        private bool _isBeingConsumed;

        public override void OnSpawn()
        {
            base.OnSpawn();
            _edible = GetComponent<Edible>();
        }

        private void OnConsumptionChanged(bool _, bool value)
        {
            if (_edible != null)
                _edible.isBeingConsumed = value;
        }

        private void Update()
        {
            if (!isServer || !inSession)
                return;

            _isBeingConsumed = _edible.isBeingConsumed;
        }
    }
}
