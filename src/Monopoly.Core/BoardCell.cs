namespace Monopoly.Core
{
    public enum CellType
    {
        Start, Jail, Rest, Casino, Supermarket, GasStation, Factory, Chance, TV, Food, Nightlife, Logistics, Bank, NetworkShop
    }

    public class BoardCell
    {
        public string Name { get; }
        public CellType Type { get; }
        // Меняется только администратором (§14) — у свободной компании.
        public int Price { get; internal set; }
        // Id владельца; null — компания у банка.
        public int? OwnerId { get; internal set; }
        // 0 — без филиалов, 1–4 — филиалы, 5 — головной офис.
        public int Level { get; internal set; }
        public bool IsMortgaged { get; internal set; }

        public BoardCell(string name, CellType type, int price = 0)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Type = type;
            Price = price;
        }

        public bool IsPurchasable => Type is CellType.Supermarket
            or CellType.GasStation
            or CellType.Factory
            or CellType.TV
            or CellType.Food
            or CellType.Nightlife
            or CellType.Logistics
            or CellType.Bank
            or CellType.NetworkShop;

        // Филиалы строятся только в группах по три; у АЗС и логистики своя аренда.
        public bool IsBuildable => IsPurchasable && Type is not (CellType.GasStation or CellType.Logistics);

        // Цена филиала и головного офиса — половина цены компании (§5).
        public int BranchCost => Price / 2;
        public int BranchSaleValue => BranchCost / 2;

        // Залог — половина цены, выкуп — залог + 10% (§10).
        public int MortgageValue => Price / 2;
        public int RedeemCost => MortgageValue + MortgageValue / 10;
    }
}
