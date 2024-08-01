using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using MinimalApi.CQRS;
using MinimalApi.Validations;
using MinimalApi.Helpers;
using MinimalApi.IO.Cache;

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
        "POST" => await BuildCommand(postData => Commands.CreateItem(Guid.NewGuid(), postData["Name"].ToString(), Convert.ToDecimal(postData["Price"]))),
        "PUT" => await BuildCommand(postData => Commands.UpdateItem(Guid.NewGuid(), postData["Name"].ToString(), Convert.ToDecimal(postData["Price"]))),
        "GET" => await BuildCommand(postData => {
            var id = Guid.Parse(context.Request.Query["id"].ToString() ?? string.Empty);
            return Commands.GetById(id);
        }),
        _ => await Task.FromResult(new Command("UnsupportedCommand", []))
    };
    
    var result = command.Validate(Validations.commandSchemas);

    if (!result.IsValid) {
        Results.BadRequest(result);
    }

    var redis = new RedisCache<string>("lsakdaslkdsadolk");

    return command switch
    {
        { Kind: "Insert" } => InsertedProduct(redis.GetDistributedValue, redis.SetDistributedValue, content => content, JsonSerialization.Serialize, command),
        { Kind: "Update" } => UpdatedProduct(command, redis.SetDistributedValue, JsonSerialization.Serialize),
        { Kind: "GetById" } => FetchedCommand(command, redis.SetDistributedValue, JsonSerialization.Serialize),
        { Kind: "UnsupportedCommand" } => Results.BadRequest("Unsupported command!"),
        _ => Results.BadRequest("Invalid command")
    };
});


async Task<IResult> InsertedProduct(Func<string, (bool, string)> getDistributedValue, Func<string, string, Task<bool>> setDistributedValue, Func<string, string> deserializeDistributedValue, Func<string, string> serializeNewProduct, Command command)
{
    var status = command.GetData<string>("Status");
    var name = command.GetData<string>("Name");
    var price = command.GetData<decimal>("Price");
    var cache = new TwoLevelCache<string, string>();

    // Check if the product already exists in the cache
    var product = await cache.GetAsync(status, () => Task.FromResult(status), getDistributedValue, setDistributedValue, deserializeDistributedValue, serializeNewProduct);
    if (product != default)
    {
        return Results.Conflict("Product already exists");
    }

    // Insert the new product
    products[id] = (name, price);
    await cache.SetAsync(id, (name, price), setDistributedValue, serializeNewProduct);
    var @event = Events.ItemCreated(id, name, price);
    return Results.Ok(@event);
}

async Task<IResult> UpdatedProduct(Command command, Func<Guid, (bool, string)> getDistributedValue, Func<Guid, string, Task<bool>> setDistributedValue, Func<object, string> serialize)
{
    var id = command.GetData<Guid>("Id");
    if (products.ContainsKey(id))
    {
        var name = command.GetData<string>("Name");
        var price = command.GetData<decimal>("Price");
        products[id] = (name, price);
        var cache = new TwoLevelCache<Guid, (string Name, decimal Price)>();
        await cache.SetAsync(id, (name, price), getDistributedValue, setDistributedValue, JsonSerialization.Deserialize<(string Name, decimal Price)>, serialize);
        var @event = Events.ItemUpdated(id, name, price);
        return Results.Ok(@event);
    }
    else
    {
        return Results.NotFound("Product not found");
    }
}

async Task<IResult> FetchedCommand(Command command, Func<Guid, (bool, string)> getDistributedValue, Func<string, (string Name, decimal Price)> deserialize)
{
    var id = command.GetData<Guid>("Id");
    if (products.ContainsKey(id))
    {
        var product = products[id];
        var @event = Events.ItemFetched(id, product.Name, product.Price);
        return Results.Ok(@event);
    }
    else
    {
        return Results.NotFound("Product not found");
    }
}

app.Run();
