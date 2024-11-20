#pragma warning disable IDE1006 // Naming Styles

namespace FluidicML.Gain.DTO;

public sealed class JwtQueryDto
{
    public required string query { get; set; }

    public required string uuid { get; set; }

    public required int nonce { get; set; }
}
