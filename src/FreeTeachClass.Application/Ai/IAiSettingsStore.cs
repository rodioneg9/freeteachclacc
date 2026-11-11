namespace FreeTeachClass.Application.Ai;

/// <summary>Local, non-secret AI preferences (provider, model, base URL).</summary>
public interface IAiSettingsStore
{
    Task<AiSettings> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(AiSettings settings, CancellationToken cancellationToken);
}
