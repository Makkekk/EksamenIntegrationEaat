using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using RestaurantService.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHostedService<RestaurantConsumer>();

var app = builder.Build();

app.Run();