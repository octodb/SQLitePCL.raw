using System;
using SQLitePCL;
using SQLitePCL.Ugly;
using Microsoft.Data.Sqlite;

namespace TestLiteSync
{
    class Program
    {
        static Program()
        {
            // Initialize SQLitePCL provider BEFORE any Microsoft.Data.Sqlite usage
            Console.WriteLine("Initializing LiteSync provider in static constructor...");
            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_LiteSync());
            SQLitePCL.raw.FreezeProvider(true);
            Console.WriteLine("LiteSync provider initialized and frozen.");
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Testing SQLitePCLRaw with LiteSync provider...");
            Console.WriteLine();

            // Test SQLitePCLRaw alone first
            TestSQLitePCLRawAlone();

            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine();

            // Then test Microsoft.Data.Sqlite
            TestMicrosoftDataSqlite();

            Console.WriteLine();
            Console.WriteLine("All LiteSync tests completed!");
        }

        static void TestSQLitePCLRawAlone()
        {
            Console.WriteLine("=== Testing SQLitePCLRaw Alone ===");

            try
            {
                // Provider already set in static constructor
                Console.WriteLine("✓ LiteSync provider loaded successfully");

                // Create a test database with LiteSync URI using raw APIs
                var uri = "file:test_litesync_raw.db?node=primary&bind=tcp://0.0.0.0:1234";

                using (var db = ugly.open(uri))
                {
                    Console.WriteLine("  Database opened successfully with raw API");

                    // Check if database is ready (LiteSync specific) using raw pragma
                    var ready = false;
                    var attempts = 0;
                    const int maxAttempts = 10;

                    // First verify LiteSync is working by checking version
                    var version = db.query_scalar<string>("pragma litesync_version");
                    if (string.IsNullOrEmpty(version))
                    {
                        throw new Exception("LiteSync version check failed: pragma litesync_version returned null or empty. LiteSync native library may not be properly loaded.");
                    }
                    Console.WriteLine($"✓ LiteSync version: {version}");

                    while (!ready && attempts < maxAttempts)
                    {
                        var status = db.query_scalar<string>("pragma sync_status");
                        if (string.IsNullOrEmpty(status) || !status.TrimStart().StartsWith("{"))
                        {
                            throw new Exception($"Invalid sync_status response: expected JSON starting with '{{' but got '{status ?? "null"}'. LiteSync provider may not be properly configured.");
                        }

                        Console.WriteLine($"  Sync status: {status}");
                        if (status.Contains("\"db_is_ready\": true"))
                        {
                            ready = true;
                            Console.WriteLine("✓ LiteSync database is ready");
                        }

                        if (!ready)
                        {
                            attempts++;
                            System.Threading.Thread.Sleep(500);
                        }
                    }

                    if (!ready)
                    {
                        throw new Exception($"LiteSync database not ready after {maxAttempts} attempts. This indicates the LiteSync native library may not be available or properly configured.");
                    }

                    // Create a test table using raw exec
                    db.exec(@"
                        CREATE TABLE IF NOT EXISTS test_raw_table (
                            id INTEGER PRIMARY KEY,
                            name TEXT NOT NULL,
                            sync_value INTEGER
                        )");

                    // Insert test data using raw exec with parameters
                    db.exec("INSERT INTO test_raw_table (name, sync_value) VALUES (?, ?)",
                           "SQLitePCLRaw Test Item", 456);

                    // Query the data using raw query methods
                    var names = db.query_one_column<string>("SELECT name FROM test_raw_table");
                    var values = db.query_one_column<long>("SELECT sync_value FROM test_raw_table");

                    Console.WriteLine("  Query results:");
                    foreach (var name in names)
                    {
                        Console.WriteLine($"    Found name: {name}");
                    }
                    foreach (var value in values)
                    {
                        Console.WriteLine($"    Found sync_value: {value}");
                    }

                    Console.WriteLine("✓ SQLitePCLRaw basic operations completed successfully");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ SQLitePCLRaw test failed: {ex.Message}");
                Console.WriteLine($"  Exception type: {ex.GetType().FullName}");

                if (ex.InnerException != null)
                {
                    Console.WriteLine($"  Inner exception: {ex.InnerException.Message}");
                    Console.WriteLine($"  Inner exception type: {ex.InnerException.GetType().FullName}");
                }

                // Print stack trace for debugging
                Console.WriteLine($"  Stack trace: {ex.StackTrace}");

                Console.WriteLine("  Note: LiteSync may require the actual LiteSync native library to be available");
            }
        }

        static void TestMicrosoftDataSqlite()
        {
            Console.WriteLine("=== Testing Microsoft.Data.Sqlite ===");

            try
            {
                // Provider was set in static constructor before Microsoft.Data.Sqlite loads
                Console.WriteLine("✓ Using LiteSync provider (set before Microsoft.Data.Sqlite initialization)");

                // Create a test database with LiteSync URI
                var uri = "file:test_litesync_ms.db?node=primary&bind=tcp://0.0.0.0:1234";
                var connectionString = $"Data Source={uri}";

                using (var connection = new SqliteConnection(connectionString))
                {
                    try
                    {
                        connection.Open();
                        Console.WriteLine("  Connection opened successfully");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  Failed to open connection: {ex.Message}");
                        Console.WriteLine($"  Inner exception: {ex.InnerException?.Message}");
                        throw; // Re-throw to be caught by outer catch
                    }

                    // Check if database is ready (LiteSync specific)
                    var ready = false;
                    var attempts = 0;
                    const int maxAttempts = 10;

                    // First verify LiteSync is working by checking version
                    var versionCmd = connection.CreateCommand();
                    versionCmd.CommandText = "pragma litesync_version";
                    var version = versionCmd.ExecuteScalar()?.ToString();
                    if (string.IsNullOrEmpty(version))
                    {
                        throw new Exception("LiteSync version check failed: pragma litesync_version returned null or empty. LiteSync native library may not be properly loaded.");
                    }
                    Console.WriteLine($"✓ LiteSync version: {version}");

                    while (!ready && attempts < maxAttempts)
                    {
                        var cmd = connection.CreateCommand();
                        cmd.CommandText = "pragma sync_status";
                        var status = cmd.ExecuteScalar()?.ToString();

                        if (string.IsNullOrEmpty(status) || !status.TrimStart().StartsWith("{"))
                        {
                            throw new Exception($"Invalid sync_status response: expected JSON starting with '{{' but got '{status ?? "null"}'. LiteSync provider may not be properly configured.");
                        }

                        Console.WriteLine($"  Sync status: {status}");
                        if (status.Contains("\"db_is_ready\": true"))
                        {
                            ready = true;
                            Console.WriteLine("✓ LiteSync database is ready");
                        }

                        if (!ready)
                        {
                            attempts++;
                            System.Threading.Thread.Sleep(500);
                        }
                    }

                    if (!ready)
                    {
                        throw new Exception($"LiteSync database not ready after {maxAttempts} attempts. This indicates the LiteSync native library may not be available or properly configured.");
                    }

                    // Create a test table
                    var command = connection.CreateCommand();
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS test_ms_table (
                            id INTEGER PRIMARY KEY,
                            name TEXT NOT NULL,
                            sync_value INTEGER
                        )";
                    command.ExecuteNonQuery();

                    // Insert test data
                    command.CommandText = "INSERT INTO test_ms_table (name, sync_value) VALUES (@name, @value)";
                    command.Parameters.AddWithValue("@name", "Microsoft.Data.Sqlite Test Item");
                    command.Parameters.AddWithValue("@value", 789);
                    command.ExecuteNonQuery();

                    // Query the data
                    command.CommandText = "SELECT * FROM test_ms_table";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Console.WriteLine($"  Found: {reader.GetString(1)}, Sync Value: {reader.GetInt32(2)}");
                        }
                    }

                    Console.WriteLine("✓ Microsoft.Data.Sqlite basic operations completed successfully");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Microsoft.Data.Sqlite test failed: {ex.Message}");
                Console.WriteLine($"  Exception type: {ex.GetType().FullName}");

                if (ex.InnerException != null)
                {
                    Console.WriteLine($"  Inner exception: {ex.InnerException.Message}");
                    Console.WriteLine($"  Inner exception type: {ex.InnerException.GetType().FullName}");
                }

                // Print stack trace for debugging
                Console.WriteLine($"  Stack trace: {ex.StackTrace}");

                Console.WriteLine("  Note: LiteSync may require the actual LiteSync native library to be available");
            }
        }
    }
}
