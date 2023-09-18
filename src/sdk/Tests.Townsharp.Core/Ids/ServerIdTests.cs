using Townsharp.Servers;

namespace Tests.Townsharp.Ids;

public class ServerIdTests
{
    [Fact]
    public void VerifyImplicitConversion_FromInt()
    {
        ServerId idFromInt = 1;

        ServerId idFromCtor = new(1);

        Assert.Equal(idFromCtor, idFromInt);
    }
}
