using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BepInEx.Logging;

namespace WKMSTranslation.Core
{
    public static class FontManager
    {
        private static readonly Dictionary<string, TMP_FontAsset> _tmpCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Font> _legacyCache = new(StringComparer.OrdinalIgnoreCase);
        private static ManualLogSource _log;

        public static void Initialize(ManualLogSource log, string path)
        {
            _log = log;
            string bundlePath = Path.Combine(path, "customfonts");
            if (!File.Exists(bundlePath)) return;

            var bundle = AssetBundle.LoadFromFile(bundlePath);
            if (bundle == null) return;

            foreach (var font in bundle.LoadAllAssets<TMP_FontAsset>())
            {
                string name = font.name.Trim();
                _tmpCache[name] = font;
                _log.LogInfo($"Loaded TMP font: {name}");
            }

            foreach (var font in bundle.LoadAllAssets<Font>())
            {
                string name = font.name.Trim();
                _legacyCache[name] = font;
                _log.LogInfo($"Loaded Legacy font: {name}");
            }

            bundle.Unload(false);
        }

        public static void TryReplace(TMP_Text text)
        {
            if (text == null || _tmpCache.Count == 0) return;

            string currentFontName = text.font != null ? text.font.name.Trim() : "";

            if (_tmpCache.TryGetValue(currentFontName, out var found) ||
                _tmpCache.TryGetValue("default", out found))
            {
                if (text.font != found) text.font = found;
            }
        }

        public static void TryReplace(Text text)
        {
            if (text == null || _legacyCache == null || _legacyCache.Count == 0) return;

            string currentFontName = text.font != null ? text.font.name.Trim() : "";

            if (_legacyCache.TryGetValue(currentFontName, out var found) ||
                _legacyCache.TryGetValue("default", out found))
            {
                if (text.font != found) text.font = found;
            }

        }
    }
}