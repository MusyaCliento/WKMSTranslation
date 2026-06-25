using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WKMSTranslation.Core;

namespace WKMSTranslation.Hooks
{
    [HarmonyPatch]
    public static class UnityHooks
    {
        [HarmonyPatch(typeof(TextMeshProUGUI), "Awake")]
        [HarmonyPostfix] public static void UGUI_Awake(TextMeshProUGUI __instance) => SyncTMP(__instance);

        [HarmonyPatch(typeof(TextMeshProUGUI), "OnEnable")]
        [HarmonyPostfix] public static void UGUI_Enable(TextMeshProUGUI __instance) => SyncTMP(__instance);

        [HarmonyPatch(typeof(TextMeshPro), "Awake")]
        [HarmonyPostfix] public static void World_Awake(TextMeshPro __instance) => SyncTMP(__instance);

        [HarmonyPatch(typeof(TextMeshPro), "OnEnable")]
        [HarmonyPostfix] public static void World_Enable(TextMeshPro __instance) => SyncTMP(__instance);

        [HarmonyPatch(typeof(Text), "OnEnable")]
        [HarmonyPostfix] public static void Legacy_Enable(Text __instance) => SyncLegacy(__instance);

        [HarmonyPatch(typeof(TMP_Text), "text", MethodType.Setter)]
        [HarmonyPrefix]
        public static void TMP_Setter_Prefix(TMP_Text __instance, ref string value)
        {
            if (ThirdPartyHooks.ThirdPartyDepth > 0) return;
            TextProcessor.Process(__instance, ref value);
        }

        [HarmonyPatch(typeof(Text), "text", MethodType.Setter)]
        [HarmonyPrefix]
        public static void Legacy_Setter_Prefix(Text __instance, ref string value)
        {
            TextProcessor.Process(__instance, ref value);
        }

        [HarmonyPatch(typeof(Text), "text", MethodType.Setter)]
        [HarmonyPostfix]
        public static void Legacy_Setter_Postfix(Text __instance)
        {
            TextProcessor.PostProcess(__instance);
            try { __instance?.FontTextureChanged(); } catch { }
        }

        [HarmonyPatch(typeof(InputField), "text", MethodType.Setter)]
        [HarmonyPrefix]
        public static void LegacyInputField_Setter_Prefix(InputField __instance, ref string value)
        {
            if (ThirdPartyHooks.ThirdPartyDepth > 0) return;
            TextProcessor.Process(__instance, ref value);
        }

        [HarmonyPatch(typeof(TMP_InputField), "text", MethodType.Setter)]
        [HarmonyPrefix]
        public static void TMPInputField_Setter_Prefix(TMP_InputField __instance, ref string value)
        {
            if (ThirdPartyHooks.ThirdPartyDepth > 0) return;
            TextProcessor.Process(__instance, ref value);
        }

        private static void SyncTMP(TMP_Text component)
        {
            if (component == null) return;
            
            string t = component.text;
            TextProcessor.Process(component, ref t);

            if (t != component.text)
            {
                TextProcessor.IsPatching = true;
                component.text = t;
                TextProcessor.IsPatching = false;
            }
            TextProcessor.PostProcess(component);
        }

        private static void SyncLegacy(Text component)
        {
            if (component == null) return;
            string t = component.text;
            TextProcessor.Process(component, ref t);

            if (t != component.text)
            {
                TextProcessor.IsPatching = true;
                component.text = t;
                TextProcessor.IsPatching = false;
            }
            TextProcessor.PostProcess(component);
            try { component.FontTextureChanged(); } catch { }
        }
    }
}