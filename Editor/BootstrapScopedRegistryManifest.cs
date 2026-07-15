using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Deucarian.Bootstrap.Editor
{
    internal static class BootstrapScopedRegistryManifest
    {
        public static BootstrapScopedRegistryStatus GetStatus()
        {
            return GetStatus(GetProjectManifestPath());
        }

        public static BootstrapScopedRegistryStatus GetStatus(string manifestPath)
        {
            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                return BootstrapScopedRegistryStatus.CreateError(string.Empty, "Project manifest path is empty.");
            }

            if (!File.Exists(manifestPath))
            {
                return BootstrapScopedRegistryStatus.CreateError(manifestPath, "Packages/manifest.json was not found.");
            }

            if (!TryReadManifest(manifestPath, out JsonObject root, out string errorMessage))
            {
                return BootstrapScopedRegistryStatus.CreateError(manifestPath, errorMessage);
            }

            JsonArray scopedRegistries = root.GetArray("scopedRegistries");
            if (scopedRegistries == null)
            {
                return BootstrapScopedRegistryStatus.CreateRepairNeeded(
                    manifestPath,
                    "No legacy scoped registry configuration was found.");
            }

            JsonObject registry = FindDeucarianRegistry(scopedRegistries, out bool duplicateScope);
            if (registry == null)
            {
                return BootstrapScopedRegistryStatus.CreateRepairNeeded(
                    manifestPath,
                    "No legacy Deucarian scoped registry entry was found.");
            }

            bool nameMatches = string.Equals(
                registry.GetString("name"),
                DeucarianBootstrapPackageConstants.ScopedRegistryName,
                StringComparison.Ordinal);
            bool urlMatches = string.Equals(
                registry.GetString("url"),
                DeucarianBootstrapPackageConstants.ScopedRegistryUrl,
                StringComparison.OrdinalIgnoreCase);
            bool scopeMatches = RegistryContainsScope(
                registry,
                DeucarianBootstrapPackageConstants.ScopedRegistryScope);

            if (nameMatches && urlMatches && scopeMatches && !duplicateScope)
            {
                return BootstrapScopedRegistryStatus.CreateConfigured(
                    manifestPath,
                    DeucarianBootstrapPackageConstants.ScopedRegistryUrl);
            }

            return BootstrapScopedRegistryStatus.CreateRepairNeeded(
                manifestPath,
                "The existing entry does not match the legacy Deucarian scoped registry configuration.");
        }

        private static string GetProjectManifestPath()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, "Packages", "manifest.json");
        }

        private static bool TryReadManifest(string manifestPath, out JsonObject root, out string errorMessage)
        {
            root = null;
            errorMessage = string.Empty;

            string json;

            try
            {
                json = File.ReadAllText(manifestPath);
            }
            catch (Exception exception)
            {
                errorMessage = "Could not read Packages/manifest.json: " + exception.GetBaseException().Message;
                return false;
            }

            if (!SimpleJsonParser.TryParse(json, out JsonValue value, out errorMessage))
            {
                errorMessage = "Could not parse Packages/manifest.json: " + errorMessage;
                return false;
            }

            root = value as JsonObject;
            if (root == null)
            {
                errorMessage = "Packages/manifest.json must contain a JSON object.";
                return false;
            }

            return true;
        }

        private static JsonObject FindDeucarianRegistry(JsonArray scopedRegistries, out bool duplicateScope)
        {
            JsonObject bestMatch = null;
            duplicateScope = false;

            foreach (JsonValue value in scopedRegistries.Values)
            {
                JsonObject registry = value as JsonObject;
                if (registry == null)
                {
                    continue;
                }

                bool nameMatches = string.Equals(
                    registry.GetString("name"),
                    DeucarianBootstrapPackageConstants.ScopedRegistryName,
                    StringComparison.Ordinal);
                bool urlMatches = string.Equals(
                    registry.GetString("url"),
                    DeucarianBootstrapPackageConstants.ScopedRegistryUrl,
                    StringComparison.OrdinalIgnoreCase);
                bool scopeMatches = RegistryContainsScope(
                    registry,
                    DeucarianBootstrapPackageConstants.ScopedRegistryScope);

                if (bestMatch != null && scopeMatches)
                {
                    duplicateScope = true;
                }

                if (bestMatch == null && (nameMatches || urlMatches || scopeMatches))
                {
                    bestMatch = registry;
                }
            }

            return bestMatch;
        }

        private static bool RegistryContainsScope(JsonObject registry, string scope)
        {
            JsonArray scopes = registry.GetArray("scopes");
            if (scopes == null)
            {
                return false;
            }

            foreach (JsonValue scopeValue in scopes.Values)
            {
                JsonString scopeString = scopeValue as JsonString;
                if (scopeString != null && string.Equals(scopeString.Value, scope, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private abstract class JsonValue
        {
        }

        private sealed class JsonObject : JsonValue
        {
            private readonly List<JsonProperty> _properties = new List<JsonProperty>();

            public IReadOnlyList<JsonProperty> Properties => _properties;

            public string GetString(string name)
            {
                JsonString value = Get(name) as JsonString;
                return value != null ? value.Value : string.Empty;
            }

            public JsonArray GetArray(string name)
            {
                return Get(name) as JsonArray;
            }

            public JsonValue Get(string name)
            {
                foreach (JsonProperty property in _properties)
                {
                    if (string.Equals(property.Name, name, StringComparison.Ordinal))
                    {
                        return property.Value;
                    }
                }

                return null;
            }

            public void Set(string name, JsonValue value)
            {
                for (int i = 0; i < _properties.Count; i++)
                {
                    if (string.Equals(_properties[i].Name, name, StringComparison.Ordinal))
                    {
                        _properties[i] = new JsonProperty(name, value);
                        return;
                    }
                }

                _properties.Add(new JsonProperty(name, value));
            }
        }

        private sealed class JsonArray : JsonValue
        {
            public List<JsonValue> Values { get; } = new List<JsonValue>();
        }

        private sealed class JsonString : JsonValue
        {
            public JsonString(string value)
            {
                Value = value ?? string.Empty;
            }

            public string Value { get; }
        }

        private sealed class JsonLiteral : JsonValue
        {
            public JsonLiteral(string value)
            {
                Value = value ?? "null";
            }

            public string Value { get; }
        }

        private struct JsonProperty
        {
            public JsonProperty(string name, JsonValue value)
            {
                Name = name;
                Value = value;
            }

            public string Name { get; }

            public JsonValue Value { get; }
        }

        private static class SimpleJsonParser
        {
            public static bool TryParse(string json, out JsonValue value, out string errorMessage)
            {
                value = null;
                errorMessage = string.Empty;

                try
                {
                    Parser parser = new Parser(json ?? string.Empty);
                    value = parser.Parse();
                    return true;
                }
                catch (Exception exception)
                {
                    errorMessage = exception.Message;
                    return false;
                }
            }

            private sealed class Parser
            {
                private readonly string _json;
                private int _index;

                public Parser(string json)
                {
                    _json = json;
                }

                public JsonValue Parse()
                {
                    SkipWhitespace();
                    JsonValue value = ParseValue();
                    SkipWhitespace();

                    if (_index != _json.Length)
                    {
                        throw new FormatException("Unexpected trailing JSON content.");
                    }

                    return value;
                }

                private JsonValue ParseValue()
                {
                    SkipWhitespace();

                    if (_index >= _json.Length)
                    {
                        throw new FormatException("Unexpected end of JSON.");
                    }

                    char c = _json[_index];
                    if (c == '{')
                    {
                        return ParseObject();
                    }

                    if (c == '[')
                    {
                        return ParseArray();
                    }

                    if (c == '"')
                    {
                        return new JsonString(ParseString());
                    }

                    return ParseLiteral();
                }

                private JsonObject ParseObject()
                {
                    JsonObject result = new JsonObject();
                    Expect('{');
                    SkipWhitespace();

                    if (TryConsume('}'))
                    {
                        return result;
                    }

                    while (true)
                    {
                        SkipWhitespace();
                        string name = ParseString();
                        SkipWhitespace();
                        Expect(':');
                        JsonValue value = ParseValue();
                        result.Set(name, value);
                        SkipWhitespace();

                        if (TryConsume('}'))
                        {
                            return result;
                        }

                        Expect(',');
                    }
                }

                private JsonArray ParseArray()
                {
                    JsonArray result = new JsonArray();
                    Expect('[');
                    SkipWhitespace();

                    if (TryConsume(']'))
                    {
                        return result;
                    }

                    while (true)
                    {
                        result.Values.Add(ParseValue());
                        SkipWhitespace();

                        if (TryConsume(']'))
                        {
                            return result;
                        }

                        Expect(',');
                    }
                }

                private string ParseString()
                {
                    Expect('"');
                    StringBuilder builder = new StringBuilder();

                    while (_index < _json.Length)
                    {
                        char c = _json[_index++];

                        if (c == '"')
                        {
                            return builder.ToString();
                        }

                        if (c != '\\')
                        {
                            builder.Append(c);
                            continue;
                        }

                        if (_index >= _json.Length)
                        {
                            throw new FormatException("Unterminated JSON escape sequence.");
                        }

                        char escaped = _json[_index++];
                        switch (escaped)
                        {
                            case '"':
                            case '\\':
                            case '/':
                                builder.Append(escaped);
                                break;
                            case 'b':
                                builder.Append('\b');
                                break;
                            case 'f':
                                builder.Append('\f');
                                break;
                            case 'n':
                                builder.Append('\n');
                                break;
                            case 'r':
                                builder.Append('\r');
                                break;
                            case 't':
                                builder.Append('\t');
                                break;
                            case 'u':
                                builder.Append(ParseUnicodeEscape());
                                break;
                            default:
                                throw new FormatException("Unsupported JSON escape sequence \\" + escaped + ".");
                        }
                    }

                    throw new FormatException("Unterminated JSON string.");
                }

                private char ParseUnicodeEscape()
                {
                    if (_index + 4 > _json.Length)
                    {
                        throw new FormatException("Incomplete JSON unicode escape.");
                    }

                    string hex = _json.Substring(_index, 4);
                    _index += 4;
                    return (char)Convert.ToInt32(hex, 16);
                }

                private JsonValue ParseLiteral()
                {
                    int start = _index;

                    while (_index < _json.Length)
                    {
                        char c = _json[_index];
                        if (char.IsWhiteSpace(c) || c == ',' || c == ']' || c == '}')
                        {
                            break;
                        }

                        _index++;
                    }

                    if (start == _index)
                    {
                        throw new FormatException("Expected JSON value.");
                    }

                    string literal = _json.Substring(start, _index - start);
                    if (literal == "true" || literal == "false" || literal == "null" || IsNumberLiteral(literal))
                    {
                        return new JsonLiteral(literal);
                    }

                    throw new FormatException("Invalid JSON literal " + literal + ".");
                }

                private static bool IsNumberLiteral(string value)
                {
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return false;
                    }

                    double ignored;
                    return double.TryParse(
                        value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out ignored);
                }

                private bool TryConsume(char expected)
                {
                    if (_index < _json.Length && _json[_index] == expected)
                    {
                        _index++;
                        return true;
                    }

                    return false;
                }

                private void Expect(char expected)
                {
                    if (_index >= _json.Length || _json[_index] != expected)
                    {
                        throw new FormatException("Expected '" + expected + "'.");
                    }

                    _index++;
                }

                private void SkipWhitespace()
                {
                    while (_index < _json.Length && char.IsWhiteSpace(_json[_index]))
                    {
                        _index++;
                    }
                }
            }
        }

    }

    internal sealed class BootstrapScopedRegistryStatus
    {
        private BootstrapScopedRegistryStatus(
            string manifestPath,
            bool configured,
            bool needsRepair,
            string detail)
        {
            ManifestPath = manifestPath ?? string.Empty;
            Configured = configured;
            NeedsRepair = needsRepair;
            Detail = detail ?? string.Empty;
        }

        public string ManifestPath { get; }

        public bool Configured { get; }

        public bool NeedsRepair { get; }

        public string Detail { get; }

        public static BootstrapScopedRegistryStatus CreateConfigured(string manifestPath, string detail)
        {
            return new BootstrapScopedRegistryStatus(manifestPath, true, false, detail);
        }

        public static BootstrapScopedRegistryStatus CreateRepairNeeded(string manifestPath, string detail)
        {
            return new BootstrapScopedRegistryStatus(manifestPath, false, true, detail);
        }

        public static BootstrapScopedRegistryStatus CreateError(string manifestPath, string detail)
        {
            return new BootstrapScopedRegistryStatus(manifestPath, false, false, detail);
        }
    }

}
