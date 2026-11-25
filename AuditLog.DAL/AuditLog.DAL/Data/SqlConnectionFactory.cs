using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Data.SqlClient;

namespace AuditLog.DAL.Data
{
    public sealed class SqlConnectionFactory
    {
        private readonly string _connectionString;

        public SqlConnectionFactory(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException(
                    "Database connection string is required.",
                    nameof(connectionString));
            }

            _connectionString = connectionString;
        }

        public SqlConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }
    }

}
