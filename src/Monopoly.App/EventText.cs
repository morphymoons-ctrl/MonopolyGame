using System.Collections.Generic;
using System.Linq;
using Monopoly.Core;

namespace Monopoly.App
{
    // Текст событий движка для журнала.
    public static class EventText
    {
        private static readonly IReadOnlyList<BoardCell> Cells = Board.CreateDefault();

        public static string Describe(GameEvent e, GameSnapshot snapshot)
        {
            string Name(int id) => snapshot.FindPlayer(id)?.Name ?? $"Игрок #{id}";
            string Cell(int index) => Cells[index].Name;

            return e switch
            {
                GameStarted g => $"Партия началась. Порядок ходов: {string.Join(", ", g.TurnOrder.Select(Name))}."
                    + (g.Seed is int seed ? $" Зерно: {seed}." : ""),
                TurnStarted t => $"Ходит {Name(t.PlayerId)}.",
                DiceRolled d => $"{Name(d.PlayerId)} бросил кубики: {d.Die1} + {d.Die2} = {d.Total}.",
                PlayerMoved m => $"{Name(m.PlayerId)} переходит на «{Cell(m.To)}».",
                PurchaseOffered p => $"«{Cell(p.CellIndex)}» свободна — можно купить за {p.Price} грн.",
                PropertyBought b => $"{Name(b.PlayerId)} купил «{Cell(b.CellIndex)}» за {b.Price} грн.",
                PurchaseDeclined d => $"{Name(d.PlayerId)} отказался покупать «{Cell(d.CellIndex)}».",
                RentPaid r => $"{Name(r.PayerId)} заплатил аренду {r.Amount} грн игроку {Name(r.OwnerId)}.",
                RestStarted r => $"{Name(r.PlayerId)} отдыхает и пропустит следующий ход.",
                TurnSkipped s => $"{Name(s.PlayerId)} пропускает ход (отдых).",
                _ => e.ToString(),
            };
        }
    }
}
