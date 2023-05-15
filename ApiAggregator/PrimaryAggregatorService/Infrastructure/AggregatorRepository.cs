using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using PrimaryAggregatorService.Models;
using PrimaryAggregatorService.Models.DataBases;
using System.Linq;

namespace PrimaryAggregatorService.Infrastructure
{
    public class AggregatorRepository
    {
        private readonly AggregatorContext _context;
        private readonly ConnectionString _connectionString;
        public AggregatorRepository(AggregatorContext context, IOptions<ConnectionString> settings) 
        {
            _context = context;
            _connectionString = settings.Value;
        }

        public async Task BinaryInsertOrdersAsync(List<OrderDTO> orders)
        {
            using var connection = new NpgsqlConnection(_connectionString.DefaultConnection);
            
            connection.Open();
            using var binaryImporter = connection.BeginBinaryImport(@"COPY ""Orders"" 
                    (""Duration"", ""IsBuyOrder"", ""Issued"", ""LocationId"",
                        ""MinVolume"", ""OrderId"", ""PackageDate"", ""Price"",
                        ""Range"", ""SystemId"", ""TypeId"", ""VolumeRemain"", ""VolumeTotal"") FROM STDIN (FORMAT BINARY)");
            
            foreach (var customer in orders)
            {
                binaryImporter.StartRow();

                binaryImporter.Write(customer.Duration, NpgsqlDbType.Smallint);
                binaryImporter.Write(customer.IsBuyOrder, NpgsqlDbType.Boolean);
                binaryImporter.Write(customer.Issued, NpgsqlDbType.TimestampTZ);
                binaryImporter.Write(customer.LocationId, NpgsqlDbType.Bigint);

                binaryImporter.Write(customer.MinVolume, NpgsqlDbType.Integer);
                binaryImporter.Write(customer.OrderId, NpgsqlDbType.Bigint);
                binaryImporter.Write(customer.PackageDate, NpgsqlDbType.TimestampTZ);
                binaryImporter.Write(customer.Price, NpgsqlDbType.Double);

                binaryImporter.Write(customer.Range.ToString(), NpgsqlDbType.Text);
                binaryImporter.Write(customer.SystemId, NpgsqlDbType.Integer);
                binaryImporter.Write(customer.TypeId, NpgsqlDbType.Integer);
                binaryImporter.Write(customer.VolumeRemain, NpgsqlDbType.Bigint);

                binaryImporter.Write(customer.VolumeTotal, NpgsqlDbType.Bigint);
            }
            await binaryImporter.CompleteAsync();
        }

        public async Task InsertPackage(DateTime date)
        {
            await _context.Packages.AddAsync(new Package() { PackageDate = date });
        }

        public async Task<Package> GetLastPackageAsync()
        {
            return await _context.Packages.OrderByDescending(x => x.PackageDate)
                .FirstOrDefaultAsync();
        }

        public async Task<List<OrderDTO>> GetOrdersInDataAsync(DateTime date, List<int> typesId = null )
        {
            if (typesId != null && typesId.Count > 0)
            {
                return await _context.Orders.AsNoTracking()
                    .Where(x => typesId.Contains(x.TypeId) && x.PackageDate == date)
                    .ToListAsync();
            }
            else
            {
                return await _context.Orders.AsNoTracking()
                    .Where(x => x.PackageDate == date)
                    .ToListAsync();
            }
        }

        public async Task<List<OrderDTO>> GetOrdersAsync(List<int> typesId, TimeSpan dateRange)
        {
            if (typesId != null && typesId.Count > 0)
            {
                return await _context.Orders.AsNoTracking()
                    .Where(x => typesId.Contains(x.TypeId) && x.PackageDate > (DateTime.UtcNow - dateRange))
                    .ToListAsync();
            }
            else
            {
                return await _context.Orders.AsNoTracking()
                    .Where(x => x.PackageDate > (DateTime.UtcNow - dateRange))
                    .ToListAsync();
            }
        }

        public async Task DeleteOldOrders()
        {
            using var conn = new NpgsqlConnection(_connectionString.DefaultConnection);
            conn.Open();

            using var command = new NpgsqlCommand(@"SELECT delete_orders_with_excessive_date()", conn);
            await command.ExecuteNonQueryAsync();

            conn.Close();
        }
    }
}
