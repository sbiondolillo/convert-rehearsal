using Core;
using Xunit;

namespace Specs.Unit;

public sealed class RateClientTests
{
    [Fact]
    public void BuildsTheAddressFromTheKeyAndTheCodes() =>
        Assert.Equal("https://v6.exchangerate-api.com/v6/test-key/pair/USD/EUR", RateClient.Address("test-key", "USD", "EUR").ToString());

    [Fact]
    public void EscapesACodeThatHoldsASlash() =>
        Assert.DoesNotContain("/pair/USD/a/b", RateClient.Address("k", "USD", "a/b").ToString(), StringComparison.Ordinal);

    [Fact]
    public void ParsesASuccessAnswerWithTheRateAsADecimal()
    {
        RateAnswer answer = RateClient.Parse("""{"result":"success","base_code":"USD","target_code":"EUR","conversion_rate":157.8014}""");
        Assert.Equal(157.8014m, answer.Rate);
        Assert.Null(answer.ErrorType);
        Assert.False(answer.NoAnswer);
    }

    [Fact]
    public void ParsesAnErrorAnswerWithHyphenatedFields()
    {
        RateAnswer answer = RateClient.Parse("""{"result":"error","documentation":"d","terms-of-use":"t","error-type":"invalid-key"}""");
        Assert.Equal("invalid-key", answer.ErrorType);
        Assert.Null(answer.Rate);
    }

    [Fact]
    public void NamesAnErrorWithoutATypeUnknown() =>
        Assert.Equal("unknown", RateClient.Parse("""{"result":"error"}""").ErrorType);

    [Theory]
    [InlineData("<html>")]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("""{"result":"success"}""")]
    [InlineData("""{"result":"other"}""")]
    public void TakesAnAnswerThatIsNotUsableAsNoAnswer(string body) =>
        Assert.True(RateClient.Parse(body).NoAnswer);
}
