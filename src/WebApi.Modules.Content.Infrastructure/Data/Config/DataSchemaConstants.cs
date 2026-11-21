namespace soft.square.net.WebApi.Modules.Content.Infrastructure.Data.Config;

/// <summary>Column lengths shared by several mappings.</summary>
public static class DataSchemaConstants
{
    /// <summary>Slug ids: presses, categories, shifts.</summary>
    public const int SlugLength = 64;
    /// <summary>Display names of presses and categories.</summary>
    public const int NameLength = 200;
    /// <summary>User ids (the token's <c>sub</c>).</summary>
    public const int UserIdLength = 128;
}
