using HarmonyLib;
using ONI_United.Networking.Sessions;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace ONI_United.Patches
{
    // Local menus must not change simulation speed while a session is active.
    // Evaluate session state at each call, including after starting/stopping hosting.
    [HarmonyPatch]
    internal static class ModalScreenPausePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.DeclaredMethod(typeof(KModalButtonMenu), nameof(KModalButtonMenu.OnShow));
            yield return AccessTools.DeclaredMethod(typeof(KModalScreen), nameof(KModalScreen.OnShow));
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var pause = AccessTools.Method(typeof(SpeedControlScreen),
                nameof(SpeedControlScreen.Pause), [typeof(bool), typeof(bool)]);
            var unpause = AccessTools.Method(typeof(SpeedControlScreen),
                nameof(SpeedControlScreen.Unpause), [typeof(bool)]);
            var conditionalPause = AccessTools.Method(typeof(ModalScreenPausePatch), nameof(ConditionalPause));
            var conditionalUnpause = AccessTools.Method(typeof(ModalScreenPausePatch), nameof(ConditionalUnpause));

            foreach (var instruction in instructions)
            {
                // Keep native OnShow bookkeeping and camera behavior, and retain IL metadata.
                if (instruction.Calls(pause))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = conditionalPause;
                }
                else if (instruction.Calls(unpause))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = conditionalUnpause;
                }

                yield return instruction;
            }
        }

        private static void ConditionalPause(SpeedControlScreen screen, bool playSound, bool isCrashed)
        {
            if (!Session.IsActive)
                screen.Pause(playSound, isCrashed);
        }

        private static void ConditionalUnpause(SpeedControlScreen screen, bool playSound)
        {
            if (!Session.IsActive)
                screen.Unpause(playSound);
        }
    }
}
