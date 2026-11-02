#if DEBUG
using System;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ONI_United.DebugTools
{
    internal static class NativeGalleryInspection
    {
        // Per entry/open, at most 28 nodes across all samples; no frame-based dumps.
        internal static void Record(string id, GameObject root, ref int budget)
        {
            if (root == null || budget <= 0) return;
            var text = new StringBuilder("[" + id + "] INSPECTION (not a visual suitability verdict)\n");
            var labels = root.GetComponentsInChildren<TMP_Text>(true);
            text.AppendLine("root=" + root.name + "; active=" + root.activeInHierarchy + "; labels=" + labels.Length
                + "; buttons=" + root.GetComponentsInChildren<KButton>(true).Length + "; tooltips=" + root.GetComponentsInChildren<ToolTip>(true).Length);
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
            {
                if (budget-- <= 0) { text.AppendLine("… hierarchy inspection budget reached"); break; }
                var components = node.GetComponents<Component>();
                string path = node == root.transform ? node.name : Path(node, root.transform);
                text.AppendLine(path + " activeSelf=" + node.gameObject.activeSelf + " activeHierarchy=" + node.gameObject.activeInHierarchy
                    + "; components=" + string.Join(",", components.Select(c => c == null ? "MISSING" : c.GetType().FullName + (c is Behaviour b ? "(enabled=" + b.enabled + ")" : ""))));
                foreach (var image in node.GetComponents<Image>())
                {
                    text.AppendLine(" Image sprite=" + Name(image.sprite) + "; type=" + image.type + "; material=" + Name(image.material) + "; color=" + ColorUtility.ToHtmlStringRGBA(image.color));
                    if (image is KImage native)
                    {
                        var style = native.colorStyleSetting;
                        text.AppendLine(" KImage style=" + Name(style) + "; default=" + native.defaultState
                            + (style == null ? "" : "; normal=" + ColorUtility.ToHtmlStringRGBA(style.inactiveColor) + "; hover=" + ColorUtility.ToHtmlStringRGBA(style.hoverColor)
                            + "; pressed=" + ColorUtility.ToHtmlStringRGBA(style.activeColor) + "; disabled=" + ColorUtility.ToHtmlStringRGBA(style.disabledColor)));
                    }
                }
                foreach (var label in node.GetComponents<TMP_Text>())
                    text.AppendLine(" Text type=" + label.GetType().AssemblyQualifiedName + "; value=" + NativeUiGallery.Short(label.text, 90)
                        + "; font=" + Name(label.font) + "; material=" + Name(label.fontSharedMaterial) + "; shader=" + Name(label.fontSharedMaterial != null ? label.fontSharedMaterial.shader : null)
                        + "; size=" + label.fontSize + "; wrapping=" + label.textWrappingMode
                        + (label is LocText loc ? "; textStyle=" + Name(loc.textStyleSetting) + "; key=" + loc.key : ""));
                foreach (var button in node.GetComponents<KButton>())
                    text.AppendLine(" KButton interactable=" + button.isInteractable + "; bg=" + Name(button.bgImage) + "; soundPlayer=" + (button.soundPlayer != null ? button.soundPlayer.GetType().Name : "MISSING")
                        + "; input focus/navigation supplied by native UI infrastructure");
                foreach (var toggle in node.GetComponents<MultiToggle>())
                    text.AppendLine(" MultiToggle states=" + (toggle.states != null ? toggle.states.Length : 0) + "; current=" + toggle.CurrentState + "; image=" + Name(toggle.toggle_image));
                foreach (var input in node.GetComponents<TMP_InputField>())
                    text.AppendLine(" Input type=" + input.GetType().FullName + "; interactable=" + input.interactable + "; contentType=" + input.contentType + "; placeholder=" + Name(input.placeholder) + "; textComponent=" + Name(input.textComponent));
                foreach (var dropdown in node.GetComponents<TMP_Dropdown>())
                    text.AppendLine(" Dropdown options=" + dropdown.options.Count + "; value=" + dropdown.value + "; template=" + Name(dropdown.template));
                foreach (var tooltip in node.GetComponents<ToolTip>())
                    text.AppendLine(" ToolTip enabled=" + tooltip.enabled + "; native template placement retained");
            }
            ModLog.Debug($"[NativeUiGallery] {text}");
        }
        private static string Name(UnityEngine.Object value) => value != null ? value.name : "<none>";
        private static string Path(Transform node, Transform root)
        {
            string path = node.name;
            for (var parent = node.parent; parent != null && parent != root; parent = parent.parent) path = parent.name + "/" + path;
            return root.name + "/" + path;
        }
    }
}
#endif
