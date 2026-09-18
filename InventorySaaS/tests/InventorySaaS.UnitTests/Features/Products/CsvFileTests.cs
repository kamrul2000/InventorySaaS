using FluentAssertions;
using InventorySaaS.Application.Common.Csv;

namespace InventorySaaS.UnitTests.Features.Products;

/// <summary>
/// The CSV reader is hand-rolled, so the RFC 4180 cases a spreadsheet actually emits are
/// pinned here: quoted separators, doubled quotes, embedded newlines, CRLF, and Excel's BOM.
/// </summary>
public class CsvFileTests
{
    [Fact]
    public void Parse_ShouldSplitPlainRows()
    {
        var rows = CsvFile.Parse("Name,Sku\r\nMouse,ELEC-1\r\nKeyboard,ELEC-2\r\n");

        rows.Should().HaveCount(3);
        rows[0].Should().Equal("Name", "Sku");
        rows[2].Should().Equal("Keyboard", "ELEC-2");
    }

    [Fact]
    public void Parse_ShouldKeepCommasInsideQuotedFields()
    {
        var rows = CsvFile.Parse("Name,Description\r\n\"Mouse, wireless\",\"Black, 2.4GHz\"\r\n");

        rows[1].Should().Equal("Mouse, wireless", "Black, 2.4GHz");
    }

    [Fact]
    public void Parse_ShouldUnescapeDoubledQuotes()
    {
        var rows = CsvFile.Parse("Name\r\n\"24\"\" Monitor\"\r\n");

        rows[1][0].Should().Be("24\" Monitor");
    }

    [Fact]
    public void Parse_ShouldKeepNewlinesInsideQuotedFields()
    {
        var rows = CsvFile.Parse("Name,Description\r\nMouse,\"Line one\nLine two\"\r\n");

        rows.Should().HaveCount(2);
        rows[1][1].Should().Be("Line one\nLine two");
    }

    [Fact]
    public void Parse_ShouldStripTheExcelByteOrderMark()
    {
        var rows = CsvFile.Parse("﻿Name,Sku\r\nMouse,ELEC-1\r\n");

        rows[0][0].Should().Be("Name");
    }

    [Fact]
    public void Parse_ShouldHandleAMissingTrailingNewline()
    {
        var rows = CsvFile.Parse("Name,Sku\r\nMouse,ELEC-1");

        rows.Should().HaveCount(2);
        rows[1].Should().Equal("Mouse", "ELEC-1");
    }

    [Fact]
    public void Parse_ShouldDropBlankLines()
    {
        var rows = CsvFile.Parse("Name,Sku\r\n\r\nMouse,ELEC-1\r\n\r\n");

        rows.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_ShouldPreserveEmptyFields()
    {
        var rows = CsvFile.Parse("Name,Sku,Barcode\r\nMouse,,123\r\n");

        rows[1].Should().Equal("Mouse", "", "123");
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("has,comma", "\"has,comma\"")]
    [InlineData("has\"quote", "\"has\"\"quote\"")]
    [InlineData("has\nnewline", "\"has\nnewline\"")]
    public void EscapeField_ShouldQuoteOnlyWhenNeeded(string input, string expected)
    {
        CsvFile.EscapeField(input).Should().Be(expected);
    }

    [Fact]
    public void Build_ShouldRoundTripThroughParse()
    {
        var bytes = CsvFile.Build(
            ["Name", "Description"],
            [["Mouse, wireless", "24\" reach"]]);

        var rows = CsvFile.Parse(System.Text.Encoding.UTF8.GetString(bytes));

        rows[0].Should().Equal("Name", "Description");
        rows[1].Should().Equal("Mouse, wireless", "24\" reach");
    }
}
