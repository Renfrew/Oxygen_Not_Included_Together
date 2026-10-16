using HarmonyLib;
using ONIStrings = ONI_Together.STRINGS;
using ONI_Together.UI;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace ONI_United.Patches.InterfacePatches
{
	internal class TopLeftControlScreen_Patches
	{

        [HarmonyPatch(typeof(TopLeftControlScreen), nameof(TopLeftControlScreen.OnActivate))]
        public class TopLeftControlScreen_OnActivate_Patch
        {
            public static void Postfix(TopLeftControlScreen __instance)
			{
				var chatButton = Util.KInstantiateUI(__instance.sandboxToggle.gameObject, __instance.sandboxToggle.transform.parent.gameObject, true).transform;
				chatButton.SetSiblingIndex(__instance.sandboxToggle.transform.GetSiblingIndex() + 1);
				chatButton.Find("FG").GetComponent<Image>().sprite = Assets.GetSprite("icon_main_menu_forums");
				chatButton.Find("Label").GetComponent<LocText>().text = ONIStrings.UI.MP_CHATBOX.TOPBAR.LABEL;
				chatButton.rectTransform().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 65f);
				chatButton.TryGetComponent<MultiToggle>(out UnityChatBoxUI.ChatToggle);
				chatButton.TryGetComponent<ToolTip>(out var tt);
				tt.SetSimpleTooltip(ONIStrings.UI.MP_CHATBOX.TOPBAR.TOOLTIP);
				UnityChatBoxUI.InitToggle();
				
            }
        }
	}
}
