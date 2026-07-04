namespace MiniDB.Core.Storage;

public sealed class PageIdGenerator
{
    private uint _next = 1;

    public uint Next() => _next++;
}