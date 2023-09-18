namespace Townsharp.Groups;

public readonly record struct GroupId
{
    private readonly int value;

    public GroupId(int value)
    {
        this.value = value;
    }

    public static implicit operator int(GroupId id) => id.value;

    public static implicit operator GroupId(int value) => new(value);

    public override string ToString()
    {
        return value.ToString();
    }
}