using Indotalent.Data;
using Indotalent.Infrastructures.Repositories;
using Indotalent.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace Indotalent.Applications.Products
{
    public class ProductService : Repository<Product>
    {
        public ProductService(
            ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor,
            IAuditColumnTransformer auditColumnTransformer) :
                base(context, httpContextAccessor, auditColumnTransformer)
        {
        }

        /// <summary>
        /// Updates the quantity of a product by a specific amount.
        /// </summary>
        /// <param name="productId">The ID of the product to update.</param>
        /// <param name="quantityChange">The amount to adjust the quantity by. Negative values reduce the quantity.</param>
        /// <returns></returns>
        public async Task UpdateProductQuantity(int productId, double quantityChange)
        {
            var product = await _context.Product.FindAsync(productId);
            if (product == null)
            {
                throw new ArgumentException("Product not found with provided ID", nameof(productId));
            }

            if (!product.Physical)
            {
                throw new InvalidOperationException("Cannot update quantity for a non-physical product.");
            }

            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    product.Quantity += quantityChange;

                    // Optionally, handle negative inventory scenarios:
                    if (product.Quantity < 0)
                    {
                        // Handle how your business rules dictate, e.g., throw error, set to zero, etc.
                        throw new InvalidOperationException("Updating quantity resulted in negative stock level.");
                    }

                    _context.Product.Update(product);
                    await _context.SaveChangesAsync();

                    transaction.Commit();  // Commit transaction if all operations succeed
                }
                catch (Exception)
                {
                    transaction.Rollback();  // Rollback transaction on error
                    throw;  // Re-throw the exception to be handled by the caller
                }
            }
        }
    }
}
