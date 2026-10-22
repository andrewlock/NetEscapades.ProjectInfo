using System;
using System.Collections.Generic;

namespace NetEscapades.ProjectInfo.Tests;

/// <summary>Builds the analyzer options that the MSBuild targets would produce for a set of project items</summary>
internal static class ProjectInfoItems
{
    public const string ItemsKey = "build_property.ProjectInfoItems";

    public static string S(string name, string value) => Entry('S', name, value);

    public static string U(string name, string value) => Entry('U', name, value);

    private static string Entry(char kind, string name, string value)
        => $"{kind}|{Uri.EscapeDataString(name)}|{Uri.EscapeDataString(value)}";

    public static Dictionary<string, string> Config(string[] items, params (string Key, string Value)[] others)
    {
        var config = new Dictionary<string, string>();
        if (items.Length > 0)
        {
            config[ItemsKey] = string.Join(",", items);
        }

        foreach (var (key, value) in others)
        {
            config[key] = value;
        }

        return config;
    }
}
