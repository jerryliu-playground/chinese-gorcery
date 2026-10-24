namespace ChineseGrocery.Sim.Tasks;

/// <summary>The shared task queue. A task is claimed by at most one worker at a time.</summary>
public sealed class TaskBoard
{
    private readonly List<SimTask> _tasks = [];
    private readonly Dictionary<int, int> _claims = [];
    private int _nextId = 1;

    public IReadOnlyList<SimTask> Tasks => _tasks;

    /// <summary>Unclaimed tasks, highest priority first, then oldest.</summary>
    public IEnumerable<SimTask> OpenTasks =>
        _tasks.Where(t => !_claims.ContainsKey(t.Id)).OrderByDescending(t => t.Priority).ThenBy(t => t.Id);

    public int? ClaimedBy(int taskId) => _claims.TryGetValue(taskId, out var worker) ? worker : null;

    public SimTask? Find(int taskId) => _tasks.Find(t => t.Id == taskId);

    internal T Add<T>(Func<int, T> create) where T : SimTask
    {
        var task = create(_nextId++);
        _tasks.Add(task);
        return task;
    }

    internal bool TryClaim(int taskId, int workerId)
    {
        if (Find(taskId) is null || _claims.ContainsKey(taskId)) return false;
        _claims[taskId] = workerId;
        return true;
    }

    internal void Release(int taskId) => _claims.Remove(taskId);

    internal void Remove(int taskId)
    {
        _tasks.RemoveAll(t => t.Id == taskId);
        _claims.Remove(taskId);
    }

    /// <summary>Removes unclaimed tasks matching the predicate.</summary>
    internal void RemoveOpen(Func<SimTask, bool> predicate) =>
        _tasks.RemoveAll(t => !_claims.ContainsKey(t.Id) && predicate(t));
}
