#pragma warning disable IDE1006 // Naming Styles

namespace FluidicML.Gain.DTO;

public sealed class QueryDto
{
    public required int id { get; set; }

    public required string query { get; set; }
}
