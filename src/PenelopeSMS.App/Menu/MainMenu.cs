namespace PenelopeSMS.App.Menu;

public sealed class MainMenu
{
    private readonly TextWriter output = Console.Out;

    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        output.WriteLine("PenelopeSMS");
        output.WriteLine("1. Import phone numbers (coming soon)");
        output.WriteLine("0. Exit");

        return Task.CompletedTask;
    }
}
