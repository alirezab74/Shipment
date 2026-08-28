using ShipmentTelemetry.Application.Abstractions;

namespace ShipmentTelemetry.Infrastructure.Security;

public sealed class Sha256PayloadHasher : IPayloadHasher
{
    public string ComputeHash(string payloadJson)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(payloadJson ?? string.Empty));

        return Convert.ToHexString(bytes);
    }
}
