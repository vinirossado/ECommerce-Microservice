using ProductCatalog.Models;

namespace ProductCatalog.Services;

public interface IProductService
{
    Task<IEnumerable<Product>> GetAllProductsAsync(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);
    Task<Product?> GetProductByIdAsync(int productId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Product>> GetProductsByCategoryAsync(int categoryId, CancellationToken cancellationToken = default);
    Task<Product> CreateProductAsync(Product product, CancellationToken cancellationToken = default);
    Task<Product?> UpdateProductAsync(int productId, Product product, CancellationToken cancellationToken = default);
    Task<bool> DeleteProductAsync(int productId, CancellationToken cancellationToken = default);
    Task<int> GetTotalProductsCountAsync(CancellationToken cancellationToken = default);
}
