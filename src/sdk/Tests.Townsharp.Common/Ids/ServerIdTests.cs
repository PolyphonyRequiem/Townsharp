using Townsharp;

namespace Tests.Townsharp.Common.Ids;

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
