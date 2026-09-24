using System.Security.Cryptography;

namespace TrainArena.Game;

/// <summary>
/// Generates 6-character uppercase alphanumeric room codes (Q4=A).
/// </summary>
public sealed class RoomCodeGenerator
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const int Length = 6;

    public string Next()
    {
        Span<char> chars = stackalloc char[Length];
        Span<byte> bytes = stackalloc byte[Length];
        RandomNumberGenerator.Fill(bytes);
        for (var i = 0; i < Length; i++)
        {
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        }

        return new string(chars);
    }
}
