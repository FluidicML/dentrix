#pragma warning disable IDE1006 // Naming Styles

namespace FluidicML.Gain.DTO;

public sealed class JwtQueryResultDto
{
    public required string uuid {  get; set; }

    public required object? value { get; set; }

    public required int status { get; set; }
}
