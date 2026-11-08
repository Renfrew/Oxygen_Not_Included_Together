using ONI_United.UI.Screens;
using Shared.Profiling;
using UnityEngine;

namespace ONI_United.Networking.Presentation
{
	public class UIVisibilityController : MonoBehaviour
	{
		private void Update()
		{
			using var _ = Profiler.Scope();

			NetworkIndicatorsScreen.Update();
		}
	}
}
