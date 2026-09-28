using MySqlConnector;
using Dapper;

public class NoteService
{
    private readonly string _connectionString;

    public NoteService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IEnumerable<Note>?> GetAllNotes(int clientId,int userId)
    {
        using var connection = new MySqlConnection(_connectionString);
        if (!await ClientService.ClientBelongsToUser(connection, clientId, userId)) return null;
        return await connection.QueryAsync<Note>(
            "SELECT id AS Id, content AS Content, client_id AS ClientId,created_at AS CreatedAt FROM notes WHERE client_id = @ClientId ORDER BY created_at DESC",
            new{ClientId = clientId}
        );
    }
    public async Task<(CreateNoteResult Result, Note? Id)> CreateNote(int clientId, Note newNote, int userId)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        if (!await ClientService.ClientBelongsToUser(connection, clientId, userId)) return (CreateNoteResult.ClientNotFound, null);
        await connection.ExecuteAsync("INSERT INTO notes(client_id,content,created_at) VALUES(@ClientId,@Content,NOW())",
            new{ClientId = clientId,newNote.Content});
        var newId = await connection.QuerySingleAsync<int>("SELECT LAST_INSERT_ID();");
        var createdNote = await connection.QuerySingleAsync<Note>(
            "SELECT id AS Id, client_id AS ClientId, content AS Content, created_at AS CreatedAt FROM notes WHERE id = @Id",
            new { Id = newId });
        return (CreateNoteResult.Success,createdNote);
    }

}

public enum CreateNoteResult
{
    Success,
    ClientNotFound
}
    
