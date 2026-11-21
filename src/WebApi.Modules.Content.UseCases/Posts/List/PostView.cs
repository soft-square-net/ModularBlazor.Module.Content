using System.Text.Json.Serialization;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.List;

/// <summary>Which list: the caller's own, the queue (accept/assign), their reviews, or the Desk's (approved and scheduled).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PostView>))]
public enum PostView { Mine, Queue, Review, Desk }
