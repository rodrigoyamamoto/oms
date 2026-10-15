using Oms.Orders.Domain.Common;
using Xunit;

namespace Oms.Orders.UnitTests.Domain;

public sealed class ResultTests
{
    [Fact]
    public void SuccessResultShouldBeSuccessfulAndContainNoneError()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
        Assert.True(result.Error.IsNone);
    }

    [Fact]
    public void FailureResultShouldBeFailedAndPreserveProvidedError()
    {
        var error = new Error("Test.Failure", "A domain validation failure occurred.");

        var result = Result.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
        Assert.Equal("Test.Failure", result.Error.Code);
        Assert.Equal("A domain validation failure occurred.", result.Error.Message);
    }

    [Fact]
    public void GenericSuccessResultShouldExposeValueAndNoneError()
    {
        var result = Result.Success("expected-value");

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal("expected-value", result.Value);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void GenericFailureResultShouldPreserveErrorAndThrowWhenAccessingValue()
    {
        var error = new Error("Test.InvalidState", "Operation is not valid in the current state.");

        var result = Result.Failure<string>(error);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void PreventionOfContradictoryResultStatesShouldRejectNoneErrorOnFailureAndNullValueOnSuccess()
    {
        Assert.Throws<ArgumentException>(() => Result.Failure(Error.None));
        Assert.Throws<ArgumentException>(() => Result.Failure(default));
        Assert.Throws<ArgumentException>(() => Result.Failure<string>(Error.None));
        Assert.Throws<ArgumentNullException>(() => Result.Success<string>(null!));
    }

    [Theory]
    [InlineData("", "Valid message")]
    [InlineData("   ", "Valid message")]
    [InlineData("Valid.Code", "")]
    [InlineData("Valid.Code", "   ")]
    public void ErrorConstructorWhenCodeOrMessageIsBlankShouldThrowArgumentException(string code, string message)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Error(code, message));
    }
}
