using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace MiHomeUnitTests.Protocols;

public sealed class DeviceEventSourceTests
{
    [Fact]
    public void Device_Event_Calls_Use_Explicit_Event_Names()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "MiHome.sln")))
            root = root.Parent;
        root.Should().NotBeNull("the test must run from a build under the repository root");

        var pattern = new Regex(@"\b(?<method>RaiseEventAsync|RaiseEvent|SetAndRaise|RaiseClick)\s*\(");
        var violations = new ConcurrentQueue<string>();
        foreach (var path in Directory.EnumerateFiles(
            Path.Combine(root!.FullName, "MiHomeLib", "Devices"), "*.cs", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(path) == "GatewaySubDeviceBase.cs") continue;
            var source = File.ReadAllText(path);
            foreach (Match match in pattern.Matches(source))
            {
                var lineStart = source.LastIndexOf('\n', match.Index);
                var lineEnd = source.IndexOf('\n', match.Index);
                if (lineEnd < 0) lineEnd = source.Length;
                var line = source[(lineStart + 1)..lineEnd];
                if (line.TrimStart().StartsWith("//", StringComparison.Ordinal)) continue;
                if (line.Contains("// device-event: obsolete duplicate, not published", StringComparison.Ordinal)) continue;
                if (line.Contains("// device-event: event name supplied by caller", StringComparison.Ordinal)) continue;

                var arguments = source[(match.Index + match.Length)..];
                if (match.Groups["method"].Value == "RaiseClick")
                {
                    if (line.Contains("void RaiseClick(", StringComparison.Ordinal)) continue;
                    if (Path.GetFileName(path) == "AqaraDualWallSwitch.cs" &&
                        line.Contains("map.LumiMqtt(", StringComparison.Ordinal)) continue;
                    if (!Regex.IsMatch(arguments, @"^\s*[^,]+,\s*nameof\s*\("))
                    {
                        var lineNumber = source[..match.Index].Split('\n').Length;
                        violations.Enqueue($"{Path.GetRelativePath(root.FullName, path)}:{lineNumber}");
                    }
                    continue;
                }

                if (!Regex.IsMatch(arguments, @"^\s*nameof\s*\("))
                {
                    var lineNumber = source[..match.Index].Split('\n').Length;
                    violations.Enqueue($"{Path.GetRelativePath(root.FullName, path)}:{lineNumber}");
                }
            }
        }

        violations.Should().BeEmpty("every device event call must name its event with nameof");
    }
}
