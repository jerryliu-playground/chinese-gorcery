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

    /// <summary>The id the next task will get (saved so ids never repeat after loading).</summary>
    public int NextId => _nextId;

    /// <summary>Task id to worker id for every claimed task.</summary>
    public IReadOnlyDictionary<int, int> Claims => _claims;

    public SimTask? Find(int taskId) => _tasks.Find(t => t.Id == taskId);

    internal T Add<T>(Func<int, T> create) where T : SimTask
    {
        var task = create(_nextId++);
        _tasks.Add(task);
        return task;
    }

    internal void Restore(IEnumerable<SimTask> tasks, IEnumerable<KeyValuePair<int, int>> claims, int nextId)
    {
        _tasks.Clear();
        _tasks.AddRange(tasks);
        _claims.Clear();
        foreach (var (task, worker) in claims) _claims[task] = worker;
        _nextId = nextId;
    }

    internal bool TryClaim(int taskId, int workerId)
    {
        if (Find(taskId) is null || _claims.ContainsKey(taskId)) return false;
        _claims[taskId] = workerId;
        return true;
    }

    internal void Release(int taskId) => _claims.Remove(taskId);

    /// <summary>Gives every task of a type a new priority.</summary>
    internal void Reprioritize(TaskType type, int priority)
    {
        for (var i = 0; i < _tasks.Count; i++)
        {
            if (_tasks[i].Type == type) _tasks[i] = _tasks[i] with { Priority = priority };
        }
    }

    internal void Remove(int taskId)
    {
        _tasks.RemoveAll(t => t.Id == taskId);
        _claims.Remove(taskId);
    }

    /// <summary>Removes unclaimed tasks matching the predicate.</summary>
    internal void RemoveOpen(Func<SimTask, bool> predicate) =>
        _tasks.RemoveAll(t => !_claims.ContainsKey(t.Id) && predicate(t));
}
