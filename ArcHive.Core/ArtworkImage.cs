using System.Buffers.Binary;

namespace ArcHive.Core;

public static class ArtworkImage
{
    public static (int Width, int Height) Dimensions(ReadOnlySpan<byte> bytes)
    {
        int width = 0, height = 0;
        if (bytes.Length >= 33 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) && bytes.Slice(12, 4).SequenceEqual("IHDR"u8))
        { width = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(16, 4)); height = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(20, 4)); }
        else if (bytes.Length > 4 && bytes[0] == 255 && bytes[1] == 216 && bytes[^2] == 255 && bytes[^1] == 217)
        {
            var offset = 2;
            while (offset + 4 <= bytes.Length)
            {
                if (bytes[offset++] != 255) break;
                while (offset < bytes.Length && bytes[offset] == 255) offset++;
                if (offset >= bytes.Length) break;
                var marker = bytes[offset++];
                if (marker is 0xD8 or 0x01 || marker is >= 0xD0 and <= 0xD7) continue;
                if (marker is 0xDA or 0xD9 || offset + 2 > bytes.Length) break;
                var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset, 2));
                if (length < 2 || offset + length > bytes.Length) break;
                if (marker is 0xC0 or 0xC1 or 0xC2 && length >= 8)
                { height = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset + 3, 2)); width = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset + 5, 2)); break; }
                offset += length;
            }
        }
        if (width <= 0 || height <= 0 || width > 8192 || height > 8192 || (long)width * height > 32_000_000)
            throw new InvalidDataException("Artwork must be a supported PNG/JPEG within 8192 pixels per side and 32 megapixels.");
        return (width, height);
    }
}
