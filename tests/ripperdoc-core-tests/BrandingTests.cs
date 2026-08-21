using Ripperdoc.Core;
using Xunit;

namespace Ripperdoc.Core.Tests;

public class BrandingTests
{
    [Fact]
    public void ToolPrefixDerivesFromTheBrandConstant()
    {
        Assert.Equal(Branding.Name + "_", Branding.ToolPrefix);
    }

    [Fact]
    public void BrandIsLowerCaseAndCarriesNoSeparators()
    {
        // The prefix is concatenated straight into snake_case tool names, so a
        // capital or a separator in the brand would produce names that cannot
        // match the surface convention.
        Assert.Equal(Branding.Name, Branding.Name.ToLowerInvariant());
        Assert.DoesNotContain('_', Branding.Name);
        Assert.DoesNotContain('-', Branding.Name);
    }
}
