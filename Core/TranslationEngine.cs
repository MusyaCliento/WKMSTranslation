using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using UnityEngine;
using WKMSTranslation.Utils;

namespace WKMSTranslation.Core
{
    public static class TranslationEngine
    {
        public static bool IsEnabled { get; set; } = true;
        private static string _lastPath = "";
        private static readonly HashSet<string> _allKeys = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> _allValues = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> _exact = new(StringComparer.OrdinalIgnoreCase);
        private static readonly List<TemplateEntry> _templates = new();
        private static readonly ConditionalWeakTable<object, string> _originals = new();
        private static readonly HashSet<string> _fitKeys = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> _cache = new(StringComparer.Ordinal);
        private static readonly ConditionalWeakTable<object, string> _assignedTranslations = new();
        private static readonly List<SampleTemplateEntry> _sampleTemplates = new();
        private static readonly List<SampleGroupEntry> _groupTemplates = new();
        private static readonly List<SampleREntry> _conditionalTemplates = new();

        private class TemplateEntry
        {
            public Regex CompiledRegex;
            public string TranslationTemplate;
            public string OriginalPattern;
            public List<string> GroupPlaceholders;
            public string AnchorAnchorText;
        }

        private class SampleTemplateEntry
        {
            public string OriginalKey;
            public Regex CompiledRegex;
            public string TranslationTemplate;
            public List<string> Placeholders;
            public bool IsAtomic;
            public bool IsLiteral;
            public string AnchorAnchorText;
        }

        private class SampleGroupEntry
        {
            public List<string> Headers = new List<string>();
            public List<SampleTemplateEntry> Rules = new List<SampleTemplateEntry>();
        }
        private class SampleREntry
        {
            public int RequiredGroupCount;
            public List<SampleTemplateEntry> MandatoryRules = new();
            public Dictionary<int, List<SampleTemplateEntry>> IndexedRules = new();
        }

        public static void Initialize(string path) { _lastPath = path; Load(); }

        public static void Load()
        {
            _exact.Clear(); _templates.Clear(); _allKeys.Clear(); _allValues.Clear(); _fitKeys.Clear(); _cache.Clear();
            _sampleTemplates.Clear(); _groupTemplates.Clear(); _conditionalTemplates.Clear();
            if (!Directory.Exists(_lastPath)) Directory.CreateDirectory(_lastPath);
            foreach (string file in Directory.GetFiles(_lastPath, "*.json"))
            {
                if (Path.GetFileName(file).Equals("DumpedTexts.json", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    var jObject = JObject.Parse(File.ReadAllText(file));
                    foreach (var prop in jObject.Properties()) ProcessToken(prop.Value, prop.Name);
                }
                catch { }
            }

            _sampleTemplates.Sort((a, b) =>
            {
                if (a.IsAtomic != b.IsAtomic) return a.IsAtomic.CompareTo(b.IsAtomic);
                return b.TranslationTemplate.Length.CompareTo(a.TranslationTemplate.Length);
            });

            Debug.Log($"[WKTranslator] Loaded {_exact.Count} exact, {_templates.Count} templates, {_sampleTemplates.Count} samples, {_groupTemplates.Count} groups.");
        }

        private static void ProcessToken(JToken token, string keyName)
        {
            if (keyName.StartsWith("sampleR", StringComparison.OrdinalIgnoreCase) && token.Type == JTokenType.Object)
            {
                var rGroup = new SampleREntry();

                var matchCount = Regex.Match(keyName, @"\d+");
                rGroup.RequiredGroupCount = matchCount.Success ? int.Parse(matchCount.Value) : 2;

                foreach (var sub in ((JObject)token).Properties())
                {
                    string name = sub.Name;
                    string val = sub.Value.ToString();

                    if (name.StartsWith("request:", StringComparison.OrdinalIgnoreCase))
                    {
                        string trigger = name.Substring(8).GetExactKey();
                        rGroup.MandatoryRules.Add(CreateSampleEntry(trigger, val));
                    }
                    else
                    {
                        var reqMatch = Regex.Match(name, @"^request(\d+):(.*)", RegexOptions.IgnoreCase);
                        if (reqMatch.Success)
                        {
                            int groupId = int.Parse(reqMatch.Groups[1].Value);
                            string trigger = reqMatch.Groups[2].Value.GetExactKey();
                            var rule = CreateSampleEntry(trigger, val);

                            if (!rGroup.IndexedRules.ContainsKey(groupId))
                                rGroup.IndexedRules[groupId] = new List<SampleTemplateEntry>();

                            rGroup.IndexedRules[groupId].Add(rule);

                            if (!string.IsNullOrWhiteSpace(val))
                                _allKeys.Add(rule.OriginalKey);
                        }
                    }
                }
                _conditionalTemplates.Add(rGroup);
                return;
            }
            if (keyName.StartsWith("sampleG:", StringComparison.OrdinalIgnoreCase) && token.Type == JTokenType.Object)
            {
                var group = new SampleGroupEntry();
                foreach (var sub in ((JObject)token).Properties())
                {
                    if (sub.Name.StartsWith("header:", StringComparison.OrdinalIgnoreCase))
                    {
                        string trigger = sub.Name.Substring(7).GetExactKey();

                        string headerCheck = trigger.StartsWith("tag:", StringComparison.OrdinalIgnoreCase) ? trigger.Substring(4) : trigger;
                        group.Headers.Add(headerCheck);

                        if (!string.IsNullOrEmpty(sub.Value.ToString()))
                        {
                            var rule = CreateSampleEntry(trigger, sub.Value.ToString());
                            group.Rules.Add(rule);
                            _allKeys.Add(rule.OriginalKey);
                        }
                    }
                    else
                    {
                        var rule = CreateSampleEntry(sub.Name, sub.Value.ToString());
                        group.Rules.Add(rule);
                        _allKeys.Add(rule.OriginalKey);
                    }
                }
                _groupTemplates.Add(group);
                return;
            }

            if (keyName.StartsWith("sample:", StringComparison.OrdinalIgnoreCase) && token.Type == JTokenType.Object)
            {
                foreach (var sub in ((JObject)token).Properties())
                {
                    var rule = CreateSampleEntry(sub.Name, sub.Value.ToString());
                    _sampleTemplates.Add(rule);
                }
                return;
            }

            string trimmedKey = keyName.GetExactKey();
            if (string.IsNullOrEmpty(trimmedKey)) return;

            if (token.Type == JTokenType.Object)
            {
                _allKeys.Add(trimmedKey);
                foreach (var sub in ((JObject)token).Properties()) ProcessToken(sub.Value, sub.Name);
            }
            else if (token.Type == JTokenType.String)
            {
                string val = token.ToString().Trim();
                if (string.IsNullOrEmpty(val)) return;

                if (val.StartsWith("sample:", StringComparison.OrdinalIgnoreCase))
                {
                    var rule = CreateSampleEntry(keyName, val.Substring(7).Trim());
                    _sampleTemplates.Add(rule);
                    return;
                }

                if (val.StartsWith("fit:", StringComparison.OrdinalIgnoreCase))
                {
                    val = val.Substring(4).Trim();
                    _fitKeys.Add(trimmedKey);
                }

                _allKeys.Add(trimmedKey);
                _allValues.Add(val.GetExactKey());

                if (Regex.IsMatch(trimmedKey, @"\{#?\d+\}")) AddTemplate(keyName, val);
                else _exact[trimmedKey] = val;
            }
        }

        private static SampleTemplateEntry CreateSampleEntry(string key, string val)
        {
            string exactKey = key.GetExactKey();
            bool isLiteral = false;

            if (exactKey.StartsWith("tag:", StringComparison.OrdinalIgnoreCase))
            {
                isLiteral = true;
                exactKey = exactKey.Substring(4);
            }

            var placeholders = new List<string>();
            foreach (Match m in Regex.Matches(exactKey, @"\{(\d+)\}"))
                placeholders.Add(m.Groups[1].Value);

            bool isAtomic = placeholders.Count == 0;
            string pattern = Regex.Escape(exactKey);
            pattern = Regex.Replace(pattern, @"\\\{\d+\}", @"(.+?)");

            if (isAtomic && !isLiteral)
            {
                string prefix = Regex.IsMatch(exactKey, @"^\w") ? @"(?<=^|\W|\\n|\\r)" : "";
                string suffix = Regex.IsMatch(exactKey, @"\w$") ? @"(?=\W|$)" : "";
                pattern = prefix + pattern + suffix;
            }

            string anchor = GetLongestStaticChunk(exactKey);

            return new SampleTemplateEntry
            {
                OriginalKey = exactKey,
                CompiledRegex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
                TranslationTemplate = val,
                Placeholders = placeholders,
                IsAtomic = isAtomic,
                IsLiteral = isLiteral,
                AnchorAnchorText = anchor
            };
        }

        private static void AddTemplate(string key, string val)
        {
            try
            {
                string cleanKey = key.Trim();
                var placeholders = new List<string>();
                foreach (Match m in Regex.Matches(cleanKey, @"\{(#?\d+)\}")) placeholders.Add(m.Groups[1].Value);

                string maskedKey = cleanKey;
                maskedKey = Regex.Replace(maskedKey, @"\{\#\d+\}", "__KEYBINDGROUP__");
                maskedKey = Regex.Replace(maskedKey, @"\{\d+\}", "__DIGITGROUP__");

                string pattern = Regex.Escape(maskedKey);
                pattern = pattern.Replace("\\\"", "[\"']?").Replace("\"", "[\"']?");
                pattern = pattern.Replace("\\'", "[\"']?").Replace("'", "[\"']?");
                pattern = pattern.Replace("__DIGITGROUP__", @"([+-]?\d+(?:[.,]\d+)?)");
                pattern = pattern.Replace("__KEYBINDGROUP__", @"([^\n<>]+?)");
                pattern = pattern.Replace(@"\n", @"\n").Replace(@"\\n", @"(?:\r?\n|\\n)");
                pattern = @"^\s*" + pattern + @"\s*$";

                string anchor = GetLongestStaticChunk(cleanKey);

                _templates.Add(new TemplateEntry
                {
                    CompiledRegex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant),
                    TranslationTemplate = val,
                    OriginalPattern = key.GetExactKey(),
                    GroupPlaceholders = placeholders,
                    AnchorAnchorText = anchor
                });
            }
            catch (Exception ex) { Debug.LogError($"[TranslationEngine] Error key: {key}: {ex.Message}"); }
        }

        public static string GetTranslation(string currentText)
        {
            if (string.IsNullOrEmpty(currentText) || currentText.Length <= 1) return currentText;
            if (_cache.TryGetValue(currentText, out var cached)) return cached;

            string exactKey = currentText.GetExactKey();

            if (_exact.TryGetValue(exactKey, out var exactTranslation))
                return _cache[currentText] = exactTranslation.Replace("\\n", "\n");

            string cleaned = exactKey.Replace("\\n", "\n");

            foreach (var t in _templates)
            {
                if (!string.IsNullOrEmpty(t.AnchorAnchorText) && cleaned.IndexOf(t.AnchorAnchorText, StringComparison.OrdinalIgnoreCase) == -1)
                    continue;

                var match = t.CompiledRegex.Match(cleaned);
                if (match.Success)
                {
                    bool hasRawTokens = false;
                    for (int g = 1; g < match.Groups.Count; g++)
                    {
                        if (match.Groups[g].Value.Contains("{") || match.Groups[g].Value.Contains("}"))
                        {
                            hasRawTokens = true;
                            break;
                        }
                    }
                    if (hasRawTokens) continue;

                    string result = t.TranslationTemplate;
                    for (int i = 1; i < match.Groups.Count; i++)
                        result = result.Replace("{" + t.GroupPlaceholders[i - 1] + "}", match.Groups[i].Value);

                    if (_fitKeys.Contains(t.OriginalPattern)) _fitKeys.Add(exactKey);
                    return _cache[currentText] = result.Replace("\\n", "\n");
                }
            }

            string workingText = exactKey;
            bool anyTranslated = false;

            foreach (var rGroup in _conditionalTemplates)
            {
                bool mandatoryPassed = true;
                foreach (var mRule in rGroup.MandatoryRules)
                {
                    bool match = mRule.IsLiteral
                        ? workingText.IndexOf(mRule.OriginalKey, StringComparison.OrdinalIgnoreCase) != -1
                        : mRule.CompiledRegex.IsMatch(workingText);

                    if (!match) { mandatoryPassed = false; break; }
                }

                if (!mandatoryPassed) continue;
                int matchedGroupsCount = 0;
                List<SampleTemplateEntry> rulesToApply = new();

                foreach (var kvp in rGroup.IndexedRules)
                {
                    bool groupMatched = false;
                    foreach (var rule in kvp.Value)
                    {
                        bool match = rule.IsLiteral
                            ? workingText.IndexOf(rule.OriginalKey, StringComparison.OrdinalIgnoreCase) != -1
                            : rule.CompiledRegex.IsMatch(workingText);

                        if (match)
                        {
                            groupMatched = true;
                            rulesToApply.Add(rule);
                        }
                    }
                    if (groupMatched) matchedGroupsCount++;
                }

                if (matchedGroupsCount >= rGroup.RequiredGroupCount)
                {
                    foreach (var mRule in rGroup.MandatoryRules)
                        workingText = ApplySampleReplacement(workingText, mRule, ref anyTranslated);

                    rulesToApply.Sort((a, b) => b.OriginalKey.Length.CompareTo(a.OriginalKey.Length));

                    foreach (var rule in rulesToApply)
                    {
                        workingText = ApplySampleReplacement(workingText, rule, ref anyTranslated);
                    }
                }
            }

            foreach (var group in _groupTemplates)
            {
                if (group.Headers.Count > 0 && group.Headers.Exists(h => workingText.IndexOf(h, StringComparison.OrdinalIgnoreCase) != -1))
                {
                    foreach (var rule in group.Rules)
                    {
                        workingText = ApplySampleReplacement(workingText, rule, ref anyTranslated);
                    }
                }
            }

            foreach (var sample in _sampleTemplates)
            {
                if (!string.IsNullOrEmpty(sample.AnchorAnchorText) && workingText.IndexOf(sample.AnchorAnchorText, StringComparison.OrdinalIgnoreCase) == -1)
                    continue;

                workingText = ApplySampleReplacement(workingText, sample, ref anyTranslated);
            }

            if (anyTranslated)
            {
                return _cache[currentText] = workingText.Replace("\\n", "\n");
            }

            return _cache[currentText] = currentText;
        }

        public static void SetAssignedTranslation(object ui, string translatedText)
        {
            if (ui == null) return;
            _assignedTranslations.Remove(ui);
            if (!string.IsNullOrEmpty(translatedText))
            {
                _assignedTranslations.Add(ui, translatedText);
            }
        }

        private static string ApplySampleReplacement(string input, SampleTemplateEntry sample, ref bool anyTranslated)
        {
            if (sample.IsLiteral)
            {
                if (input.IndexOf(sample.OriginalKey, StringComparison.OrdinalIgnoreCase) != -1)
                {
                    string result = Regex.Replace(input, Regex.Escape(sample.OriginalKey), sample.TranslationTemplate.Replace("$", "$$"), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                    if (result != input) { anyTranslated = true; return result; }
                }
                return input;
            }

            if (sample.IsAtomic)
            {
                if (input.IndexOf(sample.OriginalKey, StringComparison.OrdinalIgnoreCase) != -1)
                {
                    string result = sample.CompiledRegex.Replace(input, (m) =>
                    {
                        int searchStart = m.Index - 1;
                        if (searchStart >= 0 && searchStart < input.Length)
                        {
                            int openTag = input.LastIndexOf('<', searchStart);
                            int closeTag = input.LastIndexOf('>', searchStart);
                            if (openTag > closeTag) return m.Value;

                            int openBrace = input.LastIndexOf('{', searchStart);
                            int closeBrace = input.LastIndexOf('}', searchStart);
                            if (openBrace > closeBrace) return m.Value;
                        }

                        return sample.TranslationTemplate;
                    });
                    if (result != input) { anyTranslated = true; return result; }
                }
            }
            else
            {
                if (sample.CompiledRegex.IsMatch(input))
                {
                    string result = sample.CompiledRegex.Replace(input, (match) =>
                    {
                        string translatedSample = sample.TranslationTemplate;
                        for (int g = 1; g < match.Groups.Count; g++)
                        {
                            string placeholderName = sample.Placeholders[g - 1];
                            string capturedValue = match.Groups[g].Value.Trim();
                            translatedSample = translatedSample.Replace("{" + placeholderName + "}", capturedValue);
                        }
                        return translatedSample;
                    });
                    anyTranslated = true;
                    return result;
                }
            }
            return input;
        }

        private static string GetLongestStaticChunk(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            string clean = Regex.Replace(input, @"<[^>]+>", " ");
            clean = Regex.Replace(clean, @"\{\#?\d+\}", " ");
            clean = clean.Replace("\\n", " ").Replace("<noparse>", "").Replace("</noparse>", "");

            string[] chunks = clean.Split(new[] { ' ', '\t', '.', ',', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            string longest = string.Empty;
            foreach (var chunk in chunks) { if (chunk.Length > longest.Length) longest = chunk; }
            return longest.Length > 3 ? longest : string.Empty;
        }

        public static bool IsTranslated(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            string key = text.GetExactKey();
            return _allKeys.Contains(key) || _exact.ContainsKey(key) || _allValues.Contains(key);
        }

        public static bool IsTranslatedDeep(string text)
        {
            if (IsTranslated(text)) return true;
            string key = text.GetExactKey();
            foreach (var t in _templates) if (t.CompiledRegex.IsMatch(text) || t.OriginalPattern == key) return true;
            return false;
        }

        public static bool IsCyrillic(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return Regex.IsMatch(text, @"[а-яА-ЯёЁ]");
        }

        public static bool IsAssignedTranslation(object ui, string text)
        {
            if (ui == null || string.IsNullOrEmpty(text)) return false;
            if (_assignedTranslations.TryGetValue(ui, out string lastAssigned))
            {
                return lastAssigned == text || lastAssigned.GetExactKey() == text.GetExactKey();
            }
            return false;
        }

        public static bool RegisterOriginal(object ui, string text)
        {
            if (ui == null || string.IsNullOrEmpty(text)) return true;
            string exactKey = text.GetExactKey();

            if (_assignedTranslations.TryGetValue(ui, out string lastAssigned))
            {
                if (lastAssigned.GetExactKey() == exactKey) return false;
            }

            if (_originals.TryGetValue(ui, out string existing) && existing.GetExactKey() == exactKey) return true;
            if (_allValues.Contains(exactKey) && !_allKeys.Contains(exactKey)) return false;

            _originals.Remove(ui);
            _originals.Add(ui, text);
            return true;
        }

        public static string GetOriginal(object ui, string current) => (ui != null && _originals.TryGetValue(ui, out string orig)) ? orig : current;
        public static bool IsFitRequired(string key) => _fitKeys.Contains(key.GetExactKey());
    }
}