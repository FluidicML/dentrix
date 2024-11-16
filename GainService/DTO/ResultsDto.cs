#pragma warning disable IDE1006 // Naming Styles

namespace FluidicML.Gain.DTO;

public sealed class ResultsDto
{
    public required int id { get; set; }

    public required object[] results { get; set; }
}
