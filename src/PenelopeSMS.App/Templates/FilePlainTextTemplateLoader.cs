namespace PenelopeSMS.App.Templates;

public sealed class FilePlainTextTemplateLoader : IPlainTextTemplateLoader
{
    public async Task<PlainTextTemplateLoadResult> LoadAsync(
        string templatePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templatePath);
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(templatePath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Template file was not found: {fullPath}",
                fullPath);
        }

        var templateBody = await File.ReadAllTextAsync(fullPath, cancellationToken);

        if (string.IsNullOrWhiteSpace(templateBody))
        {
            throw new InvalidOperationException("Template file must contain plain-text message content.");
        }

        return new PlainTextTemplateLoadResult(fullPath, templateBody);
    }
}
