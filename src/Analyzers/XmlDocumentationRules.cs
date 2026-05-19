using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Analyzers;

internal sealed class XmlDocumentationRule
{
    public XmlDocumentationRule(Regex canonicalDescriptorRegex, Regex invalidValueRegex, string replacement)
    {
        CanonicalDescriptorRegex = canonicalDescriptorRegex;
        InvalidValueRegex = invalidValueRegex;
        Replacement = replacement;
    }

    public Regex CanonicalDescriptorRegex { get; }

    public Regex InvalidValueRegex { get; }

    public string Replacement { get; }
}

internal static class XmlDocumentationRules
{
    public const string ReturnsRulesFileName = "XmlDocs.Returns.rules";
    public const string SummaryRulesFileName = "XmlDocs.Summary.rules";
    public const string ParamRulesFileName = "XmlDocs.Param.rules";
    public const string TypeParamRulesFileName = "XmlDocs.TypeParam.rules";

    public static ImmutableArray<XmlDocumentationRule> GetRules(AnalyzerOptions options, string fileName)
    {
        foreach (AdditionalText additionalFile in options.AdditionalFiles)
        {
            if (!string.Equals(System.IO.Path.GetFileName(additionalFile.Path), fileName, StringComparison.Ordinal))
                continue;

            SourceText? text = additionalFile.GetText();
            if (text is null)
                return ImmutableArray<XmlDocumentationRule>.Empty;

            var builder = ImmutableArray.CreateBuilder<XmlDocumentationRule>();

            foreach (TextLine line in text.Lines)
            {
                string value = line.ToString();
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                string trimmed = value.TrimStart();
                if (trimmed.StartsWith("#", StringComparison.Ordinal))
                    continue;

                string[] parts = value.Split(new[] { ';' }, 3, StringSplitOptions.None);
                if (parts.Length != 3)
                    continue;

                try
                {
                    Regex canonicalRegex = new(parts[0], RegexOptions.CultureInvariant);
                    Regex invalidRegex = new(parts[1], RegexOptions.CultureInvariant);
                    builder.Add(new XmlDocumentationRule(canonicalRegex, invalidRegex, parts[2]));
                }
                catch (ArgumentException)
                {
                    // Invalid regex rules are ignored.
                }
            }

            return builder.ToImmutable();
        }

        return ImmutableArray<XmlDocumentationRule>.Empty;
    }

    public static XmlDocumentationRule? FindFirstMatchingRule(ImmutableArray<XmlDocumentationRule> rules, string canonicalDescriptor)
    {
        foreach (XmlDocumentationRule rule in rules)
        {
            if (rule.CanonicalDescriptorRegex.IsMatch(canonicalDescriptor))
                return rule;
        }

        return null;
    }

    public static bool IsConfiguredInvalid(XmlDocumentationRule? rule, string normalizedValue) =>
        rule is not null && rule.InvalidValueRegex.IsMatch(normalizedValue);

    public static string ApplyReplacement(XmlDocumentationRule rule, string canonicalDescriptor, string currentValue)
    {
        Match canonicalMatch = rule.CanonicalDescriptorRegex.Match(canonicalDescriptor);
        Match invalidMatch = rule.InvalidValueRegex.Match(currentValue);

        string result = rule.Replacement;
        result = ReplaceNamedGroups(result, canonicalMatch, rule.CanonicalDescriptorRegex.GetGroupNames());
        result = ReplaceNamedGroups(result, invalidMatch, rule.InvalidValueRegex.GetGroupNames());

        return result;
    }

    private static string ReplaceNamedGroups(string value, Match match, IEnumerable<string> groupNames)
    {
        if (!match.Success)
            return value;

        foreach (string groupName in groupNames.Where(static k => !int.TryParse(k, out _)))
        {
            Group group = match.Groups[groupName];
            if (!group.Success)
                continue;

            value = value.Replace("${" + groupName + "}", group.Value);
        }

        return value;
    }
}
