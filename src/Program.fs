module WorktreeApi.App

open System.Text.Json
open System.Text.Json.Serialization
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Giraffe

// === Health Check ===
let healthCheck: HttpHandler =
    fun next ctx ->
        json
            {| status = "healthy"
               timestamp = System.DateTime.UtcNow |}
            next
            ctx

// === Route Composition ===
let webApp: HttpHandler =
    choose
        [ GET >=> route "/health" >=> healthCheck

          // === DOMAIN ROUTES ===
          WorktreeApi.Users.Handlers.routes
          WorktreeApi.Products.Handlers.routes
          WorktreeApi.Orders.Handlers.routes

          RequestErrors.NOT_FOUND "Not Found" ]

// === Server Configuration ===
let configureApp (app: IApplicationBuilder) = app.UseGiraffe webApp

let configureServices (services: IServiceCollection) =
    services.AddGiraffe() |> ignore
    let jsonOpts = JsonSerializerOptions(PropertyNameCaseInsensitive = true)
    services.AddSingleton<Giraffe.Json.ISerializer>(
        Giraffe.Json.FsharpFriendlySerializer(JsonFSharpOptions.Default(), jsonOpts)) |> ignore

[<EntryPoint>]
let main args =
    Host
        .CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(fun webHost ->
            webHost.Configure(configureApp).ConfigureServices(configureServices) |> ignore)
        .Build()
        .Run()

    0
