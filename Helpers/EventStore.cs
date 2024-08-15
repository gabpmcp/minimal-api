using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace MinimalApi.Helpers
{
    public record EventRecord
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string ProductId { get; init; }
        public string EventType { get; init; }
        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
        public string Data { get; init; }

        public EventRecord(Guid id, string productId, string eventType, DateTimeOffset timestamp, string data)
        {
            Id = id;
            ProductId = !string.IsNullOrWhiteSpace(productId) ? productId : throw new ArgumentNullException(nameof(productId));
            EventType = !string.IsNullOrWhiteSpace(eventType) ? eventType : throw new ArgumentNullException(nameof(eventType));
            Timestamp = timestamp;
            Data = !string.IsNullOrWhiteSpace(data) ? data : throw new ArgumentNullException(nameof(data));
        }
    }

    public class EventStoreContext : DbContext
    {
        public DbSet<EventRecord> Events { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlServer("YourConnectionString");
    }

    public static class EventStore
    {
        public static async Task<EventRecord> SaveEventAsync(string eventType, string productId, object eventData)
        {
            var eventToSave = new EventRecord(Guid.NewGuid(), productId, eventType, DateTimeOffset.UtcNow, JsonConvert.SerializeObject(eventData));
            using var context = new EventStoreContext();
            context.Events.Add(eventToSave);
            await context.SaveChangesAsync();
            return eventToSave;
        }
    }
}