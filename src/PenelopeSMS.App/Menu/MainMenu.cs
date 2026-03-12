namespace PenelopeSMS.App.Menu;

public sealed class MainMenu(
    ImportMenuAction importMenuAction,
    EnrichmentMenuAction enrichmentMenuAction)
{
    private readonly TextReader input = Console.In;
    private readonly TextWriter output = Console.Out;

    public Task RunAsync(CancellationToken cancellationToken = default)
        => RunInternalAsync(cancellationToken);

    private async Task RunInternalAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            output.WriteLine("PenelopeSMS");
            output.WriteLine("1. Import phone numbers");
            output.WriteLine("2. Enrich or retry phone numbers");
            output.WriteLine("0. Exit");
            output.Write("> ");

            var selection = input.ReadLine();

            switch (selection)
            {
                case "1":
                    await importMenuAction.ExecuteAsync(cancellationToken);
                    break;
                case "2":
                    await enrichmentMenuAction.ExecuteAsync(cancellationToken);
                    break;
                case "0":
                case null:
                    return;
                default:
                    output.WriteLine("Unknown selection.");
                    output.WriteLine();
                    break;
            }
        }
    }
}
