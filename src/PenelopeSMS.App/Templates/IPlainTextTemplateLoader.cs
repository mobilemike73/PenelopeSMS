namespace PenelopeSMS.App.Templates;

public interface IPlainTextTemplateLoader
{
    Task<PlainTextTemplateLoadResult> LoadAsync(
        string templatePath,
        CancellationToken cancellationToken = default);
}
