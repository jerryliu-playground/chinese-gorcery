using ChineseGrocery.Sim.Inventory;
using ChineseGrocery.Sim.Tasks;

namespace ChineseGrocery.Sim.Tests.Tasks;

public class TaskBoardTests
{
    private static RestockTask Restock(int id, int priority) =>
        new(id, priority, StockLocation.Storage(3), new StockLocation(4, 0), "rice_jasmine");

    [Fact]
    public void A_task_can_only_be_claimed_by_one_worker()
    {
        var board = new TaskBoard();
        var task = board.Add(id => Restock(id, 10));

        Assert.True(board.TryClaim(task.Id, workerId: 1));
        Assert.False(board.TryClaim(task.Id, workerId: 2));
        Assert.Equal(1, board.ClaimedBy(task.Id));
    }

    [Fact]
    public void Released_task_can_be_claimed_again()
    {
        var board = new TaskBoard();
        var task = board.Add(id => Restock(id, 10));
        board.TryClaim(task.Id, 1);

        board.Release(task.Id);

        Assert.True(board.TryClaim(task.Id, 2));
    }

    [Fact]
    public void Open_tasks_come_highest_priority_first_then_oldest()
    {
        var board = new TaskBoard();
        var low = board.Add(id => Restock(id, 1));
        var highOld = board.Add(id => Restock(id, 9));
        var highNew = board.Add(id => Restock(id, 9));
        var claimed = board.Add(id => Restock(id, 99));
        board.TryClaim(claimed.Id, 1);

        Assert.Equal([highOld.Id, highNew.Id, low.Id], board.OpenTasks.Select(t => t.Id));
    }

    [Fact]
    public void Missing_task_cannot_be_claimed()
    {
        Assert.False(new TaskBoard().TryClaim(42, 1));
    }
}
