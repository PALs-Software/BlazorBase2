using BlazorBase.CRUD.Benchmarks.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.CRUD.Benchmarks.Infrastructure;

public static class DatabaseFixture
{
    public const int CategoryCount = 10;

    public const int OrderItemsPerOrder = 5;

    public static string CreateDatabaseFile(string suffix)
    {
        var directory = Path.Combine(Path.GetTempPath(), "BlazorBase.CRUD.Benchmarks");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"bench-{suffix}-{Guid.NewGuid():N}.db");
        if (File.Exists(path))
            File.Delete(path);
        return path;
    }

    public static string BuildConnectionString(string databasePath)
        => $"Data Source={databasePath};Cache=Shared;Pooling=True";

    public static void CreateAndSeed(BenchmarkDbContext context, int productCount, int orderCount)
    {
        context.Database.EnsureCreated();
        Seed(context, productCount, orderCount);
    }

    private static void Seed(BenchmarkDbContext context, int productCount, int orderCount)
    {
        var random = new Random(42);

        var categories = new List<BenchCategory>(CategoryCount);
        for (var index = 0; index < CategoryCount; index++)
        {
            categories.Add(new BenchCategory
            {
                Id = DeterministicGuid(1, index),
                Name = $"Category {index:D3}",
                DisplayOrder = index,
                IsActive = true
            });
        }
        context.Categories.AddRange(categories);
        context.SaveChanges();

        const int batchSize = 1000;
        var productBatch = new List<BenchProduct>(batchSize);
        for (var index = 0; index < productCount; index++)
        {
            productBatch.Add(new BenchProduct
            {
                Id = DeterministicGuid(2, index),
                Name = $"Product {index:D7}",
                Price = (decimal)Math.Round(random.NextDouble() * 1000.0, 2),
                Description = index % 3 == 0 ? $"Description for product {index}" : null,
                IsActive = index % 7 != 0,
                Stock = random.Next(0, 500),
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(index),
                CategoryId = categories[index % CategoryCount].Id
            });

            if (productBatch.Count >= batchSize)
            {
                context.Products.AddRange(productBatch);
                context.SaveChanges();
                productBatch.Clear();
                context.ChangeTracker.Clear();
            }
        }

        if (productBatch.Count > 0)
        {
            context.Products.AddRange(productBatch);
            context.SaveChanges();
            context.ChangeTracker.Clear();
        }

        var orderBatch = new List<BenchOrder>(batchSize);
        for (var index = 0; index < orderCount; index++)
        {
            var order = new BenchOrder
            {
                Id = DeterministicGuid(3, index),
                CustomerName = $"Customer {index:D6}",
                OrderDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(index),
                IsActive = index % 5 != 0,
                Items = []
            };

            for (var itemIndex = 0; itemIndex < OrderItemsPerOrder; itemIndex++)
            {
                order.Items.Add(new BenchOrderItem
                {
                    Id = DeterministicGuid(4, index * OrderItemsPerOrder + itemIndex),
                    OrderId = order.Id,
                    ProductName = $"Order {index} Item {itemIndex}",
                    Quantity = random.Next(1, 10),
                    UnitPrice = (decimal)Math.Round(random.NextDouble() * 100.0, 2),
                    DisplayOrder = itemIndex,
                    IsActive = itemIndex % 2 == 0
                });
            }

            orderBatch.Add(order);

            if (orderBatch.Count >= batchSize / OrderItemsPerOrder)
            {
                context.Orders.AddRange(orderBatch);
                context.SaveChanges();
                orderBatch.Clear();
                context.ChangeTracker.Clear();
            }
        }

        if (orderBatch.Count > 0)
        {
            context.Orders.AddRange(orderBatch);
            context.SaveChanges();
            context.ChangeTracker.Clear();
        }
    }

    public static Guid DeterministicGuid(int kind, int index)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes[..4], kind);
        BitConverter.TryWriteBytes(bytes.Slice(4, 4), index);
        BitConverter.TryWriteBytes(bytes.Slice(8, 4), index);
        BitConverter.TryWriteBytes(bytes.Slice(12, 4), kind ^ index);
        return new Guid(bytes);
    }
}
