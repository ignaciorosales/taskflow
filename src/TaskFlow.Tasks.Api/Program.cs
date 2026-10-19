using MongoDB.Driver;
using TaskFlow.Api.Common;
using TaskFlow.Application.Tasks;
using TaskFlow.Infrastructure;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

var jwt = JwtOptions.FromConfiguration(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration, jwt);
builder.Services.AddScoped<TaskService>();
builder.Services.AddTaskFlowApi(jwt.SigningKey(), jwt.Issuer, jwt.Audience);

var app = builder.Build();

await MongoSetup.ConfigureAsync(app.Services.GetRequiredService<IMongoDatabase>());

app.UseTaskFlowApi();
app.MapTaskFlowApi();

app.Run();
