namespace MultiplayerDemo.Core;

public readonly struct Vec2 {
	public readonly float X;
	public readonly float Y;

	public Vec2(float x, float y) {
		X = x;
		Y = y;
	}

	public static readonly Vec2 Zero = new(0f, 0f);

	public float LengthSquared => (X * X) + (Y * Y);
	public float Length => MathF.Sqrt(LengthSquared);

	public Vec2 Normalized {
		get {
			var length = Length;
			return length > 1e-6f ? new Vec2(X / length, Y / length) : Zero;
		}
	}

	public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
	public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
	public static Vec2 operator *(Vec2 a, float s) => new(a.X * s, a.Y * s);

	public override string ToString() => $"({X:0.###}, {Y:0.###})";
}
