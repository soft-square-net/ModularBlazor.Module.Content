using soft.square.net.ModularBlazor.Abstractions;

// Default settings of this module: read by the WebApi's client-modules core module when the file is uploaded.
// Open to every signed-in user: what each one may do is decided by the Content API (content.* permissions in the
// user's tenant), and the pages show its answer. The API is tenant-bound, so sign in to a tenant to use it.
[assembly: ClientModule(Name = "Contents Management", Layout = "default", RoutePrefixes = new[] { "contents" },
    Dependencies = new[] { "WebApi.Modules.Content.Client" }, Styles = new[] { "contents.css" },
    NavTitle = "Contents Management", NavHref = "contents", NavOrder = 40)]
