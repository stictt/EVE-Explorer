using Microsoft.EntityFrameworkCore;
using System;

namespace PrimaryAggregatorService.Models.DataBases
{
    public class AggregatorContext : DbContext
    {
        public AggregatorContext(DbContextOptions<AggregatorContext> options) : base(options)
        {
            //Database.EnsureCreated();   // гарантируем, что БД создана
            Database.Migrate();
            this.SaveChanges();
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder
                .Entity<OrderDTO>()
                .Property(x => x.Range)
                 .HasConversion(
                    x => x.ToString(),
                    x => (RangeOrderMarket)Enum.Parse(typeof(RangeOrderMarket), x)); 
        }
        public DbSet<OrderDTO> Orders { get; set; }
    }
}
