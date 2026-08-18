using AppTemplate.Client;
using AppTemplate.Client.Modules.Authentication.Services;
using AppTemplate.Shared.Modules.Notes.Entities;
using BlazorBase.CRUD.Extensions;
using BlazorBase.User.Models;
using BlazorBase.User.Services;
using BlazorBase.User.Wasm;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseAddress = new Uri(builder.HostEnvironment.BaseAddress);

builder.Services.AddFluentUIComponents();
builder.Services.AddLocalization();

builder.Services.AddBlazorBaseUserClient();
builder.Services.AddBlazorBaseUserWasm(builder.HostEnvironment.BaseAddress);


builder.Services.AddHttpClient<IAuthService, AuthService>(client => client.BaseAddress = apiBaseAddress);

builder.Services.AddHttpClient<IUserService, UserService>(client => client.BaseAddress = apiBaseAddress)
    .AddHttpMessageHandler<AuthTokenHandler>();

builder.Services.AddBlazorBaseCrud();
builder.Services.AddBlazorBaseCrudComponents();
builder.Services.AddBlazorBaseCrudClient(
    client => client.BaseAddress = new Uri(apiBaseAddress, "api/base/"),
    configureHttpClientBuilder: httpClientBuilder => httpClientBuilder.AddHttpMessageHandler<AuthTokenHandler>());

builder.Services.AddBlazorBaseCrudClient(
    client => client.BaseAddress = new Uri(apiBaseAddress, "api/base/"),
    typeof(UserModel).Assembly,
    httpClientBuilder => httpClientBuilder.AddHttpMessageHandler<AuthTokenHandler>());

builder.Services.AddBlazorBaseCrudClient(
    client => client.BaseAddress = new Uri(apiBaseAddress, "api/base/"),
    typeof(Note).Assembly,
    httpClientBuilder => httpClientBuilder.AddHttpMessageHandler<AuthTokenHandler>());

builder.Services.AddBlazorBaseUserManagementInputs();
builder.Services.AddBlazorBaseUserRoleProvider<AppUserRoleProvider>();

var host = builder.Build();

await host.Services.UseBlazorBaseDevelopmentSessionAsync();

await host.RunAsync();
