using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WKMSTranslation.Utils;

namespace WKMSTranslation.Core
{
    public static class TextProcessor
    {
        [ThreadStatic] public static bool IsPatching;
        [ThreadStatic] private static bool _tmpNeedsFit;

        public static void Process(object component, ref string text)
        {
            if (IsPatching || component == null || string.IsNullOrWhiteSpace(text)) return;
            if (!Regex.IsMatch(text, @"[a-zA-Zа-яА-ЯёЁ]")) return;

            if (TranslationEngine.IsAssignedTranslation(component, text)) return;

            bool isNewOriginal = TranslationEngine.RegisterOriginal(component, text);

            if (!isNewOriginal)
            {
                TranslationEngine.SetAssignedTranslation(component, text);
                ReplaceFontSafe(component);
                return;
            }

            if (!TranslationEngine.IsEnabled) return;

            string original = TranslationEngine.GetOriginal(component, text);
            string translated = TranslationEngine.GetTranslation(original);

            if (translated == original || translated == text) return;
            if (translated.Contains("{0}") && !original.Contains("{0}")) return;

            text = translated;

            TranslationEngine.SetAssignedTranslation(component, text);
            _tmpNeedsFit = TranslationEngine.IsFitRequired(original.GetExactKey());

            ReplaceFontSafe(component);
        }

        public static void PostProcess(object component)
        {
            if (!_tmpNeedsFit || component == null) return;
            _tmpNeedsFit = false;

            try
            {
                IsPatching = true;

                if (component is TMP_Text tmp) 
                {
                    if (tmp.GetComponent<Febucci.UI.Core.TAnimCore>() == null)
                        FitTMP(tmp);
                }
                else if (component is Text legacy) FitLegacy(legacy);
                else if (component is InputField inputField && inputField.textComponent != null) FitLegacy(inputField.textComponent);
                else if (component is TMP_InputField tmpInputField && tmpInputField.textComponent != null) 
                {
                    if (tmpInputField.textComponent.GetComponent<Febucci.UI.Core.TAnimCore>() == null)
                        FitTMP(tmpInputField.textComponent);
                }
            }
            finally
            {
                IsPatching = false;
            }
        }

        private static void ReplaceFontSafe(object component)
        {
            if (component is TMP_Text tmp) FontManager.TryReplace(tmp);
            else if (component is Text leg) FontManager.TryReplace(leg);
            else if (component is InputField inf && inf.textComponent != null) FontManager.TryReplace(inf.textComponent);
            else if (component is TMP_InputField tinf && tinf.textComponent != null) FontManager.TryReplace(tinf.textComponent);
        }

        public static void FitTMP(TMP_Text t)
        {
            if (t == null) return;
            try
            {
                t.enableWordWrapping = false;
                var fitter = t.GetComponent<ContentSizeFitter>();
                if (fitter != null) fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

                if (!t.enableAutoSizing)
                {
                    t.fontSizeMin = Mathf.Max(4f, t.fontSize * 0.4f);
                    t.fontSizeMax = t.fontSize;
                    t.enableAutoSizing = true;
                }
                t.overflowMode = TextOverflowModes.Overflow;
                t.ForceMeshUpdate(true);
            }
            catch { }
        }

        private static void FitLegacy(Text t)
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
    }
}