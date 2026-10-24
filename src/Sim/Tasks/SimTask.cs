namespace ChineseGrocery.Sim.Tasks;

/// <summary>A unit of work on the shared task board. Staff claim tasks automatically; the player claims them by interacting.</summary>
public abstract record SimTask(int Id, TaskType Type, int Priority);
