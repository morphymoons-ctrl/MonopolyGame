using Monopoly.Core;

namespace Monopoly.Tests
{
    public class BoardTests
    {
        private readonly List<BoardCell> board = Board.CreateDefault();

        [Fact]
        public void Board_HasFullPerimeterOfGrid()
        {
            Assert.Equal(Board.CellCount, board.Count);
            Assert.Equal((Board.SideLength - 1) * 4, board.Count);
        }

        [Fact]
        public void Board_StartsWithStart()
        {
            Assert.Equal(CellType.Start, board[0].Type);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(8)]
        [InlineData(16)]
        [InlineData(24)]
        public void Corners_AreNotPurchasable(int index)
        {
            Assert.False(board[index].IsPurchasable);
        }

        [Fact]
        public void OnlyPurchasableCells_HavePrice()
        {
            foreach (var cell in board)
            {
                Assert.Equal(cell.IsPurchasable, cell.Price > 0);
            }
        }

        [Fact]
        public void CellNames_AreUnique()
        {
            Assert.Equal(board.Count, board.Select(c => c.Name).Distinct().Count());
        }

        [Fact]
        public void NewBoard_HasNoOwners()
        {
            Assert.All(board, cell => Assert.Null(cell.OwnerId));
        }
    }
}
