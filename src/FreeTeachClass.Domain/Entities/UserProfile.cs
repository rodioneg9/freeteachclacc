namespace FreeTeachClass.Domain.Entities;

/// <summary>
/// Minimal profile of the signed-in Google account. Never stores credentials.
/// </summary>
public sealed record UserProfile(
    string Id,
    string Name,
    string? Email,
    string? PictureUrl);
