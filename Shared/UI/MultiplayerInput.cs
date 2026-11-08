using UnityEngine;

namespace Shared.UI
{
    // Text input needs no additional screen-stack entry.
    public static class MultiplayerInput
    {
        private static int endedFrame = -2;
        public static void EditingEnded(string value) => endedFrame = Time.frameCount;
        public static bool ConsumeEditingInput(KScreen dialog, KButtonEvent e)
        {
            var selected = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            var input = selected != null && selected.transform.IsChildOf(dialog.transform)
                ? selected.GetComponentInParent<KInputTextField>() : null;
            if ((input == null || !input.isFocused) && endedFrame != Time.frameCount) return false;
            e.Consumed = true;
            return true;
        }
    }
}
