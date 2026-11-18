using AuthService.API;

var builder = WebApplication.CreateBuilder(args);
var app = AuthApplication.Build(builder);

app.Run();
