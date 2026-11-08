using System;
using System.Runtime.CompilerServices;
using Klei.AI;
using UnityEngine;

namespace Shared.Replication
{
    public static class FrozenStateMachineTracker
    {
        private static readonly ConditionalWeakTable<StateMachine.Instance, object> _frozen = new();

        public static void Freeze(StateMachine.Instance instance)
        {
            if (instance == null) return;
            _frozen.GetValue(instance, _ => null);
        }

        public static void Unfreeze(StateMachine.Instance instance)
        {
            if (instance == null) return;
            _frozen.Remove(instance);
        }

        public static bool IsFrozen(StateMachine.Instance instance)
        {
            return instance != null && _frozen.TryGetValue(instance, out _);
        }
    }


    public static class SafeStateMachine
    {
        public static bool TryGoTo(this StateMachine.Instance smi, StateMachine.BaseState target)
        {
            if (smi.IsNullOrDestroyed() || target == null)
                return false;

            bool wasFrozen = FrozenStateMachineTracker.IsFrozen(smi);
            if (wasFrozen)
                FrozenStateMachineTracker.Unfreeze(smi);

            try
            {
                smi.GoTo(target.name);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SafeSM] GoTo({target.name}) suppressed: {ex.Message}");
                StateMachine.Instance.error = false;
                return false;
            }
            finally
            {
                if (wasFrozen)
                    FrozenStateMachineTracker.Freeze(smi);
            }
        }

        public static void ClearGlobalError()
        {
            StateMachine.Instance.error = false;
        }
    }
}
