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
                    "Scoped registry entry is missing.");
            }

            JsonObject registry = FindDeucarianRegistry(scopedRegistries, out bool duplicateScope);
            if (registry == null)
            {
                return BootstrapScopedRegistryStatus.CreateRepairNeeded(
                    manifestPath,
                    "Deucarian scoped registry entry is missing.");
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
                "Deucarian scoped registry entry needs repair.");
        }

        public static BootstrapScopedRegistryRepairResult EnsureConfigured()
        {
            return EnsureConfigured(GetProjectManifestPath());
        }

        public static BootstrapScopedRegistryRepairResult EnsureConfigured(string manifestPath)
        {
            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                return BootstrapScopedRegistryRepairResult.CreateFailure("Project manifest path is empty.");
            }

            if (!File.Exists(manifestPath))
            {
                return BootstrapScopedRegistryRepairResult.CreateFailure("Packages/manifest.json was not found at " + manifestPath + ".");
            }

            if (!TryReadManifest(manifestPath, out JsonObject root, out string errorMessage))
            {
                return BootstrapScopedRegistryRepairResult.CreateFailure(errorMessage);
            }

            bool changed = false;
            JsonArray scopedRegistries = root.GetArray("scopedRegistries");

            if (scopedRegistries == null)
            {
                scopedRegistries = new JsonArray();
                root.Set("scopedRegistries", scopedRegistries);
                changed = true;
            }

            JsonObject registry = FindDeucarianRegistry(scopedRegistries, out _);
            if (registry == null)
            {
                registry = CreateScopedRegistryObject();
                scopedRegistries.Values.Add(registry);
                changed = true;
            }
            else
            {
                changed |= SetStringIfDifferent(registry, "name", DeucarianBootstrapPackageConstants.ScopedRegistryName);
                changed |= SetStringIfDifferent(registry, "url", DeucarianBootstrapPackageConstants.ScopedRegistryUrl);
                changed |= EnsureScope(registry, DeucarianBootstrapPackageConstants.ScopedRegistryScope);
            }

            changed |= RemoveDuplicateScope(scopedRegistries, registry, DeucarianBootstrapPackageConstants.ScopedRegistryScope);

            if (!changed)
            {
                return BootstrapScopedRegistryRepairResult.CreateSuccess(false, "Deucarian scoped registry is already configured.");
            }

            string json = SimpleJsonWriter.Write(root);
            File.WriteAllText(manifestPath, json, new UTF8Encoding(false));
            return BootstrapScopedRegistryRepairResult.CreateSuccess(true, "Deucarian scoped registry was configured.");
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

        private static JsonObject CreateScopedRegistryObject()
        {
            JsonObject registry = new JsonObject();
            registry.Set("name", new JsonString(DeucarianBootstrapPackageConstants.ScopedRegistryName));
            registry.Set("url", new JsonString(DeucarianBootstrapPackageConstants.ScopedRegistryUrl));

            JsonArray scopes = new JsonArray();
            scopes.Values.Add(new JsonString(DeucarianBootstrapPackageConstants.ScopedRegistryScope));
            registry.Set("scopes", scopes);
            return registry;
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

        private static bool SetStringIfDifferent(JsonObject target, string name, string value)
        {
            if (string.Equals(target.GetString(name), value, StringComparison.Ordinal))
            {
                return false;
            }

            target.Set(name, new JsonString(value));
            return true;
        }

        private static bool EnsureScope(JsonObject registry, string scope)
        {
            JsonArray scopes = registry.GetArray("scopes");
            bool changed = false;

            if (scopes == null)
            {
                scopes = new JsonArray();
                registry.Set("scopes", scopes);
                changed = true;
            }

            if (!RegistryContainsScope(registry, scope))
            {
                scopes.Values.Add(new JsonString(scope));
                changed = true;
            }

            return changed;
        }

        private static bool RemoveDuplicateScope(JsonArray scopedRegistries, JsonObject keepRegistry, string scope)
        {
            bool changed = false;

            foreach (JsonValue value in scopedRegistries.Values)
            {
                JsonObject registry = value as JsonObject;
                if (registry == null || ReferenceEquals(registry, keepRegistry))
                {
                    continue;
                }

                JsonArray scopes = registry.GetArray("scopes");
                if (scopes == null)
                {
                    continue;
                }

                for (int i = scopes.Values.Count - 1; i >= 0; i--)
                {
                    JsonString scopeString = scopes.Values[i] as JsonString;
                    if (scopeString != null && string.Equals(scopeString.Value, scope, StringComparison.Ordinal))
                    {
                        scopes.Values.RemoveAt(i);
                        changed = true;
                    }
                }
            }

            return changed;
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

        private static class SimpleJsonWriter
        {
            public static string Write(JsonValue value)
            {
                StringBuilder builder = new StringBuilder();
                WriteValue(builder, value, 0);
                builder.AppendLine();
                return builder.ToString();
            }

            private static void WriteValue(StringBuilder builder, JsonValue value, int indent)
            {
                JsonObject objectValue = value as JsonObject;
                if (objectValue != null)
                {
                    WriteObject(builder, objectValue, indent);
                    return;
                }

                JsonArray arrayValue = value as JsonArray;
                if (arrayValue != null)
                {
                    WriteArray(builder, arrayValue, indent);
                    return;
                }

                JsonString stringValue = value as JsonString;
                if (stringValue != null)
                {
                    WriteString(builder, stringValue.Value);
                    return;
                }

                JsonLiteral literalValue = value as JsonLiteral;
                builder.Append(literalValue != null ? literalValue.Value : "null");
            }

            private static void WriteObject(StringBuilder builder, JsonObject value, int indent)
            {
                builder.Append('{');

                if (value.Properties.Count > 0)
                {
                    builder.AppendLine();
                    for (int i = 0; i < value.Properties.Count; i++)
                    {
                        JsonProperty property = value.Properties[i];
                        WriteIndent(builder, indent + 1);
                        WriteString(builder, property.Name);
                        builder.Append(": ");
                        WriteValue(builder, property.Value, indent + 1);

                        if (i < value.Properties.Count - 1)
                        {
                            builder.Append(',');
                        }

                        builder.AppendLine();
                    }

                    WriteIndent(builder, indent);
                }

                builder.Append('}');
            }

            private static void WriteArray(StringBuilder builder, JsonArray value, int indent)
            {
                builder.Append('[');

                if (value.Values.Count > 0)
                {
                    builder.AppendLine();
                    for (int i = 0; i < value.Values.Count; i++)
                    {
                        WriteIndent(builder, indent + 1);
                        WriteValue(builder, value.Values[i], indent + 1);

                        if (i < value.Values.Count - 1)
                        {
                            builder.Append(',');
                        }

                        builder.AppendLine();
                    }

                    WriteIndent(builder, indent);
                }

                builder.Append(']');
            }

            private static void WriteString(StringBuilder builder, string value)
            {
                builder.Append('"');

                foreach (char c in value ?? string.Empty)
                {
                    switch (c)
                    {
                        case '"':
                            builder.Append("\\\"");
                            break;
                        case '\\':
                            builder.Append("\\\\");
                            break;
                        case '\b':
                            builder.Append("\\b");
                            break;
                        case '\f':
                            builder.Append("\\f");
                            break;
                        case '\n':
                            builder.Append("\\n");
                            break;
                        case '\r':
                            builder.Append("\\r");
                            break;
                        case '\t':
                            builder.Append("\\t");
                            break;
                        default:
                            if (char.IsControl(c))
                            {
                                builder.Append("\\u");
                                builder.Append(((int)c).ToString("x4"));
                            }
                            else
                            {
                                builder.Append(c);
                            }
                            break;
                    }
                }

                builder.Append('"');
            }

            private static void WriteIndent(StringBuilder builder, int indent)
            {
                builder.Append(' ', indent * 2);
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

    internal sealed class BootstrapScopedRegistryRepairResult
    {
        private BootstrapScopedRegistryRepairResult(bool success, bool changed, string message, string errorMessage)
        {
            Success = success;
            Changed = changed;
            Message = message ?? string.Empty;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public bool Success { get; }

        public bool Changed { get; }

        public string Message { get; }

        public string ErrorMessage { get; }

        public static BootstrapScopedRegistryRepairResult CreateSuccess(bool changed, string message)
        {
            return new BootstrapScopedRegistryRepairResult(true, changed, message, string.Empty);
        }

        public static BootstrapScopedRegistryRepairResult CreateFailure(string errorMessage)
        {
            return new BootstrapScopedRegistryRepairResult(false, false, string.Empty, errorMessage);
        }
    }
}
