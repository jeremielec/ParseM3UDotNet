using Microsoft.AspNetCore.Mvc;

public class TaskBag
{

    const int MaxConcurrent = 100;
    private readonly List<(Task task, string Detail)> tasks = new();

    public void Add(Task a, string detail)
    {
        tasks.Add((a, detail));
    }

    public bool ShouldAwait() => tasks.Count() > MaxConcurrent;

    public async Task DoAwait()
    {
        List<Exception> exceptions = new();
        foreach (var row in tasks)
        {
            try
            {
                await row.task;
            }
            catch (Exception a)
            {
                throw new Exception($"Task {row.Detail} failed", a);
            }
        }
        tasks.Clear();
    }
}