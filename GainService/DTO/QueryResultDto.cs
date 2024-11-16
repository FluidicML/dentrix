#pragma warning disable IDE1006 // Naming Styles

namespace FluidicML.Gain.DTO;

public sealed class QueryResultDto
{
    public required int id { get; set; }

    public required object? value { get; set; }

    public required int status { get; set; }
}
