using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Ejercicio6_Final.Abstractions;
using Ejercicio6_Final.Models;

namespace Ejercicio6_Final.Infrastructure
{
    public class SqlProjectRepository : IProjectRepository
    {
        private readonly DbConnection _connection;

        public SqlProjectRepository(DbConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public async Task<ProjectData?> GetByIdAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await EnsureConnectionOpenAsync(cancellationToken);

            using DbCommand command = _connection.CreateCommand();
            command.CommandText = @"
SELECT Id, Budget, Expenses, OwnerEmail, Status
FROM Projects
WHERE Id = @Id;";

            DbParameter idParam = command.CreateParameter();
            idParam.ParameterName = "@Id";
            idParam.DbType = DbType.Int32;
            idParam.Value = projectId;
            command.Parameters.Add(idParam);

            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new ProjectData
            {
                Id = reader.GetInt32(0),
                Budget = reader.GetDecimal(1),
                Expenses = reader.GetDecimal(2),
                OwnerEmail = reader.GetString(3),
                Status = reader.GetString(4)
            };
        }

        public async Task SaveClosureAsync(
            int projectId,
            decimal finalBalance,
            DateTime closedAtUtc,
            string finalStatus,
            CancellationToken cancellationToken = default)
        {
            await EnsureConnectionOpenAsync(cancellationToken);

            using DbCommand command = _connection.CreateCommand();
            command.CommandText = @"
UPDATE Projects
SET Status = @Status,
    FinalBalance = @FinalBalance,
    ClosedAtUtc = @ClosedAtUtc
WHERE Id = @Id;";

            DbParameter idParam = command.CreateParameter();
            idParam.ParameterName = "@Id";
            idParam.DbType = DbType.Int32;
            idParam.Value = projectId;
            command.Parameters.Add(idParam);

            DbParameter statusParam = command.CreateParameter();
            statusParam.ParameterName = "@Status";
            statusParam.DbType = DbType.String;
            statusParam.Value = finalStatus;
            command.Parameters.Add(statusParam);

            DbParameter balanceParam = command.CreateParameter();
            balanceParam.ParameterName = "@FinalBalance";
            balanceParam.DbType = DbType.Decimal;
            balanceParam.Value = finalBalance;
            command.Parameters.Add(balanceParam);

            DbParameter closedAtParam = command.CreateParameter();
            closedAtParam.ParameterName = "@ClosedAtUtc";
            closedAtParam.DbType = DbType.DateTime2;
            closedAtParam.Value = closedAtUtc;
            command.Parameters.Add(closedAtParam);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task EnsureConnectionOpenAsync(CancellationToken cancellationToken)
        {
            if (_connection.State == ConnectionState.Open)
            {
                return;
            }

            await _connection.OpenAsync(cancellationToken);
        }
    }
}
