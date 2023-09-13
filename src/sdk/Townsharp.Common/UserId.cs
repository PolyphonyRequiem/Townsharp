namespace Townsharp;
public readonly record struct UserId
{
    private readonly int value;

    public UserId(int value)
    {
        this.value = value;
    }

    public static implicit operator int(UserId id) => id.value;
    public static implicit operator UserId(int value) => new(value);

    public override string ToString()
    {
        return this.value.ToString();
    }
}
