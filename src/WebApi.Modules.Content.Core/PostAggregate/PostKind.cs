using System.Text.Json.Serialization;

namespace soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

[JsonConverter(typeof(JsonStringEnumConverter<PostKind>))]
public enum PostKind { Article, News, Video, Reportage, PhotoStory, Other }
