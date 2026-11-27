using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace MiHomeUnitTests.Generators;

public sealed class MiotSpecImporterTests
{
    [Fact]
    public void Converts_Spec_Properties_And_Reports_Manual_Work()
    {
        var json = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "MiotSpec", "sample-instance.json"));

        var result = MiotSpecImporter.GenerateAttributes(json);

        result.Should().Be(
            "[MiotProperty(\"PowerCurrentState\", 2, 1, typeof(bool), Writable = true, Summary = \"current state\")]" + Environment.NewLine +
            "// TODO enum 2/2: 0=off, 1=on" + Environment.NewLine +
            "[MiotProperty(\"Mode\", 2, 2, typeof(byte), Writable = true, Summary = \"mode\")]" + Environment.NewLine +
            "// TODO unsupported format uint64: future value" + Environment.NewLine +
            "// TODO write-only" + Environment.NewLine +
            "[MiotProperty(\"NetworkCurrentState\", 3, 1, typeof(string), Writable = true, Summary = \"current state\")]" + Environment.NewLine +
            "[MiotProperty(\"Alias\", 3, 2, typeof(string), Writable = false, Summary = \"alias\")]" + Environment.NewLine);
    }
}
