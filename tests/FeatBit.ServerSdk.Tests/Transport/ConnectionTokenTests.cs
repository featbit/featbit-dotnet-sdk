namespace FeatBit.Sdk.Server.Transport;

public class ConnectionTokenTests
{
    [Fact]
    public void NewConnectionToken()
    {
        var token = ConnectionToken.New("qJHQTVfsZUOu1Q54RLMuIQ-JtrIvNK-k-bARYicOTNQA");

        Assert.False(string.IsNullOrWhiteSpace(token));
    }
}