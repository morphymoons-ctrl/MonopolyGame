namespace Monopoly.Core
{
    // Действия игроков. PlayerId — кто действует; движок сам проверяет, его ли сейчас ход.
    // Простые записи, чтобы на этапе 2 их можно было передавать по сети.
    public abstract record GameAction(int PlayerId);

    public sealed record RollDice(int PlayerId) : GameAction(PlayerId);

    public sealed record BuyProperty(int PlayerId) : GameAction(PlayerId);

    public sealed record DeclinePurchase(int PlayerId) : GameAction(PlayerId);

    public sealed record EndTurn(int PlayerId) : GameAction(PlayerId);
}
