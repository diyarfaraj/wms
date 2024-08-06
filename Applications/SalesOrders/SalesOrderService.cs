using Indotalent.Applications.Products;
using Indotalent.Data;
using Indotalent.Infrastructures.Repositories;
using Indotalent.Models.Contracts;
using Indotalent.Models.Entities;
using Indotalent.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Indotalent.Applications.SalesOrders
{
    public class SalesOrderService : Repository<SalesOrder>
    {
        private readonly ApplicationDbContext _context;
        private readonly ProductService _productService;
        public SalesOrderService(
            ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor,
            IAuditColumnTransformer auditColumnTransformer, 
            ProductService productService) :
                base(
                    context,
                    httpContextAccessor,
                    auditColumnTransformer)
        {
            _context = context;
            _productService = productService;
        }

        // TODO: Update products database when order is confirmed, 
        public async Task RecalculateParentAsync(int? masterId)
        {

            var master = await _context.Set<SalesOrder>()
                .Include(x => x.Tax)
                .Where(x => x.Id == masterId && x.IsNotDeleted == true)
                .FirstOrDefaultAsync();

            var childs = await _context.Set<SalesOrderItem>()
                .Where(x => x.SalesOrderId == masterId && x.IsNotDeleted == true)
                .ToListAsync();

            if (master != null)
            {
                master.BeforeTaxAmount = 0;
                foreach (var item in childs)
                {
                    master.BeforeTaxAmount += item.Total;
                }
                if (master.Tax != null)
                {
                    master.TaxAmount = (master.Tax.Percentage / 100.0) * master.BeforeTaxAmount;
                }
                master.AfterTaxAmount = master.BeforeTaxAmount + master.TaxAmount;
                _context.Set<SalesOrder>().Update(master);
                await _context.SaveChangesAsync();
            }
        }



        public override async Task UpdateAsync(SalesOrder? entity)
        {
            if (entity == null)
                throw new Exception("Entity is null");

            // Load the existing order to compare statuses
            var existingOrder = await _context.SalesOrder.AsNoTracking().FirstOrDefaultAsync(x => x.Id == entity.Id);
            if (existingOrder == null)
                throw new Exception("Existing entity not found");

            if (entity.OrderStatus == SalesOrderStatus.Confirmed && existingOrder.OrderStatus != SalesOrderStatus.Confirmed)
            {
                // Deduct quantities from products only if the status is being set to Confirmed
                await UpdateProductQuantitiesForOrder(entity.Id);
            }

            // Standard audit and update operations
            if (entity is IHasAudit auditedEntity && !string.IsNullOrEmpty(_userId))
            {
                auditedEntity.UpdatedByUserId = _userId;
                auditedEntity.UpdatedAtUtc = DateTime.Now;
            }

            _context.Update(entity);
            await _context.SaveChangesAsync();
        }

        private async Task UpdateProductQuantitiesForOrder(int orderId)
        {
            var orderItems = await _context.SalesOrderItem.Where(x => x.SalesOrderId == orderId).ToListAsync();
            foreach (var item in orderItems)
            {
                await _productService.UpdateProductQuantity(item.ProductId, -item.Quantity ?? 0);
            }
        }

    }
}
