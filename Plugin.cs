using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.IO;
using WKMSTranslation.Core;
using WKMSTranslation.Utils;
using TMPro;
using UnityEngine.UI;
using System;
using WKMSTranslation.Hooks;

namespace WKMSTranslation
{
    [BepInPlugin("com.musya.wk.translation", "WKMSTranslation", "1.3.3")]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            string dir = Path.GetDirectoryName(Info.Location);
            TranslationEngine.Initialize(dir);
            FontManager.Initialize(Logger, dir);
            Exporter.Initialize(dir);
            Harmony.CreateAndPatchAll(typeof(Plugin).Assembly);
            PlayerLoopHelper.Inject(typeof(Plugin), MyUpdate);
        }

        private void MyUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F9))
            {
                TranslationEngine.IsEnabled = !TranslationEngine.IsEnabled;
                RefreshUI(true);
            }
            if (Input.GetKeyDown(KeyCode.F10))
            {
                TranslationEngine.Load();
                RefreshUI(false);
            }
            if (Input.GetKeyDown(KeyCode.F11)) Exporter.Save();
        }

        private void RefreshUI(bool forceReset)
        {
            TextProcessor.IsPatching = true;

            foreach (var txt in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (!txt.gameObject.activeInHierarchy || !IsSafe(txt)) continue;

                string orig = TranslationEngine.GetOriginal(txt, txt.text);
                string target = TranslationEngine.IsEnabled ? TranslationEngine.GetTranslation(orig) : orig;

                var anim = txt.GetComponent<Febucci.UI.Core.TAnimCore>();
                if (anim != null)
                {
                    if (HasTypewriter(txt)) continue;

                    if (TranslationEngine.IsAssignedTranslation(txt, target))
                    {
                        if (TranslationEngine.IsEnabled) FontManager.TryReplace(txt);
                        continue;
                    }

                    TranslationEngine.SetAssignedTranslation(txt, target);
                    anim.textFull = target; 
                    
                    if (TranslationEngine.IsEnabled)
                    {
                        FontManager.TryReplace(txt);
                    }
                    continue;
                }

                if (TranslationEngine.IsAssignedTranslation(txt, target))
                {
                    if (TranslationEngine.IsEnabled)
                    {
                        FontManager.TryReplace(txt);
                        if (TranslationEngine.IsFitRequired(orig.GetExactKey())) TextProcessor.FitTMP(txt);
                    }
                    continue;
                }

                TranslationEngine.SetAssignedTranslation(txt, target);
                txt.text = target;

                if (TranslationEngine.IsEnabled)
                {
                    FontManager.TryReplace(txt);
                    if (TranslationEngine.IsFitRequired(orig.GetExactKey())) TextProcessor.FitTMP(txt);
                }

                txt.ForceMeshUpdate(true, true);
            }

            foreach (var txt in Resources.FindObjectsOfTypeAll<Text>())
            {
                if (!txt.gameObject.activeInHierarchy || !IsSafe(txt)) continue;

                string orig = TranslationEngine.GetOriginal(txt, txt.text);
                string target = TranslationEngine.IsEnabled ? TranslationEngine.GetTranslation(orig) : orig;

                if (TranslationEngine.IsAssignedTranslation(txt, target))
                {
                    if (TranslationEngine.IsEnabled && TranslationEngine.IsFitRequired(orig.GetExactKey()))
                        FitLegacyTextManually(txt);
                    continue;
                }

                TranslationEngine.SetAssignedTranslation(txt, target);
                txt.text = target;

                if (TranslationEngine.IsEnabled && TranslationEngine.IsFitRequired(orig.GetExactKey()))
                    FitLegacyTextManually(txt);

                txt.enabled = false;
                txt.enabled = true;
                try { txt.FontTextureChanged(); } catch { }
            }

            foreach (var input in Resources.FindObjectsOfTypeAll<InputField>())
            {
                if (!input.gameObject.activeInHierarchy) continue;
                string orig = TranslationEngine.GetOriginal(input, input.text);
                string target = TranslationEngine.IsEnabled ? TranslationEngine.GetTranslation(orig) : orig;
                if (TranslationEngine.IsAssignedTranslation(input, target)) continue;
                
                TranslationEngine.SetAssignedTranslation(input, target);
                input.text = target; 
            }

            foreach (var tmpInput in Resources.FindObjectsOfTypeAll<TMP_InputField>())
            {
                if (!tmpInput.gameObject.activeInHierarchy) continue;
                string orig = TranslationEngine.GetOriginal(tmpInput, tmpInput.text);
                string target = TranslationEngine.IsEnabled ? TranslationEngine.GetTranslation(orig) : orig;
                if (TranslationEngine.IsAssignedTranslation(tmpInput, target)) continue;
                
                TranslationEngine.SetAssignedTranslation(tmpInput, target);
                tmpInput.text = target;
            }

            TextProcessor.IsPatching = false;
        }

        private void FitLegacyTextManually(Text t)
        {
            if (t == null) return;
            try
            {
                var fitter = t.GetComponent<ContentSizeFitter>();
                if (fitter != null) fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.verticalOverflow = VerticalWrapMode.Truncate;

                if (!t.resizeTextForBestFit)
                {
                    t.resizeTextMinSize = Mathf.Max(1, Mathf.RoundToInt(t.fontSize * 0.4f));
                    t.resizeTextMaxSize = t.fontSize;
                    t.resizeTextForBestFit = true;
                }
            }
            catch { }
        }

        private bool HasTypewriter(Component c)
        {
            if (c == null || c.gameObject == null) return false;
            foreach (var comp in c.gameObject.GetComponents<Component>())
            {
                if (comp != null && comp.GetType().Name.Contains("Typewriter"))
                    return true;
            }
            return false;
        }

        private bool IsSafe(Component c)
        {
            var s = c.GetComponentInParent<UT_TextScrawl>() ?? c.GetComponent<UT_TextScrawl>();
            if (s == null) return true;
            var f = typeof(UT_TextScrawl).GetField("typing", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return f == null || (bool)f.GetValue(s);
        }
    }
}