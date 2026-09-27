using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Monopoly.App
{
    // Кубик с точками. Анимация броска перебирает грани по кругу — это только картинка,
    // выпавшее значение всегда приходит от хоста.
    public class DiceView : Border
    {
        private readonly Canvas pips = new();
        private readonly double size;

        public DiceView(double size)
        {
            this.size = size;
            Width = size;
            Height = size;
            CornerRadius = new CornerRadius(size * 0.18);
            Background = Brushes.White;
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xD5, 0xD9, 0xE2));
            BorderThickness = new Thickness(2);
            Effect = new DropShadowEffect { BlurRadius = 12, ShadowDepth = 3, Opacity = 0.3 };
            Child = pips;
            Show(0);
        }

        // 0 — пустой кубик (ещё не бросали).
        public void Show(int value)
        {
            pips.Children.Clear();
            double c = size / 2 - 2, o = size * 0.26, r = size * 0.1;
            var spots = value switch
            {
                1 => new[] { (0.0, 0.0) },
                2 => new[] { (-1.0, -1.0), (1.0, 1.0) },
                3 => new[] { (-1.0, -1.0), (0.0, 0.0), (1.0, 1.0) },
                4 => new[] { (-1.0, -1.0), (1.0, -1.0), (-1.0, 1.0), (1.0, 1.0) },
                5 => new[] { (-1.0, -1.0), (1.0, -1.0), (0.0, 0.0), (-1.0, 1.0), (1.0, 1.0) },
                6 => new[] { (-1.0, -1.0), (1.0, -1.0), (-1.0, 0.0), (1.0, 0.0), (-1.0, 1.0), (1.0, 1.0) },
                _ => new (double, double)[0],
            };
            foreach (var (dx, dy) in spots)
            {
                var dot = new Ellipse { Width = r * 2, Height = r * 2, Fill = new SolidColorBrush(Color.FromRgb(0x1E, 0x25, 0x33)) };
                Canvas.SetLeft(dot, c + dx * o - r);
                Canvas.SetTop(dot, c + dy * o - r);
                pips.Children.Add(dot);
            }
        }

        public async Task RollAsync(int final, int phase)
        {
            // Грани меняются всё медленнее — кубик «докатывается»: около секунды на бросок.
            for (int frame = 0; frame < 9; frame++)
            {
                Show((frame * 5 + phase) % 6 + 1);
                await Task.Delay(60 + frame * 11);
            }
            Show(final);
        }
    }
}
