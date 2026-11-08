using ONI_United.Misc;
using UnityEngine;

namespace ONI_United.Networking.Synchronization
{
	public class LogicPortManager : KMonoBehaviour
	{
		private float _timer;
		private const float CLEANUP_INTERVAL = 5f;

		// I hate this but its the only way that works
		private void Update()
		{
			_timer += Time.unscaledDeltaTime;
			if (_timer >= CLEANUP_INTERVAL)
			{
				_timer = 0f;
				BuildingUtils.CleanupOrphanedLogicVisElements();
			}
		}
	}
}
