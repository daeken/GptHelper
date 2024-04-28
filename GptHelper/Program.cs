using System.Text.Json;
using Newtonsoft.Json.Linq;

var builder = WebApplication.CreateBuilder(args);

var token = File.ReadAllText("token.txt").Trim();
var apiKey = File.ReadAllText("apikey.txt").Trim();

// Register HttpClient for Bing API
builder.Services.AddHttpClient("BingClient", client => {
	client.BaseAddress = new Uri("https://api.bing.microsoft.com/v7.0/search/");
	client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apiKey);
});

var app = builder.Build();

// Middleware for authorization
app.Use(async (context, next) => {
	var rtoken = context.Request.Query["token"];
	if(rtoken != token) {
		context.Response.StatusCode = StatusCodes.Status401Unauthorized;
		await context.Response.WriteAsync("Unauthorized: Invalid token");
		return;
	}
	await next();
});

// Search endpoint
app.MapGet("/search", async (HttpContext context, IHttpClientFactory clientFactory, string query) => {
	var client = clientFactory.CreateClient("BingClient");
	var response = await client.GetAsync($"?q={Uri.EscapeDataString(query)}&count=10");

	if(response.IsSuccessStatusCode) {
		var content = await response.Content.ReadAsStringAsync();
		dynamic json = JObject.Parse(content);
		var results = ((JArray) json.webPages.value).Select((Func<dynamic, dynamic>) (r => new {
			Title = (string) r.name,
			Description = (string) r.snippet,
			Url = (string) r.url
		})).ToList();

		await HttpResponseJsonExtensions.WriteAsJsonAsync(context.Response, results);
	} else {
		context.Response.StatusCode = (int) response.StatusCode;
		await context.Response.WriteAsync(response.ReasonPhrase);
	}
});

// Fetch endpoint
app.MapGet("/fetch", async (HttpContext context, IHttpClientFactory clientFactory, string uri) => {
	var client = clientFactory.CreateClient();
	var response = await client.GetAsync(uri);

	if(response.IsSuccessStatusCode) {
		var content = await response.Content.ReadAsStringAsync();
		context.Response.ContentType = "text/plain";
		await context.Response.WriteAsync(content);
	} else {
		context.Response.StatusCode = (int) response.StatusCode;
		await context.Response.WriteAsync(response.ReasonPhrase);
	}
});

app.Run();