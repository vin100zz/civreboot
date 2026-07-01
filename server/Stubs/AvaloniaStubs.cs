// Minimal Avalonia type stubs — allows OpenCivOne graphics code to compile without Avalonia
namespace Avalonia.Media
{
    public readonly struct Color
    {
        public byte A { get; }
        public byte R { get; }
        public byte G { get; }
        public byte B { get; }

        // Public constructor used in GBitmapPalette: new Color(a, r, g, b)
        public Color(byte a, byte r, byte g, byte b) { A = a; R = r; G = g; B = b; }

        public static Color FromUInt32(uint value) =>
            new Color((byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value);

        public static Color FromArgb(byte a, byte r, byte g, byte b) => new Color(a, r, g, b);
        public static Color FromRgb(byte r, byte g, byte b) => new Color(255, r, g, b);

        public static bool operator ==(Color left, Color right) =>
            left.A == right.A && left.R == right.R && left.G == right.G && left.B == right.B;
        public static bool operator !=(Color left, Color right) => !(left == right);
        public override bool Equals(object? obj) => obj is Color c && this == c;
        public override int GetHashCode() => HashCode.Combine(A, R, G, B);
    }
}

// Stub for Avalonia.Controls.Shapes (stray import in CRC32.cs)
namespace Avalonia.Controls.Shapes { }
