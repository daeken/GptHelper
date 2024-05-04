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

		await context.Response.WriteAsJsonAsync(results);
	} else {
		context.Response.StatusCode = (int) response.StatusCode;
		await context.Response.WriteAsync(response.ReasonPhrase);
	}
});

// Fetch endpoint
app.MapGet("/fetch", async (HttpContext context, IHttpClientFactory clientFactory, string uri) => {
	var client = clientFactory.CreateClient();
	var response = await client.GetAsync(uri);

	context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "text/plain";
	context.Response.StatusCode = (int)response.StatusCode;

	var responseBody = await response.Content.ReadAsStringAsync();
	await context.Response.WriteAsync(responseBody);
});

// Send POST request with body
app.MapGet("/post", async (HttpContext context, IHttpClientFactory clientFactory, string uri, string body) => {
	var client = clientFactory.CreateClient();

	var requestMessage = new HttpRequestMessage(HttpMethod.Post, uri) {
		Content = new StringContent(body, System.Text.Encoding.UTF8, context.Request.ContentType ?? "application/json")
	};

	var response = await client.SendAsync(requestMessage);
	
	context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "text/plain";
	context.Response.StatusCode = (int)response.StatusCode;

	var responseBody = await response.Content.ReadAsStringAsync();
	await context.Response.WriteAsync(responseBody);
});

app.Run();