module WorktreeApi.App

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
          // (각 worktree에서 여기에 route를 추가합니다)

          RequestErrors.NOT_FOUND "Not Found" ]

// === Server Configuration ===
let configureApp (app: IApplicationBuilder) = app.UseGiraffe webApp

let configureServices (services: IServiceCollection) = services.AddGiraffe() |> ignore

[<EntryPoint>]
let main args =
    Host
        .CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(fun webHost ->
            webHost.Configure(configureApp).ConfigureServices(configureServices) |> ignore)
        .Build()
        .Run()

    0
