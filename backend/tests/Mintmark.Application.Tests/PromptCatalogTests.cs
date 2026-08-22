using FluentAssertions;
using Mintmark.Application;

namespace Mintmark.Application.Tests;

public class PromptCatalogTests
{
    [Fact]
    public void IdentifyTemplateVersion_IsIdentifyV1()
    {
        PromptCatalog.IdentifyPromptTemplateVersion.Should().Be("identify-v1");
    }

    [Fact]
    public void IdentifyTemplate_ContainsTheContractsAbsoluteRules()
    {
        var template = PromptCatalog.IdentifyPromptTemplate;

        // The rules that make the audit trail trustworthy.
        template.Should().Contain("Null beats guessing");
        template.Should().Contain("Never infer a year from a design era; read the date.");
        template.Should().Contain("sizeEstimateTroyOz");
        template.Should().Contain("{EDGE_CLAUSE}");

        // The required JSON shape.
        template.Should().Contain("\"finishAttributes\": []");
        template.Should().Contain("\"authenticityFlags\"");

        // The finish definitions keyed on by the premium rules.
        template.Should().Contain("ReverseProof");
        template.Should().Contain("HighRelief");

        // Advisory-only language for authenticity.
        template.Should().Contain("Report observations, never verdicts.");
    }
}
