using Hongdal.Contracts.Common.PublicData;
using 홍달.Services.External.PublicData;

namespace Hongdal.Tests.Services.External.PublicData;

public sealed class PublicDataApiMetadataCatalogTests
{
    [Fact]
    public void GetCatalog_수입식품서버모듈의공식계약을제공한다()
    {
        var catalog = new PublicDataApiMetadataCatalog();

        var result = catalog.GetCatalog(new PublicDataApiMetadataQuery
        {
            Domain = "ImportedFood"
        });

        Assert.Equal(3, result.Items.Count);
        Assert.Contains(result.Items, item => item.Key == "mfds-imported-food-product-db");
        Assert.Contains(result.Items, item => item.Key == "mfds-imported-food-overseas-manufacturer");

        var label = Assert.Single(result.Items, item => item.Key == "mfds-imported-food-korean-label");
        Assert.Contains("IRDNT_NM", label.MainResponseFields);
        Assert.Contains(label.UsageNotes, note => note.Contains("확정값이 아니라 후보", StringComparison.Ordinal));

        var product = Assert.Single(result.Items, item => item.Key == "mfds-imported-food-product-db");
        Assert.Contains(product.UsageNotes, note => note.Contains("HSK 코드가 아니므로", StringComparison.Ordinal));
    }
}
