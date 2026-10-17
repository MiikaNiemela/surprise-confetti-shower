using System.Windows;
using System.Windows.Media;

namespace SurpriseConfettiShower;

/// <summary>Shells that rise from the bottom edge trailing sparks, then burst into fading stars.</summary>
public sealed class FireworksEffect : EffectCanvas
{
    private const int BurstSparks = 120;        // hard cap per burst
    private const int TrailSparks = 40;         // reused round-robin while a shell rises
    private const int SparksPerShell = BurstSparks + TrailSparks;
    private const double LaunchWindow = 4.0;    // every shell is on its way by now
    private const double TrailInterval = 0.03;
    private const double TwinkleRate = 17.0;
    private const double Drag = 1.3;

    // The last entry is the near-white used for trails and the occasional sparkle in a burst.
    private static readonly SolidColorBrush[] Palette =
    [
        Frozen(0xFFD166), Frozen(0xFF5D8F), Frozen(0x4CC9F0),
        Frozen(0x9D4EDD), Frozen(0x80FFDB), Frozen(0xFF9E00),
        Frozen(0xFFF3C4),
    ];

    private static readonly int Sparkle = Palette.Length - 1;

    private readonly Shell[] _shells = new Shell[Random.Shared.Next(6, 11)];
    private readonly Spark[] _sparks;
    private int _bursts;
    private int _liveSparks;

    public FireworksEffect() => _sparks = new Spark[_shells.Length * SparksPerShell];

    protected override TimeSpan MaxDuration => TimeSpan.FromSeconds(12);

    protected override bool IsFinished => _bursts == _shells.Length && _liveSparks == 0;

    protected override void Update(double dt, Size size)
    {
        // Scaled to the screen height so every monitor runs at the same pace.
        double gravity = size.Height * 0.7;

        for (int i = 0; i < _shells.Length; i++)
        {
            ref var shell = ref _shells[i];
            if (shell.HasBurst)
            {
                continue;
            }

            if (shell.Delay > 0)
            {
                shell.Delay -= dt;
                if (shell.Delay > 0)
                {
                    continue;
                }

                Launch(ref shell, gravity, size);
            }

            shell.Vy += gravity * dt;
            shell.X += shell.Vx * dt;
            shell.Y += shell.Vy * dt;

            shell.TrailTimer -= dt;
            if (shell.TrailTimer <= 0)
            {
                shell.TrailTimer = TrailInterval;
                AddTrail(ref shell, i);
            }

            if (shell.Vy >= 0 || shell.Y <= shell.TargetY)
            {
                Burst(ref shell, i, size);
            }
        }

        UpdateSparks(dt, gravity * 0.35);
    }

    protected override void Draw(DrawingContext dc, Size size)
    {
        for (int i = 0; i < _sparks.Length; i++)
        {
            ref var spark = ref _sparks[i];
            if (!spark.Alive)
            {
                continue;
            }

            double left = spark.Life / spark.MaxLife;
            double flicker = 1 - spark.Twinkle + (spark.Twinkle * (0.5 + (0.5 * Math.Sin(spark.TwinklePhase))));
            double radius = spark.Radius * (0.45 + (0.55 * left));

            dc.PushOpacity(left * flicker);
            dc.DrawEllipse(Palette[spark.Colour], null, new Point(spark.X, spark.Y), radius, radius);
            dc.Pop();
        }

        for (int i = 0; i < _shells.Length; i++)
        {
            ref var shell = ref _shells[i];
            if (shell.HasBurst || shell.Delay > 0)
            {
                continue;
            }

            var centre = new Point(shell.X, shell.Y);

            // A dim halo in the shell colour hints at what is about to happen.
            dc.PushOpacity(0.35);
            dc.DrawEllipse(Palette[shell.Colour], null, centre, 8, 8);
            dc.Pop();
            dc.DrawEllipse(Palette[Sparkle], null, centre, 3, 3);
        }
    }

    protected override void Start(Size size)
    {
        for (int i = 0; i < _shells.Length; i++)
        {
            _shells[i] = new Shell
            {
                X = Rand(0.12, 0.88) * size.Width,
                TargetY = Rand(0.12, 0.42) * size.Height,
                Delay = (i + Rand(0.1, 0.9)) / _shells.Length * LaunchWindow,
                Colour = Random.Shared.Next(Palette.Length - 1),
            };
        }
    }

    private static void Launch(ref Shell shell, double gravity, Size size)
    {
        shell.Y = size.Height + 8;
        // Aim the apex at the burst height, so the shell slows to a stop just as it opens.
        shell.Vy = -Math.Sqrt(2 * gravity * (shell.Y - shell.TargetY));
        shell.Vx = Rand(-0.03, 0.03) * size.Width;
    }

    private void Burst(ref Shell shell, int index, Size size)
    {
        shell.HasBurst = true;
        _bursts++;

        int count = Random.Shared.Next(70, BurstSparks + 1);
        double power = size.Height * Rand(0.2, 0.28);
        int slot = index * SparksPerShell;

        for (int i = 0; i < count; i++)
        {
            // An evenly spread but jittered ring, with a few slower sparks filling the middle.
            double angle = (i + Rand(0, 1)) / count * Math.Tau;
            double speed = power * (0.35 + (0.65 * Math.Sqrt(Random.Shared.NextDouble())));

            Spawn(slot + i, new Spark
            {
                X = shell.X,
                Y = shell.Y,
                Vx = Math.Cos(angle) * speed,
                Vy = Math.Sin(angle) * speed,
                Life = Rand(1.3, 2.4),
                Radius = Rand(2.6, 4.6),
                Colour = Random.Shared.NextDouble() < 0.15 ? Sparkle : shell.Colour,
                Twinkle = Rand(0, 0.5),
                TwinklePhase = Rand(0, Math.Tau),
            });
        }
    }

    private void AddTrail(ref Shell shell, int index)
    {
        int slot = (index * SparksPerShell) + BurstSparks + (shell.TrailCursor % TrailSparks);
        shell.TrailCursor++;

        Spawn(slot, new Spark
        {
            X = shell.X + Rand(-2, 2),
            Y = shell.Y + Rand(-2, 2),
            Vx = Rand(-20, 20),
            Vy = Rand(-10, 40),
            Life = Rand(0.2, 0.45),
            Radius = Rand(1.6, 3.0),
            Colour = Sparkle,
            Twinkle = Rand(0.2, 0.6),
            TwinklePhase = Rand(0, Math.Tau),
        });
    }

    /// <summary>Writes a spark into its fixed pool slot, overwriting whatever was there.</summary>
    private void Spawn(int slot, Spark spark)
    {
        ref var target = ref _sparks[slot];
        if (!target.Alive)
        {
            _liveSparks++;
        }

        spark.MaxLife = spark.Life;
        spark.Alive = true;
        target = spark;
    }

    private void UpdateSparks(double dt, double gravity)
    {
        double drag = Math.Max(0, 1 - (Drag * dt));

        for (int i = 0; i < _sparks.Length; i++)
        {
            ref var spark = ref _sparks[i];
            if (!spark.Alive)
            {
                continue;
            }

            spark.Life -= dt;
            if (spark.Life <= 0)
            {
                spark.Alive = false;
                _liveSparks--;
                continue;
            }

            spark.Vy += gravity * dt;
            spark.Vx *= drag;
            spark.Vy *= drag;
            spark.X += spark.Vx * dt;
            spark.Y += spark.Vy * dt;
            spark.TwinklePhase += TwinkleRate * dt;
        }
    }

    private struct Shell
    {
        public double X;
        public double Y;
        public double Vx;
        public double Vy;
        public double TargetY;      // burst height
        public double Delay;        // seconds left before launch
        public double TrailTimer;
        public int TrailCursor;
        public int Colour;
        public bool HasBurst;
    }

    private struct Spark
    {
        public double X;
        public double Y;
        public double Vx;
        public double Vy;
        public double Life;         // seconds left
        public double MaxLife;
        public double Radius;
        public double Twinkle;      // 0 = steady, 1 = flickers down to half brightness
        public double TwinklePhase;
        public int Colour;
        public bool Alive;
    }
}
