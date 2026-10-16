using BlazorBase.Notifications.Server.Services;
using WebPush;
using Xunit;

namespace BlazorBase.Notifications.Server.Test.Services;

public sealed class VapidKeyGeneratorTests
{
    [Fact]
    public void Generate_ReturnsNonEmptyPublicAndPrivateKeys()
    {
        var (publicKey, privateKey) = VapidKeyGenerator.Generate();

        Assert.False(string.IsNullOrWhiteSpace(publicKey));
        Assert.False(string.IsNullOrWhiteSpace(privateKey));
    }

    [Fact]
    public void Generate_ReturnsADifferentKeyPair_OnEachCall()
    {
        var first = VapidKeyGenerator.Generate();
        var second = VapidKeyGenerator.Generate();

        Assert.NotEqual(first.PublicKey, second.PublicKey);
        Assert.NotEqual(first.PrivateKey, second.PrivateKey);
    }

    [Fact]
    public void Generate_ReturnsAKeyPairOfTheSameShape_AsVapidHelperItselfProduces()
    {
        var (publicKey, privateKey) = VapidKeyGenerator.Generate();
        var keys = VapidHelper.GenerateVapidKeys();

        Assert.Equal(keys.PublicKey.Length, publicKey.Length);
        Assert.Equal(keys.PrivateKey.Length, privateKey.Length);
    }
}
