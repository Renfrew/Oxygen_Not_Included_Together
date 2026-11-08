using UnityEngine;

namespace Shared.UI
{
    // Native KTabMenuHeader selects this through ImageToggleState.
    public sealed class MultiplayerButtonState : ImageToggleState
    {
        [SerializeField] private KImage fill = null!;
        [SerializeField] private ColorStyleSetting originalStyle = null!, selectedStyle = null!;

        public void Initialize(KImage fill, ColorStyleSetting selectedStyle)
        {
            this.fill = fill;
            originalStyle = fill.colorStyleSetting;
            this.selectedStyle = selectedStyle;
            TargetImage = fill;
            colorStyleSetting = originalStyle;
            RefreshColorStyle();
        }

        public override void SetActive()
        {
            base.SetActive();
            ApplyStyle(selectedStyle);
        }

        public override void SetInactive()
        {
            base.SetInactive();
            ApplyStyle(originalStyle);
        }

        private void ApplyStyle(ColorStyleSetting style)
        {
            fill.colorStyleSetting = style;
            fill.ApplyColorStyleSetting();
        }
    }
}
