using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WKMSTranslation.Utils;

namespace WKMSTranslation.Core
{
    public static class Exporter
    {
        private static string _folder = "";
        private static readonly HashSet<string> _exported = new(StringComparer.Ordinal);
        private static readonly Regex _numFinder = new(@"\d+(?:[.,]\d+)?", RegexOptions.Compiled);
        private static readonly Regex _ghostFilter = new(@"[a-zA-Z]\d+$", RegexOptions.Compiled);
        private static readonly Regex _gameTokenFinder = new(@"\{[a-zA-Z_][a-zA-Z0-9_]*\}", RegexOptions.Compiled);

        private static readonly Regex _garbageFilter = new(@"^[.\-_~=\s\/\\|:;()\[\]{}*+?!@#$%^&<>,""']*$", RegexOptions.Compiled);

        private static readonly HashSet<string> _ignoredKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "Shift", "Tab", "Space", "Ctrl", "Alt", "Enter", "Escape", "Caps", "Backspace",
            "LMB", "RMB", "MMB", "Wheel", "WheelUp", "WheelDown", "M4", "M5",
            "Up", "Down", "Left", "Right", "W", "A", "S", "D",
            "Insert", "Delete", "Home", "End", "PageUp", "PageDown", "PrintScreen", "ScrollLock", "Pause",
            "NumLock", "NumSlash", "NumStar", "NumMinus", "NumPlus", "NumEnter",
            "Num0", "Num1", "Num2", "Num3", "Num4", "Num5", "Num6", "Num7", "Num8", "Num9",
            "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
            "Win", "Cmd", "Fn", "Tilde",
            "LB", "RB", "LT", "RT", "L1", "R1", "L2", "R2", "L3", "R3",
            "D-PadUp", "D-PadDown", "D-PadLeft", "D-PadRight",
            "A", "B", "X", "Y", "Cross", "Circle", "Square", "Triangle",
            "LS", "RS", "LS-Click", "RS-Click", "LeftStick", "RightStick",
            "Start", "Select", "Back", "Options", "Share", "Menu", "View", "Guide", "Home", "Touchpad"
        };

        public static void Initialize(string path) => _folder = path;

        public static void Register(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            if (Regex.IsMatch(text, @"[а-яА-Я]")) return;

            string key = text.GetExactKey();

            if (key.Length <= 1) return;

            if (_garbageFilter.IsMatch(key)) return;

            if (key.StartsWith("<") && key.EndsWith(">") && !key.Contains(" ")) return;

            if (_ignoredKeywords.Contains(key)) return;

            if (Regex.IsMatch(key, @"^[+-]?\d+(?:[.,]\d+)?$")) return;
            if (Regex.IsMatch(key, @"^\d{1,2}(?::\d{1,2}){1,3}$")) return;
            if (_ghostFilter.IsMatch(key)) return;
            if (_gameTokenFinder.IsMatch(key)) return;

            if (TranslationEngine.IsTranslated(text)) return;

            string template = ConvertToTemplate(key);
            if (TranslationEngine.IsTranslated(template)) return;

            lock (_exported) { _exported.Add(template); }
        }

        private static string ConvertToTemplate(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (!Regex.IsMatch(text, @"\d")) return text;

            int index = 0;
            return _numFinder.Replace(text, m => $"{{{index++}}}");
        }

        public static void Save()
        {
            CollectFromMemory();
            try
            {
                if (string.IsNullOrEmpty(_folder)) return;
                Directory.CreateDirectory(_folder);

                string path = Path.Combine(_folder, "DumpedTexts.json");
                var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (File.Exists(path))
                {
                    try
                    {
                        var existing = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path));
                        if (existing != null) data = existing;
                    }
                    catch { }
                }

                int added = 0;
                lock (_exported)
                {
                    foreach (var item in _exported)
                    {
                        if (!data.ContainsKey(item) && !TranslationEngine.IsTranslatedDeep(item))
                        {
                            data[item] = "";
                            added++;
                        }
                    }
                }

                File.WriteAllText(path, JsonConvert.SerializeObject(data, Formatting.Indented));
                Debug.Log($"[Exporter] Saved {added} new strings. Total: {data.Count}");
            }
            catch (Exception e) { Debug.LogError(e); }
        }

        private static void CollectFromMemory()
        {
            try
            {
                foreach (var txt in Resources.FindObjectsOfTypeAll<TMP_Text>())
                    if (txt.gameObject.activeInHierarchy && !string.IsNullOrEmpty(txt.text)) Register(txt.text);
                foreach (var txt in Resources.FindObjectsOfTypeAll<Text>())
                    if (txt.gameObject.activeInHierarchy && !string.IsNullOrEmpty(txt.text)) Register(txt.text);
            }
            catch { }
        }
    }
}