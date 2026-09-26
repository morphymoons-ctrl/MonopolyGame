namespace MonopolyGame
{
    public enum CellType
    {
        Start, Jail, Rest, Casino, Supermarket, GasStation, Factory, Chance, TV, Food, Back, OnlineShop, Logistics, Bank, NetworkShop
    }

    public class BoardCell
    {
        public string Name { get; }
        public CellType Type { get; }
        public int Price { get; }
        public int OwnerId { get; set; } = -1;

        public BoardCell(string name, CellType type, int price = 0)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Type = type;
            Price = price;
        }
    }
}
