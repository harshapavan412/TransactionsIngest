using TransactionsIngest.Api.Import;
using TransactionsIngest.Api.Sources;
using Xunit;

namespace TransactionsIngest.Tests;

public class SourceTransactionValidatorTests
{
    private static SourceTransaction Valid() => ImportTestHarness.Tx("Online", 1);

    [Fact]
    public void ValidRecord_IsAccepted() =>
        Assert.Null(SourceTransactionValidator.Validate(Valid(), "Online"));

    [Fact]
    public void EmptyCardNumber_IsAccepted() =>
        Assert.Null(SourceTransactionValidator.Validate(Valid() with { CardNumber = "" }, "Online"));

    [Fact]
    public void ProductNameOver20Characters_IsRejected() =>
        Assert.NotNull(SourceTransactionValidator.Validate(Valid() with { ProductName = "USB-C Docking Station" }, "Online"));

    [Fact]
    public void MismatchedSourceSystem_IsRejected() =>
        Assert.NotNull(SourceTransactionValidator.Validate(Valid(), "Store Uptown"));

    [Fact]
    public void AmountWithMoreThanTwoDecimals_IsRejected() =>
        Assert.NotNull(SourceTransactionValidator.Validate(Valid() with { Amount = 1.999m }, "Online"));
}