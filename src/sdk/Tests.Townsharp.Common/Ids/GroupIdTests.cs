using Townsharp;

namespace Tests.Townsharp.Common.Ids;

public class GroupIdTests
{
    [Fact]
    public void VerifyImplicitConversion_FromInt()
    {
        GroupId idFromInt = 1;

        GroupId idFromCtor = new(1);

        Assert.Equal(idFromCtor, idFromInt);
    }
}
