using System;
using System.Text;
using Shared.Profiling;
using UnityEngine;

namespace Shared.Helpers
{

	public static class DialogUtil
	{
		public static void CreateConfirmDialogFrontend(string title = null, string text = null, string confirm_text = null, System.Action on_confirm = null, string cancel_text = null, System.Action on_cancel = null, string configurable_text = null, System.Action on_configurable_clicked = null, Sprite image_sprite = null, bool useScreenSpaceOverlay = false, GameObject parent = null)
		=> CreateConfirmDialog(title, text, confirm_text, on_confirm, cancel_text, on_cancel, configurable_text, on_configurable_clicked, image_sprite, true, useScreenSpaceOverlay, parent);
		public static ConfirmDialogScreen CreateConfirmDialog(string title = null, string text = null, string confirm_text = null, System.Action on_confirm = null, string cancel_text = null, System.Action on_cancel = null, string configurable_text = null, System.Action on_configurable_clicked = null, Sprite image_sprite = null, bool frontend = false, bool useScreenSpaceOverlay = false, GameObject parent = null)
		{
			using var _ = Profiler.Scope();

			if (parent == null)
				parent = frontend && !useScreenSpaceOverlay ? Global.Instance.globalCanvas : GameScreenManager.Instance.GetParent(GameScreenManager.UIRenderTarget.ScreenSpaceOverlay);
			var dialogue = ((ConfirmDialogScreen)KScreenManager.Instance.StartScreen(ScreenPrefabs.Instance.ConfirmDialogScreen.gameObject, parent));

			if (!frontend)
				dialogue.Subscribe(476357528, (_) => CameraController.Instance.DisableUserCameraControl = true);
			dialogue.PopupConfirmDialog(text, on_confirm, on_cancel, configurable_text, on_configurable_clicked, title, confirm_text, cancel_text, image_sprite);
			return dialogue;
		}
	}
}
