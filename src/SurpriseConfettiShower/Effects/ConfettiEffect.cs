using System.Windows;
using System.Windows.Media;

namespace SurpriseConfettiShower;

/// <summary>A shower of paper rectangles falling from the top edge, tumbling and fluttering.</summary>
public sealed class ConfettiEffect : EffectCanvas
{
    private const int PieceCount = 200;
    private const double ReleaseWindow = 1.5;   // seconds over which the pieces start falling

    private static readonly SolidColorBrush[] Palette =
    [
        Frozen(0xFF4D6D), Frozen(0xFFB703), Frozen(0x06D6A0),
        Frozen(0x4CC9F0), Frozen(0xB388FF), Frozen(0xFF8FA3),
    ];

    private readonly Piece[] _pieces = new Piece[PieceCount];

    // One transform per piece, reused every frame: allocating 200 of them per frame per monitor
    // was the only per-frame allocation left in the app.
    private readonly RotateTransform[] _spin = new RotateTransform[PieceCount];

    private int _falling;

    protected override TimeSpan MaxDuration => TimeSpan.FromSeconds(10);

    protected override bool IsFinished => _falling == 0;

    protected override void Update(double dt, Size size)
    {
        // Speeds are a fraction of the screen height, so a tall and a short monitor take the
        // same time to empty and the two overlays close together.
        double gravity = size.Height * 0.6;

        for (int i = 0; i < _pieces.Length; i++)
        {
            ref var piece = ref _pieces[i];
            if (piece.Gone)
            {
                continue;
            }

            if (piece.Delay > 0)
            {
                piece.Delay -= dt;
                continue;
            }

            piece.Speed = Math.Min(piece.Speed + (gravity * dt), piece.TerminalSpeed * size.Height);
            piece.DriftPhase += piece.DriftRate * dt;
            piece.FlutterPhase += piece.FlutterRate * dt;
            piece.Angle += piece.Spin * dt;
            piece.X += piece.Drift * Math.Sin(piece.DriftPhase) * dt;
            piece.Y += piece.Speed * dt;

            if (piece.Y - piece.Height > size.Height)
            {
                piece.Gone = true;
                _falling--;
            }
        }
    }

    protected override void Draw(DrawingContext dc, Size size)
    {
        for (int i = 0; i < _pieces.Length; i++)
        {
            ref var piece = ref _pieces[i];
            if (piece.Gone || piece.Delay > 0)
            {
                continue;
            }

            // Squeezing the width fakes a piece of paper turning edge-on as it spins.
            double width = piece.Width * Math.Abs(Math.Cos(piece.FlutterPhase));

            var spin = _spin[i];
            spin.Angle = piece.Angle;
            spin.CenterX = piece.X;
            spin.CenterY = piece.Y;

            dc.PushTransform(spin);
            dc.DrawRectangle(
                Palette[piece.Colour],
                null,
                new Rect(piece.X - (width / 2), piece.Y - (piece.Height / 2), width, piece.Height));
            dc.Pop();
        }
    }

    protected override void Start(Size size)
    {
        _falling = _pieces.Length;

        for (int i = 0; i < _pieces.Length; i++)
        {
            _pieces[i] = new Piece
            {
                X = Rand(0, size.Width),
                Y = -Rand(20, 60),
                Width = Rand(8, 13),
                Height = Rand(12, 20),
                Delay = Rand(0, ReleaseWindow),
                Speed = size.Height * Rand(0, 0.05),
                TerminalSpeed = Rand(0.22, 0.34),
                Drift = Rand(20, 70),
                DriftRate = Rand(1.2, 2.6),
                DriftPhase = Rand(0, Math.Tau),
                FlutterRate = Rand(3, 7.5),
                FlutterPhase = Rand(0, Math.Tau),
                Angle = Rand(0, 360),
                Spin = Rand(-150, 150),
                Colour = Random.Shared.Next(Palette.Length),
            };

            _spin[i] = new RotateTransform();
        }
    }

    private struct Piece
    {
        public double X;                // centre of the piece
        public double Y;
        public double Width;
        public double Height;
        public double Delay;            // seconds left before it is released
        public double Speed;            // current fall speed, px/s
        public double TerminalSpeed;    // capped at this fraction of the screen height per second
        public double Drift;            // amplitude of the sideways sway, px/s
        public double DriftRate;
        public double DriftPhase;
        public double FlutterRate;
        public double FlutterPhase;
        public double Angle;            // degrees
        public double Spin;             // degrees/s
        public int Colour;
        public bool Gone;               // fallen past the bottom edge
    }
}
