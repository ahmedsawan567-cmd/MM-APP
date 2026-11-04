using Microsoft.Extensions.Configuration;
using MMApplication.Infrastructure.Data;
using Npgsql;

namespace MMApplication.Tests;

public class DatabaseConnectionTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(v => v.Key, v => v.Value)).Build();

    [Fact]
    public void SupabaseUri_IsConvertedToNpgsqlFormat_WithSsl()
    {
        var cs = DatabaseConnection.Normalize("postgresql://postgres.abcd:p%40ss%3Aword@aws-0-eu-central-1.pooler.supabase.com:5432/postgres");
        var b = new NpgsqlConnectionStringBuilder(cs);

        Assert.Equal("aws-0-eu-central-1.pooler.supabase.com", b.Host);
        Assert.Equal(5432, b.Port);
        Assert.Equal("postgres", b.Database);
        Assert.Equal("postgres.abcd", b.Username);
        Assert.Equal("p@ss:word", b.Password);
        Assert.Equal(SslMode.Require, b.SslMode);
    }

    [Fact]
    public void TransactionPooler_DisablesPreparedStatements()
    {
        var b = new NpgsqlConnectionStringBuilder(DatabaseConnection.Normalize("Host=x.pooler.supabase.com;Port=6543;Username=u;Password=p;Database=postgres"));
        Assert.Equal(0, b.MaxAutoPrepare);
        Assert.True(b.NoResetOnClose);
    }

    [Fact]
    public void Localhost_KeepsSslSetting()
    {
        var b = new NpgsqlConnectionStringBuilder(DatabaseConnection.Normalize("Host=localhost;Username=u;Password=p;Database=d"));
        Assert.NotEqual(SslMode.Require, b.SslMode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Host=db.x.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=SET_VIA_USER_SECRETS_OR_ENV;")]
    [InlineData("Server=localhost;Database=MMAppDb;Trusted_Connection=True;")]
    public void WithoutRealConnectionString_UsesInMemory(string? connection)
    {
        var choice = DatabaseConnection.Resolve(Config(("DatabaseProvider", "Supabase"), ("ConnectionStrings:SupabaseConnection", connection)));
        Assert.False(choice.UsePostgres);
    }

    [Fact]
    public void WithRealConnectionString_UsesPostgres()
    {
        var choice = DatabaseConnection.Resolve(Config(
            ("DatabaseProvider", "Supabase"),
            ("ConnectionStrings:SupabaseConnection", "postgresql://postgres:secret@db.example.supabase.co:5432/postgres")));
        Assert.True(choice.UsePostgres);
        Assert.Contains("db.example.supabase.co", choice.Description);
        Assert.DoesNotContain("secret", choice.Description);
    }

    [Fact]
    public void InMemoryProvider_IsRespected()
    {
        var choice = DatabaseConnection.Resolve(Config(("DatabaseProvider", "InMemory"), ("ConnectionStrings:SupabaseConnection", "Host=h;Password=p")));
        Assert.False(choice.UsePostgres);
    }
}
