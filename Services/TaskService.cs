using MySqlConnector;
using Dapper;

public class TaskService
{
    private readonly string _connectionString;

    public TaskService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IEnumerable<FollowUpTask>?> GetAllTasks(int clientId, int userId)
    {
        using var connection = new MySqlConnection(_connectionString);
        if (!await ClientService.ClientBelongsToUser(connection, clientId, userId)) return null;
        return await connection.QueryAsync<FollowUpTask>(
            "SELECT id AS Id, content AS Content, client_id AS ClientId,due_date AS DueDate, completed AS Completed FROM tasks WHERE client_id = @ClientId ORDER BY due_date ASC",
            new { ClientId = clientId }
        );
    }
    public async Task<(CreateTaskResult Result, FollowUpTask? task)> CreateTask(int clientId, FollowUpTask newTask, int userId)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        if (!await ClientService.ClientBelongsToUser(connection, clientId, userId)) return (CreateTaskResult.ClientNotFound, null);
        await connection.ExecuteAsync("INSERT INTO tasks(client_id,content,due_date,completed) VALUES(@ClientId,@Content,@DueDate,@Completed)",
            new { ClientId = clientId, newTask.Content, newTask.DueDate, Completed = false });
        var newId = await connection.QuerySingleAsync<int>("SELECT LAST_INSERT_ID();");
        var createdTask = await connection.QuerySingleAsync<FollowUpTask>(
            "SELECT id AS Id, client_id AS ClientId, content AS Content, due_date AS DueDate, completed as Completed FROM tasks WHERE id = @Id",
            new { Id = newId });
        return (CreateTaskResult.Success, createdTask);
    }

    public async Task<(EditTaskResult Result, FollowUpTask? Task)> MarkTaskComplete(int id, int userId)
    {
        using var connection = new MySqlConnection(_connectionString);
        var rowsAffected = await connection.ExecuteAsync(
            "UPDATE tasks SET completed = @Completed " +
            "WHERE id = @Id AND client_id IN (SELECT id FROM clients WHERE user_id = @UserId)",
            new { Id = id, UserId = userId, Completed = true });
        if (rowsAffected == 0) return (EditTaskResult.NotFound, null);
        var updatedTask = await connection.QuerySingleAsync<FollowUpTask>(
            "SELECT id AS Id, client_id AS ClientId, content AS Content, due_date AS DueDate, completed AS Completed FROM tasks WHERE id = @Id",
            new { Id = id });
        return (EditTaskResult.Success,updatedTask);
    }



}

public enum CreateTaskResult
{
    Success,
    ClientNotFound
}

public enum EditTaskResult
{
    Success,
    NotFound
}

