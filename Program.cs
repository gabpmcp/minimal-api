using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using MinimalApi.CQRS;
using MinimalApi.Validations;
using MinimalApi.Helpers;
using MinimalApi.IO.Cache;
using Microsoft.Identity.Client.Extensions.Msal;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();


// Configura Kestrel para escuchar en todas las interfaces
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(5000); // Puerto 5000
});

var SetUpCommandBuider = (Func<Stream, Task<Dictionary<string, object>>> deserialize) =>
    (Stream stream) => async (Func<Dictionary<string, object>, Command> createCommand) =>
{
    var deserializedData = await deserialize(stream);
    return createCommand(deserializedData);
};

var Serializer = SetUpCommandBuider(JsonSerialization.DeserializeAsync<Dictionary<string, object>>);
// var memoizedBuildCommand = Memoization.Memoize<(HttpContext context, Func<Stream, Task<Dictionary<string, object>>> deserialize), Command>(PreBuild);

app.MapMethods("/products", new[] { "POST", "PUT", "GET" }, async (HttpContext context) =>
{
    var BuildCommand = Serializer(context.Request.Body);
    var method = context.Request.Method;
    Command command = method switch
    {
        "POST" => await BuildCommand(postData => Commands.CreateItem(postData["ProductId"].ToString(), postData["Status"].ToString(), postData["Name"].ToString(), Convert.ToDecimal(postData["Price"]))),
        "PUT" => await BuildCommand(postData => Commands.UpdateItem(postData["ProductId"].ToString(), postData["Name"].ToString(), Convert.ToDecimal(postData["Price"]))),
        "GET" => await BuildCommand(postData =>
        {
            var id = Guid.Parse(context.Request.Query["id"].ToString() ?? string.Empty);
            return Commands.GetById(id);
        }),
        _ => await Task.FromResult(new Command("UnsupportedCommand", []))
    }

    var result = command.Validate(Validations.commandSchemas);

    if (!result.IsValid)
    {
        Results.BadRequest(result);
    }

    var redis = new RedisCache<string>("lsakdaslkdsadolk");
    
    return command switch
    {
        { Kind: "Insert" } => InsertedProduct(redis.GetDistributedValue, redis.SetDistributedValue, content => content, JsonSerialization.Serialize, EventStore.SaveEventAsync,
        command.GetData<string>("Id"), command.GetData<string>("Status"), command.GetData<string>("Name"), command.GetData<decimal>("Price")),
        { Kind: "Update" } => UpdatedProduct(redis.GetDistributedValue, redis.SetDistributedValue, JsonSerialization.Serialize, EventStore.SaveEventAsync, command),
        { Kind: "GetById" } => FetchedCommand(command, redis.GetDistributedValue),
        { Kind: "UnsupportedCommand" } => Results.BadRequest("Unsupported command!"),
        _ => Results.BadRequest("Invalid command")
    }
})

static async Task<IResult> InsertedProduct(
    Func<string, (bool, string)> getDistributedValue,
    Func<string, string, Task<bool>> setDistributedValue,
    Func<string, string> deserializeDistributedValue,
    Func<string, string> serializeNewProduct,
    Func<string, string, object, Task<EventRecord>> persistEvent,
    string productId, string status, string name, decimal price)
{
    var cache = TwoLevelCache<string, string>.Instance;

    // Check if the product already exists in the cache
    var productStatus = await cache.GetAsync(status, () => Task.FromResult(status), getDistributedValue, setDistributedValue, deserializeDistributedValue, serializeNewProduct);
    if (productStatus != default)
        return Results.Conflict("Product status already exists");
    
    // Create and persist the event using the HOF
    var @event = Events.ItemCreated(productId, productStatus ?? status, price);
    var persitedEvent = await persistEvent(productId, @event.Kind, @event);

    return Results.Ok(@event);
}

static async Task<IResult> UpdatedProduct(
    Func<string, (bool, string)> getDistributedValue,
    Func<string, string, Task<bool>> setDistributedValue,
    Func<object, string> serialize,
    Func<string, string, object, Task<EventRecord>> persistEvent,
    Command command)
{
    var productId = command.GetData<string>("Id");
    var status = command.GetData<string>("Status");
    if (products.ContainsKey(productId))
    {
        var name = command.GetData<string>("Name");
        var price = command.GetData<decimal>("Price");
        products[productId] = (name, price);
        var cache = TwoLevelCache<string, string>.Instance;
        await cache.SetAsync(status, command.GetData<string>("Status"), setDistributedValue, serialize);
        
        var @event = Events.ItemUpdated(productId, name, price);
        var persitedEvent = await persistEvent(productId, @event.Kind, @event);
        
        return Results.Ok(@event);
    }
    else
    {
        return Results.NotFound("Product not found");
    }
}

static async Task<IResult> FetchedCommand(Func<string, (bool, string)> getDistributedValue, Func<string, string, object, Task<EventRecord>> persistEvent, Command command)
{
    var productId = command.GetData<string>("Id");
    if (products.ContainsKey(id))
    {
        var product = products[id];
        var @event = Events.ItemFetched(productId, product.Name, product.Price);
        var persitedEvent = await persistEvent(productId, @event.Kind, @event);
        return Results.Ok(@event);
    }
    else
    {
        return Results.NotFound("Product not found");
    }
}

app.MapGet("/events/{productId}", async (string productId, DateTimeOffset startDate, DateTimeOffset endDate) =>
{
    return await FetchedEventsForProduct(productId, startDate, endDate);
})

app.Run();
