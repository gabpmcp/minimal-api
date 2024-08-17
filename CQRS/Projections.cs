public async Task<IResult> FetchedEventsForProduct(string productId, DateTimeOffset startDate, DateTimeOffset endDate)
{
    var events = await GetEventsByProductIdAsync(productId, startDate, endDate);

    if (events.Any())
    {
        return Results.Ok(events);
    }

    return Results.NotFound("No events found for the specified product and date range.");
}

public async Task<List<EventRecord>> GetEventsByProductIdAsync(string productId, DateTimeOffset startDate, DateTimeOffset endDate)
{
    using var context = new EventStoreContext();

    // Consulta usando LINQ
    var query = from e in context.Events
                where e.ProductId == productId
                && e.Timestamp >= startDate
                && e.Timestamp <= endDate
                orderby e.Timestamp descending
                select e;

    // Ejecutar la consulta y obtener los resultados
    return await query.ToListAsync();
}

public async Task<List<EventRecord>> GetEventsByProductIdAsync(string productId, DateTimeOffset startDate, DateTimeOffset endDate)
{
    using var context = new EventStoreContext();

    // Consulta usando expresiones lambda
    var events = await context.Events
                              .Where(e => e.ProductId == productId 
                                       && e.Timestamp >= startDate 
                                       && e.Timestamp <= endDate)
                              .OrderByDescending(e => e.Timestamp)
                              .ToListAsync();

    return events;
}