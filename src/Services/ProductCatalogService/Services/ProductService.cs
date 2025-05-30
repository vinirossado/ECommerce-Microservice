using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using ProductCatalog.Data;
using ProductCatalog.Models;
using System.Text.Json;

namespace ProductCatalog.Services;

public class ProductService : IProductService
{
    private readonly ProductCatalogDbContext _context;
    private readonly IDistributedCache _cache;
    private readonly ILogger<ProductService> _logger;

    public ProductService(
        ProductCatalogDbContext context,
        IDistributedCache cache,
        ILogger<ProductService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IEnumerable<Product>> GetAllProductsAsync(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        string cacheKey = $"products-page-{page}-size-{pageSize}";
        
        // Try to get from cache first
        var cachedProducts = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cachedProducts))
        {
            _logger.LogInformation("Cache hit for {CacheKey}", cacheKey);
            return JsonSerializer.Deserialize<List<Product>>(cachedProducts) ?? new List<Product>();
        }
        
        // If not in cache, get from database
        var products = await _context.Products
            .Include(p => p.Category)
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
            
        // Cache the result
        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
        };
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(products), cacheOptions, cancellationToken);
        
        return products;
    }

    public async Task<Product?> GetProductByIdAsync(int productId, CancellationToken cancellationToken = default)
    {
        string cacheKey = $"product-{productId}";
        
        // Try to get from cache first
        var cachedProduct = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cachedProduct))
        {
            _logger.LogInformation("Cache hit for {CacheKey}", cacheKey);
            return JsonSerializer.Deserialize<Product>(cachedProduct);
        }
        
        // If not in cache, get from database
        var product = await _context.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive, cancellationToken);
            
        if (product != null)
        {
            // Cache the result
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
            };
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(product), cacheOptions, cancellationToken);
        }
        
        return product;
    }

    public async Task<IEnumerable<Product>> GetProductsByCategoryAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .Include(p => p.Category)
            .Where(p => p.CategoryId == categoryId && p.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task<Product> CreateProductAsync(Product product, CancellationToken cancellationToken = default)
    {
        await _context.Products.AddAsync(product, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        
        // Invalidate relevant caches
        await _cache.RemoveAsync($"products-page-1-size-10", cancellationToken);
        
        return product;
    }

    public async Task<Product?> UpdateProductAsync(int productId, Product updatedProduct, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products.FindAsync(new object[] { productId }, cancellationToken);
        
        if (product == null)
        {
            return null;
        }

        product.Name = updatedProduct.Name;
        product.Description = updatedProduct.Description;
        product.Price = updatedProduct.Price;
        product.StockQuantity = updatedProduct.StockQuantity;
        product.ImageUrl = updatedProduct.ImageUrl;
        product.CategoryId = updatedProduct.CategoryId;
        product.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync(cancellationToken);
        
        // Invalidate caches
        await _cache.RemoveAsync($"product-{productId}", cancellationToken);
        await _cache.RemoveAsync($"products-page-1-size-10", cancellationToken);
        
        return product;
    }

    public async Task<bool> DeleteProductAsync(int productId, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products.FindAsync(new object[] { productId }, cancellationToken);
        
        if (product == null)
        {
            return false;
        }

        // Soft delete
        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync(cancellationToken);
        
        // Invalidate caches
        await _cache.RemoveAsync($"product-{productId}", cancellationToken);
        await _cache.RemoveAsync($"products-page-1-size-10", cancellationToken);
        
        return true;
    }

    public async Task<int> GetTotalProductsCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .Where(p => p.IsActive)
            .CountAsync(cancellationToken);
    }
}
