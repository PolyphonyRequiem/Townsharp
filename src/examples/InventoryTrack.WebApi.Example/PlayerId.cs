namespace InventoryTrack.WebApi.Example;

public record struct PlayerId
{
    private readonly int value;

    public PlayerId(int value)
    {
        this.value = value;
    }

    public static implicit operator int(PlayerId id) => id.value;
    public static implicit operator long(PlayerId id) => (long) id.value;
    public static implicit operator PlayerId(int value) => new PlayerId(value);

    public override string ToString()
    {
        return this.value.ToString();
    }
}