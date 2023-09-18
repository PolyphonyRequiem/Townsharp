namespace Townsharp.Servers;

public readonly record struct ServerId
{
    private readonly int value;

    public ServerId(int value)
    {
        this.value = value;
    }

    public static implicit operator int(ServerId id) => id.value;
    public static implicit operator ServerId(int value) => new(value);

    public override string ToString()
    {
        return value.ToString();
    }
}
