using MySqlConnector;
using Dapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);
var connectionString = "Server=localhost;Database=clientapi;User=root;Password=123456";

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddCors();

builder.Services.AddSingleton(new ClientService(connectionString));
builder.Services.AddSingleton(new PolicyService(connectionString));
builder.Services.AddSingleton(new UserService(connectionString));
builder.Services.AddSingleton(new NoteService(connectionString));
builder.Services.AddSingleton(new TaskService(connectionString));
builder.Services.AddSingleton(new ClaimService(connectionString));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(UserService.SecretKey))
        };
    });

builder.Services.AddAuthorization();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

var app = builder.Build();

app.UseCors(policy => policy
    .WithOrigins("http://localhost:5173")
    .AllowAnyMethod()
    .AllowAnyHeader());

app.UseAuthentication();
app.UseAuthorization();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

var clients = new List<Client>
    {
        new Client("Dana", "050-1234567"),
        new Client("Yossi", "052-7654321"),
        new Client("Noa", "054-1112222")
    };

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapGet("/clients", async (ClientService clientService, ClaimsPrincipal user, string? search) =>
{
    var userId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    if (!string.IsNullOrEmpty(search))
    {
        var filtered = await clientService.SearchClientsByName(search, userId);
        return Results.Ok(filtered);
    }

    var clients = await clientService.GetAllClients(userId);
    return Results.Ok(clients);
}).RequireAuthorization();

app.MapGet("/clients/{id}/policies", async (int id, PolicyService policyService, ClaimsPrincipal user) =>
{
    var userId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    var policies = await policyService.GetPoliciesByClientId(id, userId);
    if (policies == null) return Results.NotFound();
    return Results.Ok(policies);
})
.RequireAuthorization();

app.MapPost("/clients/{id}/policies", async (int id, Policy newPolicy, PolicyService policyService, ClaimsPrincipal user) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(newPolicy.Provider) ||
            string.IsNullOrWhiteSpace(newPolicy.PolicyType) ||
            newPolicy.Premium <= 0)
        {
            return Results.BadRequest("Provider, policy type, and a valid premium amount are required.");
        }

        var userId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var (result, newId) = await policyService.CreatePolicy(id, newPolicy, userId);

        if (result == CreatePolicyResult.ClientNotFound) return Results.NotFound();
        if (result == CreatePolicyResult.CarrierNotFound) return Results.BadRequest($"No carrier found named '{newPolicy.Provider}'.");

        newPolicy.ClientId = id;
        newPolicy.Id = newId!.Value;
        return Results.Created($"/clients/{id}/policies/{newId}", newPolicy);
    }
    catch (Exception)
    {
        return Results.Problem("An unexpected error occurred while adding the policy.");
    }
})
.RequireAuthorization();

app.MapGet("/clients/{id}", async (int id, ClientService clientService, ClaimsPrincipal user) =>
{
    var userId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    var client = await clientService.GetClientById(id, userId);
    if (client == null) return Results.NotFound();
    return Results.Ok(client);
})
.RequireAuthorization();

app.MapGet("/clients/{id}/notes", async (int id, NoteService noteService, ClaimsPrincipal user) =>
{
    var userId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    var notes = await noteService.GetAllNotes(id, userId);
    if (notes == null) return Results.NotFound();
    return Results.Ok(notes);
})
.RequireAuthorization();

app.MapPost("/clients/{id}/notes", async (int id, Note newNote, NoteService noteService, ClaimsPrincipal user) =>
{
    var userId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    var (result, note) = await noteService.CreateNote(id, newNote, userId);
    if (result == CreateNoteResult.ClientNotFound) return Results.NotFound();
    return Results.Created($"/clients/{id}/notes/{note!.Id}", note);
})
.RequireAuthorization();


app.MapPost("/clients/{id}/tasks", async (int id, FollowUpTask newTask, TaskService taskService, ClaimsPrincipal user) =>
{
    var userId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    var (result, task) = await taskService.CreateTask(id, newTask, userId);
    if (result == CreateTaskResult.ClientNotFound) return Results.NotFound();
    return Results.Created($"/clients/{id}/tasks/{task!.Id}", task);
})
.RequireAuthorization();

app.MapGet("/clients/{id}/tasks", async (int id, TaskService taskService, ClaimsPrincipal user) =>
{
    var userId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    var tasks = await taskService.GetAllTasks(id, userId);
    if (tasks == null) return Results.NotFound();
    return Results.Ok(tasks);
})
.RequireAuthorization();

app.MapPut("/tasks/{id}/completed", async (int id, TaskService taskService, ClaimsPrincipal user) =>
{
    var userId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    var (res, task) = await taskService.MarkTaskComplete(id, userId);
    if (res == EditTaskResult.NotFound) return Results.NotFound();
    return Results.Ok(task);
})
.RequireAuthorization();

app.MapPost("/clients", async (Client newClient, ClientService clientService, ClaimsPrincipal user) =>
{
    var userId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    var newId = await clientService.CreateClient(newClient, userId);
    newClient.Id = newId;
    return Results.Created($"/clients/{newId}", newClient);
})
.RequireAuthorization();

app.MapPut("/clients/{id}", async (int id, Client updatedClient, ClientService clientService, ClaimsPrincipal user) =>
{
    var userId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    var rowsAffected = await clientService.UpdateClient(id, updatedClient, userId);
    if (rowsAffected == 0) return Results.NotFound();

    updatedClient.Id = id;
    return Results.Ok(updatedClient);
})
.RequireAuthorization();

app.MapPut("/policies/{id}", async (int id, Policy updatePolicy, PolicyService policyService, ClaimsPrincipal user) =>
{
    var userId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    var result = await policyService.EditPolicy(id, updatePolicy, userId);

    if (result == EditPolicyResult.NotFound) return Results.NotFound();
    if (result == EditPolicyResult.CarrierNotFound) return Results.BadRequest($"No carrier found named '{updatePolicy.Provider}'.");

    updatePolicy.Id = id;
    return Results.Ok(updatePolicy);
}).RequireAuthorization();

app.MapDelete("/clients/{id}", async (int id, ClientService clientService, ClaimsPrincipal user) =>
{
    var userId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    var result = await clientService.DeleteClient(id, userId);

    return result switch
    {
        DeleteClientResult.NotFound => Results.NotFound(),
        DeleteClientResult.HasDependentPolicies => Results.Conflict("Cannot delete this client — they have existing policies. Delete their policies first."),
        _ => Results.Ok()
    };
})
.RequireAuthorization();

app.MapDelete("/policies/{id}", async (int id, PolicyService policyService, ClaimsPrincipal user) =>
{
    var userId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    var rowsAffected = await policyService.DeletePolicy(id, userId);
    if (rowsAffected == 0) return Results.NotFound();
    return Results.Ok();
})
.RequireAuthorization();

app.MapGet("/policies/renewals", async (PolicyService policyService, ClaimsPrincipal user) =>
{
    var userId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    var upcoming = await policyService.GetUpcomingRenewals(userId);
    return Results.Ok(upcoming);
})
.RequireAuthorization();

app.MapGet("policies/{id}/claims", async(int id,ClaimService claimService,ClaimsPrincipal user) =>
{
    var userId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    var claims = await claimService.GetClaimsByPolicyId(id,userId);
    return Results.Ok(claims);
}).RequireAuthorization();


app.MapPost("policies/{id}/claims", async(int id,InsuranceClaim newClaim,ClaimService claimService,ClaimsPrincipal user) =>
{
    var userId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    var (result,claim) = await claimService.CreateClaim(userId,newClaim,id);
    if (result == CreateClaimResult.PolicyNotFound) return Results.NotFound();
    return Results.Created($"/policies/{id}/claims/{claim!.Id}", claim);
}).RequireAuthorization();

app.MapGet("/carriers", async (PolicyService policyService) =>
{
    var carriers = await policyService.GetAllCarriers();
    return Results.Ok(carriers);
}).RequireAuthorization();

app.MapGet("/dashboard", async (ClientService clientService, PolicyService policyService, ClaimsPrincipal user) =>
{
    var userId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    var totalClients = clientService.GetTotalClientCount(userId);
    var totalPolicies = policyService.GetTotalPolicyCount(userId);
    var upcomingRenewals = policyService.GetUpcomingRenewals(userId);

    await Task.WhenAll(totalClients, totalPolicies, upcomingRenewals);

    var summary = new DashboardSummary
    {
        TotalClients = totalClients.Result,
        TotalPolicies = totalPolicies.Result,
        UpcomingRenewals = upcomingRenewals.Result
    };

    return Results.Ok(summary);
})
.RequireAuthorization();

app.MapPost("/register", async (UserService userService, RegisterRequest request) =>
{
    await userService.RegisterUser(request.Username, request.Password);
    return Results.Ok();
});

app.MapPost("/login", async (UserService userService, LoginRequest request) =>
{
    var user = await userService.ValidateLogin(request.Username, request.Password);
    if (user == null) return Results.Unauthorized();

    var token = userService.GenerateToken(user);
    return Results.Ok(new { token });
});

app.MapPut("/clients/{id}/status", async (int id, UpdateStatusRequest request, ClientService clientService, ClaimsPrincipal user) =>
{
    var userId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    var rowsAffected = await clientService.UpdateClientStatus(id, request.Status, userId);
    if (rowsAffected == 0) return Results.NotFound();
    return Results.Ok();
}).RequireAuthorization();



app.MapGet("/me", (ClaimsPrincipal user) =>
{
    var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var username = user.FindFirst(ClaimTypes.Name)?.Value;

    return Results.Ok(new { userId, username });
}).RequireAuthorization();

app.Run();


record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
