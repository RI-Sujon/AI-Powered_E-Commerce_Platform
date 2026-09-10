using Microsoft.EntityFrameworkCore;
using Project.Object.Entities;
using Project.Object.Responses;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // pgvector: enable the extension and map the embedding column (see Phase 2 + 5 docs).
        // Postgres-only - the in-memory provider used by unit tests can't map the `vector` type,
        // so the entity is excluded there (nothing in the test suite touches embeddings).
        if (Database.IsNpgsql())
        {
            builder.HasPostgresExtension("vector");
            builder.Entity<ProductEmbeddingEntity>(entity =>
            {
                entity.ToTable("ProductEmbeddings");
                entity.HasKey(e => e.ProductId);
                entity.HasOne<ProductEntity>()
                      .WithOne()
                      .HasForeignKey<ProductEmbeddingEntity>(e => e.ProductId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.Property(e => e.Embedding).HasColumnType("vector(1536)");
                entity.Property(e => e.SourceHash).HasMaxLength(64);
                entity.Property(e => e.Model).HasMaxLength(100);
            });
        }
        else
        {
            builder.Ignore<ProductEmbeddingEntity>();
        }

        // Configure Product entity
        builder.Entity<ProductEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne<CategoryEntity>()
                  .WithMany()
                  .HasForeignKey(e => e.CategoryId)
                  .IsRequired(false);
            entity.HasIndex(e => e.Slug);
            entity.HasIndex(e => e.CategoryId);
            entity.HasIndex(e => e.IsActive);
        });

        // Configure Cart entity
        builder.Entity<CartEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne<ProductEntity>()
                  .WithMany()
                  .HasForeignKey(e => e.ProductId);
        });

        // Configure User entity
        builder.Entity<UserEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // Configure Order entity
        builder.Entity<OrderEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Status);
        });

        // Configure OrderItem entity
        builder.Entity<OrderItemEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne<OrderEntity>()
                  .WithMany()
                  .HasForeignKey(e => e.OrderId);
        });
    }

    // You can add other DbSets here
    public DbSet<ProductEntity> Products { get; set; }
    public DbSet<ProductEmbeddingEntity> ProductEmbeddings { get; set; }
    public DbSet<CartEntity> Carts { get; set; }
    public DbSet<UserEntity> Users { get; set; }
    public DbSet<CategoryEntity> Categories { get; set; }
    public DbSet<OrderEntity> Orders { get; set; }
    public DbSet<OrderItemEntity> OrderItems { get; set; }
}
