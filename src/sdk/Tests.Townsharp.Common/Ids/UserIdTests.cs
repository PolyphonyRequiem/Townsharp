using Townsharp;

namespace Tests.Townsharp.Common.Ids;
public class UserIdTests
{
    [Fact]
    public void VerifyImplicitConversion_FromInt()
    {
        UserId idFromInt = 1;

        UserId idFromCtor = new(1);

        Assert.Equal(idFromCtor, idFromInt);
    }
}
