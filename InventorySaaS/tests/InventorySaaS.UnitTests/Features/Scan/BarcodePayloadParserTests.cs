using FluentAssertions;
using InventorySaaS.Application.Common.Barcodes;

namespace InventorySaaS.UnitTests.Features.Scan;

public class BarcodePayloadParserTests
{
    [Theory]
    [InlineData("8801234567890")]
    [InlineData("SKU-00042")]
    [InlineData("  8801234567890  ")]
    public void Parse_ShouldTreatOrdinaryBarcodesAsPlainCodes(string raw)
    {
        var payload = BarcodePayloadParser.Parse(raw);

        payload.Format.Should().Be(BarcodePayloadFormat.Plain);
        payload.Code.Should().Be(raw.Trim());
        payload.BatchNumber.Should().BeNull();
        payload.SerialNumber.Should().BeNull();
        payload.ExpiryDate.Should().BeNull();
    }

    [Fact]
    public void Parse_ShouldReturnEmptyCode_ForNullOrBlankInput()
    {
        BarcodePayloadParser.Parse(null).Code.Should().BeEmpty();
        BarcodePayloadParser.Parse("   ").Code.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ShouldReadParenthesisedGs1Elements()
    {
        var payload = BarcodePayloadParser.Parse("(01)08801234567890(10)B2401(17)271231(21)SN-9(30)12");

        payload.Format.Should().Be(BarcodePayloadFormat.Gs1);
        payload.Code.Should().Be("08801234567890");
        payload.BatchNumber.Should().Be("B2401");
        payload.SerialNumber.Should().Be("SN-9");
        payload.Quantity.Should().Be(12);
        payload.ExpiryDate.Should().Be(new DateTime(2027, 12, 31, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Parse_ShouldReadConcatenatedGs1ElementsSeparatedByFnc1()
    {
        // Fixed-length 01 and 17 run together; the variable-length 10 is FNC1-terminated.
        var payload = BarcodePayloadParser.Parse("010880123456789010B240117271231");

        payload.Format.Should().Be(BarcodePayloadFormat.Gs1);
        payload.Code.Should().Be("08801234567890");
        payload.BatchNumber.Should().Be("B2401");
        payload.ExpiryDate.Should().Be(new DateTime(2027, 12, 31, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Parse_ShouldExpandGs1DayZeroToLastDayOfMonth()
    {
        var payload = BarcodePayloadParser.Parse("(01)08801234567890(17)270200");

        payload.ExpiryDate.Should().Be(new DateTime(2027, 2, 28, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Parse_ShouldNotMistakeAnOrdinaryBarcodeStartingWith01ForGs1()
    {
        // A 13-digit EAN beginning "01" must stay a plain code — misreading it as a GTIN
        // element would silently truncate the value and resolve to the wrong product.
        var payload = BarcodePayloadParser.Parse("0123456789012");

        payload.Format.Should().Be(BarcodePayloadFormat.Plain);
        payload.Code.Should().Be("0123456789012");
    }

    [Fact]
    public void Parse_ShouldReadJsonQrPayload_UsingShortKeys()
    {
        var payload = BarcodePayloadParser.Parse("""{"p":"SKU-1","b":"B7","s":"SN-1","e":"2027-01-31","q":3}""");

        payload.Format.Should().Be(BarcodePayloadFormat.Json);
        payload.Code.Should().Be("SKU-1");
        payload.BatchNumber.Should().Be("B7");
        payload.SerialNumber.Should().Be("SN-1");
        payload.Quantity.Should().Be(3);
        payload.ExpiryDate!.Value.Date.Should().Be(new DateTime(2027, 1, 31));
    }

    [Fact]
    public void Parse_ShouldReadJsonQrPayload_UsingLongKeys()
    {
        var payload = BarcodePayloadParser.Parse("""{"sku":"SKU-2","batchNumber":"B8","quantity":"5"}""");

        payload.Format.Should().Be(BarcodePayloadFormat.Json);
        payload.Code.Should().Be("SKU-2");
        payload.BatchNumber.Should().Be("B8");
        payload.Quantity.Should().Be(5);
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("""{"unrelated":"value"}""")]
    public void Parse_ShouldFallBackToPlain_WhenJsonIsUnusable(string raw)
    {
        var payload = BarcodePayloadParser.Parse(raw);

        payload.Format.Should().Be(BarcodePayloadFormat.Plain);
        payload.Code.Should().Be(raw);
    }

    [Fact]
    public void Parse_ShouldNeverThrow_OnMalformedGs1()
    {
        // A truncated fixed-length element must degrade, not blow up mid-scan.
        var act = () => BarcodePayloadParser.Parse("(01)123");
        act.Should().NotThrow();
    }
}
