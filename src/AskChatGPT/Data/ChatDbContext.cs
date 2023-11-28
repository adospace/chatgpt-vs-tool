using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.LanguageServer.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.VisualStudio.Shell.ThreadedWaitDialogHelper;

namespace AskChatGPT.Data;

//public class ChatDbContext : DbContext
//{
//    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
//    {
//        optionsBuilder.UseSqlite($"Data Source={Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "chat.db")}");
//    }

//    public DbSet<ChatSession> Sessions { get; set; }

//    public DbSet<ChatMessage> Messages { get; set; }
//}

public class ChatDbRepository
{
    public async Task MigrateAsync()
    {
        using var connection = new SqliteConnection($"Data Source={Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "chat.db")}");

        await connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS "Sessions" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Sessions" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NULL,
                "Created" TEXT NOT NULL,
                "TimeStamp" TEXT NOT NULL
            )
            """);

        await connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS "Messages" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Messages" PRIMARY KEY AUTOINCREMENT,
                "SessionId" INTEGER NOT NULL,
                "Role" TEXT NULL,
                "Content" TEXT NULL,
                CONSTRAINT "FK_Messages_Sessions_SessionId" FOREIGN KEY ("SessionId") REFERENCES "Sessions" ("Id") ON DELETE CASCADE
            )
            """);

        await connection.ExecuteAsync("""
            CREATE INDEX IF NOT EXISTS "IX_Messages_SessionId" ON "Messages" ("SessionId")
            """);
    }

    public async Task<IEnumerable<ChatSession>> GetSessionsAsync()
    {
        using var connection = new SqliteConnection($"Data Source={Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "chat.db")}");
        return await connection.QueryAsync<ChatSession>("SELECT * FROM Sessions ORDER BY TimeStamp DESC;");
    }

    public async Task<IEnumerable<ChatMessage>> GetMessagesAsync(int sessionId)
    {
        using var connection = new SqliteConnection($"Data Source={Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "chat.db")}");
        return await connection.QueryAsync<ChatMessage>("SELECT * FROM Messages WHERE SessionId = @SessionId ORDER BY Id;", new { SessionId = sessionId });
    }

    public async Task InsertSessionsAsync(params ChatSession[] sessions)
    {
        using var connection = new SqliteConnection($"Data Source={Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "chat.db")}");
        await connection.ExecuteAsync("""
            INSERT INTO Sessions Name, Created, TimeStamp)
            VALUES (@Name, @Created, @TimeStamp)
        """, sessions);
    }

    public async Task InsertSessionAsync(ChatSession session)
    {
        //select last_insert_rowid()
        using var connection = new SqliteConnection($"Data Source={Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "chat.db")}");
        var id = await connection.ExecuteScalarAsync<int>("""
            INSERT INTO Sessions (Name, Created, TimeStamp)
            VALUES (@Name, @Created, @TimeStamp)
            RETURNING Id;
        """, session);

        session.Id = id;
    }

    public async Task UpdateSessionsAsync(params ChatSession[] sessions)
    {
        using var connection = new SqliteConnection($"Data Source={Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "chat.db")}");
        await connection.ExecuteAsync("""
        UPDATE Sessions 
        SET Id = @Id,
            Name = @Name,
            Created = @Created, 
            TimeStamp = @TimeStamp
        WHERE Id = @Id
        """, sessions);
    }

    public async Task InsertMessagesAsync(params ChatMessage[] messages)
    {
        using var connection = new SqliteConnection($"Data Source={Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "chat.db")}");
        await connection.ExecuteAsync("""
            INSERT INTO Messages (SessionId, Role, Content)
            VALUES (@SessionId, @Role, @Content)
        """, messages);
    }

    public async Task DeleteSessionsAsync(params int[] ids)
    {
        using var connection = new SqliteConnection($"Data Source={Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "chat.db")}");
        foreach (var id in ids)
        {
            await connection.ExecuteAsync("""
            DELETE FROM Sessions 
            WHERE Id = @id
        """, new { id });
        }
    }
    public async Task DeleteMessagesAsync(params int[] ids)
    {
        using var connection = new SqliteConnection($"Data Source={Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "chat.db")}");
        foreach (var id in ids)
        {
            await connection.ExecuteAsync("""
            DELETE FROM Messages 
            WHERE Id = @id
        """, new { id });
        }
    }
}

public class ChatSession
{
    public int Id { get; set; }

    public string Name { get; set; }

    //public List<ChatMessage> Messages { get; set; }

    public DateTime Created { get; set; }

    public DateTime TimeStamp { get; set; }
}

public class ChatMessage
{
    public int Id { get; set; }

    public int SessionId { get; set; }

    //public ChatSession Session { get; set; }

    public string Role { get; set; }

    public string Content {  get; set; }

}