using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace DeskPet.Engine;

public class ParticleSystem
{
    private readonly Canvas _canvas;
    private readonly DispatcherTimer _timer;
    private readonly List<Particle> _particles = new();
    private readonly Random _rng = new();

    public ParticleSystem(Canvas canvas)
    {
        _canvas = canvas;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTick;
    }

    public void EmitHearts(double x, double y, int count = 4)
    {
        for (var i = 0; i < count; i++)
        {
            var p = new Particle
            {
                Visual = MakeText("\u2764", System.Windows.Media.Brushes.HotPink),
                X = x + (_rng.NextDouble() - 0.5) * 20,
                Y = y,
                Vx = (_rng.NextDouble() - 0.5) * 1.5,
                Vy = -(1.0 + _rng.NextDouble() * 1.5),
                Life = 0,
                MaxLife = 800 + _rng.Next(400),
            };
            _particles.Add(p);
            Canvas.SetLeft(p.Visual, p.X);
            Canvas.SetTop(p.Visual, p.Y);
            _canvas.Children.Add(p.Visual);
        }
        if (!_timer.IsEnabled) _timer.Start();
    }

    public void EmitQuestion(double x, double y)
    {
        var p = new Particle
        {
            Visual = MakeText("?", System.Windows.Media.Brushes.Goldenrod),
            X = x + (_rng.NextDouble() - 0.5) * 10,
            Y = y,
            Vx = (_rng.NextDouble() - 0.5) * 0.8,
            Vy = -(0.8 + _rng.NextDouble() * 0.6),
            Life = 0,
            MaxLife = 700,
            StartFontSize = 18,
            EndFontSize = 12,
        };
        _particles.Add(p);
        Canvas.SetLeft(p.Visual, p.X);
        Canvas.SetTop(p.Visual, p.Y);
        _canvas.Children.Add(p.Visual);
        if (!_timer.IsEnabled) _timer.Start();
    }

    private static TextBlock MakeText(string text, System.Windows.Media.Brush color)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = color,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            IsHitTestVisible = false,
        };
    }

    private void OnTick(object? sender, EventArgs e)
    {
        for (var i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Life += 16;
            p.X += p.Vx;
            p.Y += p.Vy;
            var t = (double)p.Life / p.MaxLife;
            if (t >= 1.0)
            {
                _canvas.Children.Remove(p.Visual);
                _particles.RemoveAt(i);
                continue;
            }
            Canvas.SetLeft(p.Visual, p.X);
            Canvas.SetTop(p.Visual, p.Y);
            p.Visual.Opacity = 1.0 - t;
        }
        if (_particles.Count == 0) _timer.Stop();
    }

    private class Particle
    {
        public TextBlock Visual = null!;
        public double X, Y, Vx, Vy;
        public int Life, MaxLife;
        public double StartFontSize = 14;
        public double EndFontSize = 14;
    }
}
