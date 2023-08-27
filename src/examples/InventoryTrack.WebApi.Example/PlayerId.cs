namespace InventoryTrack.WebApi.Example;

public record struct PlayerId
{
    private readonly ulong value;

    public PlayerId(ulong value)
    {
        this.value = value;
    }

    public static implicit operator ulong(PlayerId id) => id.value;
    public static implicit operator long(PlayerId id) => (long) id.value;
    public static implicit operator PlayerId(ulong value) => new PlayerId(value);

    public override string ToString()
    {
        return this.value.ToString();
    }
}