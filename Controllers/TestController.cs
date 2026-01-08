using Microsoft.AspNetCore.Mvc;
using Backend.Models;
using Npgsql;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly string _connectionString;

    public TestController(IConfiguration configuration)
    {
        var rawConnectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? Environment.GetEnvironmentVariable("DATABASE_URL") 
            ?? throw new InvalidOperationException("Database connection string not found");
        
        // Convert PostgreSQL URL to Npgsql connection string format if needed
        _connectionString = ConvertPostgresUrlToConnectionString(rawConnectionString);
    }
    
    private string ConvertPostgresUrlToConnectionString(string connectionString)
    {
        // If it's already a connection string (not a URL), return as-is
        if (!connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) &&
            !connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }
        
        try
        {
            var uri = new Uri(connectionString);
            var builder = new System.Text.StringBuilder();
            
            // Extract components from URL
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : 5432;
            var database = uri.AbsolutePath.TrimStart('/');
            var username = Uri.UnescapeDataString(uri.UserInfo.Split(':')[0]);
            var password = uri.UserInfo.Contains(':') 
                ? Uri.UnescapeDataString(uri.UserInfo.Substring(uri.UserInfo.IndexOf(':') + 1))
                : "";
            
            // Build Npgsql connection string
            builder.Append($"Host={host};Port={port};Database={database};Username={username}");
            if (!string.IsNullOrEmpty(password))
            {
                builder.Append($";Password={password}");
            }
            
            // Parse query string for additional parameters (e.g., sslmode)
            var sslMode = "Require";
            if (!string.IsNullOrEmpty(uri.Query) && uri.Query.Length > 1)
            {
                var queryString = uri.Query.Substring(1); // Remove '?'
                var queryParams = queryString.Split('&');
                foreach (var param in queryParams)
                {
                    var parts = param.Split('=');
                    if (parts.Length == 2 && parts[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase))
                    {
                        sslMode = Uri.UnescapeDataString(parts[1]);
                        break;
                    }
                }
            }
            builder.Append($";SSL Mode={sslMode}");
            
            return builder.ToString();
        }
        catch
        {
            // If parsing fails, return original (Npgsql might handle it)
            return connectionString;
        }
    }

    // GET: api/test
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TestProjects>>> GetAll()
    {
        var projects = new List<TestProjects>();
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var quote = Convert.ToChar(34).ToString(); // Double quote for PostgreSQL identifier quoting
        var sql = "SELECT " + quote + "Id" + quote + ", " + quote + "Name" + quote + " FROM " + quote + "TestProjects" + quote + " ORDER BY " + quote + "Id" + quote + " ";
        using var cmd = new NpgsqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            projects.Add(new TestProjects
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1)
            });
        }
        return Ok(projects);
    }

    // GET: api/test/5
    [HttpGet("{id}")]
    public async Task<ActionResult<TestProjects>> Get(int id)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var quote = Convert.ToChar(34).ToString(); // Double quote for PostgreSQL identifier quoting
        var sql = "SELECT " + quote + "Id" + quote + ", " + quote + "Name" + quote + " FROM " + quote + "TestProjects" + quote + " WHERE " + quote + "Id" + quote + " = @id ";
        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return Ok(new TestProjects
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1)
            });
        }
        return NotFound();
    }

    // POST: api/test
    [HttpPost]
    public async Task<ActionResult<TestProjects>> Create([FromBody] TestProjects project)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var quote = Convert.ToChar(34).ToString(); // Double quote for PostgreSQL identifier quoting
        var sql = "INSERT INTO " + quote + "TestProjects" + quote + " (" + quote + "Name" + quote + ") VALUES (@name) RETURNING " + quote + "Id" + quote + " ";
        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("name", project.Name);
        var id = await cmd.ExecuteScalarAsync();
        project.Id = Convert.ToInt32(id);
        return CreatedAtAction(nameof(Get), new { id = project.Id }, project);
    }

    // PUT: api/test/5
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] TestProjects project)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var quote = Convert.ToChar(34).ToString(); // Double quote for PostgreSQL identifier quoting
        var sql = "UPDATE " + quote + "TestProjects" + quote + " SET " + quote + "Name" + quote + " = @name WHERE " + quote + "Id" + quote + " = @id ";
        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("name", project.Name);
        cmd.Parameters.AddWithValue("id", id);
        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        if (rowsAffected == 0) return NotFound();
        return NoContent();
    }

    // DELETE: api/test/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var quote = Convert.ToChar(34).ToString(); // Double quote for PostgreSQL identifier quoting
        var sql = "DELETE FROM " + quote + "TestProjects" + quote + " WHERE " + quote + "Id" + quote + " = @id ";
        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        if (rowsAffected == 0) return NotFound();
        return NoContent();
    }
}
