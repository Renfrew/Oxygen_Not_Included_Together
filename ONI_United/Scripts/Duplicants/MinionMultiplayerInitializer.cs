using ONI_United.Networking.Session;
using ONI_United.Networking.Identity;
using ONI_United.Networking.Synchronization;
using ONI_United.Networking.Replication.Components;
using ONI_United.Networking.Replication.Components.Entities;
using System.Collections;
using Shared.Profiling;
using UnityEngine;

namespace ONI_United.Scripts.Duplicants
{
	internal class MinionMultiplayerInitializer : KMonoBehaviour
	{
		[MyCmpGet] NetworkIdentity identity;
		[MyCmpGet] KPrefabID kpref;

		private bool HasInit = false;

        public override void OnPrefabInit()
        {
			using var _ = Profiler.Scope();
            base.OnPrefabInit();

			gameObject.AddOrGet<AnimSyncer>();
			gameObject.AddOrGet<NavigatorSyncer>();
        }

		public override void OnSpawn()
		{
			using var _ = Profiler.Scope();
			base.OnSpawn();
			if (MultiplayerSession.InActiveSession)
			{
				FinalizeInit();
			}
			else
			{
				StartCoroutine(WaitForSessionAndInit());
			}
		}

		IEnumerator WaitForSessionAndInit()
		{
			yield return new WaitUntil((() => MultiplayerSession.InActiveSession));
			FinalizeInit();
		}

		void FinalizeInit()
		{
			using var _ = Profiler.Scope();
			if (HasInit) return;

			var go = gameObject;
			if (!kpref?.HasTag(GameTags.BaseMinion) ?? false) return;

			if (MultiplayerSession.IsClient)
				InitializeClient(go);
			else
				InitializeHost(go);

			HasInit = true;
		}
		
		void InitializeClient(GameObject go)
		{
			if (go.TryGetComponent<ChoreDriver>(out var driver)) driver.enabled = false;
			if (go.TryGetComponent<ChoreConsumer>(out var consumer)) consumer.enabled = false;
			if (go.TryGetComponent<MinionBrain>(out var brain)) brain.enabled = false;
			if (go.TryGetComponent<Sensors>(out var sensors)) sensors.enabled = false;

			// Do NOT disable StateMachineController!
			// In Klei's engine, StateMachineController drives visual animation and rendering of the Duplicant.

			go.AddOrGet<ClientReceiver_ChoreErrands>();
			var statusSync = go.AddOrGet<StatusItemsSyncer>();
			statusSync.recieverType = StatusItemsSyncer.StatusRecieverType.DUPLICANT;
		}

		void InitializeHost(GameObject go)
		{
			go.AddOrGet<DuplicantChoreBroadcaster>();
			go.AddOrGet<StatusItemsSyncer>();
		}
	}
}
